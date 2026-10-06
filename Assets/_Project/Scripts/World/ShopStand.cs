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

    // Resolved lazily: a stand without scene references can Awake before the manager singletons.
    private MoneyManager Money => moneyManager != null ? moneyManager : moneyManager = MoneyManager.Instance;
    private RunManager Run => runManager != null ? runManager : runManager = RunManager.Instance;
    private bool IsShopOpen => Run == null || Run.CurrentPhase == RunPhase.MacLarens;

    private void Awake()
    {
        LootCatalog.Register(lootData);

        if (networkPurchaseAuthority == null)
            networkPurchaseAuthority = GetComponent<NetworkPurchaseAuthority>();
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (lootData == null || interactor == null)
            return false;

        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return false;

        if (!IsShopOpen)
            return false;

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null)
            return false;

        if (IsAmmoRefill(inventory))
            return Money != null && Money.TeamMoney >= AmmoRefillPrice;

        if (Money == null || Money.TeamMoney < lootData.Price)
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
            return "Comprar";

        PlayerInventory inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
        if (IsAmmoRefill(inventory))
        {
            if (!inventory.ActiveWeaponNeedsAmmo(lootData))
                return $"Munición {lootData.DisplayName} - está llena";

            return Money != null && Money.TeamMoney < AmmoRefillPrice
                ? $"Munición {lootData.DisplayName} (${AmmoRefillPrice:N0}) - no hay suficiente dinero"
                : $"Munición {lootData.DisplayName} (${AmmoRefillPrice:N0})";
        }

        string buyText = $"Comprar {lootData.DisplayName} (${lootData.Price:N0})";

        if (Money != null && Money.TeamMoney < lootData.Price)
            return $"{buyText} - no hay suficiente dinero";

        if (inventory != null && !inventory.CanAdd(lootData))
            return $"{buyText} - sin espacio";

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

        if (!IsShopOpen || Money == null)
            return false;

        if (IsAmmoRefill(inventory))
        {
            if (!inventory.ActiveWeaponNeedsAmmo(lootData) || !Money.TrySpendMoney(AmmoRefillPrice))
                return false;

            refilledAmmo = inventory.TryRefillActiveWeapon();
            return refilledAmmo;
        }

        if (inventory == null || !inventory.CanAdd(lootData))
            return false;

        if (!Money.TrySpendMoney(lootData.Price))
            return false;

        if (!inventory.TryAdd(lootData))
        {
            Money.AddMoney(lootData.Price);
            return false;
        }

        return true;
    }
}
