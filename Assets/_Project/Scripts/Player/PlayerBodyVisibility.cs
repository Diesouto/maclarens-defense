using UnityEngine;

// Moves the given renderers to the "CameraHidden" layer and excludes that layer from the local
// player's own camera, so the player only sees hands/feet (whatever is left on default layers)
// while everyone else's camera (or a future spectator/other-player camera) still renders them in
// full, since only this camera's culling mask is touched.
public class PlayerBodyVisibility : MonoBehaviour
{
    private const string HiddenLayerName = "CameraHidden";

    [SerializeField] private Camera playerCamera;
    [Tooltip("Roots (e.g. head, torso, arms, legs) whose renderers should be invisible to this player's own camera. Leave hands/feet out of this list.")]
    [SerializeField] private Transform[] hiddenFromOwnCameraRoots;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        int hiddenLayer = LayerMask.NameToLayer(HiddenLayerName);
        if (hiddenLayer < 0)
        {
            Debug.LogWarning($"{nameof(PlayerBodyVisibility)}: layer '{HiddenLayerName}' is missing from Project Settings > Tags and Layers.", this);
            return;
        }

        foreach (Transform root in hiddenFromOwnCameraRoots)
        {
            if (root != null)
                SetLayerRecursively(root, hiddenLayer);
        }

        if (playerCamera != null)
            playerCamera.cullingMask &= ~(1 << hiddenLayer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
