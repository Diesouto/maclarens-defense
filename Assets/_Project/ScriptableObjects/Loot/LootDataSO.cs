using UnityEngine;

public enum InventoryItemType
{
    Loot,
    Weapon,
    Consumable,
    Tool
}

public enum ItemAnimationProfile
{
    Carry,
    Pistol,
    Shotgun,
}

[CreateAssetMenu(fileName = "LootData", menuName = "MacLarens/Loot Data")]
public class LootDataSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName = "Loot";
    [SerializeField] private Sprite icon;
    [SerializeField] private int value = 25;
    [SerializeField] private int price = 0;

    [Header("Spawn")]
    [SerializeField, Min(0f)] private float spawnWeight = 100f;

    [Header("Inventory")]
    [SerializeField] private InventoryItemType itemType = InventoryItemType.Loot;
    [SerializeField] private bool isHeavy = false;
    [SerializeField] private bool isTwoHanded = false;
    [SerializeField] private ItemAnimationProfile animationProfile = ItemAnimationProfile.Carry;

    [Header("Weapon binding")]
    [SerializeField] private WeaponDataSO weaponData;

    [Header("World")]
    [SerializeField] private GameObject worldPrefab;
    [SerializeField] private GameObject heldPrefab;

    public string ItemId => string.IsNullOrEmpty(itemId) ? name : itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public int Value => value;
    public int Price => price;
    public float SpawnWeight => spawnWeight;

    public InventoryItemType ItemType => itemType;
    public GameObject WorldPrefab => worldPrefab;
    public GameObject HeldPrefab => heldPrefab;
    public bool IsHeavy => isHeavy;
    public bool IsTwoHanded => isTwoHanded;
    public ItemAnimationProfile AnimationProfile => animationProfile;
    public WeaponDataSO WeaponData => weaponData;
    public bool IsWeapon => itemType == InventoryItemType.Weapon && weaponData != null;
}
