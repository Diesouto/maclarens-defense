using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkPlayer : NetworkBehaviour
{
    public NetworkVariable<bool> IsAbandoned = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private NetworkWeaponAuthority weaponAuthority;
    [SerializeField] private Camera[] playerCameras;
    [SerializeField, Min(1f)] private float maximumMovementSpeed = 12f;
    [SerializeField, Min(0f)] private float movementValidationTolerance = 0.75f;

    private Vector3 lastServerPosition;

    public bool IsLocalPlayer => IsOwner;

    public void MarkAbandoned()
    {
        if (IsServer)
            IsAbandoned.Value = true;
    }

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerInputHandler == null)
            playerInputHandler = GetComponent<PlayerInputHandler>();

        if (playerInteractor == null)
            playerInteractor = GetComponent<PlayerInteractor>();

        if (weaponAuthority == null)
            weaponAuthority = GetComponent<NetworkWeaponAuthority>();

        if (playerCameras == null || playerCameras.Length == 0)
            playerCameras = GetComponentsInChildren<Camera>(true);
    }

    public override void OnNetworkSpawn()
    {
        lastServerPosition = transform.position;
        ApplyLocalOwnership(IsOwner);
    }

    public override void OnNetworkDespawn()
    {
        ApplyLocalOwnership(false);
    }

    private void Update()
    {
        if (!IsServer || NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening)
            return;

        float maximumDistance = maximumMovementSpeed * Time.deltaTime + movementValidationTolerance;
        if (Vector3.Distance(lastServerPosition, transform.position) > maximumDistance)
        {
            transform.position = lastServerPosition;
            return;
        }

        lastServerPosition = transform.position;
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
