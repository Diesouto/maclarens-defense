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

    [ServerRpc(RequireOwnership = false)]
    public void RequestBreakServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        breakable.ApplyBreak();
    }
}