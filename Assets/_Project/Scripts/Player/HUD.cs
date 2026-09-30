using UnityEngine;

// Wires the scene HUD to whichever player is local on this machine (host, client or offline).
public class HUD : MonoBehaviour
{
    [SerializeField] private HealthUI healthUI;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private BulletsUI bulletsUI;
    [SerializeField] private InteractUI interactUI;
    [SerializeField] private DamageFeedback damageFeedback;

    private void Awake()
    {
        if (damageFeedback == null)
            damageFeedback = GetComponentInChildren<DamageFeedback>(true);

        if (healthUI == null)
            healthUI = GetComponentInChildren<HealthUI>(true);

        if (inventoryUI == null)
            inventoryUI = GetComponentInChildren<InventoryUI>(true);

        if (bulletsUI == null)
            bulletsUI = GetComponentInChildren<BulletsUI>(true);

        if (interactUI == null)
            interactUI = GetComponentInChildren<InteractUI>(true);
    }

    private void OnEnable()
    {
        NetworkPlayer.LocalPlayerChanged += Initialize;

        if (NetworkPlayer.Local != null)
            Initialize(NetworkPlayer.Local);
    }

    private void OnDisable()
    {
        NetworkPlayer.LocalPlayerChanged -= Initialize;
    }

    public void Initialize(NetworkPlayer localPlayer)
    {
        if (localPlayer == null)
            return;

        PlayerInventory inventory = localPlayer.GetComponent<PlayerInventory>();

        Debug.Log($"HUD: binding to '{localPlayer.name}' (health UI: {healthUI != null}, inventory UI: {inventoryUI != null}, " +
            $"bullets UI: {bulletsUI != null}, interact UI: {interactUI != null}, inventory: {inventory != null}).", this);

        healthUI?.Bind(localPlayer.GetComponent<Health>());
        inventoryUI?.Bind(inventory);
        bulletsUI?.Bind(inventory);

        if (damageFeedback != null)
        {
            localPlayer.TryGetComponent(out PlayerController playerController);
            damageFeedback.Bind(
                localPlayer.GetComponent<Health>(),
                playerController != null ? playerController.CameraTransform : null);
        }

        if (interactUI == null)
            return;

        if (localPlayer.TryGetComponent(out PlayerInteractor interactor))
            interactor.SetInteractUI(interactUI);

        if (localPlayer.TryGetComponent(out PlayerController controller))
            controller.SetInteractUI(interactUI);
    }
}