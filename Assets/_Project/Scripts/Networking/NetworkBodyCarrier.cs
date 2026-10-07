using System;
using Unity.Netcode;
using UnityEngine;

// Where this player's own corpse is latched inside a train carriage, relative to that carriage.
public struct BodyStowState : INetworkSerializeByMemcpy, IEquatable<BodyStowState>
{
    public ulong TrainObjectId;
    public int CargoIndex;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;

    public static BodyStowState Loose => new() { CargoIndex = -1, LocalRotation = Quaternion.identity };
    public bool IsStowed => CargoIndex >= 0;

    public bool Equals(BodyStowState other)
    {
        return TrainObjectId == other.TrainObjectId && CargoIndex == other.CargoIndex &&
            LocalPosition == other.LocalPosition && LocalRotation == other.LocalRotation;
    }
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BodyCarrier))]
[DefaultExecutionOrder(-70)]
public class NetworkBodyCarrier : NetworkBehaviour
{
    [SerializeField, Min(0.5f)] private float carryDistance = 3f;

    private readonly NetworkVariable<NetworkObjectReference> carriedBody = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<BodyStowState> stowState = new(
        BodyStowState.Loose,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private BodyCarrier carrier;
    private PlayerBody ownBody;
    private CharacterRagdollController ownRagdoll;
    private TrainCargo serverStowCargo;
    private BodyStowState appliedStow = BodyStowState.Loose;

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
            ApplyReplicatedStow();
            return;
        }

        NetworkObject current = carrier.CarriedBody != null ? carrier.CarriedBody.GetComponent<NetworkObject>() : null;
        carriedBody.Value.TryGet(out NetworkObject replicated);
        if (current != replicated)
            carriedBody.Value = current != null ? new NetworkObjectReference(current) : default;

        PublishStowState();
    }

    private void PublishStowState()
    {
        TrainCargo cargo = TrainCargo.GetStowedCargo(ownBody);
        if (cargo == serverStowCargo)
            return;

        serverStowCargo = cargo;
        if (cargo == null || ownRagdoll == null || ownRagdoll.RootRigidbody == null ||
            !TryGetCargoAddress(cargo, out NetworkObject train, out int cargoIndex))
        {
            stowState.Value = BodyStowState.Loose;
            return;
        }

        Rigidbody root = ownRagdoll.RootRigidbody;
        stowState.Value = new BodyStowState
        {
            TrainObjectId = train.NetworkObjectId,
            CargoIndex = cargoIndex,
            LocalPosition = cargo.transform.InverseTransformPoint(root.position),
            LocalRotation = Quaternion.Inverse(cargo.transform.rotation) * root.rotation
        };
    }

    // Polled every frame: carry and health replicate separately, so the stow may need several tries.
    private void ApplyReplicatedStow()
    {
        if (ownBody == null)
            return;

        BodyStowState state = stowState.Value;
        TrainCargo localCargo = TrainCargo.GetStowedCargo(ownBody);
        if (!state.IsStowed)
        {
            appliedStow = state;
            if (localCargo != null)
                TrainCargo.ReleaseBody(ownBody);
            return;
        }

        TrainCargo cargo = ResolveCargo(state);
        if (cargo == null || (localCargo == cargo && appliedStow.Equals(state)))
            return;

        if (localCargo != null)
            TrainCargo.ReleaseBody(ownBody);

        if (cargo.StowAt(ownBody, cargo.transform.TransformPoint(state.LocalPosition),
                cargo.transform.rotation * state.LocalRotation))
            appliedStow = state;
    }

    private static bool TryGetCargoAddress(TrainCargo cargo, out NetworkObject train, out int cargoIndex)
    {
        train = cargo.GetComponentInParent<NetworkObject>();
        cargoIndex = train != null && train.IsSpawned
            ? Array.IndexOf(train.GetComponentsInChildren<TrainCargo>(true), cargo)
            : -1;
        return cargoIndex >= 0;
    }

    private TrainCargo ResolveCargo(BodyStowState state)
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