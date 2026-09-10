using UnityEngine;

public class QuotaUI : MonoBehaviour
{
    [SerializeField] private QuotaManager quotaManager;
    [SerializeField] private MoneyManager moneyManager;
    [SerializeField] private TMPro.TextMeshProUGUI quotaText;
    [SerializeField] private TMPro.TextMeshProUGUI statusText;

    [SerializeField] private string quotaFormat = "${0:N0} / ${1:N0}";
    [SerializeField] private string quotaMetMessage = "Quota reached";
    [SerializeField] private string quotaPendingMessage = "Quota pending";

    private void OnEnable()
    {
        if (quotaManager == null)
            quotaManager = QuotaManager.Instance;

        if (moneyManager == null)
            moneyManager = MoneyManager.Instance;

        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged += Refresh;

        if (moneyManager != null)
            moneyManager.OnMoneyChanged += Refresh;

        if (quotaManager != null)
            quotaManager.OnQuotaModifiersChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged -= Refresh;

        if (moneyManager != null)
            moneyManager.OnMoneyChanged -= Refresh;

        if (quotaManager != null)
            quotaManager.OnQuotaModifiersChanged -= Refresh;
    }

    private void Refresh()
    {
        if (quotaManager == null)
            return;

        if (quotaText != null)
        {
            quotaText.text = string.Format(
                quotaFormat,
                moneyManager != null ? moneyManager.TeamMoney : 0,
                quotaManager.EffectiveQuota
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