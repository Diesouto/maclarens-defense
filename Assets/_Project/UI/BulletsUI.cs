using UnityEngine;

public class BulletsUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI currentBulletsText;
    [SerializeField] private TMPro.TextMeshProUGUI dividerLabel;
    [SerializeField] private TMPro.TextMeshProUGUI maxBulletsText;

    private Weapon weapon;
    private PlayerInventory inventory;
    private ItemHolder itemHolder;

    private void Awake()
    {
        EnsureBindings();
    }

    private void OnEnable()
    {
        EnsureBindings();
        Refresh();
    }

    private void OnDisable()
    {
        BindWeapon(null);
        BindItemHolder(null);
        BindInventory(null);
    }

    private void Start()
    {
        EnsureBindings();
        Refresh();
    }

    private void Refresh()
    {
        EnsureBindings();
        bool visible = inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.IsWeapon;
        if (currentBulletsText != null)
            currentBulletsText.enabled = visible;
        if (dividerLabel != null)
            dividerLabel.enabled = visible;
        if (maxBulletsText != null)
            maxBulletsText.enabled = visible;

        if (visible && weapon != null)
        {
            weapon.SyncWithActiveItem();
            HandleAmmoChanged(weapon.CurrentAmmo, weapon.CurrentReserveAmmo);
        }
    }

    private void EnsureBindings()
    {
        PlayerInventory activeInventory = inventory;
        if (activeInventory == null)
            activeInventory = PlayerInventory.Instance;

        if (activeInventory == null)
            activeInventory = FindFirstObjectByType<PlayerInventory>();

        if (activeInventory != inventory)
            BindInventory(activeInventory);

        ItemHolder activeItemHolder = inventory != null ? inventory.GetComponent<ItemHolder>() : null;
        if (activeItemHolder != itemHolder)
            BindItemHolder(activeItemHolder);

        BindWeapon(itemHolder != null ? itemHolder.RuntimeWeapon : null);
    }

    private void BindInventory(PlayerInventory newInventory)
    {
        if (inventory == newInventory)
            return;

        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;

        inventory = newInventory;

        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;
    }

    private void BindItemHolder(ItemHolder newItemHolder)
    {
        if (itemHolder == newItemHolder)
            return;

        if (itemHolder != null)
            itemHolder.OnRuntimeWeaponChanged -= HandleRuntimeWeaponChanged;

        itemHolder = newItemHolder;

        if (itemHolder != null)
            itemHolder.OnRuntimeWeaponChanged += HandleRuntimeWeaponChanged;
    }

    private void BindWeapon(Weapon newWeapon)
    {
        if (weapon == newWeapon)
            return;

        if (weapon != null)
            weapon.OnAmmoChanged -= HandleAmmoChanged;

        weapon = newWeapon;

        if (weapon != null)
            weapon.OnAmmoChanged += HandleAmmoChanged;
    }

    private void HandleRuntimeWeaponChanged(Weapon runtimeWeapon)
    {
        BindWeapon(runtimeWeapon);
        Refresh();
    }

    private void HandleAmmoChanged(int currentAmmo, int magazineSize)
    {
        SetCurrentBulletsText(currentAmmo);
        SetMaxBulletsText(magazineSize);
    }

    private void SetCurrentBulletsText(int bullets)
    {
        if (currentBulletsText != null)
            currentBulletsText.text = bullets.ToString();
    }

    private void SetMaxBulletsText(int maxBullets)
    {
        if (maxBulletsText != null)
            maxBulletsText.text = maxBullets.ToString();
    }
}
