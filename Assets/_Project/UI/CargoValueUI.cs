using UnityEngine;

public class CargoValueUI : MonoBehaviour
{
    [SerializeField] private QuotaManager quotaManager;
    [SerializeField] private TrainCargo trainCargo;
    [SerializeField] private TMPro.TextMeshProUGUI quotaText;
    [SerializeField] private string quotaFormat = "{0} / {1}$";

    private void Awake()
    {
        if (quotaManager == null)
            quotaManager = QuotaManager.Instance;
    }

    private void OnEnable()
    {
        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged -= Refresh;
    }

    private void Refresh()
    {
        if (quotaManager == null)
            return;

        if (quotaText != null)
            quotaText.text = string.Format(quotaFormat, trainCargo.CargoValue, quotaManager.CurrentQuota);
    }
}
