using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CelestialBodyClickable : MonoBehaviour
{
    [Header("Info")]
    [SerializeField] private string displayName = "Planet";
    [SerializeField] [TextArea(2, 4)] private string factText = "A fun fact goes here.";
    [SerializeField] private Vector3 cameraWorldOffset = new Vector3(0f, 1.5f, -4f);
    [SerializeField] private AudioClip clickAudioClip;

    [Header("Visual Feedback")]
    [SerializeField] private bool pulseScale = true;
    [SerializeField] private float pulseScaleMultiplier = 1.15f;
    [SerializeField] private float pulseDuration = 0.15f;
    [SerializeField] private Renderer flashRenderer;
    [SerializeField] private Color flashEmissionColor = new Color(0.1f, 0.7f, 1f);
    [SerializeField] private float flashEmissionStrength = 2f;
    [SerializeField] private Color flashBaseColor = new Color(0.6f, 0.9f, 1f);

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

    private Vector3 originalScale;
    private Color originalEmissionColor = Color.black;
    private Color originalBaseColor = Color.white;
    private bool hasEmissionProperty;
    private bool hasBaseColorProperty;
    private Coroutine feedbackRoutine;

    public string DisplayName => displayName;
    public string FactText => factText;
    public Vector3 CameraWorldOffset => cameraWorldOffset;
    public AudioClip ClickAudioClip => clickAudioClip;

    private void Awake()
    {
        originalScale = transform.localScale;
        ApplyFriendlyDefaultsIfNeeded();

        if (flashRenderer == null)
        {
            flashRenderer = GetComponentInChildren<Renderer>();
        }

        if (flashRenderer != null && flashRenderer.sharedMaterial != null)
        {
            hasEmissionProperty = flashRenderer.sharedMaterial.HasProperty(EmissionColorId);
            hasBaseColorProperty = flashRenderer.sharedMaterial.HasProperty(ColorId);

            if (hasEmissionProperty)
            {
                originalEmissionColor = flashRenderer.sharedMaterial.GetColor(EmissionColorId);
            }

            if (hasBaseColorProperty)
            {
                originalBaseColor = flashRenderer.sharedMaterial.GetColor(ColorId);
            }
        }
    }

    private void OnMouseDown()
    {
        CameraFocusController.Instance?.FocusOn(this);
        TriggerFeedback();
    }

    private void TriggerFeedback()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(FeedbackRoutine());
    }

    private IEnumerator FeedbackRoutine()
    {
        float elapsed = 0f;
        Vector3 enlargedScale = originalScale * pulseScaleMultiplier;

        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pulseDuration);

            if (pulseScale)
            {
                transform.localScale = Vector3.Lerp(originalScale, enlargedScale, t);
            }

            SetFlashEmission(Mathf.Lerp(0f, 1f, t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pulseDuration);

            if (pulseScale)
            {
                transform.localScale = Vector3.Lerp(enlargedScale, originalScale, t);
            }

            SetFlashEmission(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        transform.localScale = originalScale;
        SetFlashEmission(0f);
        feedbackRoutine = null;
    }

    private void SetFlashEmission(float intensity)
    {
        if (flashRenderer == null)
        {
            return;
        }

        flashRenderer.GetPropertyBlock(propertyBlock);

        if (hasEmissionProperty)
        {
            Color currentEmissionColor = Color.Lerp(
                originalEmissionColor,
                flashEmissionColor * flashEmissionStrength,
                Mathf.Clamp01(intensity));
            propertyBlock.SetColor(EmissionColorId, currentEmissionColor);
        }

        if (hasBaseColorProperty)
        {
            Color currentBaseColor = Color.Lerp(
                originalBaseColor,
                flashBaseColor,
                Mathf.Clamp01(intensity));
            propertyBlock.SetColor(ColorId, currentBaseColor);
        }

        flashRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyFriendlyDefaultsIfNeeded()
    {
        string lowerName = gameObject.name.ToLowerInvariant();

        if (displayName == "Planet")
        {
            if (lowerName.Contains("earth"))
            {
                displayName = "Earth";
            }
            else if (lowerName.Contains("moon"))
            {
                displayName = "Moon";
            }
            else if (lowerName.Contains("sun"))
            {
                displayName = "Sun";
            }
        }

        if (factText != "A fun fact goes here.")
        {
            return;
        }

        if (lowerName.Contains("earth"))
        {
            factText = "Earth is our home. It spins once in about 24 hours.";
        }
        else if (lowerName.Contains("moon"))
        {
            factText = "The Moon is Earth's space buddy and travels around Earth.";
        }
        else if (lowerName.Contains("sun"))
        {
            factText = "The Sun is a giant star that gives us light and warmth.";
        }
    }
}
