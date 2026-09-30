using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    private static readonly HashSet<EnemyController> activeEnemies = new();

    [SerializeField] private float searchInterval = 1f;
    [SerializeField] private float attackDistance = 1.5f;
    [SerializeField] private float attackVerticalReach = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackForce = 5f;
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float attackCooldown = 2f;
    [Tooltip("Town residents only notice players within this range; threat-spawned enemies hunt anywhere.")]
    [SerializeField, Min(0f)] private float residentDetectionRange = 20f;
    [Tooltip("Another player must be this much closer (metres) than the current target to steal the enemy's attention.")]
    [SerializeField, Min(0f)] private float retargetMargin = 2f;
    [Tooltip("Kamikaze enemies (tumbleweed): detonate this instead of attacking once in reach.")]
    [SerializeField] private Explosive selfDestruct;
    [SerializeField, Min(0f)] private float selfDestructFuse = 0.3f;

    private NavMeshAgent agent;
    private Health health;
    private Animator animator;
    private Transform target;
    private PlayerController targetPlayer;
    private bool isDead;
    private bool isAttacking;
    private NetworkEnemyState networkState;

    public bool IsDead => isDead;
    public bool IsResident { get; private set; }

    public void MarkAsResident()
    {
        IsResident = true;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();
        networkState = GetComponent<NetworkEnemyState>();

        if (agent != null)
            agent.speed = runSpeed;

        if (health != null)
        {
            health.OnHit += OnHit;
            health.OnDeath += OnDeath;
        }
    }

    private void OnEnable()
    {
        activeEnemies.Add(this);
    }

    public static void DespawnAll()
    {
        foreach (EnemyController enemy in new List<EnemyController>(activeEnemies))
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }

        activeEnemies.Clear();
    }

    void Start()
    {
        if (networkState != null && networkState.IsSpawned && !networkState.IsServer)
        {
            enabled = false;
            return;
        }

        StartCoroutine(AwaitSpawnAnimation());
        StartCoroutine(BehaviorLoop());
    }

    IEnumerator AwaitSpawnAnimation()
    {
        agent.isStopped = true;

        if (animator != null)
            yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        agent.isStopped = false;
    }

    IEnumerator BehaviorLoop()
    {
        while (!isDead)
        {
            FindNearestPlayer();

            if (target != null && agent != null)
            {
                agent.SetDestination(target.position);

                // remainingDistance is measured on the NavMesh: an airborne or unreachable player projects
                // onto the mesh below, so the real 3D distance must also be within reach.
                if (!agent.pathPending && agent.remainingDistance <= attackDistance && IsWithinAttackReach(target))
                {
                    if (selfDestruct != null)
                    {
                        selfDestruct.Detonate(selfDestructFuse);
                    }
                    else
                    {
                        StartAttack(target);
                    }
                }
            }
            else if (IsAgentUsable() && agent.hasPath)
            {
                agent.ResetPath();
            }

            yield return new WaitForSeconds(searchInterval);
        }
    }

    private bool IsWithinAttackReach(Transform attackTarget)
    {
        Vector3 offset = attackTarget.position - transform.position;
        float verticalOffset = Mathf.Abs(offset.y);
        offset.y = 0f;

        return offset.sqrMagnitude <= (attackDistance + 0.5f) * (attackDistance + 0.5f) &&
            verticalOffset <= attackVerticalReach;
    }

    void StartAttack(Transform attackTarget)
    {
        if (isAttacking || isDead)
            return;

        isAttacking = true;
        networkState?.SetAttacking(true);
        agent.isStopped = true;
        animator.SetTrigger("HasAttacked");

        var player = attackTarget.GetComponent<Health>();

        if (player != null)
            player.TakeDamage(attackDamage, (player.transform.position - transform.position).normalized, attackForce);

        StartCoroutine(ResetAttack());
    }

    IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
        networkState?.SetAttacking(false);
        if (IsAgentUsable())
            agent.isStopped = false;
    }

    // Clients disable the agent (the host drives enemies), but OnHit still fires there from replicated health.
    private bool IsAgentUsable() => agent != null && agent.enabled && agent.isOnNavMesh && !isDead;

    // Re-evaluated every tick so the enemy switches to whoever is closest; the margin stops flip-flopping
    // between two players at similar distances.
    void FindNearestPlayer()
    {
        Vector3 pos = transform.position;
        bool hasTarget = targetPlayer != null && targetPlayer.IsAlive;
        float currentSqr = hasTarget ? (targetPlayer.transform.position - pos).sqrMagnitude : Mathf.Infinity;

        float bestSqr = IsResident && !hasTarget ? residentDetectionRange * residentDetectionRange : Mathf.Infinity;
        PlayerController best = null;

        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null || !player.IsAlive)
                continue;

            float d = (player.transform.position - pos).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = player;
            }
        }

        if (hasTarget && best != targetPlayer)
        {
            float currentDistance = Mathf.Sqrt(currentSqr);
            if (best == null || Mathf.Sqrt(bestSqr) > currentDistance - retargetMargin)
                best = targetPlayer;
        }

        targetPlayer = best;
        target = best != null ? best.transform : null;

        // Once a resident spots someone it stays alerted and hunts like any threat spawn.
        if (best != null)
            IsResident = false;
    }

    private void OnDestroy()
    {
        activeEnemies.Remove(this);

        if (health != null)
        {
            health.OnHit -= OnHit;
            health.OnDeath -= OnDeath;
        }
    }

    private void Update()
    {
        if (isDead || animator == null)
            return;

        float speedValue = agent != null && agent.velocity.sqrMagnitude > 0.01f ? 1f : 0f;
        animator.SetFloat("Speed", speedValue);
    }

    private void OnHit()
    {
        if (animator != null)
            animator.SetTrigger("HasBeenHit");

        if (!IsAgentUsable())
            return;

        agent.isStopped = true;
        StartCoroutine(ResetAttack());
    }

    private void OnDeath()
    {
        isDead = true;
        networkState?.SetDead(true);
        StopAllCoroutines();
        ThreatManager.Instance?.RegisterEnemyKill();

        if (agent != null)
            agent.enabled = false;

        if (animator != null)
            animator.SetFloat("Speed", 0f);

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            StartCoroutine(DespawnAfterDelay(networkObject, 30f));
        else if (networkObject == null)
            Destroy(gameObject, 30f);
        enabled = false;
    }

    private IEnumerator DespawnAfterDelay(NetworkObject networkObject, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (networkObject != null && networkObject.IsSpawned &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            networkObject.Despawn(true);
    }
}
