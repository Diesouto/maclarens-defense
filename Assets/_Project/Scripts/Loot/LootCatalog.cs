using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Maps LootDataSO.ItemId back to the asset so replicated inventory ids can be resolved on every peer.
public static class LootCatalog
{
    private static readonly Dictionary<string, LootDataSO> itemsById = new();
    private static bool scannedNetworkPrefabs;

    public static void Register(LootDataSO data)
    {
        if (data == null)
            return;

        itemsById.TryAdd(data.ItemId, data);
    }

    public static bool TryGet(string itemId, out LootDataSO data)
    {
        data = null;
        if (string.IsNullOrEmpty(itemId))
            return false;

        if (itemsById.TryGetValue(itemId, out data))
            return true;

        ScanNetworkPrefabs();
        return itemsById.TryGetValue(itemId, out data);
    }

    private static void ScanNetworkPrefabs()
    {
        if (scannedNetworkPrefabs)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.NetworkConfig?.Prefabs == null)
            return;

        scannedNetworkPrefabs = true;
        foreach (NetworkPrefab networkPrefab in networkManager.NetworkConfig.Prefabs.Prefabs)
        {
            if (networkPrefab?.Prefab == null)
                continue;

            LootItem lootItem = networkPrefab.Prefab.GetComponentInChildren<LootItem>(true);
            if (lootItem != null)
                Register(lootItem.Data);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        itemsById.Clear();
        scannedNetworkPrefabs = false;
    }
}
