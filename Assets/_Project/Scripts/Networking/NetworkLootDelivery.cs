using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(LootDeliveryPoint))]
public class NetworkLootDelivery : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float deliveryDistance = 4f;

    private LootDeliveryPoint deliveryPoint;

    private void Awake()
    {
        deliveryPoint = GetComponent<LootDeliveryPoint>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestDeliverServerRpc(NetworkObjectReference lootReference,
        RpcParams rpcParams = default)
    {
        if (!lootReference.TryGet(out NetworkObject lootObject) || lootObject == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                rpcParams.Receive.SenderClientId, out NetworkClient client) ||
            client.PlayerObject == null)
            return;

        if (Vector3.Distance(lootObject.transform.position, transform.position) > deliveryDistance)
            return;

        LootItem lootItem = lootObject.GetComponent<LootItem>();
        if (lootItem == null)
            return;

        if (!deliveryPoint.TryDeliver(lootItem))
            return;

        lootObject.Despawn(true);
    }
}