/*
Script Audit:
- Purpose: Controls TheGrid scene UI and lets the player choose route nodes.
- Attached GameObject: TheGrid scene map/controller object, usually on the main Canvas or grid controller root.
- Main responsibilities: Ensure run state exists, draw nodes and connections, color node availability, handle node clicks, and send valid selections to GameManager.
- Important variables: canvas, mapRoot, connectionRoot, nodeRoot, nodeViews, nodePositions, manager, fallbackGenerationSettings, node sprites, palette colors.
- Inputs: GameManager.currentRunGraph, visited/current node data, button clicks, and debug run settings.
- Outputs or effects: Rebuilds the map UI, updates hints, selects nodes, publishes navigation events through GameManager flow, and can start debug runs in the editor.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Start a run, click nextAvailable nodes, confirm only valid routes advance and combat nodes enter TheArena.
*/
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Playable TheGrid scene controller. It visualizes the generated run DAG,
/// marks route availability, and commits valid node selections through
/// GameManager.TrySelectRunNode.
/// </summary>
[DisallowMultipleComponent]
public class GridMapController : MonoBehaviour
{
    private enum GridConnectionVisualState
    {
        CurrentPath,
        AvailablePath,
        InactivePath
    }

    private const string GridFontResourcePath = "Fonts/NicoBold-Regular";
    private const string DecisionPanelSkinResourcePath = "UI/Grid/DecisionPanelBackground";
    private const string RouteGraphBackgroundResourcePath = "UI/Grid/RouteGraphBackground";
    private const string MainTerminalSpriteRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal";
    private const string CyberHudSpriteRoot = MainTerminalSpriteRoot + "/Components/CyberpunkHUD";
    private const string PixelHudSpriteRoot = MainTerminalSpriteRoot + "/PixelUIHUD";
    private const string GridIconSpriteRoot = "Assets/_AlgoMon/Sprites/UI/Grid/Icons";

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
    [SerializeField] private Vector2 nodeSize = new Vector2(54f, 54f);
    [SerializeField] private float nodeVisualScale = 1.12f;
    [SerializeField] private float nodeLabelScale = 1.18f;
    [SerializeField] private float nodeVerticalEdgePadding = 60f;
    [SerializeField] private float horizontalPadding = 78f;
    [SerializeField] private float verticalPadding = 48f;
    [SerializeField] private float layerNodeSpacing = 132f;
    [SerializeField] private float connectionThickness = 2.4f;
    [SerializeField] private bool preserveSceneAuthoredLayout = true;

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

    [Header("HUD Style Sprites")]
    [SerializeField] private Sprite gridOuterFrameSprite;
    [SerializeField] private Sprite gridRadarSprite;
    [SerializeField] private Sprite gridNodeGlowSprite;
    [SerializeField] private Sprite gridNodeFrameSprite;
    [SerializeField] private Sprite gridNodeReticleSprite;
    [SerializeField] private Sprite gridConnectorSprite;
    [SerializeField] private Sprite gridConnectorHeadSprite;
    [SerializeField] private Sprite gridButtonFrameSprite;
    [SerializeField] private Sprite decisionPanelSkinSprite;
    [SerializeField] private Sprite routeGraphBackgroundSprite;

    [Header("Palette")]
    [SerializeField] private Color pageBackground = new Color(0.002f, 0.006f, 0.014f, 1f);
    [SerializeField] private Color panelBackground = new Color(0.001f, 0.006f, 0.013f, 0.72f);
    [SerializeField] private Color lineLocked = new Color(0.08f, 0.22f, 0.30f, 0.34f);
    [SerializeField] private Color lineAvailable = new Color(0.18f, 0.92f, 1f, 0.92f);
    [SerializeField] private Color lineVisited = new Color(0.32f, 0.55f, 0.86f, 0.62f);
    [SerializeField] private Color lockedFill = new Color(0.004f, 0.018f, 0.032f, 0.46f);
    [SerializeField] private Color currentFill = new Color(0.020f, 0.082f, 0.108f, 0.78f);
    [SerializeField] private Color availableFill = new Color(0.010f, 0.058f, 0.076f, 0.70f);
    [SerializeField] private Color visitedFill = new Color(0.030f, 0.072f, 0.140f, 0.62f);
    [SerializeField] private Color bossFill = new Color(0.102f, 0.020f, 0.045f, 0.72f);
    [SerializeField] private Color startFill = new Color(0.018f, 0.050f, 0.078f, 0.68f);
    [SerializeField] private Color textBright = new Color(0.93f, 0.98f, 1f, 1f);
    [SerializeField] private Color textDim = new Color(0.36f, 0.58f, 0.66f, 1f);
    [SerializeField] private Color accent = new Color(0.20f, 0.92f, 1f, 1f);
    [SerializeField] private Color warning = new Color(1f, 0.68f, 0.30f, 1f);

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
    private RectTransform decisionPanel;
    private Text decisionModeText;
    private Text decisionCurrentNodeText;
    private Text decisionNodeTypeText;
    private Text decisionRiskLevelText;
    private Text decisionStatusText;
    private Text decisionRewardSignalText;
    private Text decisionAvailableLinksText;
    private Text computeBankValueText;
    private Text payloadBufferValueText;
    private Text depthValueText;
    private bool decisionPanelUsesSkin;

    private void Awake()
    {
        defaultFont = GridFont();
        ApplyCyberStyleDefaults();
        LoadGridVisualSprites();
        EnsureSceneShell();

        // When preserving the scene-authored layout, do NOT run the cosmetic
        // restyle pass (it repositions/recolors the header, panels, footer, etc.).
        // Only wire up the references the dynamic content needs so the authored
        // layout shown in the editor matches Play mode exactly.
        if (preserveSceneAuthoredLayout)
        {
            WireAuthoredReferences();
        }
        else
        {
            ApplyGridVisualStyle();
            ApplyResolvedTextStyles();
        }
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
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            RebuildEditorPreview();
            return;
        }
#endif
        RebuildMap();
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Editor Preview")]
    private void RebuildEditorPreview()
    {
        defaultFont = GridFont();
        ApplyCyberStyleDefaults();
        LoadGridVisualSprites();
        EnsureSceneShell();
        ApplyGridVisualStyle();
        ApplyResolvedTextStyles();
        ConfigureDebugNewRunButton();

        manager = ResolveManager();
        if (manager == null)
        {
            SetHeader("No preview manager", "GameManager preview unavailable.");
            SetHint("TheGrid editor preview could not create a GameManager.");
            return;
        }

        int seed = debugSeed != 0 ? debugSeed : 4040;
        manager.BeginRun(seed, fallbackGenerationSettings);
        manager.EnsureCurrentRunHasEarlyHacker();
        RebuildMap("> EDITOR PREVIEW. Runtime layout is generated by GridMapController.");

        EditorUtility.SetDirty(this);
        if (canvas != null)
            EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    [MenuItem("AlgoMon/TheGrid/Rebuild Preview UI")]
    private static void RebuildActiveGridPreview()
    {
        GridMapController controller = FindObjectOfType<GridMapController>();
        if (controller == null)
        {
            Debug.LogWarning("No GridMapController found in the active scene.");
            return;
        }

        controller.RebuildEditorPreview();
        Selection.activeObject = controller.gameObject;
    }
#endif

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
        UpdateDecisionPanel(null);
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
        Vector2 resolvedNodeSize = ResolvedNodeSize();
        float resolvedVerticalPadding = ResolvedVerticalPadding(resolvedNodeSize);
        float usableWidth = Mathf.Max(resolvedNodeSize.x, width - horizontalPadding * 2f);
        float usableHeight = Mathf.Max(resolvedNodeSize.y, height - resolvedVerticalPadding * 2f);
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

        BuildRuntimeCurrentPathConnections(graph);
        BuildRuntimeAvailableConnections(graph);
    }

    private void BuildRuntimeCurrentPathConnections(GridGraph graph)
    {
        if (manager == null ||
            graph == null ||
            manager.visitedNodeIds == null ||
            manager.visitedNodeIds.Count < 2)
            return;

        for (int i = 0; i < manager.visitedNodeIds.Count - 1; i++)
        {
            GridNode source = graph.GetNode(manager.visitedNodeIds[i]);
            GridNode target = graph.GetNode(manager.visitedNodeIds[i + 1]);
            if (source == null || target == null || ContainsNodeId(source.outgoingNodeIds, target.id))
                continue;

            CreateConnection(source, target, GridConnectionVisualState.CurrentPath);
        }
    }

    private void BuildRuntimeAvailableConnections(GridGraph graph)
    {
        if (manager == null ||
            graph == null ||
            string.IsNullOrEmpty(manager.currentNodeId))
            return;

        GridNode source = graph.GetNode(manager.currentNodeId);
        if (source == null)
            return;

        List<string> availableNodeIds = manager.GetAvailableNodeIds();
        for (int i = 0; i < availableNodeIds.Count; i++)
        {
            string targetId = availableNodeIds[i];
            if (string.IsNullOrEmpty(targetId) || ContainsNodeId(source.outgoingNodeIds, targetId))
                continue;

            GridNode target = graph.GetNode(targetId);
            if (target == null)
                continue;

            CreateConnection(source, target, GridConnectionVisualState.AvailablePath);
        }
    }

    private static bool ContainsNodeId(IList<string> nodeIds, string nodeId)
    {
        if (nodeIds == null || string.IsNullOrEmpty(nodeId))
            return false;

        for (int i = 0; i < nodeIds.Count; i++)
        {
            if (string.Equals(nodeIds[i], nodeId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
            Color fill = FillFor(node, state);
            Color outline = OutlineFor(node, state);
            bool interactable = manager != null && node != null && manager.IsNodeAvailable(node.id);
            Color textColor = TextColorFor(state);
            Sprite iconSprite = IconFor(node, state);
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
        UpdateDecisionPanel(null);
        RefreshLegend();
    }

    private GridNodeVisualState StateFor(GridNode node)
    {
        if (manager == null || node == null)
            return GridNodeVisualState.Inactive;
        if (IsCurrentNode(node))
            return GridNodeVisualState.Current;
        if (node.nodeType == NodeType.Boss)
            return GridNodeVisualState.Target;
        if (manager.IsNodeAvailable(node.id))
            return GridNodeVisualState.NextAvailable;
        if (manager.IsNodeVisited(node.id))
            return GridNodeVisualState.Visited;
        if (IsNodeUnknown(node))
            return GridNodeVisualState.Unknown;
        return GridNodeVisualState.Inactive;
    }

    private bool IsNodeUnknown(GridNode node)
    {
        if (node == null || node.nodeType == NodeType.Boss)
            return false;

        GridNode current = CurrentRouteNode();
        int currentLayer = current != null ? current.layer : 0;
        return node.layer > currentLayer + 1;
    }

    private bool IsCurrentNode(GridNode node)
    {
        return node != null && string.Equals(node.id, CurrentNodeId(), StringComparison.OrdinalIgnoreCase);
    }

    private string CurrentNodeId()
    {
        return manager != null ? manager.currentNodeId : string.Empty;
    }

    private GridNode CurrentRouteNode()
    {
        if (manager == null ||
            manager.currentRunGraph == null ||
            string.IsNullOrEmpty(manager.currentNodeId))
            return null;

        GridNode current = manager.currentRunGraph.GetNode(manager.currentNodeId);
        if (current != null)
            return current;

        if (manager.currentRunGraph.nodes == null)
            return null;

        for (int i = 0; i < manager.currentRunGraph.nodes.Count; i++)
        {
            GridNode node = manager.currentRunGraph.nodes[i];
            if (node != null && string.Equals(node.id, manager.currentNodeId, StringComparison.OrdinalIgnoreCase))
                return node;
        }

        return null;
    }

    private Color FillFor(GridNode node, GridNodeVisualState state)
    {
        switch (state)
        {
            case GridNodeVisualState.Current:
                return Color.Lerp(currentFill, textBright, 0.10f);
            case GridNodeVisualState.NextAvailable:
                return availableFill;
            case GridNodeVisualState.Target:
                return new Color(bossFill.r, bossFill.g, bossFill.b, 0.18f);
            case GridNodeVisualState.Visited:
                return visitedFill;
            case GridNodeVisualState.Unknown:
                return new Color(0.006f, 0.014f, 0.018f, 0.50f);
            case GridNodeVisualState.Inactive:
                return LockedFillFor(node);
            default:
                return lockedFill;
        }
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

    private Color OutlineFor(GridNode node, GridNodeVisualState state)
    {
        Color nodeAccent = NodeAccentFor(node);

        switch (state)
        {
            case GridNodeVisualState.Current:
                return Color.Lerp(nodeAccent, textBright, 0.62f);
            case GridNodeVisualState.NextAvailable:
                return new Color(nodeAccent.r, nodeAccent.g, nodeAccent.b, 0.74f);
            case GridNodeVisualState.Target:
                return new Color(nodeAccent.r, nodeAccent.g, nodeAccent.b, 0.68f);
            case GridNodeVisualState.Visited:
                return new Color(lineVisited.r, lineVisited.g, lineVisited.b, 0.44f);
            case GridNodeVisualState.Unknown:
                return new Color(0.18f, 0.28f, 0.32f, 0.22f);
            default:
                return Color.Lerp(lineLocked, nodeAccent, node != null && node.nodeType == NodeType.Boss ? 0.34f : 0.10f);
        }
    }

    private Color IconColorFor(GridNode node, GridNodeVisualState state)
    {
        Color nodeAccent = NodeAccentFor(node);
        if (state == GridNodeVisualState.Current)
            return textBright;
        if (state == GridNodeVisualState.Unknown)
            return new Color(0.36f, 0.48f, 0.52f, 0.42f);
        if (state == GridNodeVisualState.Inactive)
            return new Color(textDim.r, textDim.g, textDim.b, 0.28f);
        if (state == GridNodeVisualState.Visited)
            return new Color(lineVisited.r, lineVisited.g, lineVisited.b, 0.50f);
        if (state == GridNodeVisualState.Target)
            return new Color(nodeAccent.r, nodeAccent.g, nodeAccent.b, 0.74f);
        if (node != null && node.nodeType == NodeType.Boss)
            return new Color(1f, 0.82f, 0.78f, 1f);
        if (node != null && node.nodeType == NodeType.Hacker)
            return new Color(0.42f, 1f, 0.78f, 1f);
        if (node != null && (node.nodeType == NodeType.Shop || node.nodeType == NodeType.Reboot))
            return warning;
        return nodeAccent;
    }

    private Color DetailColorFor(GridNode node, GridNodeVisualState state)
    {
        if (state == GridNodeVisualState.Unknown)
            return new Color(textDim.r, textDim.g, textDim.b, 0.0f);

        if (node != null && ThreatTierRules.IsEncounterNode(node.nodeType))
        {
            Color dangerColor = DangerLevelColorFor(node.dangerRating);
            float alpha = state == GridNodeVisualState.Current ? 0.96f :
                state == GridNodeVisualState.NextAvailable ? 0.78f :
                state == GridNodeVisualState.Target ? 0.74f :
                state == GridNodeVisualState.Visited ? 0.52f : 0.32f;
            return new Color(dangerColor.r, dangerColor.g, dangerColor.b, alpha);
        }

        if (state == GridNodeVisualState.Inactive)
            return new Color(textDim.r, textDim.g, textDim.b, 0.56f);

        Color nodeAccent = NodeAccentFor(node);
        return new Color(nodeAccent.r, nodeAccent.g, nodeAccent.b, 0.82f);
    }

    private Color TextColorFor(GridNodeVisualState state)
    {
        switch (state)
        {
            case GridNodeVisualState.Current:
                return textBright;
            case GridNodeVisualState.NextAvailable:
                return new Color(0.72f, 0.96f, 0.92f, 0.82f);
            case GridNodeVisualState.Unknown:
                return new Color(0.42f, 0.54f, 0.58f, 0.56f);
            case GridNodeVisualState.Target:
                return new Color(1f, 0.68f, 0.48f, 0.76f);
            case GridNodeVisualState.Visited:
                return new Color(0.52f, 0.70f, 0.82f, 0.58f);
            default:
                return new Color(textDim.r, textDim.g, textDim.b, 0.38f);
        }
    }

    private Color NodeAccentFor(GridNode node)
    {
        if (node == null)
            return accent;

        if (node.nodeType == NodeType.Boss)
            return Color.Lerp(DangerLevelStyleFor(node.dangerRating).Color, new Color(1f, 0.42f, 0.20f, 1f), 0.38f);
        if (ThreatTierRules.IsEncounterNode(node.nodeType))
            return DangerLevelStyleFor(node.dangerRating).Color;
        if (node.nodeType == NodeType.Shop || node.nodeType == NodeType.Reboot)
            return warning;
        return accent;
    }

    private static Color DangerLevelColorFor(int dangerRating)
    {
        return DangerLevelStyleFor(dangerRating).Color;
    }

    private static DangerLevelStyle DangerLevelStyleFor(int dangerRating)
    {
        switch (Mathf.Clamp(dangerRating, 1, ThreatTierRules.MaxTier))
        {
            case 1:
                return new DangerLevelStyle("LOW RISK", new Color(0.36f, 1.00f, 0.72f, 1f));
            case 2:
                return new DangerLevelStyle("MED-LOW RISK", new Color(0.28f, 0.78f, 1.00f, 1f));
            case 3:
                return new DangerLevelStyle("WARNING", new Color(1.00f, 0.62f, 0.24f, 1f));
            case 4:
                return new DangerLevelStyle("DANGER", new Color(1.00f, 0.25f, 0.24f, 1f));
            default:
                return new DangerLevelStyle("CRITICAL", new Color(1.00f, 0.24f, 0.78f, 1f));
        }
    }

    private struct DangerLevelStyle
    {
        public readonly string Label;
        public readonly Color Color;

        public DangerLevelStyle(string label, Color color)
        {
            Label = label;
            Color = color;
        }
    }

    private Sprite IconFor(GridNode node, GridNodeVisualState state)
    {
        if (state == GridNodeVisualState.Unknown)
            return null;
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
                return rebootIcon;
            case NodeType.Boss:
                return bossIcon;
            default:
                return combatIcon;
        }
    }

    private string LabelFor(GridNode node, GridNodeVisualState state)
    {
        switch (state)
        {
            case GridNodeVisualState.Unknown:
                return string.Empty;
            case GridNodeVisualState.Current:
                return string.Empty;
            case GridNodeVisualState.NextAvailable:
                return string.Empty;
            case GridNodeVisualState.Target:
                return "TARGET";
            case GridNodeVisualState.Visited:
                return string.Empty;
            default:
                return string.Empty;
        }
    }

    private string DetailLabelFor(GridNode node)
    {
        if (node == null)
            return string.Empty;

        GridNodeVisualState state = StateFor(node);
        if (state == GridNodeVisualState.Unknown || state == GridNodeVisualState.Inactive)
            return string.Empty;

        string label = ShortTypeLabelFor(node.nodeType);
        if (!ThreatTierRules.IsEncounterNode(node.nodeType))
            return label;

        int danger = Mathf.Clamp(node.dangerRating, 1, ThreatTierRules.MaxTier);
        return $"{label} D{danger}";
    }

    private string NodeRouteLabelFor(GridNode node)
    {
        if (node == null)
            return "NO NODE";

        return DetailLabelFor(node);
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
                return "BREACH";
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

    private Vector2 ResolvedNodeSize()
    {
        float scale = Mathf.Max(0.01f, nodeVisualScale);
        return new Vector2(
            Mathf.Max(1f, nodeSize.x * scale),
            Mathf.Max(1f, nodeSize.y * scale));
    }

    private float ResolvedVerticalPadding(Vector2 resolvedNodeSize)
    {
        float authoredPadding = nodeVerticalEdgePadding > 0f ? nodeVerticalEdgePadding : 60f;
        float labelClearance = resolvedNodeSize.y * 0.92f;
        return Mathf.Max(verticalPadding, authoredPadding, labelClearance);
    }

    private int ScaledNodeFontSize(int baseSize)
    {
        return Mathf.Max(baseSize, Mathf.RoundToInt(baseSize * Mathf.Max(1f, nodeLabelScale)));
    }

    private GridNodeButton CreateNodeView(GridNode node, Vector2 anchoredPosition)
    {
        GameObject nodeObject = CreateRectObject($"Node_{node.id}", nodeRoot);
        RectTransform rect = nodeObject.GetComponent<RectTransform>();
        Vector2 resolvedNodeSize = ResolvedNodeSize();
        rect.sizeDelta = resolvedNodeSize;
        rect.anchoredPosition = anchoredPosition;

        Image image = nodeObject.AddComponent<Image>();
        Sprite nodeFrameSprite = gridNodeFrameSprite != null ? gridNodeFrameSprite : nodeFillSprite;
        if (nodeFrameSprite != null)
        {
            image.sprite = nodeFrameSprite;
            image.preserveAspect = true;
        }
        else if (nodeSprite != null)
        {
            image.sprite = nodeSprite;
            image.preserveAspect = true;
        }
        image.color = lockedFill;

        Button button = nodeObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(Color.white, accent, 0.42f);
        colors.pressedColor = Color.Lerp(Color.white, accent, 0.62f);
        colors.selectedColor = Color.Lerp(Color.white, accent, 0.42f);
        colors.disabledColor = Color.white;
        button.colors = colors;

        Image haloImage = CreateImage("HaloImage", rect, gridNodeGlowSprite);
        haloImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        haloImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        haloImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        haloImage.rectTransform.anchoredPosition = Vector2.zero;
        haloImage.rectTransform.sizeDelta = resolvedNodeSize + new Vector2(10f, 10f);
        haloImage.raycastTarget = false;
        haloImage.preserveAspect = true;
        haloImage.color = new Color(accent.r, accent.g, accent.b, 0.04f);

        Image ringImage = CreateImage("RingImage", rect, gridNodeReticleSprite != null ? gridNodeReticleSprite : nodeSprite);
        ringImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        ringImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        ringImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ringImage.rectTransform.anchoredPosition = Vector2.zero;
        ringImage.rectTransform.sizeDelta = resolvedNodeSize + new Vector2(2f, 2f);
        ringImage.raycastTarget = false;
        ringImage.color = lineLocked;

        Image coreImage = CreateImage("CoreImage", rect, gridNodeFrameSprite);
        coreImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        coreImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        coreImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        coreImage.rectTransform.anchoredPosition = Vector2.zero;
        coreImage.rectTransform.sizeDelta = Vector2.one * Mathf.Max(20f, resolvedNodeSize.x * 0.38f);
        coreImage.raycastTarget = false;
        coreImage.preserveAspect = true;
        coreImage.color = new Color(accent.r, accent.g, accent.b, 0.24f);

        Image iconImage = CreateImage("IconImage", rect, null);
        iconImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        iconImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        iconImage.rectTransform.anchoredPosition = Vector2.zero;
        iconImage.rectTransform.sizeDelta = Vector2.one * Mathf.Max(24f, resolvedNodeSize.x * 0.43f);
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;

        Text typeLabel = CreateText("TypeLabel", rect, ScaledNodeFontSize(14), FontStyle.Bold, TextAnchor.MiddleCenter);
        typeLabel.rectTransform.anchorMin = Vector2.zero;
        typeLabel.rectTransform.anchorMax = Vector2.one;
        typeLabel.rectTransform.offsetMin = new Vector2(8f, 8f);
        typeLabel.rectTransform.offsetMax = new Vector2(-8f, -8f);

        Text detailLabel = CreateText("DetailLabel", rect, ScaledNodeFontSize(9), FontStyle.Bold, TextAnchor.MiddleCenter);
        detailLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        detailLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        detailLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        detailLabel.rectTransform.anchoredPosition = new Vector2(0f, -3f);
        detailLabel.rectTransform.sizeDelta = new Vector2(Mathf.Max(92f, resolvedNodeSize.x + 38f), 18f);
        detailLabel.resizeTextForBestFit = true;
        detailLabel.resizeTextMinSize = 8;
        detailLabel.resizeTextMaxSize = ScaledNodeFontSize(9);
        detailLabel.gameObject.SetActive(false);

        Text stateLabel = CreateText("StateLabel", rect, ScaledNodeFontSize(8), FontStyle.Bold, TextAnchor.MiddleCenter);
        stateLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        stateLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        stateLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        stateLabel.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        stateLabel.rectTransform.sizeDelta = new Vector2(84f, 18f);

        GridNodeButton view = nodeObject.AddComponent<GridNodeButton>();
        view.Bind(node, HandleNodeClicked, HandleNodePreviewed);
        spawnedObjects.Add(nodeObject);
        return view;
    }

    private void CreateConnection(GridNode source, GridNode target, GridConnectionVisualState? forcedState = null)
    {
        if (!nodePositions.TryGetValue(source.id, out Vector2 start))
            return;
        if (!nodePositions.TryGetValue(target.id, out Vector2 end))
            return;

        Vector2 direction = (end - start).normalized;
        float endTrim = Mathf.Max(24f, ResolvedNodeSize().x * 0.43f);
        start += direction * endTrim;
        end -= direction * endTrim;

        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f)
            return;

        GridConnectionVisualState connectionState = forcedState ?? ConnectionStateFor(source, target);
        if (connectionState == GridConnectionVisualState.InactivePath)
            return;

        GameObject lineObject = CreateRectObject($"Connection_{source.id}_{target.id}", connectionRoot);
        RectTransform rect = lineObject.GetComponent<RectTransform>();
        bool inactiveConnection = connectionState == GridConnectionVisualState.InactivePath;
        float resolvedThickness = ConnectionThicknessFor(connectionState);
        Color connectionColor = ConnectionColor(connectionState, source, target);

        rect.sizeDelta = new Vector2(length, resolvedThickness);
        rect.anchoredPosition = start + delta * 0.5f;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        Image image = lineObject.AddComponent<Image>();
        image.sprite = gridConnectorSprite;
        image.preserveAspect = false;
        image.color = connectionColor;
        image.raycastTarget = false;

        if (inactiveConnection)
            BuildInactiveConnectionDashes(rect, length, resolvedThickness, connectionColor);
        else if (connectionState == GridConnectionVisualState.CurrentPath)
            BuildCurrentConnectionDashes(rect, length, resolvedThickness, connectionColor);
        else if (connectionState == GridConnectionVisualState.AvailablePath)
            BuildAvailableConnectionDashes(rect, length, resolvedThickness, connectionColor);
        else
        {
            Image traceImage = CreateImage("ConnectionTrace", rect, null);
            traceImage.rectTransform.anchorMin = Vector2.zero;
            traceImage.rectTransform.anchorMax = Vector2.one;
            traceImage.rectTransform.offsetMin = new Vector2(0f, resolvedThickness * 0.35f);
            traceImage.rectTransform.offsetMax = new Vector2(0f, -resolvedThickness * 0.35f);
            traceImage.raycastTarget = false;
            traceImage.preserveAspect = false;
            traceImage.color = new Color(0.64f, 1f, 0.86f, 0.24f);
        }

        if (gridConnectorHeadSprite != null &&
            (connectionState == GridConnectionVisualState.AvailablePath || connectionState == GridConnectionVisualState.CurrentPath))
        {
            Image headImage = CreateImage("ConnectionHead", rect, gridConnectorHeadSprite);
            headImage.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            headImage.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            headImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            headImage.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            float headSize = connectionState == GridConnectionVisualState.CurrentPath ? 17f : 11f;
            headImage.rectTransform.sizeDelta = new Vector2(headSize, headSize);
            headImage.raycastTarget = false;
            headImage.color = connectionState == GridConnectionVisualState.CurrentPath
                ? new Color(connectionColor.r, connectionColor.g, connectionColor.b, 0.90f)
                : new Color(connectionColor.r, connectionColor.g, connectionColor.b, 0.42f);
        }

        spawnedObjects.Add(lineObject);
    }

    private void BuildInactiveConnectionDashes(RectTransform parent, float length, float thickness, Color color)
    {
        Image baseImage = parent.GetComponent<Image>();
        if (baseImage != null)
            baseImage.color = new Color(color.r, color.g, color.b, color.a * 0.20f);

        int dashCount = Mathf.Clamp(Mathf.FloorToInt(length / 34f), 3, 12);
        float step = length / dashCount;
        for (int i = 0; i < dashCount; i++)
        {
            Image dash = CreateImage($"InactiveDash_{i:00}", parent, null);
            dash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            dash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dash.rectTransform.anchoredPosition = new Vector2(-length * 0.5f + step * (i + 0.5f), 0f);
            dash.rectTransform.sizeDelta = new Vector2(Mathf.Min(12f, step * 0.42f), Mathf.Max(0.7f, thickness));
            dash.raycastTarget = false;
            dash.color = new Color(color.r, color.g, color.b, color.a * 0.58f);
        }
    }

    private void BuildCurrentConnectionDashes(RectTransform parent, float length, float thickness, Color color)
    {
        Image baseImage = parent.GetComponent<Image>();
        if (baseImage != null)
            baseImage.color = new Color(color.r, color.g, color.b, color.a * 0.52f);

        int dashCount = Mathf.Clamp(Mathf.FloorToInt(length / 28f), 4, 16);
        float step = length / dashCount;
        for (int i = 0; i < dashCount; i++)
        {
            Image dash = CreateImage($"CurrentDash_{i:00}", parent, null);
            dash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            dash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dash.rectTransform.anchoredPosition = new Vector2(-length * 0.5f + step * (i + 0.5f), 0f);
            dash.rectTransform.sizeDelta = new Vector2(Mathf.Min(18f, step * 0.58f), Mathf.Max(1.6f, thickness));
            dash.raycastTarget = false;
            dash.color = new Color(color.r, color.g, color.b, 0.92f);
        }

        Image traceImage = CreateImage("CurrentConnectionGlow", parent, null);
        traceImage.rectTransform.anchorMin = Vector2.zero;
        traceImage.rectTransform.anchorMax = Vector2.one;
        traceImage.rectTransform.offsetMin = new Vector2(0f, -thickness * 0.92f);
        traceImage.rectTransform.offsetMax = new Vector2(0f, thickness * 0.92f);
        traceImage.raycastTarget = false;
        traceImage.color = new Color(color.r, color.g, color.b, 0.085f);
    }

    private void BuildAvailableConnectionDashes(RectTransform parent, float length, float thickness, Color color)
    {
        Image baseImage = parent.GetComponent<Image>();
        if (baseImage != null)
            baseImage.color = new Color(color.r, color.g, color.b, color.a * 0.62f);

        int dashCount = Mathf.Clamp(Mathf.FloorToInt(length / 42f), 2, 9);
        float step = length / dashCount;
        for (int i = 0; i < dashCount; i++)
        {
            Image dash = CreateImage($"AvailableDash_{i:00}", parent, null);
            dash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            dash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dash.rectTransform.anchoredPosition = new Vector2(-length * 0.5f + step * (i + 0.5f), 0f);
            dash.rectTransform.sizeDelta = new Vector2(Mathf.Min(10f, step * 0.36f), Mathf.Max(0.9f, thickness));
            dash.raycastTarget = false;
            dash.color = new Color(color.r, color.g, color.b, color.a * 0.58f);
        }

        Image traceImage = CreateImage("AvailableConnectionGlow", parent, null);
        traceImage.rectTransform.anchorMin = Vector2.zero;
        traceImage.rectTransform.anchorMax = Vector2.one;
        traceImage.rectTransform.offsetMin = new Vector2(0f, -thickness * 0.36f);
        traceImage.rectTransform.offsetMax = new Vector2(0f, thickness * 0.36f);
        traceImage.raycastTarget = false;
        traceImage.color = new Color(color.r, color.g, color.b, 0.024f);
    }

    private GridConnectionVisualState ConnectionStateFor(GridNode source, GridNode target)
    {
        if (manager == null || source == null || target == null)
            return GridConnectionVisualState.InactivePath;

        if (IsConnectionOnCurrentPath(source, target))
            return GridConnectionVisualState.CurrentPath;
        if (IsConnectionAvailable(source, target))
            return GridConnectionVisualState.AvailablePath;
        return GridConnectionVisualState.InactivePath;
    }

    private float ConnectionThicknessFor(GridConnectionVisualState state)
    {
        switch (state)
        {
            case GridConnectionVisualState.AvailablePath:
                return connectionThickness * 0.64f;
            case GridConnectionVisualState.CurrentPath:
                return connectionThickness * 1.86f;
            default:
                return connectionThickness * 0.52f;
        }
    }

    private Color ConnectionColor(GridConnectionVisualState state, GridNode source, GridNode target)
    {
        switch (state)
        {
            case GridConnectionVisualState.AvailablePath:
            {
                Color targetAccent = NodeAccentFor(target);
                return new Color(targetAccent.r, targetAccent.g, targetAccent.b, 0.54f);
            }
            case GridConnectionVisualState.CurrentPath:
                return new Color(0.38f, 1f, 0.72f, 0.98f);
            default:
                return new Color(lineLocked.r, lineLocked.g, lineLocked.b, 0.12f);
        }
    }

    private bool IsConnectionAvailable(GridNode source, GridNode target)
    {
        return manager != null &&
               source != null &&
               target != null &&
               string.Equals(source.id, CurrentNodeId(), StringComparison.OrdinalIgnoreCase) &&
               manager.IsNodeAvailable(target.id);
    }

    private bool IsConnectionOnCurrentPath(GridNode source, GridNode target)
    {
        if (manager == null ||
            source == null ||
            target == null ||
            manager.visitedNodeIds == null ||
            manager.visitedNodeIds.Count < 2)
            return false;

        for (int i = 0; i < manager.visitedNodeIds.Count - 1; i++)
        {
            if (string.Equals(manager.visitedNodeIds[i], source.id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(manager.visitedNodeIds[i + 1], target.id, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            SetHint($"{node.id} is locked from the current node.");
            RefreshNodeStates();
            return;
        }

        AudioManager.Instance?.PlayUiSfx(UiSfx.Impact);

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
            UpdateDecisionPanel(null);
            return;
        }

        SetHint(BuildPreviewHint(node));
        UpdateDecisionPanel(StateFor(node) == GridNodeVisualState.Unknown ? null : node);
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
        shopTitleText.text = "Credit Shop";

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
            shopTitleText.text = activeShopNode != null ? "Credit Shop" : "Shop Offline";
        if (shopBalanceText != null)
            shopBalanceText.text = $"CR {manager.computeBalance:000} | REROLL {manager.CurrentShopRefreshCost}";
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
            SetButtonText(button, $"{offer.ShortLabel}  {offer.ComputeCost} CR");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => TryPurchaseShopOffer(offer));
        }

        if (shopRefreshButton != null)
        {
            string reason;
            bool canRefresh = manager.CanRefreshShopOffers(out reason);
            shopRefreshButton.interactable = canRefresh;
            SetButtonText(shopRefreshButton, $"REFRESH {manager.CurrentShopRefreshCost} CR");
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

            builder.AppendLine($"{offer.DisplayName} - {offer.ComputeCost} CR{state}");
            builder.AppendLine(offer.Description);
        }

        if (manager != null)
            builder.AppendLine($"Refresh: {manager.CurrentShopRefreshCost} CR now; next refresh doubles.");

        return builder.ToString();
    }

    private void TryPurchaseShopOffer(RunShopOffer offer)
    {
        manager = ResolveManager();
        if (manager == null || offer == null)
            return;

        string message;
        if (manager.TryPurchaseShopOffer(offer, out message))
        {
            AudioManager.Instance?.PlayPurchaseSfx();
            SetHint($"> {message} Remaining credits: {manager.computeBalance:000}.");
        }
        else
        {
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            SetHint($"> Purchase rejected: {message}");
        }

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
        {
            SetHint($"> {message} Remaining credits: {manager.computeBalance:000}.");
        }
        else
        {
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            SetHint($"> Refresh rejected: {message}");
        }

        RefreshShopPanel();
        RefreshNodeStates();
    }

    private string BuildShopHint()
    {
        if (manager == null)
            return "> Shop node online.";

        return "> Shop node online. Three offers loaded; refresh costs credits and doubles each time.";
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

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ClearChildrenImmediate(connectionRoot);
            ClearChildrenImmediate(nodeRoot);
            spawnedObjects.Clear();
            return;
        }
#endif

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

#if UNITY_EDITOR
    private static void ClearChildrenImmediate(RectTransform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null)
                DestroyImmediate(child.gameObject);
        }
    }
#endif

    private void EnsureSceneShell()
    {
        EnsureEventSystem();

        if (TryResolveSceneReferences())
            return;

        if (canvas == null)
            canvas = CreateCanvas();

        BuildDefaultSceneShell();
    }

    private void LoadGridVisualSprites()
    {
#if UNITY_EDITOR
        gridOuterFrameSprite = null;
        gridRadarSprite = gridRadarSprite != null
            ? gridRadarSprite
            : LoadEditorSprite(CyberHudSpriteRoot + "/hud_radar_frame_tint.png");
        gridNodeFrameSprite = gridNodeFrameSprite != null
            ? gridNodeFrameSprite
            : LoadEditorSprite(PixelHudSpriteRoot + "/SkillTree/White/SkillSlotRound.png");
        gridNodeReticleSprite = gridNodeReticleSprite != null
            ? gridNodeReticleSprite
            : LoadEditorSprite(PixelHudSpriteRoot + "/SkillTree/White/SkillSlot_FocusRegular.png");
        gridConnectorSprite = gridConnectorSprite != null
            ? gridConnectorSprite
            : LoadEditorSprite(PixelHudSpriteRoot + "/SkillTree/White/ConnectorThinHorizontal.png");
        gridConnectorHeadSprite = gridConnectorHeadSprite != null
            ? gridConnectorHeadSprite
            : LoadEditorSprite(PixelHudSpriteRoot + "/Selectors/ChevronRight_Select.png");
        gridButtonFrameSprite = gridButtonFrameSprite != null
            ? gridButtonFrameSprite
            : LoadEditorSprite(CyberHudSpriteRoot + "/btn_wide_01_tint.png");

#endif

        // Node-type icons must resolve in standalone builds too. They previously loaded
        // through AssetDatabase inside UNITY_EDITOR only, so builds fell back to the stale
        // serialized HUD icons and never matched what the editor displayed. Resolve through
        // the runtime catalog (baked by AlgoMon > Build > Rebuild Runtime Asset Catalogs)
        // so the packaged build shows the same icons as the editor.
        startIcon = ResolveGridSprite(GridIconSpriteRoot + "/square-chevron-right.png", startIcon);
        combatIcon = ResolveGridSprite(GridIconSpriteRoot + "/sword.png", combatIcon);
        hackerIcon = ResolveGridSprite(GridIconSpriteRoot + "/square-terminal.png", hackerIcon);
        eliteIcon = ResolveGridSprite(GridIconSpriteRoot + "/swords.png", eliteIcon);
        shopIcon = ResolveGridSprite(GridIconSpriteRoot + "/shopping-bag.png", shopIcon);
        bossIcon = ResolveGridSprite(GridIconSpriteRoot + "/cpu.png", bossIcon);

        // The reboot icon is procedurally generated below in both editor and build,
        // so it always matches and does not need a catalog entry.
        rebootIcon = CreateRebootLoopArrowSprite("GridRebootLoopArrow", 96);
        gridNodeGlowSprite = CreateRadialSprite("GridNodeGlowDisc", 96, 0f, 0.42f, 0.34f);
        gridNodeFrameSprite = CreateRadialSprite("GridNodeCoreDisc", 64, 0f, 0.35f, 0.10f);
        gridNodeReticleSprite = CreateRadialSprite("GridNodeOuterRing", 64, 0.38f, 0.45f, 0.035f);
        decisionPanelSkinSprite = decisionPanelSkinSprite != null
            ? decisionPanelSkinSprite
            : LoadResourceTextureSprite(DecisionPanelSkinResourcePath, "DecisionPanelBackground");
        routeGraphBackgroundSprite = routeGraphBackgroundSprite != null
            ? routeGraphBackgroundSprite
            : LoadResourceTextureSprite(RouteGraphBackgroundResourcePath, "RouteGraphBackground");
    }

    private static Sprite CreateRadialSprite(string spriteName, int size, float innerRadius, float outerRadius, float feather)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1f) * 0.5f, (size - 1f) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float outerAlpha = Mathf.InverseLerp(outerRadius + feather, outerRadius, distance);
                float innerAlpha = innerRadius <= 0f
                    ? 1f
                    : Mathf.InverseLerp(innerRadius - feather, innerRadius, distance);
                float alpha = Mathf.Clamp01(outerAlpha * innerAlpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateRebootLoopArrowSprite(string spriteName, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1f) * 0.5f, (size - 1f) * 0.5f);
        float radius = size * 0.30f;
        float thickness = Mathf.Max(3.5f, size * 0.055f);
        float headLength = size * 0.19f;
        float tipAngle = 42f * Mathf.Deg2Rad;
        Vector2 tipDirection = new Vector2(Mathf.Cos(tipAngle), Mathf.Sin(tipAngle));
        Vector2 tip = center + tipDirection * radius;
        Vector2 headA = tip + new Vector2(-headLength, 0f);
        Vector2 headB = tip + new Vector2(0f, -headLength);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                Vector2 offset = point - center;
                float distance = offset.magnitude;
                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                if (angle < 0f)
                    angle += 360f;

                bool inArc = angle >= 58f && angle <= 334f;
                float alpha = 0f;
                if (inArc)
                {
                    float edgeDistance = Mathf.Abs(distance - radius);
                    alpha = Mathf.Max(alpha, Mathf.InverseLerp(thickness, thickness * 0.30f, edgeDistance));
                }

                alpha = Mathf.Max(alpha, LineAlpha(point, tip, headA, thickness * 0.58f));
                alpha = Mathf.Max(alpha, LineAlpha(point, tip, headB, thickness * 0.58f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float LineAlpha(Vector2 point, Vector2 start, Vector2 end, float thickness)
    {
        float distance = DistanceToSegment(point, start, end);
        return Mathf.InverseLerp(thickness, thickness * 0.38f, distance);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
            return Vector2.Distance(point, start);

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * t);
    }

    private static Sprite LoadResourceTextureSprite(string resourcePath, string spriteName)
    {
        Sprite importedSprite = Resources.Load<Sprite>(resourcePath);
        if (importedSprite != null)
            return importedSprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = spriteName;
        return sprite;
    }

    private void ApplyCyberStyleDefaults()
    {
        // When preserving scene-authored values, keep the Inspector-tuned layout
        // and palette so manual adjustments survive entering Play mode.
        if (preserveSceneAuthoredLayout)
            return;

        nodeSize = new Vector2(50f, 50f);
        nodeVerticalEdgePadding = 60f;
        horizontalPadding = 56f;
        verticalPadding = 22f;
        layerNodeSpacing = 226f;
        connectionThickness = 1.85f;

        pageBackground = new Color(0.000f, 0.003f, 0.010f, 1f);
        panelBackground = new Color(0.000f, 0.006f, 0.014f, 0.54f);
        lineLocked = new Color(0.08f, 0.20f, 0.26f, 0.16f);
        lineAvailable = new Color(0.32f, 0.98f, 0.82f, 0.78f);
        lineVisited = new Color(0.24f, 0.94f, 0.80f, 0.92f);
        lockedFill = new Color(0.002f, 0.014f, 0.026f, 0.10f);
        currentFill = new Color(0.020f, 0.135f, 0.108f, 0.36f);
        availableFill = new Color(0.006f, 0.056f, 0.080f, 0.18f);
        visitedFill = new Color(0.026f, 0.066f, 0.132f, 0.14f);
        bossFill = new Color(0.090f, 0.016f, 0.040f, 0.18f);
        startFill = new Color(0.012f, 0.045f, 0.074f, 0.22f);
        textBright = new Color(0.92f, 0.99f, 1f, 1f);
        textDim = new Color(0.34f, 0.58f, 0.66f, 1f);
        accent = new Color(0.20f, 0.94f, 0.86f, 1f);
        warning = new Color(1f, 0.60f, 0.28f, 1f);
    }

    // Resolve a sprite by its asset path for both editor and standalone builds:
    // the runtime catalog (baked into Resources) covers builds and post-bake editor,
    // the editor AssetDatabase covers a pre-bake editor session, and the serialized
    // reference is the final fallback.
    private static Sprite ResolveGridSprite(string assetPath, Sprite fallback)
    {
        Sprite fromCatalog = RuntimeUiAssetCatalog.FindSprite(assetPath);
        if (fromCatalog != null)
            return fromCatalog;
#if UNITY_EDITOR
        Sprite fromDatabase = LoadEditorSprite(assetPath);
        if (fromDatabase != null)
            return fromDatabase;
#endif
        return fallback;
    }

#if UNITY_EDITOR
    private static Sprite LoadEditorSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
#endif

    private void ApplyGridVisualStyle()
    {
        if (canvas == null)
            return;

        ConfigureCanvas(canvas);
        EnsureCanvasBackground();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform frame = FindRect(canvasRect, "GridFrame");
        if (frame == null && mapRoot != null)
            frame = mapRoot.parent as RectTransform;

        if (frame != null)
        {
            if (!preserveSceneAuthoredLayout)
            {
                frame.anchorMin = new Vector2(0.035f, 0.055f);
                frame.anchorMax = new Vector2(0.965f, 0.945f);
                frame.offsetMin = Vector2.zero;
                frame.offsetMax = Vector2.zero;
            }

            Image frameImage = frame.GetComponent<Image>() ?? frame.gameObject.AddComponent<Image>();
            if (!preserveSceneAuthoredLayout)
            {
                frameImage.sprite = null;
                frameImage.color = panelBackground;
                frameImage.raycastTarget = false;
            }

            RectTransform outerTrace = FindRect(frame, "GridOuterTrace");
            if (outerTrace != null)
                outerTrace.gameObject.SetActive(false);

            BuildFunctionalBackground(frame);
            BuildFrameCornerAccents(frame);

            StyleHeaderText(frame);
            StyleFooterText(frame);
            StyleDecisionPanel(frame);
            StyleNewRunButton();
        }

        StyleMapRoot();
        Canvas.ForceUpdateCanvases();
    }

    private void BuildFunctionalBackground(RectTransform frame)
    {
        if (frame == null)
            return;

        RectTransform layer = GetOrCreateRect("GridHudFunctionalBackground", frame);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.SetAsFirstSibling();

        RectTransform fieldBackground = GetOrCreateRect("RouteGraphFieldBackground", layer);
        fieldBackground.anchorMin = new Vector2(0.045f, 0.145f);
        fieldBackground.anchorMax = new Vector2(0.955f, 0.835f);
        fieldBackground.offsetMin = Vector2.zero;
        fieldBackground.offsetMax = Vector2.zero;
        fieldBackground.SetAsFirstSibling();
        fieldBackground.gameObject.SetActive(false);
        ConfigureOverlayImage(
            fieldBackground,
            routeGraphBackgroundSprite,
            new Color(0.70f, 0.96f, 1f, 0.46f),
            false);

        RectTransform fieldVeil = GetOrCreateRect("RouteGraphFieldVeil", layer);
        fieldVeil.anchorMin = fieldBackground.anchorMin;
        fieldVeil.anchorMax = fieldBackground.anchorMax;
        fieldVeil.offsetMin = Vector2.zero;
        fieldVeil.offsetMax = Vector2.zero;
        fieldVeil.SetSiblingIndex(Mathf.Min(1, layer.childCount - 1));
        fieldVeil.gameObject.SetActive(false);
        ConfigureOverlayImage(fieldVeil, null, new Color(0.000f, 0.004f, 0.010f, 0.20f), false);

        for (int i = 0; i <= 12; i++)
        {
            float x = Mathf.Lerp(0.045f, 0.955f, i / 12f);
            CreateHudLine(
                layer,
                $"HudGridV_{i:00}",
                new Vector2(x, 0.135f),
                new Vector2(x, 0.855f),
                new Vector2(0.7f, 0f),
                new Color(accent.r, accent.g, accent.b, i % 4 == 0 ? 0.030f : 0.016f));
        }

        for (int i = 0; i <= 7; i++)
        {
            float y = Mathf.Lerp(0.145f, 0.830f, i / 7f);
            CreateHudLine(
                layer,
                $"HudGridH_{i:00}",
                new Vector2(0.045f, y),
                new Vector2(0.955f, y),
                new Vector2(0f, 0.7f),
                new Color(accent.r, accent.g, accent.b, i % 3 == 0 ? 0.028f : 0.014f));
        }

        for (int i = 0; i < 5; i++)
        {
            float y = Mathf.Lerp(0.205f, 0.760f, i / 4f);
            CreateHudLine(
                layer,
                $"HudScanLine_{i:00}",
                new Vector2(0.055f, y),
                new Vector2(0.755f, y),
                new Vector2(0f, 1.2f),
                new Color(0.70f, 0.96f, 1f, 0.022f));
        }
    }

    private void CreateHudLine(
        RectTransform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta,
        Color color)
    {
        RectTransform line = GetOrCreateRect(objectName, parent);
        line.anchorMin = anchorMin;
        line.anchorMax = anchorMax;
        line.pivot = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = sizeDelta;
        line.gameObject.SetActive(true);
        ConfigureOverlayImage(line, null, color, false);
    }

    private void BuildFrameCornerAccents(RectTransform frame)
    {
        if (frame == null)
            return;

        CreateCornerTrace(frame, "GridCornerTL_H", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -20f), new Vector2(190f, 1.25f));
        CreateCornerTrace(frame, "GridCornerTL_V", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(1.25f, 118f));
        CreateCornerTrace(frame, "GridCornerTR_H", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -20f), new Vector2(190f, 1.25f));
        CreateCornerTrace(frame, "GridCornerTR_V", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -20f), new Vector2(1.25f, 118f));
        CreateCornerTrace(frame, "GridCornerBL_H", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 20f), new Vector2(190f, 1.25f));
        CreateCornerTrace(frame, "GridCornerBL_V", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 20f), new Vector2(1.25f, 82f));
        CreateCornerTrace(frame, "GridCornerBR_H", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(190f, 1.25f));
        CreateCornerTrace(frame, "GridCornerBR_V", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(1.25f, 82f));
    }

    private void CreateCornerTrace(
        RectTransform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        RectTransform trace = GetOrCreateRect(objectName, parent);
        trace.anchorMin = anchorMin;
        trace.anchorMax = anchorMax;
        trace.pivot = pivot;
        trace.anchoredPosition = anchoredPosition;
        trace.sizeDelta = size;
        trace.gameObject.SetActive(true);
        ConfigureOverlayImage(trace, null, new Color(accent.r, accent.g, accent.b, 0.16f), false);
    }

    private void StyleHeaderText(RectTransform frame)
    {
        if (titleText != null)
        {
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0.58f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(30f, -14f);
            titleText.rectTransform.sizeDelta = new Vector2(560f, 34f);
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = textBright;
        }

        if (seedText != null)
        {
            seedText.text = string.Empty;
            seedText.gameObject.SetActive(false);
        }

        StyleTopStatusModules(frame);
    }

    private void StyleTopStatusModules(RectTransform frame)
    {
        if (frame == null)
            return;

        computeBankValueText = CreateTopStatusModule(frame, "StatusComputeBank", "CREDITS", 0.610f, 0.720f);
        payloadBufferValueText = CreateTopStatusModule(frame, "StatusPayloadBuffer", "PAYLOAD BUFFER", 0.735f, 0.845f);
        depthValueText = CreateTopStatusModule(frame, "StatusDepth", "DEPTH", 0.860f, 0.955f);
        UpdateTopStatusModules(manager != null ? manager.currentRunGraph : null);
    }

    private Text CreateTopStatusModule(RectTransform frame, string objectName, string label, float xMin, float xMax)
    {
        RectTransform module = GetOrCreateRect(objectName, frame);
        module.anchorMin = new Vector2(xMin, 0.895f);
        module.anchorMax = new Vector2(xMax, 0.970f);
        module.offsetMin = Vector2.zero;
        module.offsetMax = Vector2.zero;

        Image image = module.GetComponent<Image>() ?? module.gameObject.AddComponent<Image>();
        image.color = new Color(0.002f, 0.014f, 0.022f, 0.22f);
        image.raycastTarget = false;

        Text labelText = GetOrCreateText("Label", module, 7, FontStyle.Bold, TextAnchor.UpperLeft);
        labelText.rectTransform.anchorMin = new Vector2(0.055f, 0.560f);
        labelText.rectTransform.anchorMax = new Vector2(0.945f, 0.940f);
        labelText.rectTransform.offsetMin = Vector2.zero;
        labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.text = label;
        labelText.color = new Color(0.48f, 0.86f, 0.92f, 0.58f);
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Text valueText = GetOrCreateText("Value", module, 13, FontStyle.Bold, TextAnchor.LowerLeft);
        valueText.rectTransform.anchorMin = new Vector2(0.055f, 0.080f);
        valueText.rectTransform.anchorMax = new Vector2(0.945f, 0.620f);
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = Vector2.zero;
        valueText.color = new Color(0.88f, 1f, 0.96f, 0.94f);
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;

        return valueText;
    }

    private void StyleFooterText(RectTransform frame)
    {
        bool telemetryStripExisted = HasChild(frame, "TelemetryStrip");
        RectTransform telemetryStrip = GetOrCreateRect("TelemetryStrip", frame);
        if (!ShouldPreserveExistingLayout(telemetryStripExisted))
        {
            telemetryStrip.anchorMin = new Vector2(0.045f, 0.045f);
            telemetryStrip.anchorMax = new Vector2(0.955f, 0.115f);
            telemetryStrip.offsetMin = new Vector2(0f, 3.204f);
            telemetryStrip.offsetMax = new Vector2(5.953f, 3.204f);
        }
        telemetryStrip.SetAsFirstSibling();
        Image telemetryImage = telemetryStrip.GetComponent<Image>() ?? telemetryStrip.gameObject.AddComponent<Image>();
        telemetryImage.color = new Color(0.002f, 0.014f, 0.026f, 0.46f);
        telemetryImage.raycastTarget = false;

        Outline telemetryOutline = telemetryStrip.GetComponent<Outline>() ?? telemetryStrip.gameObject.AddComponent<Outline>();
        telemetryOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.28f);
        telemetryOutline.effectDistance = new Vector2(1f, -1f);

        if (hintText != null)
        {
            bool hintExisted = HasChild(frame, "Hint");
            if (!ShouldPreserveExistingLayout(hintExisted))
            {
                hintText.rectTransform.anchorMin = new Vector2(0.045f, 0.045f);
                hintText.rectTransform.anchorMax = new Vector2(0.955f, 0.115f);
                hintText.rectTransform.offsetMin = new Vector2(0f, 0f);
                hintText.rectTransform.offsetMax = new Vector2(5.952f, 0f);
                hintText.fontSize = 14;
            }
            hintText.fontStyle = FontStyle.Bold;
            hintText.alignment = TextAnchor.MiddleLeft;
            hintText.color = new Color(0.70f, 0.96f, 1f, 0.86f);
            hintText.transform.SetAsLastSibling();
        }

        if (legendText != null)
        {
            legendText.rectTransform.anchorMin = new Vector2(0.64f, 0.120f);
            legendText.rectTransform.anchorMax = new Vector2(0.955f, 0.152f);
            legendText.rectTransform.offsetMin = Vector2.zero;
            legendText.rectTransform.offsetMax = Vector2.zero;
            legendText.fontSize = 10;
            legendText.fontStyle = FontStyle.Bold;
            legendText.alignment = TextAnchor.MiddleRight;
            legendText.color = new Color(0.44f, 0.76f, 0.84f, 0.50f);
        }
    }

    private void DisableNodePreviewPanel(RectTransform frame)
    {
        if (frame == null)
            return;

        RectTransform previewPanel = FindRect(frame, "NodePreviewPanel");
        if (previewPanel != null)
            previewPanel.gameObject.SetActive(false);
    }

    private void StyleDecisionPanel(RectTransform frame)
    {
        if (frame == null)
            return;

        DisableNodePreviewPanel(frame);

        bool decisionPanelExisted = HasChild(frame, "DecisionPanel");
        decisionPanel = GetOrCreateRect("DecisionPanel", frame);
        if (!ShouldPreserveExistingLayout(decisionPanelExisted))
        {
            decisionPanel.anchorMin = new Vector2(0.765f, 0.112f);
            decisionPanel.anchorMax = new Vector2(0.965f, 0.865f);
            decisionPanel.offsetMin = new Vector2(-5.938f, 5.126f);
            decisionPanel.offsetMax = new Vector2(35.730f, 31.444f);
        }
        decisionPanel.gameObject.SetActive(true);
        decisionPanel.SetAsLastSibling();

        Image panelImage = decisionPanel.GetComponent<Image>() ?? decisionPanel.gameObject.AddComponent<Image>();
        decisionPanelUsesSkin = decisionPanelSkinSprite != null;
        panelImage.sprite = decisionPanelSkinSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = false;
        panelImage.color = decisionPanelUsesSkin
            ? new Color(0.80f, 0.98f, 1f, 0.54f)
            : new Color(0.004f, 0.012f, 0.014f, 0.80f);
        panelImage.raycastTarget = false;

        Outline outline = decisionPanel.GetComponent<Outline>() ?? decisionPanel.gameObject.AddComponent<Outline>();
        outline.effectColor = decisionPanelUsesSkin
            ? new Color(accent.r, accent.g, accent.b, 0.035f)
            : new Color(accent.r, accent.g, accent.b, 0.11f);
        outline.effectDistance = decisionPanelUsesSkin ? new Vector2(0.6f, -0.6f) : new Vector2(1f, -1f);

        if (decisionPanelUsesSkin)
            SetChildrenWithPrefixActive(decisionPanel, "DecisionFrame", false);
        else
            BuildDecisionPanelFrame(decisionPanel);

        bool titleExisted = HasChild(decisionPanel, "DecisionTitle");
        Text title = GetOrCreateText("DecisionTitle", decisionPanel, 12, FontStyle.Bold, TextAnchor.UpperLeft);
        if (!ShouldPreserveExistingLayout(titleExisted))
        {
            title.rectTransform.anchorMin = decisionPanelUsesSkin ? new Vector2(0.160f, 0.895f) : new Vector2(0.090f, 0.895f);
            title.rectTransform.anchorMax = decisionPanelUsesSkin ? new Vector2(0.600f, 0.940f) : new Vector2(0.735f, 0.958f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;
            title.fontSize = decisionPanelUsesSkin ? 12 : 11;
        }
        title.text = "DECISION PANEL";
        title.color = decisionPanelUsesSkin
            ? new Color(0.72f, 0.98f, 1f, 0.68f)
            : new Color(0.72f, 0.94f, 0.94f, 0.78f);

        bool modeExisted = HasChild(decisionPanel, "DecisionMode");
        decisionModeText = GetOrCreateText("DecisionMode", decisionPanel, 9, FontStyle.Bold, TextAnchor.UpperRight);
        if (!ShouldPreserveExistingLayout(modeExisted))
        {
            decisionModeText.rectTransform.anchorMin = decisionPanelUsesSkin ? new Vector2(0.600f, 0.890f) : new Vector2(0.470f, 0.895f);
            decisionModeText.rectTransform.anchorMax = decisionPanelUsesSkin ? new Vector2(0.855f, 0.927f) : new Vector2(0.910f, 0.958f);
            decisionModeText.rectTransform.offsetMin = Vector2.zero;
            decisionModeText.rectTransform.offsetMax = Vector2.zero;
            decisionModeText.fontSize = 9;
        }
        decisionModeText.color = new Color(1f, 0.40f, 0.72f, decisionPanelUsesSkin ? 0.48f : 0.58f);

        if (decisionPanelUsesSkin)
        {
            decisionCurrentNodeText = CreateDecisionSection("DecisionCurrentNode", "CURRENT NODE", 0.775f, 0.850f);
            decisionNodeTypeText = CreateDecisionSection("DecisionNodeType", "NODE TYPE", 0.662f, 0.737f);
            decisionRiskLevelText = CreateDecisionSection("DecisionRiskLevel", "RISK LEVEL", 0.550f, 0.625f);
            decisionStatusText = CreateDecisionSection("DecisionStatus", "STATUS", 0.438f, 0.513f);
            decisionRewardSignalText = CreateDecisionSection("DecisionRewardSignal", "REWARD SIGNAL", 0.326f, 0.401f);
            decisionAvailableLinksText = CreateDecisionSection("DecisionAvailableLinks", "AVAILABLE LINKS", 0.060f, 0.285f);
        }
        else
        {
            decisionCurrentNodeText = CreateDecisionSection("DecisionCurrentNode", "CURRENT NODE", 0.765f, 0.865f);
            decisionNodeTypeText = CreateDecisionSection("DecisionNodeType", "NODE TYPE", 0.635f, 0.745f);
            decisionRiskLevelText = CreateDecisionSection("DecisionRiskLevel", "RISK LEVEL", 0.505f, 0.615f);
            decisionStatusText = CreateDecisionSection("DecisionStatus", "STATUS", 0.375f, 0.485f);
            decisionRewardSignalText = CreateDecisionSection("DecisionRewardSignal", "REWARD SIGNAL", 0.245f, 0.355f);
            decisionAvailableLinksText = CreateDecisionSection("DecisionAvailableLinks", "AVAILABLE LINKS", 0.065f, 0.225f);
        }

        if (decisionAvailableLinksText != null)
        {
            decisionAvailableLinksText.fontSize = decisionPanelUsesSkin ? 10 : 9;
            decisionAvailableLinksText.lineSpacing = decisionPanelUsesSkin ? 0.88f : 0.92f;
        }

        UpdateDecisionPanel(null);
    }

    private static void SetChildrenWithPrefixActive(RectTransform parent, string prefix, bool active)
    {
        if (parent == null)
            return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
                child.gameObject.SetActive(active);
        }
    }

    private void BuildDecisionPanelFrame(RectTransform panel)
    {
        if (panel == null)
            return;

        Color frameColor = new Color(accent.r, accent.g, accent.b, 0.28f);
        Color frameDim = new Color(accent.r, accent.g, accent.b, 0.13f);
        Color hotMark = new Color(1f, 0.34f, 0.70f, 0.075f);

        CreateHudSegment(panel, "DecisionFrameTopLeft", new Vector2(0.120f, 0.975f), new Vector2(58f, 1.4f), 0f, frameColor);
        CreateHudSegment(panel, "DecisionFrameTopLeftCut", new Vector2(0.290f, 0.962f), new Vector2(38f, 1.2f), -27f, frameDim);
        CreateHudSegment(panel, "DecisionFrameTopBridgeA", new Vector2(0.430f, 0.958f), new Vector2(62f, 1.2f), 0f, frameColor);
        CreateHudSegment(panel, "DecisionFrameTopBridgeB", new Vector2(0.600f, 0.958f), new Vector2(36f, 1.0f), 0f, frameDim);
        CreateHudSegment(panel, "DecisionFrameTopRightCut", new Vector2(0.760f, 0.962f), new Vector2(38f, 1.2f), 27f, frameDim);
        CreateHudSegment(panel, "DecisionFrameTopRight", new Vector2(0.890f, 0.975f), new Vector2(54f, 1.4f), 0f, frameColor);

        CreateHudSegment(panel, "DecisionFrameLeftUpper", new Vector2(0.040f, 0.790f), new Vector2(1.2f, 118f), 0f, frameDim);
        CreateHudSegment(panel, "DecisionFrameLeftLower", new Vector2(0.040f, 0.240f), new Vector2(1.2f, 118f), 0f, frameDim);
        CreateHudSegment(panel, "DecisionFrameRightUpper", new Vector2(0.960f, 0.790f), new Vector2(1.2f, 118f), 0f, frameDim);
        CreateHudSegment(panel, "DecisionFrameRightLower", new Vector2(0.960f, 0.240f), new Vector2(1.2f, 118f), 0f, frameDim);

        CreateHudSegment(panel, "DecisionFrameBottomLeft", new Vector2(0.155f, 0.043f), new Vector2(74f, 1.2f), 0f, frameDim);
        CreateHudSegment(panel, "DecisionFrameBottomRight", new Vector2(0.845f, 0.043f), new Vector2(74f, 1.2f), 0f, frameDim);
        CreateHudSegment(panel, "DecisionFrameBottomBridge", new Vector2(0.500f, 0.040f), new Vector2(84f, 1.0f), 0f, new Color(accent.r, accent.g, accent.b, 0.085f));

        CreateHudSegment(panel, "DecisionFrameTopTickA", new Vector2(0.185f, 0.920f), new Vector2(18f, 2.4f), 0f, frameColor);
        CreateHudSegment(panel, "DecisionFrameTopTickB", new Vector2(0.665f, 0.920f), new Vector2(18f, 2.4f), 0f, frameColor);
        CreateHudSegment(panel, "DecisionFrameDataSlitA", new Vector2(0.270f, 0.915f), new Vector2(48f, 1.0f), 0f, new Color(accent.r, accent.g, accent.b, 0.070f));
        CreateHudSegment(panel, "DecisionFrameDataSlitB", new Vector2(0.790f, 0.915f), new Vector2(48f, 1.0f), 0f, new Color(accent.r, accent.g, accent.b, 0.070f));
        CreateHudSegment(panel, "DecisionFrameGlitchMark", new Vector2(0.875f, 0.895f), new Vector2(30f, 1.4f), 0f, hotMark);
    }

    private void CreateHudSegment(
        RectTransform parent,
        string objectName,
        Vector2 anchor,
        Vector2 size,
        float zRotation,
        Color color)
    {
        RectTransform segment = GetOrCreateRect(objectName, parent);
        segment.anchorMin = anchor;
        segment.anchorMax = anchor;
        segment.pivot = new Vector2(0.5f, 0.5f);
        segment.anchoredPosition = Vector2.zero;
        segment.sizeDelta = size;
        segment.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        segment.gameObject.SetActive(true);
        ConfigureOverlayImage(segment, null, color, false);
    }

    private Text CreateDecisionSection(string objectName, string label, float yMin, float yMax)
    {
        bool isAvailableLinksSection = decisionPanelUsesSkin && objectName == "DecisionAvailableLinks";
        bool sectionExisted = HasChild(decisionPanel, objectName);
        RectTransform section = GetOrCreateRect(objectName, decisionPanel);
        if (!ShouldPreserveExistingLayout(sectionExisted))
        {
            section.anchorMin = decisionPanelUsesSkin ? new Vector2(0.165f, yMin) : new Vector2(0.070f, yMin);
            section.anchorMax = decisionPanelUsesSkin ? new Vector2(0.855f, yMax) : new Vector2(0.935f, yMax);
            section.offsetMin = Vector2.zero;
            section.offsetMax = Vector2.zero;
        }

        Image sectionImage = section.GetComponent<Image>() ?? section.gameObject.AddComponent<Image>();
        sectionImage.color = decisionPanelUsesSkin
            ? new Color(0.001f, 0.008f, 0.012f, 0.10f)
            : new Color(0.002f, 0.010f, 0.014f, 0.52f);
        sectionImage.raycastTarget = false;

        Outline sectionOutline = section.GetComponent<Outline>() ?? section.gameObject.AddComponent<Outline>();
        sectionOutline.effectColor = decisionPanelUsesSkin
            ? new Color(accent.r, accent.g, accent.b, 0.020f)
            : new Color(accent.r, accent.g, accent.b, 0.070f);
        sectionOutline.effectDistance = decisionPanelUsesSkin ? new Vector2(0.4f, -0.4f) : new Vector2(0.8f, -0.8f);

        float accentAlpha = decisionPanelUsesSkin ? 0.060f : 0.18f;
        float traceAlpha = decisionPanelUsesSkin ? 0.016f : 0.045f;
        float tickAlpha = decisionPanelUsesSkin ? 0.040f : 0.115f;
        CreateHudLine(section, "SectionAccent", new Vector2(0.010f, 0.955f), new Vector2(0.220f, 0.955f), new Vector2(0f, 1.0f), new Color(accent.r, accent.g, accent.b, accentAlpha));
        CreateHudLine(section, "SectionUnderTrace", new Vector2(0.250f, 0.955f), new Vector2(0.960f, 0.955f), new Vector2(0f, 0.75f), new Color(accent.r, accent.g, accent.b, traceAlpha));
        CreateHudSegment(section, "SectionRightTick", new Vector2(0.982f, 0.500f), new Vector2(1.0f, 14f), 0f, new Color(accent.r, accent.g, accent.b, tickAlpha));

        bool labelExisted = HasChild(section, "Label");
        Text labelText = GetOrCreateText("Label", section, decisionPanelUsesSkin ? 8 : 7, FontStyle.Bold, TextAnchor.UpperLeft);
        if (!ShouldPreserveExistingLayout(labelExisted))
        {
            labelText.rectTransform.anchorMin = isAvailableLinksSection
                ? new Vector2(0.050f, 0.750f)
                : (decisionPanelUsesSkin ? new Vector2(0.050f, 0.590f) : new Vector2(0.060f, 0.590f));
            labelText.rectTransform.anchorMax = isAvailableLinksSection
                ? new Vector2(0.930f, 0.930f)
                : (decisionPanelUsesSkin ? new Vector2(0.930f, 0.925f) : new Vector2(0.925f, 0.915f));
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;
            labelText.fontSize = decisionPanelUsesSkin ? 8 : 7;
        }
        labelText.text = label;
        labelText.color = decisionPanelUsesSkin
            ? new Color(0.48f, 0.90f, 0.96f, 0.50f)
            : new Color(0.42f, 0.86f, 0.92f, 0.42f);
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;

        bool valueExisted = HasChild(section, "Value");
        Text valueText = GetOrCreateText("Value", section, decisionPanelUsesSkin ? 11 : 10, FontStyle.Bold, TextAnchor.UpperLeft);
        if (!ShouldPreserveExistingLayout(valueExisted))
        {
            valueText.rectTransform.anchorMin = isAvailableLinksSection
                ? new Vector2(0.050f, 0.070f)
                : (decisionPanelUsesSkin ? new Vector2(0.050f, 0.125f) : new Vector2(0.060f, 0.060f));
            valueText.rectTransform.anchorMax = isAvailableLinksSection
                ? new Vector2(0.930f, 0.720f)
                : (decisionPanelUsesSkin ? new Vector2(0.930f, 0.670f) : new Vector2(0.925f, 0.640f));
            valueText.rectTransform.offsetMin = Vector2.zero;
            valueText.rectTransform.offsetMax = Vector2.zero;
            valueText.fontSize = decisionPanelUsesSkin ? 11 : 10;
        }
        valueText.color = decisionPanelUsesSkin
            ? new Color(0.78f, 0.94f, 0.98f, 0.82f)
            : new Color(0.74f, 0.90f, 0.92f, 0.74f);
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        valueText.horizontalOverflow = HorizontalWrapMode.Wrap;

        return valueText;
    }

    private void StyleMapRoot()
    {
        if (mapRoot == null)
            return;

        if (!ShouldPreserveExistingLayout(mapRoot.parent != null))
        {
            mapRoot.anchorMin = new Vector2(0.045f, 0.145f);
            mapRoot.anchorMax = new Vector2(0.755f, 0.83f);
            mapRoot.offsetMin = Vector2.zero;
            mapRoot.offsetMax = Vector2.zero;
        }

        RectTransform mapBack = GetOrCreateRect("GridLinkStyleBackground", mapRoot);
        mapBack.anchorMin = Vector2.zero;
        mapBack.anchorMax = Vector2.one;
        mapBack.offsetMin = Vector2.zero;
        mapBack.offsetMax = Vector2.zero;
        mapBack.SetAsFirstSibling();
        mapBack.gameObject.SetActive(true);
        Color mapBackColor = routeGraphBackgroundSprite != null
            ? new Color(0.70f, 0.96f, 1f, 0.46f)
            : new Color(0.000f, 0.006f, 0.014f, 0.24f);
        ConfigureOverlayImage(mapBack, routeGraphBackgroundSprite, mapBackColor, false);

        RectTransform mapVeil = GetOrCreateRect("GridLinkBackgroundVeil", mapRoot);
        mapVeil.anchorMin = Vector2.zero;
        mapVeil.anchorMax = Vector2.one;
        mapVeil.offsetMin = Vector2.zero;
        mapVeil.offsetMax = Vector2.zero;
        mapVeil.SetSiblingIndex(Mathf.Min(1, mapRoot.childCount - 1));
        mapVeil.gameObject.SetActive(routeGraphBackgroundSprite != null);
        ConfigureOverlayImage(mapVeil, null, new Color(0.000f, 0.004f, 0.010f, 0.20f), false);

        RectTransform radar = GetOrCreateRect("GridRadarGate", mapRoot);
        radar.gameObject.SetActive(false);

        DisableMapScanLines();

        if (connectionRoot != null)
        {
            connectionRoot.anchorMin = Vector2.zero;
            connectionRoot.anchorMax = Vector2.one;
            connectionRoot.offsetMin = Vector2.zero;
            connectionRoot.offsetMax = Vector2.zero;
            connectionRoot.SetAsLastSibling();
        }

        if (nodeRoot != null)
        {
            nodeRoot.anchorMin = Vector2.zero;
            nodeRoot.anchorMax = Vector2.one;
            nodeRoot.offsetMin = Vector2.zero;
            nodeRoot.offsetMax = Vector2.zero;
            nodeRoot.SetAsLastSibling();
        }
    }

    private void DisableMapScanLines()
    {
        if (mapRoot == null)
            return;

        for (int i = 0; i < 9; i++)
        {
            RectTransform scanLine = FindRect(mapRoot, $"GridScanLine_{i:00}");
            if (scanLine != null)
                scanLine.gameObject.SetActive(false);
        }

        for (int i = 0; i < 7; i++)
        {
            RectTransform scanLine = FindRect(mapRoot, $"GridAxisLine_{i:00}");
            if (scanLine != null)
                scanLine.gameObject.SetActive(false);
        }
    }

    private void StyleNewRunButton()
    {
        if (newRunButton == null)
            return;

        RectTransform buttonRect = newRunButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-30f, -64f);
        buttonRect.sizeDelta = new Vector2(126f, 32f);

        Image image = newRunButton.GetComponent<Image>() ?? newRunButton.gameObject.AddComponent<Image>();
        image.sprite = gridButtonFrameSprite;
        image.preserveAspect = false;
        image.color = new Color(0.018f, 0.072f, 0.092f, 0.74f);

        Text text = newRunButton.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.font = defaultFont;
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.74f, 0.98f, 1f, 0.92f);
        }
    }

    private static void ConfigureOverlayImage(RectTransform rect, Sprite sprite, Color color, bool preserveAspect)
    {
        Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.color = color;
        image.raycastTarget = false;
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

    // Resolves the runtime-updated Text/panel references straight from the
    // authored hierarchy, without touching any layout. Used when
    // preserveSceneAuthoredLayout is on so Play mode keeps the scene's layout.
    private void WireAuthoredReferences()
    {
        if (canvas == null)
            return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform frame = FindRect(canvasRect, "GridFrame");
        if (frame == null && mapRoot != null)
            frame = mapRoot.parent as RectTransform;
        if (frame == null)
            return;

        computeBankValueText = FindSectionValueText(frame, "StatusComputeBank");
        payloadBufferValueText = FindSectionValueText(frame, "StatusPayloadBuffer");
        depthValueText = FindSectionValueText(frame, "StatusDepth");

        decisionPanel = FindRect(frame, "DecisionPanel");
        if (decisionPanel != null)
        {
            decisionPanelUsesSkin = decisionPanelSkinSprite != null;
            decisionModeText = FindText(decisionPanel, "DecisionMode");
            decisionCurrentNodeText = FindSectionValueText(decisionPanel, "DecisionCurrentNode");
            decisionNodeTypeText = FindSectionValueText(decisionPanel, "DecisionNodeType");
            decisionRiskLevelText = FindSectionValueText(decisionPanel, "DecisionRiskLevel");
            decisionStatusText = FindSectionValueText(decisionPanel, "DecisionStatus");
            decisionRewardSignalText = FindSectionValueText(decisionPanel, "DecisionRewardSignal");
            decisionAvailableLinksText = FindSectionValueText(decisionPanel, "DecisionAvailableLinks");
        }
    }

    private static Text FindSectionValueText(RectTransform parent, string sectionName)
    {
        RectTransform section = FindRect(parent, sectionName);
        return section != null ? FindText(section, "Value") : null;
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
        legendText.text = "CURRENT HIGHLIGHT / AVAILABLE LINKS / LOCKED | D1-D5 threat | TARGET = boss";

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

    private bool ShouldPreserveExistingLayout(bool objectExisted)
    {
        return preserveSceneAuthoredLayout && objectExisted;
    }

    private static bool HasChild(RectTransform parent, string objectName)
    {
        return parent != null && parent.Find(objectName) != null;
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
        Transform existing = parent != null ? parent.Find(objectName) : null;
        bool preserveExistingText = preserveSceneAuthoredLayout &&
                                    existing != null &&
                                    existing.GetComponent<Text>() != null;
        RectTransform rect = GetOrCreateRect(objectName, parent);
        Text text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
        if (!preserveExistingText)
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
            titleText.text = "GRID LINK // ROUTE GRAPH";
        if (seedText != null)
            seedText.text = string.Empty;
        UpdateTopStatusModules(manager != null ? manager.currentRunGraph : null);
    }

    private void UpdateTopStatusModules(GridGraph graph)
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
                if (IsCurrentNode(node))
                    currentLayer = node.layer;
            }
        }

        int payloadCount = manager != null && manager.payload != null ? manager.payload.Count : 0;
        int compute = manager != null ? manager.computeBalance : 0;

        if (computeBankValueText != null)
            computeBankValueText.text = compute.ToString("0000");
        if (payloadBufferValueText != null)
            payloadBufferValueText.text = payloadCount.ToString("00");
        if (depthValueText != null)
            depthValueText.text = $"{currentLayer + 1}/{Mathf.Max(1, maxLayer + 1)}";
    }

    private void SetHint(string message)
    {
        if (hintText != null)
            hintText.text = message;
    }

    private void RefreshLegend()
    {
        if (legendText != null)
            legendText.text = "CURRENT HIGHLIGHT / AVAILABLE LINKS / ? UNKNOWN   |   D1-D5 THREAT   |   TARGET = BOSS";
    }

    private void ApplyResolvedTextStyles()
    {
        ApplyTextFont(titleText, 22);
        ApplyTextFont(seedText, 13);
        ApplyTextFont(hintText, 14);
        ApplyTextFont(legendText, 12);
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
        return "CREDITS             PAYLOAD BUFFER        CURRENT DEPTH";
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
                return "> SHOP | Spend credits on current-run buffs. Rewards: damage, CP, shield, AlgoMon EXP, or high-risk trade-off offers.";

            return BuildSelectedNodeDetail(selectedNode);
        }

        if (manager == null || string.IsNullOrEmpty(manager.currentNodeId))
            return "> System initialized. Waiting for input...";

        GridNode currentNode = CurrentRouteNode();
        return $"> Located [{NodeRouteLabelFor(currentNode)}]. Select a NEXT node. Read node label for type and D1-D5 danger.";
    }

    private string BuildPreviewHint(GridNode node)
    {
        if (node == null)
            return BuildCommandHint();
        if (StateFor(node) == GridNodeVisualState.Unknown)
            return "> PREVIEW UNKNOWN | Future route layer encrypted. Advance the cursor to decode this layer.";

        string availability = NodeAvailabilityLabel(node);
        return $"> PREVIEW {availability} | {BuildSelectedNodeDetail(node).TrimStart('>', ' ')}";
    }

    private string NodeAvailabilityLabel(GridNode node)
    {
        switch (StateFor(node))
        {
            case GridNodeVisualState.Current:
                return "CURRENT";
            case GridNodeVisualState.NextAvailable:
                return "NEXT";
            case GridNodeVisualState.Target:
                return "TARGET";
            case GridNodeVisualState.Visited:
                return "VISITED";
            case GridNodeVisualState.Unknown:
                return "UNKNOWN";
            default:
                return "LOCKED";
        }
    }

    private void UpdateDecisionPanel(GridNode previewNode)
    {
        if (decisionPanel == null)
            return;

        if (manager == null)
            manager = ResolveManager();

        GridNode currentNode = CurrentRouteNode();
        GridNode node = previewNode != null ? previewNode : currentNode;
        bool scanning = previewNode != null;
        if (node == null)
        {
            SetDecisionPanelOffline();
            return;
        }

        if (decisionModeText != null)
            decisionModeText.text = scanning ? "SCAN TARGET" : "CURRENT NODE";

        SetDecisionText(
            decisionCurrentNodeText,
            NodeRouteLabelFor(currentNode),
            textBright);

        SetDecisionText(
            decisionNodeTypeText,
            $"{ShortTypeLabelFor(node.nodeType)} // {EncounterIdentityFor(node)}",
            NodeAccentFor(node));

        SetDecisionText(
            decisionRiskLevelText,
            BuildRiskLevelLine(node),
            RiskColorFor(node));

        SetDecisionText(
            decisionStatusText,
            BuildStatusLine(node),
            StatusColorFor(node));

        SetDecisionText(
            decisionRewardSignalText,
            RewardIdentityFor(node.nodeType).ToUpperInvariant(),
            new Color(0.74f, 0.92f, 0.96f, 0.84f));

        SetDecisionText(
            decisionAvailableLinksText,
            BuildAvailableLinksLine(currentNode),
            new Color(0.72f, 0.98f, 1f, 0.86f));
    }

    private void SetDecisionPanelOffline()
    {
        if (decisionModeText != null)
            decisionModeText.text = "OFFLINE";

        SetDecisionText(decisionCurrentNodeText, "NO ROUTE DATA", textDim);
        SetDecisionText(decisionNodeTypeText, "NO SIGNAL", textDim);
        SetDecisionText(decisionRiskLevelText, "UNKNOWN", textDim);
        SetDecisionText(decisionStatusText, "WAITING FOR RUN", textDim);
        SetDecisionText(decisionRewardSignalText, "NO REWARD SIGNAL", textDim);
        SetDecisionText(decisionAvailableLinksText, "NO LINKS", textDim);
    }

    private static void SetDecisionText(Text text, string value, Color color)
    {
        if (text == null)
            return;

        text.text = value;
        text.color = color;
    }

    private string BuildRiskLevelLine(GridNode node)
    {
        if (node == null)
            return "UNKNOWN";
        if (!ThreatTierRules.IsEncounterNode(node.nodeType))
            return node.nodeType == NodeType.Start ? "ENTRY SAFE" : "SAFE";

        int danger = Mathf.Clamp(node.dangerRating, 1, ThreatTierRules.MaxTier);
        DangerLevelStyle dangerStyle = DangerLevelStyleFor(danger);
        return $"D{danger} // {dangerStyle.Label}";
    }

    private Color RiskColorFor(GridNode node)
    {
        if (node == null || !ThreatTierRules.IsEncounterNode(node.nodeType))
            return new Color(0.58f, 0.82f, 0.88f, 0.72f);

        Color riskColor = DangerLevelColorFor(node.dangerRating);
        return new Color(riskColor.r, riskColor.g, riskColor.b, 0.92f);
    }

    private string BuildStatusLine(GridNode node)
    {
        switch (StateFor(node))
        {
            case GridNodeVisualState.Current:
                return "CURRENT // ROUTE CURSOR ONLINE";
            case GridNodeVisualState.NextAvailable:
                return "NEXT // VALID LINK";
            case GridNodeVisualState.Target:
                return manager != null && manager.IsNodeAvailable(node.id)
                    ? "TARGET // VALID FINAL LINK"
                    : "TARGET // BOSS SIGNAL";
            case GridNodeVisualState.Visited:
                return "VISITED // TRACE RECORDED";
            case GridNodeVisualState.Unknown:
                return "UNKNOWN // LAYER ENCRYPTED";
            default:
                return "LOCKED // NO DIRECT LINK";
        }
    }

    private Color StatusColorFor(GridNode node)
    {
        switch (StateFor(node))
        {
            case GridNodeVisualState.Current:
                return textBright;
            case GridNodeVisualState.NextAvailable:
                return lineAvailable;
            case GridNodeVisualState.Target:
                return NodeAccentFor(node);
            case GridNodeVisualState.Visited:
                return lineVisited;
            case GridNodeVisualState.Unknown:
                return new Color(textDim.r, textDim.g, textDim.b, 0.46f);
            default:
                return new Color(textDim.r, textDim.g, textDim.b, 0.62f);
        }
    }

    private string BuildAvailableLinksLine(GridNode node)
    {
        if (node == null ||
            manager == null ||
            manager.currentRunGraph == null)
            return "NO OUTGOING LINKS";

        StringBuilder builder = new StringBuilder();
        bool nodeIsCurrent = manager != null && node.id == manager.currentNodeId;
        List<string> linkNodeIds = nodeIsCurrent
            ? manager.GetAvailableNodeIds()
            : node.outgoingNodeIds != null ? new List<string>(node.outgoingNodeIds) : new List<string>();
        if (linkNodeIds.Count == 0)
            return "NO OUTGOING LINKS";

        int rendered = 0;
        for (int i = 0; i < linkNodeIds.Count; i++)
        {
            GridNode target = manager.currentRunGraph.GetNode(linkNodeIds[i]);
            if (target == null)
                continue;

            string gate = nodeIsCurrent
                ? "OPEN"
                : "TRACE";
            string danger = ThreatTierRules.IsEncounterNode(target.nodeType)
                ? $" D{Mathf.Clamp(target.dangerRating, 1, ThreatTierRules.MaxTier)}"
                : string.Empty;
            builder.AppendLine($"{gate} {target.id.ToUpperInvariant()} // {ShortTypeLabelFor(target.nodeType)}{danger}");
            rendered++;
            if (rendered >= 4)
                break;
        }

        if (rendered == 0)
            return "NO OUTGOING LINKS";
        if (linkNodeIds.Count > rendered)
            builder.Append("+ MORE LINKS");

        return builder.ToString().TrimEnd();
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
            return node != null && node.nodeType == NodeType.Start ? "Entry safe" : "Safe utility node";

        int danger = Mathf.Clamp(node.dangerRating, 1, ThreatTierRules.MaxTier);
        return $"Danger D{danger} ({DangerLevelStyleFor(danger).Label.ToLowerInvariant()})";
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
                return "small AlgoMon EXP and credits";
            case NodeType.Hacker:
                return "higher AlgoMon EXP and credits, no Payload capture";
            case NodeType.Elite:
                return "above-average AlgoMon EXP and credits";
            case NodeType.Boss:
                return "high AlgoMon EXP, high-quality data, evolution data";
            case NodeType.Shop:
                return "spend credits for run buffs";
            case NodeType.Reboot:
                return "route flexibility, no combat reward";
            case NodeType.Start:
                return "begin or restart route planning";
            default:
                return "route progress";
        }
    }
}
