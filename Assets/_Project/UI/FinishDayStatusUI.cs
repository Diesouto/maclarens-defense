using TMPro;
using UnityEngine;

// Lives on the Finish Day entity: always-current stats, independent from Owner dialogue.
public class FinishDayStatusUI : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private QuotaManager quotaManager;
    [SerializeField] private MoneyManager moneyManager;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string statusFormat = "Day {0} / {1}\nMoney ${2:N0}\nDebt ${3:N0}\nQuota ${4:N0}";

    private void OnEnable()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        if (quotaManager == null)
            quotaManager = QuotaManager.Instance;

        if (moneyManager == null)
            moneyManager = MoneyManager.Instance;

        if (runManager != null)
            runManager.OnDayChanged += HandleDayChanged;

        if (moneyManager != null)
            moneyManager.OnMoneyChanged += Refresh;

        if (quotaManager != null)
        {
            quotaManager.OnQuotaProgressChanged += Refresh;
            quotaManager.OnDebtChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.OnDayChanged -= HandleDayChanged;

        if (moneyManager != null)
            moneyManager.OnMoneyChanged -= Refresh;

        if (quotaManager != null)
        {
            quotaManager.OnQuotaProgressChanged -= Refresh;
            quotaManager.OnDebtChanged -= Refresh;
        }
    }

    private void HandleDayChanged(int day)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (statusText == null)
            return;

        int day = runManager != null ? runManager.CurrentDay : 0;
        int totalDays = runManager != null ? runManager.TotalDays : 0;
        int money = moneyManager != null ? moneyManager.TeamMoney : 0;
        int debt = quotaManager != null ? quotaManager.DebtRemaining : 0;
        int quota = quotaManager != null ? quotaManager.EffectiveQuota : 0;

        statusText.text = string.Format(statusFormat, day, totalDays, money, debt, quota);
    }
}
