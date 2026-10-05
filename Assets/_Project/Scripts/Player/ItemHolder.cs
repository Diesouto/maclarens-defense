using System;
using UnityEngine;

public class ItemHolder : MonoBehaviour
{
    public event Action<Weapon> OnRuntimeWeaponChanged;

    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerPoseController poseController;
    [SerializeField] private NetworkPlayer networkPlayer;

    private GameObject currentHeldVisual;
    private LootDataSO currentItem;
    private ItemInstance currentInstance;
    private Weapon residentWeapon; // Weapon that lives on the player hierarchy permanently
    private Weapon playerWeapon;   // Currently active runtime weapon (resident or from held visual)
    private Weapon notifiedWeapon;
    private bool hasRefreshed;

    public Weapon RuntimeWeapon => playerWeapon;

    // Remote copies show the item in the hand bone; the local player sees the camera-framed hold point.
    private bool UseThirdPersonView => networkPlayer != null && networkPlayer != NetworkPlayer.Local;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (poseController == null)
            poseController = GetComponent<PlayerPoseController>();

        if (networkPlayer == null)
            networkPlayer = GetComponent<NetworkPlayer>();

        residentWeapon = GetComponentInChildren<Weapon>(true);
        playerWeapon = residentWeapon;
        NotifyRuntimeWeaponChanged();

        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;
    }

    private void OnEnable()
    {
        NetworkPlayer.LocalPlayerChanged += HandleLocalPlayerChanged;
    }

    private void OnDisable()
    {
        NetworkPlayer.LocalPlayerChanged -= HandleLocalPlayerChanged;
    }

    private void Start()
    {
        Refresh();
    }

    private void HandleLocalPlayerChanged(NetworkPlayer _)
    {
        hasRefreshed = false;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

    private void Refresh()
    {
        LootDataSO item = inventory != null ? inventory.ActiveItem : null;
        ItemInstance instance = inventory != null ? inventory.ActiveItemInstance : null;
        if (hasRefreshed && item == currentItem && instance == currentInstance)
            return;

        hasRefreshed = true;
        currentItem = item;
        currentInstance = instance;
        DestroyCurrentVisual();

        bool hasActiveWeapon = item != null && item.IsWeapon;

        // Resident weapon: only show it when no held prefab overrides the visual.
        bool useResidentVisual = hasActiveWeapon && item != null && item.HeldPrefab == null;
        if (residentWeapon != null)
            ApplyPlayerWeaponState(hasActiveWeapon && useResidentVisual, useResidentVisual);

        if (item == null)
        {
            SetPlayerWeapon(residentWeapon);
            return;
        }

        bool thirdPerson = UseThirdPersonView;
        Transform holdPoint = poseController != null ? poseController.GetItemHoldPoint(item.AnimationProfile, thirdPerson) : transform;
        if (holdPoint == null)
            holdPoint = transform;

        Vector3 heldPosition = thirdPerson ? item.ThirdPersonPositionOffset : item.HeldPositionOffset;
        Quaternion heldRotation = Quaternion.Euler(thirdPerson ? item.ThirdPersonRotationOffset : item.HeldRotationOffset);

        if (item.IsWeapon && item.HeldPrefab == null && residentWeapon != null)
        {
            residentWeapon.transform.SetParent(holdPoint, false);
            residentWeapon.transform.localPosition = heldPosition;
            residentWeapon.transform.localRotation = heldRotation;
            residentWeapon.transform.localScale = Vector3.one;
            residentWeapon.SyncWithActiveItem();
            SetPlayerWeapon(residentWeapon);
            return;
        }

        GameObject visualPrefab = item.HeldPrefab != null ? item.HeldPrefab : item.WorldPrefab;
        if (visualPrefab == null)
            return;

        currentHeldVisual = InstantiateHeldVisual(visualPrefab, holdPoint);
        currentHeldVisual.transform.localPosition = heldPosition;
        currentHeldVisual.transform.localRotation = heldRotation;
        currentHeldVisual.transform.localScale = Vector3.one;

        if (item.IsWeapon)
        {
            Weapon visualWeapon = currentHeldVisual.GetComponentInChildren<Weapon>(true);
            if (visualWeapon != null)
            {
                // Fix references: the visual was just instantiated so Awake may have found wrong parents.
                var playerInput = inventory != null ? inventory.GetComponent<PlayerInputHandler>() : null;
                visualWeapon.Initialize(playerInput, inventory, Camera.main);
                visualWeapon.enabled = true;
                SetPlayerWeapon(visualWeapon);
            }
        }
        else
        {
            foreach (Weapon w in currentHeldVisual.GetComponentsInChildren<Weapon>(true))
                w.enabled = false;
        }

        foreach (Collider collider in currentHeldVisual.GetComponentsInChildren<Collider>())
            collider.enabled = false;

        foreach (Rigidbody rigidbody in currentHeldVisual.GetComponentsInChildren<Rigidbody>())
        {
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
        }

        foreach (LootItem lootItem in currentHeldVisual.GetComponentsInChildren<LootItem>())
            lootItem.enabled = false;
    }

    // Instantiated under an inactive parent so Awake is deferred until world-only components are gone:
    // a WorldPrefab carries a NetworkObject, LootItem and physics that must never live in a player's hand.
    private static GameObject InstantiateHeldVisual(GameObject prefab, Transform holdPoint)
    {
        GameObject staging = new GameObject("HeldVisualStaging");
        staging.SetActive(false);

        GameObject visual = Instantiate(prefab, staging.transform);
        // Dependents first: NetworkRigidbody requires NetworkTransform, which requires NetworkObject.
        StripWorldComponents<Unity.Netcode.Components.NetworkRigidbodyBase>(visual);
        StripWorldComponents<Unity.Netcode.NetworkBehaviour>(visual);
        StripWorldComponents<Unity.Netcode.NetworkObject>(visual);
        StripWorldComponents<BreakableOnImpact>(visual);
        StripWorldComponents<LootItem>(visual);
        StripWorldComponents<TrainPassenger>(visual);
        StripWorldComponents<Joint>(visual);
        StripWorldComponents<Rigidbody>(visual);
        StripWorldComponents<Collider>(visual);

        visual.transform.SetParent(holdPoint, false);
        Destroy(staging);
        return visual;
    }

    private static void StripWorldComponents<T>(GameObject root) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            DestroyImmediate(component);
    }

    private void DestroyCurrentVisual()
    {
        if (currentHeldVisual == null)
            return;

        // If the active weapon lives on the visual, revert to the resident weapon before destroying.
        if (playerWeapon != null && playerWeapon != residentWeapon)
            SetPlayerWeapon(residentWeapon);

        Destroy(currentHeldVisual);
        currentHeldVisual = null;
    }

    public Transform ThrowPoint => poseController != null && currentItem != null
        ? poseController.GetItemHoldPoint(currentItem.AnimationProfile)
        : transform;

    private void SetPlayerWeapon(Weapon weapon)
    {
        if (playerWeapon == weapon)
            return;

        playerWeapon = weapon;
        NotifyRuntimeWeaponChanged();
    }

    private void ApplyPlayerWeaponState(bool isActiveWeapon, bool usePlayerWeaponVisual)
    {
        if (residentWeapon == null)
            return;

        residentWeapon.enabled = isActiveWeapon;
        residentWeapon.gameObject.SetActive(isActiveWeapon);

        foreach (Renderer renderer in residentWeapon.GetComponentsInChildren<Renderer>(true))
        {
            bool shouldStayVisible = renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer;
            renderer.enabled = usePlayerWeaponVisual || shouldStayVisible;
        }
    }

    private void NotifyRuntimeWeaponChanged()
    {
        if (notifiedWeapon == playerWeapon)
            return;

        notifiedWeapon = playerWeapon;
        OnRuntimeWeaponChanged?.Invoke(playerWeapon);
    }
}
