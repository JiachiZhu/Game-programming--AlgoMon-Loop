using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
// Defense note: CyberImageButtonFeedback is a Unity component attached to a scene object for this feature.
public sealed class CyberImageButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Targets")]
    [SerializeField] private Selectable selectable;
    [SerializeField] private Graphic frameGraphic;
    [SerializeField] private Graphic glowGraphic;
    [SerializeField] private Graphic labelGraphic;
    [SerializeField] private Graphic[] accentGraphics;

    [Header("State")]
    [SerializeField] private bool selected;
    [SerializeField] private bool useCustomAccentColor;
    [SerializeField] private Color customAccentColor = Color.white;
    [SerializeField] private CyberUiColorRole accentRole = CyberUiColorRole.Primary;

    [Header("Motion")]
    [SerializeField] private float colorLerpSpeed = 20f;
    [SerializeField] private float hoverScale = 1.014f;
    [SerializeField] private float pressedScale = 0.982f;
    [SerializeField, Range(0f, 1f)] private float hoverGlowAlpha = 0.20f;
    [SerializeField, Range(0f, 1f)] private float pressedGlowAlpha = 0.42f;

    private bool hovered;
    private bool pressed;
    private bool focused;
    private Vector3 baseScale = Vector3.one;
    private Color baseFrameColor = Color.white;
    private Color baseGlowColor = Color.clear;
    private Color baseLabelColor = Color.white;
    private Color[] baseAccentColors;
    private Color currentFrameColor;
    private Color currentGlowColor;
    private Color currentLabelColor;
    private Color[] currentAccentColors;
    private bool initialized;

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            if (initialized)
                ApplyImmediate();
        }
    }

    public CyberUiColorRole AccentRole
    {
        get => accentRole;
        set
        {
            accentRole = value;
            useCustomAccentColor = false;
            if (initialized)
                ApplyImmediate();
        }
    }

    public Color CustomAccentColor
    {
        get => useCustomAccentColor ? customAccentColor : AccentColor;
        set
        {
            customAccentColor = value;
            useCustomAccentColor = true;
            if (initialized)
                ApplyImmediate();
        }
    }

    private Color AccentColor => useCustomAccentColor ? customAccentColor : CyberUiTheme.ColorFor(accentRole);
    private bool IsInteractable => selectable == null || selectable.interactable;

    // Defense note: Runs the reset helper used by this script.
    private void Reset()
    {
        AutoBindTargets();
    }

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        AutoBindTargets();
        CaptureBaseColors();
        ApplyImmediate();
    }

    // Defense note: Unity lifecycle hook that runs the on enable step for this component.
    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        focused = false;
        baseScale = transform.localScale;
        if (baseAccentColors == null || baseAccentColors.Length != AccentCount)
            CaptureBaseColors();
        ApplyImmediate();
    }

    // Defense note: Unity lifecycle hook that runs the update step for this component.
    private void Update()
    {
        CyberImageButtonVisualState state = ResolveState();
        float t = 1f - Mathf.Exp(-colorLerpSpeed * Time.unscaledDeltaTime);

        currentFrameColor = Color.Lerp(currentFrameColor, FrameColorFor(state), t);
        currentGlowColor = Color.Lerp(currentGlowColor, GlowColorFor(state), t);
        currentLabelColor = Color.Lerp(currentLabelColor, LabelColorFor(state), t);

        EnsureAccentBuffers();
        for (int i = 0; i < AccentCount; i++)
            currentAccentColors[i] = Color.Lerp(currentAccentColors[i], AccentGraphicColorFor(state, i), t);

        ApplyColors();

        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * ScaleMultiplierFor(state), t);
    }

    // Defense note: Runs the on pointer enter helper used by this script.
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        ApplyImmediate();
    }

    // Defense note: Runs the on pointer exit helper used by this script.
    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        ApplyImmediate();
    }

    // Defense note: Runs the on pointer down helper used by this script.
    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        ApplyImmediate();
    }

    // Defense note: Runs the on pointer up helper used by this script.
    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        ApplyImmediate();
    }

    // Defense note: Runs the on select helper used by this script.
    public void OnSelect(BaseEventData eventData)
    {
        focused = true;
        ApplyImmediate();
    }

    // Defense note: Runs the on deselect helper used by this script.
    public void OnDeselect(BaseEventData eventData)
    {
        focused = false;
        pressed = false;
        ApplyImmediate();
    }

    // Defense note: Runs the auto bind targets helper used by this script.
    private void AutoBindTargets()
    {
        if (selectable == null)
            selectable = GetComponent<Selectable>();
        if (frameGraphic == null)
            frameGraphic = GetComponent<Graphic>();
        if (glowGraphic == null)
        {
            Transform glow = transform.Find("HoverGlow");
            if (glow == null)
                glow = transform.Find("Glow");
            if (glow != null)
                glowGraphic = glow.GetComponent<Graphic>();
        }
        if (labelGraphic == null)
        {
            Transform label = transform.Find("Label");
            if (label == null)
                label = transform.Find("Text");
            if (label != null)
                labelGraphic = label.GetComponent<Graphic>();
        }
        if (accentGraphics == null || accentGraphics.Length == 0)
            accentGraphics = FindAccentGraphics();
    }

    // Defense note: Finds the accent graphics reference used by this component.
    private Graphic[] FindAccentGraphics()
    {
        var graphics = new List<Graphic>();
        Graphic[] children = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Graphic graphic = children[i];
            if (graphic == null || graphic == frameGraphic || graphic == glowGraphic || graphic == labelGraphic)
                continue;

            string childName = graphic.name;
            if (childName.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                childName.IndexOf("Signal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                childName.IndexOf("Rail", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                childName.IndexOf("Notch", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                graphics.Add(graphic);
            }
        }

        return graphics.ToArray();
    }

    // Defense note: Runs the capture base colors helper used by this script.
    private void CaptureBaseColors()
    {
        baseScale = transform.localScale;
        baseFrameColor = frameGraphic != null ? frameGraphic.color : Color.white;
        baseGlowColor = glowGraphic != null ? glowGraphic.color : Color.clear;
        baseLabelColor = labelGraphic != null ? labelGraphic.color : CyberUiTheme.TextPrimary;
        int count = AccentCount;
        baseAccentColors = new Color[count];
        currentAccentColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            Color color = accentGraphics[i] != null ? accentGraphics[i].color : AccentColor;
            baseAccentColors[i] = color;
            currentAccentColors[i] = color;
        }

        currentFrameColor = baseFrameColor;
        currentGlowColor = baseGlowColor;
        currentLabelColor = baseLabelColor;
        initialized = true;
    }

    // Defense note: Applies the immediate change to gameplay or UI state.
    private void ApplyImmediate()
    {
        CyberImageButtonVisualState state = ResolveState();
        currentFrameColor = FrameColorFor(state);
        currentGlowColor = GlowColorFor(state);
        currentLabelColor = LabelColorFor(state);
        EnsureAccentBuffers();
        for (int i = 0; i < AccentCount; i++)
            currentAccentColors[i] = AccentGraphicColorFor(state, i);
        ApplyColors();
        transform.localScale = baseScale * ScaleMultiplierFor(state);
    }

    // Defense note: Applies the colors change to gameplay or UI state.
    private void ApplyColors()
    {
        if (frameGraphic != null)
            frameGraphic.color = currentFrameColor;
        if (glowGraphic != null)
            glowGraphic.color = currentGlowColor;
        if (labelGraphic != null)
            labelGraphic.color = currentLabelColor;

        for (int i = 0; i < AccentCount; i++)
        {
            if (accentGraphics[i] != null && currentAccentColors != null && i < currentAccentColors.Length)
                accentGraphics[i].color = currentAccentColors[i];
        }
    }

    // Defense note: Resolves the state step and updates dependent state.
    private CyberImageButtonVisualState ResolveState()
    {
        if (!IsInteractable)
            return CyberImageButtonVisualState.Disabled;
        if (pressed)
            return CyberImageButtonVisualState.Pressed;
        if (selected)
            return CyberImageButtonVisualState.Selected;
        if (hovered || focused)
            return CyberImageButtonVisualState.Hover;
        return CyberImageButtonVisualState.Normal;
    }

    // Defense note: Runs the frame color for helper used by this script.
    private Color FrameColorFor(CyberImageButtonVisualState state)
    {
        Color accent = AccentColor;
        switch (state)
        {
            case CyberImageButtonVisualState.Disabled:
                return CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, Mathf.Min(baseFrameColor.a, 0.62f));
            case CyberImageButtonVisualState.Hover:
                return Blend(baseFrameColor, Color.Lerp(accent, Color.white, 0.24f), 0.34f, Mathf.Max(baseFrameColor.a, 0.92f));
            case CyberImageButtonVisualState.Selected:
                return Blend(baseFrameColor, Color.Lerp(accent, CyberUiTheme.Selected, 0.32f), 0.42f, Mathf.Max(baseFrameColor.a, 0.96f));
            case CyberImageButtonVisualState.Pressed:
                return Blend(baseFrameColor, CyberUiTheme.Selected, 0.58f, 1f);
            case CyberImageButtonVisualState.Normal:
            default:
                return baseFrameColor;
        }
    }

    // Defense note: Runs the glow color for helper used by this script.
    private Color GlowColorFor(CyberImageButtonVisualState state)
    {
        Color accent = AccentColor;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
        float alpha;
        switch (state)
        {
            case CyberImageButtonVisualState.Hover:
                alpha = hoverGlowAlpha * (0.76f + 0.24f * pulse);
                break;
            case CyberImageButtonVisualState.Selected:
                alpha = hoverGlowAlpha * 1.08f;
                break;
            case CyberImageButtonVisualState.Pressed:
                alpha = pressedGlowAlpha;
                break;
            default:
                alpha = 0f;
                break;
        }

        return CyberUiTheme.WithAlpha(Color.Lerp(accent, Color.white, state == CyberImageButtonVisualState.Pressed ? 0.28f : 0.08f), alpha);
    }

    // Defense note: Runs the label color for helper used by this script.
    private Color LabelColorFor(CyberImageButtonVisualState state)
    {
        switch (state)
        {
            case CyberImageButtonVisualState.Disabled:
                return CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.72f);
            case CyberImageButtonVisualState.Hover:
            case CyberImageButtonVisualState.Pressed:
            case CyberImageButtonVisualState.Selected:
                return Color.white;
            case CyberImageButtonVisualState.Normal:
            default:
                return baseLabelColor;
        }
    }

    // Defense note: Runs the accent graphic color for helper used by this script.
    private Color AccentGraphicColorFor(CyberImageButtonVisualState state, int index)
    {
        Color baseColor = baseAccentColors != null && index < baseAccentColors.Length ? baseAccentColors[index] : AccentColor;
        Color accent = AccentColor;
        switch (state)
        {
            case CyberImageButtonVisualState.Disabled:
                return CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.58f);
            case CyberImageButtonVisualState.Hover:
                return Blend(baseColor, Color.Lerp(accent, Color.white, 0.18f), 0.46f, Mathf.Max(baseColor.a, 0.92f));
            case CyberImageButtonVisualState.Selected:
                return Blend(baseColor, CyberUiTheme.Selected, 0.34f, Mathf.Max(baseColor.a, 0.98f));
            case CyberImageButtonVisualState.Pressed:
                return Blend(baseColor, Color.white, 0.58f, 1f);
            case CyberImageButtonVisualState.Normal:
            default:
                return baseColor;
        }
    }

    // Defense note: Runs the blend helper used by this script.
    private static Color Blend(Color from, Color to, float amount, float alpha)
    {
        Color color = Color.Lerp(from, to, Mathf.Clamp01(amount));
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    // Defense note: Runs the scale multiplier for helper used by this script.
    private float ScaleMultiplierFor(CyberImageButtonVisualState state)
    {
        if (state == CyberImageButtonVisualState.Pressed)
            return pressedScale;
        if (state == CyberImageButtonVisualState.Hover || state == CyberImageButtonVisualState.Selected)
            return hoverScale;
        return 1f;
    }

    // Defense note: Ensures the accent buffers dependency or state exists before use.
    private void EnsureAccentBuffers()
    {
        int count = AccentCount;
        if (baseAccentColors == null || baseAccentColors.Length != count)
            CaptureBaseColors();
        if (currentAccentColors == null || currentAccentColors.Length != count)
            currentAccentColors = new Color[count];
    }

    private int AccentCount => accentGraphics != null ? accentGraphics.Length : 0;

    // Defense note: CyberImageButtonVisualState defines the valid cyber image button visual state options used by the gameplay systems.
    private enum CyberImageButtonVisualState
    {
        Normal,
        Hover,
        Selected,
        Pressed,
        Disabled
    }
}
