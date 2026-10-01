using UnityEngine;

// A MacLarens purchase point: hands the player a LootItem and withdraws its Price from TeamMoney.
public class ShopStand : MonoBehaviour, IInteractable
{
    [SerializeField] private LootDataSO lootData;
    [SerializeField] private RunManager runManager;
    [SerializeField] private MoneyManager moneyManager;
    [SerializeField] private NetworkPurchaseAuthority networkPurchaseAuthority;
    [Tooltip("Fraction of the weapon price charged to refill it when the player already holds it.")]
    [SerializeField, Range(0f, 1f)] private float ammoRefillPriceMultiplier = 0.5f;

    private int AmmoRefillPrice => Mathf.CeilToInt(lootData.Price * ammoRefillPriceMultiplier);

    private void Awake()
    {
        LootCatalog.Register(lootData);

        if (runManager == null)
            runManager = RunManager.Instance;

        if (moneyManager == null)
            moneyManager = MoneyManager.Instance;

        if (networkPurchaseAuthority == null)
            networkPurchaseAuthority = GetComponent<NetworkPurchaseAuthority>();
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (lootData == null || interactor == null)
            return false;

        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return false;

        if (runManager != null &&
            runManager.CurrentPhase != RunPhase.MacLarens &&
            runManager.CurrentPhase != RunPhase.ResolvingDay)
        {
            return false;
        }

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null)
            return false;

        if (IsAmmoRefill(inventory))
            return moneyManager != null && moneyManager.TeamMoney >= AmmoRefillPrice;

        if (moneyManager == null || moneyManager.TeamMoney < lootData.Price)
            return false;

        return inventory.CanAdd(lootData);
    }

    // Holding this stand's own gun turns the purchase into a cheaper ammo refill.
    private bool IsAmmoRefill(PlayerInventory inventory)
    {
        return lootData != null && lootData.IsWeapon && inventory != null && inventory.ActiveItem == lootData;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        if (lootData == null)
            return "Buy";

        PlayerInventory inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
        if (IsAmmoRefill(inventory))
        {
            if (!inventory.ActiveWeaponNeedsAmmo(lootData))
                return $"{lootData.DisplayName} ammo is full";

            return moneyManager != null && moneyManager.TeamMoney < AmmoRefillPrice
                ? $"Refill {lootData.DisplayName} ammo (${AmmoRefillPrice:N0}) - not enough money"
                : $"Refill {lootData.DisplayName} ammo (${AmmoRefillPrice:N0})";
        }

        string buyText = $"Buy {lootData.DisplayName} (${lootData.Price:N0})";

        if (moneyManager != null && moneyManager.TeamMoney < lootData.Price)
            return $"{buyText} - not enough money";

        if (inventory != null && !inventory.CanAdd(lootData))
            return $"{buyText} - no space";

        return buyText;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (networkPurchaseAuthority != null && networkPurchaseAuthority.IsSpawned &&
            !networkPurchaseAuthority.IsServer)
        {
            networkPurchaseAuthority.RequestPurchaseServerRpc();
            return;
        }

        if (!CanInteract(interactor))
            return;

        TryPurchase(interactor != null ? interactor.GetComponent<PlayerInventory>() : null, out _);
    }

    public bool TryPurchase(PlayerInventory inventory, out bool refilledAmmo)
    {
        refilledAmmo = false;

        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return false;

        if (runManager != null && runManager.CurrentPhase != RunPhase.MacLarens &&
            runManager.CurrentPhase != RunPhase.ResolvingDay)
            return false;

        if (IsAmmoRefill(inventory))
        {
            if (!inventory.ActiveWeaponNeedsAmmo(lootData) || !moneyManager.TrySpendMoney(AmmoRefillPrice))
                return false;

            refilledAmmo = inventory.TryRefillActiveWeapon();
            return refilledAmmo;
        }

        if (inventory == null || !inventory.CanAdd(lootData))
            return false;

        if (!moneyManager.TrySpendMoney(lootData.Price))
            return false;

        if (!inventory.TryAdd(lootData))
        {
            moneyManager.AddMoney(lootData.Price);
            return false;
        }

        return true;
    }
}
