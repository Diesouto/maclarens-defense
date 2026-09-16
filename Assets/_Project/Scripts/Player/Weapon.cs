using System;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    public event Action<int, int> OnAmmoChanged;

    [Header("Weapon Settings")]
    [SerializeField] private WeaponDataSO weaponData;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private InteractUI interactUI;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private GameObject gunshotParticleReference;
    [SerializeField] private float hitForceMultiplier = 12f;

    private PlayerInventory inventory;
    private float nextTimeToFire;
    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isReloading;
    private float reloadTimer;
    private ItemInstance equippedInstance;
    private NetworkWeaponAuthority networkAuthority;

    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponentInParent<PlayerInputHandler>();

        inventory = GetComponentInParent<PlayerInventory>();
        networkAuthority = GetComponentInParent<NetworkWeaponAuthority>();

        if (playerCamera == null)
            playerCamera = FindFirstObjectByType<Camera>();

        if (weaponData != null)
        {
            currentAmmo = Mathf.Max(weaponData.magazineSize, 0);
            currentReserveAmmo = Mathf.Max(weaponData.maxAmmo, 0);
            OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
        }

        if (interactUI == null)
            interactUI = FindFirstObjectByType<InteractUI>();
    }

    private void Update()
    {
        if (!SyncWithActiveItemInternal())
            return;

        if (weaponData == null)
        {
            return;
        }

        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0f)
                FinishReload();
        }
    }

    public void Equip(WeaponDataSO newWeaponData)
    {
        weaponData = newWeaponData;

        if (weaponData == null)
            return;

        currentAmmo = Mathf.Max(weaponData.magazineSize, 0);
        currentReserveAmmo = Mathf.Max(weaponData.maxAmmo, 0);

        if (inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.WeaponData == weaponData && inventory.TryGetWeaponAmmo(inventory.ActiveItemInstance, out int savedAmmo, out int savedReserveAmmo))
        {
            currentAmmo = savedAmmo;
            currentReserveAmmo = savedReserveAmmo;
        }

        SaveAmmoState();
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
    }

    public void SyncWithActiveItem()
    {
        SyncWithActiveItemInternal();
    }

    // Called by ItemHolder to fix references when instantiated as a held visual.
    public void Initialize(PlayerInputHandler handler, PlayerInventory inv, Camera cam)
    {
        if (handler != null) inputHandler = handler;
        if (inv != null) inventory = inv;
        if (cam != null) playerCamera = cam;
    }

    public bool TryFire()
    {
        if (!SyncWithActiveItemInternal() || !CanFire())
            return false;

        Fire();
        return true;
    }

    public void HandleInput(bool firePressed, bool fireHeld, bool reloadPressed)
    {
        if (!SyncWithActiveItemInternal())
            return;

        if (networkAuthority != null && networkAuthority.IsSpawned && !networkAuthority.IsServer)
        {
            networkAuthority.RequestWeaponInputServerRpc(firePressed, fireHeld, reloadPressed);
            return;
        }

        if (isReloading)
            return;

        if (reloadPressed)
        {
            TryStartReload();
            return;
        }

        bool shouldFire = weaponData != null && (weaponData.automatic ? fireHeld : firePressed);
        if (shouldFire)
            TryFire();
    }

    private bool IsReloadPressed()
    {
        return inputHandler != null && inputHandler.ReloadPressed;
    }

    private bool IsFirePressed()
    {
        if (weaponData == null)
            return false;

        if (weaponData.automatic)
            return inputHandler != null && inputHandler.FireHeld;

        return inputHandler != null && inputHandler.FirePressed;
    }

    private bool CanFire()
    {
        return weaponData != null && !isReloading && currentAmmo > 0 && Time.time >= nextTimeToFire;
    }

    private void Fire()
    {
        Debug.Log($"{name} fired {weaponData.name}!");

        float fireRate = Mathf.Max(weaponData.fireRate, 0.01f);
        nextTimeToFire = Time.time + 1f / fireRate;
        currentAmmo--;
        SaveAmmoState();
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
        gunshotParticleReference?.GetComponent<ParticleSystem>()?.Play();

        ShootRaycast();
    }

    private void ShootRaycast()
    {
        if (playerCamera == null || weaponData == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));

        int remainingPenetrations = weaponData.canPenetrate
            ? weaponData.maxPenetrations
            : 0;

        Vector3 currentOrigin = ray.origin;

        while (true)
        {
            Ray currentRay = new Ray(currentOrigin, ray.direction);

            if (!Physics.SphereCast(
                    currentRay,
                    weaponData.shotRadius,
                    out RaycastHit hit,
                    weaponData.range,
                    hitMask,
                    QueryTriggerInteraction.Collide))
            {
                break;
            }

            Debug.DrawRay(
                currentRay.origin,
                currentRay.direction * hit.distance,
                Color.red,
                2f);

            Debug.Log(
                $"{name} hit {hit.collider.name} at {hit.point} " +
                $"with normal {hit.normal}");

            // Find the object that can receive damage.
            IDamageable damageable =
                hit.collider.GetComponentInParent<IDamageable>();

            // Base damage.
            float damage = weaponData.damage;

            // Check which part of the enemy was hit.
            Hitbox hitbox =
                hit.collider.GetComponent<Hitbox>();

            bool isHeadshot =
                hitbox != null &&
                hitbox.Type == Hitbox.HitboxType.Head;

            if (isHeadshot)
            {
                damage *= weaponData.headshotMultiplier;

                Debug.Log(
                    $"HEADSHOT! {weaponData.damage} -> {damage} damage");
            }

            // Direction of the shot.
            Vector3 hitDirection =
                ray.direction.sqrMagnitude > 0f
                    ? ray.direction.normalized
                    : hit.normal;

            // Final damage determines hit force too.
            float hitForce = damage * hitForceMultiplier;

            if (damageable != null)
            {
                NetworkHealth networkTarget = hit.collider.GetComponentInParent<NetworkHealth>();
                if (networkAuthority != null && networkAuthority.IsSpawned &&
                    !networkAuthority.IsServer && networkTarget != null)
                {
                    networkAuthority.RequestHitServerRpc(
                        networkTarget.NetworkObject,
                        hitDirection,
                        hitForce);
                }
                else
                {
                    damageable.TakeDamage(damage, hitDirection, hitForce);
                }
            }

            SpawnHitEffect(hit);

            // No penetration: stop at the first collider.
            if (remainingPenetrations <= 0)
                break;

            remainingPenetrations--;

            // Move the ray origin slightly past the surface
            // so we don't hit the same collider again.
            currentOrigin =
                hit.point + ray.direction * 0.01f;
        }

        SpawnMuzzleFlash();
    }

    private void SpawnMuzzleFlash()
    {
        if (weaponData == null || weaponData.muzzleFlash == null)
            return;

        Vector3 spawnPosition = muzzleTransform != null ? muzzleTransform.position : transform.position;
        Instantiate(weaponData.muzzleFlash, spawnPosition, Quaternion.identity);
    }

    private void SpawnHitEffect(RaycastHit hit)
    {
        if (weaponData == null || weaponData.hitEffect == null)
            return;

        Instantiate(weaponData.hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
    }

    private void TryStartReload()
    {
        if (weaponData == null || isReloading || currentAmmo >= weaponData.magazineSize || currentReserveAmmo <= 0)
            return;

        isReloading = true;
        reloadTimer = Mathf.Max(weaponData.reloadTime, 0f);
        interactUI?.StartProgress(reloadTimer, "Reloading...");
    }

    public void ApplyServerInput(bool firePressed, bool fireHeld, bool reloadPressed)
    {
        if (!SyncWithActiveItemInternal())
            return;

        if (isReloading)
            return;

        if (reloadPressed)
        {
            TryStartReload();
            return;
        }

        bool shouldFire = weaponData != null && (weaponData.automatic ? fireHeld : firePressed);
        if (shouldFire)
            TryFire();
    }

    private void FinishReload()
    {
        if (weaponData == null)
            return;

        isReloading = false;

        int ammoNeeded = weaponData.magazineSize - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, currentReserveAmmo);

        currentAmmo += ammoToLoad;
        currentReserveAmmo -= ammoToLoad;
        SaveAmmoState();
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
    }

    private void SaveAmmoState()
    {
        if (inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.IsWeapon)
            inventory.SetWeaponAmmo(inventory.ActiveItemInstance, currentAmmo, currentReserveAmmo);
    }

    private bool SyncWithActiveItemInternal()
    {
        ItemInstance activeInstance = inventory != null ? inventory.ActiveItemInstance : null;
        LootDataSO activeData = activeInstance?.Data;

        if (activeData == null || !activeData.IsWeapon)
        {
            equippedInstance = null;
            return false;
        }

        if (equippedInstance != activeInstance || weaponData != activeData.WeaponData)
        {
            equippedInstance = activeInstance;
            Equip(activeData.WeaponData);
        }

        return weaponData != null;
    }

    public int CurrentAmmo => currentAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public int MagazineSize => weaponData != null ? weaponData.magazineSize : 0;
    public int MaxReserveAmmo => weaponData != null ? weaponData.maxAmmo : 0;
    public bool IsReloading => isReloading;
    public WeaponDataSO WeaponData => weaponData;
}
