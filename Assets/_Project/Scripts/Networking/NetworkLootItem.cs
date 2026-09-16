using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(LootItem))]
public class NetworkLootItem : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float pickupDistance = 3f;

    public NetworkVariable<bool> IsCollected = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private LootItem lootItem;

    private void Awake()
    {
        lootItem = GetComponent<LootItem>();
    }

    public override void OnNetworkSpawn()
    {
        IsCollected.OnValueChanged += HandleCollectedChanged;
        ApplyCollectedState(IsCollected.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsCollected.OnValueChanged -= HandleCollectedChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPickupServerRpc(ServerRpcParams rpcParams = default)
    {
        if (IsCollected.Value || NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                rpcParams.Receive.SenderClientId, out NetworkClient client) ||
            client.PlayerObject == null)
            return;

        if (Vector3.Distance(client.PlayerObject.transform.position, transform.position) > pickupDistance)
            return;

        PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
        if (inventory == null || !lootItem.TryCollect(inventory))
            return;

        IsCollected.Value = true;
    }

    private void HandleCollectedChanged(bool previous, bool current)
    {
        ApplyCollectedState(current);
    }

    private void ApplyCollectedState(bool collected)
    {
        if (collected && gameObject.activeSelf)
            gameObject.SetActive(false);
        else if (!collected && !gameObject.activeSelf)
            gameObject.SetActive(true);
    }
}