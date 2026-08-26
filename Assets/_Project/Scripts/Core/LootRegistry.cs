using System.Collections.Generic;
using UnityEngine;

public class LootRegistry : MonoBehaviour
{
    public static LootRegistry Instance { get; private set; }

    private readonly HashSet<LootItem> registeredLoot = new();

    public int Count => registeredLoot.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Register(LootItem loot)
    {
        if (loot == null)
            return;

        registeredLoot.Add(loot);
    }

    public void Unregister(LootItem loot)
    {
        if (loot == null)
            return;

        registeredLoot.Remove(loot);
    }

    public bool Contains(LootItem loot)
    {
        return loot != null && registeredLoot.Contains(loot);
    }

    public int GetExistingLootCount()
    {
        CleanupNullEntries();
        return registeredLoot.Count;
    }

    private void CleanupNullEntries()
    {
        registeredLoot.RemoveWhere(loot => loot == null);
    }

    public IReadOnlyCollection<LootItem> GetAllLoot()
    {
        CleanupNullEntries();
        return registeredLoot;
    }
}