using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkThreatState : NetworkBehaviour
{
    public static NetworkThreatState Instance { get; private set; }

    public NetworkVariable<float> CurrentThreat = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<ThreatLevel> CurrentLevel = new(
        ThreatLevel.Calm,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> PlayerCount = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> AliveEnemyCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private ThreatManager threatManager;
    [SerializeField] private EnemySpawner enemySpawner;

    public float CurrentThreatMultiplier { get; private set; } = 1f;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (threatManager == null)
            threatManager = ThreatManager.Instance;

        if (IsServer)
        {
            if (enemySpawner == null)
                enemySpawner = FindFirstObjectByType<EnemySpawner>();
            return;
        }

        CurrentThreat.OnValueChanged += HandleThreatChanged;
        CurrentLevel.OnValueChanged += HandleLevelChanged;
        ApplyToManager();
    }

    public override void OnNetworkDespawn()
    {
        CurrentThreat.OnValueChanged -= HandleThreatChanged;
        CurrentLevel.OnValueChanged -= HandleLevelChanged;
    }

    private void HandleThreatChanged(float previous, float current) => ApplyToManager();

    private void HandleLevelChanged(ThreatLevel previous, ThreatLevel current) => ApplyToManager();

    private void ApplyToManager()
    {
        if (threatManager == null)
            threatManager = ThreatManager.Instance;

        if (threatManager != null)
            threatManager.ApplyReplicatedThreat(CurrentThreat.Value, CurrentLevel.Value);
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        int playerCount = Mathf.Clamp(NetworkManager.Singleton.ConnectedClientsIds.Count, 1, 4);
        if (PlayerCount.Value != playerCount)
            PlayerCount.Value = playerCount;
        CurrentThreatMultiplier = RunManager.Instance != null ? RunManager.Instance.Scaling.threat : 1f;

        if (threatManager != null)
        {
            if (!Mathf.Approximately(CurrentThreat.Value, threatManager.CurrentThreat))
                CurrentThreat.Value = threatManager.CurrentThreat;
            if (CurrentLevel.Value != threatManager.CurrentLevel)
                CurrentLevel.Value = threatManager.CurrentLevel;
        }

        if (enemySpawner != null)
        {
            if (AliveEnemyCount.Value != enemySpawner.AliveEnemyCount)
                AliveEnemyCount.Value = enemySpawner.AliveEnemyCount;
        }
    }
}