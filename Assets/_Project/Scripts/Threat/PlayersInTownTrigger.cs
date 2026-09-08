using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayersInTownTrigger : MonoBehaviour
{
    public static PlayersInTownTrigger Instance { get; private set; }

    [SerializeField] private bool killPlayersOnDeparture = true;
    [SerializeField] private float killDistanceThreshold = 2f;

    private readonly HashSet<PlayerController> playersInsideTown = new();

    public bool AnyPlayerInside => playersInsideTown.Count > 0;

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
        if (player == null)
            return;

        playersInsideTown.Add(player);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        playersInsideTown.Remove(player);
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
}
