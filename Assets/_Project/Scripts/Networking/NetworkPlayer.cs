using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkPlayer : NetworkBehaviour
{
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
    [SerializeField, Min(1f)] private float maximumMovementSpeed = 12f;
    [SerializeField, Min(0f)] private float movementValidationTolerance = 0.75f;

    private Vector3 lastServerPosition;
    private Transform[] characterModels;
    private TextMeshPro nameplate;

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

        characterModels = FindCharacterModels(transform);
    }

    public override void OnNetworkSpawn()
    {
        lastServerPosition = transform.position;
        DisplayName.OnValueChanged += HandleDisplayNameChanged;
        CharacterIndex.OnValueChanged += HandleCharacterIndexChanged;
        ApplyLocalOwnership(IsOwner);
        ApplyCharacterSelection(CharacterIndex.Value);
        EnsureNameplate();
        UpdateNameplate(DisplayName.Value);
    }

    public override void OnNetworkDespawn()
    {
        DisplayName.OnValueChanged -= HandleDisplayNameChanged;
        CharacterIndex.OnValueChanged -= HandleCharacterIndexChanged;
        ApplyLocalOwnership(false);
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
        EnsureNameplate();
        UpdateNameplate(DisplayName.Value);
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

    private void EnsureNameplate()
    {
        if (nameplate != null)
            return;

        GameObject nameplateObject = new GameObject("PlayerNameplate");
        nameplateObject.transform.SetParent(transform, false);
        nameplateObject.transform.localPosition = Vector3.up * 2.6f;
        nameplate = nameplateObject.AddComponent<TextMeshPro>();
        nameplate.fontSize = 3f;
        nameplate.alignment = TextAlignmentOptions.Center;
        nameplate.color = Color.white;
        nameplate.outlineWidth = 0.12f;
        nameplate.outlineColor = Color.black;
        nameplate.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
    }

    private void UpdateNameplate(FixedString64Bytes playerName)
    {
        EnsureNameplate();
        nameplate.text = playerName.ToString();
    }

    private void LateUpdate()
    {
        if (nameplate == null || Camera.main == null)
            return;

        Vector3 toCamera = Camera.main.transform.position - nameplate.transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.001f)
            nameplate.transform.rotation = Quaternion.LookRotation(toCamera);
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
