using System.Collections.Generic;
using UnityEngine;

// Drives the screen-space reticle and world-space NPC name tags.
// Sits on the same GameObject as FirstPersonCamera (added by PlayerManager).
//
// Reticle contexts:
//   Default   small white square (spell aim, general look)
//   Talk      golden — within talk range of a live, talkable NPC
//   Interact  cyan   — within range of an interactable prop
//
// When the reticle is in Talk or Interact context, AttackPressed triggers
// the interaction instead of combat. PlayerCombat checks HasTarget to skip
// its own attack logic.
public enum ReticleContext { Default, Talk, Interact }

[RequireComponent(typeof(Camera))]
public class InteractionHUD : MonoBehaviour
{
    // True whenever Talk or Interact context is active this frame.
    // PlayerCombat reads this to suppress the melee attack.
    public static bool HasTarget { get; private set; }

    [Header("Ranges")]
    public float talkRange      = 3f;
    public float tradeRange     = 3f;
    public float lootRange      = 2.5f;
    public float containerRange = 3f;
    public float nameTagRange   = 20f;
    [Tooltip("Max distance the aim-raycast probes for an interactable. Should be ≥ the largest range above.")]
    public float maxInteractDistance = 6f;

    [Header("Reticle")]
    public float reticleHalf = 9f;

    [Header("Name Tags")]
    public float nameTagHeadOffset = 1.15f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    Camera           _cam;
    ReticleContext   _context;
    GUIStyle         _tagStyle;

    NpcController    _talkTarget;
    NpcController    _corpseTarget;
    Merchant         _merchantTarget;
    LootContainer    _containerTarget;
    InteractableProp _propTarget;
    Gateway          _gatewayTarget;

    struct LabelEntry
    {
        public string text;
        public float  guiX, guiY, alpha;
        public bool   isDead;
    }

    readonly List<LabelEntry> _labels = new();

    // ── Colours ───────────────────────────────────────────────────────────────

    static readonly Color ReticleDefault  = new(1f,    1f,    1f,    0.45f);
    static readonly Color ReticleTalk     = new(1f,    0.88f, 0.25f, 0.90f);
    static readonly Color ReticleInteract = new(0.35f, 0.95f, 0.95f, 0.90f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake() => _cam = GetComponent<Camera>();

    void Update()
    {
        HasTarget        = false;
        _talkTarget      = null;
        _corpseTarget    = null;
        _merchantTarget  = null;
        _containerTarget = null;
        _propTarget      = null;
        _gatewayTarget   = null;
        _labels.Clear();
        _context = ReticleContext.Default;

        if (FirstPersonCamera.Active == null) return;
        // A modal window (loot, shop) owns the cursor — don't target or click through it.
        if (UIModal.IsOpen) return;

        ResolveAimTarget();   // raycast under the crosshair → sets the active target
        ScanProps();          // discovery / active highlights
        ScanGateways();       // interact-mode gateways (proximity)
        ScanNPCs();           // name tags only

        // Resolve priority: prop > container > corpse loot > merchant > gateway > talk.
        if (_propTarget != null || _containerTarget != null
            || _corpseTarget != null || _merchantTarget != null || _gatewayTarget != null)
        {
            _context  = ReticleContext.Interact;
            HasTarget = true;
        }
        else if (_talkTarget != null)
        {
            _context  = ReticleContext.Talk;
            HasTarget = true;
        }

        // The talk / trade target turns to look at the player.
        Transform player = PlayerManager.Instance?.Player != null
            ? PlayerManager.Instance.Player.transform : null;
        if (player != null)
        {
            NpcController attend = _merchantTarget != null
                ? _merchantTarget.GetComponent<NpcController>()
                : _talkTarget;
            attend?.AttendTo(player);
        }

        // Left-click triggers interaction when a target is active.
        if (HasTarget && InputManager.AttackPressed && !PauseMenuController.IsPaused && !GameUI.IsOpen)
        {
            if (_propTarget != null)
                _propTarget.Interact();
            else if (_containerTarget != null)
                _containerTarget.OpenLoot();
            else if (_corpseTarget != null)
                _corpseTarget.OpenLoot();
            else if (_merchantTarget != null)
                _merchantTarget.Open();
            else if (_gatewayTarget != null)
                _gatewayTarget.Activate();
            else if (_talkTarget != null)
                HandleTalkInteract(_talkTarget);
        }
    }

    // Interact-mode gateways use simple proximity targeting (closest in range),
    // mirroring how props are handled.
    void ScanGateways()
    {
        Vector3 camPos     = _cam.transform.position;
        float   closest    = float.MaxValue;

        foreach (var gw in Gateway.All)
        {
            if (gw == null || gw.trigger != GatewayTrigger.Interact) continue;
            float dist = Vector3.Distance(camPos, gw.transform.position);
            if (dist <= gw.interactionRange && dist < closest)
            {
                closest        = dist;
                _gatewayTarget = gw;
            }
        }
    }

    // Casts a ray through the crosshair to find the NPC under it — covering the
    // whole body (the NPC capsule) at any distance up to the relevant range.
    // Props are handled separately by proximity (ScanProps); a prop or wall hit
    // here simply blocks line-of-sight to an NPC behind it.
    void ResolveAimTarget()
    {
        var ray = new Ray(_cam.transform.position, _cam.transform.forward);
        var hits = Physics.RaycastAll(ray, maxInteractDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform playerRoot = PlayerManager.Instance?.Player != null
            ? PlayerManager.Instance.Player.transform : null;

        foreach (var h in hits)
        {
            // Skip the player's own colliders (ray starts inside them).
            if (playerRoot != null && h.collider.transform.IsChildOf(playerRoot)) continue;

            var container = h.collider.GetComponentInParent<LootContainer>();
            if (container != null)
            {
                if (container.CanLoot && h.distance <= containerRange) _containerTarget = container;
                return;
            }

            var npc = h.collider.GetComponentInParent<NpcController>();
            if (npc != null)
            {
                if (npc.IsDead)
                {
                    if (npc.CanLoot && h.distance <= lootRange) _corpseTarget = npc;
                }
                else
                {
                    var merchant = npc.GetComponent<Merchant>();
                    // A hostile/in-combat NPC isn't talkable or tradeable — fall through
                    // to the default reticle so left-click attacks it instead.
                    if (npc.IsInCombat)
                    {
                        // no target set → regular reticle, melee handles the click
                    }
                    else if (merchant != null && merchant.CanTrade && h.distance <= tradeRange)
                        _merchantTarget = merchant;
                    else if (npc.canTalk && h.distance <= talkRange)
                        _talkTarget = npc;
                }
                return;
            }

            // First solid hit is geometry or a prop — it blocks any NPC behind it.
            return;
        }
    }

    // ── Scanning ──────────────────────────────────────────────────────────────

    // Builds floating name tags for nearby NPCs (display only — targeting is
    // handled by the aim-raycast in ResolveAimTarget).
    void ScanNPCs()
    {
        Vector3 camPos = _cam.transform.position;

        foreach (var npc in NpcController.All)
        {
            if (npc == null) continue;

            float dist = Vector3.Distance(camPos, npc.transform.position);
            if (dist > nameTagRange) continue;

            Vector3 headWorld = npc.transform.position + Vector3.up * nameTagHeadOffset;
            Vector3 screen    = _cam.WorldToScreenPoint(headWorld);
            if (screen.z <= 0f) continue;
            if (screen.x < 0 || screen.x > Screen.width ||
                screen.y < 0 || screen.y > Screen.height) continue;

            // Non-hostile NPCs only show their name when the player can actually
            // see them (no name tags through walls). Hostile ones always show.
            if (!npc.IsInCombat && IsOccluded(camPos, headWorld, npc)) continue;

            bool isDead = npc.IsDead;

            string label = npc.DisplayName;
            if (isDead) label += " (Dead)";

            float fade  = Mathf.InverseLerp(nameTagRange, nameTagRange * 0.5f, dist);
            float alpha = Mathf.Lerp(0.4f, 1f, fade);

            _labels.Add(new LabelEntry
            {
                text   = label,
                guiX   = screen.x,
                guiY   = Screen.height - screen.y,
                alpha  = alpha,
                isDead = isDead,
            });
        }

        _labels.Sort((a, b) => b.alpha.CompareTo(a.alpha));
    }

    // True if solid geometry sits between the camera and the NPC's head — i.e. the
    // player can't actually see it. Ignores the player's own and the NPC's colliders.
    static bool IsOccluded(Vector3 from, Vector3 to, NpcController npc)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;

        var hits = Physics.RaycastAll(from, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform playerRoot = PlayerManager.Instance?.Player != null
            ? PlayerManager.Instance.Player.transform : null;

        foreach (var h in hits)
        {
            if (playerRoot != null && h.collider.transform.IsChildOf(playerRoot)) continue; // ignore self
            if (h.collider.transform.IsChildOf(npc.transform)) return false;                // reached the NPC → visible
            return true;                                                                    // something blocks first
        }
        return false;
    }

    // Props use proximity targeting (closest interactable in range) plus a
    // silver "discovered" highlight at discovery range and gold when active.
    void ScanProps()
    {
        Vector3 camPos      = _cam.transform.position;
        float   closestProp = float.MaxValue;

        foreach (var prop in InteractableProp.All)
        {
            if (prop == null) continue;

            float dist = Vector3.Distance(camPos, prop.InteractionPoint);
            if (dist <= prop.discoveryRange && prop.IsInteractable)
            {
                prop.SetHighlight(PropHighlightState.Discovered);
                if (dist <= prop.interactionRange && dist < closestProp)
                {
                    closestProp = dist;
                    _propTarget = prop;
                }
            }
            else
            {
                prop.SetHighlight(PropHighlightState.None);
            }
        }

        if (_propTarget != null)
            _propTarget.SetHighlight(PropHighlightState.Active);
    }

    // ── Talk interaction ──────────────────────────────────────────────────────

    static void HandleTalkInteract(NpcController npc)
    {
        // Corruption gate: a corrupted player may be refused or attacked instead.
        if (npc.CorruptionBlocksInteraction()) return;

        QuestSystem.Instance?.ReportTalkToNpc(npc.saveId);   // progress TalkToNpc objectives

        // Delegate to any NPCBase component on the same GameObject
        // (DialogueController, QuestGiver, etc.).
        var npcBase = npc.GetComponent<NPCBase>();
        if (npcBase != null) { npcBase.Interact(); return; }

        // Fallback: just log the greeting if the NPC has one.
        string greeting = npc.greetingLine;
        if (!string.IsNullOrEmpty(greeting))
            Debug.Log($"[{npc.DisplayName}] {greeting}");
    }

    // ── IMGUI ─────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (FirstPersonCamera.Active == null) return;
        if (PauseMenuController.IsPaused) return;
        if (GameUI.IsOpen || UIModal.IsOpen) return;

        EnsureStyle();
        DrawReticle();
        DrawNameTags();
    }

    void DrawReticle()
    {
        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;
        float h  = reticleHalf;

        Color col = _context switch
        {
            ReticleContext.Talk     => ReticleTalk,
            ReticleContext.Interact => ReticleInteract,
            _                      => ReticleDefault,
        };

        GUI.color = col;
        GUI.DrawTexture(new Rect(cx - h,     cy - h,     h * 2f, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - h,     cy + h - 1, h * 2f, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - h,     cy - h,     1f, h * 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + h - 1, cy - h,     1f, h * 2f), Texture2D.whiteTexture);

        if (_context != ReticleContext.Default)
        {
            string contextLabel = _context == ReticleContext.Talk
                ? "Talk"
                : _propTarget != null ? _propTarget.Label
                : _containerTarget != null ? _containerTarget.Label
                : _corpseTarget != null ? "Loot"
                : _merchantTarget != null ? "Trade"
                : _gatewayTarget != null ? _gatewayTarget.Label
                : "Interact";

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = col;
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 50f, cy + h + 5f, 100f, 18f), contextLabel, style);
        }

        GUI.color = Color.white;
    }

    void DrawNameTags()
    {
        const float padX = 8f, padY = 3f, gap = 3f;
        var placed = new List<Rect>(_labels.Count);

        foreach (var entry in _labels)
        {
            Vector2 textSize = _tagStyle.CalcSize(new GUIContent(entry.text));
            float   w        = textSize.x + padX * 2f;
            float   h        = textSize.y + padY * 2f;

            var rect     = new Rect(entry.guiX - w * 0.5f, entry.guiY - h, w, h);
            bool overlapped = true;
            int  safety     = 0;
            while (overlapped && safety++ < 30)
            {
                overlapped = false;
                foreach (var p in placed)
                {
                    if (!rect.Overlaps(p)) continue;
                    rect.y     = p.y - h - gap;
                    overlapped = true;
                    break;
                }
            }

            placed.Add(rect);

            GUI.color = new Color(0f, 0f, 0f, 0.55f * entry.alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            _tagStyle.normal.textColor = entry.isDead
                ? new Color(0.65f, 0.65f, 0.65f, entry.alpha)
                : new Color(1f,    1f,    1f,    entry.alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + padX, rect.y + padY,
                               rect.width - padX * 2f, rect.height - padY * 2f),
                      entry.text, _tagStyle);
        }

        GUI.color = Color.white;
    }

    void EnsureStyle()
    {
        if (_tagStyle != null) return;
        _tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        _tagStyle.normal.textColor = Color.white;
    }
}
