using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
// Tracks all temporary entities that can be affected by Town extraction.
public class InTownTrigger : MonoBehaviour
{
    public static InTownTrigger Instance { get; private set; }

    [FormerlySerializedAs("killPlayersOnDeparture")]
    [SerializeField] private bool dieWhenLeftBehind = true;

    private Collider townCollider;
    private readonly HashSet<LootItem> lootInsideTown = new();

    public bool AnyPlayerInside
    {
        get
        {
            foreach (PlayerController player in PlayerController.ActivePlayers)
            {
                if (IsPlayerInsideTown(player))
                    return true;
            }

            return false;
        }
    }

    public bool IsPlayerInsideTown(PlayerController player)
    {
        if (player == null || townCollider == null || !townCollider.enabled)
            return false;

        bool hasBody = player.TryGetComponent(out PlayerBody body);
        if (hasBody && body.IsHidden)
            return false;

        Vector3 position = hasBody ? body.BodyPosition : player.transform.position;
        return (townCollider.ClosestPoint(position) - position).sqrMagnitude < 0.0001f;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        townCollider = GetComponent<Collider>();
        if (townCollider != null)
            townCollider.isTrigger = true;
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
            return;

        LootItem lootItem = other.GetComponentInParent<LootItem>();
        if (lootItem != null)
            lootInsideTown.Add(lootItem);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
            return;

        LootItem lootItem = other.GetComponentInParent<LootItem>();
        if (lootItem != null)
            lootInsideTown.Remove(lootItem);
    }

    public void KillPlayersStillInsideTown()
    {
        AbandonPlayersStillInsideTown();
    }

    public void AbandonPlayersStillInsideTown()
    {
        if (!dieWhenLeftBehind || NetworkRole.IsClientOnly)
            return;

        foreach (PlayerController player in new List<PlayerController>(PlayerController.ActivePlayers))
        {
            if (!IsPlayerInsideTown(player) || !player.IsAlive)
                continue;

            if (player.TryGetComponent(out PlayerBody body) && TrainCargo.IsBodyAboard(body))
                continue;

            if (player.TryGetComponent(out TrainPassenger passenger) && passenger.CurrentCarriage != null &&
                passenger.CurrentCarriage.IsNear(player.transform.position, 1.5f))
                continue;

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
