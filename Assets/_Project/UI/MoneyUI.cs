using UnityEngine;

public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI moneyText;

    [SerializeField] private MoneyManager moneyManager;

    private void OnEnable()
    {
        if (moneyManager == null)
            moneyManager = MoneyManager.Instance;

        if (moneyManager == null)
            return;

        moneyManager.OnMoneyChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (moneyManager != null)
            moneyManager.OnMoneyChanged -= Refresh;
    }

    private void Refresh()
    {
        SetMoneyText(moneyManager != null ? moneyManager.TeamMoney : 0);
    }

    private void SetMoneyText(int money)
    {
        if (moneyText != null)
            moneyText.text = $"${money:N0}";
    }
}
