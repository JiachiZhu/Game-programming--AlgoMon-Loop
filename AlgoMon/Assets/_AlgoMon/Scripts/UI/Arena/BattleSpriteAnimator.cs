/*
Script Audit:
- Purpose: Animates one battle combatant sprite using data-driven clips or fallback motion.
- Attached GameObject: Player or enemy battle sprite object in TheArena.
- Main responsibilities: Play entry, idle, attack, defense, status, hit, and faint animations; move toward targets; flash colors; hold/continue profile clips; and calculate feedback positions.
- Important variables: body, primaryRenderer, bodyRenderers, shadowRenderer, animationProfile, idle settings, feedback settings, profileClipRoutine, heldProfileClip.
- Inputs: BattleAnimationProfile data and commands from BattlePresentationController.
- Outputs or effects: Changes sprite frames, transform movement, scale, rotation, and renderer colors.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Trigger every battle animation state and confirm fallback animation still works when no profile is assigned.
*/
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

    [Header("Entry")]
    [SerializeField] private bool playEntryOnProfileSet = true;

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
    [SerializeField, Min(0f)] private float switchRevealDefaultDuration = 0.48f;
    [SerializeField, Min(0f)] private float switchRevealScalePop = 0.18f;
    [SerializeField, Range(0f, 1f)] private float contactPerspectiveScaleBlend;
    [SerializeField, Min(0)] private int contactSortingOrderBoost = 4;

    [Header("Profile Scaling")]
    [SerializeField] private bool normalizeProfileVisualHeight = true;
    [SerializeField, Min(0.1f)] private float targetProfileVisualHeight = 2.25f;
    [SerializeField, Min(0.05f)] private float minimumProfileScaleMultiplier = 0.45f;
    [SerializeField, Min(0.05f)] private float maximumProfileScaleMultiplier = 2.80f;

    [Header("Profile Movement")]
    [SerializeField] private bool faceReturnDirectionForSmoothMovement = true;

    private Vector3 baseBodyLocalPosition;
    private Vector3 baseBodyLocalScale;
    private Vector3 authoredBodyLocalScale;
    private Quaternion baseBodyLocalRotation;
    private Vector3 feedbackOffset;
    private float contactScaleMultiplier = 1f;
    private float battleSideFacingSign = 1f;
    private float motionFacingSign = 1f;
    private float hitFlash;
    private float statusFlash;
    private float switchRevealAlpha = 1f;
    private float switchRevealFlash;
    private float switchRevealScaleOffset;
    private Color statusColor = Color.white;
    private Color[] baseRendererColors;
    private int[] baseRendererSortingOrders;
    private Color baseShadowColor;
    private Sprite basePrimarySprite;
    private bool initialized;
    private bool hasCapturedBodyPose;
    private bool useBattleSideFacing;
    private bool profileClipPlaying;
    private bool contactSortingActive;
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

    public Vector3 VisualCenterWorldPosition
    {
        get
        {
            Initialize();
            if (TryGetVisualBounds(out Bounds bounds))
                return bounds.center;

            Transform target = body != null ? body : transform;
            return target.position + Vector3.up * 0.45f;
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

    public int MaxBodySortingOrder
    {
        get
        {
            Initialize();
            int maxOrder = int.MinValue;
            if (bodyRenderers != null)
            {
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        maxOrder = Mathf.Max(maxOrder, bodyRenderers[i].sortingOrder);
                }
            }

            return maxOrder == int.MinValue ? 0 : maxOrder;
        }
    }

    public float HitPlaybackDurationSeconds
    {
        get
        {
            Initialize();
            return HitPlaybackDuration();
        }
    }

    // Full faint/defeat clip length, so the battle can hold the result panel until
    // the KO animation has actually played out. Profile-less faints use the dim
    // fallback (no animated frames), so they report 0.
    public float FaintPlaybackDurationSeconds
    {
        get
        {
            Initialize();
            return TryGetClipTiming(BattleAnimationState.Faint, out _, out float duration) ? duration : 0f;
        }
    }

    public BattleAnimationProfile AnimationProfile => animationProfile;

    public void ConfigureSpriteBindings(
        Transform bodyTransform,
        SpriteRenderer primarySpriteRenderer,
        SpriteRenderer[] renderers,
        SpriteRenderer shadowSpriteRenderer)
    {
        RestoreRendererBaseColors();

        if (body != bodyTransform)
            hasCapturedBodyPose = false;

        body = bodyTransform;
        primaryRenderer = primarySpriteRenderer;
        bodyRenderers = renderers;
        shadowRenderer = shadowSpriteRenderer;

        initialized = false;
        Initialize();
        BeginIdleClip();
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
        Vector3 revealScale = new Vector3(1f + switchRevealScaleOffset, 1f + switchRevealScaleOffset, 1f);

        body.localPosition = baseBodyLocalPosition + idleOffset + feedbackOffset;
        Vector3 facingScale = baseBodyLocalScale;
        facingScale.x *= motionFacingSign;
        body.localScale = Vector3.Scale(facingScale * contactScaleMultiplier, Vector3.Scale(idleScale, revealScale));
        body.localRotation = baseBodyLocalRotation * Quaternion.Euler(0f, 0f, wave * idleTiltDegrees);

        if (shadowRenderer != null)
        {
            float shadowPulse = 1f - Mathf.Abs(wave) * 0.06f;
            Color shadowColor = baseShadowColor;
            shadowColor.a = baseShadowColor.a * shadowPulse * switchRevealAlpha;
            shadowRenderer.color = shadowColor;
        }

        ApplyRendererColors();
    }

    public void SetBattleSideFacing(bool playerSide)
    {
        Initialize();
        useBattleSideFacing = true;
        battleSideFacingSign = playerSide ? -1f : 1f;
        motionFacingSign = 1f;
        ApplyProfileFacing();
        ApplyBaseScaleImmediately();
    }

    public void SetAnimationProfile(BattleAnimationProfile profile)
    {
        Initialize();
        animationProfile = profile;
        ApplyProfileFacing();
        if (animationProfile != null)
            DisableLegacyLoopingEffects();
        ResetToIdle();
        if (playEntryOnProfileSet)
            PlayEntry();
    }

    public void ResetToIdle()
    {
        Initialize();
        StopProfileClip();
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        motionFacingSign = 1f;
        ResetSwitchRevealVisuals();
        RestoreSortingOrders();
        fainted = false;
        faintFallbackApplied = false;
        hitFlash = 0f;
        statusFlash = 0f;
        BeginIdleClip();
    }

    public IEnumerator PlaySwitchReveal(float duration = -1f)
    {
        Initialize();
        if (body == null)
            yield break;

        StopProfileClip();
        if (actionRoutine != null)
            StopCoroutine(actionRoutine);
        if (hitRoutine != null)
            StopCoroutine(hitRoutine);
        if (statusRoutine != null)
            StopCoroutine(statusRoutine);

        actionRoutine = null;
        hitRoutine = null;
        statusRoutine = null;
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        motionFacingSign = 1f;
        hitFlash = 0f;
        statusFlash = 0f;
        fainted = false;
        faintFallbackApplied = false;
        RestoreSortingOrders();
        BeginIdleClip();

        float revealDuration = duration >= 0f ? duration : switchRevealDefaultDuration;
        if (revealDuration <= 0f)
        {
            ResetSwitchRevealVisuals();
            yield break;
        }

        switchRevealAlpha = 0.28f;
        switchRevealFlash = 1f;
        switchRevealScaleOffset = Mathf.Max(0f, switchRevealScalePop);
        ApplyRendererColors();
        ApplySwitchRevealShadowColor();

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / revealDuration);
            float eased = SmoothStep01(p);
            float flashFade = 1f - SmoothStep01(Mathf.InverseLerp(0.18f, 0.74f, p));
            float pulse = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 9f)) * (1f - eased) * 0.28f;

            switchRevealAlpha = Mathf.Lerp(0.28f, 1f, eased);
            switchRevealFlash = Mathf.Clamp01(flashFade + pulse);
            switchRevealScaleOffset = Mathf.Lerp(Mathf.Max(0f, switchRevealScalePop), 0f, eased);
            yield return null;
        }

        ResetSwitchRevealVisuals();
        BeginIdleClip();
        ApplyRendererColors();
        ApplySwitchRevealShadowColor();
    }

    public bool PlayEntry()
    {
        Initialize();
        if (fainted)
            return false;

        return TryPlayProfileClip(BattleAnimationState.Entry, FeedbackWorldPosition, false);
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

    // Hard-stops whatever action clip is playing and drops back to idle. Used to
    // truncate a countered action (e.g. cut the rest of a status windup the moment
    // an Attack counter connects) regardless of whether a Hit clip exists.
    public void CancelActionToIdle()
    {
        Initialize();
        if (fainted)
            return;

        StopProfileClip();
        if (actionRoutine != null)
            StopCoroutine(actionRoutine);
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        motionFacingSign = 1f;
        RestoreSortingOrders();
        BeginIdleClip();
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
        motionFacingSign = 1f;
        RestoreSortingOrders();
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

        profileClipRoutine = StartCoroutine(ProfileClipRoutine(clip, state, worldTarget, useTarget, targetAnimator, ClipStartFrameIndex(clip), true));
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
            if (!hasCapturedBodyPose)
            {
                baseBodyLocalPosition = body.localPosition;
                authoredBodyLocalScale = body.localScale;
                baseBodyLocalRotation = body.localRotation;
                hasCapturedBodyPose = true;
            }
        }

        if (bodyRenderers != null)
        {
            baseRendererColors = new Color[bodyRenderers.Length];
            baseRendererSortingOrders = new int[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                baseRendererColors[i] = bodyRenderers[i] != null ? bodyRenderers[i].color : Color.white;
                baseRendererSortingOrders[i] = bodyRenderers[i] != null ? bodyRenderers[i].sortingOrder : 0;
            }
        }

        if (primaryRenderer != null)
            basePrimarySprite = primaryRenderer.sprite;

        if (shadowRenderer != null)
            baseShadowColor = shadowRenderer.color;

        initialized = true;
        ApplyProfileFacing();
    }

    private void ApplyProfileFacing()
    {
        if (body == null)
            return;

        Vector3 scale = authoredBodyLocalScale;
        scale.x = Mathf.Abs(scale.x);
        scale *= ProfileVisualScaleMultiplier();
        scale *= ProfileAuthoredScaleMultiplier();
        scale.x *= FacingSign();
        baseBodyLocalScale = scale;
    }

    private float FacingSign()
    {
        float sign = useBattleSideFacing ? battleSideFacingSign : AuthoredFacingSign();
        if (animationProfile != null && animationProfile.mirrorX)
            sign *= -1f;
        return sign;
    }

    private float AuthoredFacingSign()
    {
        return authoredBodyLocalScale.x < 0f ? -1f : 1f;
    }

    private void ApplyBaseScaleImmediately()
    {
        if (body == null)
            return;

        Vector3 facingScale = baseBodyLocalScale;
        facingScale.x *= motionFacingSign;
        Vector3 revealScale = new Vector3(1f + switchRevealScaleOffset, 1f + switchRevealScaleOffset, 1f);
        body.localScale = Vector3.Scale(facingScale * contactScaleMultiplier, revealScale);
    }

    private float ProfileAuthoredScaleMultiplier()
    {
        if (animationProfile == null)
            return 1f;

        return Mathf.Max(0.1f, animationProfile.visualScaleMultiplier);
    }

    private float ProfileVisualScaleMultiplier()
    {
        if (!normalizeProfileVisualHeight || animationProfile == null || targetProfileVisualHeight <= 0f)
            return 1f;

        float spriteHeight = EstimateProfileSpriteHeight(animationProfile);
        float authoredHeight = spriteHeight * Mathf.Max(0.001f, Mathf.Abs(authoredBodyLocalScale.y));
        if (authoredHeight <= 0.001f)
            return 1f;

        float multiplier = targetProfileVisualHeight / authoredHeight;
        float min = Mathf.Min(minimumProfileScaleMultiplier, maximumProfileScaleMultiplier);
        float max = Mathf.Max(minimumProfileScaleMultiplier, maximumProfileScaleMultiplier);
        return Mathf.Clamp(multiplier, min, max);
    }

    private static float EstimateProfileSpriteHeight(BattleAnimationProfile profile)
    {
        if (profile == null)
            return 0f;

        float idleHeight = MaxFrameSpriteHeight(profile.idle);
        if (idleHeight > 0f)
            return idleHeight;

        return Mathf.Max(
            MaxFrameSpriteHeight(profile.entry),
            MaxFrameSpriteHeight(profile.attack),
            MaxFrameSpriteHeight(profile.defense),
            MaxFrameSpriteHeight(profile.status),
            MaxFrameSpriteHeight(profile.hit),
            MaxFrameSpriteHeight(profile.faint));
    }

    private static float MaxFrameSpriteHeight(params BattleAnimationClipData[] clips)
    {
        float maxHeight = 0f;
        if (clips == null)
            return maxHeight;

        for (int c = 0; c < clips.Length; c++)
        {
            BattleAnimationClipData clip = clips[c];
            if (clip == null || clip.frames == null)
                continue;

            for (int i = 0; i < clip.frames.Length; i++)
            {
                maxHeight = Mathf.Max(maxHeight, SpriteVisualHeight(clip.frames[i]));
            }
        }

        return maxHeight;
    }

    private static float SpriteVisualHeight(Sprite sprite)
    {
        if (sprite == null)
            return 0f;

        Vector2[] vertices = sprite.vertices;
        if (vertices != null && vertices.Length > 0)
        {
            float minY = vertices[0].y;
            float maxY = vertices[0].y;
            for (int i = 1; i < vertices.Length; i++)
            {
                minY = Mathf.Min(minY, vertices[i].y);
                maxY = Mathf.Max(maxY, vertices[i].y);
            }

            float vertexHeight = maxY - minY;
            if (vertexHeight > 0.001f)
                return vertexHeight;
        }

        return sprite.bounds.size.y;
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

        profileClipRoutine = StartCoroutine(ProfileClipRoutine(clip, state, worldTarget, useTarget, targetAnimator, ClipStartFrameIndex(clip), false));
        return true;
    }

    private static int ClipStartFrameIndex(BattleAnimationClipData clip)
    {
        if (clip == null || clip.FrameCount <= 0)
            return 0;

        int startFrame = clip.StartFrameIndex;
        return startFrame >= 0 ? startFrame : 0;
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
        motionFacingSign = 1f;
        float secondsPerFrame = clip.SecondsPerFrame;
        int firstFrame = Mathf.Clamp(startFrame, 0, Mathf.Max(0, clip.FrameCount - 1));
        int holdFrame = clip.ActionFrameIndex >= 0 ? clip.ActionFrameIndex : firstFrame;

        for (int i = firstFrame; i < clip.FrameCount; i++)
        {
            SetPrimarySprite(clip.frames[i]);
            ApplyClipMotion(clip, i, 0f, worldTarget, useTarget, targetAnimator);
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
                float frameProgress = secondsPerFrame <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / secondsPerFrame);
                ApplyClipMotion(clip, i, frameProgress, worldTarget, useTarget, targetAnimator);
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
        motionFacingSign = 1f;
        RestoreSortingOrders();
        ClearHeldProfileClip();
        BeginIdleClip();
    }

    public bool PlayActionMarkerWindowLoop(BattleAnimationState state, float durationSeconds)
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

        profileClipRoutine = StartCoroutine(ActionMarkerWindowLoopRoutine(clip, Mathf.Max(0f, durationSeconds)));
        return true;
    }

    private IEnumerator ActionMarkerWindowLoopRoutine(BattleAnimationClipData clip, float durationSeconds)
    {
        profileClipPlaying = true;
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        motionFacingSign = 1f;

        int center = clip.ActionFrameIndex >= 0 ? clip.ActionFrameIndex : 0;
        int previous = Mathf.Clamp(center - 1, 0, clip.FrameCount - 1);
        int current = Mathf.Clamp(center, 0, clip.FrameCount - 1);
        int next = Mathf.Clamp(center + 1, 0, clip.FrameCount - 1);
        int[] loopFrames = { previous, current, next };

        float secondsPerFrame = clip.SecondsPerFrame;
        float elapsed = 0f;
        int frame = 0;
        float minimumDuration = Mathf.Max(secondsPerFrame, secondsPerFrame * loopFrames.Length);
        float targetDuration = durationSeconds > 0f ? durationSeconds : minimumDuration;

        while (elapsed < targetDuration)
        {
            SetPrimarySprite(clip.frames[loopFrames[frame % loopFrames.Length]]);
            frame++;

            float frameElapsed = 0f;
            while (frameElapsed < secondsPerFrame && elapsed < targetDuration)
            {
                float deltaTime = Time.deltaTime;
                frameElapsed += deltaTime;
                elapsed += deltaTime;
                yield return null;
            }
        }

        profileClipPlaying = false;
        profileClipRoutine = null;
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        motionFacingSign = 1f;
        RestoreSortingOrders();
        BeginIdleClip();
    }

    private void ApplyClipMotion(
        BattleAnimationClipData clip,
        int frameIndex,
        float frameProgress,
        Vector3 worldTarget,
        bool useTarget,
        BattleSpriteAnimator targetAnimator)
    {
        bool sortAboveTarget = ShouldSortAboveTarget(clip, frameIndex, useTarget, targetAnimator);
        ApplyContactSorting(targetAnimator, sortAboveTarget);

        if (useTarget && clip.ContactFrameIndex >= 0)
        {
            Vector3 contactOffset = ContactOffsetFor(clip, worldTarget, targetAnimator);
            float contactScale = ContactScaleMultiplierFor(targetAnimator);
            if (clip.smoothContactMovement && frameIndex <= clip.ContactFrameIndex)
            {
                float contactT = Mathf.Clamp01((frameIndex + Mathf.Clamp01(frameProgress)) / (clip.ContactFrameIndex + 1f));
                contactT = SmoothStep01(contactT);
                feedbackOffset = Vector3.Lerp(Vector3.zero, contactOffset, contactT);
                contactScaleMultiplier = Mathf.Lerp(1f, contactScale, contactT);
                motionFacingSign = 1f;
            }
            else if (frameIndex >= clip.ContactFrameIndex)
            {
                feedbackOffset = contactOffset;
                contactScaleMultiplier = contactScale;
                motionFacingSign = 1f;
            }
        }

        if (clip.ReturnFrameIndex >= 0 && frameIndex >= clip.ReturnFrameIndex)
        {
            if (clip.smoothReturnMovement)
            {
                Vector3 contactOffset = ContactOffsetFor(clip, worldTarget, targetAnimator);
                int returnSpan = Mathf.Max(1, clip.FrameCount - clip.ReturnFrameIndex);
                float returnT = Mathf.Clamp01((frameIndex - clip.ReturnFrameIndex + Mathf.Clamp01(frameProgress)) / returnSpan);
                returnT = SmoothStep01(returnT);
                feedbackOffset = Vector3.Lerp(contactOffset, Vector3.zero, returnT);
                contactScaleMultiplier = Mathf.Lerp(ContactScaleMultiplierFor(targetAnimator), 1f, returnT);
                motionFacingSign = faceReturnDirectionForSmoothMovement ? -1f : 1f;
            }
            else
            {
                feedbackOffset = Vector3.zero;
                contactScaleMultiplier = 1f;
                motionFacingSign = 1f;
            }
        }
    }

    private static bool ShouldSortAboveTarget(
        BattleAnimationClipData clip,
        int frameIndex,
        bool useTarget,
        BattleSpriteAnimator targetAnimator)
    {
        if (!useTarget || targetAnimator == null || clip == null || clip.ContactFrameIndex < 0)
            return false;

        int sortingStartFrame = Mathf.Max(0, clip.ContactFrameIndex - 1);
        if (frameIndex < sortingStartFrame)
            return false;

        return clip.ReturnFrameIndex < 0 || frameIndex < clip.ReturnFrameIndex;
    }

    private void ApplyContactSorting(BattleSpriteAnimator targetAnimator, bool active)
    {
        if (!active || targetAnimator == null || contactSortingOrderBoost <= 0)
        {
            RestoreSortingOrders();
            return;
        }

        if (bodyRenderers == null || baseRendererSortingOrders == null)
            return;

        int delta = Mathf.Max(
            0,
            targetAnimator.MaxBodySortingOrder + contactSortingOrderBoost - BaseBodySortingOrder());
        for (int i = 0; i < bodyRenderers.Length && i < baseRendererSortingOrders.Length; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].sortingOrder = baseRendererSortingOrders[i] + delta;
        }

        contactSortingActive = true;
    }

    private int BaseBodySortingOrder()
    {
        int minOrder = int.MaxValue;
        if (baseRendererSortingOrders != null)
        {
            for (int i = 0; i < baseRendererSortingOrders.Length; i++)
                minOrder = Mathf.Min(minOrder, baseRendererSortingOrders[i]);
        }

        return minOrder == int.MaxValue ? 0 : minOrder;
    }

    private void RestoreSortingOrders()
    {
        if (!contactSortingActive)
            return;

        if (bodyRenderers != null && baseRendererSortingOrders != null)
        {
            for (int i = 0; i < bodyRenderers.Length && i < baseRendererSortingOrders.Length; i++)
            {
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].sortingOrder = baseRendererSortingOrders[i];
            }
        }

        contactSortingActive = false;
    }

    private static float SmoothStep01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
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

    private Vector3 ContactOffsetFor(
        BattleAnimationClipData clip,
        Vector3 worldTarget,
        BattleSpriteAnimator targetAnimator)
    {
        Vector3 selfPosition = ContactWorldPosition;
        Vector3 targetPosition = AdjustedTargetContactPoint(worldTarget, targetAnimator, selfPosition);
        Vector3 delta = targetPosition - selfPosition;
        delta.z = 0f;
        if (delta.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 direction = delta.normalized;
        float stopDistance = Mathf.Max(0f, clip.contactDistanceFromTarget);
        float travelDistance = Mathf.Max(0f, delta.magnitude - stopDistance);
        float signedX = clip.contactOffset.x * Mathf.Sign(direction.x == 0f ? 1f : direction.x);
        Vector3 authoredOffset = new Vector3(signedX, clip.contactOffset.y, 0f);
        Vector3 worldOffset = direction * travelDistance + authoredOffset;
        return WorldOffsetToBodyLocal(worldOffset);
    }

    private Vector3 AdjustedTargetContactPoint(
        Vector3 worldTarget,
        BattleSpriteAnimator targetAnimator,
        Vector3 selfPosition)
    {
        Vector3 adjusted = worldTarget;
        if (targetAnimator == null)
            return adjusted;

        Vector3 direction = worldTarget - selfPosition;
        if (Mathf.Abs(direction.x) <= 0.001f)
            return adjusted;

        if (targetAnimator.TryGetVisualBounds(out Bounds targetBounds))
            adjusted.x = direction.x > 0f ? targetBounds.min.x : targetBounds.max.x;

        return adjusted;
    }

    private Vector3 WorldOffsetToBodyLocal(Vector3 worldOffset)
    {
        Transform localSpace = body != null && body.parent != null ? body.parent : transform;
        return localSpace != null
            ? localSpace.InverseTransformVector(worldOffset)
            : worldOffset;
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
        feedbackOffset = Vector3.zero;
        contactScaleMultiplier = 1f;
        motionFacingSign = 1f;
        RestoreSortingOrders();
        ClearHeldProfileClip();
    }

    private void ResetSwitchRevealVisuals()
    {
        switchRevealAlpha = 1f;
        switchRevealFlash = 0f;
        switchRevealScaleOffset = 0f;
    }

    private void RestoreRendererBaseColors()
    {
        if (!initialized)
            return;

        if (bodyRenderers != null && baseRendererColors != null)
        {
            for (int i = 0; i < bodyRenderers.Length && i < baseRendererColors.Length; i++)
            {
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].color = baseRendererColors[i];
            }
        }

        if (shadowRenderer != null)
            shadowRenderer.color = baseShadowColor;
    }

    private void ApplySwitchRevealShadowColor()
    {
        if (shadowRenderer == null)
            return;

        Color shadowColor = baseShadowColor;
        shadowColor.a = baseShadowColor.a * switchRevealAlpha;
        shadowRenderer.color = shadowColor;
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
        motionFacingSign = 1f;
        RestoreSortingOrders();
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
        motionFacingSign = 1f;
        RestoreSortingOrders();
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
            color = Color.Lerp(color, Color.white, switchRevealFlash);
            color.a *= switchRevealAlpha;
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
