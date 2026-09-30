using UnityEngine;

[System.Serializable]
public class ItemInstance
{
    [SerializeField] private LootDataSO data;
    [SerializeField] private int currentAmmo = -1;
    [SerializeField] private int currentReserveAmmo = -1;

    public LootDataSO Data => data;
    public int CurrentAmmo => currentAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public LootItem WorldItem { get; private set; }

    // Prevents drop-and-repickup from re-triggering the one-time Threat spike for this item.
    public bool HasTriggeredThreat { get; private set; }

    public void MarkThreatTriggered()
    {
        HasTriggeredThreat = true;
    }

    public ItemInstance(LootDataSO itemData)
    {
        data = itemData;
    }

    public void SetAmmo(int ammo, int reserveAmmo)
    {
        currentAmmo = Mathf.Max(ammo, 0);
        currentReserveAmmo = Mathf.Max(reserveAmmo, 0);
    }

    // Bumped on shop refills so an equipped Weapon knows to re-read ammo it didn't write itself.
    public int RefillCount { get; private set; }

    public void Refill(int ammo, int reserveAmmo)
    {
        SetAmmo(ammo, reserveAmmo);
        RefillCount++;
    }

    public void BindWorldItem(LootItem worldItem)
    {
        WorldItem = worldItem;
    }

    public bool HasAmmoState => currentAmmo >= 0 && currentReserveAmmo >= 0;
}
