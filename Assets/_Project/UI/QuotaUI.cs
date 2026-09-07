using UnityEngine;

public class QuotaUI : MonoBehaviour
{
    [SerializeField] private QuotaManager quotaManager;
    [SerializeField] private TMPro.TextMeshProUGUI quotaText;
    [SerializeField] private TMPro.TextMeshProUGUI statusText;

    [SerializeField] private string quotaFormat = "{0} / {1}$";
    [SerializeField] private string quotaMetMessage = "Quota reached";
    [SerializeField] private string quotaPendingMessage = "Quota pending";

    private void OnEnable()
    {
        if (quotaManager == null)
            quotaManager = QuotaManager.Instance;

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
        {
            quotaText.text = string.Format(
                quotaFormat,
                quotaManager.DeliveredValue,
                quotaManager.CurrentQuota
            );
        }

        if (statusText != null)
        {
            statusText.text = quotaManager.QuotaMet
                ? quotaMetMessage
                : quotaPendingMessage;
        }
    }
}