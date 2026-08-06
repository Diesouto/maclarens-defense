using UnityEngine;

public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI moneyText;

    private void SetMoneyText(int money)
    {
        if (moneyText != null)
            moneyText.text = $"{money}$";
    }
}
