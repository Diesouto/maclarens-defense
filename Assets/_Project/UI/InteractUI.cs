using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class InteractUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI interactText;
    [SerializeField] private Image interactProgressBar;
    [SerializeField] private Image interactProgressBarBackground;

    private Coroutine progressCoroutine;
    private string defaultText = "";

    public bool IsInProgress { get; private set; }

    private void Awake()
    {
        if (interactText != null)
            defaultText = interactText.text;

        SetVisible(false);
    }

    /// <summary>
    /// Start a generic progress UI that fills over <paramref name="duration"/> seconds.
    /// Optional <paramref name="displayText"/> overrides the default text while progress runs.
    /// </summary>
    public void StartProgress(float duration, string displayText = null)
    {
        if (duration <= 0f)
        {
            FinishProgress();
            return;
        }

        if (progressCoroutine != null)
            StopCoroutine(progressCoroutine);

        progressCoroutine = StartCoroutine(ProgressRoutine(duration, displayText));
    }

    /// <summary>
    /// Cancel an ongoing progress and reset UI immediately.
    /// </summary>
    public void CancelProgress()
    {
        if (progressCoroutine != null)
        {
            StopCoroutine(progressCoroutine);
            progressCoroutine = null;
        }

        IsInProgress = false;
        ResetUI();
        SetVisible(false);
    }

    /// <summary>
    /// Finish progress (used when the operation completes normally).
    /// Resets and hides the UI.
    /// </summary>
    public void FinishProgress()
    {
        IsInProgress = false;
        ResetUI();
        SetVisible(false);
        progressCoroutine = null;
    }

    private IEnumerator ProgressRoutine(float duration, string displayText)
    {
        IsInProgress = true;
        SetVisible(true);

        if (interactText != null)
            interactText.text = string.IsNullOrEmpty(displayText) ? defaultText : displayText;

        if (interactProgressBar != null)
            interactProgressBar.fillAmount = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (interactProgressBar != null)
                interactProgressBar.fillAmount = t;
            yield return null;
        }

        if (interactProgressBar != null)
            interactProgressBar.fillAmount = 1f;

        FinishProgress();
    }

    private void ResetUI()
    {
        if (interactText != null)
            interactText.text = defaultText;

        if (interactProgressBar != null)
            interactProgressBar.fillAmount = 0f;
    }

    private void SetVisible(bool visible)
    {
        if (interactText != null)
            interactText.gameObject.SetActive(visible);

        if (interactProgressBar != null)
            interactProgressBar.gameObject.SetActive(visible);

        if (interactProgressBarBackground != null)
            interactProgressBarBackground.gameObject.SetActive(visible);
    }
}
