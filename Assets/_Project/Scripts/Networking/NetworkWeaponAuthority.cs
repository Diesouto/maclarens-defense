using Unity.Netcode;
using UnityEngine;

// The owner fires locally (own camera, instant feedback); the host validates each reported hit
// against the weapon in the replicated inventory, its fire rate, range and target proximity.
[RequireComponent(typeof(NetworkObject))]
public class NetworkWeaponAuthority : NetworkBehaviour
{
    [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
    [Tooltip("How far a reported hit point may be from the target's colliders on the host (interpolation lag).")]
    [SerializeField, Min(0f)] private float hitPointTolerance = 1.5f;
    [SerializeField, Min(0f)] private float maxHitForce = 100f;

    private PlayerInventory inventory;
    private int lastShotId = int.MinValue;
    private float lastShotTime = float.NegativeInfinity;
    private int hitsThisShot;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestHitServerRpc(NetworkObjectReference targetReference, Vector3 hitPoint, Vector3 hitDirection,
        float hitForce, bool claimedHeadshot, int shotId, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            !targetReference.TryGet(out NetworkObject targetObject) || targetObject == null ||
            !targetObject.TryGetComponent(out NetworkHealth targetHealth))
            return;

        WeaponDataSO weaponData = inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.IsWeapon
            ? inventory.ActiveItem.WeaponData
            : null;
        if (weaponData == null || !TryConsumeShot(weaponData, shotId))
            return;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        if (Vector3.Distance(eye, hitPoint) > weaponData.range + hitPointTolerance ||
            !IsNearAnyCollider(targetObject, hitPoint, hitPointTolerance))
            return;

        float damage = weaponData.damage;
        if (claimedHeadshot && IsNearHeadHitbox(targetObject, hitPoint))
            damage *= weaponData.headshotMultiplier;

        Vector3 direction = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : (hitPoint - eye).normalized;
        Health targetHealthComponent = targetObject.GetComponent<Health>();
        targetHealthComponent?.SetPendingAttacker(OwnerClientId);
        targetHealth.ApplyServerDamage(damage, direction, Mathf.Clamp(hitForce, 0f, maxHitForce));
        targetHealthComponent?.ClearPendingAttacker();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestDetonateServerRpc(NetworkObjectReference explosiveReference, Vector3 hitPoint, int shotId,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            !explosiveReference.TryGet(out NetworkObject explosiveObject) || explosiveObject == null ||
            !explosiveObject.TryGetComponent(out Explosive explosive))
            return;

        WeaponDataSO weaponData = inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.IsWeapon
            ? inventory.ActiveItem.WeaponData
            : null;
        if (weaponData == null || !TryConsumeShot(weaponData, shotId))
            return;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        if (Vector3.Distance(eye, hitPoint) > weaponData.range + hitPointTolerance ||
            !IsNearAnyCollider(explosiveObject, hitPoint, hitPointTolerance))
            return;

        explosive.TakeDamage(weaponData.damage, (hitPoint - eye).normalized, 0f);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void ReportAmmoServerRpc(int currentAmmo, int reserveAmmo, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || inventory == null)
            return;

        ItemInstance instance = inventory.ActiveItemInstance;
        WeaponDataSO weaponData = instance?.Data != null && instance.Data.IsWeapon ? instance.Data.WeaponData : null;
        if (weaponData == null)
            return;

        currentAmmo = Mathf.Clamp(currentAmmo, 0, weaponData.magazineSize);
        reserveAmmo = Mathf.Clamp(reserveAmmo, 0, weaponData.maxAmmo);

        // Reloading only moves bullets between magazine and reserve; the total can never grow.
        if (instance.HasAmmoState && currentAmmo + reserveAmmo > instance.CurrentAmmo + instance.CurrentReserveAmmo)
            return;

        instance.SetAmmo(currentAmmo, reserveAmmo);
    }

    // A penetrating shot may report several hits with the same id; new ids respect the fire rate.
    private bool TryConsumeShot(WeaponDataSO weaponData, int shotId)
    {
        if (shotId != lastShotId)
        {
            float minInterval = 0.8f / Mathf.Max(weaponData.fireRate, 0.01f);
            if (Time.time - lastShotTime < minInterval)
                return false;

            lastShotId = shotId;
            lastShotTime = Time.time;
            hitsThisShot = 0;
        }

        int maxHits = weaponData.canPenetrate ? weaponData.maxPenetrations + 1 : 1;
        return ++hitsThisShot <= maxHits;
    }

    public static bool IsNearAnyCollider(NetworkObject target, Vector3 point, float tolerance)
    {
        float sqrTolerance = tolerance * tolerance;
        foreach (Collider targetCollider in target.GetComponentsInChildren<Collider>())
        {
            if ((targetCollider.ClosestPointOnBounds(point) - point).sqrMagnitude <= sqrTolerance)
                return true;
        }

        return false;
    }

    private static bool IsNearHeadHitbox(NetworkObject target, Vector3 point)
    {
        foreach (Hitbox hitbox in target.GetComponentsInChildren<Hitbox>())
        {
            if (hitbox.Type != Hitbox.HitboxType.Head || !hitbox.TryGetComponent(out Collider headCollider))
                continue;

            if ((headCollider.ClosestPointOnBounds(point) - point).sqrMagnitude <= 0.5f * 0.5f)
                return true;
        }

        return false;
    }
}