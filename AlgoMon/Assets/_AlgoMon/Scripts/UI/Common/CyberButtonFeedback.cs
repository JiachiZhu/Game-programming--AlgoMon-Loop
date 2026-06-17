using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
// Defense note: CyberButtonFeedback is a Unity component attached to a scene object for this feature.
public sealed class CyberButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Targets")]
    [SerializeField] private Selectable selectable;
    [SerializeField] private CyberFrameGraphic frame;
    [SerializeField] private Graphic fallbackGraphic;
    [SerializeField] private Graphic labelGraphic;
    [SerializeField] private Graphic[] accentGraphics;

    [Header("State")]
    [SerializeField] private bool selected;
    [SerializeField] private CyberUiColorRole accentRole = CyberUiColorRole.Primary;
    [SerializeField] private bool takeOverSelectableTransition = true;

    [Header("Motion")]
    [SerializeField] private float colorLerpSpeed = 18f;
    [SerializeField] private float hoverScale = 1.012f;
    [SerializeField] private float pressedScale = 0.985f;

    private bool hovered;
    private bool pressed;
    private bool focused;
    private Vector3 baseScale;
    private Color currentFill;
    private Color currentBorder;
    private Color currentAccent;
    private Color currentText;

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            ApplyImmediate();
        }
    }

    public CyberUiColorRole AccentRole
    {
        get => accentRole;
        set
        {
            accentRole = value;
            ApplyImmediate();
        }
    }

    private bool IsInteractable => selectable == null || selectable.interactable;

    // Defense note: Runs the reset helper used by this script.
    private void Reset()
    {
        selectable = GetComponent<Selectable>();
        frame = GetComponent<CyberFrameGraphic>();
        fallbackGraphic = GetComponent<Graphic>();
        labelGraphic = GetComponentInChildren<Text>();
    }

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        if (selectable == null)
            selectable = GetComponent<Selectable>();
        if (frame == null)
            frame = GetComponent<CyberFrameGraphic>();
        if (fallbackGraphic == null)
            fallbackGraphic = GetComponent<Graphic>();
        if (labelGraphic == null)
            labelGraphic = GetComponentInChildren<Text>();

        if (takeOverSelectableTransition && selectable != null)
            selectable.transition = Selectable.Transition.None;

        baseScale = transform.localScale;
        ApplyImmediate();
    }

    // Defense note: Unity lifecycle hook that runs the on enable step for this component.
    private void OnEnable()
    {
        baseScale = transform.localScale;
        ApplyImmediate();
    }

    // Defense note: Unity lifecycle hook that runs the update step for this component.
    private void Update()
    {
        CyberButtonVisualState state = ResolveState();
        Color targetFill = FillFor(state);
        Color targetBorder = BorderFor(state);
        Color targetAccent = AccentFor(state);
        Color targetText = TextFor(state);
        float t = 1f - Mathf.Exp(-colorLerpSpeed * Time.unscaledDeltaTime);

        currentFill = Color.Lerp(currentFill, targetFill, t);
        currentBorder = Color.Lerp(currentBorder, targetBorder, t);
        currentAccent = Color.Lerp(currentAccent, targetAccent, t);
        currentText = Color.Lerp(currentText, targetText, t);
        ApplyColors();

        float targetScale = state == CyberButtonVisualState.Pressed
            ? pressedScale
            : (state == CyberButtonVisualState.Hover || state == CyberButtonVisualState.Selected ? hoverScale : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * targetScale, t);
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
        pressed = true;
    }

    // Defense note: Runs the on pointer up helper used by this script.
    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    // Defense note: Runs the on select helper used by this script.
    public void OnSelect(BaseEventData eventData)
    {
        focused = true;
    }

    // Defense note: Runs the on deselect helper used by this script.
    public void OnDeselect(BaseEventData eventData)
    {
        focused = false;
        pressed = false;
    }

    // Defense note: Updates the selected state or visual value.
    public void SetSelected(bool isSelected)
    {
        Selected = isSelected;
    }

    // Defense note: Applies the immediate change to gameplay or UI state.
    private void ApplyImmediate()
    {
        CyberButtonVisualState state = ResolveState();
        currentFill = FillFor(state);
        currentBorder = BorderFor(state);
        currentAccent = AccentFor(state);
        currentText = TextFor(state);
        ApplyColors();
    }

    // Defense note: Applies the colors change to gameplay or UI state.
    private void ApplyColors()
    {
        if (frame != null)
        {
            frame.FillColor = currentFill;
            frame.BorderColor = currentBorder;
            frame.AccentColor = currentAccent;
        }
        else if (fallbackGraphic != null)
        {
            fallbackGraphic.color = currentFill;
        }

        if (labelGraphic != null)
            labelGraphic.color = currentText;

        if (accentGraphics == null)
            return;

        for (int i = 0; i < accentGraphics.Length; i++)
        {
            if (accentGraphics[i] != null)
                accentGraphics[i].color = currentAccent;
        }
    }

    // Defense note: Resolves the state step and updates dependent state.
    private CyberButtonVisualState ResolveState()
    {
        if (!IsInteractable)
            return CyberButtonVisualState.Disabled;
        if (pressed)
            return CyberButtonVisualState.Pressed;
        if (selected)
            return CyberButtonVisualState.Selected;
        if (hovered || focused)
            return CyberButtonVisualState.Hover;
        return CyberButtonVisualState.Normal;
    }

    // Defense note: Runs the fill for helper used by this script.
    private Color FillFor(CyberButtonVisualState state)
    {
        Color roleColor = CyberUiTheme.ColorFor(accentRole);
        switch (state)
        {
            case CyberButtonVisualState.Hover:
                return CyberUiTheme.WithAlpha(roleColor, 0.14f);
            case CyberButtonVisualState.Selected:
                return CyberUiTheme.WithAlpha(roleColor, 0.18f);
            case CyberButtonVisualState.Pressed:
                return CyberUiTheme.WithAlpha(roleColor, 0.24f);
            case CyberButtonVisualState.Disabled:
                return CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.38f);
            case CyberButtonVisualState.Normal:
            default:
                return CyberUiTheme.WithAlpha(CyberUiTheme.Panel, 0.88f);
        }
    }

    // Defense note: Runs the border for helper used by this script.
    private Color BorderFor(CyberButtonVisualState state)
    {
        Color roleColor = CyberUiTheme.ColorFor(accentRole);
        switch (state)
        {
            case CyberButtonVisualState.Hover:
                return CyberUiTheme.WithAlpha(roleColor, 0.92f);
            case CyberButtonVisualState.Selected:
                return CyberUiTheme.WithAlpha(roleColor, 1f);
            case CyberButtonVisualState.Pressed:
                return CyberUiTheme.WithAlpha(roleColor, 1f);
            case CyberButtonVisualState.Disabled:
                return CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.64f);
            case CyberButtonVisualState.Normal:
            default:
                return CyberUiTheme.WithAlpha(roleColor, 0.52f);
        }
    }

    // Defense note: Runs the accent for helper used by this script.
    private Color AccentFor(CyberButtonVisualState state)
    {
        Color roleColor = CyberUiTheme.ColorFor(accentRole);
        switch (state)
        {
            case CyberButtonVisualState.Disabled:
                return CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.28f);
            case CyberButtonVisualState.Normal:
                return CyberUiTheme.WithAlpha(roleColor, 0.46f);
            case CyberButtonVisualState.Hover:
                return CyberUiTheme.WithAlpha(roleColor, 0.72f);
            case CyberButtonVisualState.Selected:
            case CyberButtonVisualState.Pressed:
            default:
                return CyberUiTheme.WithAlpha(roleColor, 1f);
        }
    }

    // Defense note: Runs the text for helper used by this script.
    private static Color TextFor(CyberButtonVisualState state)
    {
        switch (state)
        {
            case CyberButtonVisualState.Disabled:
                return CyberUiTheme.WithAlpha(CyberUiTheme.TextSecondary, 0.54f);
            case CyberButtonVisualState.Selected:
                return CyberUiTheme.Selected;
            case CyberButtonVisualState.Pressed:
                return CyberUiTheme.TextPrimary;
            case CyberButtonVisualState.Hover:
                return CyberUiTheme.TextPrimary;
            case CyberButtonVisualState.Normal:
            default:
                return CyberUiTheme.TextPrimary;
        }
    }

    // Defense note: CyberButtonVisualState defines the valid cyber button visual state options used by the gameplay systems.
    private enum CyberButtonVisualState
    {
        Normal,
        Hover,
        Selected,
        Pressed,
        Disabled
    }
}
