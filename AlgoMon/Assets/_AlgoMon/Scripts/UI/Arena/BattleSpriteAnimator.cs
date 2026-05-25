using System.Collections;
using UnityEngine;

/// <summary>
/// Battle sprite presentation for one combatant. It can play data-driven
/// BattleAnimationProfile frame clips, while preserving the old generic
/// transform/color feedback as a fallback when no profile is assigned.
/// </summary>
[DisallowMultipleComponent]
public class BattleSpriteAnimator : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer primaryRenderer;
    [SerializeField] private SpriteRenderer[] bodyRenderers;
    [SerializeField] private SpriteRenderer shadowRenderer;

    [Header("Animation Profile")]
    [SerializeField] private BattleAnimationProfile animationProfile;

    [Header("Idle")]
    [SerializeField, Min(0f)] private float idleBobAmplitude = 0.04f;
    [SerializeField, Min(0f)] private float idleBobSpeed = 1.18f;
    [SerializeField, Min(0f)] private float idleScaleAmplitude = 0.01f;
    [SerializeField, Min(0f)] private float idleTiltDegrees = 0.55f;
    [SerializeField] private float phaseOffset;

    [Header("Feedback")]
    [SerializeField, Min(0f)] private float hitShakeAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float hitDuration = 0.22f;
    [SerializeField, Min(0f)] private float actionLungeDistance = 0.38f;
    [SerializeField, Min(0f)] private float actionDuration = 0.28f;
    [SerializeField, Min(0f)] private float statusPulseDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float contactPerspectiveScaleBlend;

    private Vector3 baseBodyLocalPosition;
    private Vector3 baseBodyLocalScale;
    private Quaternion baseBodyLocalRotation;
    private Vector3 feedbackOffset;
    private float contactScaleMultiplier = 1f;
    private float hitFlash;
    private float statusFlash;
    private Color statusColor = Color.white;
    private Color[] baseRendererColors;
    private Color baseShadowColor;
    private Sprite basePrimarySprite;
    private bool initialized;
    private bool profileClipPlaying;
    private bool fainted;
    private bool faintFallbackApplied;
    private int idleFrameIndex;
    private float idleFrameTimer;
    private Coroutine hitRoutine;
    private Coroutine actionRoutine;
    private Coroutine statusRoutine;
    private Coroutine profileClipRoutine;
    private BattleAnimationClipData heldProfileClip;
    private BattleAnimationState heldProfileState;
    private Vector3 heldProfileWorldTarget;
    private bool heldProfileUseTarget;
    private BattleSpriteAnimator heldProfileTargetAnimator;
    private int heldProfileNextFrameIndex;

    public Vector3 FeedbackWorldPosition
    {
        get
        {
            Transform target = body != null ? body : transform;
            return target.position + Vector3.up * 0.9f;
        }
    }

    public Vector3 SideFeedbackWorldPosition(float horizontalDirection, float horizontalPadding, float verticalOffset)
    {
        Initialize();
        float direction = horizontalDirection < 0f ? -1f : 1f;
        if (TryGetVisualBounds(out Bounds bounds))
        {
            float sideX = direction > 0f ? bounds.max.x : bounds.min.x;
            return new Vector3(
                sideX + direction * Mathf.Max(0f, horizontalPadding),
                bounds.center.y + verticalOffset,
                bounds.center.z);
        }

        Transform target = body != null ? body : transform;
        return target.position + new Vector3(direction * (0.9f + Mathf.Max(0f, horizontalPadding)), verticalOffset, 0f);
    }

    public Vector3 ContactWorldPosition
    {
        get
        {
            if (shadowRenderer != null)
                return shadowRenderer.transform.position;
            return transform.position;
        }
    }

    public bool HasProfileClip(BattleAnimationState state)
    {
        BattleAnimationClipData clip = animationProfile != null ? animationProfile.ClipFor(state) : null;
        return clip != null && clip.HasFrames;
    }

    public float HitPlaybackDurationSeconds
    {
        get
        {
            Initialize();
            return HitPlaybackDuration();
        }
    }

    public bool TryGetActionMarkerDelay(BattleAnimationState state, out float delaySeconds)
    {
        delaySeconds = 0f;
        BattleAnimationClipData clip = animationProfile != null ? animationProfile.ClipFor(state) : null;
        if (clip == null || !clip.HasFrames)
            return false;

        int markerIndex = clip.ActionFrameIndex >= 0 ? clip.ActionFrameIndex : 0;
        delaySeconds = markerIndex * clip.SecondsPerFrame;
        return true;
    }

    public bool TryGetClipTiming(BattleAnimationState state, out float actionDelaySeconds, out float durationSeconds)
    {
        actionDelaySeconds = 0f;
        durationSeconds = 0f;
        BattleAnimationClipData clip = animationProfile != null ? animationProfile.ClipFor(state) : null;
        if (clip == null || !clip.HasFrames)
            return false;

        int markerIndex = clip.ActionFrameIndex >= 0 ? clip.ActionFrameIndex : 0;
        actionDelaySeconds = markerIndex * clip.SecondsPerFrame;
        durationSeconds = clip.FrameCount * clip.SecondsPerFrame;
        return true;
    }

    private void Awake()
    {
        Initialize();
        BeginIdleClip();
    }

    private void OnEnable()
    {
        Initialize();
        BeginIdleClip();
    }

    private void Update()
    {
        Initialize();
        if (body == null)
            return;

        if (!fainted && !profileClipPlaying)
            AdvanceIdleClip(Time.deltaTime);

        float t = Time.time * idleBobSpeed + phaseOffset;
        float wave = Mathf.Sin(t);
        float softWave = Mathf.Sin(t + Mathf.PI * 0.5f);

        Vector3 idleOffset = Vector3.up * (wave * idleBobAmplitude);
        float scalePulse = softWave * idleScaleAmplitude;
        Vector3 idleScale = new Vector3(1f + scalePulse, 1f + scalePulse, 1f);

        body.localPosition = baseBodyLocalPosition + idleOffset + feedbackOffset;
        body.localScale = Vector3.Scale(baseBodyLocalScale * contactScaleMultiplier, idleScale);
        body.localRotation = baseBodyLocalRotation * Quaternion.Euler(0f, 0f, wave * idleTiltDegrees);

        if (shadowRenderer != null)
        {
            float shadowPulse = 1f - Mathf.Abs(wave) * 0.06f;
            Color shadowColor = baseShadowColor;
            shadowColor.a = baseShadowColor.a * shadowPulse;
            shadowRenderer.color = shadowColor;
        }

        ApplyRendererColors();
    }

    public void SetAnimationProfile(BattleAnimationProfile profile)
    {
        Initialize();
        animationProfile = profile;
        if (animationProfile != null)
            DisableLegacyLoopingEffects();
        ResetToIdle();
    }

    public void ResetToIdle()
    {
        Initialize();
        StopProfileClip();
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        fainted = false;
        faintFallbackApplied = false;
        hitFlash = 0f;
        statusFlash = 0f;
        BeginIdleClip();
    }

    public void PlayAttackToward(Vector3 worldTarget)
    {
        PlayAttackToward(worldTarget, null);
    }

    public void PlayAttackToward(Vector3 worldTarget, BattleSpriteAnimator targetAnimator)
    {
        Initialize();
        if (TryPlayProfileClip(BattleAnimationState.Attack, worldTarget, true, targetAnimator))
            return;

        PlayActionToward(worldTarget);
    }

    public void PlayActionToward(Vector3 worldTarget)
    {
        Initialize();
        if (fainted)
            return;

        Vector3 direction = worldTarget - ContactWorldPosition;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        if (actionRoutine != null)
            StopCoroutine(actionRoutine);
        actionRoutine = StartCoroutine(ActionRoutine(direction.normalized));
    }

    public void PlayDefense()
    {
        Initialize();
        if (TryPlayProfileClip(BattleAnimationState.Defense, FeedbackWorldPosition, false))
            return;

        PlayStatus(new Color(0.72f, 0.94f, 1f, 1f));
    }

    public void PlayStatusAction(Color color)
    {
        Initialize();
        statusColor = color;
        if (TryPlayProfileClip(BattleAnimationState.Status, FeedbackWorldPosition, false))
            return;

        PlayStatus(color);
    }

    public void PlayHit()
    {
        Initialize();
        if (fainted)
            return;

        TryPlayProfileClip(BattleAnimationState.Hit, FeedbackWorldPosition, false);

        if (hitRoutine != null)
            StopCoroutine(hitRoutine);
        hitRoutine = StartCoroutine(HitRoutine(hitDuration));
    }

    public void PlayStatus(Color color)
    {
        Initialize();
        if (fainted)
            return;

        statusColor = color;
        if (statusRoutine != null)
            StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(StatusRoutine());
    }

    public void PlayFaint()
    {
        Initialize();
        StopProfileClip();
        if (actionRoutine != null)
            StopCoroutine(actionRoutine);
        if (hitRoutine != null)
            StopCoroutine(hitRoutine);
        if (statusRoutine != null)
            StopCoroutine(statusRoutine);

        fainted = true;
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        hitFlash = 0f;
        statusFlash = 0f;

        if (!TryPlayProfileClip(BattleAnimationState.Faint, FeedbackWorldPosition, false))
            ApplyFaintFallback();
    }

    public bool PlayStateToActionMarkerAndHold(
        BattleAnimationState state,
        Vector3 worldTarget,
        bool useTarget,
        BattleSpriteAnimator targetAnimator = null)
    {
        Initialize();
        if (fainted && state != BattleAnimationState.Faint)
            return true;

        BattleAnimationClipData clip = animationProfile != null ? animationProfile.ClipFor(state) : null;
        if (clip == null || !clip.HasFrames || primaryRenderer == null)
            return false;

        StopProfileClip();
        if (actionRoutine != null)
            StopCoroutine(actionRoutine);

        profileClipRoutine = StartCoroutine(ProfileClipRoutine(clip, state, worldTarget, useTarget, targetAnimator, 0, true));
        return true;
    }

    public bool ContinueHeldProfileClip()
    {
        Initialize();
        if (heldProfileClip == null)
            return false;

        BattleAnimationClipData clip = heldProfileClip;
        BattleAnimationState state = heldProfileState;
        Vector3 worldTarget = heldProfileWorldTarget;
        bool useTarget = heldProfileUseTarget;
        BattleSpriteAnimator targetAnimator = heldProfileTargetAnimator;
        int startFrame = heldProfileNextFrameIndex;
        ClearHeldProfileClip();

        profileClipRoutine = StartCoroutine(ProfileClipRoutine(clip, state, worldTarget, useTarget, targetAnimator, startFrame, false));
        return true;
    }

    private void Initialize()
    {
        if (initialized)
            return;

        if (body == null && transform.childCount > 0)
            body = transform.GetChild(0);

        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = FindBodyRenderers();

        if (primaryRenderer == null && bodyRenderers != null && bodyRenderers.Length > 0)
            primaryRenderer = bodyRenderers[0];

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

        if (primaryRenderer != null)
            basePrimarySprite = primaryRenderer.sprite;

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

    private void DisableLegacyLoopingEffects()
    {
        BattleLoopingSpriteEffect[] legacyEffects = GetComponentsInChildren<BattleLoopingSpriteEffect>(true);
        for (int i = 0; i < legacyEffects.Length; i++)
        {
            BattleLoopingSpriteEffect legacyEffect = legacyEffects[i];
            if (legacyEffect == null)
                continue;

            legacyEffect.enabled = false;
            if (body != null && legacyEffect.transform != body)
            {
                SpriteRenderer effectRenderer = legacyEffect.GetComponent<SpriteRenderer>();
                if (effectRenderer != null)
                    effectRenderer.enabled = false;
            }
        }
    }

    private bool TryPlayProfileClip(
        BattleAnimationState state,
        Vector3 worldTarget,
        bool useTarget,
        BattleSpriteAnimator targetAnimator = null)
    {
        if (fainted && state != BattleAnimationState.Faint)
            return true;

        BattleAnimationClipData clip = animationProfile != null ? animationProfile.ClipFor(state) : null;
        if (clip == null || !clip.HasFrames || primaryRenderer == null)
            return false;

        StopProfileClip();
        if (actionRoutine != null)
            StopCoroutine(actionRoutine);

        profileClipRoutine = StartCoroutine(ProfileClipRoutine(clip, state, worldTarget, useTarget, targetAnimator, 0, false));
        return true;
    }

    private IEnumerator ProfileClipRoutine(
        BattleAnimationClipData clip,
        BattleAnimationState state,
        Vector3 worldTarget,
        bool useTarget,
        BattleSpriteAnimator targetAnimator,
        int startFrame,
        bool holdAtActionMarker)
    {
        profileClipPlaying = true;
        float secondsPerFrame = clip.SecondsPerFrame;
        int firstFrame = Mathf.Clamp(startFrame, 0, Mathf.Max(0, clip.FrameCount - 1));
        int holdFrame = clip.ActionFrameIndex >= 0 ? clip.ActionFrameIndex : firstFrame;

        for (int i = firstFrame; i < clip.FrameCount; i++)
        {
            SetPrimarySprite(clip.frames[i]);
            ApplyClipMarkers(clip, i, worldTarget, useTarget, targetAnimator);
            if (holdAtActionMarker && i >= holdFrame)
            {
                HoldProfileClip(clip, state, worldTarget, useTarget, targetAnimator, i + 1);
                profileClipRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < secondsPerFrame)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        profileClipPlaying = false;
        profileClipRoutine = null;

        if (state == BattleAnimationState.Faint || clip.holdLastFrame)
        {
            fainted = state == BattleAnimationState.Faint || fainted;
            yield break;
        }

        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        ClearHeldProfileClip();
        BeginIdleClip();
    }

    private void ApplyClipMarkers(
        BattleAnimationClipData clip,
        int frameIndex,
        Vector3 worldTarget,
        bool useTarget,
        BattleSpriteAnimator targetAnimator)
    {
        if (useTarget && frameIndex == clip.ContactFrameIndex)
        {
            feedbackOffset = ContactOffsetFor(clip, worldTarget);
            contactScaleMultiplier = ContactScaleMultiplierFor(targetAnimator);
        }

        if (frameIndex == clip.ReturnFrameIndex)
        {
            feedbackOffset = Vector3.zero;
            contactScaleMultiplier = 1f;
        }
    }

    private float ContactScaleMultiplierFor(BattleSpriteAnimator targetAnimator)
    {
        if (targetAnimator == null || contactPerspectiveScaleBlend <= 0f)
            return 1f;

        float ownScale = Mathf.Max(0.001f, VisualImageScale);
        float targetScale = Mathf.Max(0.001f, targetAnimator.VisualImageScale);
        float matchedScale = targetScale / ownScale;
        return Mathf.Lerp(1f, matchedScale, contactPerspectiveScaleBlend);
    }

    private float VisualImageScale
    {
        get
        {
            if (TryGetVisualBounds(out Bounds bounds))
                return Mathf.Max(0.001f, Mathf.Max(bounds.size.x, bounds.size.y));

            Transform scaleSource = body != null ? body : transform;
            Vector3 scale = scaleSource.lossyScale;
            return Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        }
    }

    private bool TryGetVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        if (bodyRenderers == null)
            return false;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds && bounds.size.sqrMagnitude > 0.0001f;
    }

    private void HoldProfileClip(
        BattleAnimationClipData clip,
        BattleAnimationState state,
        Vector3 worldTarget,
        bool useTarget,
        BattleSpriteAnimator targetAnimator,
        int nextFrameIndex)
    {
        heldProfileClip = clip;
        heldProfileState = state;
        heldProfileWorldTarget = worldTarget;
        heldProfileUseTarget = useTarget;
        heldProfileTargetAnimator = targetAnimator;
        heldProfileNextFrameIndex = Mathf.Clamp(nextFrameIndex, 0, clip.FrameCount);
    }

    private void ClearHeldProfileClip()
    {
        heldProfileClip = null;
        heldProfileTargetAnimator = null;
        heldProfileNextFrameIndex = 0;
    }

    private Vector3 ContactOffsetFor(BattleAnimationClipData clip, Vector3 worldTarget)
    {
        Vector3 delta = worldTarget - ContactWorldPosition;
        delta.z = 0f;
        if (delta.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 direction = delta.normalized;
        float stopDistance = Mathf.Max(0f, clip.contactDistanceFromTarget);
        float travelDistance = Mathf.Max(0f, delta.magnitude - stopDistance);
        float signedX = clip.contactOffset.x * Mathf.Sign(direction.x == 0f ? 1f : direction.x);
        Vector3 authoredOffset = new Vector3(signedX, clip.contactOffset.y, 0f);
        return direction * travelDistance + authoredOffset;
    }

    private void BeginIdleClip()
    {
        idleFrameIndex = 0;
        idleFrameTimer = 0f;

        BattleAnimationClipData idle = animationProfile != null ? animationProfile.idle : null;
        if (idle != null && idle.HasFrames)
            SetPrimarySprite(idle.frames[0]);
        else if (primaryRenderer != null && basePrimarySprite != null)
            primaryRenderer.sprite = basePrimarySprite;
    }

    private void AdvanceIdleClip(float deltaTime)
    {
        BattleAnimationClipData idle = animationProfile != null ? animationProfile.idle : null;
        if (idle == null || !idle.HasFrames || primaryRenderer == null)
            return;

        idleFrameTimer += deltaTime;
        float secondsPerFrame = idle.SecondsPerFrame;
        while (idleFrameTimer >= secondsPerFrame)
        {
            idleFrameTimer -= secondsPerFrame;
            idleFrameIndex = (idleFrameIndex + 1) % idle.FrameCount;
        }

        SetPrimarySprite(idle.frames[idleFrameIndex]);
    }

    private void SetPrimarySprite(Sprite sprite)
    {
        if (primaryRenderer != null && sprite != null)
            primaryRenderer.sprite = sprite;
    }

    private void StopProfileClip()
    {
        if (profileClipRoutine != null)
            StopCoroutine(profileClipRoutine);

        profileClipRoutine = null;
        profileClipPlaying = false;
        contactScaleMultiplier = 1f;
        ClearHeldProfileClip();
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
        contactScaleMultiplier = 1f;
        actionRoutine = null;
    }

    private float HitPlaybackDuration()
    {
        BattleAnimationClipData clip = animationProfile != null ? animationProfile.hit : null;
        if (clip != null && clip.HasFrames)
            return Mathf.Max(hitDuration, clip.FrameCount * clip.SecondsPerFrame);

        return hitDuration;
    }

    private IEnumerator HitRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float fade = 1f - p;
            float shake = Mathf.Sin(p * Mathf.PI * 8f) * hitShakeAmplitude * fade;
            feedbackOffset = new Vector3(shake, 0f, 0f);
            hitFlash = fade;
            yield return null;
        }

        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
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
            color = Color.Lerp(color, Color.white, hitFlash * 0.75f);
            if (faintFallbackApplied)
                color.a *= 0.45f;
            renderer.color = color;
        }
    }

    private void ApplyFaintFallback()
    {
        faintFallbackApplied = true;
    }

}
