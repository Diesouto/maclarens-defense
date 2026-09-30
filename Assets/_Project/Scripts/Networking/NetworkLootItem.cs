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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPickupServerRpc(RpcParams rpcParams = default)
    {
        if (IsCollected.Value || NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                rpcParams.Receive.SenderClientId, out NetworkClient client) ||
            client.PlayerObject == null)
            return;

        if (!IsWithinReach(client.PlayerObject.transform))
        {
            Debug.LogWarning($"NetworkLootItem: pickup of '{name}' by client {rpcParams.Receive.SenderClientId} rejected (too far on host).", this);
            return;
        }

        PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
        if (inventory != null && !lootItem.TryCollect(inventory))
            Debug.LogWarning($"NetworkLootItem: pickup of '{name}' by client {rpcParams.Receive.SenderClientId} rejected (inventory can't take it).", this);
    }

    // Interaction raycasts from the camera, so measure from eye height to the loot's surface, not root to root.
    private bool IsWithinReach(Transform player)
    {
        Vector3 eye = player.position + Vector3.up * 1.6f;
        Vector3 closest = transform.position;
        float best = float.MaxValue;
        foreach (Collider lootCollider in GetComponentsInChildren<Collider>())
        {
            Vector3 point = lootCollider.ClosestPointOnBounds(eye);
            float distance = (point - eye).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                closest = point;
            }
        }

        // Small slack for the owner-to-host position lag.
        return Vector3.Distance(eye, closest) <= pickupDistance + 0.75f;
    }

    public void SetCollectedOnServer(bool collected)
    {
        if (IsServer && IsSpawned && IsCollected.Value != collected)
            IsCollected.Value = collected;
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