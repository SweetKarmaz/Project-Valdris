using System.Collections;
using UnityEngine;

// Fired by Spellcaster when a Projectile-type spell uses this prefab.
// Flies in a straight line at a set speed. On impact:
//   - Damageable character: deals damage, triggers their animations, vanishes.
//   - Anything else (wall/floor/ceiling): expands over 0.5 s to fake an explosion, vanishes.
//
// Inspector values (speed, damage, range) are the prefab defaults.
// Spellcaster always calls Launch() which overrides damage and range at runtime
// so player stat scaling and spell modifiers are respected.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class EmberProjectile : MonoBehaviour
{
    [Header("Prefab Defaults (overridden by Spellcaster at runtime)")]
    public float speed  = 15f;
    public float damage = 10f;
    public float range  = 50f;

    Rigidbody _rb;
    Vector3 _origin;
    bool _spent;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity  = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    // Called by Spellcaster immediately after instantiation.
    // direction should already be normalized.
    public void Launch(Vector3 direction, float injectedDamage, float injectedRange)
    {
        damage  = injectedDamage;
        range   = injectedRange;
        _origin = transform.position;
        _rb.linearVelocity = direction.normalized * speed;
    }

    void Update()
    {
        if (_spent) return;
        if (range > 0f && (transform.position - _origin).sqrMagnitude > range * range)
        {
            _spent = true;
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (_spent) return;
        if (col.gameObject.CompareTag("Player")) return;

        _spent = true;
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic    = true;

        // Check the hit object and its whole hierarchy for a damageable character.
        var prisoner = col.gameObject.GetComponentInParent<PrisonerNPC>()
                    ?? col.gameObject.GetComponent<PrisonerNPC>();

        if (prisoner != null)
        {
            prisoner.TakeDamage(damage);
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(Explode());
        }
    }

    System.Collections.IEnumerator Explode()
    {
        float elapsed  = 0f;
        const float duration = 0.5f;
        Vector3 start  = transform.localScale;
        Vector3 end    = start * 4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
