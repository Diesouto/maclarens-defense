using UnityEngine;

public class LootItem : MonoBehaviour, IInteractable
{
    [SerializeField] private LootDataSO lootData;
    [SerializeField] private bool hideOnPickup = true;

    public LootDataSO Data => lootData;
    public ItemInstance Instance { get; private set; }
    public bool IsCollected { get; private set; }

    private LootSpawnPoint spawnPoint;
    private TrainCargo cargo;

    private void Awake()
    {
        if (lootData != null && Instance == null)
            Instance = new ItemInstance(lootData);

        Instance?.BindWorldItem(this);
        ApplyRigidbodyMass();
    }

    public void SetData(LootDataSO newData)
    {
        lootData = newData;
        if (Instance == null || Instance.Data != newData)
            Instance = new ItemInstance(newData);

        Instance.BindWorldItem(this);
        ApplyRigidbodyMass();
    }

    public void SetInstance(ItemInstance itemInstance)
    {
        Instance = itemInstance ?? new ItemInstance(lootData);
        Instance.BindWorldItem(this);
        lootData = Instance.Data;
        ApplyRigidbodyMass();
    }

    // Keeps physics weight authored on LootDataSO instead of tuned by hand on every prefab.
    private void ApplyRigidbodyMass()
    {
        if (lootData == null)
            return;

        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null)
            rigidbody.mass = lootData.RigidbodyMass;
    }

    public void SetSpawnPoint(LootSpawnPoint point)
    {
        spawnPoint = point;
    }
    

    public void SetCargo(TrainCargo newCargo)
    {
        cargo = newCargo;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (interactor == null || IsCollected || lootData == null)
            return false;

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        return inventory != null && inventory.CanAdd(lootData);
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        if (lootData == null)
            return "Loot";

        return $"Pick up {lootData.DisplayName} ({lootData.Value})";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();

        if (inventory == null || !inventory.TryAdd(lootData, Instance))
            return;

        cargo?.RemoveItem(this);

        IsCollected = true;

        spawnPoint?.SetOccupied(false);

        if (hideOnPickup)
            gameObject.SetActive(false);
    }

    public static LootItem CreateDroppedLoot(LootDataSO data, Vector3 position, Quaternion rotation, ItemInstance itemInstance = null, Vector3? throwForce = null)
    {
        if (data == null)
            return null;

        GameObject instance = itemInstance?.WorldItem != null
                ? itemInstance.WorldItem.gameObject
                : data.WorldPrefab != null
                    ? Instantiate(data.WorldPrefab, position, rotation)
                    : new GameObject(data.DisplayName);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        LootItem lootItem = instance.GetComponent<LootItem>();
        if (lootItem == null)
            lootItem = instance.AddComponent<LootItem>();

        Rigidbody rigidbody = instance.GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = instance.AddComponent<Rigidbody>();

        rigidbody.mass = data.RigidbodyMass;
        rigidbody.isKinematic = false;
        rigidbody.useGravity = true;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            collider.enabled = true;

        foreach (LootItem childLootItem in instance.GetComponentsInChildren<LootItem>(true))
            childLootItem.enabled = true;

        foreach (BreakableOnImpact breakable in instance.GetComponentsInChildren<BreakableOnImpact>(true))
            breakable.enabled = true;

        BreakableOnImpact breakableComponent = instance.GetComponentInChildren<BreakableOnImpact>(true);
        BreakableOnImpact sourceBreakable = data.WorldPrefab != null
            ? data.WorldPrefab.GetComponentInChildren<BreakableOnImpact>(true)
            : null;
        if (breakableComponent == null && sourceBreakable != null)
        {
            breakableComponent = instance.AddComponent<BreakableOnImpact>();
            breakableComponent.CopySettingsFrom(sourceBreakable);
        }

        if (breakableComponent != null)
            breakableComponent.enabled = true;

        if (throwForce.HasValue && throwForce.Value.sqrMagnitude > 0f)
            rigidbody.AddForce(throwForce.Value, ForceMode.Impulse);

        Weapon weapon = instance.GetComponentInChildren<Weapon>(true);
        if (weapon != null)
            weapon.enabled = false;

        lootItem.SetInstance(itemInstance ?? new ItemInstance(data));
        lootItem.IsCollected = false;
        return lootItem;
    }
}
