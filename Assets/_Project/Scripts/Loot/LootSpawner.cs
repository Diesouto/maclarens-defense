using System.Collections.Generic;
using UnityEngine;

public class LootSpawner : MonoBehaviour
{
    public static LootSpawner Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private int minActiveLoot = 12;
    [SerializeField] private int maxActiveLoot = 18;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float quotaToLootScale = 75f;

    [Header("Distribution")]
    [SerializeField, Min(0f)]
    private float minSpawnDistance = 8f;

    [SerializeField, Range(0f, 1f)]
    private float distanceWeight = 0.75f;

    private readonly List<LootSpawnPoint> spawnPoints = new();

    // Spawn points selected during the current spawning operation.
    private readonly List<LootSpawnPoint> recentlyUsedPoints = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        FindSpawnPoints();
    }

    private void Start()
    {
        if (spawnOnStart)
            InitialSpawn();

        if (RunManager.Instance != null)
            RunManager.Instance.OnDayChanged += HandleDayChanged;
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnDayChanged -= HandleDayChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Each new day tops up leftover loot instead of a full re-spawn, per the Roadmap P3 criteria.
    private void HandleDayChanged(int day)
    {
        Restock();
    }

    private void FindSpawnPoints()
    {
        spawnPoints.Clear();

        spawnPoints.AddRange(
            FindObjectsByType<LootSpawnPoint>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            )
        );

        Debug.Log(
            $"[LootSpawner] Found {spawnPoints.Count} spawn points."
        );
    }

    public void InitialSpawn()
    {
        if (LootRegistry.Instance == null)
        {
            Debug.LogError(
                "[LootSpawner] No LootRegistry found in the scene."
            );

            return;
        }

        SpawnUntilLimit();
    }

    public void Restock()
    {
        if (LootRegistry.Instance == null)
        {
            Debug.LogError(
                "[LootSpawner] No LootRegistry found in the scene."
            );

            return;
        }

        SpawnUntilLimit();
    }

    private void SpawnUntilLimit()
    {
        int targetCount = ResolveTargetLootCount();
        int existingLoot = LootRegistry.Instance.GetExistingLootCount();
        int amountToSpawn = Mathf.Max(targetCount - existingLoot, 0);

        if (amountToSpawn <= 0)
            return;

        recentlyUsedPoints.Clear();

        for (int i = 0; i < amountToSpawn; i++)
        {
            if (!TrySpawnLoot())
                break;
        }
    }

    private bool TrySpawnLoot()
    {
        List<LootSpawnPoint> availablePoints =
            GetAvailableSpawnPoints();

        if (availablePoints.Count == 0)
        {
            Debug.Log(
                "[LootSpawner] No available spawn points."
            );

            return false;
        }

        LootSpawnPoint spawnPoint =
            ChooseSpawnPoint(availablePoints);

        if (spawnPoint == null)
            return false;

        LootDataSO lootData =
            spawnPoint.GetRandomLootData();

        if (lootData == null)
        {
            Debug.LogWarning(
                $"[LootSpawner] Spawn point '{spawnPoint.name}' " +
                "has no valid loot option."
            );

            return false;
        }

        if (lootData.WorldPrefab == null)
        {
            Debug.LogWarning(
                $"[LootSpawner] Loot '{lootData.DisplayName}' " +
                "has no WorldPrefab assigned."
            );

            return false;
        }

        GameObject instance = Instantiate(
            lootData.WorldPrefab,
            spawnPoint.transform.position,
            spawnPoint.transform.rotation
        );

        LootItem spawnedLoot =
            instance.GetComponentInChildren<LootItem>(true);

        if (spawnedLoot == null)
        {
            spawnedLoot = instance.AddComponent<LootItem>();
            Debug.LogWarning(
                $"[LootSpawner] Prefab '{lootData.WorldPrefab.name}' " +
                "did not contain a LootItem component, so one was added automatically."
            );
        }

        spawnedLoot.SetData(lootData);
        spawnedLoot.SetSpawnPoint(spawnPoint);

        spawnPoint.SetOccupied(true);

        LootRegistry.Instance.Register(spawnedLoot);

        recentlyUsedPoints.Add(spawnPoint);

        return true;
    }

    private LootSpawnPoint ChooseSpawnPoint(
        List<LootSpawnPoint> availablePoints)
    {
        if (availablePoints.Count == 1)
            return availablePoints[0];

        // Calculate a score for every available point.
        // Points further away from recently used points get a higher score.

        float totalScore = 0f;

        List<float> scores = new();

        foreach (LootSpawnPoint point in availablePoints)
        {
            float score = CalculateSpawnPointScore(point);

            scores.Add(score);
            totalScore += score;
        }

        if (totalScore <= 0f)
        {
            return availablePoints[
                Random.Range(0, availablePoints.Count)
            ];
        }

        float randomValue = Random.Range(0f, totalScore);

        for (int i = 0; i < availablePoints.Count; i++)
        {
            randomValue -= scores[i];

            if (randomValue <= 0f)
                return availablePoints[i];
        }

        return availablePoints[availablePoints.Count - 1];
    }

    private float CalculateSpawnPointScore(
        LootSpawnPoint point)
    {
        if (recentlyUsedPoints.Count == 0)
            return 1f;

        float closestDistance = float.MaxValue;

        foreach (LootSpawnPoint usedPoint in recentlyUsedPoints)
        {
            if (usedPoint == null)
                continue;

            float distance = Vector3.Distance(
                point.transform.position,
                usedPoint.transform.position
            );

            if (distance < closestDistance)
                closestDistance = distance;
        }

        // If there is no valid comparison, treat the point normally.
        if (closestDistance == float.MaxValue)
            return 1f;

        if (closestDistance >= minSpawnDistance)
            return 1f;

        // Points closer than minSpawnDistance become less likely,
        // but they are NOT forbidden.
        float distanceRatio =
            closestDistance / minSpawnDistance;

        return Mathf.Lerp(
            1f - distanceWeight,
            1f,
            distanceRatio
        );
    }

    private int ResolveTargetLootCount()
    {
        int currentQuota = QuotaManager.Instance != null
            ? QuotaManager.Instance.CurrentQuota
            : 0;

        int quotaDrivenTarget = currentQuota > 0
            ? Mathf.CeilToInt(currentQuota / quotaToLootScale)
            : minActiveLoot;

        int targetCount = Mathf.Clamp(quotaDrivenTarget, minActiveLoot, maxActiveLoot);
        return Mathf.Max(targetCount, minActiveLoot);
    }

    private List<LootSpawnPoint> GetAvailableSpawnPoints()
    {
        List<LootSpawnPoint> availablePoints = new();

        foreach (LootSpawnPoint point in spawnPoints)
        {
            if (point == null)
                continue;

            if (!point.IsOccupied && point.HasLootOptions())
                availablePoints.Add(point);
        }

        return availablePoints;
    }

    public int SpawnPointCount => spawnPoints.Count;

    public int ActiveLootCount =>
        LootRegistry.Instance != null
            ? LootRegistry.Instance.GetExistingLootCount()
            : 0;
}