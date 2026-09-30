using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct NetworkInventorySlot : INetworkSerializable, IEquatable<NetworkInventorySlot>
{
    public const int HeldSlotIndex = -1;

    public int SlotIndex;
    public FixedString64Bytes ItemId;
    public int CurrentAmmo;
    public int ReserveAmmo;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref CurrentAmmo);
        serializer.SerializeValue(ref ReserveAmmo);
    }

    public bool Equals(NetworkInventorySlot other)
    {
        return SlotIndex == other.SlotIndex && ItemId == other.ItemId &&
            CurrentAmmo == other.CurrentAmmo && ReserveAmmo == other.ReserveAmmo;
    }
}

// Host owns the real inventory; every other peer mirrors it so held visuals, poses and HUD match.
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerInventory))]
public class NetworkInventoryState : NetworkBehaviour
{
    public NetworkList<NetworkInventorySlot> Slots { get; private set; }
    public NetworkVariable<int> SelectedSlotIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> TotalValue = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerInventory inventory;
    private readonly List<NetworkInventorySlot> pendingSlots = new();
    private bool isReplicaDirty;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        Slots = new NetworkList<NetworkInventorySlot>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SyncFromInventory();
            return;
        }

        Slots.OnListChanged += HandleSlotsChanged;
        SelectedSlotIndex.OnValueChanged += HandleSelectedChanged;
        isReplicaDirty = true;
    }

    public override void OnNetworkDespawn()
    {
        Slots.OnListChanged -= HandleSlotsChanged;
        SelectedSlotIndex.OnValueChanged -= HandleSelectedChanged;
    }

    // Polled so ammo changes (which don't raise OnInventoryChanged) replicate too; at most 5 entries.
    private void LateUpdate()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            SyncFromInventory();
            return;
        }

        if (isReplicaDirty)
            ApplyToInventory();
    }

    private void HandleSlotsChanged(NetworkListEvent<NetworkInventorySlot> changeEvent) => isReplicaDirty = true;

    private void HandleSelectedChanged(int previous, int current) => isReplicaDirty = true;

    private void SyncFromInventory()
    {
        pendingSlots.Clear();
        for (int index = 0; index < inventory.MaxSlots; index++)
            AddPendingSlot(index, inventory.GetInstanceAtSlot(index));

        AddPendingSlot(NetworkInventorySlot.HeldSlotIndex, inventory.HeldItemInstance);

        if (!MatchesCurrentSlots())
        {
            Slots.Clear();
            foreach (NetworkInventorySlot slot in pendingSlots)
                Slots.Add(slot);
        }

        if (TotalValue.Value != inventory.TotalValue)
            TotalValue.Value = inventory.TotalValue;
        if (SelectedSlotIndex.Value != inventory.SelectedSlotIndex)
            SelectedSlotIndex.Value = inventory.SelectedSlotIndex;
    }

    private void AddPendingSlot(int slotIndex, ItemInstance instance)
    {
        if (instance?.Data == null)
            return;

        pendingSlots.Add(new NetworkInventorySlot
        {
            SlotIndex = slotIndex,
            ItemId = new FixedString64Bytes(instance.Data.ItemId),
            CurrentAmmo = instance.HasAmmoState ? instance.CurrentAmmo : -1,
            ReserveAmmo = instance.HasAmmoState ? instance.CurrentReserveAmmo : -1
        });
    }

    private bool MatchesCurrentSlots()
    {
        if (Slots.Count != pendingSlots.Count)
            return false;

        for (int i = 0; i < pendingSlots.Count; i++)
        {
            if (!Slots[i].Equals(pendingSlots[i]))
                return false;
        }

        return true;
    }

    private void ApplyToInventory()
    {
        isReplicaDirty = false;

        var backpack = new ItemInstance[PlayerInventory.BackpackSlotsCount];
        ItemInstance held = null;

        foreach (NetworkInventorySlot slot in Slots)
        {
            string itemId = slot.ItemId.ToString();
            if (!LootCatalog.TryGet(itemId, out LootDataSO data))
            {
                Debug.LogWarning($"NetworkInventoryState: unknown item id '{itemId}'.", this);
                continue;
            }

            bool isHeld = slot.SlotIndex == NetworkInventorySlot.HeldSlotIndex;
            if (!isHeld && (slot.SlotIndex < 0 || slot.SlotIndex >= backpack.Length))
                continue;

            ItemInstance existing = isHeld ? inventory.HeldItemInstance : inventory.GetInstanceAtSlot(slot.SlotIndex);
            bool reuseExisting = existing != null && existing.Data == data;
            ItemInstance instance = reuseExisting ? existing : new ItemInstance(data);
            // The owner simulates its own ammo and reports it upstream; don't roll it back with stale host values.
            bool keepLocalAmmo = reuseExisting && IsOwner && existing.HasAmmoState;
            if (!keepLocalAmmo && slot.CurrentAmmo >= 0 && slot.ReserveAmmo >= 0)
                instance.SetAmmo(slot.CurrentAmmo, slot.ReserveAmmo);

            if (isHeld)
                held = instance;
            else
                backpack[slot.SlotIndex] = instance;
        }

        inventory.ApplyReplicatedState(backpack, held, SelectedSlotIndex.Value);
    }
}
