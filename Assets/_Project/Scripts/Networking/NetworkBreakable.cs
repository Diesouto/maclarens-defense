using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BreakableOnImpact))]
public class NetworkBreakable : NetworkBehaviour
{
    private BreakableOnImpact breakable;

    private void Awake()
    {
        breakable = GetComponent<BreakableOnImpact>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestBreakServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        breakable.ApplyBreak();
    }
}