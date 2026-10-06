using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class BreakableOnImpact : MonoBehaviour
{
    [SerializeField] private float breakVelocity = 6f;
    [Tooltip("Legacy single replacement; kept so existing prefabs keep working. Spawned alongside brokenPrefabs.")]
    [SerializeField] private GameObject brokenPrefab;
    [Tooltip("Pieces spawned when it breaks (e.g. top and bottom halves of a bottle). Always local debris on every peer; network components are stripped.")]
    [SerializeField] private GameObject[] brokenPrefabs;
    [Tooltip("Seconds before local debris is cleaned up; 0 keeps it forever.")]
    [SerializeField, Min(0f)] private float debrisLifetime = 20f;
    [Tooltip("Outward impulse applied to each shard so the pieces scatter instead of dropping straight down.")]
    [SerializeField, Min(0f)] private float debrisForce = 3f;
    [SerializeField, Min(0.01f)] private float debrisRadius = 1f;
    [SerializeField, Min(0f)] private float debrisTorque = 2f;
    [SerializeField] private AudioClip breakSound;

    private bool hasBroken;
    private bool breakPending;
    private float ignoreImpactsUntil;

    // Dropped items spawn overlapping each other and depenetrate fast enough to shatter on their own.
    private void OnEnable()
    {
        ignoreImpactsUntil = Time.time + 0.4f;
    }

    public void CopySettingsFrom(BreakableOnImpact source)
    {
        if (source == null)
            return;

        breakVelocity = source.breakVelocity;
        brokenPrefab = source.brokenPrefab;
        brokenPrefabs = source.brokenPrefabs;
        debrisLifetime = source.debrisLifetime;
        debrisForce = source.debrisForce;
        debrisRadius = source.debrisRadius;
        debrisTorque = source.debrisTorque;
        breakSound = source.breakSound;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (NetworkRole.IsClientOnly || hasBroken || breakPending || Time.time < ignoreImpactsUntil ||
            collision.relativeVelocity.magnitude < breakVelocity)
            return;

        // Breaking destroys components, which Unity forbids from inside a physics callback.
        breakPending = true;
    }

    private void Update()
    {
        if (!breakPending)
            return;

        breakPending = false;
        Break();
    }

    private void Break()
    {
        ApplyBreak();
    }

    // Healing consumables (e.g. a thrown potion) splash every player in range when they shatter,
    // and bring back any corpse lying in the splash (no quota penalty).
    private void HealPlayersInRange()
    {
        if (!TryGetComponent(out LootItem lootItem) || lootItem.Data == null || lootItem.Data.BreakHealPercent <= 0f)
            return;

        float healPercent = lootItem.Data.BreakHealPercent;
        float sqrRadius = lootItem.Data.BreakHealRadius * lootItem.Data.BreakHealRadius;
        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null || !player.IsAlive ||
                (player.transform.position - transform.position).sqrMagnitude > sqrRadius ||
                !player.TryGetComponent(out Health playerHealth))
                continue;

            playerHealth.Heal(playerHealth.MaxHealth * healPercent);
        }

        foreach (PlayerBody body in PlayerBody.AllBodies)
        {
            if (body == null || !body.IsDead || body.IsHidden ||
                (body.BodyPosition - transform.position).sqrMagnitude > sqrRadius)
                continue;

            body.Revive(null, healPercent);
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
            if (piecePrefab == null)
                continue;

            GameObject piece = InstantiateDebris(piecePrefab, position, rotation);
            foreach (Rigidbody pieceBody in piece.GetComponentsInChildren<Rigidbody>())
            {
                pieceBody.linearVelocity = velocity;

                if (debrisForce > 0f)
                    pieceBody.AddExplosionForce(debrisForce, position, debrisRadius, 0.2f, ForceMode.Impulse);

                if (debrisTorque > 0f)
                    pieceBody.AddTorque(Random.insideUnitSphere * debrisTorque, ForceMode.Impulse);
            }

            if (debrisLifetime > 0f)
                Destroy(piece, debrisLifetime);
        }
    }

    // Staged under an inactive parent so network components are removed before their Awake runs.
    // DestroyImmediate is required here (Destroy is deferred, so Awake would still see them) and is
    // only legal because the break is deferred out of the physics callback.
    private static GameObject InstantiateDebris(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject staging = new GameObject("DebrisStaging");
        staging.SetActive(false);

        GameObject piece = Instantiate(prefab, position, rotation, staging.transform);
        StripComponents<Unity.Netcode.Components.NetworkRigidbodyBase>(piece);
        StripComponents<NetworkBehaviour>(piece);
        StripComponents<NetworkObject>(piece);

        piece.transform.SetParent(null, true);
        Destroy(staging);
        return piece;
    }

    private static void StripComponents<T>(GameObject root) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            DestroyImmediate(component);
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
