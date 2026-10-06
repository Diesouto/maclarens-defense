using UnityEngine;

// MacLarens counter where the crew explicitly pays the current quota from TeamMoney.
public class PayQuotaInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private NetworkTrainState networkTrainState;

    private RunManager Run => runManager != null ? runManager : runManager = RunManager.Instance;

    private void Awake()
    {
        if (networkTrainState == null)
            networkTrainState = FindFirstObjectByType<NetworkTrainState>();
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return Run != null && Run.CanPayQuota;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        int quota = QuotaManager.Instance != null ? QuotaManager.Instance.EffectiveQuota : 0;
        string payText = $"Pagar deuda (${quota:N0})";

        if (Run != null && Run.CurrentPhase != RunPhase.MacLarens)
            return $"{payText} - el tren no está en el MacLarens";

        if (QuotaManager.Instance != null && !QuotaManager.Instance.QuotaMet)
            return $"{payText} - no hay suficiente dinero";

        return payText;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (networkTrainState != null && networkTrainState.IsSpawned)
            networkTrainState.RequestPayQuota();
        else
            Run.PayQuota();
    }
}
