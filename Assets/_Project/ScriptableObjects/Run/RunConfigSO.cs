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

    [Header("Player count scaling")]
    [Tooltip("One entry per team size (1-4); 1 player is the baseline.")]
    [SerializeField] private PlayerScaling[] playerScaling =
    {
        new PlayerScaling { playerCount = 1, quota = 1f, enemyCap = 1f, spawnInterval = 1f, threat = 1f },
        new PlayerScaling { playerCount = 2, quota = 1.7f, enemyCap = 1.5f, spawnInterval = 0.85f, threat = 1.15f },
        new PlayerScaling { playerCount = 3, quota = 2.3f, enemyCap = 2f, spawnInterval = 0.75f, threat = 1.3f },
        new PlayerScaling { playerCount = 4, quota = 2.9f, enemyCap = 2.5f, spawnInterval = 0.65f, threat = 1.45f },
    };

    [System.Serializable]
    public struct PlayerScaling
    {
        [Range(1, 4)] public int playerCount;
        [Min(0f)] public float quota;
        [Min(0f)] public float enemyCap;
        [Min(0.01f)] public float spawnInterval;
        [Min(0f)] public float threat;
    }

    public float TimeCarryOverFraction => timeCarryOverFraction;
    public int DefaultQuotasToWin => defaultQuotasToWin;

    public PlayerScaling GetPlayerScaling(int players)
    {
        int count = Mathf.Clamp(players, 1, 4);
        if (playerScaling != null)
        {
            foreach (PlayerScaling entry in playerScaling)
            {
                if (entry.playerCount == count)
                    return entry;
            }
        }

        return new PlayerScaling { playerCount = count, quota = 1f, enemyCap = 1f, spawnInterval = 1f, threat = 1f };
    }

    // round is 1-based.
    public int GetQuota(int round, int players = 1)
    {
        if (quotaAmounts == null || quotaAmounts.Length == 0)
            return 0;

        int index = Mathf.Max(round - 1, 0);
        float baseQuota;
        if (index < quotaAmounts.Length)
        {
            baseQuota = Mathf.Max(quotaAmounts[index], 0);
        }
        else
        {
            int lastQuota = Mathf.Max(quotaAmounts[quotaAmounts.Length - 1], 0);
            int extraRounds = index - (quotaAmounts.Length - 1);
            baseQuota = lastQuota * Mathf.Pow(infiniteGrowth, extraRounds);
        }

        return Mathf.RoundToInt(baseQuota * GetPlayerScaling(players).quota);
    }

    public float GetTimeLimit(int round)
    {
        if (quotaTimeLimits == null || quotaTimeLimits.Length == 0)
            return 600f;

        int index = Mathf.Clamp(round - 1, 0, quotaTimeLimits.Length - 1);
        return Mathf.Max(quotaTimeLimits[index], 1f);
    }
}
