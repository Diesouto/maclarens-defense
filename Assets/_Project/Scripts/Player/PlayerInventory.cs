using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public const int BackpackSlotsCount = 4;

    public event Action OnInventoryChanged;
    // Raised on the host (or offline) after a consumable leaves the inventory; listeners apply its effects.
    public event Action<LootDataSO> OnItemConsumed;

    [SerializeField] private int maxSlots = BackpackSlotsCount;
    [SerializeField] private Transform dropOrigin;

    private readonly ItemInstance[] backpackSlots = new ItemInstance[BackpackSlotsCount];
    private ItemInstance activeHeldItem;
    private int selectedSlotIndex = -1;
    private NetworkInventoryAuthority networkAuthority;

    public int MaxSlots => maxSlots;
    public int BackpackCount => backpackSlots.Count(slot => slot != null && slot.Data != null);
    public int Count => BackpackCount + (activeHeldItem != null ? 1 : 0);
    public int TotalValue => backpackSlots.Where(slot => slot != null && slot.Data != null).Sum(item => item.Data.Value) + (activeHeldItem?.Data != null ? activeHeldItem.Data.Value : 0);
    public bool IsFull => BackpackCount >= maxSlots;
    public bool HasAnyLoot => Count > 0;
    public bool HasActiveItem => ActiveItem != null;
    public ItemInstance ActiveItemInstance => activeHeldItem ?? GetInstanceAtSlot(selectedSlotIndex);
    public ItemInstance HeldItemInstance => activeHeldItem;
    public LootDataSO ActiveItem => ActiveItemInstance?.Data;
    public ItemAnimationProfile CurrentAnimationProfile => ActiveItem != null ? ActiveItem.AnimationProfile : ItemAnimationProfile.Carry;
    public int SelectedSlotIndex => selectedSlotIndex;
    public Transform DropOrigin => dropOrigin;

    private void Awake()
    {
        if (dropOrigin == null)
            dropOrigin = transform;

        maxSlots = Mathf.Clamp(maxSlots, 1, BackpackSlotsCount);
        networkAuthority = GetComponent<NetworkInventoryAuthority>();
    }

    public bool CanAdd(LootDataSO loot)
    {
        if (loot == null)
            return false;

        if (activeHeldItem != null)
            return CanStoreInBackpack(loot) && !IsFull && HasFreeBackpackSlot();

        if (!CanStoreInBackpack(loot) || IsFull)
            return true;

        return HasFreeBackpackSlot();
    }

    public bool TryAdd(LootDataSO loot)
    {
        return TryAdd(loot, null);
    }

    public bool TryAdd(LootDataSO loot, ItemInstance itemInstance)
    {
        if (!CanAdd(loot))
            return false;

        // A held two-hander can't share hands with a backpack item, so it drops at the player's feet.
        if (activeHeldItem != null)
            DropHeldItem();

        if (!CanStoreInBackpack(loot) || IsFull)
        {
            activeHeldItem = itemInstance ?? new ItemInstance(loot);
            selectedSlotIndex = -1;
            NotifyChanged();
            return true;
        }

        int emptySlot = FindFirstEmptyBackpackSlot();
        backpackSlots[emptySlot] = itemInstance ?? new ItemInstance(loot);
        selectedSlotIndex = emptySlot;
        NotifyChanged();
        return true;
    }

    public bool TryStoreActiveInSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= backpackSlots.Length || activeHeldItem == null || IsFull)
            return false;

        if (!CanStoreInBackpack(activeHeldItem.Data))
            return false;

        if (backpackSlots[slotIndex] == null)
        {
            backpackSlots[slotIndex] = activeHeldItem;
            activeHeldItem = null;
            selectedSlotIndex = slotIndex;
            NotifyChanged();
            return true;
        }

        ItemInstance previous = backpackSlots[slotIndex];
        backpackSlots[slotIndex] = activeHeldItem;
        activeHeldItem = previous;
        selectedSlotIndex = slotIndex;
        NotifyChanged();
        return true;
    }

    public bool TrySelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= backpackSlots.Length)
            return false;

        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestSelectSlotServerRpc(slotIndex);
            return true;
        }

        if (activeHeldItem != null)
            DropHeldItem();

        if (backpackSlots[slotIndex] == null)
        {
            selectedSlotIndex = -1;
            NotifyChanged();
            return true;
        }

        if (selectedSlotIndex == slotIndex)
        {
            selectedSlotIndex = -1;
            NotifyChanged();
            return true;
        }

        selectedSlotIndex = slotIndex;
        NotifyChanged();
        return true;
    }

    public bool TrySelectSlotByNumber(int slotNumber)
    {
        return TrySelectSlot(slotNumber - 1);
    }

    public bool TryDropSelected(Vector3? worldPosition = null)
    {
        return TryThrowSelected(worldPosition, Vector3.zero);
    }

    public bool TryThrowSelected(Vector3? worldPosition, Vector3 throwForce)
    {
        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestDropServerRpc(worldPosition ?? transform.position + transform.forward * 1.5f, throwForce);
            return true;
        }

        if (activeHeldItem != null)
        {
            Vector3 dropPosition = worldPosition ?? transform.position + transform.forward * 1.5f;
            DropItem(activeHeldItem, dropPosition, Quaternion.LookRotation(transform.forward), throwForce);
            activeHeldItem = null;
            selectedSlotIndex = -1;
            NotifyChanged();
            return true;
        }

        if (selectedSlotIndex >= 0 && selectedSlotIndex < backpackSlots.Length && backpackSlots[selectedSlotIndex] != null)
        {
            Vector3 dropPosition = worldPosition ?? transform.position + transform.forward * 1.5f;
            DropItem(backpackSlots[selectedSlotIndex], dropPosition, Quaternion.LookRotation(transform.forward), throwForce);
            backpackSlots[selectedSlotIndex] = null;
            selectedSlotIndex = -1;
            NotifyChanged();
            return true;
        }

        return false;
    }

    public bool TryUseActiveItem(Weapon targetWeapon)
    {
        if (targetWeapon == null || ActiveItem == null || !ActiveItem.IsWeapon)
            return false;

        if (targetWeapon.WeaponData != ActiveItem.WeaponData)
            targetWeapon.Equip(ActiveItem.WeaponData);

        return true;
    }

    public bool ActiveWeaponNeedsAmmo(LootDataSO weapon)
    {
        ItemInstance instance = ActiveItemInstance;
        if (weapon == null || !weapon.IsWeapon || instance?.Data != weapon || !instance.HasAmmoState)
            return false;

        return instance.CurrentAmmo < weapon.WeaponData.magazineSize ||
            instance.CurrentReserveAmmo < weapon.WeaponData.maxAmmo;
    }

    public bool TryConsumeActive()
    {
        ItemInstance instance = ActiveItemInstance;
        LootDataSO item = instance?.Data;
        if (item == null || !item.IsConsumable)
            return false;

        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestConsumeServerRpc();
            return true;
        }

        if (activeHeldItem == instance)
            activeHeldItem = null;
        else
            backpackSlots[selectedSlotIndex] = null;

        selectedSlotIndex = -1;
        DestroyWorldItem(instance);
        NotifyChanged();
        OnItemConsumed?.Invoke(item);
        return true;
    }

    // A picked-up item keeps its hidden world object for re-dropping; a consumed one is gone for good.
    private static void DestroyWorldItem(ItemInstance instance)
    {
        LootItem worldItem = instance.WorldItem;
        if (worldItem == null)
            return;

        LootRegistry.Instance?.Unregister(worldItem);
        if (worldItem.TryGetComponent(out Unity.Netcode.NetworkObject networkObject) && networkObject.IsSpawned)
            networkObject.Despawn(true);
        else
            Destroy(worldItem.gameObject);
    }

    public bool TryRefillActiveWeapon()
    {
        ItemInstance instance = ActiveItemInstance;
        if (instance?.Data == null || !instance.Data.IsWeapon)
            return false;

        instance.Refill(instance.Data.WeaponData.magazineSize, instance.Data.WeaponData.maxAmmo);
        NotifyChanged();
        return true;
    }

    public LootDataSO GetItemAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= backpackSlots.Length)
            return null;

        return backpackSlots[slotIndex]?.Data;
    }

    public ItemInstance GetInstanceAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= backpackSlots.Length)
            return null;

        return backpackSlots[slotIndex];
    }

    public bool HasFreeBackpackSlot()
    {
        return backpackSlots.Any(slot => slot == null);
    }

    public bool TryGetWeaponAmmo(ItemInstance weaponInstance, out int currentAmmo, out int currentReserveAmmo)
    {
        if (weaponInstance != null && weaponInstance.HasAmmoState)
        {
            currentAmmo = weaponInstance.CurrentAmmo;
            currentReserveAmmo = weaponInstance.CurrentReserveAmmo;
            return true;
        }

        currentAmmo = 0;
        currentReserveAmmo = 0;
        return false;
    }

    public void SetWeaponAmmo(ItemInstance weaponInstance, int currentAmmo, int currentReserveAmmo)
    {
        if (weaponInstance == null)
            return;

        weaponInstance.SetAmmo(currentAmmo, currentReserveAmmo);
    }

    // Host/offline only; clients mirror the result through the replicated inventory state.
    public void DropAllItems(Vector3 position)
    {
        if (NetworkRole.IsClientOnly)
            return;

        int dropped = 0;
        void DropScattered(ItemInstance item)
        {
            if (item == null)
                return;

            // Fanned out so simultaneous drops don't spawn overlapped and knock each other apart.
            float angle = dropped++ * 72f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.5f;
            DropItem(item, position + Vector3.up * 0.5f + offset, Quaternion.identity, Vector3.zero);
        }

        DropScattered(activeHeldItem);
        for (int i = 0; i < backpackSlots.Length; i++)
            DropScattered(backpackSlots[i]);

        Clear();
    }

    public void Clear()
    {
        activeHeldItem = null;
        Array.Clear(backpackSlots, 0, backpackSlots.Length);
        selectedSlotIndex = -1;
        NotifyChanged();
    }

    public bool TryRemove(LootDataSO loot)
    {
        if (loot == null)
            return false;

        if (activeHeldItem?.Data == loot)
        {
            activeHeldItem = null;
            NotifyChanged();
            return true;
        }

        for (int i = 0; i < backpackSlots.Length; i++)
        {
            if (backpackSlots[i]?.Data == loot)
            {
                backpackSlots[i] = null;
                if (selectedSlotIndex == i)
                    selectedSlotIndex = -1;
                NotifyChanged();
                return true;
            }
        }

        return false;
    }

    public IList<LootDataSO> GetBackpackSlots()
    {
        return backpackSlots.Select(slot => slot?.Data).ToList();
    }

    public bool TrySelectPreviousSlot()
    {
        if (backpackSlots.Length == 0)
            return false;

        int startIndex = selectedSlotIndex < 0 ? 0 : selectedSlotIndex;
        int candidate = FindOccupiedSlot(startIndex, -1);

        return candidate >= 0 && TrySelectSlot(candidate);
    }

    public bool TrySelectNextSlot()
    {
        if (backpackSlots.Length == 0)
            return false;

        int startIndex = selectedSlotIndex < 0 ? -1 : selectedSlotIndex;
        int candidate = FindOccupiedSlot(startIndex, 1);

        return candidate >= 0 && TrySelectSlot(candidate);
    }

    public void DropHeldItem()
    {
        if (activeHeldItem == null)
            return;

        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestDropServerRpc(transform.position + transform.forward * 1.5f, Vector3.zero);
            return;
        }

        Vector3 dropPosition = transform.position + transform.forward * 1.5f;
        DropItem(activeHeldItem, dropPosition, Quaternion.LookRotation(transform.forward), Vector3.zero);
        activeHeldItem = null;
        selectedSlotIndex = -1;
        NotifyChanged();
    }

    private int FindFirstEmptyBackpackSlot()
    {
        for (int i = 0; i < backpackSlots.Length; i++)
        {
            if (backpackSlots[i] == null)
                return i;
        }

        return -1;
    }

    private int FindOccupiedSlot(int startIndex, int direction)
    {
        for (int offset = 1; offset <= backpackSlots.Length; offset++)
        {
            int candidate = (startIndex + direction * offset) % backpackSlots.Length;
            if (candidate < 0)
                candidate += backpackSlots.Length;

            if (backpackSlots[candidate] != null)
                return candidate;
        }

        return -1;
    }

    private static bool CanStoreInBackpack(LootDataSO loot)
    {
        return loot != null && !loot.IsHeavy && !loot.IsTwoHanded;
    }

    private void DropItem(ItemInstance item, Vector3 position, Quaternion rotation, Vector3 throwForce)
    {
        if (item == null || item.Data == null)
            return;

        LootItem.CreateDroppedLoot(item.Data, position, rotation, item, throwForce);
    }

    // Client-side mirror of the host's inventory (see NetworkInventoryState); never re-sent upstream.
    public void ApplyReplicatedState(ItemInstance[] backpack, ItemInstance held, int selectedSlot)
    {
        for (int i = 0; i < backpackSlots.Length; i++)
            backpackSlots[i] = backpack != null && i < backpack.Length ? backpack[i] : null;

        activeHeldItem = held;
        selectedSlotIndex = selectedSlot;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}
