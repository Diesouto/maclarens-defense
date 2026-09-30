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
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackForce = 5f;
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float attackCooldown = 2f;

    private NavMeshAgent agent;
    private Health health;
    private Animator animator;
    private Transform target;
    private bool isDead;
    private bool isAttacking;
    private NetworkEnemyState networkState;

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
            if (target == null)
                FindNearestPlayer();

            if (target != null && agent != null)
            {
                agent.SetDestination(target.position);

                if (!agent.pathPending && agent.remainingDistance <= attackDistance)
                {
                    StartAttack(target);
                    target = null;
                }
            }

            yield return new WaitForSeconds(searchInterval);
        }
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
        agent.isStopped = false;
    }

    void FindNearestPlayer()
    {
        float bestSqr = Mathf.Infinity;
        Transform best = null;
        Vector3 pos = transform.position;

        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null || !player.IsAlive)
                continue;

            float d = (player.transform.position - pos).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = player.transform;
            }
        }

        target = best;
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

        agent.isStopped = true;
        StartCoroutine(ResetAttack());
    }

    private void OnDeath()
    {
        isDead = true;
        networkState?.SetDead(true);
        StopAllCoroutines();

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
