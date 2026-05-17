using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight battle sprite motion: idle bob / breathing plus generic hit,
/// action, and status feedback. It works with placeholder sprites and later
/// replacement art because it only animates transforms and renderer colors.
/// </summary>
[DisallowMultipleComponent]
public class BattleSpriteAnimator : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer[] bodyRenderers;
    [SerializeField] private SpriteRenderer shadowRenderer;

    [Header("Idle")]
    [SerializeField, Min(0f)] private float idleBobAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float idleBobSpeed = 1.7f;
    [SerializeField, Min(0f)] private float idleScaleAmplitude = 0.035f;
    [SerializeField, Min(0f)] private float idleTiltDegrees = 1.5f;
    [SerializeField] private float phaseOffset;

    [Header("Feedback")]
    [SerializeField, Min(0f)] private float hitShakeAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float hitDuration = 0.22f;
    [SerializeField, Min(0f)] private float actionLungeDistance = 0.38f;
    [SerializeField, Min(0f)] private float actionDuration = 0.28f;
    [SerializeField, Min(0f)] private float statusPulseDuration = 0.35f;

    [Header("Counter Clash")]
    [SerializeField, Min(0f)] private float counterInterruptedDistance = 0.72f;
    [SerializeField, Min(0f)] private float counterInterruptedStartup = 0.24f;
    [SerializeField, Min(0f)] private float counterFreezeDuration = 0.48f;
    [SerializeField, Min(0f)] private float counterRecoverDuration = 0.2f;
    [SerializeField] private Color counterFreezeColor = new Color(0.72f, 0.94f, 1f, 1f);

    private Vector3 baseBodyLocalPosition;
    private Vector3 baseBodyLocalScale;
    private Quaternion baseBodyLocalRotation;
    private Vector3 feedbackOffset;
    private float hitFlash;
    private float statusFlash;
    private float counterFreezeFlash;
    private Color statusColor = Color.white;
    private Color[] baseRendererColors;
    private Color baseShadowColor;
    private bool initialized;
    private Coroutine hitRoutine;
    private Coroutine actionRoutine;
    private Coroutine statusRoutine;

    public Vector3 FeedbackWorldPosition
    {
        get
        {
            Transform target = body != null ? body : transform;
            return target.position + Vector3.up * 0.9f;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Update()
    {
        Initialize();
        if (body == null)
            return;

        float t = Time.time * idleBobSpeed + phaseOffset;
        float wave = Mathf.Sin(t);
        float softWave = Mathf.Sin(t + Mathf.PI * 0.5f);

        Vector3 idleOffset = Vector3.up * (wave * idleBobAmplitude);
        Vector3 idleScale = new Vector3(
            1f + softWave * idleScaleAmplitude,
            1f - softWave * idleScaleAmplitude * 0.55f,
            1f);

        body.localPosition = baseBodyLocalPosition + idleOffset + feedbackOffset;
        body.localScale = Vector3.Scale(baseBodyLocalScale, idleScale);
        body.localRotation = baseBodyLocalRotation * Quaternion.Euler(0f, 0f, wave * idleTiltDegrees);

        if (shadowRenderer != null)
        {
            float shadowPulse = 1f - Mathf.Abs(wave) * 0.12f;
            Color shadowColor = baseShadowColor;
            shadowColor.a = baseShadowColor.a * shadowPulse;
            shadowRenderer.color = shadowColor;
        }

        ApplyRendererColors();
    }

    public void PlayActionToward(Vector3 worldTarget)
    {
        Initialize();
        Vector3 direction = worldTarget - transform.position;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        if (actionRoutine != null)
            StopCoroutine(actionRoutine);
        actionRoutine = StartCoroutine(ActionRoutine(direction.normalized));
    }

    public void PlayHit()
    {
        Initialize();
        if (hitRoutine != null)
            StopCoroutine(hitRoutine);
        hitRoutine = StartCoroutine(HitRoutine());
    }

    public void PlayStatus(Color color)
    {
        Initialize();
        statusColor = color;
        if (statusRoutine != null)
            StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(StatusRoutine());
    }

    public void PlayCounterInterruptedToward(Vector3 worldTarget)
    {
        PlayCounterInterruptedToward(worldTarget, counterFreezeDuration);
    }

    public void PlayCounterInterruptedToward(Vector3 worldTarget, float holdDuration)
    {
        Initialize();
        Vector3 direction = worldTarget - transform.position;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        if (actionRoutine != null)
            StopCoroutine(actionRoutine);
        actionRoutine = StartCoroutine(CounterInterruptedRoutine(direction.normalized, holdDuration));
    }

    private void Initialize()
    {
        if (initialized)
            return;

        if (body == null && transform.childCount > 0)
            body = transform.GetChild(0);

        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = FindBodyRenderers();

        if (body != null)
        {
            baseBodyLocalPosition = body.localPosition;
            baseBodyLocalScale = body.localScale;
            baseBodyLocalRotation = body.localRotation;
        }

        if (bodyRenderers != null)
        {
            baseRendererColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
                baseRendererColors[i] = bodyRenderers[i] != null ? bodyRenderers[i].color : Color.white;
        }

        if (shadowRenderer != null)
            baseShadowColor = shadowRenderer.color;

        initialized = true;
    }

    private SpriteRenderer[] FindBodyRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (shadowRenderer == null)
            return renderers;

        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] != shadowRenderer)
                count++;
        }

        var filtered = new SpriteRenderer[count];
        int index = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] != shadowRenderer)
                filtered[index++] = renderers[i];
        }
        return filtered;
    }

    private IEnumerator ActionRoutine(Vector3 direction)
    {
        float elapsed = 0f;
        while (elapsed < actionDuration)
        {
            elapsed += Time.deltaTime;
            float p = actionDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / actionDuration);
            float punch = Mathf.Sin(p * Mathf.PI) * actionLungeDistance;
            feedbackOffset = direction * punch;
            yield return null;
        }

        feedbackOffset = Vector3.zero;
        actionRoutine = null;
    }

    private IEnumerator CounterInterruptedRoutine(Vector3 direction, float holdDuration)
    {
        float elapsed = 0f;
        while (elapsed < counterInterruptedStartup)
        {
            elapsed += Time.deltaTime;
            float p = counterInterruptedStartup <= 0f ? 1f : Mathf.Clamp01(elapsed / counterInterruptedStartup);
            feedbackOffset = direction * (EaseOutQuad(p) * counterInterruptedDistance);
            counterFreezeFlash = p * 0.5f;
            yield return null;
        }

        feedbackOffset = direction * counterInterruptedDistance;
        elapsed = 0f;
        float freezeDuration = Mathf.Max(counterFreezeDuration, holdDuration);
        while (elapsed < freezeDuration)
        {
            elapsed += Time.deltaTime;
            float p = freezeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / freezeDuration);
            counterFreezeFlash = 0.65f + Mathf.Sin(p * Mathf.PI * 8f) * 0.18f;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < counterRecoverDuration)
        {
            elapsed += Time.deltaTime;
            float p = counterRecoverDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / counterRecoverDuration);
            float ease = 1f - EaseOutQuad(p);
            feedbackOffset = direction * (counterInterruptedDistance * ease);
            counterFreezeFlash = ease * 0.45f;
            yield return null;
        }

        feedbackOffset = Vector3.zero;
        counterFreezeFlash = 0f;
        actionRoutine = null;
    }

    private IEnumerator HitRoutine()
    {
        float elapsed = 0f;
        while (elapsed < hitDuration)
        {
            elapsed += Time.deltaTime;
            float p = hitDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / hitDuration);
            float fade = 1f - p;
            float shake = Mathf.Sin(p * Mathf.PI * 8f) * hitShakeAmplitude * fade;
            feedbackOffset = new Vector3(shake, 0f, 0f);
            hitFlash = fade;
            yield return null;
        }

        feedbackOffset = Vector3.zero;
        hitFlash = 0f;
        hitRoutine = null;
    }

    private IEnumerator StatusRoutine()
    {
        float elapsed = 0f;
        while (elapsed < statusPulseDuration)
        {
            elapsed += Time.deltaTime;
            float p = statusPulseDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / statusPulseDuration);
            statusFlash = Mathf.Sin(p * Mathf.PI);
            yield return null;
        }

        statusFlash = 0f;
        statusRoutine = null;
    }

    private void ApplyRendererColors()
    {
        if (bodyRenderers == null || baseRendererColors == null)
            return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null)
                continue;

            Color color = i < baseRendererColors.Length ? baseRendererColors[i] : Color.white;
            color = Color.Lerp(color, statusColor, statusFlash * 0.45f);
            color = Color.Lerp(color, counterFreezeColor, counterFreezeFlash);
            color = Color.Lerp(color, Color.white, hitFlash * 0.75f);
            renderer.color = color;
        }
    }

    private static float EaseOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}
