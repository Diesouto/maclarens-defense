using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BodyCarrier))]
public class NetworkBodyCarrier : NetworkBehaviour
{
    [SerializeField, Min(0.5f)] private float carryDistance = 3f;

    private BodyCarrier carrier;

    private void Awake()
    {
        carrier = GetComponent<BodyCarrier>();
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