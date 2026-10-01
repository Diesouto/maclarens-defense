using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class InteractUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI interactText;
    [Tooltip("Optional separate label for the key hint. When empty, the hint is prepended as a first line of interactText.")]
    [SerializeField] private TextMeshProUGUI keyHintText;
    [SerializeField] private string interactKeyLabel = "E";
    [SerializeField] private Image interactProgressBar;
    [SerializeField] private Image interactProgressBarBackground;
    [SerializeField] private Color availableColor = Color.white;
    [Tooltip("Used when the object is interactable in principle but blocked right now (no money, full inventory...).")]
    [SerializeField] private Color blockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private Coroutine progressCoroutine;
    private string defaultText = "";

    public bool IsInProgress { get; private set; }

    private void Awake()
    {
        if (interactText != null)
            defaultText = interactText.text;

        SetVisible(false);
    }

    public void ShowPrompt(string displayText, bool canInteract = true)
    {
        SetPromptText(displayText, canInteract);

        // A running progress bar owns its own visibility; only the text is refreshed under it.
        if (IsInProgress)
            return;

        SetPromptVisible(true);
    }

    public void HidePrompt()
    {
        if (IsInProgress)
            return;

        ResetUI();
        SetPromptVisible(false);
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

    public void StartHeldProgress(float duration, string displayText = null)
    {
        if (progressCoroutine != null)
            StopCoroutine(progressCoroutine);

        progressCoroutine = StartCoroutine(HeldProgressRoutine(duration, displayText));
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
        SetPromptText(displayText, true);

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

    private IEnumerator HeldProgressRoutine(float duration, string displayText)
    {
        IsInProgress = true;
        SetVisible(true);
        SetPromptText(displayText, true);

        if (interactProgressBar != null)
            interactProgressBar.fillAmount = 0f;

        float elapsed = 0f;
        while (elapsed < duration && IsInProgress)
        {
            elapsed += Time.deltaTime;
            if (interactProgressBar != null)
                interactProgressBar.fillAmount = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        if (interactProgressBar != null)
            interactProgressBar.fillAmount = 1f;

        progressCoroutine = null;
    }

    private void SetPromptText(string displayText, bool canInteract)
    {
        string body = string.IsNullOrEmpty(displayText) ? defaultText : displayText;
        Color color = canInteract ? availableColor : blockedColor;
        string hint = string.IsNullOrEmpty(interactKeyLabel) ? string.Empty : $"[{interactKeyLabel}]";

        if (keyHintText != null)
        {
            keyHintText.text = hint;
            keyHintText.color = color;
        }
        else if (!string.IsNullOrEmpty(hint) && !string.IsNullOrEmpty(body))
        {
            body = $"{hint}\n{body}";
        }

        if (interactText == null)
            return;

        interactText.text = body;
        interactText.color = color;
    }

    private void ResetUI()
    {
        SetPromptText(null, true);

        if (interactProgressBar != null)
            interactProgressBar.fillAmount = 0f;
    }

    private void SetVisible(bool visible)
    {
        if (interactText != null)
            interactText.gameObject.SetActive(visible);

        if (keyHintText != null)
            keyHintText.gameObject.SetActive(visible);

        if (interactProgressBar != null)
            interactProgressBar.gameObject.SetActive(visible);

        if (interactProgressBarBackground != null)
            interactProgressBarBackground.gameObject.SetActive(visible);
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactText != null)
            interactText.gameObject.SetActive(visible);

        if (keyHintText != null)
            keyHintText.gameObject.SetActive(visible);

        if (interactProgressBar != null)
            interactProgressBar.gameObject.SetActive(false);

        if (interactProgressBarBackground != null)
            interactProgressBarBackground.gameObject.SetActive(false);
    }
}
