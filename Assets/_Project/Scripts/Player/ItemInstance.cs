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

    public ItemInstance(LootDataSO itemData)
    {
        data = itemData;
    }

    public void SetAmmo(int ammo, int reserveAmmo)
    {
        currentAmmo = Mathf.Max(ammo, 0);
        currentReserveAmmo = Mathf.Max(reserveAmmo, 0);
    }

    public void BindWorldItem(LootItem worldItem)
    {
        WorldItem = worldItem;
    }

    public bool HasAmmoState => currentAmmo >= 0 && currentReserveAmmo >= 0;
}
