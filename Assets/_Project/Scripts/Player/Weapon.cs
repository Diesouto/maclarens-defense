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

    private PlayerInventory inventory;
    private float nextTimeToFire;
    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isReloading;
    private float reloadTimer;
    private ItemInstance equippedInstance;

    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponentInParent<PlayerInputHandler>();

        inventory = GetComponentInParent<PlayerInventory>();

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

        if (inventory != null && inventory.ActiveItemInstance != null && inventory.ActiveItemInstance.Data.WeaponData == weaponData && inventory.TryGetWeaponAmmo(inventory.ActiveItemInstance, out int savedAmmo, out int savedReserveAmmo))
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
        gunshotParticleReference?.GetComponent<ParticleSystem>().Play();

        ShootRaycast();
    }

    private void ShootRaycast()
    {
        Debug.Log($"{name} is shooting a raycast from the camera!");

        if (playerCamera == null || weaponData == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));

        if (Physics.Raycast(ray, out RaycastHit hit, weaponData.range, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawRay(ray.origin, ray.direction * weaponData.range, Color.red, 2f);
            Debug.Log($"{name} hit {hit.collider.name} at {hit.point} with normal {hit.normal}");

            IDamageable damageable = null;
            if (hit.collider.TryGetComponent<IDamageable>(out var d))
                damageable = d;
            else
                damageable = hit.collider.GetComponentInParent<IDamageable>();

            Vector3 hitDirection = ray.direction.sqrMagnitude > 0f ? ray.direction.normalized : hit.normal;
            float hitForce = weaponData.damage * 12f;

            if (damageable != null)
                damageable.TakeDamage(weaponData.damage, hitDirection, hitForce);

            SpawnHitEffect(hit);
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
        if (inventory != null && inventory.ActiveItemInstance != null && inventory.ActiveItemInstance.Data.IsWeapon)
            inventory.SetWeaponAmmo(inventory.ActiveItemInstance, currentAmmo, currentReserveAmmo);
    }

    private bool SyncWithActiveItemInternal()
    {
        if (inventory == null || inventory.ActiveItemInstance == null || !inventory.ActiveItemInstance.Data.IsWeapon)
        {
            equippedInstance = null;
            return false;
        }

        if (equippedInstance != inventory.ActiveItemInstance || weaponData != inventory.ActiveItemInstance.Data.WeaponData)
        {
            equippedInstance = inventory.ActiveItemInstance;
            Equip(inventory.ActiveItemInstance.Data.WeaponData);
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
