using Unity.Netcode;
using UnityEngine;

// Host decides who carries which body; every other peer mirrors it by dragging its own local ragdoll
// (ragdolls are per-peer presentation, so only "who carries what" and throws need to be replicated).
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BodyCarrier))]
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
    }

    private void LateUpdate()
    {
        if (!IsServer || !IsSpawned)
            return;

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
        carrier.ApplyReplicatedCarry(body);
    }

    [Rpc(SendTo.NotServer)]
    private void BodyThrownRpc(Vector3 force)
    {
        carrier.ApplyThrow(force);
    }

    public void BroadcastThrow(Vector3 force)
    {
        if (IsServer && IsSpawned)
            BodyThrownRpc(force);
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

        if (Vector3.Distance(bodyObject.transform.position, transform.position) > carryDistance)
            return;

        PlayerBody body = bodyObject.GetComponent<PlayerBody>();
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