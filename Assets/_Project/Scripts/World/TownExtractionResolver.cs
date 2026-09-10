using UnityEngine;

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
            runManager.OnDayChanged += ResetForNextDay;
    }

    private void OnDisable()
    {
        if (trainSplineFollower != null)
            trainSplineFollower.OnTownExitReached -= ResolveTownExit;

        if (runManager != null)
            runManager.OnDayChanged -= ResetForNextDay;
    }

    private void ResetForNextDay(int day)
    {
        extractionResolved = false;
    }

    private void ResolveTownExit()
    {
        if (extractionResolved)
            return;

        extractionResolved = true;
        InTownTrigger.Instance?.AbandonPlayersStillInsideTown();
        InTownTrigger.Instance?.DestroyLootInsideTown();
        EnemyController.DespawnAll();
        ThreatManager.Instance?.ResetThreat();
        DestroyTemporaryEntities();
    }

    private void DestroyTemporaryEntities()
    {
        if (temporaryEntitiesRoot == null)
            return;

        for (int index = temporaryEntitiesRoot.childCount - 1; index >= 0; index--)
            Destroy(temporaryEntitiesRoot.GetChild(index).gameObject);
    }
}
