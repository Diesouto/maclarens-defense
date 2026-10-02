using TMPro;
using UnityEngine;

// HUD countdown for the current quota. Hidden until the train first leaves for this quota.
public class QuotaTimerUI : MonoBehaviour
{
    [SerializeField] private RunManager runManager;
    [Tooltip("Optional. When empty a centered label is created under this object.")]
    [SerializeField] private TMP_Text timerText;
    [Tooltip("Hidden until the timer starts. Must not be this GameObject. Defaults to timerText.")]
    [SerializeField] private GameObject visibilityRoot;
    [SerializeField] private string timerFormat = "{0:00}:{1:00}";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(0.95f, 0.3f, 0.3f);
    [SerializeField, Min(0f)] private float warningThresholdSeconds = 60f;

    private int lastShownSeconds = -1;

    private void Awake()
    {
        if (timerText == null)
            timerText = CreateDefaultLabel();

        if (visibilityRoot == gameObject)
            visibilityRoot = timerText.gameObject;

        if (visibilityRoot == null && timerText != null && timerText.gameObject != gameObject)
            visibilityRoot = timerText.gameObject;
    }

    private void Update()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        bool visible = runManager != null && runManager.HasTimerStarted;
        if (visibilityRoot != null && visibilityRoot.activeSelf != visible)
            visibilityRoot.SetActive(visible);

        if (!visible || timerText == null)
            return;

        int seconds = Mathf.CeilToInt(runManager.TimeRemaining);
        if (seconds == lastShownSeconds)
            return;

        lastShownSeconds = seconds;
        timerText.text = string.Format(timerFormat, seconds / 60, seconds % 60);
        timerText.color = runManager.TimeRemaining <= warningThresholdSeconds ? warningColor : normalColor;
    }

    private TMP_Text CreateDefaultLabel()
    {
        var labelObject = new GameObject("QuotaTimerLabel", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);

        var rect = (RectTransform)labelObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(300f, 70f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 48f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }
}
