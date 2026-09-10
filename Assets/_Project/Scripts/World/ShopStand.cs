using UnityEngine;

// A MacLarens purchase point: hands the player a LootItem and withdraws its Price from TeamMoney.
public class ShopStand : MonoBehaviour, IInteractable
{
    [SerializeField] private LootDataSO lootData;
    [SerializeField] private RunManager runManager;
    [SerializeField] private MoneyManager moneyManager;

    private void Awake()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        if (moneyManager == null)
            moneyManager = MoneyManager.Instance;
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

        if (moneyManager == null || moneyManager.TeamMoney < lootData.Price)
            return false;

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        return inventory != null && inventory.CanAdd(lootData);
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        if (lootData == null)
            return "Buy";

        return $"Buy {lootData.DisplayName} (${lootData.Price:N0})";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.CanAdd(lootData))
            return;

        if (!moneyManager.TrySpendMoney(lootData.Price))
            return;

        if (!inventory.TryAdd(lootData))
            moneyManager.AddMoney(lootData.Price);
    }
}
