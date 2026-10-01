using UnityEngine;

public class CargoValueUI : MonoBehaviour
{
    [SerializeField] private QuotaManager quotaManager;
    [SerializeField] private RunManager runManager;
    [SerializeField] private TMPro.TextMeshProUGUI quotaText;
    [SerializeField] private string quotaFormat = "Total: {0}$";
    [Tooltip("Optional. When empty, the Canvas on this GameObject is toggled instead (hides text and background).")]
    [SerializeField] private GameObject visibilityRoot;

    private Canvas ownCanvas;

    private void Awake()
    {
        // Toggling the Canvas component hides every child graphic but keeps this script running.
        if (visibilityRoot == null)
            ownCanvas = GetComponent<Canvas>();

        if (visibilityRoot == null && ownCanvas == null && quotaText != null && quotaText.gameObject != gameObject)
            visibilityRoot = quotaText.gameObject;
    }

    private void OnEnable()
    {
        if (quotaManager == null)
            quotaManager = QuotaManager.Instance;

        if (runManager == null)
            runManager = RunManager.Instance;

        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged += Refresh;

        if (runManager != null)
            runManager.OnPhaseChanged += HandlePhaseChanged;

        Refresh();
        RefreshVisibility();
    }

    // RunManager may Awake after this UI.
    private void Start()
    {
        if (runManager != null)
            return;

        runManager = RunManager.Instance;
        if (runManager != null)
            runManager.OnPhaseChanged += HandlePhaseChanged;

        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged -= Refresh;

        if (runManager != null)
            runManager.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(RunPhase phase) => RefreshVisibility();

    private void RefreshVisibility()
    {
        bool visible = runManager != null && runManager.CurrentPhase == RunPhase.Town;

        if (visibilityRoot != null && visibilityRoot != gameObject)
            visibilityRoot.SetActive(visible);
        else if (ownCanvas != null)
            ownCanvas.enabled = visible;
    }

    private void Refresh()
    {
        if (quotaManager == null)
            return;

        if (quotaText != null)
        {
            quotaText.text = string.Format(
                quotaFormat,
                quotaManager.CurrentCargoValue
            );
        }
    }
}