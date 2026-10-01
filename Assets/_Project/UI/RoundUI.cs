using UnityEngine;

public class RoundUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI roundNumberText;

    [SerializeField] private RunManager runManager;

    private void OnEnable()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        if (runManager == null)
            return;

        runManager.OnRunStateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.OnRunStateChanged -= Refresh;
    }

    private void Refresh()
    {
        if (roundNumberText != null && runManager != null)
            roundNumberText.text = $"{runManager.CurrentQuotaRound} / {RunSettings.Describe(runManager.QuotasToWin)}";
    }
}
