using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class BreakableOnImpact : MonoBehaviour
{
    [SerializeField] private float breakVelocity = 6f;
    [Tooltip("Legacy single replacement; kept so existing prefabs keep working. Spawned alongside brokenPrefabs.")]
    [SerializeField] private GameObject brokenPrefab;
    [Tooltip("Pieces spawned when it breaks (e.g. top and bottom halves of a bottle). Pieces without a NetworkObject are local debris on every peer.")]
    [SerializeField] private GameObject[] brokenPrefabs;
    [Tooltip("Seconds before local debris is cleaned up; 0 keeps it forever.")]
    [SerializeField, Min(0f)] private float debrisLifetime = 20f;
    [SerializeField] private AudioClip breakSound;

    private bool hasBroken;

    public void CopySettingsFrom(BreakableOnImpact source)
    {
        if (source == null)
            return;

        breakVelocity = source.breakVelocity;
        brokenPrefab = source.brokenPrefab;
        brokenPrefabs = source.brokenPrefabs;
        debrisLifetime = source.debrisLifetime;
        breakSound = source.breakSound;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (NetworkRole.IsClientOnly || hasBroken || collision.relativeVelocity.magnitude < breakVelocity)
            return;

        Break();
    }

    private void Break()
    {
        ApplyBreak();
    }

    // Healing consumables (e.g. a thrown potion) splash every living player in range when they shatter.
    private void HealPlayersInRange()
    {
        if (!TryGetComponent(out LootItem lootItem) || lootItem.Data == null || lootItem.Data.BreakHealPercent <= 0f)
            return;

        float sqrRadius = lootItem.Data.BreakHealRadius * lootItem.Data.BreakHealRadius;
        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null || !player.IsAlive ||
                (player.transform.position - transform.position).sqrMagnitude > sqrRadius ||
                !player.TryGetComponent(out Health playerHealth))
                continue;

            playerHealth.Heal(playerHealth.MaxHealth * lootItem.Data.BreakHealPercent);
        }
    }

    public void ApplyBreak()
    {
        if (NetworkRole.IsClientOnly || hasBroken)
            return;

        hasBroken = true;

        // Explosives (nitro) replace shattering with their own blast, VFX and cleanup.
        if (TryGetComponent(out Explosive explosive))
        {
            explosive.Detonate(0f);
            return;
        }

        HealPlayersInRange();

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = TryGetComponent(out Rigidbody body) ? body.linearVelocity : Vector3.zero;

        SpawnNetworkedPieces(position, rotation);

        if (TryGetComponent(out NetworkBreakable networkBreakable) && networkBreakable.IsSpawned)
            networkBreakable.PlayBreakEffects(position, rotation, velocity);
        else
            PlayLocalBreakEffects(position, rotation, velocity);

        if (TryGetComponent(out LootItem lootItem))
        {
            lootItem.Cargo?.RemoveItem(lootItem);
            LootRegistry.Instance?.Unregister(lootItem);
        }

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            networkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    // Runs on every peer: sound plus non-networked debris pieces that inherit the bottle's velocity.
    public void PlayLocalBreakEffects(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, position);

        foreach (GameObject piecePrefab in GetBrokenPrefabs())
        {
            if (piecePrefab == null || piecePrefab.GetComponent<NetworkObject>() != null)
                continue;

            GameObject piece = Instantiate(piecePrefab, position, rotation);
            foreach (Rigidbody pieceBody in piece.GetComponentsInChildren<Rigidbody>())
                pieceBody.linearVelocity = velocity;

            if (debrisLifetime > 0f)
                Destroy(piece, debrisLifetime);
        }
    }

    // Pieces that carry a NetworkObject (e.g. a broken bottle that is still loot) are spawned by the host.
    private void SpawnNetworkedPieces(Vector3 position, Quaternion rotation)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        bool isNetworked = networkManager != null && networkManager.IsListening;

        foreach (GameObject piecePrefab in GetBrokenPrefabs())
        {
            if (piecePrefab == null || piecePrefab.GetComponent<NetworkObject>() == null)
                continue;

            if (isNetworked && !networkManager.NetworkConfig.Prefabs.Contains(piecePrefab))
            {
                Debug.LogError($"BreakableOnImpact: broken piece '{piecePrefab.name}' has a NetworkObject but isn't registered in NetworkPrefabs.", piecePrefab);
                continue;
            }

            GameObject piece = Instantiate(piecePrefab, position, rotation);
            if (isNetworked)
                piece.GetComponent<NetworkObject>().Spawn(true);
        }
    }

    private System.Collections.Generic.IEnumerable<GameObject> GetBrokenPrefabs()
    {
        if (brokenPrefab != null)
            yield return brokenPrefab;

        if (brokenPrefabs == null)
            yield break;

        foreach (GameObject piecePrefab in brokenPrefabs)
            yield return piecePrefab;
    }
}
