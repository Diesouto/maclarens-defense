using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class JasperDog : NetworkBehaviour, IInteractable
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

    private readonly NetworkVariable<bool> replicatedFlipping = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NavMeshAgent agent;
    private Rigidbody body;
    private Vector3 origin;
    private float idleUntil;
    private bool isFlipping;
    private Vector3 previousPosition;

    private void Awake()
    {
        // In-scene NetworkObjects nested under a plain GameObject (Environment) get misplaced on clients.
        if (transform.parent != null && TryGetComponent(out NetworkObject networkObject))
        {
            networkObject.AutoObjectParentSync = false;
            transform.SetParent(null, true);
        }

        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        // NetworkRigidbody would restore the serialized non-kinematic state on the host, so gravity fights the
        // NavMeshAgent every frame (the jitter that only stopped after a backflip re-applied kinematic).
        if (TryGetComponent(out NetworkRigidbody networkRigidbody))
            networkRigidbody.AutoUpdateKinematicState = false;
        origin = transform.position;
        previousPosition = transform.position;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (IsSpawned && !IsServer)
            agent.enabled = false;

        idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y);
    }

    public override void OnNetworkSpawn()
    {
        replicatedFlipping.OnValueChanged += HandleReplicatedFlippingChanged;
        ApplyFlippingState(replicatedFlipping.Value);

        if (!IsServer)
        {
            agent.enabled = false;
            body.isKinematic = true;
        }
    }

    protected override void OnNetworkPostSpawn()
    {
        if (!isFlipping)
            body.isKinematic = true;
    }

    public override void OnNetworkDespawn()
    {
        replicatedFlipping.OnValueChanged -= HandleReplicatedFlippingChanged;
    }

    private void Update()
    {
        UpdateAnimator();

        if ((IsSpawned && !IsServer) || isFlipping || !agent.enabled || !agent.isOnNavMesh)
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

        float speed = 0f;
        if (IsSpawned && !IsServer)
        {
            speed = (transform.position - previousPosition).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        }
        else if (agent.enabled && agent.isOnNavMesh)
        {
            speed = agent.velocity.magnitude;
        }

        previousPosition = transform.position;
        float vert = !isFlipping && agent.speed > 0f ? Mathf.Clamp01(speed / agent.speed) : 0f;
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
        return $"Acariciar {dogName}";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (IsSpawned && !IsServer)
        {
            RequestBackflipServerRpc();
            return;
        }

        BeginBackflip();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestBackflipServerRpc(RpcParams rpcParams = default)
    {
        if (isFlipping || NetworkManager == null ||
            !NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client) ||
            client.PlayerObject == null ||
            (client.PlayerObject.transform.position - transform.position).sqrMagnitude > 16f ||
            (client.PlayerObject.TryGetComponent(out Health playerHealth) && playerHealth.IsDead))
            return;

        BeginBackflip();
    }

    private void BeginBackflip()
    {
        if (isFlipping || (IsSpawned && !IsServer))
            return;

        if (IsSpawned)
        {
            replicatedFlipping.Value = true;
            ApplyFlippingState(true);
        }
        else
            ApplyFlippingState(true);

        StartCoroutine(BackflipRoutine());
    }

    private void HandleReplicatedFlippingChanged(bool previous, bool current)
    {
        ApplyFlippingState(current);
    }

    private void ApplyFlippingState(bool flipping)
    {
        isFlipping = flipping;
        if (IsSpawned && !IsServer)
            return;

        if (flipping)
        {
            agent.ResetPath();
            agent.enabled = false;
            body.isKinematic = false;
            body.linearVelocity = Vector3.up * jumpSpeed;
            body.angularVelocity = -transform.right * flipSpinSpeed;
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        if (!agent.enabled)
            agent.enabled = true;
        if (agent.isOnNavMesh)
            agent.Warp(transform.position);
    }

    private IEnumerator BackflipRoutine()
    {
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
        idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y);
        if (IsSpawned)
        {
            replicatedFlipping.Value = false;
            ApplyFlippingState(false);
        }
        else
            ApplyFlippingState(false);
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
