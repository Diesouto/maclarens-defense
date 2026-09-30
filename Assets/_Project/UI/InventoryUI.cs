using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private List<Image> backpackSlotIcons = new();
    [SerializeField] private List<Image> backpackSlotActiveBorders = new();

    private void Awake()
    {
        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

    public void Bind(PlayerInventory playerInventory)
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;

        inventory = playerInventory;

        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;

        Refresh();
    }

    private void Refresh()
    {
        if (inventory == null)
        {
            ResetVisuals();
            return;
        }

        for (int i = 0; i < backpackSlotIcons.Count; i++)
        {
            LootDataSO item = inventory.GetItemAtSlot(i);
            if (backpackSlotIcons.Count > i)
            {
                backpackSlotIcons[i].sprite = item != null ? item.Icon : null;
                backpackSlotIcons[i].enabled = item != null;
            }

            if (backpackSlotActiveBorders.Count > i && backpackSlotActiveBorders[i] != null)
                backpackSlotActiveBorders[i].enabled = inventory.SelectedSlotIndex == i;
        }

    }

    private void ResetVisuals()
    {
        foreach (Image slotImage in backpackSlotIcons)
        {
            if (slotImage != null)
            {
                slotImage.sprite = null;
                slotImage.enabled = false;
            }
        }

        foreach (Image activeBorder in backpackSlotActiveBorders)
        {
            if (activeBorder != null)
                activeBorder.enabled = false;
        }

    }
}
