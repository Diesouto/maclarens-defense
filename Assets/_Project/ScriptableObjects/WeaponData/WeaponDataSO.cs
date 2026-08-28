using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "MacLarens/Weapon")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Damage")]
    public float damage = 20f;
    public float range = 100f;
    public float headshotMultiplier = 2f;

    [Header("Ballistics")]
    public float shotRadius = 0.03f;
    public bool canPenetrate = false;
    public int maxPenetrations = 0;

    [Header("Fire")]
    public float fireRate = 3f;
    public bool automatic = false;

    [Header("Ammo")]
    public int magazineSize = 6;
    public int maxAmmo = 24;
    public float reloadTime = 2f;

    [Header("Effects")]
    public GameObject muzzleFlash;
    public GameObject hitEffect;
    public AudioClip shotSound;
}