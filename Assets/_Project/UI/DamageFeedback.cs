using UnityEngine;
using UnityEngine.UI;

// Local-player hit feedback: red screen flash, low-health tint and a short camera shake.
// Bound by HUD to whichever player is local; builds its own overlay if none is assigned.
[DefaultExecutionOrder(500)]
public class DamageFeedback : MonoBehaviour
{
    [SerializeField] private Image overlay;
    [SerializeField] private Color flashColor = new(0.75f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float hitFlashAlpha = 0.45f;
    [SerializeField, Min(0.01f)] private float flashFadeSpeed = 1.5f;
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.35f;
    [SerializeField, Range(0f, 1f)] private float lowHealthMaxAlpha = 0.25f;

    [Header("Camera Shake")]
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.25f;
    [SerializeField, Min(0f)] private float shakeFrequency = 30f;

    private Health health;
    private Transform shakeTarget;
    private float flashAlpha;
    private float shakeTimeLeft;
    private Vector3 appliedShakeOffset;

    private void Awake()
    {
        if (overlay == null)
            overlay = CreateOverlay();

        SetOverlayAlpha(0f);
    }

    private void OnDestroy()
    {
        Bind(null, null);
    }

    public void Bind(Health playerHealth, Transform cameraTransform)
    {
        if (health != null)
            health.OnHit -= HandleHit;

        RemoveShakeOffset();
        health = playerHealth;
        shakeTarget = cameraTransform;
        flashAlpha = 0f;
        shakeTimeLeft = 0f;

        if (health != null)
            health.OnHit += HandleHit;
    }

    private void HandleHit()
    {
        flashAlpha = hitFlashAlpha;
        shakeTimeLeft = shakeDuration;
    }

    private void LateUpdate()
    {
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, flashFadeSpeed * Time.deltaTime);
        SetOverlayAlpha(Mathf.Max(flashAlpha, GetLowHealthAlpha()));
        UpdateShake();
    }

    private float GetLowHealthAlpha()
    {
        if (health == null || health.IsDead || health.MaxHealth <= 0f)
            return 0f;

        float ratio = health.CurrentHealth / health.MaxHealth;
        if (ratio >= lowHealthThreshold)
            return 0f;

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
        return lowHealthMaxAlpha * (1f - ratio / lowHealthThreshold) * pulse;
    }

    // Additive so it never fights PlayerController (rotation only) or the death-time reparenting.
    private void UpdateShake()
    {
        RemoveShakeOffset();

        if (shakeTarget == null || shakeTimeLeft <= 0f || (health != null && health.IsDead))
        {
            shakeTimeLeft = 0f;
            return;
        }

        shakeTimeLeft -= Time.deltaTime;
        float strength = shakeAmplitude * Mathf.Clamp01(shakeTimeLeft / shakeDuration);
        float t = Time.time * shakeFrequency;
        appliedShakeOffset = new Vector3(
            (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f,
            0f) * strength;

        shakeTarget.localPosition += appliedShakeOffset;
    }

    private void RemoveShakeOffset()
    {
        if (shakeTarget != null)
            shakeTarget.localPosition -= appliedShakeOffset;

        appliedShakeOffset = Vector3.zero;
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlay == null)
            return;

        Color color = flashColor;
        color.a = alpha;
        overlay.color = color;
        overlay.enabled = alpha > 0.001f;
    }

    private Image CreateOverlay()
    {
        var overlayObject = new GameObject("DamageOverlay", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)overlayObject.transform;
        rect.SetParent(transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = overlayObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
