using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public Transform player;

    private NavMeshAgent _agent;
    private EnemyBase _enemy;
    private CharacterBuffs _buffs;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _enemy = GetComponent<EnemyBase>();
        _buffs = GetComponent<CharacterBuffs>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void Update()
    {
        // Crowd control: an asleep/rooted enemy stops in place.
        if (_buffs != null && _buffs.IsMovementPrevented)
        {
            _agent.isStopped = true;
            return;
        }
        _agent.isStopped = false;

        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= detectionRange) _agent.SetDestination(player.position);
        if (dist <= attackRange) AttackPlayer();
    }

    private void AttackPlayer()
    {
        if (_buffs != null && _buffs.AreAbilitiesPrevented) return;
        // TODO: attack logic
    }
}
