using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public float searchInterval = 1f;
    public float stealDistance = 1.5f;

    [SerializeField] private float runSpeed = 4.5f;

    private NavMeshAgent agent;
    private Health health;
    private Animator animator;
    private Transform target;
    private bool isDead;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();

        if (agent != null)
            agent.speed = runSpeed;

        if (health != null)
            health.OnDeath += OnDeath;
    }

    void Start()
    {
        StartCoroutine(BehaviorLoop());
    }

    IEnumerator BehaviorLoop()
    {
        while (!isDead)
        {
            if (target == null)
                FindNearestBooze();

            if (target != null && agent != null)
            {
                agent.SetDestination(target.position);

                if (!agent.pathPending && agent.remainingDistance <= stealDistance)
                {
                    var booze = target.GetComponent<BoozeItem>();
                    if (booze != null)
                        booze.OnStolen();
                    else
                        Destroy(target.gameObject);

                    target = null;
                }
            }

            yield return new WaitForSeconds(searchInterval);
        }
    }

    void FindNearestBooze()
    {
        var items = GameObject.FindGameObjectsWithTag("Booze");
        float bestSqr = Mathf.Infinity;
        Transform best = null;
        Vector3 pos = transform.position;

        foreach (var go in items)
        {
            if (go == null) continue;
            float d = (go.transform.position - pos).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = go.transform;
            }
        }

        target = best;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= OnDeath;
    }

    private void Update()
    {
        if (isDead || animator == null)
            return;

        float speedValue = agent != null && agent.velocity.sqrMagnitude > 0.01f ? 1f : 0f;
        animator.SetFloat("Speed", speedValue);
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
