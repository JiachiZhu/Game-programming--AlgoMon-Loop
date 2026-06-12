using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minimal run result screen for the Sprint 3 loop.
/// GameManager owns the outcome state; this scene only presents it and returns
/// to MainTerminal when the player confirms.
/// </summary>
[DisallowMultipleComponent]
public class RunResultController : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text titleText;
    [SerializeField] private Text resultText;
    [SerializeField] private Text detailText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Image accentFill;
    [SerializeField] private Text statusPillText;
    [SerializeField] private Text routeMetaText;

    private GameManager manager;
    private Font defaultFont;
    private Sprite panelChromeSprite;
    private Sprite panelChromeDefeatSprite;

    // Outcome-themed chrome captured at build time, retinted in Refresh.
    private Image panelImage;
    private Outline panelOutlineGlow;
    private Image topLineImage;
    private Image bannerBand;
    private Image bannerLineTop;
    private Image bannerLineBottom;
    private Image statusPillFrame;
    private Image rewardsHeaderBar;
    private Image summaryHeaderBar;
    private Image continueButtonImage;
    private readonly List<Image> backdropScanlines = new List<Image>();
    private readonly List<Image> cornerBrackets = new List<Image>();
    private readonly List<Image> edgeStrips = new List<Image>();

    private const int ResultRowCount = 6;
    private const string FontResourcePath = "Fonts/NicoBold-Regular";

    private readonly Image[] resultRowFrames = new Image[ResultRowCount];
    private readonly Image[] resultRowAccents = new Image[ResultRowCount];
    private readonly Text[] resultRowLabels = new Text[ResultRowCount];
    private readonly Text[] resultRowValues = new Text[ResultRowCount];

    // Matches the battle HUD's unified cyber-glass chrome (BattleHudController),
    // so the run-result screen reads as the same UI family instead of the old
    // flat default fill.
    private static readonly Color PanelFill = new Color(0.039f, 0.063f, 0.090f, 0.95f);
    private static readonly Color32 PanelBorder = new Color32(78, 206, 230, 255);
    private static readonly Color PanelGlow = new Color(0.30f, 0.80f, 0.94f, 0.45f);
    private static readonly Color VictoryAccent = new Color(1f, 0.68f, 0.22f, 1f);
    private static readonly Color VictorySecondary = new Color(0.26f, 0.92f, 1f, 1f);
    private static readonly Color DefeatAccent = new Color(1f, 0.22f, 0.30f, 1f);
    private static readonly Color DefeatSecondary = new Color(0.60f, 0.72f, 0.82f, 1f);
    private static readonly Color TextPrimary = new Color(0.91f, 0.98f, 1f, 1f);
    private static readonly Color TextMuted = new Color(0.56f, 0.76f, 0.84f, 1f);
    private static readonly Color RowFill = new Color(0.026f, 0.047f, 0.070f, 0.82f);

    private void Awake()
    {
        defaultFont = Resources.Load<Font>(FontResourcePath);
        if (defaultFont == null)
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        manager = GameManager.EnsureInstance();
        EnsureSceneObjects();
    }

    private void OnEnable()
    {
        if (continueButton == null)
            return;

        continueButton.onClick.RemoveListener(ReturnToTerminal);
        continueButton.onClick.AddListener(ReturnToTerminal);
    }

    private void OnDisable()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(ReturnToTerminal);
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (accentFill != null)
            accentFill.fillAmount = 0.50f + Mathf.PingPong(Time.unscaledTime * 0.12f, 0.42f);

        // Subtle life: banner hairlines and corner brackets breathe.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.4f);
        if (bannerLineTop != null)
            SetAlpha(bannerLineTop, 0.30f + pulse * 0.35f);
        if (bannerLineBottom != null)
            SetAlpha(bannerLineBottom, 0.30f + (1f - pulse) * 0.35f);
        for (int i = 0; i < cornerBrackets.Count; i++)
        {
            if (cornerBrackets[i] != null)
                SetAlpha(cornerBrackets[i], 0.55f + 0.30f * Mathf.Sin(Time.unscaledTime * 2.2f + i * 0.7f));
        }
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private void Refresh()
    {
        RunOutcome outcome = manager != null ? manager.pendingRunOutcome : RunOutcome.None;
        bool victory = outcome == RunOutcome.Victory;
        bool defeat = outcome == RunOutcome.Defeat;
        Color accent = victory ? VictoryAccent : defeat ? DefeatAccent : DefeatSecondary;
        Color secondary = victory ? VictorySecondary : DefeatSecondary;
        RunRewardSummary rewards = manager != null ? manager.completedRunRewards : null;
        bool hasRewards = HasRewardData(rewards);

        if (titleText != null)
            titleText.text = "RUN RESULT";

        if (resultText != null)
        {
            resultText.text = victory ? "VICTORY" : outcome == RunOutcome.Defeat ? "DEFEAT" : "NO RESULT";
            resultText.color = accent;
        }

        if (detailText != null)
            detailText.text = "REWARDS";

        if (statusPillText != null)
            statusPillText.text = victory ? "NODE SECURED" : defeat ? "PARTY OFFLINE" : "NO PACKET";

        if (routeMetaText != null)
            routeMetaText.text = "RUN SUMMARY";

        if (accentFill != null)
            accentFill.color = new Color(accent.r, accent.g, accent.b, 0.54f);

        ApplyOutcomeChrome(victory, defeat, accent, secondary);

        if (hasRewards)
        {
            SetResultRow(0, "ALGOMON EXP", $"+{RewardExp(rewards)}", accent);
            SetResultRow(1, "CREDITS", $"+{RewardCompute(rewards)}", secondary);
            SetResultRow(2, "FORM DATA", DataRewardText(rewards), Color.Lerp(accent, secondary, 0.40f));
        }
        else
        {
            SetResultRow(0, "REWARDS", "NONE", TextMuted);
            SetResultRow(1, string.Empty, string.Empty, TextMuted, false);
            SetResultRow(2, string.Empty, string.Empty, TextMuted, false);
        }

        SetResultRow(3, "NODE", NodeTypeText(), secondary);
        SetResultRow(4, "THREAT TIER", $"T{CompletedThreatTier()}", secondary);
        SetResultRow(5, "VISITED NODES", $"{CompletedVisitedCount()}", secondary);
    }

    /// <summary>Retint every chrome piece by outcome: victory keeps the cyan family
    /// with amber highlights; defeat shifts the panel border, brackets, and bands red.</summary>
    private void ApplyOutcomeChrome(bool victory, bool defeat, Color accent, Color secondary)
    {
        if (panelImage != null)
            panelImage.sprite = defeat ? CyberPanelDefeatSprite() : CyberPanelSprite();
        if (statusPillFrame != null)
            statusPillFrame.sprite = defeat ? CyberPanelDefeatSprite() : CyberPanelSprite();
        if (panelOutlineGlow != null)
            panelOutlineGlow.effectColor = new Color(accent.r, accent.g, accent.b, 0.40f);
        if (topLineImage != null)
            topLineImage.color = new Color(secondary.r, secondary.g, secondary.b, 0.62f);

        if (bannerBand != null)
            bannerBand.color = new Color(accent.r, accent.g, accent.b, 0.05f);
        if (bannerLineTop != null)
            bannerLineTop.color = new Color(accent.r, accent.g, accent.b, 0.55f);
        if (bannerLineBottom != null)
            bannerLineBottom.color = new Color(accent.r, accent.g, accent.b, 0.55f);

        if (rewardsHeaderBar != null)
            rewardsHeaderBar.color = new Color(accent.r, accent.g, accent.b, 0.70f);
        if (summaryHeaderBar != null)
            summaryHeaderBar.color = new Color(secondary.r, secondary.g, secondary.b, 0.55f);

        for (int i = 0; i < cornerBrackets.Count; i++)
        {
            if (cornerBrackets[i] != null)
                cornerBrackets[i].color = new Color(accent.r, accent.g, accent.b, 0.70f);
        }

        Color scanTint = victory ? new Color(0.20f, 0.85f, 0.95f, 1f) : defeat ? new Color(1f, 0.30f, 0.40f, 1f) : secondary;
        for (int i = 0; i < backdropScanlines.Count; i++)
        {
            if (backdropScanlines[i] != null)
                backdropScanlines[i].color = new Color(scanTint.r, scanTint.g, scanTint.b, i % 4 == 0 ? 0.045f : 0.022f);
        }

        for (int i = 0; i < edgeStrips.Count; i++)
        {
            if (edgeStrips[i] != null)
                edgeStrips[i].color = new Color(secondary.r, secondary.g, secondary.b, i % 2 == 0 ? 0.16f : 0.30f);
        }

        if (continueButtonImage != null)
        {
            continueButtonImage.sprite = defeat ? CyberPanelDefeatSprite() : CyberPanelSprite();
            continueButtonImage.color = victory
                ? new Color(0.30f, 0.90f, 0.96f, 0.85f)
                : defeat
                    ? new Color(0.95f, 0.45f, 0.52f, 0.85f)
                    : new Color(0.28f, 0.88f, 0.94f, 0.78f);
        }
    }

    private void ReturnToTerminal()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager != null)
        {
            manager.EndRun();
            manager.ClearRunResult();
        }

        GameManager.GoTo(GameScene.MainTerminal);
    }

    private string NodeTypeText()
    {
        return manager != null ? manager.completedRunNodeType.ToString().ToUpperInvariant() : "--";
    }

    private int CompletedVisitedCount()
    {
        return manager != null ? Mathf.Max(0, manager.completedRunVisitedCount) : 0;
    }

    private int CompletedThreatTier()
    {
        return manager != null ? Mathf.Max(0, manager.completedRunThreatTier) : 0;
    }

    private static int RewardExp(RunRewardSummary rewards)
    {
        return rewards != null ? Mathf.Max(0, rewards.algoMonExp) : 0;
    }

    private static int RewardCompute(RunRewardSummary rewards)
    {
        return rewards != null ? Mathf.Max(0, rewards.compute) : 0;
    }

    private static string DataRewardText(RunRewardSummary rewards)
    {
        if (rewards == null)
            return "NONE";

        int data = Mathf.Max(0, rewards.baseDataCount) + Mathf.Max(0, rewards.evolutionDataCount);
        if (data <= 0)
            return "NONE";
        if (rewards.highQualityBaseDataCount > 0)
            return $"+{data} HIGH";

        return $"+{data}";
    }

    private static bool HasRewardData(RunRewardSummary rewards)
    {
        return rewards != null &&
               (rewards.algoMonExp > 0 ||
                rewards.compute > 0 ||
                rewards.baseDataCount > 0 ||
                rewards.highQualityBaseDataCount > 0 ||
                rewards.evolutionDataCount > 0);
    }

    private void EnsureSceneObjects()
    {
        EnsureEventSystem();

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            canvas = CreateCanvas();

        // Pixel-perfect snapping keeps the Nico bitmap font and 1-2px chrome
        // borders crisp (same setup as MainTerminal's ConfigureCrispCanvas).
        canvas.pixelPerfect = true;
        CanvasScaler crispScaler = canvas.GetComponent<CanvasScaler>();
        if (crispScaler != null)
        {
            crispScaler.referencePixelsPerUnit = 100f;
            crispScaler.dynamicPixelsPerUnit = 100f;
        }

        RectTransform root = canvas.GetComponent<RectTransform>();

        Image background = CreateImage("Background", root, new Color(0.006f, 0.009f, 0.02f, 1f));
        SetStretchRect(background.rectTransform, Vector2.zero, Vector2.one);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = new Color(0.006f, 0.009f, 0.020f, 1f);

        Image shade = CreateImage("BackgroundShade", root, new Color(0.000f, 0.012f, 0.020f, 0.34f));
        SetStretchRect(shade.rectTransform, Vector2.zero, Vector2.one);

        // Atmosphere: faint full-screen scanlines + vertical data strips so the
        // space around the panel is not a flat void. All retinted in Refresh.
        backdropScanlines.Clear();
        for (int i = 0; i < 16; i++)
        {
            Image line = CreateImage("BackdropScanline_" + i, root, Color.clear);
            RectTransform lineRect = line.rectTransform;
            lineRect.anchorMin = new Vector2(0f, (i + 0.5f) / 16f);
            lineRect.anchorMax = new Vector2(1f, (i + 0.5f) / 16f);
            lineRect.sizeDelta = new Vector2(0f, i % 4 == 0 ? 2f : 1f);
            lineRect.anchoredPosition = Vector2.zero;
            backdropScanlines.Add(line);
        }

        edgeStrips.Clear();
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? 0.085f : 0.915f;
            Image strip = CreateImage("EdgeStrip_" + i, root, Color.clear);
            SetStretchRect(strip.rectTransform, new Vector2(x - 0.002f, 0.12f), new Vector2(x + 0.002f, 0.88f));
            edgeStrips.Add(strip);

            Image stripDash = CreateImage("EdgeStripDash_" + i, root, Color.clear);
            SetStretchRect(stripDash.rectTransform, new Vector2(x - 0.010f, 0.46f), new Vector2(x + 0.010f, 0.54f));
            edgeStrips.Add(stripDash);
        }

        panelImage = CreateImage("ResultPanel", root, Color.white);
        panelImage.sprite = CyberPanelSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.pixelsPerUnitMultiplier = 1.3f;
        SetStretchRect(panelImage.rectTransform, new Vector2(0.225f, 0.120f), new Vector2(0.775f, 0.875f));
        panelOutlineGlow = panelImage.gameObject.AddComponent<Outline>();
        panelOutlineGlow.effectColor = PanelGlow;
        panelOutlineGlow.effectDistance = new Vector2(2f, -2f);
        panelOutlineGlow.useGraphicAlpha = false;

        // Corner brackets — the signature cyber target-lock framing.
        cornerBrackets.Clear();
        CreateCornerBracket(root, new Vector2(0.225f, 0.875f), 1f, -1f);
        CreateCornerBracket(root, new Vector2(0.775f, 0.875f), -1f, -1f);
        CreateCornerBracket(root, new Vector2(0.225f, 0.120f), 1f, 1f);
        CreateCornerBracket(root, new Vector2(0.775f, 0.120f), -1f, 1f);

        topLineImage = CreateImage("TopLine", root, new Color(0.11f, 0.75f, 0.88f, 0.58f));
        SetLineRect(topLineImage.rectTransform, new Vector2(0.285f, 0.765f), new Vector2(0.715f, 0.765f), 3f);

        // Outcome banner: translucent band + hairlines behind the big verdict text.
        bannerBand = CreateImage("OutcomeBand", root, Color.clear);
        SetStretchRect(bannerBand.rectTransform, new Vector2(0.265f, 0.615f), new Vector2(0.735f, 0.745f));
        bannerLineTop = CreateImage("OutcomeBandTop", root, Color.clear);
        SetLineRect(bannerLineTop.rectTransform, new Vector2(0.265f, 0.748f), new Vector2(0.735f, 0.748f), 2f);
        bannerLineBottom = CreateImage("OutcomeBandBottom", root, Color.clear);
        SetLineRect(bannerLineBottom.rectTransform, new Vector2(0.265f, 0.612f), new Vector2(0.735f, 0.612f), 2f);

        accentFill = accentFill != null
            ? accentFill
            : CreateImage("SignalFill", root, new Color(0.96f, 0.32f, 0.52f, 0.45f));
        accentFill.type = Image.Type.Filled;
        accentFill.fillMethod = Image.FillMethod.Horizontal;
        SetLineRect(accentFill.rectTransform, new Vector2(0.350f, 0.545f), new Vector2(0.650f, 0.545f), 4f);

        titleText = titleText != null ? titleText : CreateText("Title", root, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetStretchRect(titleText.rectTransform, new Vector2(0.320f, 0.780f), new Vector2(0.680f, 0.835f));
        titleText.color = new Color(0.62f, 0.92f, 1f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;

        resultText = resultText != null ? resultText : CreateText("Outcome", root, 72, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetStretchRect(resultText.rectTransform, new Vector2(0.260f, 0.620f), new Vector2(0.740f, 0.735f));
        resultText.color = new Color(1f, 0.88f, 0.62f, 1f);

        statusPillFrame = CreateImage("StatusPillFrame", root, Color.white);
        statusPillFrame.sprite = CyberPanelSprite();
        statusPillFrame.type = Image.Type.Sliced;
        statusPillFrame.pixelsPerUnitMultiplier = 1.5f;
        SetStretchRect(statusPillFrame.rectTransform, new Vector2(0.400f, 0.560f), new Vector2(0.600f, 0.608f));

        statusPillText = statusPillText != null ? statusPillText : CreateText("StatusPill", root, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetStretchRect(statusPillText.rectTransform, new Vector2(0.340f, 0.565f), new Vector2(0.660f, 0.605f));
        statusPillText.color = TextPrimary;

        detailText = detailText != null ? detailText : CreateText("Details", root, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        SetStretchRect(detailText.rectTransform, new Vector2(0.270f, 0.485f), new Vector2(0.490f, 0.525f));
        detailText.color = new Color(0.82f, 0.90f, 0.95f, 1f);
        detailText.alignment = TextAnchor.MiddleLeft;
        detailText.fontStyle = FontStyle.Bold;

        rewardsHeaderBar = CreateImage("RewardsHeaderBar", root, Color.clear);
        SetLineRect(rewardsHeaderBar.rectTransform, new Vector2(0.270f, 0.482f), new Vector2(0.490f, 0.482f), 2f);

        routeMetaText = routeMetaText != null ? routeMetaText : CreateText("RouteMeta", root, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetStretchRect(routeMetaText.rectTransform, new Vector2(0.510f, 0.485f), new Vector2(0.730f, 0.525f));
        routeMetaText.color = TextMuted;
        routeMetaText.alignment = TextAnchor.MiddleLeft;

        summaryHeaderBar = CreateImage("SummaryHeaderBar", root, Color.clear);
        SetLineRect(summaryHeaderBar.rectTransform, new Vector2(0.510f, 0.482f), new Vector2(0.730f, 0.482f), 2f);

        CreateResultRow(0, root, "RewardRow_Exp", new Vector2(0.270f, 0.415f), new Vector2(0.490f, 0.475f));
        CreateResultRow(1, root, "RewardRow_Compute", new Vector2(0.270f, 0.342f), new Vector2(0.490f, 0.402f));
        CreateResultRow(2, root, "RewardRow_Data", new Vector2(0.270f, 0.269f), new Vector2(0.490f, 0.329f));
        CreateResultRow(3, root, "SummaryRow_Node", new Vector2(0.510f, 0.415f), new Vector2(0.730f, 0.475f));
        CreateResultRow(4, root, "SummaryRow_Tier", new Vector2(0.510f, 0.342f), new Vector2(0.730f, 0.402f));
        CreateResultRow(5, root, "SummaryRow_Visited", new Vector2(0.510f, 0.269f), new Vector2(0.730f, 0.329f));

        continueButton = continueButton != null ? continueButton : CreateButton("ContinueButton", root, "RETURN TO TERMINAL");
        continueButtonImage = continueButton.GetComponent<Image>();
        RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
        SetStretchRect(buttonRect, new Vector2(0.375f, 0.150f), new Vector2(0.625f, 0.218f));
    }

    private void CreateCornerBracket(Transform parent, Vector2 corner, float xDir, float yDir)
    {
        const float armLong = 0.030f;
        const float armShort = 0.0035f;

        Image horizontal = CreateImage("Bracket_H", parent, Color.clear);
        SetStretchRect(
            horizontal.rectTransform,
            new Vector2(Mathf.Min(corner.x, corner.x + xDir * armLong), Mathf.Min(corner.y, corner.y + yDir * armShort * 1.8f)),
            new Vector2(Mathf.Max(corner.x, corner.x + xDir * armLong), Mathf.Max(corner.y, corner.y + yDir * armShort * 1.8f)));
        cornerBrackets.Add(horizontal);

        Image vertical = CreateImage("Bracket_V", parent, Color.clear);
        SetStretchRect(
            vertical.rectTransform,
            new Vector2(Mathf.Min(corner.x, corner.x + xDir * armShort), Mathf.Min(corner.y, corner.y + yDir * armLong * 1.8f)),
            new Vector2(Mathf.Max(corner.x, corner.x + xDir * armShort), Mathf.Max(corner.y, corner.y + yDir * armLong * 1.8f)));
        cornerBrackets.Add(vertical);
    }

    private void CreateResultRow(int index, Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (index < 0 || index >= ResultRowCount)
            return;

        Image frame = CreateImage(objectName, parent, Color.white);
        frame.sprite = CyberPanelSprite();
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.1f;
        SetStretchRect(frame.rectTransform, anchorMin, anchorMax);
        resultRowFrames[index] = frame;

        Image accent = CreateImage("Accent", frame.transform, Color.white);
        SetStretchRect(accent.rectTransform, new Vector2(0.045f, 0.22f), new Vector2(0.065f, 0.78f));
        resultRowAccents[index] = accent;

        Text label = CreateText("Label", frame.transform, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetStretchRect(label.rectTransform, new Vector2(0.10f, 0.20f), new Vector2(0.60f, 0.80f));
        label.color = TextMuted;
        resultRowLabels[index] = label;

        Text value = CreateText("Value", frame.transform, 20, FontStyle.Bold, TextAnchor.MiddleRight);
        SetStretchRect(value.rectTransform, new Vector2(0.55f, 0.12f), new Vector2(0.94f, 0.88f));
        resultRowValues[index] = value;
    }

    private void SetResultRow(int index, string label, string value, Color accent, bool active = true)
    {
        if (index < 0 || index >= ResultRowCount)
            return;

        if (resultRowFrames[index] != null)
        {
            resultRowFrames[index].gameObject.SetActive(active);
            // White keeps the chrome border crisp; the sprite itself carries the
            // dark fill (multiplying by RowFill used to bury the border).
            resultRowFrames[index].color = active ? new Color(0.92f, 0.97f, 1f, 0.96f) : Color.clear;
        }

        if (!active)
            return;

        if (resultRowAccents[index] != null)
        {
            resultRowAccents[index].color = new Color(accent.r, accent.g, accent.b, 0.92f);
        }

        if (resultRowLabels[index] != null)
        {
            resultRowLabels[index].text = label;
            resultRowLabels[index].color = Color.Lerp(TextMuted, accent, 0.15f);
        }

        if (resultRowValues[index] != null)
        {
            resultRowValues[index].text = value;
            resultRowValues[index].color = Color.Lerp(TextPrimary, accent, 0.20f);
        }
    }

    private static void SetStretchRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetLineRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = Vector2.zero;
    }

    // 9-slice cyber-glass chrome: dark translucent fill, 2px cyan border, chamfered
    // corners. Purely procedural so the run-result scene needs no art asset or scene
    // wiring and looks identical to the battle HUD panels.
    private Sprite CyberPanelSprite()
    {
        if (panelChromeSprite == null)
            panelChromeSprite = BuildChromeSprite((Color)PanelBorder, "RunResultPanelChrome");
        return panelChromeSprite;
    }

    private Sprite CyberPanelDefeatSprite()
    {
        if (panelChromeDefeatSprite == null)
            panelChromeDefeatSprite = BuildChromeSprite(new Color(0.91f, 0.25f, 0.34f, 1f), "RunResultPanelChromeDefeat");
        return panelChromeDefeatSprite;
    }

    private static Sprite BuildChromeSprite(Color edge, string spriteName)
    {
        const int size = 28;
        const int chamfer = 5;
        const int border = 3;
        const int slice = chamfer + border;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color fill = PanelFill;
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int l = x;
                int r = size - 1 - x;
                int b = y;
                int t = size - 1 - y;

                if ((l + b < chamfer) || (r + b < chamfer) || (l + t < chamfer) || (r + t < chamfer))
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                bool diagonalEdge = (l + b < chamfer + border) || (r + b < chamfer + border) ||
                                    (l + t < chamfer + border) || (r + t < chamfer + border);
                int straight = Mathf.Min(Mathf.Min(l, r), Mathf.Min(b, t));
                texture.SetPixel(x, y, diagonalEdge || straight < border ? edge : fill);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(slice, slice, slice, slice));
        sprite.name = spriteName;
        return sprite;
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas_RunResult", typeof(RectTransform));
        Canvas newCanvas = canvasObject.AddComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        return newCanvas;
    }

    // Pixel-font crispness: fixed sizes (best-fit produces fractional scales) and
    // no Shadow/Outline effects (their sub-pixel offsets blur bitmap glyphs) —
    // same recipe as MainTerminal's ApplyCrispCyberText.
    private Text CreateText(string objectName, Transform parent, int size, FontStyle style, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Button CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.sprite = CyberPanelSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.1f;
        image.color = new Color(0.28f, 0.88f, 0.94f, 0.78f);
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.78f, 0.34f, 1f);
        colors.pressedColor = new Color(1f, 0.36f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text labelText = CreateText("Text", buttonObject.transform, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        labelText.text = label;
        labelText.color = new Color(0.92f, 1f, 0.98f, 1f);
        labelText.raycastTarget = false;
        SetStretchRect(labelText.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

}
