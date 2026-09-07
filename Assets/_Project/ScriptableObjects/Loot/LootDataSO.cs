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
    [SerializeField, Min(0.01f)] private float rigidbodyMass = 1f;

    [Header("Carry Pose Override")]
    [Tooltip("Local offset applied on top of the default hold point, for oversized/awkward items (e.g. vault) that would otherwise obstruct the camera.")]
    [SerializeField] private Vector3 heldPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 heldRotationOffset = Vector3.zero;

    public string ItemId => string.IsNullOrEmpty(itemId) ? name : itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public int Value => value;
    public int Price => price;
    public float SpawnWeight => spawnWeight;

    public InventoryItemType ItemType => itemType;
    public GameObject WorldPrefab => worldPrefab;
    public GameObject HeldPrefab => heldPrefab;
    public float RigidbodyMass => rigidbodyMass;
    public Vector3 HeldPositionOffset => heldPositionOffset;
    public Vector3 HeldRotationOffset => heldRotationOffset;
    public bool IsHeavy => isHeavy;
    public bool IsTwoHanded => isTwoHanded;
    public ItemAnimationProfile AnimationProfile => animationProfile;
    public WeaponDataSO WeaponData => weaponData;
    public bool IsWeapon => itemType == InventoryItemType.Weapon && weaponData != null;
}
