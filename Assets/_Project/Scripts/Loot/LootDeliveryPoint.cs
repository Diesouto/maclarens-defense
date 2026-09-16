using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LootDeliveryPoint : MonoBehaviour
{
    [SerializeField] private Collider deliveryTrigger;

    private void Awake()
    {
        if (deliveryTrigger == null)
            deliveryTrigger = GetComponent<Collider>();

        deliveryTrigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        LootItem lootItem = other.GetComponentInParent<LootItem>();

        if (lootItem == null)
            return;

        NetworkLootDelivery networkDelivery = GetComponent<NetworkLootDelivery>();
        if (networkDelivery != null && networkDelivery.IsSpawned && !networkDelivery.IsServer)
        {
            NetworkLootItem networkLoot = lootItem.GetComponent<NetworkLootItem>();
            if (networkLoot != null && networkLoot.IsSpawned)
                networkDelivery.RequestDeliverServerRpc(networkLoot.NetworkObject);

            return;
        }

        TryDeliver(lootItem);
    }

    public bool TryDeliver(LootItem lootItem)
    {
        if (lootItem == null || lootItem.Data == null)
            return false;

        int value = lootItem.Data.Value;

        if (lootItem.Cargo != null)
            lootItem.Cargo.RemoveItem(lootItem);

        // Remove the item from the active loot registry.
        LootRegistry.Instance?.Unregister(lootItem);

        // Delivered loot becomes shared team money. Quota payment happens later at Finish Day.
        MoneyManager.Instance?.AddMoney(value);

        // Networked loot is despawned by NetworkLootDelivery after this method returns.
        if (lootItem.GetComponent<NetworkLootItem>() == null)
            Destroy(lootItem.gameObject);

        return true;
    }
}