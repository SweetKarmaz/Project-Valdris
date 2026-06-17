using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

// Core NPC component. Attach to every character prefab placed in the editor.
// Drag an NpcDefinition onto the 'definition' slot to give the character its
// archetype defaults. Most archetype values can be overridden per placed NPC
// via the "Override …" toggles (see NpcControllerEditor). Items, story flags,
// and dialogue are per-instance and live only here.
[DefaultExecutionOrder(-5)]  // before NpcAppearanceComponent.Start so restore pre-empts randomize
[RequireComponent(typeof(NavMeshAgent))]
public class NpcController : MonoBehaviour, IDamageable
{
    public static readonly List<NpcController> All = new();

    [Header("Class")]
    [Tooltip("ScriptableObject archetype. Supplies default values; most can be overridden below.")]
    public NpcDefinition definition;

    [Header("Instance")]
    [Tooltip("Leave blank to use the definition's display name.")]
    public string displayNameOverride;
    [Tooltip("Unique ID used to match this NPC to its saved state. Must be unique within the scene.")]
    public string saveId;

    [Header("Inventory & Loot")]
    [Tooltip("This NPC's carried items — set per placed character. Feeds combat (thrown-weapon " +
             "stock) and corpse loot on death. Each entry: a LootItem prefab, quantity, dropChance.")]
    public List<NpcItem> items = new();

    [Header("Story (per-instance)")]
    [Tooltip("All of these world flags must be true for this NPC to be active. Checked on Start.")]
    public string[] requiredWorldFlags;
    [Tooltip("These world flags are set to true when this NPC dies.")]
    public string[] setsWorldFlagsOnDeath;

    [Header("Dialogue (per-instance)")]
    public bool   canTalk;
    public string greetingLine;

    [Header("Attention (look at player during talk/trade)")]
    [Tooltip("Half-angle of the frontal cone (deg). Player inside it → head-look only; outside → turn body.")]
    public float attentionConeHalfAngle = 70f;
    [Tooltip("Body turn speed (deg/sec) when the player is outside the cone.")]
    public float bodyTurnSpeed = 220f;
    [Range(0f, 1f), Tooltip("How far the head turns toward the player (1 = fully).")]
    public float headLookWeight = 0.7f;
    [Tooltip("Head turn smoothing speed (higher = snappier).")]
    public float headLookLerp = 7f;
    [Tooltip("Rotation offset to align the head bone's forward axis with the look direction. " +
             "Tune per rig if the head faces the wrong way (Synty rigs often need e.g. (-90,0,0) or (0,90,0)).")]
    public Vector3 headForwardOffsetEuler = Vector3.zero;

    // ── Overrides (default from Definition) ───────────────────────────────────
    // Each pair: an "override" toggle + the value used when the toggle is on.
    // NpcControllerEditor hides the value unless the toggle is enabled.

    [HideInInspector] public bool overrideFaction;             public NpcFaction faction = NpcFaction.None;
    [HideInInspector] public bool overrideIsEssential;         public bool isEssential;

    [HideInInspector] public bool overrideLevel;               public int   level = 1;
    [HideInInspector] public bool overrideMaxHealth;           public float maxHealth = 100f;
    [HideInInspector] public bool overrideMaxMana;             public float maxMana = 0f;
    [HideInInspector] public bool overrideBaseArmor;           public float baseArmor = 0f;
    [HideInInspector] public bool overrideXpReward;            public int   xpReward = 10;

    [HideInInspector] public bool overrideBehaviorType;        public NpcBehaviorType behaviorType = NpcBehaviorType.Wander;
    [HideInInspector] public bool overrideWalkSpeed;           public float walkSpeed = 1.5f;
    [HideInInspector] public bool overrideRunSpeed;            public float runSpeed = 4f;
    [HideInInspector] public bool overrideAllowedZones;        public string[] allowedZones = new string[0];
    [HideInInspector] public bool overrideIdleSecondsMin;      public float idleSecondsMin = 2f;
    [HideInInspector] public bool overrideIdleSecondsMax;      public float idleSecondsMax = 8f;

    [HideInInspector] public bool overrideAttackPriority;      public AttackType[] attackPriority = { AttackType.Melee };

    [HideInInspector] public bool overrideWitnessedKillReaction; public WitnessedKillReaction witnessedKillReaction = WitnessedKillReaction.None;

    [HideInInspector] public bool overrideKnownSpells;         public List<SpellData> knownSpells = new();
    [HideInInspector] public bool overrideManaRegenRate;       public float manaRegenRate = 1f;

    // ── Effective accessors (override → definition → hardcoded fallback) ───────

    public NpcFaction      Faction       => overrideFaction       ? faction       : definition != null ? definition.faction       : NpcFaction.None;
    public bool            IsEssential   => overrideIsEssential   ? isEssential   : definition != null && definition.isEssential;
    public int             Level         => overrideLevel         ? level         : definition != null ? definition.level         : 1;
    public float           MaxHealth     => overrideMaxHealth     ? maxHealth     : definition != null ? definition.maxHealth     : 100f;
    public float           MaxMana       => overrideMaxMana       ? maxMana       : definition != null ? definition.maxMana       : 0f;
    public float           BaseArmor     => overrideBaseArmor     ? baseArmor     : definition != null ? definition.baseArmor     : 0f;
    public int             XpReward      => overrideXpReward      ? xpReward      : definition != null ? definition.xpReward      : 10;
    public NpcBehaviorType BehaviorType  => overrideBehaviorType  ? behaviorType  : definition != null ? definition.behaviorType  : NpcBehaviorType.Wander;
    public float           WalkSpeed     => overrideWalkSpeed     ? walkSpeed     : definition != null ? definition.walkSpeed     : 1.5f;
    public float           RunSpeed      => overrideRunSpeed      ? runSpeed      : definition != null ? definition.runSpeed      : 4f;
    public string[]        AllowedZones  => overrideAllowedZones  ? allowedZones  : definition != null ? definition.allowedZones  : System.Array.Empty<string>();
    public float           IdleSecondsMin => overrideIdleSecondsMin ? idleSecondsMin : definition != null ? definition.idleSecondsMin : 2f;
    public float           IdleSecondsMax => overrideIdleSecondsMax ? idleSecondsMax : definition != null ? definition.idleSecondsMax : 8f;
    public AttackType[]    AttackPriority => overrideAttackPriority ? attackPriority : definition != null ? definition.attackPriority : new[] { AttackType.Melee };
    public WitnessedKillReaction WitnessedKillReaction => overrideWitnessedKillReaction ? witnessedKillReaction : definition != null ? definition.witnessedKillReaction : WitnessedKillReaction.None;
    public List<SpellData> KnownSpells   => overrideKnownSpells   ? knownSpells   : definition != null ? definition.knownSpells   : new List<SpellData>();
    public float           ManaRegenRate => overrideManaRegenRate ? manaRegenRate : definition != null ? definition.manaRegenRate : 1f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    public string DisplayName => !string.IsNullOrEmpty(displayNameOverride)
        ? displayNameOverride
        : definition != null ? definition.displayName : gameObject.name;

    float _health;
    float _mana;
    NavMeshAgent _agent;
    CharacterBuffs _buffs;
    CharacterAnimator _charAnim;
    float _idleUntil;
    bool _wasMovementPrevented;

    // Attention: the talk/trade target looks at the player. Set each frame by
    // InteractionHUD via AttendTo(); expires shortly after to avoid flicker.
    Transform _attendTarget;
    float     _attendUntil;
    Transform _headBone;
    float     _headBlend;   // 0→1 ramp for how far the head is turned

    bool Attending => _attendTarget != null && Time.time <= _attendUntil && !IsDead;

    // Called by InteractionHUD while this NPC is the active talk/trade target.
    public void AttendTo(Transform target)
    {
        _attendTarget = target;
        _attendUntil  = Time.time + 0.25f;   // grace so it doesn't flicker frame-to-frame
    }

    // Finds the skeleton head bone. Synty rigs usually name it "Head", but match
    // defensively: prefer an exact "Head", else a transform whose name contains
    // "head" that isn't a mesh-group/attachment node.
    Transform FindHeadBone()
    {
        Transform fuzzy = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Head") return t;
            string n = t.name.ToLowerInvariant();
            if (fuzzy == null && n.Contains("head")
                && !n.Contains("covering") && !n.Contains("attachment")
                && !n.Contains("hair") && !n.Contains("element") && !n.Contains("gear"))
                fuzzy = t;
        }
        return fuzzy;
    }

    // Runtime thrown-weapon stock. Populated from this NPC's instance items for
    // any Projectile-type LootItem. Consumed one-per-throw during combat.
    readonly Dictionary<LootItem, int> _thrownStock = new();

    // ── Corpse loot ───────────────────────────────────────────────────────────
    // Rolled once on death from this NPC's instance items (dropChance) + the
    // definition's goldMin/Max.
    readonly Inventory _corpseLoot = new(100000);  // effectively uncapped corpse
    int     _corpseGold;
    bool    _lootRolled;
    bool    _looted;
    bool    _lootOpen;
    Vector2 _lootScroll;

    public bool IsDead => _charAnim != null && _charAnim.IsDead;

    // A corpse the player can still loot: dead, flagged lootable, and not empty.
    public bool CanLoot =>
        IsDead
        && (definition == null || definition.isLootable)
        && (_corpseLoot.SlotCount > 0 || _corpseGold > 0);

    public void OpenLoot()
    {
        if (CanLoot && !_lootOpen) { _lootOpen = true; UIModal.Push(); }
    }

    void CloseLoot()
    {
        if (!_lootOpen) return;
        _lootOpen = false;
        UIModal.Pop();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        All.Add(this);

        _health = MaxHealth;
        _mana   = MaxMana;

        _agent = GetComponent<NavMeshAgent>();
        _agent.speed                  = WalkSpeed;
        _agent.angularSpeed           = 360f;
        _agent.acceleration           = 8f;
        _agent.stoppingDistance       = 0.5f;
        _agent.radius                 = 0.4f;
        _agent.height                 = 1.9f;
        _agent.autoTraverseOffMeshLink = true;

        var col = gameObject.AddComponent<CapsuleCollider>();
        col.height = 1.9f; col.radius = 0.4f; col.center = new Vector3(0, 0.95f, 0);

        _buffs = gameObject.AddComponent<CharacterBuffs>();
        gameObject.AddComponent<CharacterResistances>();
        _charAnim = gameObject.AddComponent<CharacterAnimator>();

        _headBone = FindHeadBone();
        if (_headBone == null)
            Debug.LogWarning($"[NpcController] No head bone found on '{name}' — head-look disabled. " +
                "Check the rig's bone names.", this);

        // Populate thrown-weapon stock from this NPC's instance items.
        if (items != null)
            foreach (var npcItem in items)
                if (npcItem.lootItem != null &&
                    npcItem.lootItem.itemType == LootItemType.Projectile)
                    _thrownStock[npcItem.lootItem] = npcItem.quantity;

        _idleUntil = Time.time + Random.Range(0f, IdleSecondsMax);
    }

    void Start()
    {
        // Disable this NPC if any required world flag is not set.
        if (requiredWorldFlags != null)
            foreach (string flag in requiredWorldFlags)
                if (WorldStateSystem.Instance != null && !WorldStateSystem.Instance.GetFlag(flag))
                {
                    gameObject.SetActive(false);
                    return;
                }

        // Self-register so SceneStateManager can save/restore this NPC's state.
        SceneStateManager.Instance?.RegisterNPC(this);

        // Always-aggressive NPCs (guards, bandits) draw their weapon up front.
        // Other triggers (being attacked, aggro, witnessed kills) call EnterCombat
        // when their systems are in place; being attacked already does (TakeDamage).
        if (!IsDead && definition != null && definition.isAggressive)
            EnterCombat();
    }

    void OnDestroy()
    {
        All.Remove(this);
        SceneStateManager.Instance?.UnregisterNPC(this);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_charAnim.IsDead) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        if (PauseMenuController.IsPaused)
        {
            _agent.isStopped = true;
            return;
        }

        // Pause wandering while attending to the player (the look-at runs in LateUpdate).
        if (Attending)
        {
            _agent.isStopped = true;
            _charAnim.SetSpeed(0f);
            return;
        }

        bool movePrevented = _buffs != null && _buffs.IsMovementPrevented;
        if (movePrevented)
        {
            if (!_wasMovementPrevented) { _agent.isStopped = true; _wasMovementPrevented = true; }
            _charAnim.SetSpeed(0f);
            return;
        }

        if (_wasMovementPrevented)
        {
            _agent.isStopped      = false;
            _wasMovementPrevented = false;
            _idleUntil = Time.time + Random.Range(IdleSecondsMin, IdleSecondsMax);
        }

        _agent.isStopped = false;

        float walkSpeed = WalkSpeed;
        _charAnim.SetSpeed(_agent.hasPath ? _agent.velocity.magnitude / walkSpeed : 0f);

        bool arrived = !_agent.pathPending
            && _agent.remainingDistance <= _agent.stoppingDistance
            && (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f);

        if (arrived)
        {
            if (_idleUntil == float.MaxValue) OnArrived();
            else if (Time.time >= _idleUntil)  PickDestination();
        }
    }

    // ── Attention look-at ─────────────────────────────────────────────────────
    // Runs after the Animator so it can override the head pose. When the player
    // is inside the frontal cone the NPC keeps its body and just turns its head;
    // outside the cone it rotates its whole body to face the player.
    void LateUpdate()
    {
        if (!Attending) { _headBlend = 0f; return; }

        Vector3 flat = _attendTarget.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.SignedAngle(transform.forward, flat, Vector3.up);
            if (Mathf.Abs(angle) > attentionConeHalfAngle)
            {
                Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, bodyTurnSpeed * Time.deltaTime);
            }
        }

        if (_headBone != null && headLookWeight > 0f)
        {
            // Ramp the blend in smoothly, then apply it as a fixed blend from the
            // animated pose toward the look target (NOT a per-frame slerp step —
            // the Animator overwrites the bone each frame, so a tiny step would
            // never accumulate and the head would look frozen).
            _headBlend = Mathf.MoveTowards(_headBlend, 1f, Time.deltaTime * headLookLerp);

            Vector3 headTo = (_attendTarget.position + Vector3.up * 1.4f) - _headBone.position;
            if (headTo.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(headTo.normalized, Vector3.up)
                                     * Quaternion.Euler(headForwardOffsetEuler);
                _headBone.rotation = Quaternion.Slerp(
                    _headBone.rotation, desired, headLookWeight * _headBlend);
            }
        }
    }

    // ── Combat ────────────────────────────────────────────────────────────────

    public void TakeDamage(float amount) =>
        TakeDamage(new DamageInfo(amount, DamageType.Physical));

    public void TakeDamage(DamageInfo info)
    {
        if (_charAnim.IsDead) return;

        float damage = info.type == DamageType.Physical
            ? Mathf.Max(0f, info.amount - BaseArmor)
            : Mathf.Max(0f, info.amount);
        _health = Mathf.Max(0f, _health - damage);

        CombatTextSystem.Instance?.ShowDamage(transform, damage, info.isCrit, info.type);
        _buffs?.NotifyDamageTaken();

        if (_health <= 0f)
        {
            _agent.isStopped = true;
            _agent.enabled   = false;
            _charAnim.TriggerDeath();
            ExitCombat();          // put the weapon/shield away on death
            RollCorpseLoot();
            FireDeathFlags();
            ReportKillToQuestSystem();
        }
        else
        {
            _charAnim.TriggerHit();
            EnterCombat();   // being attacked makes the NPC draw a weapon
        }
    }

    // ── Combat entry / weapon selection ───────────────────────────────────────

    const string RightHandBone = "Hand_R";
    const string LeftHandBone  = "Hand_L";

    bool       _inCombat;
    LootItem   _equippedWeapon;
    GameObject _weaponMount;
    GameObject _shieldMount;

    public LootItem EquippedWeapon => _equippedWeapon;

    // Call when the NPC decides to fight (attacked, aggroed, witnessed a kill it
    // reacts to, etc.). Draws and shows the best weapon it can currently use.
    public void EnterCombat()
    {
        if (_inCombat || IsDead) return;
        _inCombat = true;
        EquipBestWeapon();
    }

    // Re-selects the weapon — call when ammo runs out so the NPC falls back to
    // the next attack-priority tier (e.g. archer out of arrows → melee).
    public void RefreshWeapon()
    {
        if (!_inCombat || IsDead) return;
        EquipBestWeapon();
    }

    // Call when combat ends (target lost, threat gone, NPC calms down). Puts the
    // weapon/shield away by destroying the held visuals.
    public void ExitCombat()
    {
        _inCombat       = false;
        _equippedWeapon = null;
        if (_weaponMount != null) { Destroy(_weaponMount); _weaponMount = null; }
        if (_shieldMount != null) { Destroy(_shieldMount); _shieldMount = null; }
    }

    void EquipBestWeapon()
    {
        LootItem weapon = SelectWeapon();
        if (weapon == _equippedWeapon && _weaponMount != null) return;

        if (_weaponMount != null) { Destroy(_weaponMount); _weaponMount = null; }
        _equippedWeapon = weapon;

        if (weapon != null)
            _weaponMount = HeldItemVisual.Attach(transform, weapon, EquipSlot.MainHand,
                                                 RightHandBone, LeftHandBone);

        // A shield goes in the off hand alongside a one-handed (or no) weapon.
        bool wantShield = weapon == null || !weapon.isTwoHanded;
        if (_shieldMount != null) { Destroy(_shieldMount); _shieldMount = null; }
        if (wantShield)
        {
            var shield = FindShield();
            if (shield != null)
                _shieldMount = HeldItemVisual.Attach(transform, shield, EquipSlot.OffHand,
                                                     RightHandBone, LeftHandBone);
        }
    }

    // Walks the attack-priority order and returns the strongest weapon the NPC
    // can currently use for the first tier it has one for.
    LootItem SelectWeapon()
    {
        foreach (AttackType type in AttackPriority)
        {
            LootItem best = BestWeaponForType(type);
            if (best != null) return best;
        }
        return null;
    }

    LootItem BestWeaponForType(AttackType type)
    {
        LootItem best = null;
        float bestDamage = -1f;

        foreach (var ni in items)
        {
            var it = ni.lootItem;
            if (it == null) continue;

            switch (type)
            {
                case AttackType.Melee:
                    if (it.itemType == LootItemType.Weapon && it.weaponCategory == WeaponCategory.Melee
                        && it.weaponDamage > bestDamage)
                    { best = it; bestDamage = it.weaponDamage; }
                    break;

                case AttackType.Ranged:
                    if (it.itemType == LootItemType.Weapon && it.weaponCategory == WeaponCategory.Ranged
                        && HasAmmoFor(it) && it.weaponDamage > bestDamage)
                    { best = it; bestDamage = it.weaponDamage; }
                    break;

                case AttackType.Thrown:
                    if (InventorySystem.IsThrownWeapon(it) && _thrownStock.TryGetValue(it, out int stock)
                        && stock > 0 && it.projectileDamage > bestDamage)
                    { best = it; bestDamage = it.projectileDamage; }
                    break;

                case AttackType.Spells:
                    break;   // spells need no held weapon
            }
        }
        return best;
    }

    bool HasAmmoFor(LootItem rangedWeapon)
    {
        if (rangedWeapon.requiredProjectile == ProjectileType.None) return true;
        foreach (var kvp in _thrownStock)
            if (kvp.Key != null && kvp.Key.projectileType == rangedWeapon.requiredProjectile && kvp.Value > 0)
                return true;
        return false;
    }

    LootItem FindShield()
    {
        foreach (var ni in items)
            if (ni.lootItem != null && ni.lootItem.itemType == LootItemType.Armor
                && ni.lootItem.equipSlot == EquipSlot.OffHand)
                return ni.lootItem;
        return null;
    }

    // ── Thrown-weapon API (used by NPC attack priority: AttackType.Thrown) ────

    public LootItem GetThrownWeapon()
    {
        foreach (var kvp in _thrownStock)
            if (kvp.Value > 0) return kvp.Key;
        return null;
    }

    public bool ConsumeThrown(LootItem item)
    {
        if (item == null || !_thrownStock.TryGetValue(item, out int count) || count <= 0)
            return false;
        _thrownStock[item] = count - 1;
        return true;
    }

    public bool HasThrownAmmo => _thrownStock.Any(kvp => kvp.Value > 0);

    // ── Wandering ─────────────────────────────────────────────────────────────

    void FireDeathFlags()
    {
        if (setsWorldFlagsOnDeath == null) return;
        foreach (string flag in setsWorldFlagsOnDeath)
            WorldStateSystem.Instance?.SetFlag(flag, true);
    }

    void ReportKillToQuestSystem()
    {
        if (definition == null) return;
        QuestSystem.Instance?.ReportKill(definition.name);
    }

    void PickDestination()
    {
        var zones = AllowedZones;
        if (zones == null || zones.Length == 0) return;

        var candidates = new List<Zone>();
        foreach (string id in zones)
            candidates.AddRange(Zone.WithId(id));
        if (candidates.Count == 0) return;

        Bounds b = candidates[Random.Range(0, candidates.Count)].Bounds;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            var point = new Vector3(
                Random.Range(b.min.x + 1f, b.max.x - 1f),
                transform.position.y,
                Random.Range(b.min.z + 1f, b.max.z - 1f));

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
                _idleUntil = float.MaxValue;
                return;
            }
        }

        _idleUntil = Time.time + Random.Range(IdleSecondsMin, IdleSecondsMax);
    }

    void OnArrived() => _idleUntil = Time.time + Random.Range(IdleSecondsMin, IdleSecondsMax);

    // ── Corpse loot ─────────────────────────────────────────────────────────

    void RollCorpseLoot()
    {
        if (_lootRolled) return;
        _lootRolled = true;
        // isLootable / gold range remain archetype traits on the definition;
        // the item contents come from this NPC's per-instance list.
        if (definition != null && !definition.isLootable) return;

        if (items != null)
            foreach (var npcItem in items)
                if (npcItem.lootItem != null && Random.value <= npcItem.dropChance)
                    _corpseLoot.Add(npcItem.lootItem, Mathf.Max(1, npcItem.quantity));

        int goldMin = definition != null ? definition.goldMin : 0;
        int goldMax = definition != null ? definition.goldMax : 0;
        _corpseGold = goldMax > goldMin ? Random.Range(goldMin, goldMax + 1) : goldMin;
    }

    void OnGUI()
    {
        if (!_lootOpen) return;
        if (PauseMenuController.IsPaused || GameUI.IsOpen) { CloseLoot(); return; }

        if (!LootWindowGUI.Draw(DisplayName, _corpseLoot, ref _corpseGold, ref _lootScroll))
            CloseLoot();

        // Mark emptied so the corpse stops offering loot and persists as looted.
        if (_corpseLoot.SlotCount == 0 && _corpseGold == 0)
        {
            _looted = true;
            CloseLoot();
            SceneStateManager.Instance?.SaveState();
        }
    }

    // ── Scene state save / restore ────────────────────────────────────────────

    public SavedNPCState CaptureState()
    {
        var state = new SavedNPCState
        {
            id            = saveId,
            prefabName    = gameObject.name,
            position      = transform.position,
            yRotation     = transform.eulerAngles.y,
            isAlive       = !_charAnim.IsDead,
            currentHealth = _health,
            maxHealth     = MaxHealth,
            lootRolled    = _lootRolled,
            hasBeenLooted = _looted,
            remainingGold = _corpseGold,
            inventory     = _corpseLoot.Capture(),
        };

        if (_charAnim.IsDead && transform.childCount > 0)
        {
            var model = transform.GetChild(0);
            state.hasModelOffset          = true;
            state.modelLocalPosition      = model.localPosition;
            state.modelLocalRotationEuler = model.localEulerAngles;
        }

        // Merchant stock + gold (so trading state survives scene reloads).
        var merchant = GetComponent<Merchant>();
        if (merchant != null)
        {
            state.isMerchant    = true;
            state.merchantGold  = merchant.CurrentGold;
            state.merchantStock = merchant.CaptureStock();
        }

        var appearance = GetComponent<NpcAppearanceComponent>();
        if (appearance != null)
        {
            state.appearanceLocked = true;
            state.appearanceSlots  = appearance.CaptureAppearance();
        }

        return state;
    }

    public void RestoreFromState(SavedNPCState state)
    {
        _health = state.currentHealth;

        // Restore corpse loot so a partially-looted body keeps its contents.
        _lootRolled = state.lootRolled;
        _looted     = state.hasBeenLooted;
        _corpseGold = state.remainingGold;
        _corpseLoot.Restore(state.inventory, SaveSystem.Instance?.database?.lootRegistry);

        // Restore merchant stock + gold.
        if (state.isMerchant)
            GetComponent<Merchant>()?.RestoreState(
                state.merchantStock, state.merchantGold,
                SaveSystem.Instance?.database?.lootRegistry);

        if (!state.isAlive)
        {
            _agent.isStopped = true;
            _agent.enabled   = false;
            _charAnim.ForceDeadImmediate();

            if (state.hasModelOffset && transform.childCount > 0)
            {
                var model = transform.GetChild(0);
                model.localPosition    = state.modelLocalPosition;
                model.localEulerAngles = state.modelLocalRotationEuler;
            }
        }

        // Restore the locked appearance before Awake can re-randomize.
        if (state.appearanceLocked)
        {
            var appearance = GetComponent<NpcAppearanceComponent>();
            appearance?.RestoreAppearance(state.appearanceSlots);
        }
    }
}
