using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TrainCargo : MonoBehaviour
{
    [SerializeField] private BoxCollider cargoTrigger;
    [SerializeField] private Transform cargoRoot;

    private readonly HashSet<LootItem> itemsInCargo = new();

    private void Awake()
    {
        if (cargoTrigger == null)
            cargoTrigger = GetComponent<BoxCollider>();

        cargoTrigger.isTrigger = true;

        if (cargoRoot == null)
            cargoRoot = transform;

        if (GetComponent<TrainSplineFollower>() == null && GetComponent<TrainCarFollower>() == null)
            Debug.LogWarning($"{name}: TrainCargo's cargoRoot has no TrainSplineFollower/TrainCarFollower, so deposited loot won't move with the train.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        LootItem lootItem = other.GetComponentInParent<LootItem>();
        if (lootItem == null || !itemsInCargo.Add(lootItem))
            return;

        // Keep world position so the item stays put when the train later moves as a whole.
        lootItem.transform.SetParent(cargoRoot, true);
        RecalculateCargoValue();
    }

    private void OnTriggerExit(Collider other)
    {
        LootItem lootItem = other.GetComponentInParent<LootItem>();
        if (lootItem == null || !itemsInCargo.Remove(lootItem))
            return;

        if (lootItem.transform.parent == cargoRoot)
            lootItem.transform.SetParent(null, true);

        RecalculateCargoValue();
    }

    private void RecalculateCargoValue()
    {
        itemsInCargo.RemoveWhere(item => item == null || item.IsCollected || !item.gameObject.activeInHierarchy);

        int totalValue = itemsInCargo.Sum(item => item.Data != null ? item.Data.Value : 0);
        QuotaManager.Instance?.SetCargoValue(totalValue);
    }
}
