using System.Collections.Generic;
using UnityEngine;

public enum TownZoneTier
{
    Outskirts,
    Middle,
    Center
}

// A hostility region of the town. Guard counts and loot bias are authored here; live guard state is written by EnemySpawner.
public class TownZone : MonoBehaviour
{
    private static readonly List<TownZone> zones = new();

    [SerializeField] private TownZoneTier tier = TownZoneTier.Outskirts;
    [SerializeField, Min(1f)] private float radius = 30f;

    [Header("Guards")]
    [Tooltip("Guards placed here each time the train heads to town. Empty falls back to the spawner's enemy prefabs.")]
    [SerializeField] private GameObject[] guardPrefabs;
    [SerializeField, Min(0)] private int guardCount = 2;
    [Tooltip("Threat spawns from this zone never drop below this share of their weight, even when every guard is dead.")]
    [SerializeField, Range(0f, 1f)] private float minimumReinforcementShare = 0.25f;

    [Header("Loot")]
    [Tooltip("Multiplies how likely loot spawn points inside this zone are picked.")]
    [SerializeField, Min(0f)] private float lootWeight = 1f;

    public static IReadOnlyList<TownZone> All => zones;

    public TownZoneTier Tier => tier;
    public GameObject[] GuardPrefabs => guardPrefabs;
    public int GuardCount => guardCount;
    public int GuardsAlive { get; private set; }

    // 1 = fully garrisoned, 0 = every guard killed this visit.
    public float GuardStrength => guardCount <= 0 ? 1f : Mathf.Clamp01((float)GuardsAlive / guardCount);

    public float ReinforcementShare => Mathf.Lerp(minimumReinforcementShare, 1f, GuardStrength);

    private void OnEnable()
    {
        zones.Add(this);
    }

    private void OnDisable()
    {
        zones.Remove(this);
    }

    public void SetGuardsAlive(int count)
    {
        GuardsAlive = Mathf.Max(count, 0);
    }

    public bool Contains(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= radius * radius;
    }

    // Smaller zones win so a Center zone can sit inside a Middle one.
    public static TownZone FindAt(Vector3 position)
    {
        TownZone best = null;
        foreach (TownZone zone in zones)
        {
            if (zone == null || !zone.Contains(position))
                continue;

            if (best == null || zone.radius < best.radius)
                best = zone;
        }

        return best;
    }

    public static float GetLootWeight(Vector3 position)
    {
        TownZone zone = FindAt(position);
        return zone != null ? zone.lootWeight : 1f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = tier == TownZoneTier.Center ? Color.red : tier == TownZoneTier.Middle ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
