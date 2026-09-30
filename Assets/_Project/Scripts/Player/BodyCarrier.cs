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

    public PlayerBody CarriedBody { get; private set; }
    public bool IsCarryingBody => CarriedBody != null;

    private Rigidbody carriedRoot;
    private bool carriedRootHadGravity;
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

    private void FixedUpdate()
    {
        if (carriedRoot == null)
            return;

        Vector3 toTarget = carryPoint.position - carriedRoot.position;
        carriedRoot.linearVelocity = toTarget * followSpeed;
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
            return false;

        CarriedBody = body;
        carriedRagdoll = ragdoll;
        carriedRoot = root;
        body.AttachTo(this);

        carriedRootHadGravity = root.useGravity;
        root.useGravity = false;

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
    }

    private void ReleaseCarry()
    {
        if (!IsCarryingBody)
            return;

        PlayerBody body = CarriedBody;

        if (carriedRoot != null)
            carriedRoot.useGravity = carriedRootHadGravity;

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
