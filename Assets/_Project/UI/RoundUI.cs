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

        runManager.OnDayChanged += Refresh;
        Refresh(runManager.CurrentDay);
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.OnDayChanged -= Refresh;
    }

    private void Refresh(int round)
    {
        if (roundNumberText != null)
            roundNumberText.text = round.ToString();
    }
}
