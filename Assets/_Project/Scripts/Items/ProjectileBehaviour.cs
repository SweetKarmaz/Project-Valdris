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

    LootItem   _data;
    LootItem   _weapon;   // firing weapon (bow): its damage + riders add to the hit
    GameObject _source;   // shooter, for damage attribution
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
    public void Launch(Vector3 direction, float attackerDamage, LootItem projectileData,
                       LootItem weapon = null, GameObject source = null)
    {
        if (_launched) return;

        _data          = projectileData;
        _weapon        = weapon;
        _source        = source;
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
        if (other.isTrigger)             return;  // ignore trigger volumes (zones, etc.)

        // Never hit the shooter (player or NPC). Fall back to the Player tag when
        // no source was supplied (legacy callers).
        if (_source != null)
        {
            if (other.transform == _source.transform || other.transform.IsChildOf(_source.transform)) return;
        }
        else if (other.CompareTag("Player")) return;

        // ── Damage target ────────────────────────────────────────────────────

        bool hitCharacter = other.GetComponentInParent<IDamageable>() != null;
        if (hitCharacter)
        {
            // Physical = arrow base (+bonus) + the firing weapon's damage. Riders =
            // the arrow's elemental tips plus any the bow itself carries.
            float physical = _data.projectileDamage + _data.projectileDamageBonus
                             + _attackerDamage + (_weapon != null ? _weapon.weaponDamage : 0f);

            var riders = new List<OnHitEffect>();
            if (_data.onHitEffects != null)   riders.AddRange(_data.onHitEffects);
            if (_weapon != null && _weapon.onHitEffects != null) riders.AddRange(_weapon.onHitEffects);

            Combat.ApplyHit(other.gameObject, _source, physical, riders);
            ApplyOnHitModifiers(other.gameObject);
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
