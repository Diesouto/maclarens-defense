using UnityEngine;

public class ThreatUI : MonoBehaviour
{
    [SerializeField] private ThreatManager threatManager;
    [SerializeField] private TMPro.TextMeshProUGUI threatText;
    [SerializeField] private UnityEngine.UI.Image threatFillBar;

    [SerializeField] private string threatFormat = "Peligro: {0:0}";

    private void OnEnable()
    {
        if (threatManager == null)
            threatManager = ThreatManager.Instance;

        if (threatManager != null)
            threatManager.OnThreatChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (threatManager != null)
            threatManager.OnThreatChanged -= Refresh;
    }

    private void Refresh()
    {
        if (threatManager == null)
            return;

        if (threatText != null)
            threatText.text = string.Format(threatFormat, threatManager.CurrentThreat);

        if (threatFillBar != null)
        {
            threatFillBar.fillAmount = threatManager.MaxThreat > 0f
                ? threatManager.CurrentThreat / threatManager.MaxThreat
                : 0f;
        }
    }
}
