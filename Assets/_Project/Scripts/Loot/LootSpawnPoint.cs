using System.Collections.Generic;
using UnityEngine;

public class LootSpawnPoint : MonoBehaviour
{
    [Header("Possible Loot")]
    [SerializeField]
    private List<LootDataSO> lootOptions = new();

    public bool IsOccupied { get; private set; }

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
}