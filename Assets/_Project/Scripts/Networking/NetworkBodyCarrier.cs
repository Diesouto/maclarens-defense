using System;
using Unity.Netcode;
using UnityEngine;

public enum BodyRestMode : byte
{
    Loose,
    Resting,
    Stowed
}

// Host-decided pose of this player's own corpse: world pose when Resting, carriage-local when Stowed.
public struct BodyRestState : INetworkSerializeByMemcpy, IEquatable<BodyRestState>
{
    public BodyRestMode Mode;
    public ulong TrainObjectId;
    public int CargoIndex;
    public Vector3 Position;
    public Quaternion Rotation;

    public bool Equals(BodyRestState other)
    {
        return Mode == other.Mode && TrainObjectId == other.TrainObjectId && CargoIndex == other.CargoIndex &&
            Position == other.Position && Rotation == other.Rotation;
    }
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BodyCarrier))]
[DefaultExecutionOrder(-70)]
public class NetworkBodyCarrier : NetworkBehaviour
{
    [SerializeField, Min(0.5f)] private float carryDistance = 3f;

    [Header("Corpse Sync")]
    [Tooltip("A loose corpse slower than this for settleDuration is frozen on the host and sent to clients.")]
    [SerializeField, Min(0.01f)] private float settleSpeed = 0.25f;
    [SerializeField, Min(0f)] private float settleDuration = 0.5f;
    [SerializeField, Min(0.5f)] private float maximumLooseDuration = 6f;
    [Tooltip("Never freeze in world space this close to a carriage: the train could leave it behind.")]
    [SerializeField, Min(0f)] private float trainFreezeExclusion = 5f;

    private readonly NetworkVariable<NetworkObjectReference> carriedBody = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<BodyRestState> restState = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private BodyCarrier carrier;
    private PlayerBody ownBody;
    private CharacterRagdollController ownRagdoll;
    private TrainCargo serverStowCargo;
    private bool serverResting;
    private float settledTime;
    private float looseTime;
    private BodyRestState appliedRest;

    private void Awake()
    {
        carrier = GetComponent<BodyCarrier>();
        ownBody = GetComponent<PlayerBody>();
        ownRagdoll = GetComponent<CharacterRagdollController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            return;

        carriedBody.OnValueChanged += HandleCarriedBodyChanged;
        ApplyReplicatedCarry(carriedBody.Value);
    }

    public override void OnNetworkDespawn()
    {
        carriedBody.OnValueChanged -= HandleCarriedBodyChanged;
        carrier.ApplyReplicatedCarry(null);
    }

    private void LateUpdate()
    {
        if (!IsSpawned)
            return;

        if (!IsServer)
        {
            ApplyReplicatedCarry(carriedBody.Value);
            ApplyReplicatedRest();
            return;
        }

        NetworkObject current = carrier.CarriedBody != null ? carrier.CarriedBody.GetComponent<NetworkObject>() : null;
        carriedBody.Value.TryGet(out NetworkObject replicated);
        if (current != replicated)
            carriedBody.Value = current != null ? new NetworkObjectReference(current) : default;

        if (ownBody != null && ownRagdoll != null && ownRagdoll.RootRigidbody != null)
        {
            UpdateServerSettle();
            PublishRestState();
        }
    }

    private bool IsLooseCorpse()
    {
        return ownBody.IsDead && !ownBody.IsHidden && !ownBody.IsBeingCarried && !ownBody.IsBeingPulled &&
            ownRagdoll.IsRagdollActive && TrainCargo.GetStowedCargo(ownBody) == null;
    }

    // Each peer simulates its own ragdoll, so a settled corpse is frozen on the host and its pose replicated.
    private void UpdateServerSettle()
    {
        if (!IsLooseCorpse())
        {
            serverResting = false;
            settledTime = 0f;
            looseTime = 0f;
            return;
        }

        if (serverResting)
        {
            // Someone else (e.g. a lasso pull or a drop) handed the body back to physics.
            if (!ownRagdoll.IsPhysicsSuspended)
                serverResting = false;
            return;
        }

        Rigidbody root = ownRagdoll.RootRigidbody;
        bool slow = root.isKinematic || root.linearVelocity.sqrMagnitude <= settleSpeed * settleSpeed;
        settledTime = slow ? settledTime + Time.deltaTime : 0f;
        looseTime += Time.deltaTime;
        if (settledTime < settleDuration && looseTime < maximumLooseDuration)
            return;

        if (TrainCargo.TryStowBody(ownBody, ownBody.BodyPosition) ||
            TrainCargo.DistanceToNearestCargo(ownBody.BodyPosition) <= trainFreezeExclusion)
            return;

        ownRagdoll.SetPhysicsSuspended(true);
        serverResting = true;
    }

    private void PublishRestState()
    {
        TrainCargo cargo = TrainCargo.GetStowedCargo(ownBody);
        BodyRestState current = restState.Value;
        Rigidbody root = ownRagdoll.RootRigidbody;

        if (cargo != null && TryGetCargoAddress(cargo, out NetworkObject train, out int cargoIndex))
        {
            if (current.Mode == BodyRestMode.Stowed && cargo == serverStowCargo)
                return;

            serverStowCargo = cargo;
            restState.Value = new BodyRestState
            {
                Mode = BodyRestMode.Stowed,
                TrainObjectId = train.NetworkObjectId,
                CargoIndex = cargoIndex,
                Position = cargo.transform.InverseTransformPoint(root.position),
                Rotation = Quaternion.Inverse(cargo.transform.rotation) * root.rotation
            };
            return;
        }

        serverStowCargo = null;
        if (serverResting && IsLooseCorpse())
        {
            if (current.Mode == BodyRestMode.Resting)
                return;

            restState.Value = new BodyRestState
            {
                Mode = BodyRestMode.Resting,
                Position = root.position,
                Rotation = root.rotation
            };
            return;
        }

        if (current.Mode != BodyRestMode.Loose)
            restState.Value = default;
    }

    // Polled every frame: carry and health replicate separately, so applying may need several tries.
    private void ApplyReplicatedRest()
    {
        if (ownBody == null || ownRagdoll == null)
            return;

        BodyRestState state = restState.Value;
        TrainCargo localCargo = TrainCargo.GetStowedCargo(ownBody);
        bool free = ownBody.IsDead && !ownBody.IsHidden && !ownBody.IsBeingCarried && !ownBody.IsBeingPulled &&
            ownRagdoll.IsRagdollActive;

        switch (state.Mode)
        {
            case BodyRestMode.Stowed:
            {
                TrainCargo cargo = ResolveCargo(state);
                if (cargo == null || (localCargo == cargo && appliedRest.Equals(state)))
                    return;

                if (localCargo != null)
                    TrainCargo.ReleaseBody(ownBody);

                if (cargo.StowAt(ownBody, cargo.transform.TransformPoint(state.Position),
                        cargo.transform.rotation * state.Rotation))
                    appliedRest = state;
                return;
            }
            case BodyRestMode.Resting:
                if (localCargo != null)
                    TrainCargo.ReleaseBody(ownBody);

                if (!free || (appliedRest.Equals(state) && ownRagdoll.IsPhysicsSuspended))
                    return;

                ownRagdoll.SetPhysicsSuspended(true);
                ownRagdoll.SetBodyPose(state.Position, state.Rotation);
                appliedRest = state;
                return;
            default:
                if (appliedRest.Mode == BodyRestMode.Stowed && localCargo != null)
                    TrainCargo.ReleaseBody(ownBody);
                else if (appliedRest.Mode == BodyRestMode.Resting && free)
                    ownRagdoll.SetPhysicsSuspended(false);

                appliedRest = state;
                return;
        }
    }

    private static bool TryGetCargoAddress(TrainCargo cargo, out NetworkObject train, out int cargoIndex)
    {
        train = cargo.GetComponentInParent<NetworkObject>();
        cargoIndex = train != null && train.IsSpawned
            ? Array.IndexOf(train.GetComponentsInChildren<TrainCargo>(true), cargo)
            : -1;
        return cargoIndex >= 0;
    }

    private TrainCargo ResolveCargo(BodyRestState state)
    {
        if (NetworkManager == null ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.TrainObjectId, out NetworkObject train) ||
            train == null)
            return null;

        TrainCargo[] cargo = train.GetComponentsInChildren<TrainCargo>(true);
        return state.CargoIndex < cargo.Length ? cargo[state.CargoIndex] : null;
    }

    private void HandleCarriedBodyChanged(NetworkObjectReference previous, NetworkObjectReference current)
    {
        ApplyReplicatedCarry(current);
    }

    private void ApplyReplicatedCarry(NetworkObjectReference reference)
    {
        PlayerBody body = reference.TryGet(out NetworkObject bodyObject) && bodyObject != null
            ? bodyObject.GetComponent<PlayerBody>()
            : null;
        if (body == null && !reference.Equals(default(NetworkObjectReference)))
            return;
        if (body != null && (!body.IsDead || body.IsHidden))
            return;

        carrier.ApplyReplicatedCarry(body);
    }

    public void BroadcastRelease(PlayerBody body, Vector3 force)
    {
        if (!IsServer || !IsSpawned || body == null ||
            !body.TryGetComponent(out CharacterRagdollController ragdoll) || ragdoll.RootRigidbody == null)
            return;

        Vector3 position = ragdoll.RootRigidbody.position;
        Quaternion rotation = ragdoll.RootRigidbody.rotation;
        NetworkObjectReference trainReference = default;
        int cargoIndex = -1;
        TrainCargo cargo = TrainCargo.GetBodyCargo(body);
        if (cargo != null && TryGetCargoAddress(cargo, out NetworkObject trainObject, out cargoIndex))
        {
            trainReference = trainObject;
            position = cargo.transform.InverseTransformPoint(position);
            rotation = Quaternion.Inverse(cargo.transform.rotation) * rotation;
        }

        BodyReleasedRpc(body.GetComponent<NetworkObject>(), trainReference, cargoIndex, position, rotation, force);
    }

    [Rpc(SendTo.NotServer)]
    private void BodyReleasedRpc(NetworkObjectReference bodyReference, NetworkObjectReference trainReference,
        int cargoIndex, Vector3 position, Quaternion rotation, Vector3 force)
    {
        if (!bodyReference.TryGet(out NetworkObject bodyObject) || bodyObject == null ||
            !bodyObject.TryGetComponent(out PlayerBody body) || !body.IsDead)
            return;

        if (carrier.CarriedBody == body)
            carrier.ApplyReplicatedCarry(null);
        if (body.IsBeingCarried || body.IsHidden)
            return;

        if (cargoIndex >= 0)
        {
            if (!trainReference.TryGet(out NetworkObject trainObject) || trainObject == null)
                return;

            TrainCargo[] cargo = trainObject.GetComponentsInChildren<TrainCargo>(true);
            if (cargoIndex >= cargo.Length)
                return;

            position = cargo[cargoIndex].transform.TransformPoint(position);
            rotation = cargo[cargoIndex].transform.rotation * rotation;
        }

        body.ApplyBodyPose(position, rotation);
        if (force.sqrMagnitude > 0f)
            TrainCargo.ReleaseBodyForThrow(body);
        // A stowed body is re-latched by its owner's stow state on the next LateUpdate.
        if (body.TryGetComponent(out CharacterRagdollController ragdoll) &&
            ragdoll.RootRigidbody != null && !ragdoll.RootRigidbody.isKinematic)
            ragdoll.RootRigidbody.AddForce(force, ForceMode.Impulse);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestPickupServerRpc(NetworkObjectReference bodyReference,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            !bodyReference.TryGet(out NetworkObject bodyObject))
            return;

        if (bodyObject == null)
            return;

        PlayerBody body = bodyObject.GetComponent<PlayerBody>();
        if (body == null ||
            Vector3.Distance(body.BodyPosition, transform.position) > carryDistance + 2f)
            return;

        carrier.ApplyPickup(body);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestDropServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == OwnerClientId)
            carrier.Drop();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestThrowServerRpc(Vector3 force, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == OwnerClientId)
            carrier.ApplyThrow(Vector3.ClampMagnitude(force, 20f));
    }
}