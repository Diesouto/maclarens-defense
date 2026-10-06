using UnityEngine;

// Carries the match setup chosen in MenuScene into MainScene. 0 quotas to win = infinite mode.
public static class RunSettings
{
    public const int MaximumQuotasToWin = 99;
    public const int InfiniteQuotas = 0;
    public const int DefaultQuotasToWin = 3;
    private const string QuotasToWinKey = "QuotasToWin";
    private const string QuotaMinutesKey = "QuotaMinutes";
    private const string StartingMoneyKey = "StartingMoney";
    private const string BaseQuotaKey = "BaseQuota";

    // 0 on the three overrides below = use the RunConfigSO / MoneyManager defaults.
    public const int MaximumQuotaMinutes = 60;
    public const int MaximumStartingMoney = 100000;
    public const int MaximumBaseQuota = 100000;

    private static bool hasValue;
    private static int quotasToWin;
    private static int quotaMinutes;
    private static int startingMoney;
    private static int baseQuota;

    public static bool HasValue => hasValue;
    public static int QuotasToWin => hasValue ? quotasToWin : SavedQuotasToWin;
    public static int SavedQuotasToWin => Sanitize(PlayerPrefs.GetInt(QuotasToWinKey, DefaultQuotasToWin));

    public static int QuotaMinutes => hasValue ? quotaMinutes : SavedQuotaMinutes;
    public static int StartingMoney => hasValue ? startingMoney : SavedStartingMoney;
    public static int BaseQuota => hasValue ? baseQuota : SavedBaseQuota;
    public static int SavedQuotaMinutes => SanitizeMinutes(PlayerPrefs.GetInt(QuotaMinutesKey, 0));
    public static int SavedStartingMoney => SanitizeMoney(PlayerPrefs.GetInt(StartingMoneyKey, 0));
    public static int SavedBaseQuota => SanitizeBaseQuota(PlayerPrefs.GetInt(BaseQuotaKey, 0));

    public static void SetQuotasToWin(int value)
    {
        quotasToWin = Sanitize(value);
        quotaMinutes = SavedQuotaMinutes;
        startingMoney = SavedStartingMoney;
        baseQuota = SavedBaseQuota;
        hasValue = true;
        PlayerPrefs.SetInt(QuotasToWinKey, quotasToWin);
        PlayerPrefs.Save();
    }

    public static void SetQuotaMinutes(int value) => PlayerPrefs.SetInt(QuotaMinutesKey, SanitizeMinutes(value));
    public static void SetStartingMoney(int value) => PlayerPrefs.SetInt(StartingMoneyKey, SanitizeMoney(value));
    public static void SetBaseQuota(int value) => PlayerPrefs.SetInt(BaseQuotaKey, SanitizeBaseQuota(value));

    // Commits the lobby's match setup for the run about to start.
    public static void SetMatchSetup(int quotasToWinValue, int minutes, int money, int quota)
    {
        SetQuotaMinutes(minutes);
        SetStartingMoney(money);
        SetBaseQuota(quota);
        SetQuotasToWin(quotasToWinValue);
    }

    public static int SanitizeMinutes(int value) => Mathf.Clamp(value, 0, MaximumQuotaMinutes);
    public static int SanitizeMoney(int value) => Mathf.Clamp(value, 0, MaximumStartingMoney);
    public static int SanitizeBaseQuota(int value) => Mathf.Clamp(value, 0, MaximumBaseQuota);

    public static int Sanitize(int value) => Mathf.Clamp(value, InfiniteQuotas, MaximumQuotasToWin);

    // Accepts only plain digits so "-3", "2.5" or "abc" are rejected rather than coerced.
    public static bool TryParse(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.Trim();
        foreach (char character in trimmed)
        {
            if (character < '0' || character > '9')
                return false;
        }

        if (!int.TryParse(trimmed, out int parsed))
            return false;

        value = Sanitize(parsed);
        return true;
    }

    public static string Describe(int value) => value == InfiniteQuotas ? "\u221E" : value.ToString();
}
