using UnityEngine;

// Carries the match setup chosen in MenuScene into MainScene. 0 quotas to win = infinite mode.
public static class RunSettings
{
    public const int MaximumQuotasToWin = 99;
    public const int InfiniteQuotas = 0;
    public const int DefaultQuotasToWin = 3;
    private const string QuotasToWinKey = "QuotasToWin";

    private static bool hasValue;
    private static int quotasToWin;

    public static bool HasValue => hasValue;
    public static int QuotasToWin => hasValue ? quotasToWin : SavedQuotasToWin;
    public static int SavedQuotasToWin => Sanitize(PlayerPrefs.GetInt(QuotasToWinKey, DefaultQuotasToWin));

    public static void SetQuotasToWin(int value)
    {
        quotasToWin = Sanitize(value);
        hasValue = true;
        PlayerPrefs.SetInt(QuotasToWinKey, quotasToWin);
        PlayerPrefs.Save();
    }

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
