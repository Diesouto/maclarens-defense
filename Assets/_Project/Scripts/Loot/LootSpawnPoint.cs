using System.Collections.Generic;
using UnityEngine;

public class LootSpawnPoint : MonoBehaviour
{
    [Header("Possible Loot")]
    [SerializeField]
    private List<GameObject> lootPrefabs = new();

    public bool IsOccupied { get; private set; }

    public GameObject GetRandomLootPrefab()
    {
        if (lootPrefabs == null || lootPrefabs.Count == 0)
            return null;

        return lootPrefabs[Random.Range(0, lootPrefabs.Count)];
    }

    public void SetOccupied(bool occupied)
    {
        IsOccupied = occupied;
    }

    public bool HasLootOptions()
    {
        return lootPrefabs != null && lootPrefabs.Count > 0;
    }
}