using System.Collections.Generic;
using UnityEngine;

// Attach to any prefab in Assets/_Project/Prefabs/WeaponProjectiles/.
// PlayerRanged (and later NpcRanged) calls Launch() immediately after
// Instantiating the prefab. The component reads all flight and damage
// parameters from the LootItem data asset so individual prefabs need no
// per-instance tuning — just set the values on the LootItem.
//
// Requires: Rigidbody (useGravity = false), one Collider set as IsTrigger.
[RequireComponent(typeof(Rigidbody))]
public class ProjectileBehaviour : MonoBehaviour
{
    // ── Runtime state ─────────────────────────────────────────────────────────

    LootItem  _data;
    float     _attackerDamage;
    float     _distanceTravelled;
    Vector3   _lastPos;
    bool      _launched;
    bool      _stopped;
    Rigidbody _rb;

    // Tracks already-hit colliders so piercing projectiles don't double-damage.
    readonly HashSet<Collider> _hit = new();

    // ── Public API ────────────────────────────────────────────────────────────

    // Called by PlayerRanged (or NpcRanged) right after Instantiate().
    // attackerDamage: the attacker's base AttackDamage stat, added on top of
    // the projectile's own projectileDamage.
    public void Launch(Vector3 direction, float attackerDamage, LootItem projectileData)
    {
        if (_launched) return;

        _data          = projectileData;
        _attackerDamage = attackerDamage;
        _lastPos       = transform.position;
        _launched      = true;

        _rb            = GetComponent<Rigidbody>();
        _rb.useGravity = false;             // gravity handled manually via gravityScale
        _rb.isKinematic = false;
        _rb.linearVelocity = direction.normalized * projectileData.projectileSpeed;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    // ── Physics update ────────────────────────────────────────────────────────

    void FixedUpdate()
    {
        if (!_launched || _stopped) return;

        // Apply scaled gravity so 0 = straight, 1 = full Physics.gravity.
        if (_data.projectileGravityScale > 0f)
            _rb.AddForce(Physics.gravity * _data.projectileGravityScale, ForceMode.Acceleration);

        // Rotate to follow velocity (arrow points along its arc).
        if (_rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);

        // Track distance and despawn if the projectile has flown its maximum range.
        _distanceTravelled += Vector3.Distance(transform.position, _lastPos);
        _lastPos            = transform.position;

        if (_distanceTravelled >= _data.projectileRange)
            Despawn();
    }

    // ── Collision ─────────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!_launched || _stopped)      return;
        if (_hit.Contains(other))        return;
        if (other.CompareTag("Player"))  return;  // never hit the shooter
        if (other.isTrigger)             return;  // ignore trigger volumes (zones, etc.)

        // ── Damage target ────────────────────────────────────────────────────

        bool hitCharacter = false;

        // Try NpcController first (covers all living NPCs).
        if (other.TryGetComponent<NpcController>(out var npc))
        {
            npc.TakeDamage(TotalDamage());
            ApplyOnHitModifiers(other.gameObject);
            hitCharacter = true;
        }
        // Fallback: EnemyBase for any non-NPC enemies.
        else if (other.TryGetComponent<EnemyBase>(out var enemy))
        {
            enemy.TakeDamage(TotalDamage());
            hitCharacter = true;
        }

        _hit.Add(other);

        // ── Decide what to do next ───────────────────────────────────────────

        if (_data.projectilePiercing && hitCharacter)
            return; // keep flying — don't stop on this character

        if (_data.embedOnHit)
            Embed(other);
        else
            Despawn();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    float TotalDamage() =>
        (_data.projectileDamage + _data.projectileDamageBonus + _attackerDamage);

    void ApplyOnHitModifiers(GameObject target)
    {
        if (_data.onHitModifiers == null || _data.onHitModifiers.Count == 0) return;

        // Wire to CharacterBuffs / stat system when buff application is implemented.
        // For now, log so designers can verify the data is read.
        foreach (var mod in _data.onHitModifiers)
            Debug.Log($"[Projectile] OnHit modifier: {mod.stat} {mod.amount:+0.##;-0.##} on {target.name}");
    }

    // Stop the projectile and stick it into the surface or character it hit.
    void Embed(Collider surface)
    {
        _stopped       = true;
        _rb.isKinematic = true;  // freeze in place

        // Parent to the hit object so it moves with a ragdoll or door, etc.
        transform.SetParent(surface.transform, worldPositionStays: true);

        Invoke(nameof(Despawn), _data.despawnDelay);
    }

    void Despawn()
    {
        CancelInvoke(nameof(Despawn));
        Destroy(gameObject);
    }
}
