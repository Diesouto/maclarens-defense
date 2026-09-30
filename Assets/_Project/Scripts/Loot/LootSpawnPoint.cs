using System.Collections.Generic;
using UnityEngine;

public class LootSpawnPoint : MonoBehaviour
{
    [Header("Possible Loot")]
    [SerializeField]
    private List<LootDataSO> lootOptions = new();

    public bool IsOccupied { get; private set; }

    private void Awake()
    {
        foreach (LootDataSO lootData in lootOptions)
            LootCatalog.Register(lootData);
    }

    public bool HasLootOptions()
    {
        return lootOptions != null && lootOptions.Count > 0;
    }

    public LootDataSO GetRandomLootData()
    {
        if (!HasLootOptions())
            return null;

        float totalWeight = 0f;

        foreach (LootDataSO lootData in lootOptions)
        {
            if (lootData == null)
                continue;

            totalWeight += lootData.SpawnWeight;
        }

        if (totalWeight <= 0f)
            return null;

        float randomValue = Random.Range(0f, totalWeight);

        foreach (LootDataSO lootData in lootOptions)
        {
            if (lootData == null || lootData.SpawnWeight <= 0f)
                continue;

            randomValue -= lootData.SpawnWeight;

            if (randomValue <= 0f)
                return lootData;
        }

        return null;
    }

    public void SetOccupied(bool occupied)
    {
        IsOccupied = occupied;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsOccupied ? new Color(1f, 0.5f, 0f, 0.9f) : new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.65f);

        if (lootOptions != null && lootOptions.Count > 0)
        {
            Gizmos.color = new Color(1f, 0.94f, 0.3f, 0.9f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.2f);
        }
    }
}