using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Drags the carried body's ragdoll root toward the carry point every FixedUpdate instead of
// parenting it, so the ragdoll keeps simulating (and dangling) the whole time it's carried -
// the same trick used to drag a ragdoll by the collar. Throwing it just stops the drag and
// lets an impulse take over.
public class BodyCarrier : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;
    [SerializeField, Min(0f)] private float followSpeed = 12f;
    [SerializeField, Min(0.5f)] private float snapDistance = 2.5f;
    [SerializeField, Min(0f)] private float maxFollowSpeed = 25f;

    public PlayerBody CarriedBody { get; private set; }
    public bool IsCarryingBody => CarriedBody != null;

    private Rigidbody carriedRoot;
    private readonly List<Rigidbody> gravityDisabledBodies = new();
    private CharacterRagdollController carriedRagdoll;
    private Collider[] carrierColliders;
    private NetworkBodyCarrier networkAuthority;
    private Vector3 lastCarryPointPosition;

    private void Awake()
    {
        if (carryPoint == null)
            carryPoint = transform;

        carrierColliders = GetComponentsInChildren<Collider>();
        networkAuthority = GetComponent<NetworkBodyCarrier>();
    }

    private void FixedUpdate()
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
        Vector3 carrierVelocity = (carryPosition - lastCarryPointPosition) / Time.fixedDeltaTime;
        lastCarryPointPosition = carryPosition;

        Vector3 toTarget = carryPosition - carriedRoot.position;

        // Fell behind (fast carrier, train, snag): move the whole ragdoll instead of stretching its joints.
        if (toTarget.magnitude > snapDistance)
        {
            foreach (Rigidbody bone in carriedRagdoll.RagdollRigidbodies)
            {
                if (bone == null)
                    continue;

                bone.position += toTarget;
                bone.linearVelocity = Vector3.zero;
                bone.angularVelocity = Vector3.zero;
            }

            return;
        }

        // Feed-forward the carrier's own velocity so the body keeps pace instead of trailing by spring lag.
        carriedRoot.linearVelocity = Vector3.ClampMagnitude(toTarget * followSpeed + carrierVelocity, maxFollowSpeed);
    }

    public bool TryPickUp(PlayerBody body)
    {
        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestPickupServerRpc(body.GetComponent<NetworkObject>());
            return true;
        }

        return ApplyPickup(body);
    }

    public bool ApplyPickup(PlayerBody body)
    {
        if (body == null || IsCarryingBody || body.IsBeingCarried)
            return false;

        CharacterRagdollController ragdoll = body.GetComponent<CharacterRagdollController>();
        Rigidbody root = ragdoll != null ? ragdoll.RootRigidbody : null;

        if (root == null)
        {
            Debug.LogWarning($"BodyCarrier: '{body.name}' has no ragdoll Rigidbody to carry (run the Ragdoll Wizard on the Player rig).", body);
            return false;
        }

        CarriedBody = body;
        carriedRagdoll = ragdoll;
        carriedRoot = root;
        body.AttachTo(this);

        gravityDisabledBodies.Clear();
        foreach (Rigidbody bone in ragdoll.RagdollRigidbodies)
        {
            if (bone == null || !bone.useGravity)
                continue;

            bone.useGravity = false;
            gravityDisabledBodies.Add(bone);
        }

        lastCarryPointPosition = carryPoint.position;

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

        ReleaseCarry();
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
        ReleaseCarry();

        if (root != null)
            root.AddForce(force, ForceMode.Impulse);

        if (networkAuthority != null && networkAuthority.IsServer)
            networkAuthority.BroadcastThrow(force);
    }

    // Replicas mirror the host's carry locally: each peer drags its own ragdoll toward this carrier.
    public void ApplyReplicatedCarry(PlayerBody body)
    {
        if (CarriedBody == body)
            return;

        ReleaseCarry();
        if (body != null)
            ApplyPickup(body);
    }

    private void ReleaseCarry()
    {
        if (!IsCarryingBody)
            return;

        PlayerBody body = CarriedBody;

        foreach (Rigidbody bone in gravityDisabledBodies)
        {
            if (bone != null)
                bone.useGravity = true;
        }

        gravityDisabledBodies.Clear();

        SetIgnoreBodyCollisions(carriedRagdoll, false);

        carriedRoot = null;
        carriedRagdoll = null;
        CarriedBody = null;
        body.Detach();
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
