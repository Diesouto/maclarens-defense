using System;
using System.Collections.Generic;
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
        new ThreatSpawnSettings { level = ThreatLevel.Calm, maxAliveEnemies = 2, spawnInterval = 8f },
        new ThreatSpawnSettings { level = ThreatLevel.Low, maxAliveEnemies = 4, spawnInterval = 6f },
        new ThreatSpawnSettings { level = ThreatLevel.Medium, maxAliveEnemies = 6, spawnInterval = 4f },
        new ThreatSpawnSettings { level = ThreatLevel.High, maxAliveEnemies = 9, spawnInterval = 3f },
        new ThreatSpawnSettings { level = ThreatLevel.Critical, maxAliveEnemies = 12, spawnInterval = 2f },
    };

    [Header("Spawn Settings")]
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<EnemyController> aliveEnemies = new();

    private float spawnTimer;

    private void Start()
    {
        if (spawnOnStart)
            spawnTimer = GetCurrentSettings().spawnInterval;
    }

    private void Update()
    {
        CleanupDeadEnemies();

        ThreatSpawnSettings settings = GetCurrentSettings();

        if (aliveEnemies.Count >= settings.maxAliveEnemies)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            TrySpawnEnemy();
            spawnTimer = settings.spawnInterval;
        }
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

            return;
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
            return;
        }

        aliveEnemies.Add(enemy);
        spawnPoint.MarkUsed();
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

            float score = point.GetScore();
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
        aliveEnemies.RemoveAll(enemy =>
            enemy == null || !enemy.gameObject.activeInHierarchy
        );
    }

    public int AliveEnemyCount => aliveEnemies.Count;
}