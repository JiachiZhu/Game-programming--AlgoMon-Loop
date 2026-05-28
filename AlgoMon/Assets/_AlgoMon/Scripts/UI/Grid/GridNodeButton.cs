/*
Script Audit:
- Purpose: Displays and forwards clicks for one node on TheGrid map.
- Attached GameObject: Runtime/generated UI Button object for a single GridNode.
- Main responsibilities: Bind a GridNode, update icon/text/ring colors, set interactability, and call the click callback.
- Important variables: typeLabel, detailLabel, stateLabel, ringImage, iconImage, button, background, node, clicked.
- Inputs: GridNode data, visual state values, sprites/colors, and player click events.
- Outputs or effects: Updates node UI and tells GridMapController which node was clicked.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Click a node button and verify GridMapController receives the correct GridNode.
*/
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime view for one route node on TheGrid map.
/// GridMapController owns the layout and state decisions; this class only
/// presents a node and forwards valid click attempts.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class GridNodeButton : MonoBehaviour
{
    [SerializeField] private Text typeLabel;
    [SerializeField] private Text detailLabel;
    [SerializeField] private Text stateLabel;
    [SerializeField] private Image ringImage;
    [SerializeField] private Image iconImage;

    private Button button;
    private Image background;
    private GridNode node;
    private Action<GridNode> clicked;

    public GridNode Node => node;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(GridNode gridNode, Action<GridNode> onClicked)
    {
        CacheReferences();

        node = gridNode;
        clicked = onClicked;

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

    public void SetVisual(
        GridNodeVisualState visualState,
        Color fillColor,
        Color outlineColor,
        Color textColor,
        Sprite iconSprite,
        Color iconColor,
        string stateText,
        bool interactable)
    {
        CacheReferences();

        if (background != null)
            background.color = fillColor;
        if (ringImage != null)
            ringImage.color = outlineColor;
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
            typeLabel.gameObject.SetActive(useTextFallback);
        if (detailLabel != null)
            detailLabel.gameObject.SetActive(false);

        SetTextColor(typeLabel, textColor);
        SetTextColor(detailLabel, textColor);
        SetTextColor(stateLabel, textColor);

        if (stateLabel != null)
        {
            stateLabel.text = stateText;
            stateLabel.gameObject.SetActive(!string.IsNullOrEmpty(stateText));
        }
    }

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
        if (ringImage == null)
            ringImage = FindImage("RingImage");
        if (iconImage == null)
            iconImage = FindImage("IconImage");
    }

    private Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private Image FindImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private void HandleClick()
    {
        clicked?.Invoke(node);
    }

    private static void SetTextColor(Text text, Color color)
    {
        if (text != null)
            text.color = color;
    }
}
