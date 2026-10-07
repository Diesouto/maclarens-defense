using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Moves the given renderers to the "CameraHidden" layer and excludes that layer from the local
// player's own camera, so the player only sees hands/feet (whatever is left on default layers)
// while everyone else's camera (or a future spectator/other-player camera) still renders them in
// full, since only this camera's culling mask is touched. Only applied on the owning instance (or offline).
public class PlayerBodyVisibility : NetworkBehaviour
{
    private const string HiddenLayerName = "CameraHidden";

    [SerializeField] private Camera playerCamera;
    [Tooltip("Roots (e.g. head, torso, arms, legs) whose renderers should be invisible to this player's own camera. Leave hands/feet out of this list.")]
    [SerializeField] private Transform[] hiddenFromOwnCameraRoots;

    private bool applied;
    private Health health;
    private readonly Dictionary<Transform, int> originalLayers = new();
    private int originalCullingMask;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += RefreshVisibility;
            health.OnRevived += RefreshVisibility;
        }

        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= RefreshVisibility;
            health.OnRevived -= RefreshVisibility;
        }

        RestoreVisibility();
    }

    private void Start()
    {
        if (!IsSpawned)
            Apply();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            Apply();
        else
        {
            RestoreVisibility();
            applied = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        RestoreVisibility();
        applied = false;
    }

    private void Apply()
    {
        if (applied)
            return;
        int hiddenLayer = LayerMask.NameToLayer(HiddenLayerName);
        if (hiddenLayer < 0)
        {
            Debug.LogWarning($"{nameof(PlayerBodyVisibility)}: layer '{HiddenLayerName}' is missing from Project Settings > Tags and Layers.", this);
            return;
        }

        applied = true;
        if (playerCamera != null)
            originalCullingMask = playerCamera.cullingMask;

        if (hiddenFromOwnCameraRoots != null)
        {
            foreach (Transform root in hiddenFromOwnCameraRoots)
            {
                if (root != null)
                    SetLayerRecursively(root, hiddenLayer);
            }
        }

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (!applied)
            return;

        if (health != null && health.IsDead)
        {
            RestoreVisibility();
            return;
        }

        int hiddenLayer = LayerMask.NameToLayer(HiddenLayerName);
        if (hiddenLayer < 0)
            return;

        foreach (Transform part in originalLayers.Keys)
        {
            if (part != null)
                part.gameObject.layer = hiddenLayer;
        }

        if (playerCamera != null)
            playerCamera.cullingMask = originalCullingMask & ~(1 << hiddenLayer);
    }

    private void RestoreVisibility()
    {
        foreach (KeyValuePair<Transform, int> entry in originalLayers)
        {
            if (entry.Key != null)
                entry.Key.gameObject.layer = entry.Value;
        }

        if (applied && playerCamera != null)
            playerCamera.cullingMask = originalCullingMask;
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        if (!originalLayers.ContainsKey(root))
            originalLayers.Add(root, root.gameObject.layer);
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
