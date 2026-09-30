using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer Local { get; private set; }
    public static event Action<NetworkPlayer> LocalPlayerChanged;

    public NetworkVariable<FixedString64Bytes> DisplayName = new(
        new FixedString64Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CharacterIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsAbandoned = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private NetworkWeaponAuthority weaponAuthority;
    [SerializeField] private Camera[] playerCameras;
    [SerializeField] private CinemachineCamera[] playerVirtualCameras;
    [SerializeField] private TMP_Text playerNameLabel;
    [SerializeField, Min(1f)] private float maximumMovementSpeed = 12f;
    [SerializeField, Min(0f)] private float movementValidationTolerance = 0.75f;

    private Vector3 lastServerPosition;
    private Transform[] characterModels;
    private WorldSpaceBillboard nameplateBillboard;
    private Coroutine outputCameraSearch;

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

        if (playerVirtualCameras == null || playerVirtualCameras.Length == 0)
            playerVirtualCameras = GetComponentsInChildren<CinemachineCamera>(true);

        if (playerNameLabel == null)
        {
            foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.gameObject.name == "PlayerName")
                {
                    playerNameLabel = label;
                    break;
                }
            }
        }

        if (playerNameLabel != null)
            nameplateBillboard = playerNameLabel.GetComponentInParent<WorldSpaceBillboard>();

        characterModels = FindCharacterModels(transform);
    }

    public override void OnNetworkSpawn()
    {
        lastServerPosition = transform.position;
        DisplayName.OnValueChanged += HandleDisplayNameChanged;
        CharacterIndex.OnValueChanged += HandleCharacterIndexChanged;
        ApplyLocalOwnership(IsOwner);
        ApplyCharacterSelection(CharacterIndex.Value);
        UpdateNameplate(DisplayName.Value);
        if (IsOwner)
        {
            SetLocal(this);
            BeginOutputCameraSearch();
        }
    }

    public override void OnNetworkDespawn()
    {
        DisplayName.OnValueChanged -= HandleDisplayNameChanged;
        CharacterIndex.OnValueChanged -= HandleCharacterIndexChanged;
        ApplyLocalOwnership(false);
        if (Local == this)
            SetLocal(null);
        if (outputCameraSearch != null)
        {
            StopCoroutine(outputCameraSearch);
            outputCameraSearch = null;
        }
    }

    public void SetServerProfile(string playerName, int characterIndex)
    {
        if (!IsServer)
            return;

        string safeName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        if (safeName.Length > 24)
            safeName = safeName.Substring(0, 24);

        DisplayName.Value = new FixedString64Bytes(safeName);
        CharacterIndex.Value = NormalizeCharacterIndex(characterIndex);
        ApplyCharacterSelection(CharacterIndex.Value);
        UpdateNameplate(DisplayName.Value);
    }

    public void ConfigureOfflinePlayer(string playerName, int characterIndex)
    {
        string safeName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        if (safeName.Length > 24)
            safeName = safeName.Substring(0, 24);

        ApplyLocalOwnership(true);
        ApplyCharacterSelection(characterIndex);
        SetPlayerNameLabel(safeName);
        SetLocal(this);
        BeginOutputCameraSearch();
    }

    private static void SetLocal(NetworkPlayer player)
    {
        Local = player;
        LocalPlayerChanged?.Invoke(player);
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

        if (playerCameras != null)
        {
            foreach (Camera playerCamera in playerCameras)
            {
                if (playerCamera != null)
                    playerCamera.enabled = isLocal;
            }
        }

        if (playerVirtualCameras == null)
            return;

        foreach (CinemachineCamera virtualCamera in playerVirtualCameras)
        {
            if (virtualCamera == null)
                continue;

            virtualCamera.enabled = isLocal;
            if (isLocal)
            {
                virtualCamera.Priority = 1000;
                virtualCamera.Prioritize();
            }
        }
    }

    private void HandleDisplayNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        UpdateNameplate(newValue);
    }

    private void HandleCharacterIndexChanged(int previousValue, int newValue)
    {
        ApplyCharacterSelection(newValue);
    }

    private int NormalizeCharacterIndex(int index)
    {
        return characterModels == null || characterModels.Length == 0
            ? 0
            : (index % characterModels.Length + characterModels.Length) % characterModels.Length;
    }

    private void ApplyCharacterSelection(int index)
    {
        if (characterModels == null || characterModels.Length == 0)
            characterModels = FindCharacterModels(transform);

        if (characterModels.Length == 0)
            return;

        int selectedIndex = NormalizeCharacterIndex(index);
        for (int i = 0; i < characterModels.Length; i++)
            characterModels[i].gameObject.SetActive(i == selectedIndex);
    }

    private void UpdateNameplate(FixedString64Bytes playerName)
    {
        SetPlayerNameLabel(playerName.ToString());
    }

    private void BeginOutputCameraSearch()
    {
        if (outputCameraSearch == null)
            outputCameraSearch = StartCoroutine(FindOutputCameraThenBind());
    }

    private IEnumerator FindOutputCameraThenBind()
    {
        Camera outputCamera = null;
        CinemachineBrain brain = null;
        while (true)
        {
            if (outputCamera == null)
                outputCamera = Camera.main;

            if (outputCamera == null)
            {
                brain = FindFirstObjectByType<CinemachineBrain>();
                if (brain != null)
                    outputCamera = brain.OutputCamera;
            }

            if (outputCamera == null)
            {
                yield return null;
                continue;
            }

            if (!outputCamera.enabled)
            {
                outputCamera = null;
                yield return null;
                continue;
            }

            playerController?.SetOutputCamera(outputCamera);
            nameplateBillboard?.SetTargetCamera(outputCamera);

            if (brain == null)
                brain = outputCamera.GetComponent<CinemachineBrain>();

            if (playerVirtualCameras == null || playerVirtualCameras.Length == 0)
            {
                Debug.LogError("NetworkPlayer: no CinemachineCamera found on the spawned Player.", this);
                outputCameraSearch = null;
                yield break;
            }

            yield return null;

            CinemachineCamera ownedCamera = null;
            foreach (CinemachineCamera virtualCamera in playerVirtualCameras)
            {
                if (virtualCamera != null && virtualCamera.enabled)
                {
                    ownedCamera = virtualCamera;
                    break;
                }
            }

            if (brain == null)
                brain = outputCamera.GetComponent<CinemachineBrain>();

            if (brain == null)
            {
                Debug.LogWarning("NetworkPlayer: output Camera found without a CinemachineBrain; rendering directly from the output Camera.", outputCamera);
                outputCameraSearch = null;
                yield break;
            }

            if (ownedCamera != null && brain.IsLiveChild(ownedCamera))
                Debug.Log($"NetworkPlayer: CinemachineBrain is driving owned camera '{ownedCamera.Name}'.", this);
            else
                Debug.LogError($"NetworkPlayer: CinemachineBrain did not select this player's virtual camera. Active camera: '{brain.ActiveVirtualCamera?.Name ?? "none"}', channel match: {ownedCamera != null && brain.IsValidChannel(ownedCamera)}.", this);

            outputCameraSearch = null;
            yield break;
        }
    }

    private void SetPlayerNameLabel(string value)
    {
        if (playerNameLabel == null)
        {
            Debug.LogWarning("NetworkPlayer: PlayerName TMP label is not assigned or present under the player prefab.", this);
            return;
        }

        playerNameLabel.text = value;
    }

    private static Transform[] FindCharacterModels(Transform root)
    {
        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        var models = new List<Transform>();
        foreach (Transform candidate in allTransforms)
        {
            if (candidate.name.StartsWith("Character_") && candidate.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                models.Add(candidate);
        }

        models.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return models.ToArray();
    }
}
