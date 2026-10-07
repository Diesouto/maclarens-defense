using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-60)]
public class BodyCarrier : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;
    [SerializeField, Min(0f)] private float followSpeed = 12f;
    [SerializeField, Min(0.5f)] private float snapDistance = 2.5f;
    [SerializeField, Min(0f)] private float maxFollowSpeed = 25f;

    public PlayerBody CarriedBody { get; private set; }
    public bool IsCarryingBody => CarriedBody != null;

    private Rigidbody carriedRoot;
    private CharacterRagdollController carriedRagdoll;
    private Collider[] carrierColliders;
    private NetworkBodyCarrier networkAuthority;

    private void Awake()
    {
        if (carryPoint == null)
            carryPoint = transform;

        carrierColliders = GetComponentsInChildren<Collider>();
        networkAuthority = GetComponent<NetworkBodyCarrier>();
    }

    private void OnDisable()
    {
        ReleaseCarry();
    }

    private void LateUpdate()
    {
        if (carriedRoot == null)
            return;

        // The body was revived (ragdoll off) while carried: stop dragging it.
        if (carriedRagdoll != null && !carriedRagdoll.IsRagdollActive)
        {
            ReleaseCarry();
            return;
        }

        Vector3 carryPosition = carryPoint.position;
        Vector3 toTarget = carryPosition - carriedRoot.position;
        Vector3 movement = toTarget.magnitude > snapDistance
            ? toTarget
            : Vector3.ClampMagnitude(toTarget * Mathf.Min(1f, followSpeed * Time.deltaTime), maxFollowSpeed * Time.deltaTime);
        carriedRagdoll.SetBodyPose(carriedRoot.position + movement, carriedRoot.rotation);
    }

    public bool TryPickUp(PlayerBody body)
    {
        if (body == null)
            return false;

        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestPickupServerRpc(body.GetComponent<NetworkObject>());
            return true;
        }

        return ApplyPickup(body);
    }

    public bool ApplyPickup(PlayerBody body)
    {
        if (body == null || !body.IsDead || body.IsHidden || IsCarryingBody || body.IsBeingCarried)
            return false;

        if (TryGetComponent(out Health health) && health.IsDead)
            return false;

        CharacterRagdollController ragdoll = body.GetComponent<CharacterRagdollController>();
        Rigidbody root = ragdoll != null ? ragdoll.RootRigidbody : null;

        if (root == null)
        {
            Debug.LogWarning($"BodyCarrier: '{body.name}' has no ragdoll Rigidbody to carry (run the Ragdoll Wizard on the Player rig).", body);
            return false;
        }

        TrainCargo.ReleaseBody(body);
        ragdoll.SetPhysicsSuspended(true);
        CarriedBody = body;
        carriedRagdoll = ragdoll;
        carriedRoot = root;
        body.AttachTo(this);

        SetIgnoreBodyCollisions(ragdoll, true);

        return true;
    }

    public void Drop()
    {
        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestDropServerRpc();
            return;
        }

        PlayerBody body = CarriedBody;
        ReleaseCarry();
        if (body != null && networkAuthority != null && networkAuthority.IsServer)
            networkAuthority.BroadcastRelease(body, Vector3.zero);
    }

    public void Throw(Vector3 force)
    {
        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestThrowServerRpc(force);
            return;
        }

        ApplyThrow(force);
    }

    public void ApplyThrow(Vector3 force)
    {
        if (!IsCarryingBody)
            return;

        Rigidbody root = carriedRoot;
        PlayerBody body = CarriedBody;
        ReleaseCarry(false);

        if (root != null && !root.isKinematic)
            root.AddForce(force, ForceMode.Impulse);

        TrainCargo.ReleaseBodyForThrow(body);

        if (networkAuthority != null && networkAuthority.IsServer)
            networkAuthority.BroadcastRelease(body, force);
    }

    public void ApplyReplicatedCarry(PlayerBody body)
    {
        if (CarriedBody == body)
            return;

        ReleaseCarry();
        if (body != null)
            ApplyPickup(body);
    }

    private void ReleaseCarry(bool allowCargoCapture = true)
    {
        if (!IsCarryingBody)
            return;

        PlayerBody body = CarriedBody;

        SetIgnoreBodyCollisions(carriedRagdoll, false);
        if (carriedRagdoll != null)
            carriedRagdoll.SetPhysicsSuspended(false);

        carriedRoot = null;
        carriedRagdoll = null;
        CarriedBody = null;
        body.Detach();
        if (allowCargoCapture)
            TrainCargo.TryStowBody(body, transform.position);
    }

    private void SetIgnoreBodyCollisions(CharacterRagdollController ragdoll, bool ignore)
    {
        if (ragdoll == null || carrierColliders == null)
            return;

        IReadOnlyList<Collider> bodyColliders = ragdoll.RagdollColliders;
        foreach (Collider bodyCollider in bodyColliders)
        {
            if (bodyCollider == null)
                continue;

            foreach (Collider carrierCollider in carrierColliders)
            {
                if (carrierCollider == null)
                    continue;

                Physics.IgnoreCollision(bodyCollider, carrierCollider, ignore);
            }
        }
    }
}
