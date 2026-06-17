using UnityEngine;

// Simple forward-flying spell projectile. Damage is injected by the caster
// (PlayerMagic) so all stat/skill/buff math stays in one place.
[RequireComponent(typeof(Collider))]
public class SpellProjectile : MonoBehaviour
{
    public float speed = 20f;
    public GameObject impactVFXPrefab;

    private float _damage;
    private float _maxDistance;
    private Vector3 _origin;
    private bool _launched;
    private bool _isCrit;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    public void Launch(float damage, float maxDistance, bool isCrit = false)
    {
        _damage = damage;
        _maxDistance = maxDistance > 0f ? maxDistance : 50f;
        _isCrit = isCrit;
        _origin = transform.position;
        _launched = true;
    }

    private void Update()
    {
        if (!_launched) return;
        transform.position += transform.forward * speed * Time.deltaTime;
        if ((transform.position - _origin).sqrMagnitude > _maxDistance * _maxDistance)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return; // don't hit the caster
        if (other.TryGetComponent<EnemyBase>(out var enemy))
            enemy.TakeDamage(_damage, _isCrit);
        if (impactVFXPrefab != null)
            Instantiate(impactVFXPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
