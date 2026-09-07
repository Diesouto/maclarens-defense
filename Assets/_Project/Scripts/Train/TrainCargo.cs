using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrainCargo : MonoBehaviour
{
    [SerializeField] private Collider cargoTrigger;

    public IReadOnlyCollection<LootItem> ItemsInCargo => itemsInCargo;

    private readonly HashSet<LootItem> itemsInCargo = new();

    private void Awake()
    {
        if (cargoTrigger == null)
            cargoTrigger = GetComponent<Collider>();

        cargoTrigger.isTrigger = true;

        if (GetComponent<TrainSplineFollower>() == null &&
            GetComponent<TrainCarFollower>() == null)
        {
            Debug.LogWarning(
                $"{name}: TrainCargo has no TrainSplineFollower/TrainCarFollower.",
                this
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        LootItem lootItem = other.GetComponentInParent<LootItem>();

        if (lootItem == null)
            return;

        if (!itemsInCargo.Add(lootItem))
            return;

        lootItem.SetCargo(this);
        AttachToCargo(lootItem);

        int value = lootItem.Data != null
            ? lootItem.Data.Value
            : 0;

        QuotaManager.Instance?.AddCargoValue(value);

        Debug.Log(
            $"TrainCargo: Added {lootItem.name} worth ${value}.",
            this
        );
    }

    private void OnTriggerExit(Collider other)
    {
        LootItem lootItem = other.GetComponentInParent<LootItem>();

        if (lootItem == null)
            return;

        if (!itemsInCargo.Remove(lootItem))
            return;

        lootItem.SetCargo(null);
        DetachFromCargo(lootItem);

        int value = lootItem.Data != null
            ? lootItem.Data.Value
            : 0;

        QuotaManager.Instance?.AddCargoValue(-value);

        Debug.Log(
            $"TrainCargo: Removed {lootItem.name} worth ${value}.",
            this
        );
    }

    public bool RemoveItem(LootItem lootItem)
    {
        if (lootItem == null)
            return false;

        if (!itemsInCargo.Remove(lootItem))
            return false;

        lootItem.SetCargo(null);
        DetachFromCargo(lootItem);

        int value = lootItem.Data != null
            ? lootItem.Data.Value
            : 0;

        QuotaManager.Instance?.AddCargoValue(-value);

        Debug.Log(
            $"TrainCargo: Removed {lootItem.name} worth ${value}.",
            this
        );

        return true;
    }

    // Loot must ride along with the train, mirroring how TrainPassenger attaches players to a carriage.
    private void AttachToCargo(LootItem lootItem)
    {
        lootItem.transform.SetParent(transform, true);
    }

    private void DetachFromCargo(LootItem lootItem)
    {
        if (lootItem.transform.parent == transform)
            lootItem.transform.SetParent(null, true);
    }
}