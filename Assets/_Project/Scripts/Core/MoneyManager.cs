using System;
using UnityEngine;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [SerializeField, Min(0)] private int startingTeamMoney;

    public int TeamMoney { get; private set; }

    public event Action OnMoneyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        TeamMoney = Mathf.Max(startingTeamMoney, 0);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        TeamMoney += amount;
        OnMoneyChanged?.Invoke();
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0 || TeamMoney < amount)
            return false;

        if (amount == 0)
            return true;

        TeamMoney -= amount;
        OnMoneyChanged?.Invoke();
        return true;
    }

    public void ResetMoney()
    {
        TeamMoney = Mathf.Max(startingTeamMoney, 0);
        OnMoneyChanged?.Invoke();
    }
}
