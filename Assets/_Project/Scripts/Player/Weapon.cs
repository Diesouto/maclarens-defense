using System;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private float nextTimeToFire;
    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isReloading;
    private float reloadTimer;

    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponentInParent<PlayerInputHandler>();

        if (playerCamera == null)
            playerCamera = Camera.main;

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
        if (weaponData == null)
            return;

        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0f)
                FinishReload();
            return;
        }

        if (IsReloadPressed())
        {
            TryStartReload();
            return;
        }

        if (CanFire() && IsFirePressed())
            Fire();
    }

    private bool IsReloadPressed()
    {
        return inputHandler.ReloadPressed;
    }

    private bool IsFirePressed()
    {
        if (weaponData.automatic)
            return inputHandler.FireHeld;

        return inputHandler.FirePressed;
    }

    private bool CanFire()
    {
        return !isReloading && currentAmmo > 0 && Time.time >= nextTimeToFire;
    }

    private void Fire()
    {
        float fireRate = Mathf.Max(weaponData.fireRate, 0.01f);
        nextTimeToFire = Time.time + 1f / fireRate;
        currentAmmo--;
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
        gunshotParticleReference?.GetComponent<ParticleSystem>().Play();

        ShootRaycast();
    }

    private void ShootRaycast()
    {
        if (playerCamera == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));

        if (Physics.Raycast(ray, out RaycastHit hit, weaponData.range, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"Hit: {hit.collider.name} at {hit.point}");

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
        if (weaponData.muzzleFlash == null)
            return;

        Vector3 spawnPosition = muzzleTransform != null ? muzzleTransform.position : transform.position;
        Instantiate(weaponData.muzzleFlash, spawnPosition, Quaternion.identity);
    }

    private void SpawnHitEffect(RaycastHit hit)
    {
        if (weaponData.hitEffect == null)
            return;

        Instantiate(weaponData.hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
    }

    private void TryStartReload()
    {
        if (isReloading || currentAmmo >= weaponData.magazineSize || currentReserveAmmo <= 0)
            return;

        isReloading = true;
        reloadTimer = Mathf.Max(weaponData.reloadTime, 0f);
        interactUI?.StartProgress(reloadTimer, "Reloading...");
    }

    private void FinishReload()
    {
        isReloading = false;

        int ammoNeeded = weaponData.magazineSize - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, currentReserveAmmo);

        currentAmmo += ammoToLoad;
        currentReserveAmmo -= ammoToLoad;
        OnAmmoChanged?.Invoke(currentAmmo, currentReserveAmmo);
    }

    public int CurrentAmmo => currentAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public int MagazineSize => weaponData != null ? weaponData.magazineSize : 0;
    public int MaxReserveAmmo => weaponData != null ? weaponData.maxAmmo : 0;
    public bool IsReloading => isReloading;
}
