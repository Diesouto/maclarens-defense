using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Serializable]
    public class ThreatSpawnSettings
    {
        public ThreatLevel level = ThreatLevel.Calm;
        public int maxAliveEnemies = 2;
        public float spawnInterval = 8f;
    }

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Spawn Points")]
    [SerializeField] private EnemySpawnPoint[] spawnPoints;

    [Header("Threat Intensity")]
    [SerializeField] private ThreatSpawnSettings[] intensityLevels =
    {
        new ThreatSpawnSettings { level = ThreatLevel.Calm, maxAliveEnemies = 1, spawnInterval = 10f },
        new ThreatSpawnSettings { level = ThreatLevel.Low, maxAliveEnemies = 3, spawnInterval = 8f },
        new ThreatSpawnSettings { level = ThreatLevel.Medium, maxAliveEnemies = 5, spawnInterval = 6f },
        new ThreatSpawnSettings { level = ThreatLevel.High, maxAliveEnemies = 7, spawnInterval = 4.5f },
        new ThreatSpawnSettings { level = ThreatLevel.Critical, maxAliveEnemies = 10, spawnInterval = 3.5f },
    };

    [Header("Spawn Settings")]
    [SerializeField] private bool spawnOnStart = true;
    [Tooltip("Enemies placed across town when the train heads there (same moment loot restocks); they don't count toward the threat cap.")]
    [SerializeField, Min(0)] private int townResidentEnemies = 6;

    [Header("Ghost")]
    [Tooltip("Immortal ghost (GhostController) that only appears once threat is high; only a thrown cross banishes it.")]
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private ThreatLevel ghostMinimumLevel = ThreatLevel.High;
    [SerializeField, Min(0)] private int maxGhosts = 1;
    [Tooltip("Seconds at or above the minimum threat level before each ghost appears.")]
    [SerializeField, Min(0f)] private float ghostSpawnDelay = 30f;
    [SerializeField] private float ghostSpawnHeight = 1f;

    private float ghostTimer;
    private readonly List<EnemyController> aliveEnemies = new();
    private readonly HashSet<EnemyController> residentEnemies = new();
    private RunManager runManager;

    private float spawnTimer;

    private void Start()
    {
        if (spawnOnStart)
            spawnTimer = ScaledInterval(GetCurrentSettings());

        ghostTimer = ghostSpawnDelay;

        runManager = RunManager.Instance;
        if (runManager != null)
            runManager.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDestroy()
    {
        if (runManager != null)
            runManager.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.TravelingToTown && !NetworkRole.IsClientOnly)
            SpawnTownResidents();
    }

    // Populates the town regardless of threat so it never feels empty; threat only adds pressure on top.
    private void SpawnTownResidents()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return;

        var candidates = new List<EnemySpawnPoint>();
        foreach (EnemySpawnPoint point in spawnPoints)
        {
            if (point != null && point.gameObject.activeInHierarchy)
                candidates.Add(point);
        }

        CleanupDeadEnemies();
        int toSpawn = Mathf.Min(townResidentEnemies - residentEnemies.Count, candidates.Count);
        for (int i = 0; i < toSpawn; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            EnemySpawnPoint point = candidates[index];
            candidates.RemoveAt(index);

            EnemyController resident = SpawnAt(point);
            if (resident != null)
            {
                resident.MarkAsResident();
                residentEnemies.Add(resident);
            }
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return;

        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return;

        CleanupDeadEnemies();
        UpdateGhostSpawning();

        ThreatSpawnSettings settings = GetCurrentSettings();

        if (aliveEnemies.Count - residentEnemies.Count >= ScaledCap(settings))
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            TrySpawnEnemy();
            spawnTimer = ScaledInterval(settings);
        }
    }

    private static int ScaledCap(ThreatSpawnSettings settings)
    {
        float multiplier = RunManager.Instance != null ? RunManager.Instance.Scaling.enemyCap : 1f;
        return Mathf.Max(Mathf.RoundToInt(settings.maxAliveEnemies * multiplier), 1);
    }

    private static float ScaledInterval(ThreatSpawnSettings settings)
    {
        float multiplier = RunManager.Instance != null ? RunManager.Instance.Scaling.spawnInterval : 1f;
        return settings.spawnInterval * multiplier;
    }

    private void UpdateGhostSpawning()
    {
        if (ghostPrefab == null || GhostController.ActiveCount >= maxGhosts)
            return;

        ThreatLevel level = ThreatManager.Instance != null ? ThreatManager.Instance.CurrentLevel : ThreatLevel.Calm;
        if (level < ghostMinimumLevel)
        {
            ghostTimer = ghostSpawnDelay;
            return;
        }

        ghostTimer -= Time.deltaTime;
        if (ghostTimer > 0f)
            return;

        EnemySpawnPoint point = GetBestSpawnPoint();
        if (point == null)
            return;

        ghostTimer = ghostSpawnDelay;
        GameObject ghost = Instantiate(ghostPrefab, point.Position + Vector3.up * ghostSpawnHeight, point.transform.rotation);
        point.MarkUsed();

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        if (!ghost.TryGetComponent(out NetworkObject networkObject) ||
            !NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(ghostPrefab))
        {
            Destroy(ghost);
            Debug.LogError($"{ghostPrefab.name} must have a registered NetworkObject while networking is active.", ghostPrefab);
            return;
        }

        networkObject.Spawn(true);
    }

    private ThreatSpawnSettings GetCurrentSettings()
    {
        ThreatLevel currentLevel = ThreatManager.Instance != null
            ? ThreatManager.Instance.CurrentLevel
            : ThreatLevel.Calm;

        if (intensityLevels != null)
        {
            foreach (ThreatSpawnSettings settings in intensityLevels)
            {
                if (settings != null && settings.level == currentLevel)
                    return settings;
            }
        }

        return new ThreatSpawnSettings();
    }

    private void TrySpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning($"{name}: No enemy prefabs configured.", this);
            return;
        }

        EnemySpawnPoint spawnPoint = GetBestSpawnPoint();

        if (spawnPoint == null)
            return;

        SpawnAt(spawnPoint);
    }

    private EnemyController SpawnAt(EnemySpawnPoint spawnPoint)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return null;

        GameObject prefab = enemyPrefabs[
            UnityEngine.Random.Range(0, enemyPrefabs.Length)
        ];

        Vector3 spawnPosition = spawnPoint.Position;

        // Make sure the position is actually on the NavMesh.
        if (!NavMesh.SamplePosition(
                spawnPosition,
                out NavMeshHit navHit,
                2f,
                NavMesh.AllAreas))
        {
            Debug.LogWarning(
                $"{name}: Spawn point '{spawnPoint.name}' has no valid NavMesh nearby.",
                spawnPoint
            );

            return null;
        }

        GameObject instance = Instantiate(
            prefab,
            navHit.position,
            spawnPoint.transform.rotation
        );

        EnemyController enemy = instance.GetComponent<EnemyController>();

        if (enemy == null)
        {
            Debug.LogWarning(
                $"{prefab.name} does not contain an EnemyController.",
                prefab
            );

            Destroy(instance);
            return null;
        }

        aliveEnemies.Add(enemy);
        spawnPoint.MarkUsed();

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (networkObject == null ||
                !NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(prefab))
            {
                Destroy(instance);
                aliveEnemies.Remove(enemy);
                Debug.LogError($"{prefab.name} must have a registered NetworkObject while networking is active.", prefab);
                return null;
            }

            if (!networkObject.IsSpawned)
                networkObject.Spawn(true);
        }

        return enemy;
    }

    // Picks the valid point farthest from any player, so enemies feel like they come from the town, not thin air.
    private EnemySpawnPoint GetBestSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"{name}: No spawn points configured.", this);
            return null;
        }

        EnemySpawnPoint best = null;
        float bestScore = float.NegativeInfinity;

        foreach (EnemySpawnPoint point in spawnPoints)
        {
            if (point == null || !point.IsValid())
                continue;

            // TEMP playtest: random valid point instead of the one farthest from every player.
            // float score = point.GetScore();
            float score = UnityEngine.Random.value;
            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    private void CleanupDeadEnemies()
    {
        // Dead enemies linger as ragdolls for a while; they must not keep occupying spawn slots.
        aliveEnemies.RemoveAll(enemy =>
            enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy
        );
        residentEnemies.RemoveWhere(enemy => enemy == null || enemy.IsDead);
    }

    public int AliveEnemyCount => aliveEnemies.Count;

    public void DespawnAll()
    {
        foreach (EnemyController enemy in aliveEnemies)
        {
            if (enemy != null)
            {
                NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned &&
                    NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    networkObject.Despawn(true);
                else
                    Destroy(enemy.gameObject);
            }
        }

        aliveEnemies.Clear();
        residentEnemies.Clear();
    }
}