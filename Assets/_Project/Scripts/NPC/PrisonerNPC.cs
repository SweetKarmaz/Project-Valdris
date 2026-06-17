using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// All live PrisonerNPCs register here so InteractionHUD can scan without
// calling FindObjectsByType every frame.
[RequireComponent(typeof(NavMeshAgent))]
public class PrisonerNPC : MonoBehaviour
{
    public static readonly List<PrisonerNPC> All = new();

    [Header("Identity")]
    [Tooltip("Display name shown in the world-space name tag.")]
    public string displayName = "Prisoner";

    [Header("Visuals")]
    [Tooltip("Assign SM_Chr_Peasant_Male_01 (or any character prefab with an Animator).")]
    public GameObject modelPrefab;

    [Header("Wandering")]
    public float walkSpeed = 1.2f;
    public float idleSecondsMin = 2f;
    public float idleSecondsMax = 8f;
    public string[] allowedZones = { "Cell Block A", "Cell Block B", "Common Hall" };

    [Header("Health")]
    public float maxHealth = 30f;

    // Set by the spawner immediately after Instantiate so scene state can use them.
    [HideInInspector] public string SaveId;
    [HideInInspector] public string PrefabName;

    float _health;

    NavMeshAgent _agent;
    CharacterBuffs _buffs;
    CharacterAnimator _charAnim;
    float _idleUntil;
    bool _wasMovementPrevented;

    void Awake()
    {
        All.Add(this);
        _health = maxHealth;

        _agent = GetComponent<NavMeshAgent>();
        _agent.speed          = walkSpeed;
        _agent.angularSpeed   = 360f;
        _agent.acceleration   = 8f;
        _agent.stoppingDistance = 0.5f;
        _agent.radius         = 0.4f;
        _agent.height         = 1.9f;
        _agent.autoTraverseOffMeshLink = true;

        var col = gameObject.AddComponent<CapsuleCollider>();
        col.height = 1.9f; col.radius = 0.4f; col.center = new Vector3(0, 0.95f, 0);

        _buffs = gameObject.AddComponent<CharacterBuffs>();
        gameObject.AddComponent<CharacterResistances>();

        // Model: use assigned prefab, fall back to a plain capsule for prototyping.
        if (modelPrefab != null)
        {
            var model = Instantiate(modelPrefab, transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // Search the whole hierarchy in case the Animator is on a child rig object.
            var anim = model.GetComponentInChildren<Animator>(true);
            if (anim != null) anim.applyRootMotion = false;
        }
        else
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0, 0.95f, 0);
            float tint = Random.Range(0.35f, 0.55f);
            visual.GetComponent<Renderer>().material.color = new Color(tint, tint * 0.95f, tint * 0.8f);
        }

        // CharacterAnimator drives Speed/Cast/Hit/Dead on the child Animator.
        _charAnim = gameObject.AddComponent<CharacterAnimator>();

        _idleUntil = Time.time + Random.Range(0f, idleSecondsMax);
    }

    void OnDestroy() => All.Remove(this);

    void Update()
    {
        if (_charAnim.IsDead) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        if (PauseMenuController.IsPaused)
        {
            _agent.isStopped = true;
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
            _agent.isStopped = false;
            _wasMovementPrevented = false;
            _idleUntil = Time.time + Random.Range(idleSecondsMin, idleSecondsMax);
        }

        _agent.isStopped = false;

        // Drive walk/idle blend.
        float speed = _agent.hasPath ? _agent.velocity.magnitude / walkSpeed : 0f;
        _charAnim.SetSpeed(speed);

        bool reachedDestination = !_agent.pathPending
            && _agent.remainingDistance <= _agent.stoppingDistance
            && (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f);

        if (reachedDestination)
        {
            if (_idleUntil == float.MaxValue)
                OnArrived();
            else if (Time.time >= _idleUntil)
                PickDestination();
        }
    }

    public void TakeDamage(float amount)
    {
        if (_charAnim.IsDead) return;
        _health = Mathf.Max(0f, _health - amount);

        CombatTextSystem.Instance?.Show(
            transform, amount.ToString("0"), new Color(1f, 0.35f, 0.35f));

        if (_health <= 0f)
        {
            // Disable the agent entirely so it can't move the transform and
            // fight the death animation.
            _agent.isStopped = true;
            _agent.enabled   = false;
            _charAnim.TriggerDeath();
        }
        else
        {
            _charAnim.TriggerHit();
        }
    }

    void PickDestination()
    {
        var candidates = new List<Zone>();
        foreach (string id in allowedZones) candidates.AddRange(Zone.WithId(id));
        if (candidates.Count == 0) return;

        Zone targetZone = candidates[Random.Range(0, candidates.Count)];
        Bounds b = targetZone.Bounds;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            var candidate = new Vector3(
                Random.Range(b.min.x + 1f, b.max.x - 1f),
                transform.position.y,
                Random.Range(b.min.z + 1f, b.max.z - 1f));

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
                _idleUntil = float.MaxValue;
                return;
            }
        }

        _idleUntil = Time.time + Random.Range(idleSecondsMin, idleSecondsMax);
    }

    void OnArrived()
    {
        _idleUntil = Time.time + Random.Range(idleSecondsMin, idleSecondsMax);
    }

    // ── Scene state save / restore ────────────────────────────────────────────

    public SavedNPCState CaptureState()
    {
        var state = new SavedNPCState
        {
            id            = SaveId,
            prefabName    = PrefabName,
            position      = transform.position,
            yRotation     = transform.eulerAngles.y,
            isAlive       = !_charAnim.IsDead,
            currentHealth = _health,
            maxHealth     = maxHealth,
        };

        // When a character dies, TriggerDeath sets applyRootMotion=true so the
        // death animation slides the model child transform away from the NPC root.
        // Save that offset so we can restore the corpse pose without replaying.
        if (_charAnim.IsDead && transform.childCount > 0)
        {
            var model = transform.GetChild(0);
            state.hasModelOffset          = true;
            state.modelLocalPosition      = model.localPosition;
            state.modelLocalRotationEuler = model.localEulerAngles;
        }

        return state;
    }

    // Applied immediately after Instantiate when restoring from a saved scene state.
    public void RestoreFromState(SavedNPCState state)
    {
        _health   = state.currentHealth;
        maxHealth = state.maxHealth;

        if (!state.isAlive)
        {
            _agent.isStopped = true;
            _agent.enabled   = false;
            _charAnim.ForceDeadImmediate();

            // Restore the model child's displaced position from the death animation.
            // Must happen AFTER ForceDeadImmediate (which locks applyRootMotion=false)
            // so the animator can't move it again.
            if (state.hasModelOffset && transform.childCount > 0)
            {
                var model = transform.GetChild(0);
                model.localPosition    = state.modelLocalPosition;
                model.localEulerAngles = state.modelLocalRotationEuler;
            }
        }
    }
}
