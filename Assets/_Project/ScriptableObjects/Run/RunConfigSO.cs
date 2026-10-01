using UnityEngine;

// Static tuning for the timed-quota loop. Live run state belongs to RunManager/QuotaManager.
[CreateAssetMenu(fileName = "RunConfig", menuName = "MacLarens/Run Config")]
public class RunConfigSO : ScriptableObject
{
    [Header("Quotas")]
    [Tooltip("Quota amount for each round. Rounds past the end keep growing by infiniteGrowth.")]
    [SerializeField] private int[] quotaAmounts = { 300, 600, 1000 };
    [Tooltip("Multiplier applied per round once quotaAmounts runs out (infinite mode or long runs).")]
    [SerializeField, Min(1f)] private float infiniteGrowth = 1.35f;

    [Header("Time")]
    [Tooltip("Seconds available for each quota round. Rounds past the end reuse the last entry.")]
    [SerializeField] private float[] quotaTimeLimits = { 600f, 720f, 840f };
    [Tooltip("Fraction of the time left when paying early that carries over to the next quota. 0 disables.")]
    [SerializeField, Range(0f, 1f)] private float timeCarryOverFraction = 0.5f;

    [Header("Win condition")]
    [Tooltip("Default quotas to win when nothing was chosen in the menu. 0 = infinite.")]
    [SerializeField, Range(0, RunSettings.MaximumQuotasToWin)] private int defaultQuotasToWin = 3;

    public float TimeCarryOverFraction => timeCarryOverFraction;
    public int DefaultQuotasToWin => defaultQuotasToWin;

    // round is 1-based.
    public int GetQuota(int round)
    {
        if (quotaAmounts == null || quotaAmounts.Length == 0)
            return 0;

        int index = Mathf.Max(round - 1, 0);
        if (index < quotaAmounts.Length)
            return Mathf.Max(quotaAmounts[index], 0);

        int lastQuota = Mathf.Max(quotaAmounts[quotaAmounts.Length - 1], 0);
        int extraRounds = index - (quotaAmounts.Length - 1);
        return Mathf.RoundToInt(lastQuota * Mathf.Pow(infiniteGrowth, extraRounds));
    }

    public float GetTimeLimit(int round)
    {
        if (quotaTimeLimits == null || quotaTimeLimits.Length == 0)
            return 600f;

        int index = Mathf.Clamp(round - 1, 0, quotaTimeLimits.Length - 1);
        return Mathf.Max(quotaTimeLimits[index], 1f);
    }
}
