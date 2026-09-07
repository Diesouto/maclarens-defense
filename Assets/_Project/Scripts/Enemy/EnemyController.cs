using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();

        if (agent != null)
            agent.speed = runSpeed;

        if (health != null)
        {
            health.OnHit += OnHit;
            health.OnDeath += OnDeath;
        }
    }

    void Start()
    {
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
        agent.isStopped = false;
    }

    void FindNearestPlayer()
    {
        float bestSqr = Mathf.Infinity;
        Transform best = null;
        Vector3 pos = transform.position;

        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null)
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
        StopAllCoroutines();

        if (agent != null)
            agent.enabled = false;

        if (animator != null)
            animator.SetFloat("Speed", 0f);

        Destroy(gameObject, 30f);
        enabled = false;
    }
}
