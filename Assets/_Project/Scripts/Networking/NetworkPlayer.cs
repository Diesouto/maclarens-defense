using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private Camera[] playerCameras;

    public bool IsLocalPlayer => IsOwner;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerInputHandler == null)
            playerInputHandler = GetComponent<PlayerInputHandler>();

        if (playerInteractor == null)
            playerInteractor = GetComponent<PlayerInteractor>();

        if (playerCameras == null || playerCameras.Length == 0)
            playerCameras = GetComponentsInChildren<Camera>(true);
    }

    public override void OnNetworkSpawn()
    {
        ApplyLocalOwnership(IsOwner);
    }

    public override void OnNetworkDespawn()
    {
        ApplyLocalOwnership(false);
    }

    private void ApplyLocalOwnership(bool isLocal)
    {
        if (playerController != null)
            playerController.enabled = isLocal;

        if (playerInputHandler != null)
            playerInputHandler.enabled = isLocal;

        if (playerInteractor != null)
            playerInteractor.enabled = isLocal;

        if (playerCameras == null)
            return;

        foreach (Camera playerCamera in playerCameras)
        {
            if (playerCamera != null)
                playerCamera.enabled = isLocal;
        }
    }
}
