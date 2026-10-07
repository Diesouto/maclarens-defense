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
    private Vector3 lastCarryPosition;
    private Quaternion lastCarrierYaw = Quaternion.identity;

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
        Quaternion carrierYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        // Ride along with the carrier (walking or train) rigidly; only the leftover offset is eased in.
        Quaternion yawDelta = carrierYaw * Quaternion.Inverse(lastCarrierYaw);
        Vector3 position = carryPosition + yawDelta * (carriedRoot.position - lastCarryPosition);
        Quaternion rotation = yawDelta * carriedRoot.rotation;
        lastCarryPosition = carryPosition;
        lastCarrierYaw = carrierYaw;

        Vector3 toTarget = carryPosition - position;
        Vector3 movement = toTarget.magnitude > snapDistance
            ? toTarget
            : Vector3.ClampMagnitude(toTarget * Mathf.Min(1f, followSpeed * Time.deltaTime), maxFollowSpeed * Time.deltaTime);
        carriedRagdoll.SetBodyPose(position + movement, rotation);
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
        lastCarryPosition = carryPoint.position;
        lastCarrierYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

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
