using UnityEngine;

public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI moneyText;

    [SerializeField] private MoneyManager moneyManager;

    [Header("Count animation")]
    [Tooltip("Seconds the counter takes to travel from the old total to the new one.")]
    [SerializeField, Min(0f)] private float countDuration = 0.5f;
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color gainColor = new Color(0.35f, 0.9f, 0.4f);
    [SerializeField] private Color lossColor = new Color(0.95f, 0.35f, 0.35f);

    private int displayedMoney;
    private float countSpeed;
    private bool isCounting;
    private bool hasInitialized;

    private void OnEnable()
    {
        if (moneyManager == null)
            moneyManager = MoneyManager.Instance;

        if (moneyManager == null)
            return;

        moneyManager.OnMoneyChanged += Refresh;
        Refresh();
    }

    // OnEnable can run before MoneyManager.Awake sets the starting money (which raises no event).
    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (moneyManager != null)
            moneyManager.OnMoneyChanged -= Refresh;
    }

    private void Refresh()
    {
        int target = moneyManager != null ? moneyManager.TeamMoney : 0;

        if (countDuration <= 0f || !hasInitialized)
        {
            hasInitialized = true;
            displayedMoney = target;
            isCounting = false;
            SetMoneyText(displayedMoney, neutralColor);
            return;
        }

        int delta = target - displayedMoney;
        if (delta == 0)
        {
            isCounting = false;
            SetMoneyText(displayedMoney, neutralColor);
            return;
        }

        isCounting = true;
        countSpeed = Mathf.Abs(delta) / countDuration;
    }

    private void Update()
    {
        if (!isCounting)
            return;

        int target = moneyManager != null ? moneyManager.TeamMoney : 0;
        int delta = target - displayedMoney;
        if (delta == 0)
        {
            isCounting = false;
            SetMoneyText(displayedMoney, neutralColor);
            return;
        }

        // Always advance at least one unit so tiny deltas still resolve.
        int step = Mathf.Max(1, Mathf.RoundToInt(countSpeed * Time.deltaTime));
        displayedMoney += delta > 0 ? Mathf.Min(step, delta) : -Mathf.Min(step, -delta);
        SetMoneyText(displayedMoney, delta > 0 ? gainColor : lossColor);
    }

    private void SetMoneyText(int money, Color color)
    {
        if (moneyText == null)
            return;

        moneyText.text = $"${money:N0}";
        moneyText.color = color;
    }
}
