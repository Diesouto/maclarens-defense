using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Slow, immortal ghost: floats straight at the nearest living player through walls (trigger colliders,
// no NavMesh) and can only be banished by a thrown cross. The host simulates; clients follow NetworkTransform.
[RequireComponent(typeof(Rigidbody))]
public class GhostController : NetworkBehaviour
{
    private static readonly HashSet<GhostController> activeGhosts = new();
    public static int ActiveCount => activeGhosts.Count;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.5f;
    [Tooltip("Height above the target's feet the ghost floats towards.")]
    [SerializeField] private float hoverHeight = 0.8f;
    [SerializeField, Min(0.05f)] private float retargetInterval = 0.5f;
    [Tooltip("Another player must be this much closer (metres) than the current target to steal the ghost's attention.")]
    [SerializeField, Min(0f)] private float retargetMargin = 2f;
    [SerializeField, Min(0f)] private float turnSpeed = 180f;

    [Header("Attack")]
    [SerializeField, Min(0.1f)] private float attackDistance = 1.2f;
    [SerializeField, Min(0f)] private float attackDamage = 15f;
    [SerializeField, Min(0f)] private float attackForce = 3f;
    [SerializeField, Min(0f)] private float attackCooldown = 2f;

    [Header("Banishing")]
    [Tooltip("Only this item, thrown at the ghost, can kill it (the cross).")]
    [SerializeField] private LootDataSO banishItem;
    [SerializeField, Min(0f)] private float minimumBanishSpeed = 2f;
    [SerializeField] private GameObject banishVfxPrefab;
    [SerializeField, Min(0f)] private float banishVfxLifetime = 3f;
    [SerializeField] private AudioClip banishSfx;

    private PlayerController targetPlayer;
    private float nextRetargetTime;
    private float nextAttackTime;
    private bool isBanished;

    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // Passes through walls and bullets: triggers only, on a layer weapon raycasts skip.
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = ignoreRaycastLayer;

        foreach (Collider ghostCollider in GetComponentsInChildren<Collider>(true))
            ghostCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        activeGhosts.Add(this);
    }

    private void OnDisable()
    {
        activeGhosts.Remove(this);
    }

    public static void DespawnAll()
    {
        foreach (GhostController ghost in new List<GhostController>(activeGhosts))
        {
            if (ghost == null)
                continue;

            if (ghost.IsSpawned && ghost.IsServer)
                ghost.NetworkObject.Despawn(true);
            else if (!ghost.IsSpawned)
                Destroy(ghost.gameObject);
        }
    }

    private void Update()
    {
        if (isBanished || NetworkRole.IsClientOnly)
            return;

        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return;

        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + retargetInterval;
            FindNearestPlayer();
        }

        if (targetPlayer == null || !targetPlayer.IsAlive)
        {
            targetPlayer = null;
            return;
        }

        Vector3 toGoal = targetPlayer.transform.position + Vector3.up * hoverHeight - transform.position;
        float distance = toGoal.magnitude;

        if (distance > attackDistance * 0.5f)
            transform.position += toGoal / distance * Mathf.Min(moveSpeed * Time.deltaTime, distance);

        Vector3 flat = new Vector3(toGoal.x, 0f, toGoal.z);
        if (flat.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);

        if (distance <= attackDistance && Time.time >= nextAttackTime)
            Attack(flat);
    }

    private void Attack(Vector3 direction)
    {
        nextAttackTime = Time.time + attackCooldown;

        if (targetPlayer.TryGetComponent(out Health playerHealth))
            playerHealth.TakeDamage(attackDamage, direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward, attackForce);
    }

    private void FindNearestPlayer()
    {
        Vector3 position = transform.position;
        bool hasTarget = targetPlayer != null && targetPlayer.IsAlive;
        float currentDistance = hasTarget ? Vector3.Distance(targetPlayer.transform.position, position) : Mathf.Infinity;

        PlayerController best = null;
        float bestDistance = Mathf.Infinity;
        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null || !player.IsAlive)
                continue;

            float distance = Vector3.Distance(player.transform.position, position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = player;
            }
        }

        if (hasTarget && best != targetPlayer && bestDistance > currentDistance - retargetMargin)
            best = targetPlayer;

        targetPlayer = best;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBanished || NetworkRole.IsClientOnly || banishItem == null)
            return;

        Rigidbody body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out LootItem item) || item.IsCollected || item.Data == null ||
            item.Data.ItemId != banishItem.ItemId || body.linearVelocity.magnitude < minimumBanishSpeed)
            return;

        Banish();
    }

    private void Banish()
    {
        isBanished = true;
        ThreatManager.Instance?.RegisterEnemyKill();

        Vector3 position = transform.position;
        if (IsSpawned)
        {
            PlayBanishEffectsRpc(position);
            NetworkObject.Despawn(true);
            return;
        }

        PlayBanishEffects(position);
        Destroy(gameObject);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayBanishEffectsRpc(Vector3 position)
    {
        PlayBanishEffects(position);
    }

    private void PlayBanishEffects(Vector3 position)
    {
        if (banishSfx != null)
            AudioSource.PlayClipAtPoint(banishSfx, position);

        if (banishVfxPrefab == null)
            return;

        GameObject vfx = Instantiate(banishVfxPrefab, position, Quaternion.identity);
        if (banishVfxLifetime > 0f)
            Destroy(vfx, banishVfxLifetime);
    }
}
