using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ShopStand))]
public class NetworkPurchaseAuthority : NetworkBehaviour
{
    [SerializeField, Min(3f)] private float purchaseDistance = 4f;

    private ShopStand shopStand;

    private void Awake()
    {
        shopStand = GetComponent<ShopStand>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPurchaseServerRpc(RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                rpcParams.Receive.SenderClientId, out NetworkClient client) ||
            client.PlayerObject == null)
            return;

        if (Vector3.Distance(client.PlayerObject.transform.position, transform.position) > purchaseDistance)
            return;

        PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
        if (shopStand.TryPurchase(inventory, out bool refilledAmmo) && refilledAmmo &&
            client.ClientId != NetworkManager.ServerClientId)
        {
            AmmoRefilledRpc(RpcTarget.Single(client.ClientId, RpcTargetUse.Temp));
        }
    }

    // The owner simulates its own ammo, so it must apply the refill locally too (see NetworkInventoryState).
    [Rpc(SendTo.SpecifiedInParams)]
    private void AmmoRefilledRpc(RpcParams rpcParams)
    {
        NetworkPlayer localPlayer = NetworkPlayer.Local;
        if (localPlayer != null && localPlayer.TryGetComponent(out PlayerInventory localInventory))
            localInventory.TryRefillActiveWeapon();
    }
}