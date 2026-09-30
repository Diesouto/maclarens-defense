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

    public void PlayBreakEffects(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        if (IsServer && IsSpawned)
            PlayBreakEffectsRpc(position, rotation, velocity);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayBreakEffectsRpc(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        breakable.PlayLocalBreakEffects(position, rotation, velocity);
    }
}