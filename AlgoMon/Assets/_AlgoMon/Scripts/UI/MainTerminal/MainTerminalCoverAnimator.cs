using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cosmetic animator for the cover art. It uses scene-placed overlay patches
/// so the flat background can feel alive without requiring layered source art.
/// </summary>
[DisallowMultipleComponent]
public class MainTerminalCoverAnimator : MonoBehaviour
{
    [System.Serializable]
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

    private void Awake()
    {
        CacheFingerOrigins();
        InitializeLeftMotion();
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        AnimateLeftMotion(Time.unscaledDeltaTime);
        AnimateFingerPatches(time);
        AnimateGlow(time);
        AnimateStatusDots(time);
        AnimateKeyGlows(time);
    }

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

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
