using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Spawn Points")]
    [SerializeField] private EnemySpawnPoint[] spawnPoints;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private int maxAliveEnemies = 10;
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<EnemyController> aliveEnemies = new();

    private float spawnTimer;

    private void Start()
    {
        if (spawnOnStart)
            spawnTimer = spawnInterval;
    }

    private void Update()
    {
        CleanupDeadEnemies();

        if (aliveEnemies.Count >= maxAliveEnemies)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            TrySpawnEnemy();
            spawnTimer = spawnInterval;
        }
    }

    private void TrySpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning($"{name}: No enemy prefabs configured.", this);
            return;
        }

        EnemySpawnPoint spawnPoint = GetRandomSpawnPoint();

        if (spawnPoint == null)
            return;

        GameObject prefab = enemyPrefabs[
            Random.Range(0, enemyPrefabs.Length)
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
    }

    private EnemySpawnPoint GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"{name}: No spawn points configured.", this);
            return null;
        }

        List<EnemySpawnPoint> validPoints = new();

        foreach (EnemySpawnPoint point in spawnPoints)
        {
            if (point != null && point.IsValid())
                validPoints.Add(point);
        }

        if (validPoints.Count == 0)
            return null;

        return validPoints[
            Random.Range(0, validPoints.Count)
        ];
    }

    private void CleanupDeadEnemies()
    {
        aliveEnemies.RemoveAll(enemy =>
            enemy == null || !enemy.gameObject.activeInHierarchy
        );
    }

    public int AliveEnemyCount => aliveEnemies.Count;
}