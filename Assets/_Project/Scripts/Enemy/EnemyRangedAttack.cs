using System.Collections;
using UnityEngine;

// Hitscan shooter for armed bandits. Pair with EnemyController.keepDistance > 0 and attackDamage 0 so it holds position instead of meleeing.
[RequireComponent(typeof(EnemyController))]
public class EnemyRangedAttack : MonoBehaviour
{
    [SerializeField, Min(1f)] private float range = 25f;
    [SerializeField, Min(0.1f)] private float fireInterval = 2f;
    [Tooltip("Delay between lining up the shot and firing, so players can break line of sight.")]
    [SerializeField, Min(0f)] private float windup = 0.5f;
    [SerializeField, Min(0f)] private float damage = 6f;
    [SerializeField, Min(0f)] private float hitForce = 3f;
    [Tooltip("Hit probability at point-blank and at max range; weak aim keeps these low.")]
    [SerializeField, Range(0f, 1f)] private float closeHitChance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float farHitChance = 0.15f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField, Min(1f)] private float turnSpeed = 360f;

    [Header("Effects")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shotClip;

    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    private EnemyController enemy;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
    }

    private void Start()
    {
        if (NetworkRole.IsClientOnly)
        {
            enabled = false;
            return;
        }

        StartCoroutine(ShootLoop());
    }

    private void Update()
    {
        if (enemy.IsDead)
            return;

        PlayerController player = enemy.TargetPlayer;
        if (player == null || !player.IsAlive)
            return;

        Vector3 flat = player.transform.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f || flat.sqrMagnitude > range * range)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
    }

    private IEnumerator ShootLoop()
    {
        var interval = new WaitForSeconds(fireInterval);
        var wind = new WaitForSeconds(windup);

        while (!enemy.IsDead)
        {
            yield return interval;

            PlayerController player = enemy.TargetPlayer;
            if (!CanShoot(player))
                continue;

            yield return wind;

            if (enemy.IsDead)
                yield break;

            player = enemy.TargetPlayer;
            if (CanShoot(player))
                Shoot(player);
        }
    }

    private bool CanShoot(PlayerController player)
    {
        return player != null && player.IsAlive && HasLineOfSight(player, out _);
    }

    private Vector3 Origin => muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.5f;

    private bool HasLineOfSight(PlayerController player, out float distance)
    {
        Vector3 origin = Origin;
        Vector3 targetPoint = player.transform.position + Vector3.up * 1.2f;
        Vector3 toTarget = targetPoint - origin;
        distance = toTarget.magnitude;

        if (distance > range || distance < 0.01f)
            return false;

        int count = Physics.RaycastNonAlloc(origin, toTarget / distance, hitBuffer, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            Transform hitTransform = hitBuffer[i].collider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(player.transform))
                continue;

            if (hitBuffer[i].distance < nearest)
            {
                nearest = hitBuffer[i].distance;
                blocked = true;
            }
        }

        return !blocked;
    }

    private void Shoot(PlayerController player)
    {
        if (!HasLineOfSight(player, out float distance))
            return;

        Vector3 direction = (player.transform.position - transform.position).normalized;
        float hitChance = Mathf.Lerp(closeHitChance, farHitChance, Mathf.Clamp01(distance / range));

        if (muzzleFlash != null)
            Instantiate(muzzleFlash, Origin, Quaternion.LookRotation(direction));

        if (audioSource != null && shotClip != null)
            audioSource.PlayOneShot(shotClip);

        if (Random.value > hitChance)
            return;

        player.GetComponent<Health>()?.TakeDamage(damage, direction, hitForce);
    }
}
