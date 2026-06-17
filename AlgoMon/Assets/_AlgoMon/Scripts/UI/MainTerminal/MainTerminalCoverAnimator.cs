using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cosmetic animator for the cover art. It uses scene-placed overlay patches
/// so the flat background can feel alive without requiring layered source art.
/// </summary>
[DisallowMultipleComponent]
// Defense note: MainTerminalCoverAnimator is a Unity component attached to a scene object for this feature.
public class MainTerminalCoverAnimator : MonoBehaviour
{
    [System.Serializable]
    // Defense note: FingerTapPatch groups small runtime values that are passed around together.
    private struct FingerTapPatch
    {
        public RectTransform patch;
        public CanvasGroup group;
        public float phase;
        public float weight;
        public float xPixels;
        public float rotationDegrees;
        public float scalePulse;
    }

    [Header("Typing Overlays")]
    [SerializeField] private FingerTapPatch[] fingerPatches;
    [SerializeField] private float typingPixels = 2f;
    [SerializeField] private float typingSpeed = 4.8f;

    [Header("Left Cover Motion")]
    [SerializeField] private Image leftMotionCurrent;
    [SerializeField] private Image leftMotionNext;
    [SerializeField] private Sprite[] leftMotionFrames;
    [SerializeField] private float leftMotionFrameSeconds = 0.24f;
    [SerializeField] private float leftMotionCrossFadeSeconds = 0.1f;

    [Header("Terminal Glow")]
    [SerializeField] private Image keyboardGlow;
    [SerializeField] private Image screenVeil;
    [SerializeField] private Image cursor;
    [SerializeField] private Image[] statusDots;
    [SerializeField] private Image[] keyGlows;

    private Vector2[] fingerOrigins;
    private int leftMotionIndex;
    private float leftMotionTimer;

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        CacheFingerOrigins();
        InitializeLeftMotion();
    }

    // Defense note: Unity lifecycle hook that runs the update step for this component.
    private void Update()
    {
        float time = Time.unscaledTime;
        AnimateLeftMotion(Time.unscaledDeltaTime);
        AnimateFingerPatches(time);
        AnimateGlow(time);
        AnimateStatusDots(time);
        AnimateKeyGlows(time);
    }

    // Defense note: Runs the cache finger origins helper used by this script.
    private void CacheFingerOrigins()
    {
        if (fingerPatches == null || fingerPatches.Length == 0)
        {
            fingerOrigins = System.Array.Empty<Vector2>();
            return;
        }

        fingerOrigins = new Vector2[fingerPatches.Length];
        for (int i = 0; i < fingerPatches.Length; i++)
        {
            fingerOrigins[i] = fingerPatches[i].patch != null
                ? fingerPatches[i].patch.anchoredPosition
                : Vector2.zero;
        }
    }

    // Defense note: Runs the animate finger patches helper used by this script.
    private void AnimateFingerPatches(float time)
    {
        if (fingerPatches == null || fingerOrigins == null)
            return;

        for (int i = 0; i < fingerPatches.Length && i < fingerOrigins.Length; i++)
        {
            FingerTapPatch finger = fingerPatches[i];
            if (finger.patch == null)
                continue;

            float cycle = Mathf.Repeat(time * typingSpeed + finger.phase, 1f);
            const float pressWindow = 0.34f;
            float press = cycle < pressWindow
                ? Mathf.Sin((cycle / pressWindow) * Mathf.PI)
                : 0f;
            press = Mathf.SmoothStep(0f, 1f, press);
            float weight = finger.weight <= 0f ? 1f : finger.weight;

            finger.patch.anchoredPosition = fingerOrigins[i] + new Vector2(finger.xPixels * press, -typingPixels * weight * press);
            finger.patch.localRotation = Quaternion.Euler(0f, 0f, finger.rotationDegrees * press);
            float scale = 1f + finger.scalePulse * press;
            finger.patch.localScale = new Vector3(scale, scale, 1f);

            if (finger.group != null)
                finger.group.alpha = 0.05f + 0.30f * press;
        }
    }

    // Defense note: Runs the initialize left motion helper used by this script.
    private void InitializeLeftMotion()
    {
        leftMotionIndex = 0;
        leftMotionTimer = 0f;

        if (leftMotionCurrent == null || leftMotionFrames == null || leftMotionFrames.Length == 0)
            return;

        leftMotionCurrent.sprite = leftMotionFrames[0];
        SetAlpha(leftMotionCurrent, 1f);

        if (leftMotionNext != null)
        {
            leftMotionNext.sprite = leftMotionFrames.Length > 1 ? leftMotionFrames[1] : leftMotionFrames[0];
            SetAlpha(leftMotionNext, 0f);
        }
    }

    // Defense note: Runs the animate left motion helper used by this script.
    private void AnimateLeftMotion(float deltaTime)
    {
        if (leftMotionCurrent == null || leftMotionFrames == null || leftMotionFrames.Length <= 1)
            return;

        float frameSeconds = Mathf.Max(0.05f, leftMotionFrameSeconds);
        float crossFadeSeconds = Mathf.Clamp(leftMotionCrossFadeSeconds, 0f, frameSeconds * 0.45f);

        leftMotionTimer += deltaTime;

        if (leftMotionTimer >= frameSeconds)
        {
            leftMotionTimer -= frameSeconds;
            leftMotionIndex = (leftMotionIndex + 1) % leftMotionFrames.Length;
            leftMotionCurrent.sprite = leftMotionFrames[leftMotionIndex];
            SetAlpha(leftMotionCurrent, 1f);

            if (leftMotionNext != null)
                SetAlpha(leftMotionNext, 0f);
        }

        if (leftMotionNext == null || crossFadeSeconds <= 0f)
            return;

        int nextIndex = (leftMotionIndex + 1) % leftMotionFrames.Length;
        leftMotionNext.sprite = leftMotionFrames[nextIndex];

        float fadeStart = frameSeconds - crossFadeSeconds;
        float fade = leftMotionTimer > fadeStart
            ? Mathf.InverseLerp(fadeStart, frameSeconds, leftMotionTimer)
            : 0f;
        SetAlpha(leftMotionNext, Mathf.SmoothStep(0f, 1f, fade));
    }

    // Defense note: Runs the animate glow helper used by this script.
    private void AnimateGlow(float time)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 7.2f);

        if (keyboardGlow != null)
            SetAlpha(keyboardGlow, 0.18f + 0.16f * pulse);
        if (screenVeil != null)
            SetAlpha(screenVeil, 0.05f + 0.035f * Mathf.Sin(time * 2.1f));
        if (cursor != null)
            SetAlpha(cursor, Mathf.PingPong(time * 2.5f, 1f) > 0.5f ? 0.95f : 0.12f);
    }

    // Defense note: Runs the animate status dots helper used by this script.
    private void AnimateStatusDots(float time)
    {
        if (statusDots == null)
            return;

        for (int i = 0; i < statusDots.Length; i++)
        {
            if (statusDots[i] == null)
                continue;

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4.1f + i * 0.75f);
            SetAlpha(statusDots[i], 0.35f + pulse * 0.55f);
        }
    }

    // Defense note: Runs the animate key glows helper used by this script.
    private void AnimateKeyGlows(float time)
    {
        if (keyGlows == null)
            return;

        for (int i = 0; i < keyGlows.Length; i++)
        {
            if (keyGlows[i] == null)
                continue;

            float cycle = Mathf.Repeat(time * typingSpeed + i * 0.18f, 1f);
            float flash = cycle < 0.24f
                ? Mathf.Sin((cycle / 0.24f) * Mathf.PI)
                : 0f;
            SetAlpha(keyGlows[i], 0.04f + 0.42f * flash);
        }
    }

    // Defense note: Updates the alpha state or visual value.
    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
