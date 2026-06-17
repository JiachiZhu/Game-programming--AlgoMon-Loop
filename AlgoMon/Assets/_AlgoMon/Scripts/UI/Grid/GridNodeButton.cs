/*
Script Audit:
- Purpose: Displays and forwards clicks for one node on TheGrid map.
- Attached GameObject: Runtime/generated UI Button object for a single GridNode.
- Main responsibilities: Bind a GridNode, update icon/text/ring colors, set interactability, and call the click callback.
- Important variables: typeLabel, detailLabel, stateLabel, ringImage, iconImage, button, background, node, clicked.
- Inputs: GridNode data, visual state values, sprites/colors, and player click events.
- Outputs or effects: Updates node UI and tells GridMapController which node was clicked.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Click a nextAvailable node button and verify GridMapController receives the correct GridNode.
*/
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime view for one route node on TheGrid map.
/// GridMapController owns the layout and state decisions; this class only
/// presents a node and forwards valid click attempts.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
// Defense note: GridNodeButton wraps button state and click behavior for a UI element.
public class GridNodeButton : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    private const float HoverLerpSpeed = 18f;
    private const float CurrentPulseSpeed = 4.8f;

    [SerializeField] private Text typeLabel;
    [SerializeField] private Text detailLabel;
    [SerializeField] private Text stateLabel;
    [SerializeField] private Image haloImage;
    [SerializeField] private Image ringImage;
    [SerializeField] private Image coreImage;
    [SerializeField] private Image iconImage;

    private Button button;
    private Image background;
    private GridNode node;
    private Action<GridNode> clicked;
    private Action<GridNode> previewed;
    private GridNodeVisualState currentVisualState;
    private bool currentlyInteractable;
    private bool hovered;
    private bool focused;
    private bool pressed;
    private float hoverBlend;
    private Vector3 baseScale = Vector3.one;
    private Color baseHaloColor;
    private Color baseRingColor;
    private Color baseCoreColor;
    private bool hasAnimatedColors;

    public GridNode Node => node;

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        CacheReferences();
        baseScale = transform.localScale;
    }

    // Defense note: Unity lifecycle hook that runs the on enable step for this component.
    private void OnEnable()
    {
        if (baseScale == Vector3.zero)
            baseScale = transform.localScale;
        hovered = false;
        pressed = false;
        focused = false;
        ApplyAnimatedVisuals(true);
    }

    // Defense note: Unity lifecycle hook that runs the update step for this component.
    private void Update()
    {
        ApplyAnimatedVisuals(false);
    }

    // Defense note: Unity lifecycle hook that runs the on destroy step for this component.
    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    // Defense note: Runs the bind helper used by this script.
    public void Bind(GridNode gridNode, Action<GridNode> onClicked, Action<GridNode> onPreviewed = null)
    {
        CacheReferences();

        node = gridNode;
        clicked = onClicked;
        previewed = onPreviewed;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        if (typeLabel != null)
            typeLabel.text = (gridNode != null ? gridNode.nodeType : NodeType.Combat).ToGridIcon();
        if (detailLabel != null)
        {
            detailLabel.text = gridNode != null ? gridNode.nodeType.ToGridLabelUpper() : string.Empty;
            detailLabel.gameObject.SetActive(false);
        }
    }

    // Defense note: Updates the visual state or visual value.
    public void SetVisual(
        GridNodeVisualState visualState,
        Color fillColor,
        Color outlineColor,
        Color textColor,
        Sprite iconSprite,
        Color iconColor,
        string stateText,
        string detailText,
        Color detailColor,
        bool interactable)
    {
        CacheReferences();

        currentVisualState = visualState;
        currentlyInteractable = interactable;

        if (background != null)
            background.color = fillColor;
        baseHaloColor = HaloColorFor(visualState, outlineColor);
        baseRingColor = RingColorFor(visualState, outlineColor);
        baseCoreColor = CoreColorFor(visualState, outlineColor);
        hasAnimatedColors = true;
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = iconSprite != null;
            iconImage.color = iconColor;
        }
        if (button != null)
            button.interactable = interactable;

        bool useTextFallback = iconSprite == null;
        if (typeLabel != null)
            typeLabel.text = visualState == GridNodeVisualState.Unknown
                ? "?"
                : node != null ? node.nodeType.ToGridIcon() : string.Empty;
        if (typeLabel != null)
            typeLabel.gameObject.SetActive(useTextFallback);
        if (detailLabel != null)
        {
            detailLabel.text = detailText;
            detailLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(detailText));
        }

        SetTextColor(typeLabel, textColor);
        SetTextColor(detailLabel, detailColor);
        SetTextColor(stateLabel, textColor);

        if (stateLabel != null)
        {
            stateLabel.text = stateText;
            stateLabel.gameObject.SetActive(!string.IsNullOrEmpty(stateText));
        }

        ApplyAnimatedVisuals(true);
    }

    // Defense note: Runs the cache references helper used by this script.
    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (background == null)
            background = GetComponent<Image>();

        if (typeLabel == null)
            typeLabel = FindText("TypeLabel");
        if (detailLabel == null)
            detailLabel = FindText("DetailLabel");
        if (stateLabel == null)
            stateLabel = FindText("StateLabel");
        if (haloImage == null)
            haloImage = FindImage("HaloImage");
        if (ringImage == null)
            ringImage = FindImage("RingImage");
        if (coreImage == null)
            coreImage = FindImage("CoreImage");
        if (iconImage == null)
            iconImage = FindImage("IconImage");
    }

    // Defense note: Finds the text reference used by this component.
    private Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    // Defense note: Finds the image reference used by this component.
    private Image FindImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    // Defense note: Handles the click event or player interaction.
    private void HandleClick()
    {
        clicked?.Invoke(node);
    }

    // Defense note: Runs the on pointer enter helper used by this script.
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        previewed?.Invoke(node);
    }

    // Defense note: Runs the on pointer exit helper used by this script.
    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        previewed?.Invoke(null);
    }

    // Defense note: Runs the on select helper used by this script.
    public void OnSelect(BaseEventData eventData)
    {
        focused = true;
        previewed?.Invoke(node);
    }

    // Defense note: Runs the on deselect helper used by this script.
    public void OnDeselect(BaseEventData eventData)
    {
        focused = false;
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

    // Defense note: Applies the animated visuals change to gameplay or UI state.
    private void ApplyAnimatedVisuals(bool immediate)
    {
        if (!hasAnimatedColors)
            return;

        float t = immediate ? 1f : 1f - Mathf.Exp(-HoverLerpSpeed * Time.unscaledDeltaTime);
        bool hoverTargetActive = hovered || focused;
        hoverBlend = immediate
            ? (hoverTargetActive ? 1f : 0f)
            : Mathf.Lerp(hoverBlend, hoverTargetActive ? 1f : 0f, t);

        float pulse = currentVisualState == GridNodeVisualState.Current
            ? Mathf.Sin(Time.unscaledTime * CurrentPulseSpeed) * 0.5f + 0.5f
            : 0f;
        float nextPulse = currentVisualState == GridNodeVisualState.NextAvailable
            ? Mathf.Sin(Time.unscaledTime * (CurrentPulseSpeed * 0.72f)) * 0.5f + 0.5f
            : 0f;

        float hoverScale = HoverScaleFor(currentVisualState, currentlyInteractable);
        float targetScale = 1f + hoverBlend * hoverScale + pulse * 0.018f + nextPulse * 0.006f;
        if (pressed)
            targetScale -= 0.035f;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * targetScale, t);

        SetGraphicColor(haloImage, BoostAlpha(baseHaloColor, HaloBoostFor(currentVisualState, hoverBlend, pulse, nextPulse)), t);
        SetGraphicColor(ringImage, BoostAlpha(baseRingColor, RingBoostFor(currentVisualState, hoverBlend, pulse, nextPulse)), t);
        SetGraphicColor(coreImage, BoostAlpha(baseCoreColor, CoreBoostFor(currentVisualState, hoverBlend, pulse, nextPulse)), t);
    }

    // Defense note: Updates the text color state or visual value.
    private static void SetTextColor(Text text, Color color)
    {
        if (text != null)
            text.color = color;
    }

    // Defense note: Updates the graphic color state or visual value.
    private static void SetGraphicColor(Graphic graphic, Color targetColor, float t)
    {
        if (graphic != null)
            graphic.color = Color.Lerp(graphic.color, targetColor, t);
    }

    // Defense note: Runs the boost alpha helper used by this script.
    private static Color BoostAlpha(Color color, float boost)
    {
        return new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a + boost));
    }

    // Defense note: Runs the hover scale for helper used by this script.
    private static float HoverScaleFor(GridNodeVisualState visualState, bool interactable)
    {
        if (visualState == GridNodeVisualState.Inactive || visualState == GridNodeVisualState.Unknown)
            return 0.025f;
        if (visualState == GridNodeVisualState.Current)
            return 0.055f;
        if (interactable || visualState == GridNodeVisualState.NextAvailable)
            return 0.085f;
        return 0.045f;
    }

    // Defense note: Runs the halo boost for helper used by this script.
    private static float HaloBoostFor(GridNodeVisualState visualState, float hover, float pulse, float nextPulse)
    {
        switch (visualState)
        {
            case GridNodeVisualState.Unknown:
                return 0.010f;
            case GridNodeVisualState.Current:
                return hover * 0.095f + pulse * 0.130f;
            case GridNodeVisualState.NextAvailable:
                return hover * 0.105f + nextPulse * 0.030f;
            case GridNodeVisualState.Target:
                return hover * 0.080f;
            case GridNodeVisualState.Visited:
                return hover * 0.045f;
            default:
                return hover * 0.018f;
        }
    }

    // Defense note: Runs the ring boost for helper used by this script.
    private static float RingBoostFor(GridNodeVisualState visualState, float hover, float pulse, float nextPulse)
    {
        switch (visualState)
        {
            case GridNodeVisualState.Unknown:
                return 0.018f;
            case GridNodeVisualState.Current:
                return hover * 0.110f + pulse * 0.140f;
            case GridNodeVisualState.NextAvailable:
                return hover * 0.130f + nextPulse * 0.035f;
            case GridNodeVisualState.Target:
                return hover * 0.090f;
            case GridNodeVisualState.Visited:
                return hover * 0.060f;
            default:
                return hover * 0.025f;
        }
    }

    // Defense note: Runs the core boost for helper used by this script.
    private static float CoreBoostFor(GridNodeVisualState visualState, float hover, float pulse, float nextPulse)
    {
        switch (visualState)
        {
            case GridNodeVisualState.Unknown:
                return 0.010f;
            case GridNodeVisualState.Current:
                return hover * 0.070f + pulse * 0.090f;
            case GridNodeVisualState.NextAvailable:
                return hover * 0.070f + nextPulse * 0.025f;
            case GridNodeVisualState.Target:
                return hover * 0.050f;
            case GridNodeVisualState.Visited:
                return hover * 0.030f;
            default:
                return hover * 0.015f;
        }
    }

    // Defense note: Runs the core color for helper used by this script.
    private static Color CoreColorFor(GridNodeVisualState visualState, Color outlineColor)
    {
        float alpha;
        switch (visualState)
        {
            case GridNodeVisualState.Unknown:
                alpha = 0.035f;
                break;
            case GridNodeVisualState.Current:
                alpha = 0.52f;
                break;
            case GridNodeVisualState.NextAvailable:
                alpha = 0.30f;
                break;
            case GridNodeVisualState.Target:
                alpha = 0.24f;
                break;
            case GridNodeVisualState.Visited:
                alpha = 0.16f;
                break;
            default:
                alpha = 0.055f;
                break;
        }

        return new Color(outlineColor.r, outlineColor.g, outlineColor.b, alpha);
    }

    // Defense note: Runs the ring color for helper used by this script.
    private static Color RingColorFor(GridNodeVisualState visualState, Color outlineColor)
    {
        float alpha;
        switch (visualState)
        {
            case GridNodeVisualState.Unknown:
                alpha = 0.070f;
                break;
            case GridNodeVisualState.Current:
                alpha = 0.78f;
                break;
            case GridNodeVisualState.NextAvailable:
                alpha = 0.52f;
                break;
            case GridNodeVisualState.Target:
                alpha = 0.48f;
                break;
            case GridNodeVisualState.Visited:
                alpha = 0.26f;
                break;
            default:
                alpha = 0.12f;
                break;
        }

        return new Color(outlineColor.r, outlineColor.g, outlineColor.b, alpha);
    }

    // Defense note: Runs the halo color for helper used by this script.
    private static Color HaloColorFor(GridNodeVisualState visualState, Color outlineColor)
    {
        float alpha;
        switch (visualState)
        {
            case GridNodeVisualState.Unknown:
                alpha = 0.006f;
                break;
            case GridNodeVisualState.Current:
                alpha = 0.24f;
                break;
            case GridNodeVisualState.NextAvailable:
                alpha = 0.070f;
                break;
            case GridNodeVisualState.Target:
                alpha = 0.060f;
                break;
            case GridNodeVisualState.Visited:
                alpha = 0.030f;
                break;
            default:
                alpha = 0.012f;
                break;
        }

        return new Color(outlineColor.r, outlineColor.g, outlineColor.b, alpha);
    }
}
