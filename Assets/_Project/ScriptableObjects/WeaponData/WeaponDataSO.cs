using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "MacLarens/Weapon")]
public class WeaponDataSO : ScriptableObject
{
    [Header("General")]
    public string weaponName;
    public Sprite icon;
    public int price;

    [Header("Damage")]
    public float damage = 20f;
    public float range = 100f;

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