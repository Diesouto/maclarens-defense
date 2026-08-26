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

        TryDeliver(lootItem);
    }

    private bool TryDeliver(LootItem lootItem)
    {
        if (lootItem == null || lootItem.Data == null)
            return false;

        int value = lootItem.Data.Value;

        // Remove the item from the active loot registry.
        LootRegistry.Instance?.Unregister(lootItem);

        // The value now officially counts towards the quota.
        QuotaManager.Instance?.AddDeliveredValue(value);

        // The delivered object no longer exists in the world.
        Destroy(lootItem.gameObject);

        return true;
    }
}