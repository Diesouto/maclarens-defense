using Unity.Netcode;
using UnityEngine;

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

    private BodyCarrier carrier;

    private void Awake()
    {
        carrier = GetComponent<BodyCarrier>();
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
            return;
        }

        NetworkObject current = carrier.CarriedBody != null ? carrier.CarriedBody.GetComponent<NetworkObject>() : null;
        carriedBody.Value.TryGet(out NetworkObject replicated);
        if (current != replicated)
            carriedBody.Value = current != null ? new NetworkObjectReference(current) : default;
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
        NetworkObject trainObject = cargo != null ? cargo.GetComponentInParent<NetworkObject>() : null;
        if (trainObject != null && trainObject.IsSpawned)
        {
            trainReference = trainObject;
            cargoIndex = System.Array.IndexOf(trainObject.GetComponentsInChildren<TrainCargo>(true), cargo);
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
        if (!TrainCargo.TryStoreBody(body) && body.TryGetComponent(out CharacterRagdollController ragdoll) &&
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