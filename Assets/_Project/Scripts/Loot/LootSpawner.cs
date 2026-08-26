using System.Collections.Generic;
using UnityEngine;

public class LootSpawner : MonoBehaviour
{
    public static LootSpawner Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private int maxActiveLoot = 15;
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<LootSpawnPoint> spawnPoints = new();

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
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
        SpawnUntilLimit();
    }

    private void SpawnUntilLimit()
    {
        int existingLoot =
            LootRegistry.Instance.GetExistingLootCount();

        int amountToSpawn =
            maxActiveLoot - existingLoot;

        if (amountToSpawn <= 0)
            return;

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
            availablePoints[
                Random.Range(0, availablePoints.Count)
            ];

        GameObject lootPrefab =
            spawnPoint.GetRandomLootPrefab();

        GameObject instance = Instantiate(
            lootPrefab,
            spawnPoint.transform.position,
            spawnPoint.transform.rotation
        );

        LootItem spawnedLoot = instance.GetComponent<LootItem>();

        spawnedLoot.SetSpawnPoint(spawnPoint);
        spawnPoint.SetOccupied(true);

        LootRegistry.Instance.Register(spawnedLoot);

        return true;
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