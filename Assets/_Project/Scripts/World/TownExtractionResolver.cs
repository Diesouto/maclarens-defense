using UnityEngine;
using Unity.Netcode;

public class TownExtractionResolver : MonoBehaviour
{
    [SerializeField] private TrainSplineFollower trainSplineFollower;
    [SerializeField] private RunManager runManager;
    [SerializeField] private Transform temporaryEntitiesRoot;

    private bool extractionResolved;

    private void Awake()
    {
        if (trainSplineFollower == null)
            trainSplineFollower = FindFirstObjectByType<TrainSplineFollower>();

        if (runManager == null)
            runManager = RunManager.Instance;
    }

    private void OnEnable()
    {
        if (trainSplineFollower != null)
            trainSplineFollower.OnTownExitReached += ResolveTownExit;

        if (runManager != null)
            runManager.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (trainSplineFollower != null)
            trainSplineFollower.OnTownExitReached -= ResolveTownExit;

        if (runManager != null)
            runManager.OnPhaseChanged -= HandlePhaseChanged;
    }

    // The crew may make several trips per quota, so every new trip needs its own extraction.
    private void HandlePhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.TravelingToTown)
            extractionResolved = false;
    }

    private void ResolveTownExit()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return;

        if (extractionResolved)
            return;

        extractionResolved = true;
        InTownTrigger.Instance?.AbandonPlayersStillInsideTown();
        BodyRecoveryManager.Instance?.ResolveBodiesAtTownExit();
        InTownTrigger.Instance?.DestroyLootInsideTown();
        EnemyController.DespawnAll();
        GhostController.DespawnAll();
        ThreatManager.Instance?.ResetThreat();
        DestroyTemporaryEntities();
    }

    private void DestroyTemporaryEntities()
    {
        if (temporaryEntitiesRoot == null)
            return;

        for (int index = temporaryEntitiesRoot.childCount - 1; index >= 0; index--)
        {
            GameObject entity = temporaryEntitiesRoot.GetChild(index).gameObject;
            NetworkObject networkObject = entity.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned &&
                NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                networkObject.Despawn(true);
            else
                Destroy(entity);
        }
    }
}
