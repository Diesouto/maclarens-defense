using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
// Tracks all temporary entities that can be affected by Town extraction.
public class InTownTrigger : MonoBehaviour
{
    public static InTownTrigger Instance { get; private set; }

    [SerializeField] private bool killPlayersOnDeparture = true;
    [SerializeField] private float killDistanceThreshold = 2f;

    private readonly HashSet<PlayerController> playersInsideTown = new();
    private readonly HashSet<LootItem> lootInsideTown = new();

    public bool AnyPlayerInside => playersInsideTown.Count > 0;

    public bool IsPlayerInsideTown(PlayerController player)
    {
        return player != null && playersInsideTown.Contains(player);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            playersInsideTown.Add(player);
            return;
        }

        LootItem lootItem = other.GetComponentInParent<LootItem>();
        if (lootItem != null)
            lootInsideTown.Add(lootItem);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            playersInsideTown.Remove(player);
            return;
        }

        LootItem lootItem = other.GetComponentInParent<LootItem>();
        if (lootItem != null)
            lootInsideTown.Remove(lootItem);
    }

    public void KillPlayersStillInsideTown()
    {
        if (!killPlayersOnDeparture)
            return;

        foreach (PlayerController player in new List<PlayerController>(playersInsideTown))
        {
            if (player == null)
            {
                playersInsideTown.Remove(player);
                continue;
            }

            if (Vector3.Distance(player.transform.position, transform.position) <= killDistanceThreshold)
            {
                Health health = player.GetComponent<Health>();
                if (health != null)
                    health.TakeDamage(health.MaxHealth);
                else
                    Destroy(player.gameObject);
            }
        }
    }

    public void AbandonPlayersStillInsideTown()
    {
        foreach (PlayerController player in new List<PlayerController>(playersInsideTown))
        {
            if (player == null)
            {
                playersInsideTown.Remove(player);
                continue;
            }

            Health health = player.GetComponent<Health>();
            if (health != null)
                health.TakeDamage(health.MaxHealth);
            else
                Destroy(player.gameObject);
        }
    }

    public void DestroyLootInsideTown()
    {
        foreach (LootItem lootItem in new List<LootItem>(lootInsideTown))
        {
            if (lootItem == null)
            {
                lootInsideTown.Remove(lootItem);
                continue;
            }

            if (lootItem.Cargo != null)
                continue;

            LootRegistry.Instance?.Unregister(lootItem);
            Destroy(lootItem.gameObject);
        }

        lootInsideTown.Clear();
    }
}
