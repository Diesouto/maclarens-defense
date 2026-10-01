using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuotaModifier
{
    public int Amount;
    public string Reason;

    public QuotaModifier(int amount, string reason)
    {
        Amount = amount;
        Reason = reason;
    }
}

public class QuotaManager : MonoBehaviour
{
    public static QuotaManager Instance { get; private set; }

    [SerializeField] private int currentQuota = 100;

    public int CurrentQuota => currentQuota;
    public int EffectiveQuota => Mathf.Max(currentQuota + GetModifierTotal(), 0);
    public IReadOnlyList<QuotaModifier> Modifiers => modifiers;

    // Value currently stored across ALL train cargos.
    public int CurrentCargoValue { get; private set; }

    // Value delivered to the delivery point during the current quota round.
    public int DeliveredValue { get; private set; }

    public bool QuotaMet => MoneyManager.Instance != null &&
        MoneyManager.Instance.TeamMoney >= EffectiveQuota;

    public event Action OnQuotaProgressChanged;
    public event Action OnQuotaMet;
    public event Action OnQuotaModifiersChanged;

    private bool quotaWasMet;
    private readonly List<QuotaModifier> modifiers = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentQuota = Mathf.Max(currentQuota, 0);
    }

    private void Start()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.ApplyCurrentQuota();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Cargo is left untouched: loot still aboard the train belongs to the next round too.
    public void SetQuota(int quota)
    {
        if (IsNetworkClient())
            return;

        currentQuota = Mathf.Max(quota, 0);

        DeliveredValue = 0;
        quotaWasMet = false;

        OnQuotaProgressChanged?.Invoke();

        if (currentQuota == 0)
            SetQuotaMet();
    }

    public bool TryPayCurrentQuota(MoneyManager moneyManager)
    {
        if (IsNetworkClient())
            return false;

        if (moneyManager == null || !moneyManager.TrySpendMoney(EffectiveQuota))
            return false;

        ClearQuotaModifiers();
        return true;
    }

    public void AddQuotaModifier(int amount, string reason)
    {
        if (IsNetworkClient())
            return;

        if (amount == 0)
            return;

        modifiers.Add(new QuotaModifier(amount, reason));
        OnQuotaModifiersChanged?.Invoke();
        OnQuotaProgressChanged?.Invoke();
    }

    public void ClearQuotaModifiers()
    {
        if (IsNetworkClient())
            return;

        if (modifiers.Count == 0)
            return;

        modifiers.Clear();
        OnQuotaModifiersChanged?.Invoke();
        OnQuotaProgressChanged?.Invoke();
    }

    public void AddCargoValue(int value)
    {
        if (IsNetworkClient())
            return;

        if (value == 0)
            return;

        CurrentCargoValue = Mathf.Max(CurrentCargoValue + value, 0);

        OnQuotaProgressChanged?.Invoke();
    }

    public void AddDeliveredValue(int value)
    {
        if (IsNetworkClient())
            return;

        if (value <= 0)
            return;

        DeliveredValue += value;

        OnQuotaProgressChanged?.Invoke();

        if (!quotaWasMet && DeliveredValue >= CurrentQuota)
            SetQuotaMet();
    }

    private void SetQuotaMet()
    {
        if (quotaWasMet)
            return;

        quotaWasMet = true;
        OnQuotaMet?.Invoke();
    }

    private int GetModifierTotal()
    {
        int total = 0;

        foreach (QuotaModifier modifier in modifiers)
            total += modifier.Amount;

        return total;
    }

    // Clients mirror the host's effective quota directly; modifiers themselves stay host-side.
    public void ApplyReplicatedState(int effectiveQuota, int cargoValue, int deliveredValue)
    {
        currentQuota = Mathf.Max(effectiveQuota, 0);
        CurrentCargoValue = cargoValue;
        DeliveredValue = deliveredValue;

        OnQuotaProgressChanged?.Invoke();

        if (!quotaWasMet && currentQuota > 0 && DeliveredValue >= currentQuota)
            SetQuotaMet();
    }

    private static bool IsNetworkClient() => NetworkRole.IsClientOnly;
}