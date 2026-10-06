using UnityEngine;

// Drops a pickup when this enemy dies, so killing armed enemies pays off. Host-only; the loot replicates like any spawned loot.
[RequireComponent(typeof(EnemyController))]
public class EnemyLootDrop : MonoBehaviour
{
    [SerializeField] private LootDataSO dropLoot;
    [SerializeField, Range(0f, 1f)] private float dropChance = 1f;
    [Tooltip("The weapon model the enemy carries: hidden on death and used as the drop position. Optional.")]
    [SerializeField] private GameObject heldVisual;
    [SerializeField, Min(0f)] private float dropHeight = 1f;
    [SerializeField, Min(0f)] private float tossForce = 1.5f;

    private EnemyController enemy;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        enemy.Died += HandleDied;
    }

    private void OnDestroy()
    {
        if (enemy != null)
            enemy.Died -= HandleDied;
    }

    private void HandleDied(EnemyController _)
    {
        if (NetworkRole.IsClientOnly)
            return;

        Vector3 position = heldVisual != null ? heldVisual.transform.position : transform.position + Vector3.up * dropHeight;
        Quaternion rotation = heldVisual != null ? heldVisual.transform.rotation : Quaternion.identity;

        if (heldVisual != null)
            heldVisual.SetActive(false);

        if (dropLoot == null || Random.value > dropChance)
            return;

        Vector3 toss = new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f)).normalized * tossForce;
        LootItem.CreateDroppedLoot(dropLoot, position, rotation, null, toss);
    }
}
