using Unity.Netcode;
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
        // Loot physics is host-authoritative; clients only see replicated copies.
        if (NetworkRole.IsClientOnly)
            return;

        LootItem lootItem = other.GetComponentInParent<LootItem>();

        if (lootItem == null)
            return;

        TryDeliver(lootItem);
    }

    public bool TryDeliver(LootItem lootItem)
    {
        if (NetworkRole.IsClientOnly || lootItem == null || lootItem.Data == null)
            return false;

        int value = lootItem.Data.Value;

        if (lootItem.Cargo != null)
            lootItem.Cargo.RemoveItem(lootItem);

        // Remove the item from the active loot registry.
        LootRegistry.Instance?.Unregister(lootItem);

        // Delivered loot becomes shared team money. The quota is paid later, explicitly, in MacLarens.
        MoneyManager.Instance?.AddEarnings(value);

        // Networked loot must be despawned (not destroyed) so every peer removes it.
        NetworkObject networkObject = lootItem.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
            networkObject.Despawn(true);
        else
            Destroy(lootItem.gameObject);

        return true;
    }
}