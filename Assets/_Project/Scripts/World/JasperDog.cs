using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class JasperDog : MonoBehaviour, IInteractable
{
    [Header("Wander")]
    [SerializeField] private float wanderRadius = 6f;
    [SerializeField] private Vector2 idleTimeRange = new Vector2(1.5f, 4f);
    [SerializeField] private float arriveThreshold = 0.4f;

    [Header("Backflip")]
    [SerializeField] private string dogName = "Jasper";
    [SerializeField] private float jumpSpeed = 5f;
    [SerializeField] private float flipSpinSpeed = 8f;
    [SerializeField] private float groundCheckDistance = 0.25f;
    [SerializeField] private float maxFlipDuration = 2.5f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float vertDampTime = 0.1f;

    private static readonly int VertHash = Animator.StringToHash("Vert");

    private NavMeshAgent agent;
    private Rigidbody body;
    private Vector3 origin;
    private float idleUntil;
    private bool isFlipping;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        origin = transform.position;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y);
    }

    private void Update()
    {
        UpdateAnimator();

        if (isFlipping || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (agent.hasPath && agent.remainingDistance > arriveThreshold)
        {
            return;
        }

        if (Time.time < idleUntil)
        {
            return;
        }

        PickNewDestination();
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        float vert = 0f;
        if (!isFlipping && agent.enabled && agent.isOnNavMesh && agent.speed > 0f)
        {
            vert = Mathf.Clamp01(agent.velocity.magnitude / agent.speed);
        }

        animator.SetFloat(VertHash, vert, vertDampTime, Time.deltaTime);
    }

    private void PickNewDestination()
    {
        idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y);

        Vector3 candidate = origin + Random.insideUnitSphere * wanderRadius;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !isFlipping;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        return $"Pet {dogName}";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (isFlipping)
        {
            return;
        }

        StartCoroutine(BackflipRoutine());
    }

    private IEnumerator BackflipRoutine()
    {
        isFlipping = true;
        agent.ResetPath();
        agent.enabled = false;

        body.isKinematic = false;
        body.linearVelocity = Vector3.up * jumpSpeed;
        // Negative right-axis spin tips the dog backwards.
        body.angularVelocity = -transform.right * flipSpinSpeed;

        // Let it leave the ground before checking for landing.
        yield return new WaitForSeconds(0.2f);

        float elapsed = 0.2f;
        while (elapsed < maxFlipDuration && !IsGrounded())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Let the dog settle briefly after touchdown.
        yield return new WaitForSeconds(0.2f);

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 position = transform.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            position = hit.position;
        }

        transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, Vector3.up));
        agent.enabled = true;
        agent.Warp(position);

        idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y);
        isFlipping = false;
    }

    private bool IsGrounded()
    {
        if (body.linearVelocity.y > 0f)
        {
            return false;
        }

        Vector3 start = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(start, Vector3.down, groundCheckDistance + 0.1f, groundMask, QueryTriggerInteraction.Ignore);
    }
}
