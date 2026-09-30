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
        shopStand.TryPurchase(inventory);
    }
}