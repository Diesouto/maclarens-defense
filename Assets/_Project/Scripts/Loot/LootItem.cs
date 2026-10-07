using UnityEngine;
using Unity.Netcode;

public class LootItem : MonoBehaviour, IInteractable
{
    [SerializeField] private LootDataSO lootData;
    [SerializeField] private bool hideOnPickup = true;

    public LootDataSO Data => lootData;
    public ItemInstance Instance { get; private set; }
    public bool IsCollected { get; private set; }
    public TrainCargo Cargo => cargo;

    private LootSpawnPoint spawnPoint;
    private TrainCargo cargo;

    private void Awake()
    {
        LootCatalog.Register(lootData);

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

        // Clients have the replicated inventory, so they can pre-check space and avoid a prompt the host would reject.
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        return inventory != null && inventory.CanAdd(lootData);
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        if (lootData == null)
            return "Loot";

        PlayerInventory inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
        if (inventory != null && !inventory.CanAdd(lootData))
            return $"{lootData.DisplayName} - no hay espacio";

        return $"Recoger {lootData.DisplayName} ({lootData.Value})";
    }

    public void Interact(PlayerInteractor interactor)
    {
        NetworkLootItem networkLoot = GetComponent<NetworkLootItem>();
        if (networkLoot != null && networkLoot.IsSpawned && !networkLoot.IsServer)
        {
            networkLoot.RequestPickupServerRpc();
            return;
        }

        // A client collecting locally desyncs from the host's inventory (item vanishes, hits rejected).
        if (NetworkRole.IsClientOnly)
        {
            Debug.LogWarning($"LootItem: '{name}' has no spawned NetworkLootItem; clients can't pick it up.", this);
            return;
        }

        TryCollect(interactor != null ? interactor.GetComponent<PlayerInventory>() : null);
    }

    public bool TryCollect(PlayerInventory inventory)
    {
        if (inventory == null || !CanAddToInventory(inventory))
            return false;

        if (!inventory.TryAdd(lootData, Instance))
            return false;

        cargo?.RemoveItem(this);

        if (Instance != null && !Instance.HasTriggeredThreat)
        {
            Instance.MarkThreatTriggered();
            ThreatManager.Instance?.RegisterLootPickup();
        }

        IsCollected = true;
        spawnPoint?.SetOccupied(false);
        if (TryGetComponent(out NetworkLootItem networkLoot))
            networkLoot.SetCollectedOnServer(true);

        if (hideOnPickup)
            gameObject.SetActive(false);

        return true;
    }

    private bool CanAddToInventory(PlayerInventory inventory)
    {
        return inventory.CanAdd(lootData);
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

        bool isThrown = throwForce.HasValue && throwForce.Value.sqrMagnitude > 0f;
        if (breakableComponent != null)
        {
            breakableComponent.enabled = true;
            if (isThrown)
                breakableComponent.UseThrownImpactGrace();
        }

        if (isThrown)
            rigidbody.AddForce(throwForce.Value, ForceMode.Impulse);

        Weapon weapon = instance.GetComponentInChildren<Weapon>(true);
        if (weapon != null)
            weapon.enabled = false;

        lootItem.SetInstance(itemInstance ?? new ItemInstance(data));
        lootItem.IsCollected = false;
        LootRegistry.Instance?.Register(lootItem);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer && networkObject != null && !networkObject.IsSpawned)
            networkObject.Spawn(true);

        if (instance.TryGetComponent(out NetworkLootItem networkLoot))
            networkLoot.SetCollectedOnServer(false);

        return lootItem;
    }
}
