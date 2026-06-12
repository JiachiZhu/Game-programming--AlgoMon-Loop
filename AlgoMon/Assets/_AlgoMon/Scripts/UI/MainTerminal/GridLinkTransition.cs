using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class GridLinkTransition : MonoBehaviour
{
    private const float WarmupSeconds = 0.28f;
    private const float GraphBuildSeconds = 0.88f;
    private const float HandoffSeconds = 0.24f;
    private const float ExitSeconds = 0.52f;
    private const int SortingOrder = 32000;
    private const string FontResourcePath = "Fonts/NicoBold-Regular";
    private const string MainTerminalSpriteRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal";
    private const string CyberHudSpriteRoot = MainTerminalSpriteRoot + "/CyberpunkHUD";
    private const string PixelHudSpriteRoot = MainTerminalSpriteRoot + "/PixelUIHUD";

    private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

    public static bool IsActive { get; private set; }

    private readonly List<NodeView> nodes = new List<NodeView>();
    private readonly List<EdgeView> edges = new List<EdgeView>();
    private readonly List<RectTransform> dataBars = new List<RectTransform>();
    private readonly List<Image> dataBarImages = new List<Image>();
    private readonly List<CyberFrameGraphic> tunnelFrames = new List<CyberFrameGraphic>();

    private CanvasGroup canvasGroup;
    private RectTransform root;
    private RectTransform graphRoot;
    private RectTransform portalShellRect;
    private RectTransform portalRadarRect;
    private RectTransform portalGlyphRect;
    private RectTransform progressFillRect;
    private RectTransform scanBand;
    private Image veilImage;
    private Image portalBackplateImage;
    private Image portalShellImage;
    private Image portalRadarImage;
    private Image portalGlyphImage;
    private Image progressFillImage;
    private Image progressTrackImage;
    private Text titleText;
    private Text statusText;
    private Text progressText;
    private Font transitionFont;
    private Sprite portalBackplateSprite;
    private Sprite portalShellSprite;
    private Sprite portalRadarSprite;
    private Sprite portalGlyphSprite;
    private Sprite nodeFrameSprite;
    private Sprite nodeReticleSprite;
    private Sprite edgeLineSprite;
    private Sprite edgeHeadSprite;
    private Sprite progressTrackSprite;
    private Sprite progressFillSprite;

    private sealed class NodeView
    {
        public RectTransform Rect;
        public Image Core;
        public Image Glow;
        public Image Reticle;
        public Text Label;
        public float RevealAt;
    }

    private sealed class EdgeView
    {
        public RectTransform Root;
        public Image Line;
        public Image HeadA;
        public Image HeadB;
        public float RevealAt;
    }

    public static void Play(Action prepareRun, Action loadGrid)
    {
        if (IsActive)
            return;

        GameObject transitionObject = new GameObject(
            "GridLinkTransition",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(GridLinkTransition));
        DontDestroyOnLoad(transitionObject);

        GridLinkTransition transition = transitionObject.GetComponent<GridLinkTransition>();
        transition.BuildVisualTree();
        transition.Begin(prepareRun, loadGrid);
    }

    private void Begin(Action prepareRun, Action loadGrid)
    {
        IsActive = true;
        StartCoroutine(TransitionRoutine(prepareRun, loadGrid));
    }

    private void OnDestroy()
    {
        IsActive = false;
    }

    private IEnumerator TransitionRoutine(Action prepareRun, Action loadGrid)
    {
        bool prepared = false;
        float startTime = Time.unscaledTime;
        float preLoadTotal = WarmupSeconds + GraphBuildSeconds + HandoffSeconds;

        while (Time.unscaledTime - startTime < preLoadTotal)
        {
            float elapsed = Time.unscaledTime - startTime;
            float cover = Smooth01(Mathf.Clamp01(elapsed / WarmupSeconds));
            float graphProgress = Mathf.Clamp01((elapsed - WarmupSeconds * 0.45f) / GraphBuildSeconds);
            float displayedProgress = Mathf.Clamp01(elapsed / preLoadTotal);

            canvasGroup.alpha = cover;
            UpdateVisuals(elapsed, graphProgress, displayedProgress, false);

            if (!prepared && elapsed >= WarmupSeconds)
            {
                prepared = true;
                SetStatus("ROUTE GRAPH GENERATING");
                InvokeSafely(prepareRun, "Grid run preparation failed.");
            }

            yield return null;
        }

        // Render the finished 100% bar before the synchronous scene load, so the
        // hitch hides behind a completed frame instead of freezing the bar mid-fill.
        canvasGroup.alpha = 1f;
        UpdateVisuals(preLoadTotal, 1f, 1f, false);
        SetStatus("DIGITAL HANDOFF ACCEPTED");
        yield return null;
        yield return null;

        InvokeSafely(loadGrid, "Grid scene handoff failed.");
        yield return null;

        float exitStartTime = Time.unscaledTime;
        while (Time.unscaledTime - exitStartTime < ExitSeconds)
        {
            float exitElapsed = Time.unscaledTime - exitStartTime;
            float exitProgress = Mathf.Clamp01(exitElapsed / ExitSeconds);
            float fade = 1f - Smooth01(exitProgress);

            canvasGroup.alpha = fade;
            UpdateVisuals(preLoadTotal + exitElapsed, 1f, Mathf.Lerp(0.96f, 1f, exitProgress), true);
            if (graphRoot != null)
                graphRoot.localScale = Vector3.one * Mathf.Lerp(1.04f, 1.12f, exitProgress);

            yield return null;
        }

        IsActive = false;
        Destroy(gameObject);
    }

    private void InvokeSafely(Action action, string errorMessage)
    {
        if (action == null)
            return;

        try
        {
            action.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError(errorMessage);
            Debug.LogException(exception);
            SetStatus("LINK ERROR - CHECK CONSOLE");
        }
    }

    private void BuildVisualTree()
    {
        transitionFont = ResolveFont();
        LoadVisualSprites();

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        veilImage = CreateImage("DataVeil", root, new Color(0.004f, 0.014f, 0.028f, 0.98f), true);
        Stretch(veilImage.rectTransform);

        BuildScanlines();
        BuildDataBars();
        BuildPortalFrame();
        BuildTunnelFrames();
        BuildGraph();
        BuildTextAndProgress();
    }

    private void BuildScanlines()
    {
        for (int i = 0; i < 18; i++)
        {
            Image line = CreateImage("Scanline_" + i, root, new Color(0.24f, 0.96f, 1f, i % 3 == 0 ? 0.055f : 0.025f), false);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 28f + i * 39f);
            rect.sizeDelta = new Vector2(0f, i % 3 == 0 ? 2f : 1f);
        }

        scanBand = CreateRect("HandshakeScanSweep", root);
        scanBand.anchorMin = new Vector2(0f, 0.5f);
        scanBand.anchorMax = new Vector2(1f, 0.5f);
        scanBand.pivot = new Vector2(0.5f, 0.5f);
        scanBand.sizeDelta = new Vector2(0f, 54f);
        scanBand.anchoredPosition = new Vector2(0f, -420f);

        CreateSweepLine("SweepLead", scanBand, 0f, 2f, 0.24f);
        CreateSweepLine("SweepUpperEcho", scanBand, 14f, 1f, 0.075f);
        CreateSweepLine("SweepLowerEcho", scanBand, -12f, 1f, 0.055f);
        CreateSweepDash("SweepDash_A", scanBand, new Vector2(-430f, 8f), new Vector2(118f, 1f), 0.16f);
        CreateSweepDash("SweepDash_B", scanBand, new Vector2(-92f, -6f), new Vector2(84f, 1f), 0.12f);
        CreateSweepDash("SweepDash_C", scanBand, new Vector2(360f, 11f), new Vector2(148f, 1f), 0.14f);
    }

    private Image CreateSweepLine(string objectName, RectTransform parent, float y, float height, float alpha)
    {
        Image line = CreateImage(objectName, parent, new Color(0.22f, 0.92f, 1f, alpha), false);
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
        return line;
    }

    private Image CreateSweepDash(string objectName, RectTransform parent, Vector2 position, Vector2 size, float alpha)
    {
        Image dash = CreateImage(objectName, parent, new Color(0.66f, 1f, 0.96f, alpha), false);
        RectTransform rect = dash.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return dash;
    }

    private void BuildDataBars()
    {
        for (int i = 0; i < 16; i++)
        {
            float x = Mathf.Lerp(-600f, 600f, i / 15f);
            Image bar = CreateImage("DataStream_" + i, root, Color.white, false);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(i % 4 == 0 ? 4f : 2f, Mathf.Lerp(90f, 190f, (i % 5) / 4f));
            rect.anchoredPosition = new Vector2(x, -420f + (i % 4) * 38f);
            bar.color = ColorForStream(i, 0.18f);

            dataBars.Add(rect);
            dataBarImages.Add(bar);
        }
    }

    private void BuildPortalFrame()
    {
        Image backplate = CreateImage("GridLinkBackplate", root, new Color(0.02f, 0.12f, 0.19f, 0.055f), false);
        portalBackplateImage = backplate;
        RectTransform backplateRect = backplate.rectTransform;
        backplateRect.anchorMin = new Vector2(0.5f, 0.5f);
        backplateRect.anchorMax = new Vector2(0.5f, 0.5f);
        backplateRect.pivot = new Vector2(0.5f, 0.5f);
        backplateRect.anchoredPosition = new Vector2(0f, -8f);
        backplateRect.sizeDelta = new Vector2(860f, 330f);
        if (portalBackplateSprite != null)
        {
            backplate.sprite = portalBackplateSprite;
            backplate.type = Image.Type.Sliced;
        }
        backplate.gameObject.SetActive(false);

        Image shell = CreateImage("CyberHudPortalShell", root, new Color(0.12f, 0.90f, 1f, 0.34f), false);
        portalShellImage = shell;
        portalShellRect = shell.rectTransform;
        portalShellRect.anchorMin = new Vector2(0.5f, 0.5f);
        portalShellRect.anchorMax = new Vector2(0.5f, 0.5f);
        portalShellRect.pivot = new Vector2(0.5f, 0.5f);
        portalShellRect.anchoredPosition = new Vector2(0f, -6f);
        portalShellRect.sizeDelta = new Vector2(900f, 300f);
        if (portalShellSprite != null)
        {
            shell.sprite = portalShellSprite;
            shell.type = Image.Type.Simple;
            shell.preserveAspect = false;
        }
        shell.gameObject.SetActive(false);

        Image radar = CreateImage("RouteRadarGate", root, new Color(0.18f, 1f, 0.96f, 0.30f), false);
        portalRadarImage = radar;
        portalRadarRect = radar.rectTransform;
        portalRadarRect.anchorMin = new Vector2(0.5f, 0.5f);
        portalRadarRect.anchorMax = new Vector2(0.5f, 0.5f);
        portalRadarRect.pivot = new Vector2(0.5f, 0.5f);
        portalRadarRect.anchoredPosition = new Vector2(0f, -4f);
        portalRadarRect.sizeDelta = new Vector2(420f, 172f);
        if (portalRadarSprite != null)
        {
            radar.sprite = portalRadarSprite;
            radar.type = Image.Type.Simple;
            radar.preserveAspect = false;
        }

        Image glyph = CreateImage("GridLinkCoreGlyph", root, new Color(0.72f, 1f, 0.95f, 0.54f), false);
        portalGlyphImage = glyph;
        portalGlyphRect = glyph.rectTransform;
        portalGlyphRect.anchorMin = new Vector2(0.5f, 0.5f);
        portalGlyphRect.anchorMax = new Vector2(0.5f, 0.5f);
        portalGlyphRect.pivot = new Vector2(0.5f, 0.5f);
        portalGlyphRect.anchoredPosition = new Vector2(0f, -4f);
        portalGlyphRect.sizeDelta = new Vector2(112f, 112f);
        if (portalGlyphSprite != null)
        {
            glyph.sprite = portalGlyphSprite;
            glyph.type = Image.Type.Simple;
            glyph.preserveAspect = true;
        }
    }

    private void BuildTunnelFrames()
    {
        Vector2[] sizes =
        {
            new Vector2(360f, 190f),
            new Vector2(520f, 290f),
            new Vector2(700f, 390f),
            new Vector2(900f, 500f)
        };

        for (int i = 0; i < sizes.Length; i++)
        {
            RectTransform rect = CreateRect("TunnelFrame_" + i, root);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizes[i];
            rect.anchoredPosition = Vector2.zero;

            CyberFrameGraphic frame = rect.gameObject.AddComponent<CyberFrameGraphic>();
            frame.raycastTarget = false;
            frame.FillColor = new Color(0.005f, 0.035f, 0.066f, 0.08f);
            frame.BorderColor = new Color(0.10f, 0.80f, 1f, 0.30f);
            frame.AccentColor = new Color(1f, 0.20f, 0.56f, 0.28f);
            frame.CornerCut = 28f;
            frame.BorderThickness = 2f;
            tunnelFrames.Add(frame);
        }
    }

    private void BuildGraph()
    {
        graphRoot = CreateRect("RouteGraphAssembly", root);
        graphRoot.anchorMin = new Vector2(0.5f, 0.5f);
        graphRoot.anchorMax = new Vector2(0.5f, 0.5f);
        graphRoot.pivot = new Vector2(0.5f, 0.5f);
        graphRoot.sizeDelta = new Vector2(900f, 420f);
        graphRoot.anchoredPosition = new Vector2(0f, -10f);
        graphRoot.localScale = Vector3.one * 0.92f;

        Vector2[] positions =
        {
            new Vector2(0f, -180f),
            new Vector2(-255f, -92f),
            new Vector2(0f, -76f),
            new Vector2(250f, -96f),
            new Vector2(-330f, 28f),
            new Vector2(-116f, 54f),
            new Vector2(116f, 54f),
            new Vector2(330f, 28f),
            new Vector2(-132f, 166f),
            new Vector2(132f, 166f),
            new Vector2(0f, 218f)
        };
        string[] labels =
        {
            "START", "D1", "HACK", "SHOP", "D2", "ELITE", "D3", "RBT", "D4", "GATE", "BOSS"
        };
        int[,] connections =
        {
            { 0, 1 }, { 0, 2 }, { 0, 3 },
            { 1, 4 }, { 1, 5 }, { 2, 5 }, { 2, 6 }, { 3, 6 }, { 3, 7 },
            { 4, 8 }, { 5, 8 }, { 5, 9 }, { 6, 9 }, { 7, 9 },
            { 8, 10 }, { 9, 10 }
        };

        for (int i = 0; i < connections.GetLength(0); i++)
        {
            int from = connections[i, 0];
            int to = connections[i, 1];
            edges.Add(CreateEdge("Edge_" + from + "_" + to, positions[from], positions[to], i));
        }

        for (int i = 0; i < positions.Length; i++)
            nodes.Add(CreateNode("Node_" + labels[i], positions[i], labels[i], i));
    }

    private EdgeView CreateEdge(string objectName, Vector2 start, Vector2 end, int index)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        RectTransform edgeRoot = CreateRect(objectName, graphRoot);
        edgeRoot.anchorMin = new Vector2(0.5f, 0.5f);
        edgeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        edgeRoot.pivot = new Vector2(0f, 0.5f);
        edgeRoot.anchoredPosition = start;
        edgeRoot.sizeDelta = new Vector2(length, 18f);
        edgeRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        edgeRoot.localScale = new Vector3(0f, 1f, 1f);

        Image line = CreateImage("Line", edgeRoot, new Color(0.18f, 0.88f, 1f, 0.72f), false);
        if (edgeLineSprite != null)
        {
            line.sprite = edgeLineSprite;
            line.type = Image.Type.Simple;
            line.preserveAspect = false;
        }
        line.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        line.rectTransform.anchoredPosition = Vector2.zero;
        line.rectTransform.sizeDelta = new Vector2(0f, edgeLineSprite != null ? 8f : 3f);

        Image headA = CreateEdgeHead("HeadA", edgeRoot, length, 34f);
        Image headB = CreateEdgeHead("HeadB", edgeRoot, length, -34f);

        return new EdgeView
        {
            Root = edgeRoot,
            Line = line,
            HeadA = headA,
            HeadB = headB,
            RevealAt = 0.10f + index * 0.033f
        };
    }

    private Image CreateEdgeHead(string objectName, RectTransform parent, float length, float rotation)
    {
        Image head = CreateImage(objectName, parent, new Color(0.56f, 1f, 0.98f, 0.86f), false);
        if (edgeHeadSprite != null)
        {
            head.sprite = edgeHeadSprite;
            head.type = Image.Type.Simple;
            head.preserveAspect = true;
        }
        RectTransform rect = head.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(Mathf.Max(0f, length - 7f), 0f);
        rect.sizeDelta = edgeHeadSprite != null ? new Vector2(22f, 18f) : new Vector2(14f, 3f);
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return head;
    }

    private NodeView CreateNode(string objectName, Vector2 position, string label, int index)
    {
        RectTransform rect = CreateRect(objectName, graphRoot);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(index == 10 ? 76f : 54f, index == 10 ? 46f : 38f);
        rect.localScale = Vector3.one * 0.25f;

        Image glow = CreateImage("Glow", rect, new Color(0.18f, 0.86f, 1f, 0f), false);
        if (portalBackplateSprite != null)
        {
            glow.sprite = portalBackplateSprite;
            glow.type = Image.Type.Sliced;
        }
        glow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        glow.rectTransform.sizeDelta = rect.sizeDelta + new Vector2(28f, 22f);
        glow.rectTransform.anchoredPosition = Vector2.zero;

        Image core = CreateImage("Core", rect, NodeColor(index, 0f), false);
        Stretch(core.rectTransform);
        if (nodeFrameSprite != null)
        {
            core.sprite = nodeFrameSprite;
            core.type = Image.Type.Simple;
            core.preserveAspect = false;
        }
        else
        {
            Outline outline = core.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.24f, 0.92f, 1f, 0.74f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
        }

        Image reticle = CreateImage("Reticle", rect, new Color(0.80f, 1f, 0.96f, 0f), false);
        if (nodeReticleSprite != null)
        {
            reticle.sprite = nodeReticleSprite;
            reticle.type = Image.Type.Simple;
            reticle.preserveAspect = false;
        }
        reticle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        reticle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        reticle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        reticle.rectTransform.anchoredPosition = Vector2.zero;
        reticle.rectTransform.sizeDelta = rect.sizeDelta + new Vector2(index == 10 ? 26f : 20f, index == 10 ? 20f : 16f);

        Text nodeLabel = CreateText("Label", rect, index == 10 ? 13 : 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(nodeLabel.rectTransform);
        nodeLabel.text = label;
        nodeLabel.color = new Color(0.90f, 1f, 0.98f, 0f);

        return new NodeView
        {
            Rect = rect,
            Core = core,
            Glow = glow,
            Reticle = reticle,
            Label = nodeLabel,
            RevealAt = 0.04f + index * 0.065f
        };
    }

    private void BuildTextAndProgress()
    {
        titleText = CreateText("Title", root, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        titleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        titleText.rectTransform.sizeDelta = new Vector2(720f, 42f);
        titleText.text = "GRID LINK // ROUTE GRAPH";
        titleText.color = new Color(0.88f, 1f, 0.98f, 0f);

        statusText = CreateText("Status", root, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        statusText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, 118f);
        statusText.rectTransform.sizeDelta = new Vector2(740f, 28f);
        statusText.text = "OPENING DIGITAL ROUTE";
        statusText.color = new Color(0.70f, 0.92f, 1f, 0f);

        RectTransform progressTrack = CreateRect("ProgressTrack", root);
        progressTrack.anchorMin = new Vector2(0.5f, 0f);
        progressTrack.anchorMax = new Vector2(0.5f, 0f);
        progressTrack.pivot = new Vector2(0.5f, 0.5f);
        progressTrack.anchoredPosition = new Vector2(0f, 90f);
        progressTrack.sizeDelta = new Vector2(560f, 22f);
        progressTrackImage = progressTrack.gameObject.AddComponent<Image>();
        progressTrackImage.color = new Color(0.10f, 0.74f, 0.94f, 0.72f);
        progressTrackImage.raycastTarget = false;
        if (progressTrackSprite != null)
        {
            progressTrackImage.sprite = progressTrackSprite;
            progressTrackImage.type = Image.Type.Simple;
            progressTrackImage.preserveAspect = false;
        }

        Image fill = CreateImage("ProgressFill", progressTrack, new Color(0.28f, 1f, 0.92f, 0.94f), false);
        progressFillImage = fill;
        if (progressFillSprite != null)
        {
            fill.sprite = progressFillSprite;
            fill.type = Image.Type.Tiled;
        }
        progressFillRect = fill.rectTransform;
        progressFillRect.anchorMin = new Vector2(0.025f, 0.28f);
        progressFillRect.anchorMax = new Vector2(0.025f, 0.72f);
        progressFillRect.pivot = new Vector2(0f, 0.5f);
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = Vector2.zero;

        progressText = CreateText("ProgressText", root, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        progressText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        progressText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        progressText.rectTransform.pivot = new Vector2(0.5f, 0f);
        progressText.rectTransform.anchoredPosition = new Vector2(0f, 62f);
        progressText.rectTransform.sizeDelta = new Vector2(520f, 24f);
        progressText.text = "000%";
        progressText.color = new Color(0.72f, 1f, 0.96f, 0f);
    }

    private void UpdateVisuals(float elapsed, float graphProgress, float displayedProgress, bool exiting)
    {
        float pulse = 0.5f + Mathf.Sin(elapsed * 9.5f) * 0.5f;
        float textAlpha = Smooth01(Mathf.Clamp01(elapsed / 0.36f));

        if (veilImage != null)
            veilImage.color = new Color(0.004f, 0.014f, 0.028f, exiting ? 0.96f : Mathf.Lerp(0.86f, 0.98f, textAlpha));

        if (titleText != null)
            titleText.color = new Color(0.82f + pulse * 0.10f, 1f, 0.98f, textAlpha);
        if (statusText != null)
            statusText.color = new Color(0.64f, 0.90f + pulse * 0.10f, 1f, textAlpha);
        if (progressText != null)
        {
            progressText.color = new Color(0.72f, 1f, 0.96f, textAlpha);
            progressText.text = Mathf.RoundToInt(Mathf.Clamp01(displayedProgress) * 100f).ToString("000") + "%";
        }
        if (progressFillRect != null)
            progressFillRect.anchorMax = new Vector2(Mathf.Lerp(0.025f, 0.975f, Mathf.Clamp01(displayedProgress)), 0.72f);
        if (progressTrackImage != null)
            progressTrackImage.color = Color.Lerp(new Color(0.08f, 0.50f, 0.72f, textAlpha * 0.56f), new Color(0.36f, 1f, 0.92f, textAlpha * 0.82f), pulse);
        if (progressFillImage != null)
            progressFillImage.color = Color.Lerp(new Color(0.14f, 0.86f, 1f, 0.86f), new Color(0.78f, 1f, 0.92f, 1f), pulse);

        if (scanBand != null)
        {
            float scan = Mathf.Repeat(elapsed * 0.58f, 1f);
            scanBand.anchoredPosition = new Vector2(0f, Mathf.Lerp(-430f, 430f, scan));
        }

        UpdatePortalFrame(elapsed, graphProgress, pulse, exiting);
        UpdateTunnelFrames(elapsed, graphProgress);
        UpdateDataBars(elapsed, graphProgress);
        UpdateEdges(graphProgress, pulse);
        UpdateNodes(elapsed, graphProgress, pulse);

        if (graphRoot != null && !exiting)
            graphRoot.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.04f, Smooth01(graphProgress));
    }

    private void UpdateTunnelFrames(float elapsed, float graphProgress)
    {
        for (int i = 0; i < tunnelFrames.Count; i++)
        {
            CyberFrameGraphic frame = tunnelFrames[i];
            if (frame == null)
                continue;

            float cycle = Mathf.Repeat(elapsed * 0.42f + i * 0.18f, 1f);
            float alpha = Mathf.Lerp(0.08f, 0.38f, Smooth01(graphProgress)) * (1f - cycle * 0.72f);
            frame.color = new Color(1f, 1f, 1f, alpha);
            frame.transform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.13f, cycle);
        }
    }

    private void UpdatePortalFrame(float elapsed, float graphProgress, float pulse, bool exiting)
    {
        float reveal = Smooth01(Mathf.Clamp01((graphProgress + 0.12f) / 0.82f));
        float exitBoost = exiting ? 1f : 0f;
        if (portalBackplateImage != null)
        {
            portalBackplateImage.color = new Color(
                0.02f,
                0.14f + pulse * 0.08f,
                0.20f + pulse * 0.10f,
                Mathf.Lerp(0.035f, 0.105f, reveal) * (1f - exitBoost * 0.38f));
        }

        if (portalShellRect != null)
        {
            float shellScale = Mathf.Lerp(1.18f, 0.96f, reveal) + pulse * 0.018f + exitBoost * 0.12f;
            portalShellRect.localScale = new Vector3(shellScale, shellScale, 1f);
        }
        if (portalShellImage != null)
        {
            portalShellImage.color = new Color(
                0.08f + pulse * 0.14f,
                0.72f + pulse * 0.22f,
                1f,
                Mathf.Lerp(0.020f, 0.075f, reveal) * (1f - exitBoost * 0.45f));
        }

        if (portalRadarRect != null)
        {
            float radarScale = Mathf.Lerp(0.84f, 1.06f, reveal) + pulse * 0.025f;
            portalRadarRect.localScale = new Vector3(radarScale, radarScale, 1f);
        }
        if (portalRadarImage != null)
        {
            portalRadarImage.color = new Color(0.14f, 0.95f, 1f, Mathf.Lerp(0.16f, 0.56f, reveal) * (1f - exitBoost * 0.62f));
        }

        if (portalGlyphRect != null)
        {
            portalGlyphRect.localRotation = Quaternion.Euler(0f, 0f, elapsed * -42f);
            float glyphScale = Mathf.Lerp(0.72f, 1.06f, reveal) + pulse * 0.04f;
            portalGlyphRect.localScale = new Vector3(glyphScale, glyphScale, 1f);
        }
        if (portalGlyphImage != null)
        {
            portalGlyphImage.color = new Color(0.70f + pulse * 0.18f, 1f, 0.94f, Mathf.Lerp(0.16f, 0.74f, reveal) * (1f - exitBoost * 0.70f));
        }
    }

    private void UpdateDataBars(float elapsed, float graphProgress)
    {
        for (int i = 0; i < dataBars.Count; i++)
        {
            RectTransform rect = dataBars[i];
            Image image = i < dataBarImages.Count ? dataBarImages[i] : null;
            if (rect == null || image == null)
                continue;

            float phase = Mathf.Repeat(elapsed * (0.18f + i * 0.011f) + i * 0.071f, 1f);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Lerp(-440f, 440f, phase));
            float alpha = Mathf.Lerp(0.06f, 0.30f, Smooth01(graphProgress)) * (0.56f + Mathf.Sin((elapsed + i) * 5.1f) * 0.22f);
            image.color = ColorForStream(i, alpha);
        }
    }

    private void UpdateEdges(float graphProgress, float pulse)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            EdgeView edge = edges[i];
            if (edge == null || edge.Root == null)
                continue;

            float reveal = Smooth01(Mathf.Clamp01((graphProgress - edge.RevealAt) * 7.5f));
            edge.Root.localScale = new Vector3(reveal, 1f, 1f);
            Color color = new Color(0.18f + pulse * 0.20f, 0.78f + pulse * 0.20f, 1f, reveal * 0.82f);
            SetImageColor(edge.Line, color);
            SetImageColor(edge.HeadA, color);
            SetImageColor(edge.HeadB, color);
        }
    }

    private void UpdateNodes(float elapsed, float graphProgress, float pulse)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            NodeView node = nodes[i];
            if (node == null || node.Rect == null)
                continue;

            float reveal = Smooth01(Mathf.Clamp01((graphProgress - node.RevealAt) * 8f));
            float flash = reveal > 0f && reveal < 1f ? Mathf.Sin(reveal * Mathf.PI) * 0.22f : 0f;
            node.Rect.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f + flash, reveal);
            SetImageColor(node.Core, NodeColor(i, reveal));
            SetImageColor(node.Glow, new Color(0.16f, 0.88f, 1f, reveal * (0.16f + pulse * 0.24f)));
            if (node.Glow != null)
                node.Glow.rectTransform.localScale = Vector3.one * (1.05f + pulse * 0.22f);
            if (node.Reticle != null)
            {
                node.Reticle.color = new Color(0.68f, 1f, 0.96f, reveal * (0.10f + pulse * 0.42f));
                node.Reticle.rectTransform.localScale = Vector3.one * (1.00f + pulse * 0.10f);
                node.Reticle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * elapsed * 32f);
            }
            if (node.Label != null)
                node.Label.color = new Color(0.90f, 1f, 0.98f, reveal);
        }
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private static Color NodeColor(int index, float alpha)
    {
        Color baseColor;
        if (index == 0)
            baseColor = new Color(0.10f, 0.35f, 0.52f, 1f);
        else if (index == 2)
            baseColor = new Color(0.10f, 0.58f, 0.40f, 1f);
        else if (index == 3 || index == 7)
            baseColor = new Color(0.62f, 0.40f, 0.12f, 1f);
        else if (index == 5)
            baseColor = new Color(0.62f, 0.18f, 0.24f, 1f);
        else if (index == 10)
            baseColor = new Color(0.62f, 0.12f, 0.24f, 1f);
        else
            baseColor = new Color(0.08f, 0.24f, 0.40f, 1f);

        baseColor.a = Mathf.Clamp01(alpha) * 0.94f;
        return baseColor;
    }

    private static Color ColorForStream(int index, float alpha)
    {
        Color color = index % 5 == 0
            ? new Color(1f, 0.26f, 0.58f, alpha)
            : new Color(0.18f, 0.82f, 1f, alpha);
        return color;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void LoadVisualSprites()
    {
        portalBackplateSprite =
            LoadSprite(PixelHudSpriteRoot + "/Panels/White/FrameDigitalLarge.png") ??
            LoadSprite(PixelHudSpriteRoot + "/Panels/White/PanelOutlined.png") ??
            LoadSprite(PixelHudSpriteRoot + "/Panels/Blue/PanelDigital.png");
        portalShellSprite = LoadSprite(CyberHudSpriteRoot + "/panel_base_01_outer_shell.png");
        portalRadarSprite = LoadSprite(CyberHudSpriteRoot + "/hud_radar_frame.png");
        portalGlyphSprite =
            LoadSprite(CyberHudSpriteRoot + "/deco_misc_03.png") ??
            LoadSprite(CyberHudSpriteRoot + "/icon_skill_06.png");
        nodeFrameSprite =
            LoadSprite(PixelHudSpriteRoot + "/SkillTree/White/SkillSlotSharp.png") ??
            LoadSprite(PixelHudSpriteRoot + "/SkillTree/White/SkillSlotRound.png");
        nodeReticleSprite =
            LoadSprite(PixelHudSpriteRoot + "/Selectors/Reticle_Select.png") ??
            LoadSprite(PixelHudSpriteRoot + "/Grid/White/SelectorEdge_Focus.png");
        edgeLineSprite =
            LoadSprite(PixelHudSpriteRoot + "/SkillTree/White/ConnectorThinHorizontal.png") ??
            LoadSprite(PixelHudSpriteRoot + "/SkillTree/White/ConnectorHorizontal.png");
        edgeHeadSprite = LoadSprite(PixelHudSpriteRoot + "/Selectors/ChevronRight_Select.png");
        progressTrackSprite = LoadSprite(CyberHudSpriteRoot + "/progress_bar_striped_frame.png");
        progressFillSprite = LoadSprite(CyberHudSpriteRoot + "/progress_fill_striped_texture.png");
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = null;
#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#endif
        if (sprite == null)
            sprite = RuntimeUiAssetCatalog.FindSprite(assetPath);
        return sprite;
    }

    private Font ResolveFont()
    {
        Font font = Resources.Load<Font>(FontResourcePath);
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private Image CreateImage(string objectName, Transform parent, Color color, bool raycastTarget)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = transitionFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetImageColor(Image image, Color color)
    {
        if (image != null)
            image.color = color;
    }
}
