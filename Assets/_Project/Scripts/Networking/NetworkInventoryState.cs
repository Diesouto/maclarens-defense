using System;
using Unity.Netcode;
using UnityEngine;

public struct NetworkInventorySlot : INetworkSerializable, IEquatable<NetworkInventorySlot>
{
    public int SlotIndex;
    public string ItemName;
    public int ItemValue;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref ItemName);
        serializer.SerializeValue(ref ItemValue);
    }

    public bool Equals(NetworkInventorySlot other)
    {
        return SlotIndex == other.SlotIndex && ItemName == other.ItemName && ItemValue == other.ItemValue;
    }
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerInventory))]
public class NetworkInventoryState : NetworkBehaviour
{
    public NetworkList<NetworkInventorySlot> Slots { get; private set; }
    public NetworkVariable<int> TotalValue = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerInventory inventory;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        Slots = new NetworkList<NetworkInventorySlot>();
    }

    public override void OnNetworkSpawn()
    {
        inventory.OnInventoryChanged += HandleInventoryChanged;

        if (IsServer)
            SyncFromInventory();
    }

    public override void OnNetworkDespawn()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged()
    {
        if (IsServer)
            SyncFromInventory();
    }

    private void SyncFromInventory()
    {
        if (!IsServer)
            return;

        Slots.Clear();
        for (int index = 0; index < inventory.MaxSlots; index++)
        {
            LootDataSO item = inventory.GetItemAtSlot(index);
            if (item == null)
                continue;

            Slots.Add(new NetworkInventorySlot
            {
                SlotIndex = index,
                ItemName = item.DisplayName,
                ItemValue = item.Value
            });
        }

        TotalValue.Value = inventory.TotalValue;
    }
}
