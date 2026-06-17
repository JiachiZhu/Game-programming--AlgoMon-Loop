using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared hover / press feedback for battle HUD buttons (skill slots + action
/// icons). Drives a scale lerp (hover grow, press shrink, release pop), an
/// optional background colour lerp, an optional icon tint, and an optional
/// overlay image whose alpha rises on hover and flashes on release. Configured
/// from code (BattleHudController) because the slots it decorates are
/// runtime-built; nothing here is serialized into the prefab.
/// </summary>
[DisallowMultipleComponent]
// Defense note: BattleHudButtonFeedback is a Unity component attached to a scene object for this feature.
public sealed class BattleHudButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private const float LerpSpeed = 16f;
    private const float ReleasePopSeconds = 0.18f;
    private const float ReleasePopScale = 0.05f;

    private Selectable selectable;
    private RectTransform scaleTarget;
    private float hoverScale = 1.03f;
    private float pressedScale = 0.95f;

    private Graphic background;
    private Color backgroundNormal;
    private Color backgroundHover;
    private Color backgroundPressed;
    private Color backgroundDisabled;
    private bool driveBackground;

    private Graphic icon;
    private Color iconNormal;
    private Color iconHover;
    private Color iconDisabled;
    private bool driveIcon;

    private Image overlay;
    private Color overlayColor;
    private float overlayHoverAlpha;
    private float overlayPressedAlpha;
    private float overlayFlashAlpha;
    private bool driveOverlay;

    private CanvasGroup dimGroup;
    private float dimNormalAlpha = 1f;
    private float dimDisabledAlpha = 1f;
    private bool driveDim;

    private bool hovered;
    private bool pressed;
    private float releaseTimer;
    private Vector3 baseScale;
    private bool configured;

    private bool IsInteractable => selectable == null || selectable.interactable;

    // Defense note: Runs the configure helper used by this script.
    public void Configure(Selectable targetSelectable, float hover, float press)
    {
        selectable = targetSelectable != null ? targetSelectable : GetComponent<Selectable>();
        scaleTarget = transform as RectTransform;
        hoverScale = hover;
        pressedScale = press;
        baseScale = Vector3.one;
        configured = true;

        // This component owns all visual response; the Button's own tint/sprite
        // swap would fight the lerps below.
        if (selectable != null)
            selectable.transition = Selectable.Transition.None;

        ApplyImmediate();
    }

    // Defense note: Updates the background state or visual value.
    public void SetBackground(Graphic graphic, Color normal, Color hover, Color pressedColor, Color disabled)
    {
        background = graphic;
        backgroundNormal = normal;
        backgroundHover = hover;
        backgroundPressed = pressedColor;
        backgroundDisabled = disabled;
        driveBackground = graphic != null;
        ApplyImmediate();
    }

    // Defense note: Updates the icon state or visual value.
    public void SetIcon(Graphic graphic, Color normal, Color hover, Color disabled)
    {
        icon = graphic;
        iconNormal = normal;
        iconHover = hover;
        iconDisabled = disabled;
        driveIcon = graphic != null;
        ApplyImmediate();
    }

    // Defense note: Updates the overlay state or visual value.
    public void SetOverlay(Image overlayImage, Color color, float hoverAlpha, float pressedAlpha, float flashAlpha)
    {
        overlay = overlayImage;
        overlayColor = color;
        overlayHoverAlpha = hoverAlpha;
        overlayPressedAlpha = pressedAlpha;
        overlayFlashAlpha = flashAlpha;
        driveOverlay = overlayImage != null;
        ApplyImmediate();
    }

    /// <summary>
    /// Fades a CanvasGroup (the whole button content) between a normal and a
    /// disabled alpha so unavailable slots read as dimmed.
    /// </summary>
    // Defense note: Updates the dim group state or visual value.
    public void SetDimGroup(CanvasGroup group, float normalAlpha, float disabledAlpha)
    {
        dimGroup = group;
        dimNormalAlpha = normalAlpha;
        dimDisabledAlpha = disabledAlpha;
        driveDim = group != null;
        ApplyImmediate();
    }

    // Defense note: Unity lifecycle hook that runs the on enable step for this component.
    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        releaseTimer = 0f;
        if (configured)
            ApplyImmediate();
    }

    // Defense note: Unity lifecycle hook that runs the on disable step for this component.
    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        releaseTimer = 0f;
        if (scaleTarget != null)
            scaleTarget.localScale = baseScale;
    }

    // Defense note: Unity lifecycle hook that runs the update step for this component.
    private void Update()
    {
        if (!configured)
            return;

        if (releaseTimer > 0f)
            releaseTimer = Mathf.Max(0f, releaseTimer - Time.unscaledDeltaTime);

        float t = 1f - Mathf.Exp(-LerpSpeed * Time.unscaledDeltaTime);
        bool interactable = IsInteractable;
        bool hoverActive = interactable && hovered;
        bool pressActive = interactable && pressed;
        float pop = releaseTimer / ReleasePopSeconds;

        if (scaleTarget != null)
        {
            float target = pressActive ? pressedScale : (hoverActive ? hoverScale : 1f);
            // Release pop: a brief overshoot past the resting scale right after
            // the click so the press visibly "lands".
            target += ReleasePopScale * pop * pop;
            scaleTarget.localScale = Vector3.Lerp(scaleTarget.localScale, baseScale * target, t);
        }

        if (driveBackground)
        {
            Color target = !interactable ? backgroundDisabled
                : pressActive ? backgroundPressed
                : hoverActive ? backgroundHover
                : backgroundNormal;
            background.color = Color.Lerp(background.color, target, t);
        }

        if (driveIcon)
        {
            Color target = !interactable ? iconDisabled
                : (hoverActive || pressActive) ? iconHover
                : iconNormal;
            icon.color = Color.Lerp(icon.color, target, t);
        }

        if (driveOverlay)
        {
            float targetAlpha = !interactable ? 0f
                : pressActive ? overlayPressedAlpha
                : hoverActive ? overlayHoverAlpha
                : 0f;
            targetAlpha = Mathf.Max(targetAlpha, overlayFlashAlpha * pop * pop);
            Color current = overlay.color;
            Color target = overlayColor;
            target.a = targetAlpha;
            overlay.color = Color.Lerp(current, target, t);
            overlay.enabled = overlay.color.a > 0.004f;
        }

        if (driveDim)
        {
            float target = interactable ? dimNormalAlpha : dimDisabledAlpha;
            dimGroup.alpha = Mathf.Lerp(dimGroup.alpha, target, t);
        }
    }

    // Defense note: Applies the immediate change to gameplay or UI state.
    private void ApplyImmediate()
    {
        if (scaleTarget != null)
            scaleTarget.localScale = baseScale;

        if (driveBackground)
            background.color = IsInteractable ? backgroundNormal : backgroundDisabled;

        if (driveIcon)
            icon.color = IsInteractable ? iconNormal : iconDisabled;

        if (driveOverlay)
        {
            Color clear = overlayColor;
            clear.a = 0f;
            overlay.color = clear;
            overlay.enabled = false;
        }

        if (driveDim)
            dimGroup.alpha = IsInteractable ? dimNormalAlpha : dimDisabledAlpha;
    }

    // Defense note: Runs the on pointer enter helper used by this script.
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    // Defense note: Runs the on pointer exit helper used by this script.
    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    // Defense note: Runs the on pointer down helper used by this script.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable)
            return;
        pressed = true;
        releaseTimer = 0f;
    }

    // Defense note: Runs the on pointer up helper used by this script.
    public void OnPointerUp(PointerEventData eventData)
    {
        if (pressed)
            releaseTimer = ReleasePopSeconds;
        pressed = false;
    }
}
