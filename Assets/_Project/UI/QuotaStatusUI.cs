using TMPro;
using UnityEngine;

// Lives on the Pay Quota entity: always-current stats, independent from Owner dialogue.
public class QuotaStatusUI : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [SerializeField] private QuotaManager quotaManager;
    [SerializeField] private MoneyManager moneyManager;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("{0} = quota round, {1} = quotas to win (or \u221E), {2} = team money, {3} = quota.")]
    [SerializeField] private string quotaStatusFormat = "Quota {0} / {1}\nMoney ${2:N0}\nQuota ${3:N0}";

    private void OnEnable()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        if (quotaManager == null)
            quotaManager = QuotaManager.Instance;

        if (moneyManager == null)
            moneyManager = MoneyManager.Instance;

        if (runManager != null)
            runManager.OnRunStateChanged += Refresh;

        if (moneyManager != null)
            moneyManager.OnMoneyChanged += Refresh;

        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged += Refresh;

        Refresh();
    }

    // OnEnable can run before the managers' Awake initializes money and quota.
    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.OnRunStateChanged -= Refresh;

        if (moneyManager != null)
            moneyManager.OnMoneyChanged -= Refresh;

        if (quotaManager != null)
            quotaManager.OnQuotaProgressChanged -= Refresh;
    }

    private void Refresh()
    {
        if (statusText == null)
            return;

        int round = runManager != null ? runManager.CurrentQuotaRound : 0;
        string target = runManager != null ? RunSettings.Describe(runManager.QuotasToWin) : "-";
        int money = moneyManager != null ? moneyManager.TeamMoney : 0;
        int quota = quotaManager != null ? quotaManager.EffectiveQuota : 0;

        statusText.text = string.Format(quotaStatusFormat, round, target, money, quota);
    }
}
