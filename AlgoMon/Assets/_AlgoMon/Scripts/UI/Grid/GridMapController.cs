/*
Script Audit:
- Purpose: Controls TheGrid scene UI and lets the player choose route nodes.
- Attached GameObject: TheGrid scene map/controller object, usually on the main Canvas or grid controller root.
- Main responsibilities: Ensure run state exists, draw nodes and connections, color node availability, handle node clicks, and send valid selections to GameManager.
- Important variables: canvas, mapRoot, connectionRoot, nodeRoot, nodeViews, nodePositions, manager, fallbackGenerationSettings, node sprites, palette colors.
- Inputs: GameManager.currentRunGraph, visited/current node data, button clicks, and debug run settings.
- Outputs or effects: Rebuilds the map UI, updates hints, selects nodes, publishes navigation events through GameManager flow, and can start debug runs in the editor.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Start a run, click available and locked nodes, confirm only valid routes advance and combat nodes enter TheArena.
*/
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Playable TheGrid scene controller. It visualizes the generated run DAG,
/// marks route availability, and commits valid node selections through
/// GameManager.TrySelectRunNode.
/// </summary>
[DisallowMultipleComponent]
public class GridMapController : MonoBehaviour
{
    private const string GridFontResourcePath = "Fonts/NicoBold-Regular";

    [Header("Scene References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform mapRoot;
    [SerializeField] private RectTransform connectionRoot;
    [SerializeField] private RectTransform nodeRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text seedText;
    [SerializeField] private Text hintText;
    [SerializeField] private Text legendText;
    [SerializeField] private Button newRunButton;

    [Header("Editor Debug")]
    [SerializeField] private bool beginRunIfMissing;
    [SerializeField] private bool showDebugNewRunButton;
    [SerializeField] private int debugSeed;
    [SerializeField] private GridGenerationSettings fallbackGenerationSettings = new GridGenerationSettings();

    [Header("Layout")]
    [SerializeField] private Vector2 nodeSize = new Vector2(56f, 56f);
    [SerializeField] private float horizontalPadding = 84f;
    [SerializeField] private float verticalPadding = 58f;
    [SerializeField] private float layerNodeSpacing = 132f;
    [SerializeField] private float connectionThickness = 1.35f;

    [Header("Sprites")]
    [SerializeField] private Sprite nodeSprite;
    [SerializeField] private Sprite nodeFillSprite;
    [SerializeField] private Sprite startIcon;
    [SerializeField] private Sprite combatIcon;
    [SerializeField] private Sprite hackerIcon;
    [SerializeField] private Sprite eliteIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite rebootIcon;
    [SerializeField] private Sprite bossIcon;

    [Header("Palette")]
    [SerializeField] private Color pageBackground = new Color(0.02f, 0.022f, 0.026f, 1f);
    [SerializeField] private Color panelBackground = new Color(0.135f, 0.145f, 0.15f, 1f);
    [SerializeField] private Color lineLocked = new Color(0.16f, 0.18f, 0.22f, 0.78f);
    [SerializeField] private Color lineAvailable = new Color(0.68f, 0.78f, 1f, 0.95f);
    [SerializeField] private Color lineVisited = new Color(0.45f, 0.56f, 0.86f, 0.82f);
    [SerializeField] private Color lockedFill = new Color(0.045f, 0.052f, 0.066f, 0.96f);
    [SerializeField] private Color currentFill = new Color(0.055f, 0.075f, 0.13f, 0.98f);
    [SerializeField] private Color availableFill = new Color(0.055f, 0.075f, 0.12f, 0.98f);
    [SerializeField] private Color visitedFill = new Color(0.105f, 0.155f, 0.31f, 1f);
    [SerializeField] private Color bossFill = new Color(0.12f, 0.066f, 0.07f, 0.97f);
    [SerializeField] private Color startFill = new Color(0.052f, 0.07f, 0.11f, 0.97f);
    [SerializeField] private Color textBright = new Color(0.93f, 0.98f, 1f, 1f);
    [SerializeField] private Color textDim = new Color(0.54f, 0.62f, 0.68f, 1f);
    [SerializeField] private Color accent = new Color(0.62f, 0.73f, 1f, 1f);
    [SerializeField] private Color warning = new Color(1f, 0.72f, 0.32f, 1f);

    private readonly Dictionary<string, GridNodeButton> nodeViews = new Dictionary<string, GridNodeButton>();
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private readonly Dictionary<string, Vector2> nodePositions = new Dictionary<string, Vector2>();
    private readonly List<Button> shopOfferButtons = new List<Button>();

    private GameManager manager;
    private Font defaultFont;
    private RectTransform shopPanel;
    private Text shopTitleText;
    private Text shopBalanceText;
    private Text shopBodyText;
    private Button shopRefreshButton;
    private Button shopCloseButton;
    private GridNode activeShopNode;

    private void Awake()
    {
        defaultFont = GridFont();
        EnsureSceneShell();
        ApplyResolvedTextStyles();
    }

    private void Start()
    {
        ConfigureDebugNewRunButton();

        EnsureRunState();
        RebuildMap();
    }

    private void OnDestroy()
    {
        if (newRunButton != null)
            newRunButton.onClick.RemoveListener(BeginNewRun);
    }

    public void RebuildMap()
    {
        RebuildMap(null);
    }

    [ContextMenu("Rebuild Map")]
    private void RebuildMapFromContextMenu()
    {
        RebuildMap();
    }

    private void RebuildMap(string overrideHint)
    {
        manager = ResolveManager();
        ClearSpawnedMapObjects();

        if (manager == null || manager.currentRunGraph == null)
        {
            SetHeader("No run data", "Open MainTerminal or start a debug run.");
            SetHint("TheGrid is waiting for GameManager.BeginRun().");
            return;
        }

        GridGraph graph = manager.currentRunGraph;
        BuildPositions(graph);
        BuildConnections(graph);
        BuildNodes(graph);
        RefreshNodeStates();
        SetHeader(BuildTerminalStatus(graph), BuildDepthStatus(graph));
        SetHint(!string.IsNullOrEmpty(overrideHint) ? overrideHint : BuildCommandHint());
    }

    public void BeginNewRun()
    {
        manager = ResolveManager();
        if (manager == null)
            return;

        int seed = NewSeed();
        manager.BeginRun(seed, fallbackGenerationSettings);
        HideShopPanel();
        RebuildMap();
    }

    private void EnsureRunState()
    {
        manager = ResolveManager();
        if (manager == null || !CanUseEditorDebugFeatures() || !beginRunIfMissing)
            return;

        if (!manager.IsRunActive || manager.currentRunGraph == null)
            manager.BeginRun(NewSeed(), fallbackGenerationSettings);
        else if (manager.visitedNodeIds == null || manager.visitedNodeIds.Count <= 1)
            manager.EnsureCurrentRunHasEarlyHacker();
    }

    private GameManager ResolveManager()
    {
        return GameManager.EnsureInstance();
    }

    private static bool CanUseEditorDebugFeatures()
    {
#if UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    private int NewSeed()
    {
        if (debugSeed != 0)
            return debugSeed;

        return (int)(DateTime.UtcNow.Ticks & int.MaxValue);
    }

    private void BuildPositions(GridGraph graph)
    {
        nodePositions.Clear();
        if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
            return;

        int maxLayer = 0;
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] != null && graph.nodes[i].layer > maxLayer)
                maxLayer = graph.nodes[i].layer;
        }

        Rect rect = mapRoot != null ? mapRoot.rect : new Rect(0f, 0f, 980f, 560f);
        float width = rect.width > 1f ? rect.width : 980f;
        float height = rect.height > 1f ? rect.height : 560f;
        float usableWidth = Mathf.Max(nodeSize.x, width - horizontalPadding * 2f);
        float usableHeight = Mathf.Max(nodeSize.y, height - verticalPadding * 2f);
        float yStep = maxLayer > 0 ? usableHeight / maxLayer : 0f;

        for (int layer = 0; layer <= maxLayer; layer++)
        {
            List<GridNode> layerNodes = graph.NodesInLayer(layer);
            float xStep = layerNodes.Count > 1
                ? Mathf.Min(layerNodeSpacing, usableWidth / (layerNodes.Count - 1))
                : 0f;
            float startX = -(layerNodes.Count - 1) * xStep * 0.5f;

            for (int index = 0; index < layerNodes.Count; index++)
            {
                GridNode node = layerNodes[index];
                float x = startX + xStep * index;
                float y = -usableHeight * 0.5f + yStep * layer;
                nodePositions[node.id] = new Vector2(x, y);
            }
        }
    }

    private void BuildConnections(GridGraph graph)
    {
        if (graph == null || graph.nodes == null || connectionRoot == null)
            return;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            GridNode source = graph.nodes[i];
            if (source == null || source.outgoingNodeIds == null)
                continue;

            for (int edgeIndex = 0; edgeIndex < source.outgoingNodeIds.Count; edgeIndex++)
            {
                string targetId = source.outgoingNodeIds[edgeIndex];
                GridNode target = graph.GetNode(targetId);
                if (target == null)
                    continue;

                CreateConnection(source, target);
            }
        }
    }

    private void BuildNodes(GridGraph graph)
    {
        if (graph == null || graph.nodes == null || nodeRoot == null)
            return;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            GridNode node = graph.nodes[i];
            if (node == null || !nodePositions.TryGetValue(node.id, out Vector2 position))
                continue;

            GridNodeButton view = CreateNodeView(node, position);
            nodeViews[node.id] = view;
        }
    }

    private void RefreshNodeStates()
    {
        foreach (KeyValuePair<string, GridNodeButton> entry in nodeViews)
        {
            GridNode node = entry.Value.Node;
            GridNodeVisualState state = StateFor(node);
            bool isVisited = manager != null && node != null && manager.IsNodeVisited(node.id);
            Color fill = FillFor(node, state, isVisited);
            Color outline = OutlineFor(node, state, isVisited);
            bool interactable = state == GridNodeVisualState.Available;
            Color textColor = state == GridNodeVisualState.Locked ? textDim : textBright;
            Sprite iconSprite = IconFor(node);
            Color iconColor = IconColorFor(node, state);
            string detailLabel = DetailLabelFor(node);
            Color detailColor = DetailColorFor(node, state);

            entry.Value.SetVisual(
                state,
                fill,
                outline,
                textColor,
                iconSprite,
                iconColor,
                LabelFor(node, state),
                detailLabel,
                detailColor,
                interactable);
        }

        if (manager != null)
            SetHeader(BuildTerminalStatus(manager.currentRunGraph), BuildDepthStatus(manager.currentRunGraph));
        RefreshLegend();
    }

    private GridNodeVisualState StateFor(GridNode node)
    {
        if (manager == null || node == null)
            return GridNodeVisualState.Locked;
        if (node.id == manager.currentNodeId)
            return GridNodeVisualState.Current;
        if (manager.IsNodeAvailable(node.id))
            return GridNodeVisualState.Available;
        if (manager.IsNodeVisited(node.id))
            return GridNodeVisualState.Visited;
        return GridNodeVisualState.Locked;
    }

    private Color FillFor(GridNode node, GridNodeVisualState state, bool isVisited)
    {
        switch (state)
        {
            case GridNodeVisualState.Current:
                return currentFill;
            case GridNodeVisualState.Visited:
                return visitedFill;
            case GridNodeVisualState.Locked:
                return LockedFillFor(node);
        }

        if (isVisited)
            return visitedFill;
        if (node != null && node.nodeType == NodeType.Boss)
            return bossFill;
        if (node != null && node.nodeType == NodeType.Start)
            return startFill;
        if (state == GridNodeVisualState.Available)
            return availableFill;
        return lockedFill;
    }

    private Color LockedFillFor(GridNode node)
    {
        if (node == null)
            return lockedFill;
        if (node.nodeType == NodeType.Boss)
            return Color.Lerp(lockedFill, bossFill, 0.42f);
        if (node.nodeType == NodeType.Start)
            return Color.Lerp(lockedFill, startFill, 0.45f);
        return lockedFill;
    }

    private Color OutlineFor(GridNode node, GridNodeVisualState state, bool isVisited)
    {
        Color nodeAccent = NodeAccentFor(node);

        if (state == GridNodeVisualState.Locked)
            return Color.Lerp(lineLocked, nodeAccent, node != null && node.nodeType == NodeType.Boss ? 0.48f : 0.26f);
        if (state == GridNodeVisualState.Visited || isVisited)
            return Color.Lerp(lineVisited, nodeAccent, 0.24f);

        switch (state)
        {
            case GridNodeVisualState.Current:
            case GridNodeVisualState.Available:
                return nodeAccent;
            case GridNodeVisualState.Visited:
                return lineVisited;
            default:
                return isVisited ? lineVisited : lineLocked;
        }
    }

    private Color IconColorFor(GridNode node, GridNodeVisualState state)
    {
        Color nodeAccent = NodeAccentFor(node);
        if (state == GridNodeVisualState.Locked)
            return new Color(nodeAccent.r, nodeAccent.g, nodeAccent.b, 0.48f);
        if (node != null && node.nodeType == NodeType.Boss)
            return new Color(1f, 0.82f, 0.78f, 1f);
        if (node != null && node.nodeType == NodeType.Hacker)
            return new Color(0.42f, 1f, 0.78f, 1f);
        if (node != null && (node.nodeType == NodeType.Shop || node.nodeType == NodeType.Reboot))
            return warning;
        if (state == GridNodeVisualState.Visited)
            return lineVisited;
        if (state == GridNodeVisualState.Current)
            return textBright;
        return nodeAccent;
    }

    private Color DetailColorFor(GridNode node, GridNodeVisualState state)
    {
        if (state == GridNodeVisualState.Locked)
            return new Color(textDim.r, textDim.g, textDim.b, 0.86f);
        return NodeAccentFor(node);
    }

    private Color NodeAccentFor(GridNode node)
    {
        if (node == null)
            return accent;

        if (node.nodeType == NodeType.Boss)
            return new Color(1f, 0.70f, 0.66f, 1f);
        if (node.nodeType == NodeType.Hacker)
            return new Color(0.42f, 1f, 0.78f, 1f);
        if (node.nodeType == NodeType.Elite)
            return new Color(1f, 0.55f, 0.36f, 1f);
        if (node.nodeType == NodeType.Shop || node.nodeType == NodeType.Reboot)
            return warning;
        if (node.dangerRating >= 4)
            return new Color(1f, 0.62f, 0.42f, 1f);
        return accent;
    }

    private Sprite IconFor(GridNode node)
    {
        if (node == null)
            return combatIcon;

        switch (node.nodeType)
        {
            case NodeType.Start:
                return startIcon;
            case NodeType.Combat:
                return combatIcon;
            case NodeType.Hacker:
                return hackerIcon;
            case NodeType.Elite:
                return eliteIcon;
            case NodeType.Shop:
                return shopIcon;
            case NodeType.Reboot:
                return null;
            case NodeType.Boss:
                return bossIcon;
            default:
                return combatIcon;
        }
    }

    private string LabelFor(GridNode node, GridNodeVisualState state)
    {
        if (node != null && node.nodeType == NodeType.Boss && state != GridNodeVisualState.Current)
            return "TARGET";

        switch (state)
        {
            case GridNodeVisualState.Current:
                return "HERE";
            case GridNodeVisualState.Available:
                return "NEXT";
            case GridNodeVisualState.Visited:
                return "DONE";
            default:
                return string.Empty;
        }
    }

    private string DetailLabelFor(GridNode node)
    {
        if (node == null)
            return string.Empty;

        string label = ShortTypeLabelFor(node.nodeType);
        if (!ThreatTierRules.IsEncounterNode(node.nodeType))
            return label;

        int danger = Mathf.Clamp(node.dangerRating, 1, ThreatTierRules.MaxTier);
        return $"{label} D{danger}";
    }

    private static string ShortTypeLabelFor(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Start:
                return "START";
            case NodeType.Combat:
                return "WILD";
            case NodeType.Hacker:
                return "HACK";
            case NodeType.Elite:
                return "ELITE";
            case NodeType.Shop:
                return "SHOP";
            case NodeType.Reboot:
                return "REBOOT";
            case NodeType.Boss:
                return "BOSS";
            default:
                return nodeType.ToString().ToUpperInvariant();
        }
    }

    private GridNodeButton CreateNodeView(GridNode node, Vector2 anchoredPosition)
    {
        GameObject nodeObject = CreateRectObject($"Node_{node.id}", nodeRoot);
        RectTransform rect = nodeObject.GetComponent<RectTransform>();
        rect.sizeDelta = nodeSize;
        rect.anchoredPosition = anchoredPosition;

        Image image = nodeObject.AddComponent<Image>();
        if (nodeFillSprite != null)
        {
            image.sprite = nodeFillSprite;
            image.preserveAspect = true;
        }
        else if (nodeSprite != null)
        {
            image.sprite = nodeSprite;
            image.preserveAspect = true;
        }
        image.color = lockedFill;

        Button button = nodeObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(Color.white, accent, 0.35f);
        colors.pressedColor = Color.Lerp(Color.white, accent, 0.55f);
        colors.selectedColor = Color.Lerp(Color.white, accent, 0.35f);
        colors.disabledColor = Color.white;
        button.colors = colors;

        Image ringImage = CreateImage("RingImage", rect, nodeSprite);
        ringImage.rectTransform.anchorMin = Vector2.zero;
        ringImage.rectTransform.anchorMax = Vector2.one;
        ringImage.rectTransform.offsetMin = Vector2.zero;
        ringImage.rectTransform.offsetMax = Vector2.zero;
        ringImage.raycastTarget = false;
        ringImage.color = lineLocked;

        Image iconImage = CreateImage("IconImage", rect, null);
        iconImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        iconImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        iconImage.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        iconImage.rectTransform.sizeDelta = new Vector2(23f, 23f);
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;

        Text typeLabel = CreateText("TypeLabel", rect, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        typeLabel.rectTransform.anchorMin = Vector2.zero;
        typeLabel.rectTransform.anchorMax = Vector2.one;
        typeLabel.rectTransform.offsetMin = new Vector2(8f, 8f);
        typeLabel.rectTransform.offsetMax = new Vector2(-8f, -8f);

        Text detailLabel = CreateText("DetailLabel", rect, 9, FontStyle.Bold, TextAnchor.LowerCenter);
        detailLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        detailLabel.rectTransform.anchorMax = new Vector2(1f, 0.42f);
        detailLabel.rectTransform.offsetMin = new Vector2(3f, 2f);
        detailLabel.rectTransform.offsetMax = new Vector2(-3f, 0f);
        detailLabel.resizeTextForBestFit = true;
        detailLabel.resizeTextMinSize = 8;
        detailLabel.resizeTextMaxSize = 9;
        detailLabel.gameObject.SetActive(false);

        Text stateLabel = CreateText("StateLabel", rect, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
        stateLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        stateLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        stateLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        stateLabel.rectTransform.anchoredPosition = new Vector2(0f, 7f);
        stateLabel.rectTransform.sizeDelta = new Vector2(82f, 18f);

        GridNodeButton view = nodeObject.AddComponent<GridNodeButton>();
        view.Bind(node, HandleNodeClicked, HandleNodePreviewed);
        spawnedObjects.Add(nodeObject);
        return view;
    }

    private void CreateConnection(GridNode source, GridNode target)
    {
        if (!nodePositions.TryGetValue(source.id, out Vector2 start))
            return;
        if (!nodePositions.TryGetValue(target.id, out Vector2 end))
            return;

        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f)
            return;

        GameObject lineObject = CreateRectObject($"Connection_{source.id}_{target.id}", connectionRoot);
        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(length, connectionThickness);
        rect.anchoredPosition = start + delta * 0.5f;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        Image image = lineObject.AddComponent<Image>();
        image.color = ConnectionColor(source, target);
        image.raycastTarget = false;

        spawnedObjects.Add(lineObject);
    }

    private Color ConnectionColor(GridNode source, GridNode target)
    {
        if (manager == null || source == null || target == null)
            return lineLocked;

        if (source.id == manager.currentNodeId && manager.IsNodeAvailable(target.id))
            return lineAvailable;
        if (manager.IsNodeVisited(source.id) && manager.IsNodeVisited(target.id))
            return lineVisited;
        return lineLocked;
    }

    private void HandleNodeClicked(GridNode node)
    {
        if (node == null)
            return;

        manager = ResolveManager();
        if (manager == null)
        {
            SetHint("No GameManager is available.");
            return;
        }

        string previousNodeId = manager.currentNodeId;
        GridNode previousNode = manager.currentRunGraph != null ? manager.currentRunGraph.GetNode(previousNodeId) : null;
        bool wasVisited = manager.IsNodeVisited(node.id);
        if (!manager.TrySelectRunNode(node.id))
        {
            SetHint($"{node.id} is locked from the current node.");
            RefreshNodeStates();
            return;
        }

        bool returnedToStart = previousNode != null &&
                               previousNode.nodeType == NodeType.Reboot &&
                               manager.currentRunGraph != null &&
                               node.id == manager.currentRunGraph.startNodeId;

        RebuildMap(BuildCommandHint(node, returnedToStart));

        bool opensShop = node.nodeType == NodeType.Shop;
        if (!opensShop)
            HideShopPanel();

        EventBus.Publish(new NodeSelectedEvent
        {
            NodeId = node.id,
            Type = node.nodeType,
            Node = node,
            WasVisited = wasVisited,
            IsFirstVisit = !wasVisited,
            ReturnedToStart = returnedToStart
        });

        if (opensShop)
            ShowShopPanel(node);
    }

    private void HandleNodePreviewed(GridNode node)
    {
        if (node == null)
        {
            SetHint(BuildCommandHint());
            return;
        }

        SetHint(BuildPreviewHint(node));
    }

    private void ShowShopPanel(GridNode shopNode)
    {
        activeShopNode = shopNode;
        EnsureShopPanel();
        if (shopPanel == null)
            return;

        manager = ResolveManager();
        if (manager != null)
            manager.EnsureShopOffersForNode(shopNode != null ? shopNode.id : string.Empty);

        shopPanel.SetAsLastSibling();
        shopPanel.gameObject.SetActive(true);
        RefreshShopPanel();
        SetHint(BuildShopHint());
    }

    private void HideShopPanel()
    {
        activeShopNode = null;
        if (shopPanel != null)
            shopPanel.gameObject.SetActive(false);
    }

    private void EnsureShopPanel()
    {
        if (shopPanel != null)
            return;

        RectTransform parent = mapRoot != null ? mapRoot.parent as RectTransform : null;
        if (parent == null && canvas != null)
            parent = canvas.GetComponent<RectTransform>();
        if (parent == null)
            return;

        shopPanel = GetOrCreateRect("ShopPanel", parent);
        shopPanel.anchorMin = new Vector2(0.58f, 0.17f);
        shopPanel.anchorMax = new Vector2(0.96f, 0.80f);
        shopPanel.offsetMin = Vector2.zero;
        shopPanel.offsetMax = Vector2.zero;

        Image panelImage = shopPanel.GetComponent<Image>() ?? shopPanel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.047f, 0.056f, 0.98f);

        Outline outline = shopPanel.GetComponent<Outline>() ?? shopPanel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(warning.r, warning.g, warning.b, 0.56f);
        outline.effectDistance = new Vector2(2f, -2f);

        shopTitleText = GetOrCreateText("Title", shopPanel, 21, FontStyle.Bold, TextAnchor.MiddleLeft);
        shopTitleText.rectTransform.anchorMin = new Vector2(0.06f, 0.88f);
        shopTitleText.rectTransform.anchorMax = new Vector2(0.62f, 0.98f);
        shopTitleText.rectTransform.offsetMin = Vector2.zero;
        shopTitleText.rectTransform.offsetMax = Vector2.zero;
        shopTitleText.text = "Compute Shop";

        shopBalanceText = GetOrCreateText("Balance", shopPanel, 13, FontStyle.Bold, TextAnchor.MiddleRight);
        shopBalanceText.rectTransform.anchorMin = new Vector2(0.50f, 0.88f);
        shopBalanceText.rectTransform.anchorMax = new Vector2(0.94f, 0.98f);
        shopBalanceText.rectTransform.offsetMin = Vector2.zero;
        shopBalanceText.rectTransform.offsetMax = Vector2.zero;
        shopBalanceText.color = warning;

        shopBodyText = GetOrCreateText("Body", shopPanel, 12, FontStyle.Normal, TextAnchor.UpperLeft);
        shopBodyText.rectTransform.anchorMin = new Vector2(0.06f, 0.43f);
        shopBodyText.rectTransform.anchorMax = new Vector2(0.94f, 0.86f);
        shopBodyText.rectTransform.offsetMin = Vector2.zero;
        shopBodyText.rectTransform.offsetMax = Vector2.zero;
        shopBodyText.verticalOverflow = VerticalWrapMode.Truncate;

        shopOfferButtons.Clear();
        for (int i = 0; i < RunShopCatalog.OfferSlots; i++)
        {
            Button button = GetOrCreateButton($"Offer{i + 1}", shopPanel, string.Empty);
            RectTransform rect = button.GetComponent<RectTransform>();
            float top = 0.37f - i * 0.082f;
            rect.anchorMin = new Vector2(0.06f, top - 0.062f);
            rect.anchorMax = new Vector2(0.94f, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            shopOfferButtons.Add(button);
        }

        shopRefreshButton = GetOrCreateButton("RefreshButton", shopPanel, "REFRESH");
        RectTransform refreshRect = shopRefreshButton.GetComponent<RectTransform>();
        refreshRect.anchorMin = new Vector2(0.06f, 0.03f);
        refreshRect.anchorMax = new Vector2(0.48f, 0.105f);
        refreshRect.offsetMin = Vector2.zero;
        refreshRect.offsetMax = Vector2.zero;
        shopRefreshButton.onClick.RemoveAllListeners();
        shopRefreshButton.onClick.AddListener(TryRefreshShopOffers);

        shopCloseButton = GetOrCreateButton("CloseButton", shopPanel, "CLOSE");
        RectTransform closeRect = shopCloseButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.62f, 0.03f);
        closeRect.anchorMax = new Vector2(0.94f, 0.095f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;
        shopCloseButton.onClick.RemoveAllListeners();
        shopCloseButton.onClick.AddListener(() =>
        {
            HideShopPanel();
            SetHint(BuildCommandHint());
        });

        shopPanel.gameObject.SetActive(false);
    }

    private void RefreshShopPanel()
    {
        if (shopPanel == null || !shopPanel.gameObject.activeSelf)
            return;

        manager = ResolveManager();
        if (manager == null)
            return;
        if (activeShopNode != null)
            manager.EnsureShopOffersForNode(activeShopNode.id);

        if (shopTitleText != null)
            shopTitleText.text = activeShopNode != null ? "Compute Shop" : "Shop Offline";
        if (shopBalanceText != null)
            shopBalanceText.text = $"CMP {manager.computeBalance:000} | REROLL {manager.CurrentShopRefreshCost}";
        if (shopBodyText != null)
            shopBodyText.text = BuildShopBody();

        List<RunShopOffer> offers = manager.CurrentShopOffers();
        for (int i = 0; i < shopOfferButtons.Count; i++)
        {
            Button button = shopOfferButtons[i];
            if (button == null)
                continue;

            bool hasOffer = i < offers.Count;
            button.gameObject.SetActive(hasOffer);
            if (!hasOffer)
                continue;

            RunShopOffer offer = offers[i];
            string reason;
            bool canPurchase = manager.CanPurchaseShopOffer(offer, out reason);
            button.interactable = canPurchase;
            SetButtonText(button, $"{offer.ShortLabel}  {offer.ComputeCost} CMP");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => TryPurchaseShopOffer(offer));
        }

        if (shopRefreshButton != null)
        {
            string reason;
            bool canRefresh = manager.CanRefreshShopOffers(out reason);
            shopRefreshButton.interactable = canRefresh;
            SetButtonText(shopRefreshButton, $"REFRESH {manager.CurrentShopRefreshCost} CMP");
        }
    }

    private string BuildShopBody()
    {
        var builder = new StringBuilder();
        builder.AppendLine("ACTIVE RUN BUFFS");
        builder.AppendLine(manager != null ? manager.CurrentRunBuffSummary() : "No run data.");
        builder.AppendLine();
        builder.AppendLine($"OFFERS: choose 1 of {RunShopCatalog.OfferSlots}. Refresh cost doubles.");

        List<RunShopOffer> offers = manager != null ? manager.CurrentShopOffers() : new List<RunShopOffer>();
        for (int i = 0; i < offers.Count; i++)
        {
            RunShopOffer offer = offers[i];
            string state = string.Empty;
            if (manager != null && manager.HasRunBuff(offer.BuffType))
            {
                state = " [ACTIVE]";
            }
            else if (manager != null && !manager.CanPurchaseShopOffer(offer, out string reason))
            {
                state = $" [{reason}]";
            }
            else if (offer.HighRisk)
            {
                state = " [HIGH RISK]";
            }

            builder.AppendLine($"{offer.DisplayName} - {offer.ComputeCost} CMP{state}");
            builder.AppendLine(offer.Description);
        }

        if (manager != null)
            builder.AppendLine($"Refresh: {manager.CurrentShopRefreshCost} CMP now; next refresh doubles.");

        return builder.ToString();
    }

    private void TryPurchaseShopOffer(RunShopOffer offer)
    {
        manager = ResolveManager();
        if (manager == null || offer == null)
            return;

        string message;
        if (manager.TryPurchaseShopOffer(offer, out message))
            SetHint($"> {message} Remaining compute: {manager.computeBalance:000}.");
        else
            SetHint($"> Purchase rejected: {message}");

        RefreshShopPanel();
        RefreshNodeStates();
    }

    private void TryRefreshShopOffers()
    {
        manager = ResolveManager();
        if (manager == null)
            return;

        string message;
        if (manager.TryRefreshShopOffers(out message))
            SetHint($"> {message} Remaining compute: {manager.computeBalance:000}.");
        else
            SetHint($"> Refresh rejected: {message}");

        RefreshShopPanel();
        RefreshNodeStates();
    }

    private string BuildShopHint()
    {
        if (manager == null)
            return "> Shop node online.";

        return "> Shop node online. Three offers loaded; refresh costs compute and doubles each time.";
    }

    private static void SetButtonText(Button button, string label)
    {
        Text text = button != null ? button.GetComponentInChildren<Text>() : null;
        if (text != null)
            text.text = label;
    }

    private void ConfigureDebugNewRunButton()
    {
        if (newRunButton == null)
            return;

        bool showButton = CanUseEditorDebugFeatures() && showDebugNewRunButton;
        newRunButton.gameObject.SetActive(showButton);
        newRunButton.onClick.RemoveListener(BeginNewRun);
        if (showButton)
            newRunButton.onClick.AddListener(BeginNewRun);
    }

    private void ClearSpawnedMapObjects()
    {
        nodeViews.Clear();
        nodePositions.Clear();

        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
            {
                spawnedObjects[i].SetActive(false);
                Destroy(spawnedObjects[i]);
            }
        }
        spawnedObjects.Clear();
    }

    private void EnsureSceneShell()
    {
        EnsureEventSystem();

        if (TryResolveSceneReferences())
            return;

        if (canvas == null)
            canvas = CreateCanvas();

        BuildDefaultSceneShell();
    }

    private bool TryResolveSceneReferences()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            return false;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform frame = FindRect(canvasRect, "GridFrame");

        if (frame != null)
        {
            titleText = titleText != null ? titleText : FindText(frame, "Title");
            seedText = seedText != null ? seedText : FindText(frame, "SeedStatus");
            hintText = hintText != null ? hintText : FindText(frame, "Hint");
            legendText = legendText != null ? legendText : FindText(frame, "Legend");
            newRunButton = newRunButton != null ? newRunButton : FindButton(frame, "NewRunButton");

            mapRoot = mapRoot != null ? mapRoot : FindRect(frame, "MapRoot");
            if (mapRoot != null)
            {
                connectionRoot = connectionRoot != null ? connectionRoot : FindRect(mapRoot, "Connections");
                nodeRoot = nodeRoot != null ? nodeRoot : FindRect(mapRoot, "Nodes");
            }
        }

        return HasSceneReferences();
    }

    private bool HasSceneReferences()
    {
        return canvas != null &&
               mapRoot != null &&
               connectionRoot != null &&
               nodeRoot != null &&
               titleText != null &&
               seedText != null &&
               hintText != null &&
               legendText != null;
    }

    private void BuildDefaultSceneShell()
    {
        ConfigureCanvas(canvas);
        EnsureCanvasBackground();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform frame = GetOrCreateRect("GridFrame", canvasRect);
        frame.anchorMin = new Vector2(0.04f, 0.06f);
        frame.anchorMax = new Vector2(0.96f, 0.94f);
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;
        Image frameImage = frame.GetComponent<Image>() ?? frame.gameObject.AddComponent<Image>();
        frameImage.color = panelBackground;

        titleText = titleText != null ? titleText : GetOrCreateText("Title", frame, 26, FontStyle.Bold, TextAnchor.MiddleLeft);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0.55f, 1f);
        titleText.rectTransform.pivot = new Vector2(0f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(26f, -18f);
        titleText.rectTransform.sizeDelta = new Vector2(520f, 40f);
        titleText.text = "Data Node Terminal v1.0.4";

        seedText = seedText != null ? seedText : GetOrCreateText("SeedStatus", frame, 14, FontStyle.Normal, TextAnchor.MiddleRight);
        seedText.rectTransform.anchorMin = new Vector2(1f, 1f);
        seedText.rectTransform.anchorMax = new Vector2(1f, 1f);
        seedText.rectTransform.pivot = new Vector2(1f, 1f);
        seedText.rectTransform.anchoredPosition = new Vector2(-88f, -26f);
        seedText.rectTransform.sizeDelta = new Vector2(560f, 46f);
        seedText.alignment = TextAnchor.UpperRight;

        mapRoot = mapRoot != null ? mapRoot : GetOrCreateRect("MapRoot", frame);
        mapRoot.anchorMin = new Vector2(0.04f, 0.16f);
        mapRoot.anchorMax = new Vector2(0.96f, 0.86f);
        mapRoot.offsetMin = Vector2.zero;
        mapRoot.offsetMax = Vector2.zero;

        connectionRoot = connectionRoot != null ? connectionRoot : GetOrCreateRect("Connections", mapRoot);
        connectionRoot.anchorMin = Vector2.zero;
        connectionRoot.anchorMax = Vector2.one;
        connectionRoot.offsetMin = Vector2.zero;
        connectionRoot.offsetMax = Vector2.zero;

        nodeRoot = nodeRoot != null ? nodeRoot : GetOrCreateRect("Nodes", mapRoot);
        nodeRoot.anchorMin = Vector2.zero;
        nodeRoot.anchorMax = Vector2.one;
        nodeRoot.offsetMin = Vector2.zero;
        nodeRoot.offsetMax = Vector2.zero;

        hintText = hintText != null ? hintText : GetOrCreateText("Hint", frame, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
        hintText.rectTransform.anchorMin = new Vector2(0.04f, 0.06f);
        hintText.rectTransform.anchorMax = new Vector2(0.62f, 0.13f);
        hintText.rectTransform.offsetMin = Vector2.zero;
        hintText.rectTransform.offsetMax = Vector2.zero;

        legendText = legendText != null ? legendText : GetOrCreateText("Legend", frame, 13, FontStyle.Normal, TextAnchor.MiddleRight);
        legendText.rectTransform.anchorMin = new Vector2(0.58f, 0.06f);
        legendText.rectTransform.anchorMax = new Vector2(0.96f, 0.13f);
        legendText.rectTransform.offsetMin = Vector2.zero;
        legendText.rectTransform.offsetMax = Vector2.zero;
        legendText.text = "Current / Available / Visited / Locked";

        newRunButton = newRunButton != null ? newRunButton : GetOrCreateButton("NewRunButton", frame, "NEW RUN");
        RectTransform buttonRect = newRunButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-26f, -64f);
        buttonRect.sizeDelta = new Vector2(104f, 30f);

        Canvas.ForceUpdateCanvases();
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas_Grid", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        ConfigureCanvas(createdCanvas);
        return createdCanvas;
    }

    private static void ConfigureCanvas(Canvas targetCanvas)
    {
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        RectTransform rect = targetCanvas.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1280f, 720f);
        rect.localScale = Vector3.one;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        if (targetCanvas.GetComponent<GraphicRaycaster>() == null)
            targetCanvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void EnsureCanvasBackground()
    {
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform background = GetOrCreateRect("Background", canvasRect);
        background.SetAsFirstSibling();
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;

        Image image = background.GetComponent<Image>() ?? background.gameObject.AddComponent<Image>();
        image.color = pageBackground;
        image.raycastTarget = false;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(null);
    }

    private static RectTransform FindRect(RectTransform parent, string objectName)
    {
        Transform child = parent != null ? parent.Find(objectName) : null;
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private static Text FindText(RectTransform parent, string objectName)
    {
        Transform child = parent != null ? parent.Find(objectName) : null;
        return child != null ? child.GetComponent<Text>() : null;
    }

    private static Button FindButton(RectTransform parent, string objectName)
    {
        Transform child = parent != null ? parent.Find(objectName) : null;
        return child != null ? child.GetComponent<Button>() : null;
    }

    private RectTransform GetOrCreateRect(string objectName, RectTransform parent)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        if (existing != null)
            return existing.GetComponent<RectTransform>();

        GameObject rectObject = CreateRectObject(objectName, parent);
        return rectObject.GetComponent<RectTransform>();
    }

    private GameObject CreateRectObject(string objectName, RectTransform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = Vector2.zero;
        return rectObject;
    }

    private Image CreateImage(string objectName, RectTransform parent, Sprite sprite)
    {
        GameObject imageObject = CreateRectObject(objectName, parent);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        return image;
    }

    private Text GetOrCreateText(
        string objectName,
        RectTransform parent,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        RectTransform rect = GetOrCreateRect(objectName, parent);
        Text text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
        ConfigureText(text, fontSize, fontStyle, alignment);
        return text;
    }

    private Text CreateText(
        string objectName,
        RectTransform parent,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        GameObject textObject = CreateRectObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        ConfigureText(text, fontSize, fontStyle, alignment);
        return text;
    }

    private void ConfigureText(Text text, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        text.font = defaultFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = textBright;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private Font GridFont()
    {
        Font font = Resources.Load<Font>(GridFontResourcePath);
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    private Button GetOrCreateButton(string objectName, RectTransform parent, string label)
    {
        RectTransform rect = GetOrCreateRect(objectName, parent);
        Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.065f, 0.078f, 0.095f, 1f);

        Button button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(Color.white, accent, 0.24f);
        colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
        colors.selectedColor = Color.Lerp(Color.white, accent, 0.24f);
        colors.disabledColor = new Color(0.65f, 0.68f, 0.72f, 1f);
        button.colors = colors;

        Outline outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.42f);
        outline.effectDistance = new Vector2(1f, -1f);

        Text text = GetOrCreateText("Text", rect, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.text = label;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        return button;
    }

    private void SetHeader(string seedLine, string currentLine)
    {
        if (titleText != null)
            titleText.text = "Data Node Terminal v1.0.4";
        if (seedText != null)
            seedText.text = $"{seedLine}\n{currentLine}";
    }

    private void SetHint(string message)
    {
        if (hintText != null)
            hintText.text = message;
    }

    private void RefreshLegend()
    {
        if (legendText != null)
            legendText.text = "HERE / NEXT / DONE / dim locked | D1-D5 danger | TARGET = boss";
    }

    private void ApplyResolvedTextStyles()
    {
        ApplyTextFont(titleText, 26);
        ApplyTextFont(seedText, 14);
        ApplyTextFont(hintText, 16);
        ApplyTextFont(legendText, 14);
    }

    private void ApplyTextFont(Text text, int minimumSize)
    {
        if (text == null)
            return;

        text.font = defaultFont;
        text.fontSize = Mathf.Max(text.fontSize, minimumSize);
        text.resizeTextForBestFit = false;
    }

    private string BuildTerminalStatus(GridGraph graph)
    {
        return "COMPUTE BANK      MATERIAL PAYLOAD    CURRENT DEPTH";
    }

    private string BuildDepthStatus(GridGraph graph)
    {
        int currentLayer = 0;
        int maxLayer = 0;
        if (graph != null && graph.nodes != null)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                GridNode node = graph.nodes[i];
                if (node == null)
                    continue;
                if (node.layer > maxLayer)
                    maxLayer = node.layer;
                if (manager != null && node.id == manager.currentNodeId)
                    currentLayer = node.layer;
            }
        }

        int payloadCount = manager != null && manager.payload != null ? manager.payload.Count : 0;
        int compute = manager != null ? manager.computeBalance : 0;
        return $"{compute:0000}              {payloadCount}                   {currentLayer + 1}/{Mathf.Max(1, maxLayer + 1)}";
    }

    private string BuildCommandHint(GridNode selectedNode = null, bool returnedToStart = false)
    {
        if (selectedNode != null)
        {
            if (returnedToStart)
                return "> START | Route cursor returned to terminal entry. Choose a new forward route.";
            if (selectedNode.nodeType == NodeType.Reboot)
                return "> REBOOT | Route reset node. Choose a forward route or return to terminal entry.";
            if (selectedNode.nodeType == NodeType.Shop)
                return "> SHOP | Spend compute on current-run buffs. Rewards: damage, CP, shield, EXP, or high-risk trade-off offers.";

            return BuildSelectedNodeDetail(selectedNode);
        }

        if (manager == null || string.IsNullOrEmpty(manager.currentNodeId))
            return "> System initialized. Waiting for input...";

        return $"> Located [{manager.currentNodeId}]. Select a NEXT node. Read node label for type and D1-D5 danger.";
    }

    private string BuildPreviewHint(GridNode node)
    {
        if (node == null)
            return BuildCommandHint();

        string availability = NodeAvailabilityLabel(node);
        return $"> PREVIEW {availability} | {BuildSelectedNodeDetail(node).TrimStart('>', ' ')}";
    }

    private string NodeAvailabilityLabel(GridNode node)
    {
        switch (StateFor(node))
        {
            case GridNodeVisualState.Current:
                return "HERE";
            case GridNodeVisualState.Available:
                return "NEXT";
            case GridNodeVisualState.Visited:
                return "DONE";
            default:
                return "LOCKED";
        }
    }

    private string BuildSelectedNodeDetail(GridNode node)
    {
        if (node == null)
            return "> Route data unavailable.";

        string typeLabel = node.nodeType.ToGridLabel().ToUpperInvariant();
        string encounter = EncounterIdentityFor(node);
        string risk = RiskSummaryFor(node);
        string reward = RewardIdentityFor(node.nodeType);
        return $"> {typeLabel} | {encounter} | {risk} | Rewards: {reward}.";
    }

    private string EncounterIdentityFor(GridNode node)
    {
        if (node == null)
            return "Unknown route";

        switch (node.nodeType)
        {
            case NodeType.Combat:
                return $"Wild AlgoMon encounter, Lv {DisplayEncounterLevel(node)}";
            case NodeType.Hacker:
                return $"Hacker party pressure, Lv {DisplayEncounterLevel(node)}";
            case NodeType.Elite:
                return $"Elite encounter, Lv {DisplayEncounterLevel(node)}";
            case NodeType.Boss:
                return $"Boss target, evolved foe, Lv {DisplayEncounterLevel(node)}";
            case NodeType.Shop:
                return "Compute shop";
            case NodeType.Reboot:
                return "Route reset";
            case NodeType.Start:
                return "Terminal entry";
            default:
                return node.nodeType.ToGridLabel();
        }
    }

    private static string DisplayEncounterLevel(GridNode node)
    {
        if (node == null || node.encounterLevel <= 0)
            return "??";

        return node.encounterLevel.ToString("00");
    }

    private static string RiskSummaryFor(GridNode node)
    {
        if (node == null || !ThreatTierRules.IsEncounterNode(node.nodeType))
            return "Safe utility node";

        int danger = Mathf.Clamp(node.dangerRating, 1, ThreatTierRules.MaxTier);
        return $"Danger D{danger} ({DepthLabelFor(node.depthBand)})";
    }

    private static string DepthLabelFor(EncounterDepthBand depthBand)
    {
        switch (depthBand)
        {
            case EncounterDepthBand.Early:
                return "early";
            case EncounterDepthBand.Middle:
                return "mid";
            case EncounterDepthBand.Late:
                return "late";
            case EncounterDepthBand.Boss:
                return "run target";
            default:
                return "utility";
        }
    }

    private static string RewardIdentityFor(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Combat:
                return "small EXP, compute, base Payload data";
            case NodeType.Hacker:
                return "higher EXP and compute, no Payload capture";
            case NodeType.Elite:
                return "above-average EXP and compute";
            case NodeType.Boss:
                return "high EXP, high-quality data, evolution data";
            case NodeType.Shop:
                return "spend compute for run buffs";
            case NodeType.Reboot:
                return "route flexibility, no combat reward";
            case NodeType.Start:
                return "begin or restart route planning";
            default:
                return "route progress";
        }
    }
}
