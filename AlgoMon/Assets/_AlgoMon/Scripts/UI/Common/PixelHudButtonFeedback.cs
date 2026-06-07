using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PixelHudButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private const float HoverLerpSpeed = 18f;
    private const float ReleaseFlashSeconds = 0.12f;

    [SerializeField] private Selectable selectable;
    [SerializeField] private Image hoverImage;
    [SerializeField] private Image pressImage;

    private bool hovered;
    private bool pressed;
    private float releaseTimer;
    private bool configured;

    private bool IsInteractable => selectable == null || selectable.interactable;

    public void Configure(Selectable targetSelectable, Image targetHoverImage, Image targetPressImage)
    {
        selectable = targetSelectable != null ? targetSelectable : GetComponent<Selectable>();
        hoverImage = targetHoverImage;
        pressImage = targetPressImage;
        configured = true;

        if (hoverImage != null)
            hoverImage.color = Color.clear;

        if (pressImage != null)
        {
            pressImage.color = Color.clear;
            pressImage.enabled = false;
        }
    }

    private void Awake()
    {
        if (selectable == null)
            selectable = GetComponent<Selectable>();
    }

    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        releaseTimer = 0f;
        ResetVisuals();
    }

    private void OnDisable()
    {
        pressed = false;
        releaseTimer = 0f;
        ResetVisuals();
    }

    private void Update()
    {
        if (!configured)
            return;

        UpdateHover();
        UpdatePress();
    }

    private void UpdateHover()
    {
        if (hoverImage == null)
            return;

        bool active = IsInteractable && (hovered || pressed);
        float targetAlpha = active ? (pressed ? 1f : 0.9f) : 0f;
        float t = 1f - Mathf.Exp(-HoverLerpSpeed * Time.unscaledDeltaTime);

        Color target = Color.white;
        target.a = targetAlpha;
        hoverImage.color = Color.Lerp(hoverImage.color, target, t);
    }

    private void UpdatePress()
    {
        if (pressImage == null)
            return;

        if (pressed && IsInteractable)
        {
            pressImage.color = Color.white;
            pressImage.enabled = true;
            return;
        }

        if (releaseTimer > 0f)
        {
            releaseTimer = Mathf.Max(0f, releaseTimer - Time.unscaledDeltaTime);
            float alpha = ReleaseFlashSeconds <= 0f ? 0f : releaseTimer / ReleaseFlashSeconds;
            pressImage.color = new Color(1f, 1f, 1f, alpha * 0.72f);
            pressImage.enabled = alpha > 0.01f;
            return;
        }

        pressImage.enabled = false;
    }

    private void ResetVisuals()
    {
        if (hoverImage != null)
            hoverImage.color = Color.clear;

        if (pressImage != null)
        {
            pressImage.color = Color.clear;
            pressImage.enabled = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable)
            return;

        pressed = true;
        releaseTimer = ReleaseFlashSeconds;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pressed)
            releaseTimer = ReleaseFlashSeconds;
        pressed = false;
    }

}
