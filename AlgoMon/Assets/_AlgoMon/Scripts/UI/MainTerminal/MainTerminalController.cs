/*
Script Audit:
- Purpose: Controls the MainTerminal menu scene and starts the gameplay run.
- Attached GameObject: MainTerminal scene UI/controller object, usually the terminal canvas or screen root.
- Main responsibilities: Wire menu buttons, create starter party if needed, start a run, show payload summary, update status text, and build fallback HUD widgets.
- Important variables: enterGridButton, geneLabButton, payloadButton, moduleText, detailText, partyPreviewText, statsText, payloadPanel, fallbackStarter, manager.
- Inputs: Player button clicks, GameManager payload/party data, fallback starter asset, and Time.unscaledTime.
- Outputs or effects: Updates terminal UI, starts GameManager.BeginRun, and transitions to TheGrid.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Open MainTerminal, start a run, inspect payload, and confirm starter party and TheGrid transition work.
*/
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Thin interaction layer for the MainTerminal cover scene.
/// Visual layout lives in the scene; this script only owns run start routing
/// and small status text updates.
/// </summary>
[DisallowMultipleComponent]
public class MainTerminalController : MonoBehaviour
{
    private const int StarterLevel = 20;
    private const int MinimumPlayablePartySize = 2;
    private const string PseudoSpriteResourceRoot = "UI/MainTerminal/PseudoSprite_Tier";
    private const string SourceLayoutTrialVisualName = "MainTerminal_SourceLayoutTrialVisual";
    private const string AlgoMonAssetSearchFolder = "Assets/_AlgoMon/ScriptableObjects/AlgoMons";
    private const string EncounterSpeciesCatalogResourcePath = "EncounterSpeciesCatalog";
    private const float BossRouteFallbackIdleFps = 8f;
    private const float BossRouteSelectionFlashSeconds = 0.34f;
    private const float BossRouteSelectionFlashStepSeconds = 0.055f;
    private const float BossRouteSelectionLineThickness = 5f;
    private const float BossRouteSelectionFrameYOffset = 2f;
    private const string BossRouteSelectionPanelSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD/Panels/Blue/PanelDigital.png";
    private const int BossRouteCodeFontSize = 12;
    private const int BossRouteLabelFontSize = 14;
    private const int BossRouteElementFontSize = 12;
    private const int BossRouteStatusFontSize = 12;
    private const float BossRouteCodeBitmapScale = 0.78f;
    private const float BossRouteLabelBitmapScale = 0.92f;
    private const float BossRouteElementBitmapScale = 0.96f;
    private const float BossRouteStatusBitmapScale = 0.96f;
    private const string BossRouteBitmapFontAtlasPath = "Assets/_AlgoMon/Fonts/NicoBitmap/BoldBasic/BoldBasic.png";
    private const string BossRouteBitmapFontMetricsPath = "Assets/_AlgoMon/Fonts/NicoBitmap/BoldBasic/BoldBasic.txt";
    private static readonly Vector2 SourceLayoutBossRouteFallbackSize = new Vector2(118f, 232f);
    private static readonly Vector2 SourceLayoutBossRouteSelectionPadding = new Vector2(16f, 10f);
    private static readonly Vector2 BossRouteSelectionCornerSize = new Vector2(20f, 18f);
    private static readonly string[] PreferredReserveSpecies =
    {
        "Heapion",
        "Cachelon",
        "Recursix",
        "Overflux",
        "Nullbyte",
        "Sortex"
    };
    private static readonly string[] BossRouteSpecies =
    {
        "Cachelon",
        "Heapion",
        "Nullbyte",
        "Overflux",
        "Recursix",
        "Sortex"
    };

    [Header("Actions")]
    [SerializeField] private Button enterGridButton;
    [SerializeField] private Button geneLabButton;
    [SerializeField] private Button payloadButton;
    [SerializeField] private Button systemLogButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Status")]
    [SerializeField] private Text moduleText;
    [SerializeField] private Text warningText;
    [SerializeField] private Text detailText;
    [SerializeField] private Text footerText;
    [SerializeField] private Text partyPreviewText;
    [SerializeField] private Text statsText;
    [SerializeField] private Image progressFill;
    [SerializeField] private Image progressTrack;
    [SerializeField] private Image progressBase;
    [SerializeField] private Image progressScan;
    [SerializeField] private Image statusPanel;
    [SerializeField] private RectTransform payloadPanel;
    [SerializeField] private Image payloadPortraitImage;
    [SerializeField] private Text payloadPortraitFallbackText;
    [SerializeField] private Text payloadListText;
    [SerializeField] private Text payloadDetailPanelText;
    [SerializeField] private RectTransform depthTierPanel;
    [SerializeField] private Text depthTierTitleText;
    [SerializeField] private Text depthTierDetailText;
    [SerializeField] private Button[] depthTierButtons;
    [SerializeField] private bool unlockAllThreatTiersForVerticalSlice = true;

    [Header("Visual Assets")]
    [SerializeField] private Sprite[] depthTierPseudoSprites;

    [Header("Starter Fallback")]
    [SerializeField] private AlgoMonData fallbackStarter;

    private GameManager manager;
    private float bootTime;
    private Font defaultFont;
    private int selectedPayloadIndex = -1;
    private UnityEngine.Events.UnityAction[] depthTierButtonActions;
    private Image depthTierAvatarImage;
    private Text depthTierSelectedSummaryText;
    private Text depthTierRewardSummaryText;
    private Image[] depthTierSpriteImages;
    private Text[] depthTierButtonLabels;
    private Text[] depthTierButtonCodeLabels;
    private Text[] depthTierRecommendationLabels;
    private CyberFrameGraphic[] depthTierButtonFrames;
    private CyberButtonFeedback[] depthTierButtonFeedbacks;
    private bool usingSourceLayoutTrialDepthButtons;
    private Button launchProtocolButton;
    private Text launchProtocolTitleText;
    private Text launchProtocolDetailText;
    private CyberButtonFeedback launchProtocolFeedback;
    private Button sourceLayoutEnterGridButton;
    private Button sourceLayoutGeneLabButton;
    private Button sourceLayoutPayloadButton;
    private Button sourceLayoutSettingsButton;
    private Button sourceLayoutExitButton;
    private Button[] sourceLayoutBossRouteButtons;
    private UnityEngine.Events.UnityAction[] sourceLayoutBossRouteActions;
    private CyberImageButtonFeedback[] sourceLayoutBossRouteFeedbacks;
    private Graphic[] sourceLayoutBossRouteFrames;
    private Graphic[] sourceLayoutBossRouteFills;
    private Graphic[] sourceLayoutBossRouteActiveFrames;
    private Graphic[] sourceLayoutBossRouteShadows;
    private Graphic[] sourceLayoutBossRoutePortraits;
    private Graphic[] sourceLayoutBossRoutePortraitBackdrops;
    private Graphic[] sourceLayoutBossRouteSignalNotches;
    private RectTransform[] sourceLayoutBossRouteSelectionFrames;
    private Image[] sourceLayoutBossRouteSelectionPanels;
    private Image[] sourceLayoutBossRouteSelectionTopBars;
    private Image[] sourceLayoutBossRouteSelectionBottomBars;
    private Image[] sourceLayoutBossRouteSelectionLeftBars;
    private Image[] sourceLayoutBossRouteSelectionRightBars;
    private Image[] sourceLayoutBossRouteSelectionTopLeftCorners;
    private Image[] sourceLayoutBossRouteSelectionBottomRightCorners;
    private Image[] sourceLayoutBossRoutePortraitImages;
    private Sprite[][] sourceLayoutBossRouteIdleFrames;
    private float[] sourceLayoutBossRouteIdleFrameSeconds;
    private float[] sourceLayoutBossRouteIdleTimers;
    private int[] sourceLayoutBossRouteIdleFrameIndices;
    private Text[] sourceLayoutBossRouteLabels;
    private Text[] sourceLayoutBossRouteCodes;
    private Text[] sourceLayoutBossRouteElementTags;
    private Text[] sourceLayoutBossRouteStatuses;
    private CyberBitmapTextGraphic[] sourceLayoutBossRouteBitmapLabels;
    private CyberBitmapTextGraphic[] sourceLayoutBossRouteBitmapCodes;
    private CyberBitmapTextGraphic[] sourceLayoutBossRouteBitmapElementTags;
    private CyberBitmapTextGraphic[] sourceLayoutBossRouteBitmapStatuses;
    private Transform[] sourceLayoutBossRouteSelectedRails;
    private string sourceLayoutBossRouteLastSelectedCode;
    private float sourceLayoutBossRouteSelectionFlashStartTime = -999f;
    private static Sprite bossRouteSelectionBarSprite;
    private static Sprite bossRouteSelectionPanelSprite;
    private static Font bossRouteDefaultFont;
    private static Texture2D bossRouteBitmapFontAtlas;
    private static TextAsset bossRouteBitmapFontMetrics;

    private void Awake()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        manager = GameManager.EnsureInstance();
        EnsureThreatTierAccess(manager);
        EnsureStarterParty(manager, fallbackStarter);
        EnsureHudWidgets();
        HideLegacySceneButtonVisuals();
        NormalizeMainTerminalFonts();
        RefreshRunOverview();
    }

    private void OnEnable()
    {
        WireButton(enterGridButton, StartRun);
        WireButton(geneLabButton, ShowGeneLabPlaceholder);
        WireButton(payloadButton, ShowPayloadBox);
        WireButton(systemLogButton, ShowSystemLogPlaceholder);
        WireButton(settingsButton, ShowSettingsPlaceholder);
        WireButton(exitButton, ShowExitPlaceholder);
        WireButton(sourceLayoutEnterGridButton, StartRun);
        WireButton(sourceLayoutGeneLabButton, ShowGeneLabPlaceholder);
        WireButton(sourceLayoutPayloadButton, ShowPayloadBox);
        WireButton(sourceLayoutSettingsButton, ShowSettingsPlaceholder);
        WireButton(sourceLayoutExitButton, ShowExitPlaceholder);
        WireButton(launchProtocolButton, StartRun);
        WireDepthTierButtons();
        WireBossRouteButtons();
    }

    private void OnDisable()
    {
        UnwireButton(enterGridButton, StartRun);
        UnwireButton(geneLabButton, ShowGeneLabPlaceholder);
        UnwireButton(payloadButton, ShowPayloadBox);
        UnwireButton(systemLogButton, ShowSystemLogPlaceholder);
        UnwireButton(settingsButton, ShowSettingsPlaceholder);
        UnwireButton(exitButton, ShowExitPlaceholder);
        UnwireButton(sourceLayoutEnterGridButton, StartRun);
        UnwireButton(sourceLayoutGeneLabButton, ShowGeneLabPlaceholder);
        UnwireButton(sourceLayoutPayloadButton, ShowPayloadBox);
        UnwireButton(sourceLayoutSettingsButton, ShowSettingsPlaceholder);
        UnwireButton(sourceLayoutExitButton, ShowExitPlaceholder);
        UnwireButton(launchProtocolButton, StartRun);
        UnwireDepthTierButtons();
        UnwireBossRouteButtons();
    }

    private void Start()
    {
        bootTime = Time.unscaledTime;
        SetModule("ENTER_GRID", "DEPTH TIER:", "GRID LINK READY", BuildDepthTierDetail(manager));
        RefreshRunOverview();
    }

    private void Update()
    {
        if (footerText != null)
        {
            float elapsed = Time.unscaledTime - bootTime;
            footerText.text = $"> SESSION_USER: ADMIN | LOCAL_TIME: {FormatClock(elapsed)} | SYSTEM_STABILITY: 98.4%";
        }

        if (progressFill != null)
        {
            const float segmentWidth = 0.34f;
            float cycle = Mathf.Repeat(Time.unscaledTime * 0.52f, 1f);
            float start = Mathf.Lerp(-segmentWidth, 1f, cycle);
            float glow = Mathf.PingPong(Time.unscaledTime * 2.8f, 1f);
            progressFill.color = Color.Lerp(
                new Color(1f, 0.34f, 0.67f, 0.86f),
                new Color(1f, 0.62f, 0.86f, 0.98f),
                glow);

            RectTransform fillRect = progressFill.rectTransform;
            fillRect.anchorMin = new Vector2(start, 0f);
            fillRect.anchorMax = new Vector2(start + segmentWidth, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        if (progressScan != null && progressScan.enabled)
        {
            const float segmentWidth = 0.34f;
            const float scanWidth = 0.055f;
            float cycle = Mathf.Repeat(Time.unscaledTime * 0.52f, 1f);
            float start = Mathf.Lerp(-segmentWidth, 1f, cycle);
            float scanStart = start + segmentWidth * 0.68f;
            RectTransform scanRect = progressScan.rectTransform;
            scanRect.anchorMin = new Vector2(scanStart, 0f);
            scanRect.anchorMax = new Vector2(scanStart + scanWidth, 1f);
            scanRect.offsetMin = Vector2.zero;
            scanRect.offsetMax = Vector2.zero;
        }

        RefreshRunOverview();
    }

    private void StartRun()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        EnsureStarterParty(manager, fallbackStarter);
        RefreshRunOverview();
        manager.BeginRun();
        GameManager.GoTo(GameScene.TheGrid);
    }

    private void SelectDepthTier(int tier)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        if (manager.IsRunActive)
        {
            SetModule("ENTER_GRID", "RUN ACTIVE:", "DEPTH TIER LOCKED", BuildDepthTierDetail(manager));
            RefreshRunOverview();
            return;
        }

        if (manager.TrySetSelectedThreatTier(tier))
            SetModule("ENTER_GRID", "DEPTH TIER:", $"DEPTH {tier}F ROUTE SELECTED", BuildDepthTierDetail(manager));
        else
            SetModule("ENTER_GRID", "LOCKED:", $"DEPTH {tier}F UNAVAILABLE", BuildDepthTierDetail(manager));

        RefreshRunOverview();
    }

    private void SelectBossRoute(string speciesCodeName)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        if (manager.IsRunActive)
        {
            SetModule("BOSS_TARGET", "RUN ACTIVE:", "BOSS TARGET LOCKED", BuildBossTargetDetail(manager));
            RefreshRunOverview();
            return;
        }

        if (manager.TrySetSelectedBossSpecies(speciesCodeName))
        {
            string selected = manager.SelectedBossSpeciesCodeName.ToUpperInvariant();
            SetModule("BOSS_TARGET", "TARGET:", $"{selected} PRIME CONFIRMED", BuildBossTargetDetail(manager));
        }

        RefreshRunOverview();
    }

    private void ShowGeneLabPlaceholder()
    {
        SetModule("GENE_LAB", "LOCKED:", "GENE MERGE PROTOCOL OFFLINE.", "Gene Lab routing is scheduled for a later sprint.");
    }

    private void ShowPayloadBox()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        int payloadCount = manager != null && manager.payload != null ? manager.payload.Count : 0;
        if (payloadCount > 0)
            selectedPayloadIndex = Mathf.Clamp(selectedPayloadIndex < 0 ? payloadCount - 1 : selectedPayloadIndex, 0, payloadCount - 1);

        SetModule(
            "PAYLOAD_BOX",
            "PAYLOAD:",
            $"{payloadCount} DATA FRAGMENT(S) STORED.",
            BuildPayloadPreview(manager));
        RenderPayloadPanel(manager);
    }

    private void ShowSystemLogPlaceholder()
    {
        SetModule("SYSTEM_LOG", "LOG:", "GRID MODULE READY.", "Start Run will initialize a fresh route graph.");
    }

    private void ShowSettingsPlaceholder()
    {
        SetModule("SETTINGS", "LOCKED:", "CONFIG PANEL NOT DEPLOYED.", "Settings are outside the Sprint 3 playable loop.");
    }

    private void ShowExitPlaceholder()
    {
        SetModule("EXIT_SYSTEM", "STANDBY:", "TERMINAL SESSION HELD OPEN.", "Exit is disabled in editor builds.");
    }

    private void SetModule(string moduleId, string warning, string headline, string detail)
    {
        if (moduleText != null)
            moduleText.text = $"MODULE_ID: {moduleId}";
        if (warningText != null)
            warningText.text = warning;
        if (detailText != null)
            detailText.text = $"{headline}\n\n{detail}";
        if (moduleId != "PAYLOAD_BOX")
            HidePayloadPanel();
    }

    private void RefreshRunOverview()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        EnsureStarterParty(manager, fallbackStarter);

        if (partyPreviewText != null)
            partyPreviewText.text = BuildPartyPreview(manager);

        if (statsText != null)
        {
            int payloadCount = manager.payload != null ? manager.payload.Count : 0;
            int evolutionDataCount = manager.evolutionDataSpeciesCodes != null ? manager.evolutionDataSpeciesCodes.Count : 0;
            string runStatus = manager.IsRunActive ? "ACTV" : "STBY";
            int rewardPercent = manager.IsRunActive
                ? Mathf.RoundToInt(manager.currentRewardMultiplier * 100f)
                : ThreatTierRules.RewardMultiplierPercent(manager.SelectedThreatTier, manager.HighestUnlockedThreatTier);
            statsText.text =
                $"USER// XP {manager.playerExp:0000} CMP {manager.computeBalance:0000}\n" +
                $"PAYLOAD// {payloadCount:00}\n" +
                $"EVO// {evolutionDataCount:00}\n" +
                $"BOSS// {manager.SelectedBossSpeciesCodeName.ToUpperInvariant()}\n" +
                $"RUN// {runStatus} T{manager.SelectedThreatTierNumber:00}/{manager.HighestUnlockedThreatTierNumber:00} x{rewardPercent:000}%\n" +
                $"SQUAD// {PartyCount(manager):00}/{GameManager.MaxPartySize:00}";
        }

        RefreshDepthTierSelector();
        RefreshSourceLayoutBossRoutes();
        RefreshLaunchProtocolText();

        if (payloadPanel != null && payloadPanel.gameObject.activeSelf)
            RenderPayloadPanel(manager);
    }

    private void EnsureThreatTierAccess(GameManager targetManager)
    {
        if (targetManager == null || !unlockAllThreatTiersForVerticalSlice)
            return;

        if (targetManager.HighestUnlockedThreatTierNumber < ThreatTierRules.MaxTier)
            targetManager.SetHighestUnlockedThreatTier(ThreatTierRules.MaxTier);
    }

    private string BuildDepthTierDetail(GameManager targetManager)
    {
        if (targetManager == null)
            return "Depth tier data unavailable.";

        int selected = targetManager.SelectedThreatTierNumber;
        int highest = targetManager.HighestUnlockedThreatTierNumber;
        ThreatTier tier = targetManager.SelectedThreatTier;
        int rewardPercent = targetManager.IsRunActive
            ? Mathf.RoundToInt(targetManager.currentRewardMultiplier * 100f)
            : ThreatTierRules.RewardMultiplierPercent(tier, targetManager.HighestUnlockedThreatTier);

        return $"DEPTH {selected}F / TIER T{selected:00}/{highest:00}\nENEMY BAND LV {ThreatTierRules.MinLevel(tier):00}-{ThreatTierRules.MaxLevel(tier):00}\nREWARD MULTIPLIER x{rewardPercent:000}%";
    }

    private string BuildBossTargetDetail(GameManager targetManager)
    {
        if (targetManager == null)
            return "Boss target data unavailable.";

        string selected = targetManager.SelectedBossSpeciesCodeName.ToUpperInvariant();
        int depth = targetManager.SelectedThreatTierNumber;
        return $"FINAL BOSS: {selected} PRIME\nDEPTH {depth}F / EVOLVED FORM LOCK\nCLICK ANOTHER ROUTE BEFORE LAUNCH TO RE-TARGET.";
    }

    private Sprite ResolveDepthTierSprite(int tier)
    {
        int index = Mathf.Clamp(tier, ThreatTierRules.MinTier, ThreatTierRules.MaxTier) - ThreatTierRules.MinTier;
        if (depthTierPseudoSprites == null || depthTierPseudoSprites.Length != ThreatTierRules.MaxTier)
            depthTierPseudoSprites = new Sprite[ThreatTierRules.MaxTier];

        Sprite cached = depthTierPseudoSprites[index];
        if (cached != null)
            return cached;

        cached = Resources.Load<Sprite>($"{PseudoSpriteResourceRoot}{tier}");
        depthTierPseudoSprites[index] = cached;
        return cached;
    }

    private static Color AccentColorForTier(int tier)
    {
        switch (Mathf.Clamp(tier, ThreatTierRules.MinTier, ThreatTierRules.MaxTier))
        {
            case 1:
                return CyberUiTheme.Success;
            case 2:
                return CyberUiTheme.Selected;
            case 3:
                return CyberUiTheme.Primary;
            case 4:
                return CyberUiTheme.Reward;
            case 5:
            default:
                return CyberUiTheme.Danger;
        }
    }

    private static CyberUiColorRole AccentRoleForTier(int tier)
    {
        switch (Mathf.Clamp(tier, ThreatTierRules.MinTier, ThreatTierRules.MaxTier))
        {
            case 1:
                return CyberUiColorRole.Success;
            case 2:
                return CyberUiColorRole.Selected;
            case 3:
                return CyberUiColorRole.Primary;
            case 4:
                return CyberUiColorRole.Reward;
            case 5:
            default:
                return CyberUiColorRole.Danger;
        }
    }

    private string BuildPartyPreview(GameManager targetManager)
    {
        if (targetManager == null || targetManager.party == null || targetManager.party.Count == 0)
            return "SQUAD// EMPTY";

        var builder = new StringBuilder("SQUAD// ACTIVE");
        for (int i = 0; i < targetManager.party.Count && i < GameManager.MaxPartySize; i++)
        {
            AlgoMonInstance mon = targetManager.party[i];

            builder.Append('\n');
            builder.Append(FormatPartyMon(mon, i + 1));
        }

        return builder.ToString();
    }

    private static string FormatPartyMon(AlgoMonInstance mon, int slot)
    {
        if (mon == null)
            return $"SLOT {slot}: EMPTY";

        string name = DisplayNameFor(mon);
        string element = mon.data != null ? mon.data.elementType.ToString().ToUpperInvariant() : "NORMAL";
        return $"{slot:00}// {name.ToUpperInvariant()}\nL{mon.level:00} [{ShortElement(element)}]\nBAT{mon.Battery:00} SPD{mon.ClockSpeed:00}";
    }

    private static string BuildPayloadPreview(GameManager targetManager)
    {
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
            return "No extracted AlgoMon data yet.\nDefeat encounters in TheArena to auto-store copies here.";

        const int maxVisible = 8;
        var builder = new StringBuilder("EXTRACTED DATA CACHE");
        int visible = Mathf.Min(targetManager.payload.Count, maxVisible);
        for (int i = 0; i < visible; i++)
        {
            builder.Append('\n');
            builder.Append(FormatPayloadMon(targetManager.payload[i], i + 1));
        }

        int hidden = targetManager.payload.Count - visible;
        if (hidden > 0)
            builder.Append($"\n... {hidden} more record(s)");

        return builder.ToString();
    }

    private static string FormatPayloadMon(AlgoMonInstance mon, int slot)
    {
        if (mon == null)
            return $"{slot:00}// CORRUPTED RECORD";

        string name = DisplayNameFor(mon);
        string element = mon.data != null ? mon.data.elementType.ToString().ToUpperInvariant() : "NORMAL";
        return $"{slot:00}// {name.ToUpperInvariant()} L{mon.level:00} [{ShortElement(element)}] BAT{mon.Battery:00} CPU{mon.ComputingPower:00} TP{mon.Throughput:00}";
    }

    private void RenderPayloadPanel(GameManager targetManager)
    {
        if (payloadPanel == null)
            return;

        payloadPanel.gameObject.SetActive(true);
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
        {
            selectedPayloadIndex = -1;
            SetPayloadPortrait(null, "NO DATA");
            if (payloadListText != null)
                payloadListText.text = "PAYLOAD INDEX\n-- EMPTY --";
            if (payloadDetailPanelText != null)
                payloadDetailPanelText.text = "Defeat an encounter in TheArena to archive its persistent AlgoMon data here.";
            return;
        }

        selectedPayloadIndex = Mathf.Clamp(selectedPayloadIndex, 0, targetManager.payload.Count - 1);
        AlgoMonInstance selected = targetManager.payload[selectedPayloadIndex];
        SetPayloadPortrait(ResolvePayloadSprite(selected), DisplayNameFor(selected).ToUpperInvariant());

        if (payloadListText != null)
            payloadListText.text = BuildPayloadList(targetManager, selectedPayloadIndex);
        if (payloadDetailPanelText != null)
            payloadDetailPanelText.text = BuildPayloadDetail(selected);
    }

    private void HidePayloadPanel()
    {
        if (payloadPanel != null)
            payloadPanel.gameObject.SetActive(false);
    }

    private void SetPayloadPortrait(Sprite sprite, string fallbackLabel)
    {
        if (payloadPortraitImage != null)
        {
            payloadPortraitImage.sprite = sprite;
            payloadPortraitImage.enabled = sprite != null;
            payloadPortraitImage.preserveAspect = true;
            payloadPortraitImage.color = Color.white;
        }

        if (payloadPortraitFallbackText != null)
        {
            payloadPortraitFallbackText.enabled = sprite == null;
            payloadPortraitFallbackText.text = string.IsNullOrWhiteSpace(fallbackLabel) ? "NO IMAGE" : fallbackLabel;
        }
    }

    private static string BuildPayloadList(GameManager targetManager, int selectedIndex)
    {
        const int maxVisible = 8;
        int count = targetManager.payload.Count;
        int start = Mathf.Clamp(selectedIndex - maxVisible + 1, 0, Mathf.Max(0, count - maxVisible));
        int end = Mathf.Min(count, start + maxVisible);
        var builder = new StringBuilder("PAYLOAD INDEX");

        for (int i = start; i < end; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            string marker = i == selectedIndex ? ">" : " ";
            string label = mon != null
                ? $"{DisplayNameFor(mon).ToUpperInvariant()} L{mon.level:00}"
                : "CORRUPTED RECORD";
            builder.Append('\n');
            builder.Append($"{marker} {i + 1:00}// {label}");
        }

        return builder.ToString();
    }

    private static string BuildPayloadDetail(AlgoMonInstance mon)
    {
        if (mon == null)
            return "CORRUPTED RECORD";

        AlgoMonData data = mon.data;
        string codeName = data != null && !string.IsNullOrWhiteSpace(data.codeName) ? data.codeName.Trim() : DisplayNameFor(mon);
        string element = data != null ? data.elementType.ToString().ToUpperInvariant() : "NORMAL";
        string subroutine = data != null && data.subroutine != null && !string.IsNullOrWhiteSpace(data.subroutine.subroutineName)
            ? data.subroutine.subroutineName.Trim()
            : "NONE";

        var builder = new StringBuilder();
        builder.AppendLine($"{DisplayNameFor(mon).ToUpperInvariant()}");
        builder.AppendLine($"CODE: {codeName.ToUpperInvariant()}  ELEMENT: {element}");
        builder.AppendLine($"LV {mon.level:00}/{AlgoMonInstance.MAX_LEVEL}  EXP {mon.exp}/{mon.expToNextLevel}");
        builder.AppendLine($"DATA QUALITY: {EncounterReward.FormatQuality(mon.dataQuality)}");
        builder.AppendLine($"SUBROUTINE: {subroutine.ToUpperInvariant()}");
        builder.AppendLine();
        builder.AppendLine("ACTIVE STATS");
        builder.AppendLine($"BAT {mon.Battery:000}  SPD {mon.ClockSpeed:000}");
        builder.AppendLine($"CPU {mon.ComputingPower:000}  TP  {mon.Throughput:000}");
        builder.AppendLine($"FW  {mon.Firewall:000}  ENC {mon.Encryption:000}");
        builder.AppendLine();
        builder.AppendLine("HARDWARE IV");
        builder.AppendLine($"BAT {mon.iv_Battery:000}  SPD {mon.iv_ClockSpeed:000}");
        builder.AppendLine($"CPU {mon.iv_ComputingPower:000}  TP  {mon.iv_Throughput:000}");
        builder.AppendLine($"FW  {mon.iv_Firewall:000}  ENC {mon.iv_Encryption:000}");
        builder.AppendLine();
        builder.AppendLine("SKILLS");
        AppendPayloadSkills(builder, mon);

        if (data != null && !string.IsNullOrWhiteSpace(data.description))
        {
            builder.AppendLine();
            builder.AppendLine("PROFILE");
            builder.Append(data.description.Trim());
        }

        return builder.ToString();
    }

    private static void AppendPayloadSkills(StringBuilder builder, AlgoMonInstance mon)
    {
        if (mon.knownSkills == null || mon.knownSkills.Count == 0)
        {
            builder.Append("- NONE");
            return;
        }

        for (int i = 0; i < mon.knownSkills.Count; i++)
        {
            SkillData skill = mon.knownSkills[i];
            if (skill == null)
            {
                builder.AppendLine("- CORRUPTED SKILL");
                continue;
            }

            string name = !string.IsNullOrWhiteSpace(skill.skillName) ? skill.skillName.Trim() : skill.name;
            string type = skill.instructionType.ToString().ToUpperInvariant();
            string element = skill.elementType.ToString().ToUpperInvariant();
            builder.AppendLine($"- {name.ToUpperInvariant()} [{type}/{element}] PWR {skill.basePower:00} CP {skill.cpCost:00}");
        }
    }

    private static Sprite ResolvePayloadSprite(AlgoMonInstance mon)
    {
        if (mon == null || mon.data == null)
            return null;

        if (mon.data.portrait != null)
            return mon.data.portrait;

#if UNITY_EDITOR
        string codeName = PayloadSpriteName(mon.data.codeName);
        if (string.IsNullOrEmpty(codeName))
            return null;

        string path = $"Assets/_AlgoMon/Sprites/{codeName.ToUpperInvariant()}/{codeName}_Base.png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }

    private static string PayloadSpriteName(string codeName)
    {
        if (string.IsNullOrWhiteSpace(codeName))
            return string.Empty;

        var builder = new StringBuilder(codeName.Length);
        for (int i = 0; i < codeName.Length; i++)
        {
            char c = codeName[i];
            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static string DisplayNameFor(AlgoMonInstance mon)
    {
        if (mon == null)
            return "AlgoMon";

        if (!string.IsNullOrWhiteSpace(mon.nickname))
            return mon.nickname.Trim();

        return mon.data != null && !string.IsNullOrWhiteSpace(mon.data.codeName)
            ? mon.data.codeName.Trim()
            : "AlgoMon";
    }

    private static string ShortElement(string element)
    {
        if (string.IsNullOrEmpty(element))
            return "NORM";

        return element.Length <= 4 ? element : element.Substring(0, 4);
    }

    private static int PartyCount(GameManager targetManager)
    {
        return targetManager != null && targetManager.party != null ? targetManager.party.Count : 0;
    }

    private void EnsureHudWidgets()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        RectTransform root = canvas.GetComponent<RectTransform>();
        RectTransform overlay = CreateRect("TerminalStatusOverlay", root);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        bool useSourceLayoutTrialVisual = HasSourceLayoutTrialVisual();
        if (useSourceLayoutTrialVisual)
        {
            DisableRuntimeDepthWidgets();
            BindSourceLayoutMenuButtons();
            BindSourceLayoutDepthButtons();
            BindSourceLayoutBossRouteButtons();
        }
        else
        {
            usingSourceLayoutTrialDepthButtons = false;
            EnsureDepthTierSelector(overlay);
            EnsureLaunchProtocolButton(overlay);
        }

        moduleText = moduleText != null
            ? moduleText
            : CreateText("ModuleText", overlay, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 1f, 1f, 1f));
        moduleText.gameObject.SetActive(false);
        moduleText.color = new Color(0.78f, 1f, 1f, 1f);
        ApplyCyberText(moduleText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1f, -1f));
        SetAnchors(moduleText.rectTransform, new Vector2(0.392f, 0.806f), new Vector2(0.665f, 0.854f));

        warningText = warningText != null
            ? warningText
            : CreateText("WarningText", overlay, 14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.54f, 1f, 0.72f, 1f));
        warningText.gameObject.SetActive(false);
        warningText.fontSize = 14;
        warningText.alignment = TextAnchor.MiddleRight;
        warningText.color = new Color(0.54f, 1f, 0.72f, 1f);
        ApplyCyberText(warningText, new Color(0f, 0.2f, 0.12f, 1f), new Vector2(1.1f, -1.1f));
        SetAnchors(warningText.rectTransform, new Vector2(0.668f, 0.806f), new Vector2(0.848f, 0.854f));

        detailText = detailText != null
            ? detailText
            : CreateText("DetailText", overlay, 10, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.96f, 0.99f, 1f, 1f));
        detailText.gameObject.SetActive(false);
        detailText.fontSize = 10;
        detailText.lineSpacing = 0.9f;
        detailText.color = new Color(0.96f, 0.99f, 1f, 1f);
        ApplyCyberText(detailText, new Color(0.03f, 0.1f, 0.16f, 0.9f), new Vector2(1.35f, -1.35f));
        SetAnchors(detailText.rectTransform, new Vector2(0.405f, 0.106f), new Vector2(0.840f, 0.194f));

        RectTransform statusPanelRect = EnsureStatusPanel(overlay);
        statusPanelRect.gameObject.SetActive(false);

        partyPreviewText = partyPreviewText != null
            ? partyPreviewText
            : CreateText("PartyPreviewText", statusPanelRect, 12, FontStyle.Bold, TextAnchor.UpperRight, new Color(0.54f, 1f, 0.72f, 1f));
        partyPreviewText.transform.SetParent(statusPanelRect, false);
        partyPreviewText.fontSize = 12;
        partyPreviewText.color = new Color(0.54f, 1f, 0.72f, 1f);
        partyPreviewText.lineSpacing = 0.92f;
        ApplyCyberText(partyPreviewText, new Color(0f, 0.2f, 0.12f, 1f), new Vector2(1.15f, -1.15f));
        SetAnchors(partyPreviewText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.48f));

        statsText = statsText != null
            ? statsText
            : CreateText("StatsText", statusPanelRect, 12, FontStyle.Bold, TextAnchor.UpperRight, new Color(0.78f, 1f, 1f, 1f));
        statsText.transform.SetParent(statusPanelRect, false);
        statsText.fontSize = 12;
        statsText.color = new Color(0.78f, 1f, 1f, 1f);
        statsText.lineSpacing = 0.95f;
        ApplyCyberText(statsText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1.2f, -1.2f));
        SetAnchors(statsText.rectTransform, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.90f));

        footerText = footerText != null
            ? footerText
            : CreateText("FooterText", overlay, 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.58f, 0.9f, 1f, 1f));
        footerText.gameObject.SetActive(false);
        footerText.color = new Color(0.58f, 0.9f, 1f, 1f);
        ApplyCyberText(footerText, new Color(0f, 0.12f, 0.18f, 0.95f), new Vector2(1f, -1f));
        SetAnchors(footerText.rectTransform, new Vector2(0.405f, 0.072f), new Vector2(0.845f, 0.110f));

        RectTransform progressTrackRect = EnsureMainScreenProgress(overlay);
        progressTrackRect.gameObject.SetActive(false);
        RectTransform progressRailRect = progressBase != null
            ? progressBase.rectTransform
            : progressTrackRect;

        progressFill = progressFill != null
            ? progressFill
            : CreateImage("ProgressFill", progressRailRect, new Color(1f, 0.48f, 0.72f, 0.86f));
        progressFill.transform.SetParent(progressRailRect, false);
        progressFill.type = Image.Type.Simple;
        progressFill.raycastTarget = false;
        SetAnchors(progressFill.rectTransform, new Vector2(-0.34f, 0f), new Vector2(0f, 1f));

        progressScan = progressScan != null
            ? progressScan
            : CreateImage("ProgressScan", progressRailRect, Color.clear);
        progressScan.transform.SetParent(progressRailRect, false);
        progressScan.color = Color.clear;
        progressScan.enabled = false;
        progressScan.raycastTarget = false;

        EnsurePayloadPanel(overlay);
        HidePayloadPanel();
    }

    private bool HasSourceLayoutTrialVisual()
    {
        return FindSourceLayoutTrialVisual() != null;
    }

    private Transform FindSourceLayoutTrialVisual()
    {
        Transform current = transform;
        while (current != null)
        {
            Transform found = current.Find(SourceLayoutTrialVisualName);
            if (found != null)
                return found;
            current = current.parent;
        }

        GameObject visual = GameObject.Find(SourceLayoutTrialVisualName);
        return visual != null ? visual.transform : null;
    }

    private void DisableRuntimeDepthWidgets()
    {
        if (depthTierPanel != null)
            depthTierPanel.gameObject.SetActive(false);
        if (launchProtocolButton != null)
            launchProtocolButton.gameObject.SetActive(false);
    }

    private void BindSourceLayoutDepthButtons()
    {
        Transform visual = FindSourceLayoutTrialVisual();
        if (visual == null)
            return;

        usingSourceLayoutTrialDepthButtons = true;
        int tierCount = ThreatTierRules.MaxTier - ThreatTierRules.MinTier + 1;
        depthTierButtons = new Button[tierCount];
        depthTierSpriteImages = new Image[tierCount];
        depthTierButtonLabels = new Text[tierCount];
        depthTierButtonCodeLabels = new Text[tierCount];
        depthTierRecommendationLabels = new Text[tierCount];
        depthTierButtonFrames = new CyberFrameGraphic[tierCount];
        depthTierButtonFeedbacks = new CyberButtonFeedback[tierCount];

        Transform[] children = visual.GetComponentsInChildren<Transform>(true);
        depthTierAvatarImage = null;
        for (int i = 0; i < tierCount; i++)
        {
            int tier = i + ThreatTierRules.MinTier;
            string buttonName = $"DepthButton_{tier}F";
            Button button = null;

            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                if (children[childIndex].name != buttonName)
                    continue;

                button = children[childIndex].GetComponent<Button>();
                break;
            }

            if (button == null)
                continue;

            depthTierButtons[i] = button;
            CacheDepthTierButtonParts(i, button);
        }

        NormalizeSourceLayoutDepthButtons();

        for (int childIndex = 0; childIndex < children.Length; childIndex++)
        {
            if (children[childIndex].name != "DepthPreviewSprite")
                continue;

            depthTierAvatarImage = children[childIndex].GetComponent<Image>();
            break;
        }
    }

    private void BindSourceLayoutMenuButtons()
    {
        Transform visual = FindSourceLayoutTrialVisual();
        if (visual == null)
            return;

        sourceLayoutEnterGridButton = FindChildButton(visual, "Button_ENTERGRID");
        sourceLayoutGeneLabButton = FindChildButton(visual, "Button_GENELAB");
        sourceLayoutPayloadButton = FindChildButton(visual, "Button_PAYLOAD");
        if (sourceLayoutPayloadButton == null)
            sourceLayoutPayloadButton = FindChildButton(visual, "Button_PAYLOADBOX");
        sourceLayoutSettingsButton = FindChildButton(visual, "Button_SETTINGS");
        sourceLayoutExitButton = FindChildButton(visual, "Button_EXIT");
    }

    private void BindSourceLayoutBossRouteButtons()
    {
        Transform visual = FindSourceLayoutTrialVisual();
        if (visual == null)
            return;

        SetChildActive(visual, "Trial_BossRouteSelector/BossRouteTopRail", false);

        int count = BossRouteSpecies.Length;
        sourceLayoutBossRouteButtons = new Button[count];
        sourceLayoutBossRouteFeedbacks = new CyberImageButtonFeedback[count];
        sourceLayoutBossRouteFrames = new Graphic[count];
        sourceLayoutBossRouteFills = new Graphic[count];
        sourceLayoutBossRouteActiveFrames = new Graphic[count];
        sourceLayoutBossRouteShadows = new Graphic[count];
        sourceLayoutBossRoutePortraits = new Graphic[count];
        sourceLayoutBossRoutePortraitBackdrops = new Graphic[count];
        sourceLayoutBossRouteSignalNotches = new Graphic[count];
        sourceLayoutBossRouteSelectionFrames = new RectTransform[count];
        sourceLayoutBossRouteSelectionPanels = new Image[count];
        sourceLayoutBossRouteSelectionTopBars = new Image[count];
        sourceLayoutBossRouteSelectionBottomBars = new Image[count];
        sourceLayoutBossRouteSelectionLeftBars = new Image[count];
        sourceLayoutBossRouteSelectionRightBars = new Image[count];
        sourceLayoutBossRouteSelectionTopLeftCorners = new Image[count];
        sourceLayoutBossRouteSelectionBottomRightCorners = new Image[count];
        sourceLayoutBossRoutePortraitImages = new Image[count];
        sourceLayoutBossRouteIdleFrames = new Sprite[count][];
        sourceLayoutBossRouteIdleFrameSeconds = new float[count];
        sourceLayoutBossRouteIdleTimers = new float[count];
        sourceLayoutBossRouteIdleFrameIndices = new int[count];
        sourceLayoutBossRouteLabels = new Text[count];
        sourceLayoutBossRouteCodes = new Text[count];
        sourceLayoutBossRouteElementTags = new Text[count];
        sourceLayoutBossRouteStatuses = new Text[count];
        sourceLayoutBossRouteBitmapLabels = new CyberBitmapTextGraphic[count];
        sourceLayoutBossRouteBitmapCodes = new CyberBitmapTextGraphic[count];
        sourceLayoutBossRouteBitmapElementTags = new CyberBitmapTextGraphic[count];
        sourceLayoutBossRouteBitmapStatuses = new CyberBitmapTextGraphic[count];
        sourceLayoutBossRouteSelectedRails = new Transform[count];

        for (int i = 0; i < count; i++)
        {
            Button button = FindChildButton(visual, "BossRoute_" + BossRouteSpecies[i].ToUpperInvariant());
            if (button == null)
                continue;

            sourceLayoutBossRouteButtons[i] = button;
            CacheSourceLayoutBossRouteParts(i, button);
        }
    }

    private void CacheSourceLayoutBossRouteParts(int index, Button button)
    {
        if (button == null || index < 0)
            return;

        ConfigureSourceLayoutBossRouteLayout(index, button);
        EnsureSourceLayoutBossRouteSelectionFrame(index, button);

        if (sourceLayoutBossRouteFeedbacks != null && index < sourceLayoutBossRouteFeedbacks.Length)
        {
            sourceLayoutBossRouteFeedbacks[index] = button.GetComponent<CyberImageButtonFeedback>();
            if (sourceLayoutBossRouteFeedbacks[index] != null)
            {
                sourceLayoutBossRouteFeedbacks[index].Selected = false;
                sourceLayoutBossRouteFeedbacks[index].enabled = false;
                button.transform.localScale = Vector3.one;
            }
        }
        if (sourceLayoutBossRouteFrames != null && index < sourceLayoutBossRouteFrames.Length)
            sourceLayoutBossRouteFrames[index] = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
        if (sourceLayoutBossRouteFills != null && index < sourceLayoutBossRouteFills.Length)
            sourceLayoutBossRouteFills[index] = button.transform.Find("ButtonFill")?.GetComponent<Graphic>();
        if (sourceLayoutBossRouteActiveFrames != null && index < sourceLayoutBossRouteActiveFrames.Length)
            sourceLayoutBossRouteActiveFrames[index] = button.transform.Find("ActiveDigitalFrame")?.GetComponent<Graphic>();
        if (sourceLayoutBossRouteShadows != null && index < sourceLayoutBossRouteShadows.Length)
            sourceLayoutBossRouteShadows[index] = button.transform.Find("InactiveShadowTone")?.GetComponent<Graphic>();
        if (sourceLayoutBossRoutePortraits != null && index < sourceLayoutBossRoutePortraits.Length)
            sourceLayoutBossRoutePortraits[index] = button.transform.Find("BossPortraitMask/BossSprite")?.GetComponent<Graphic>();
        if (sourceLayoutBossRoutePortraitImages != null && index < sourceLayoutBossRoutePortraitImages.Length)
        {
            sourceLayoutBossRoutePortraitImages[index] = button.transform.Find("BossPortraitMask/BossSprite")?.GetComponent<Image>();
            CacheSourceLayoutBossRouteIdleFrames(index, sourceLayoutBossRoutePortraitImages[index]);
        }
        if (sourceLayoutBossRoutePortraitBackdrops != null && index < sourceLayoutBossRoutePortraitBackdrops.Length)
            sourceLayoutBossRoutePortraitBackdrops[index] = button.transform.Find("BossPortraitMask")?.GetComponent<Graphic>();
        if (sourceLayoutBossRouteSignalNotches != null && index < sourceLayoutBossRouteSignalNotches.Length)
            sourceLayoutBossRouteSignalNotches[index] = button.transform.Find("SignalNotch")?.GetComponent<Graphic>();
        if (sourceLayoutBossRouteLabels != null && index < sourceLayoutBossRouteLabels.Length)
            sourceLayoutBossRouteLabels[index] = button.transform.Find("Label")?.GetComponent<Text>();
        if (sourceLayoutBossRouteCodes != null && index < sourceLayoutBossRouteCodes.Length)
            sourceLayoutBossRouteCodes[index] = button.transform.Find("RouteCode")?.GetComponent<Text>();
        if (sourceLayoutBossRouteElementTags != null && index < sourceLayoutBossRouteElementTags.Length)
            sourceLayoutBossRouteElementTags[index] = button.transform.Find("ElementTag")?.GetComponent<Text>();
        if (sourceLayoutBossRouteStatuses != null && index < sourceLayoutBossRouteStatuses.Length)
            sourceLayoutBossRouteStatuses[index] = button.transform.Find("RouteStatus")?.GetComponent<Text>();
        DisableBossRouteBitmapText(button.transform, "Label");
        DisableBossRouteBitmapText(button.transform, "RouteCode");
        DisableBossRouteBitmapText(button.transform, "ElementTag");
        DisableBossRouteBitmapText(button.transform, "RouteStatus");
        if (sourceLayoutBossRouteSelectedRails != null && index < sourceLayoutBossRouteSelectedRails.Length)
            sourceLayoutBossRouteSelectedRails[index] = button.transform.Find("SelectedRail");
    }

    private void ConfigureSourceLayoutBossRouteLayout(int index, Button button)
    {
        Transform root = button.transform;
        SetChildActive(root, "HoverGlow", false);
        SetChildActive(root, "ActiveDigitalFrame", false);
        SetChildActive(root, "InactiveShadowTone", false);
        SetChildActive(root, "SelectedRail", false);
        SetChildActive(root, "BossPortraitMask/PortraitScan", false);
        ConfigureBossRouteText(root, "RouteCode", BossRouteCodeFontSize);
        ConfigureBossRouteText(root, "Label", BossRouteLabelFontSize);
        ConfigureBossRouteText(root, "ElementTag", BossRouteElementFontSize);
        ConfigureBossRouteText(root, "RouteStatus", BossRouteStatusFontSize);
    }

    private void EnsureSourceLayoutBossRouteSelectionFrame(int index, Button button)
    {
        if (button == null || index < 0)
            return;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        Vector2 cardSize = BossRouteCardSize(buttonRect);
        Vector2 frameSize = cardSize + SourceLayoutBossRouteSelectionPadding;
        RectTransform frameRoot = GetOrCreateChildRect(button.transform, "CleanSelectionFrame");
        frameRoot.anchorMin = new Vector2(0.5f, 0.5f);
        frameRoot.anchorMax = new Vector2(0.5f, 0.5f);
        frameRoot.pivot = new Vector2(0.5f, 0.5f);
        frameRoot.anchoredPosition = new Vector2(0f, BossRouteSelectionFrameYOffset);
        frameRoot.sizeDelta = RoundVector(frameSize);
        frameRoot.SetAsFirstSibling();
        frameRoot.gameObject.SetActive(false);

        Image selectionPanel = frameRoot.GetComponent<Image>();
        if (selectionPanel == null)
            selectionPanel = frameRoot.gameObject.AddComponent<Image>();
        selectionPanel.sprite = BossRouteSelectionPanelSprite();
        selectionPanel.type = Image.Type.Sliced;
        selectionPanel.raycastTarget = false;
        selectionPanel.preserveAspect = false;
        selectionPanel.color = Color.white;

        Image top = GetOrCreateChildImage(frameRoot, "SelectionFlashTop");
        Image bottom = GetOrCreateChildImage(frameRoot, "SelectionFlashBottom");
        Image left = GetOrCreateChildImage(frameRoot, "SelectionFlashLeft");
        Image right = GetOrCreateChildImage(frameRoot, "SelectionFlashRight");
        Image topLeftCorner = GetOrCreateChildImage(frameRoot, "SelectionCornerTopLeft");
        Image bottomRightCorner = GetOrCreateChildImage(frameRoot, "SelectionCornerBottomRight");
        ConfigureSelectionShape(top);
        ConfigureSelectionShape(bottom);
        ConfigureSelectionShape(left);
        ConfigureSelectionShape(right);
        ConfigureSelectionShape(topLeftCorner);
        ConfigureSelectionShape(bottomRightCorner);

        float halfWidth = frameSize.x * 0.5f;
        float halfHeight = frameSize.y * 0.5f;
        float halfLine = BossRouteSelectionLineThickness * 0.5f;
        ConfigureSelectionBar(top.rectTransform, new Vector2(0f, halfHeight - halfLine), new Vector2(frameSize.x, BossRouteSelectionLineThickness));
        ConfigureSelectionBar(bottom.rectTransform, new Vector2(0f, -halfHeight + halfLine), new Vector2(frameSize.x, BossRouteSelectionLineThickness));
        ConfigureSelectionBar(left.rectTransform, new Vector2(-halfWidth + halfLine, 0f), new Vector2(BossRouteSelectionLineThickness, frameSize.y));
        ConfigureSelectionBar(right.rectTransform, new Vector2(halfWidth - halfLine, 0f), new Vector2(BossRouteSelectionLineThickness, frameSize.y));
        ConfigureSelectionBar(
            topLeftCorner.rectTransform,
            new Vector2(-halfWidth + BossRouteSelectionCornerSize.x * 0.5f, halfHeight - BossRouteSelectionCornerSize.y * 0.5f),
            BossRouteSelectionCornerSize);
        ConfigureSelectionBar(
            bottomRightCorner.rectTransform,
            new Vector2(halfWidth - BossRouteSelectionCornerSize.x * 0.5f, -halfHeight + BossRouteSelectionCornerSize.y * 0.5f),
            BossRouteSelectionCornerSize);

        if (sourceLayoutBossRouteSelectionFrames != null && index < sourceLayoutBossRouteSelectionFrames.Length)
            sourceLayoutBossRouteSelectionFrames[index] = frameRoot;
        if (sourceLayoutBossRouteSelectionPanels != null && index < sourceLayoutBossRouteSelectionPanels.Length)
            sourceLayoutBossRouteSelectionPanels[index] = selectionPanel;
        if (sourceLayoutBossRouteSelectionTopBars != null && index < sourceLayoutBossRouteSelectionTopBars.Length)
            sourceLayoutBossRouteSelectionTopBars[index] = top;
        if (sourceLayoutBossRouteSelectionBottomBars != null && index < sourceLayoutBossRouteSelectionBottomBars.Length)
            sourceLayoutBossRouteSelectionBottomBars[index] = bottom;
        if (sourceLayoutBossRouteSelectionLeftBars != null && index < sourceLayoutBossRouteSelectionLeftBars.Length)
            sourceLayoutBossRouteSelectionLeftBars[index] = left;
        if (sourceLayoutBossRouteSelectionRightBars != null && index < sourceLayoutBossRouteSelectionRightBars.Length)
            sourceLayoutBossRouteSelectionRightBars[index] = right;
        if (sourceLayoutBossRouteSelectionTopLeftCorners != null && index < sourceLayoutBossRouteSelectionTopLeftCorners.Length)
            sourceLayoutBossRouteSelectionTopLeftCorners[index] = topLeftCorner;
        if (sourceLayoutBossRouteSelectionBottomRightCorners != null && index < sourceLayoutBossRouteSelectionBottomRightCorners.Length)
            sourceLayoutBossRouteSelectionBottomRightCorners[index] = bottomRightCorner;
    }

    private static void ConfigureSelectionShape(Image image)
    {
        if (image == null)
            return;

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.color = Color.clear;
        image.gameObject.SetActive(false);
    }

    private static void ConfigureSelectionBar(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = RoundVector(anchoredPosition);
        rect.sizeDelta = RoundVector(size);
    }

    private RectTransform GetOrCreateChildRect(Transform parent, string childName)
    {
        Transform existing = parent != null ? parent.Find(childName) : null;
        if (existing is RectTransform existingRect)
            return existingRect;

        return CreateRect(childName, parent);
    }

    private static Image GetOrCreateChildImage(Transform parent, string childName)
    {
        Transform existing = parent != null ? parent.Find(childName) : null;
        if (existing != null)
        {
            Image image = existing.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                return image;
            }
        }

        return CreateImage(childName, parent, Color.clear);
    }

    private void CacheSourceLayoutBossRouteIdleFrames(int index, Image portraitImage)
    {
        if (index < 0 || sourceLayoutBossRouteIdleFrames == null || index >= sourceLayoutBossRouteIdleFrames.Length)
            return;

        float secondsPerFrame;
        Sprite[] frames = LoadBossRouteIdleFrames(BossRouteSpecies[index], out secondsPerFrame);
        if ((frames == null || frames.Length == 0) && portraitImage != null && portraitImage.sprite != null)
            frames = new[] { portraitImage.sprite };

        sourceLayoutBossRouteIdleFrames[index] = frames ?? Array.Empty<Sprite>();
        sourceLayoutBossRouteIdleFrameSeconds[index] = secondsPerFrame;
        sourceLayoutBossRouteIdleTimers[index] = 0f;
        sourceLayoutBossRouteIdleFrameIndices[index] = 0;

        if (portraitImage != null && sourceLayoutBossRouteIdleFrames[index].Length > 0)
            portraitImage.sprite = sourceLayoutBossRouteIdleFrames[index][0];
    }

    private static Sprite[] LoadBossRouteIdleFrames(string speciesCodeName, out float secondsPerFrame)
    {
        secondsPerFrame = 1f / BossRouteFallbackIdleFps;

        BattleAnimationProfile profile = BattleAnimationProfileLoader.TryLoadEditorProfile(speciesCodeName, "Evolved");
        if (profile == null || profile.idle == null || !profile.idle.HasFrames)
            return Array.Empty<Sprite>();

        secondsPerFrame = profile.idle.SecondsPerFrame;
        return profile.idle.frames;
    }

    private static Sprite BossRouteSelectionBarSprite()
    {
        if (bossRouteSelectionBarSprite != null)
            return bossRouteSelectionBarSprite;

#if UNITY_EDITOR
        bossRouteSelectionBarSprite =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD/Grid/White/SelectorThick_Focus.png") ??
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD/Selectors/Square_Select.png");
#endif
        return bossRouteSelectionBarSprite;
    }

    private static Sprite BossRouteSelectionPanelSprite()
    {
        if (bossRouteSelectionPanelSprite != null)
            return bossRouteSelectionPanelSprite;

#if UNITY_EDITOR
        bossRouteSelectionPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossRouteSelectionPanelSpritePath);
#endif
        return bossRouteSelectionPanelSprite;
    }

    private static Vector2 BossRouteCardSize(RectTransform rect)
    {
        if (rect == null || rect.sizeDelta.x <= 0f || rect.sizeDelta.y <= 0f)
            return SourceLayoutBossRouteFallbackSize;

        return rect.sizeDelta;
    }

    private static Vector2 BossRoutePortraitOffset(string bossName, Vector2 cardSize)
    {
        float x = Mathf.Clamp(cardSize.x * 0.12f, 12f, 22f);
        float y = Mathf.Clamp(cardSize.y * -0.012f, -7f, -2f);
        switch (NormalizeBossRouteCode(bossName))
        {
            case "OVERFLUX":
                return new Vector2(x + 2f, y + 1f);
            case "NULLBYTE":
                return new Vector2(x + 3f, y);
            case "SORTEX":
                return new Vector2(x + 18f, y + 2f);
            case "RECURSIX":
                return new Vector2(x, y + 3f);
            case "HEAPION":
            case "CACHELON":
            default:
                return new Vector2(x, y);
        }
    }

    private static Vector2 BossRoutePortraitSize(string bossName, Vector2 cardSize)
    {
        float width = Mathf.Clamp(cardSize.x * 1.82f, 205f, 278f);
        float height = Mathf.Clamp(cardSize.y * 0.72f, 184f, 250f);
        switch (NormalizeBossRouteCode(bossName))
        {
            case "OVERFLUX":
                return new Vector2(width * 0.98f, height * 0.96f);
            case "NULLBYTE":
                return new Vector2(width, height * 0.98f);
            case "SORTEX":
                return new Vector2(width, height);
            case "RECURSIX":
                return new Vector2(width * 1.02f, height * 0.90f);
            case "HEAPION":
            case "CACHELON":
            default:
                return new Vector2(width, height);
        }
    }

    private static void SetChildRect(Transform parent, string childPath, Vector2 anchoredPosition, Vector2 size)
    {
        Transform child = parent != null ? parent.Find(childPath) : null;
        RectTransform rect = child as RectTransform;
        if (rect == null)
            return;

        rect.anchoredPosition = RoundVector(anchoredPosition);
        rect.sizeDelta = RoundVector(size);
    }

    private static void ConfigureBossRouteText(Transform parent, string childPath, int fontSize)
    {
        Transform child = parent != null ? parent.Find(childPath) : null;
        Text text = child != null ? child.GetComponent<Text>() : null;
        if (text == null)
            return;

        text.font = ResolveBossRouteDefaultFont();
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 0.86f;
    }

    private static Font ResolveBossRouteDefaultFont()
    {
        if (bossRouteDefaultFont == null)
            bossRouteDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return bossRouteDefaultFont;
    }

    private CyberBitmapTextGraphic EnsureBossRouteBitmapText(Transform buttonRoot, string legacyTextPath, float fontScale)
    {
        Transform legacyTextTransform = buttonRoot != null ? buttonRoot.Find(legacyTextPath) : null;
        if (legacyTextTransform == null)
            return null;

        Text legacyText = legacyTextTransform.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.enabled = true;
            legacyText.raycastTarget = false;
        }

        RectTransform bitmapRect = GetOrCreateChildRect(legacyTextTransform, legacyTextPath + "Bitmap");
        bitmapRect.anchorMin = Vector2.zero;
        bitmapRect.anchorMax = Vector2.one;
        bitmapRect.pivot = new Vector2(0.5f, 0.5f);
        bitmapRect.anchoredPosition = Vector2.zero;
        bitmapRect.sizeDelta = Vector2.zero;
        bitmapRect.gameObject.SetActive(false);

        CyberBitmapTextGraphic bitmapText = bitmapRect.GetComponent<CyberBitmapTextGraphic>();
        bool createdBitmapText = bitmapText == null;
        if (bitmapText == null)
            bitmapText = bitmapRect.gameObject.AddComponent<CyberBitmapTextGraphic>();

        bitmapText.Atlas = ResolveBossRouteBitmapFontAtlas();
        bitmapText.Metrics = ResolveBossRouteBitmapFontMetrics();
        if (createdBitmapText || bitmapText.FontScale <= 0.05f)
            bitmapText.FontScale = fontScale;
        bitmapText.LetterSpacing = 0f;
        bitmapText.Alignment = TextAnchor.MiddleCenter;
        bitmapText.raycastTarget = false;
        bitmapText.Text = legacyText != null ? legacyText.text : legacyTextPath;
        bitmapText.color = legacyText != null ? legacyText.color : CyberUiTheme.TextPrimary;
        return bitmapText;
    }

    private static void DisableBossRouteBitmapText(Transform buttonRoot, string legacyTextPath)
    {
        Transform legacyTextTransform = buttonRoot != null ? buttonRoot.Find(legacyTextPath) : null;
        if (legacyTextTransform == null)
            return;

        Transform bitmapTransform = legacyTextTransform.Find(legacyTextPath + "Bitmap");
        if (bitmapTransform != null)
            bitmapTransform.gameObject.SetActive(false);
    }

    private static Texture2D ResolveBossRouteBitmapFontAtlas()
    {
        if (bossRouteBitmapFontAtlas != null)
            return bossRouteBitmapFontAtlas;

#if UNITY_EDITOR
        EnsurePointTextureImport(BossRouteBitmapFontAtlasPath);
        bossRouteBitmapFontAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(BossRouteBitmapFontAtlasPath);
#endif
        return bossRouteBitmapFontAtlas;
    }

    private static TextAsset ResolveBossRouteBitmapFontMetrics()
    {
        if (bossRouteBitmapFontMetrics != null)
            return bossRouteBitmapFontMetrics;

#if UNITY_EDITOR
        bossRouteBitmapFontMetrics = AssetDatabase.LoadAssetAtPath<TextAsset>(BossRouteBitmapFontMetricsPath);
#endif
        return bossRouteBitmapFontMetrics;
    }

#if UNITY_EDITOR
    private static void EnsurePointTextureImport(string atlasPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (importer == null)
            return;

        bool dirty = false;
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            dirty = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }

        dirty |= EnsureUncompressedPlatformTexture(importer, "Standalone");
        dirty |= EnsureUncompressedPlatformTexture(importer, "WebGL");

        if (dirty)
            importer.SaveAndReimport();
    }

    private static bool EnsureUncompressedPlatformTexture(TextureImporter importer, string platformName)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
        bool dirty = false;
        if (!settings.overridden)
        {
            settings.overridden = true;
            dirty = true;
        }

        if (settings.textureCompression != TextureImporterCompression.Uncompressed)
        {
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        if (settings.format != TextureImporterFormat.RGBA32)
        {
            settings.format = TextureImporterFormat.RGBA32;
            dirty = true;
        }

        if (dirty)
            importer.SetPlatformTextureSettings(settings);

        return dirty;
    }
#endif

    private static Vector2 RoundVector(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.x), Mathf.Round(value.y));
    }

    private static void SetChildActive(Transform parent, string childPath, bool active)
    {
        Transform child = parent != null ? parent.Find(childPath) : null;
        if (child != null)
            child.gameObject.SetActive(active);
    }

    private static Button FindChildButton(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i].GetComponent<Button>();
        }

        return null;
    }

    private void HideLegacySceneButtonVisuals()
    {
        HideLegacySceneButtonVisual(enterGridButton);
        HideLegacySceneButtonVisual(geneLabButton);
        HideLegacySceneButtonVisual(payloadButton);
        HideLegacySceneButtonVisual(systemLogButton);
        HideLegacySceneButtonVisual(settingsButton);
        HideLegacySceneButtonVisual(exitButton);
    }

    private static void HideLegacySceneButtonVisual(Button button)
    {
        if (button == null)
            return;

        button.interactable = true;
        button.transition = Selectable.Transition.None;
        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
                continue;

            if (graphics[i] is Image image && image.GetComponent<Button>() == button)
            {
                Color color = image.color;
                color.a = 0f;
                image.color = color;
                image.enabled = true;
                image.raycastTarget = true;
                image.canvasRenderer.SetAlpha(0f);
                continue;
            }

            graphics[i].raycastTarget = false;
            graphics[i].enabled = false;
            graphics[i].canvasRenderer.SetAlpha(0f);
        }
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private RectTransform CreateCyberPanel(
        string objectName,
        Transform parent,
        Color fillColor,
        Color borderColor,
        Color accentColor,
        float cornerCut,
        bool raycastTarget = false)
    {
        RectTransform rect = CreateRect(objectName, parent);
        CyberFrameGraphic frame = rect.gameObject.AddComponent<CyberFrameGraphic>();
        frame.raycastTarget = raycastTarget;
        frame.CornerCut = cornerCut;
        frame.BorderThickness = 2f;
        frame.FillColor = fillColor;
        frame.BorderColor = borderColor;
        frame.AccentColor = accentColor;
        return rect;
    }

    private RectTransform FindOrCreateCyberPanel(
        Transform parent,
        string objectName,
        Color fillColor,
        Color borderColor,
        Color accentColor,
        float cornerCut,
        bool raycastTarget = false)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        RectTransform rect = existing != null
            ? existing.GetComponent<RectTransform>()
            : CreateCyberPanel(objectName, parent, fillColor, borderColor, accentColor, cornerCut, raycastTarget);
        ConfigureCyberFrame(rect, fillColor, borderColor, accentColor, cornerCut, raycastTarget);
        return rect;
    }

    private void ConfigureCyberFrame(
        RectTransform rect,
        Color fillColor,
        Color borderColor,
        Color accentColor,
        float cornerCut,
        bool raycastTarget)
    {
        if (rect == null)
            return;

        CyberFrameGraphic frame = rect.GetComponent<CyberFrameGraphic>();
        if (frame == null)
            frame = rect.gameObject.AddComponent<CyberFrameGraphic>();
        frame.raycastTarget = raycastTarget;
        frame.CornerCut = cornerCut;
        frame.BorderThickness = 2f;
        frame.FillColor = fillColor;
        frame.BorderColor = borderColor;
        frame.AccentColor = accentColor;
    }

    private Text FindOrCreateText(
        Transform parent,
        string objectName,
        int size,
        FontStyle style,
        TextAnchor alignment,
        Color color)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        Text text = existing != null
            ? existing.GetComponent<Text>()
            : CreateText(objectName, parent, size, style, alignment, color);
        if (text == null && existing != null)
            text = existing.gameObject.AddComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = defaultFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private void NormalizeMainTerminalFonts()
    {
        Font font = defaultFont != null ? defaultFont : ResolveBossRouteDefaultFont();
        if (font == null)
            return;

        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            texts[i].font = font;
        }
    }

    private Image FindOrCreateImage(string objectName, Transform parent, Color color)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        Image image = existing != null
            ? existing.GetComponent<Image>()
            : CreateImage(objectName, parent, color);
        if (image == null && existing != null)
            image = existing.gameObject.AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void EnsureFrameRails(RectTransform parent, string prefix, Color color, float thickness)
    {
        if (parent == null)
            return;

        EnsureRail(parent, $"{prefix}_Top", color, new Vector2(0.030f, 1f - thickness), new Vector2(0.970f, 1f));
        EnsureRail(parent, $"{prefix}_Bottom", color, new Vector2(0.030f, 0f), new Vector2(0.970f, thickness));
        EnsureRail(parent, $"{prefix}_Left", color, new Vector2(0f, 0.080f), new Vector2(thickness, 0.920f));
        EnsureRail(parent, $"{prefix}_Right", color, new Vector2(1f - thickness, 0.080f), new Vector2(1f, 0.920f));
    }

    private void EnsureRail(RectTransform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Image rail = FindOrCreateImage(objectName, parent, color);
        rail.raycastTarget = false;
        rail.color = color;
        SetAnchors(rail.rectTransform, anchorMin, anchorMax);
    }

    private Text CreateText(string objectName, Transform parent, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private RectTransform EnsureStatusPanel(Transform parent)
    {
        statusPanel = statusPanel != null
            ? statusPanel
            : CreateImage("StatusPanel", parent, new Color(0.01f, 0.018f, 0.035f, 0.66f));
        statusPanel.raycastTarget = false;
        statusPanel.color = new Color(0.01f, 0.018f, 0.035f, 0.66f);
        RectTransform panelRect = statusPanel.rectTransform;
        SetAnchors(panelRect, new Vector2(0.922f, 0.64f), new Vector2(0.992f, 0.86f));

        Image topLine = CreateImage("StatusPanelTopLine", panelRect, new Color(0.08f, 0.92f, 1f, 0.86f));
        SetAnchors(topLine.rectTransform, new Vector2(0f, 0.96f), new Vector2(1f, 1f));

        Image rightLine = CreateImage("StatusPanelRightLine", panelRect, new Color(1f, 0.25f, 0.86f, 0.62f));
        SetAnchors(rightLine.rectTransform, new Vector2(0.985f, 0f), new Vector2(1f, 1f));

        Image scanLine = CreateImage("StatusPanelScanLine", panelRect, new Color(0.54f, 1f, 0.72f, 0.28f));
        SetAnchors(scanLine.rectTransform, new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.505f));

        return panelRect;
    }

    private void EnsureDepthTierSelector(Transform parent)
    {
        if (depthTierPanel == null)
            depthTierPanel = CreateCyberPanel(
                "DepthTierSelector",
                parent,
                new Color(0.006f, 0.012f, 0.026f, 0.04f),
                new Color(0.08f, 0.78f, 1f, 0.76f),
                new Color(1f, 0.22f, 0.56f, 0.86f),
                28f);

        depthTierPanel.transform.SetParent(parent, false);
        Image legacyBackground = depthTierPanel.GetComponent<Image>();
        if (legacyBackground != null)
            legacyBackground.enabled = false;
        ConfigureCyberFrame(
            depthTierPanel,
            new Color(0.006f, 0.012f, 0.026f, 0.04f),
            new Color(0.08f, 0.78f, 1f, 0.76f),
            new Color(1f, 0.22f, 0.56f, 0.86f),
            28f,
            false);
        CyberFrameGraphic depthFrame = depthTierPanel.GetComponent<CyberFrameGraphic>();
        if (depthFrame != null)
            depthFrame.enabled = false;
        SetAnchors(depthTierPanel, new Vector2(0.388f, 0.260f), new Vector2(0.900f, 0.835f));
        EnsureFrameRails(depthTierPanel, "DepthSelectorRail", new Color(0.08f, 0.92f, 1f, 0.70f), 0.006f);

        depthTierTitleText = depthTierTitleText != null
            ? depthTierTitleText
            : CreateText("DepthTierTitle", depthTierPanel, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.08f, 0.92f, 1f, 1f));
        depthTierTitleText.transform.SetParent(depthTierPanel, false);
        depthTierTitleText.fontSize = 18;
        depthTierTitleText.alignment = TextAnchor.MiddleLeft;
        depthTierTitleText.color = new Color(0.08f, 0.92f, 1f, 1f);
        depthTierTitleText.text = "DEPTH_SELECT.exe";
        ApplyCyberText(depthTierTitleText, new Color(0f, 0.14f, 0.22f, 1f), new Vector2(1f, -1f));
        SetAnchors(depthTierTitleText.rectTransform, new Vector2(0.045f, 0.865f), new Vector2(0.44f, 0.965f));

        depthTierDetailText = depthTierDetailText != null
            ? depthTierDetailText
            : CreateText("DepthTierDetail", depthTierPanel, 8, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.65f, 0.74f, 0.86f, 1f));
        depthTierDetailText.transform.SetParent(depthTierPanel, false);
        depthTierDetailText.fontSize = 9;
        depthTierDetailText.alignment = TextAnchor.MiddleLeft;
        depthTierDetailText.color = new Color(0.65f, 0.74f, 0.86f, 1f);
        depthTierDetailText.text = "SELECT THREAT AVATAR / ROUTE DEPTH / REWARD MODEL";
        ApplyCyberText(depthTierDetailText, new Color(0f, 0.12f, 0.18f, 0.95f), new Vector2(1f, -1f));
        SetAnchors(depthTierDetailText.rectTransform, new Vector2(0.045f, 0.795f), new Vector2(0.70f, 0.865f));

        RectTransform avatarPanel = FindOrCreateCyberPanel(
            depthTierPanel,
            "ThreatAvatarPanel",
            new Color(0.003f, 0.010f, 0.020f, 0.04f),
            new Color(0.36f, 1f, 0.95f, 0.82f),
            new Color(0.08f, 0.92f, 1f, 0.92f),
            20f);
        CyberFrameGraphic avatarFrame = avatarPanel.GetComponent<CyberFrameGraphic>();
        if (avatarFrame != null)
            avatarFrame.enabled = false;
        SetAnchors(avatarPanel, new Vector2(0.045f, 0.245f), new Vector2(0.315f, 0.765f));
        EnsureFrameRails(avatarPanel, "AvatarRail", new Color(0.36f, 1f, 0.95f, 0.86f), 0.016f);

        Text avatarTitle = FindOrCreateText(
            avatarPanel,
            "ThreatAvatarTitle",
            9,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Color(0.36f, 1f, 0.95f, 1f));
        avatarTitle.text = "THREAT AVATAR";
        SetAnchors(avatarTitle.rectTransform, new Vector2(0.13f, 0.825f), new Vector2(0.88f, 0.945f));

        depthTierAvatarImage = depthTierAvatarImage != null
            ? depthTierAvatarImage
            : FindOrCreateImage("ThreatAvatarSprite", avatarPanel, Color.white);
        depthTierAvatarImage.transform.SetParent(avatarPanel, false);
        depthTierAvatarImage.raycastTarget = false;
        depthTierAvatarImage.preserveAspect = true;
        SetAnchors(depthTierAvatarImage.rectTransform, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.79f));

        RectTransform selectedSummaryPanel = FindOrCreateCyberPanel(
            depthTierPanel,
            "DepthTierSelectedSummaryPanel",
            new Color(0.003f, 0.011f, 0.020f, 0.72f),
            new Color(0.36f, 1f, 0.95f, 0.58f),
            new Color(0.36f, 1f, 0.95f, 0.78f),
            14f);
        SetAnchors(selectedSummaryPanel, new Vector2(0.350f, 0.295f), new Vector2(0.625f, 0.430f));
        EnsureFrameRails(selectedSummaryPanel, "SelectedSummaryRail", new Color(0.36f, 1f, 0.95f, 0.54f), 0.014f);

        depthTierSelectedSummaryText = depthTierSelectedSummaryText != null
            ? depthTierSelectedSummaryText
            : CreateText("DepthTierSelectedSummary", selectedSummaryPanel, 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.99f, 1f, 1f));
        depthTierSelectedSummaryText.transform.SetParent(selectedSummaryPanel, false);
        depthTierSelectedSummaryText.fontSize = 11;
        depthTierSelectedSummaryText.color = new Color(0.96f, 0.99f, 1f, 1f);
        ApplyCyberText(depthTierSelectedSummaryText, new Color(0.03f, 0.1f, 0.16f, 0.95f), new Vector2(1f, -1f));
        SetAnchors(depthTierSelectedSummaryText.rectTransform, new Vector2(0.075f, 0.145f), new Vector2(0.940f, 0.900f));

        RectTransform rewardSummaryPanel = FindOrCreateCyberPanel(
            depthTierPanel,
            "DepthTierRewardSummaryPanel",
            new Color(0.018f, 0.010f, 0.004f, 0.58f),
            new Color(1f, 0.62f, 0.20f, 0.58f),
            new Color(1f, 0.62f, 0.20f, 0.86f),
            14f);
        SetAnchors(rewardSummaryPanel, new Vector2(0.650f, 0.295f), new Vector2(0.955f, 0.430f));
        EnsureFrameRails(rewardSummaryPanel, "RewardSummaryRail", new Color(1f, 0.62f, 0.20f, 0.60f), 0.014f);

        depthTierRewardSummaryText = depthTierRewardSummaryText != null
            ? depthTierRewardSummaryText
            : CreateText("DepthTierRewardSummary", rewardSummaryPanel, 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.65f, 0.24f, 1f));
        depthTierRewardSummaryText.transform.SetParent(rewardSummaryPanel, false);
        depthTierRewardSummaryText.fontSize = 11;
        depthTierRewardSummaryText.color = new Color(1f, 0.65f, 0.24f, 1f);
        ApplyCyberText(depthTierRewardSummaryText, new Color(0.16f, 0.06f, 0f, 1f), new Vector2(1f, -1f));
        SetAnchors(depthTierRewardSummaryText.rectTransform, new Vector2(0.075f, 0.145f), new Vector2(0.940f, 0.900f));

        EnsureDepthTierButtons();
        RefreshDepthTierSelector();
    }

    private void EnsureDepthTierButtons()
    {
        if (depthTierPanel == null)
            return;

        int tierCount = ThreatTierRules.MaxTier - ThreatTierRules.MinTier + 1;
        if (depthTierButtons == null || depthTierButtons.Length != tierCount)
            depthTierButtons = new Button[tierCount];
        if (depthTierSpriteImages == null || depthTierSpriteImages.Length != tierCount)
            depthTierSpriteImages = new Image[tierCount];
        if (depthTierButtonLabels == null || depthTierButtonLabels.Length != tierCount)
            depthTierButtonLabels = new Text[tierCount];
        if (depthTierButtonCodeLabels == null || depthTierButtonCodeLabels.Length != tierCount)
            depthTierButtonCodeLabels = new Text[tierCount];
        if (depthTierRecommendationLabels == null || depthTierRecommendationLabels.Length != tierCount)
            depthTierRecommendationLabels = new Text[tierCount];
        if (depthTierButtonFrames == null || depthTierButtonFrames.Length != tierCount)
            depthTierButtonFrames = new CyberFrameGraphic[tierCount];
        if (depthTierButtonFeedbacks == null || depthTierButtonFeedbacks.Length != tierCount)
            depthTierButtonFeedbacks = new CyberButtonFeedback[tierCount];

        const float spacing = 0.018f;
        const float startX = 0.350f;
        const float endX = 0.965f;
        float width = (endX - startX - spacing * (tierCount - 1)) / tierCount;
        for (int i = 0; i < tierCount; i++)
        {
            int tier = i + ThreatTierRules.MinTier;
            if (depthTierButtons[i] == null)
                depthTierButtons[i] = CreateDepthTierButton($"DepthTierButton_{tier}F", depthTierPanel, tier, i);

            CacheDepthTierButtonParts(i, depthTierButtons[i]);

            RectTransform rect = depthTierButtons[i].GetComponent<RectTransform>();
            float minX = startX + i * (width + spacing);
            SetAnchors(rect, new Vector2(minX, 0.505f), new Vector2(minX + width, 0.720f));
        }
    }

    private Button CreateDepthTierButton(string objectName, RectTransform parent, int tier, int index)
    {
        RectTransform rect = CreateRect(objectName, parent);
        CyberFrameGraphic frame = rect.gameObject.AddComponent<CyberFrameGraphic>();
        frame.raycastTarget = true;
        frame.CornerCut = 14f;
        frame.BorderThickness = 2f;
        EnsureFrameRails(rect, "TierCardRail", AccentColorForTier(tier), 0.020f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;

        Image spriteImage = CreateImage("PseudoSprite", rect, Color.white);
        spriteImage.sprite = ResolveDepthTierSprite(tier);
        spriteImage.preserveAspect = true;
        spriteImage.raycastTarget = false;
        SetAnchors(spriteImage.rectTransform, new Vector2(0.060f, 0.115f), new Vector2(0.640f, 0.815f));

        Text label = CreateText("Text", rect, 14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.96f, 0.99f, 1f, 1f));
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 14;
        SetAnchors(label.rectTransform, new Vector2(0.450f, 0.560f), new Vector2(0.920f, 0.900f));

        Text codeLabel = CreateText("TierCode", rect, 8, FontStyle.Bold, TextAnchor.MiddleRight, AccentColorForTier(tier));
        codeLabel.raycastTarget = false;
        codeLabel.resizeTextForBestFit = true;
        codeLabel.resizeTextMinSize = 6;
        codeLabel.resizeTextMaxSize = 8;
        SetAnchors(codeLabel.rectTransform, new Vector2(0.450f, 0.160f), new Vector2(0.900f, 0.380f));

        CyberButtonFeedback feedback = rect.gameObject.AddComponent<CyberButtonFeedback>();
        feedback.AccentRole = AccentRoleForTier(tier);

        if (index >= 0 && depthTierSpriteImages != null && index < depthTierSpriteImages.Length)
        {
            depthTierSpriteImages[index] = spriteImage;
            depthTierButtonLabels[index] = label;
            depthTierButtonCodeLabels[index] = codeLabel;
            depthTierButtonFrames[index] = frame;
            depthTierButtonFeedbacks[index] = feedback;
        }

        return button;
    }

    private void CacheDepthTierButtonParts(int index, Button button)
    {
        if (button == null || index < 0)
            return;

        if (depthTierSpriteImages != null && index < depthTierSpriteImages.Length)
            depthTierSpriteImages[index] = button.transform.Find("PseudoSprite")?.GetComponent<Image>();
        if (depthTierButtonLabels != null && index < depthTierButtonLabels.Length)
            depthTierButtonLabels[index] = button.transform.Find("Text")?.GetComponent<Text>();
        if (depthTierButtonCodeLabels != null && index < depthTierButtonCodeLabels.Length)
            depthTierButtonCodeLabels[index] = button.transform.Find("TierCode")?.GetComponent<Text>();
        if (depthTierRecommendationLabels != null && index < depthTierRecommendationLabels.Length)
            depthTierRecommendationLabels[index] = button.transform.Find("RecommendedLevel")?.GetComponent<Text>();
        if (depthTierButtonFrames != null && index < depthTierButtonFrames.Length)
            depthTierButtonFrames[index] = button.GetComponent<CyberFrameGraphic>();
        if (depthTierButtonFeedbacks != null && index < depthTierButtonFeedbacks.Length)
            depthTierButtonFeedbacks[index] = button.GetComponent<CyberButtonFeedback>();
    }

    private void NormalizeSourceLayoutDepthButtons()
    {
        if (depthTierButtons == null || depthTierButtons.Length == 0)
            return;

        Text templateLabel = depthTierButtonLabels != null && depthTierButtonLabels.Length > 0
            ? depthTierButtonLabels[0]
            : null;
        Image templateButtonImage = depthTierButtons[0] != null
            ? depthTierButtons[0].GetComponent<Image>()
            : null;
        Image templateFillImage = depthTierButtons[0] != null
            ? depthTierButtons[0].transform.Find("ButtonFill")?.GetComponent<Image>()
            : null;
        Image templateNotchImage = depthTierButtons[0] != null
            ? depthTierButtons[0].transform.Find("TierNotch")?.GetComponent<Image>()
            : null;

        for (int i = 0; i < depthTierButtons.Length; i++)
        {
            Button button = depthTierButtons[i];
            if (button == null)
                continue;

            Image buttonImage = button.GetComponent<Image>();
            CopyImageStyle(templateButtonImage, buttonImage, i == 0);

            Image fillImage = button.transform.Find("ButtonFill")?.GetComponent<Image>();
            CopyImageStyle(templateFillImage, fillImage, i == 0);

            Image notchImage = button.transform.Find("TierNotch")?.GetComponent<Image>();
            CopyImageStyle(templateNotchImage, notchImage, i == 0);
            if (notchImage != null)
                notchImage.color = CyberUiTheme.WithAlpha(AccentColorForTier(i + ThreatTierRules.MinTier), 0.92f);

            Text label = depthTierButtonLabels != null && i < depthTierButtonLabels.Length
                ? depthTierButtonLabels[i]
                : button.transform.Find("Text")?.GetComponent<Text>();
            if (label != null)
            {
                label.font = defaultFont;
                label.alignment = TextAnchor.UpperLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.resizeTextForBestFit = false;
                label.fontStyle = FontStyle.Bold;
                if (label.fontSize <= 0)
                    label.fontSize = templateLabel != null ? templateLabel.fontSize : 14;
                label.lineSpacing = 0.92f;
            }

            Text codeLabel = depthTierButtonCodeLabels != null && i < depthTierButtonCodeLabels.Length
                ? depthTierButtonCodeLabels[i]
                : button.transform.Find("TierCode")?.GetComponent<Text>();
            if (codeLabel != null)
                codeLabel.gameObject.SetActive(false);

            SetChildrenActive(button.transform, "SelectedRail", false);
            depthTierRecommendationLabels[i] = EnsureDepthTierRecommendationLabel(button.transform, i + ThreatTierRules.MinTier);
        }
    }

    private Text EnsureDepthTierRecommendationLabel(Transform buttonTransform, int tier)
    {
        if (buttonTransform == null)
            return null;

        Text recommendation = buttonTransform.Find("RecommendedLevel")?.GetComponent<Text>();
        bool created = recommendation == null;
        if (recommendation == null)
            recommendation = CreateText("RecommendedLevel", buttonTransform, 10, FontStyle.Bold, TextAnchor.UpperCenter, CyberUiTheme.TextPrimary);

        recommendation.font = defaultFont;
        if (created)
        {
            recommendation.fontSize = 10;
            recommendation.fontStyle = FontStyle.Bold;
            recommendation.alignment = TextAnchor.UpperCenter;
            recommendation.horizontalOverflow = HorizontalWrapMode.Overflow;
            recommendation.verticalOverflow = VerticalWrapMode.Overflow;
        }
        recommendation.resizeTextForBestFit = false;
        recommendation.raycastTarget = false;
        recommendation.text = BuildDepthTierRecommendationText(tier);

        if (created)
        {
            RectTransform rect = recommendation.rectTransform;
            rect.anchorMin = new Vector2(-0.220f, -0.600f);
            rect.anchorMax = new Vector2(1.220f, -0.040f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.localScale = Vector3.one;
        }

        Shadow shadow = FindExactShadow(recommendation);
        if (shadow == null)
            shadow = recommendation.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;

        return recommendation;
    }

    private static string BuildDepthTierRecommendationText(int tier)
    {
        ThreatTier clamped = ThreatTierRules.ClampTier(tier);
        return $"REC LV {ThreatTierRules.MinLevel(clamped)}-{ThreatTierRules.MaxLevel(clamped)}";
    }

    private static void CopyImageStyle(Image source, Image target, bool sameObject)
    {
        if (source == null || target == null || sameObject)
            return;

        target.sprite = source.sprite;
        target.color = source.color;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillOrigin = source.fillOrigin;
        target.fillClockwise = source.fillClockwise;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.material = source.material;
        target.raycastTarget = source.raycastTarget;
    }

    private static void SetChildrenActive(Transform parent, string childName, bool active)
    {
        if (parent == null)
            return;

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
                child.gameObject.SetActive(active);
        }
    }

    private void EnsureLaunchProtocolButton(Transform parent)
    {
        Transform targetParent = depthTierPanel != null ? depthTierPanel : parent;
        if (launchProtocolButton == null)
        {
            RectTransform rect = CreateCyberPanel(
                "LaunchProtocolButton",
                targetParent,
                new Color(0.006f, 0.020f, 0.036f, 0.92f),
                new Color(0.36f, 1f, 0.95f, 0.90f),
                new Color(0.08f, 0.92f, 1f, 1f),
                26f,
                true);
            launchProtocolButton = rect.gameObject.AddComponent<Button>();
            launchProtocolButton.transition = Selectable.Transition.None;
            launchProtocolButton.targetGraphic = rect.GetComponent<CyberFrameGraphic>();
            launchProtocolFeedback = rect.gameObject.AddComponent<CyberButtonFeedback>();
            launchProtocolFeedback.AccentRole = CyberUiColorRole.Selected;
        }

        RectTransform buttonRect = launchProtocolButton.GetComponent<RectTransform>();
        buttonRect.transform.SetParent(targetParent, false);
        SetAnchors(buttonRect, new Vector2(0.350f, 0.095f), new Vector2(0.830f, 0.250f));
        EnsureFrameRails(buttonRect, "LaunchProtocolRail", new Color(0.36f, 1f, 0.95f, 0.78f), 0.014f);

        RectTransform jackIcon = FindOrCreateCyberPanel(
            buttonRect,
            "JackIcon",
            new Color(0.003f, 0.012f, 0.022f, 0.35f),
            new Color(0.08f, 0.92f, 1f, 0.86f),
            new Color(0.36f, 1f, 0.95f, 1f),
            30f);
        SetAnchors(jackIcon, new Vector2(0.055f, 0.220f), new Vector2(0.185f, 0.800f));

        Text jackText = FindOrCreateText(jackIcon, "JackGlyph", 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.08f, 0.92f, 1f, 1f));
        jackText.text = ">";
        SetAnchors(jackText.rectTransform, Vector2.zero, Vector2.one);

        launchProtocolTitleText = launchProtocolTitleText != null
            ? launchProtocolTitleText
            : FindOrCreateText(buttonRect, "LaunchProtocolTitle", 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.99f, 1f, 1f));
        launchProtocolTitleText.fontSize = 18;
        launchProtocolTitleText.alignment = TextAnchor.MiddleLeft;
        ApplyCyberText(launchProtocolTitleText, new Color(0.03f, 0.1f, 0.16f, 0.95f), new Vector2(1f, -1f));
        SetAnchors(launchProtocolTitleText.rectTransform, new Vector2(0.300f, 0.455f), new Vector2(0.790f, 0.860f));

        launchProtocolDetailText = launchProtocolDetailText != null
            ? launchProtocolDetailText
            : FindOrCreateText(buttonRect, "LaunchProtocolDetail", 9, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.36f, 1f, 0.95f, 1f));
        launchProtocolDetailText.fontSize = 9;
        launchProtocolDetailText.alignment = TextAnchor.MiddleLeft;
        ApplyCyberText(launchProtocolDetailText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1f, -1f));
        SetAnchors(launchProtocolDetailText.rectTransform, new Vector2(0.300f, 0.205f), new Vector2(0.910f, 0.505f));

        EnsureLaunchProtocolBars(buttonRect);
        RefreshLaunchProtocolText();
    }

    private void EnsureLaunchProtocolBars(RectTransform parent)
    {
        Color[] colors =
        {
            CyberUiTheme.Success,
            CyberUiTheme.Selected,
            CyberUiTheme.Primary,
            CyberUiTheme.Danger,
            CyberUiTheme.Danger
        };

        for (int i = 0; i < colors.Length; i++)
        {
            Image bar = FindOrCreateImage($"ProtocolBar_{i + 1}", parent, colors[i]);
            float xMin = 0.815f + i * 0.022f;
            SetAnchors(bar.rectTransform, new Vector2(xMin, 0.460f), new Vector2(xMin + 0.014f, 0.690f));
            bar.color = colors[i];
        }
    }

    private void RefreshLaunchProtocolText()
    {
        if (manager == null)
            return;

        int selected = manager.SelectedThreatTierNumber;
        if (launchProtocolTitleText != null)
            launchProtocolTitleText.text = manager.IsRunActive ? "REOPEN GRID LINK" : "INITIALIZE GRID LINK";
        if (launchProtocolDetailText != null)
            launchProtocolDetailText.text = $"ROUTE_KERNEL -> DAG_NETWORK -> DEPTH_{selected:00}";
        if (launchProtocolFeedback != null)
            launchProtocolFeedback.Selected = true;
    }

    private RectTransform EnsureMainScreenProgress(Transform parent)
    {
        progressTrack = progressTrack != null
            ? progressTrack
            : CreateImage("MainScreenProgressTrack", parent, new Color(0.006f, 0.008f, 0.014f, 1f));
        progressTrack.raycastTarget = false;
        progressTrack.color = new Color(0.006f, 0.008f, 0.014f, 1f);
        RectTransform trackRect = progressTrack.rectTransform;
        SetAnchors(trackRect, new Vector2(0.580f, 0.386f), new Vector2(0.788f, 0.416f));

        progressBase = progressBase != null
            ? progressBase
            : CreateImage("MainScreenProgressBase", trackRect, new Color(1f, 0.20f, 0.52f, 0.22f));
        progressBase.transform.SetParent(trackRect, false);
        progressBase.raycastTarget = false;
        progressBase.color = new Color(0.018f, 0.024f, 0.038f, 1f);
        SetAnchors(progressBase.rectTransform, new Vector2(0.045f, 0.32f), new Vector2(0.955f, 0.68f));

        RectMask2D railMask = progressBase.GetComponent<RectMask2D>();
        if (railMask == null)
            railMask = progressBase.gameObject.AddComponent<RectMask2D>();
        railMask.padding = Vector4.zero;

        return trackRect;
    }

    private void EnsurePayloadPanel(Transform parent)
    {
        if (payloadPanel == null)
        {
            Image panelImage = CreateImage("PayloadDetailPanel", parent, new Color(0.006f, 0.012f, 0.026f, 0.86f));
            payloadPanel = panelImage.rectTransform;
        }

        Image background = payloadPanel.GetComponent<Image>();
        if (background == null)
            background = payloadPanel.gameObject.AddComponent<Image>();
        background.raycastTarget = false;
        background.color = new Color(0.006f, 0.012f, 0.026f, 0.86f);
        SetAnchors(payloadPanel, new Vector2(0.515f, 0.465f), new Vector2(0.888f, 0.785f));

        Image topLine = CreateImage("PayloadTopLine", payloadPanel, new Color(0.08f, 0.92f, 1f, 0.82f));
        SetAnchors(topLine.rectTransform, new Vector2(0f, 0.985f), new Vector2(1f, 1f));

        Image sideLine = CreateImage("PayloadSideLine", payloadPanel, new Color(1f, 0.25f, 0.86f, 0.58f));
        SetAnchors(sideLine.rectTransform, new Vector2(0f, 0f), new Vector2(0.012f, 1f));

        Image portraitFrame = CreateImage("PayloadPortraitFrame", payloadPanel, new Color(0.02f, 0.036f, 0.064f, 0.92f));
        SetAnchors(portraitFrame.rectTransform, new Vector2(0.045f, 0.23f), new Vector2(0.38f, 0.88f));

        payloadPortraitImage = payloadPortraitImage != null
            ? payloadPortraitImage
            : CreateImage("PayloadPortraitImage", portraitFrame.rectTransform, Color.white);
        payloadPortraitImage.transform.SetParent(portraitFrame.rectTransform, false);
        payloadPortraitImage.preserveAspect = true;
        payloadPortraitImage.raycastTarget = false;
        SetAnchors(payloadPortraitImage.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));

        payloadPortraitFallbackText = payloadPortraitFallbackText != null
            ? payloadPortraitFallbackText
            : CreateText("PayloadPortraitFallbackText", portraitFrame.rectTransform, 16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.78f, 1f, 1f, 1f));
        payloadPortraitFallbackText.transform.SetParent(portraitFrame.rectTransform, false);
        payloadPortraitFallbackText.color = new Color(0.78f, 1f, 1f, 1f);
        payloadPortraitFallbackText.lineSpacing = 0.9f;
        ApplyCyberText(payloadPortraitFallbackText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1f, -1f));
        SetAnchors(payloadPortraitFallbackText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));

        payloadListText = payloadListText != null
            ? payloadListText
            : CreateText("PayloadListText", payloadPanel, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.54f, 1f, 0.72f, 1f));
        payloadListText.transform.SetParent(payloadPanel, false);
        payloadListText.fontSize = 10;
        payloadListText.lineSpacing = 0.9f;
        payloadListText.color = new Color(0.54f, 1f, 0.72f, 1f);
        ApplyCyberText(payloadListText, new Color(0f, 0.2f, 0.12f, 1f), new Vector2(1f, -1f));
        SetAnchors(payloadListText.rectTransform, new Vector2(0.045f, 0.035f), new Vector2(0.38f, 0.205f));

        payloadDetailPanelText = payloadDetailPanelText != null
            ? payloadDetailPanelText
            : CreateText("PayloadDetailText", payloadPanel, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.96f, 0.99f, 1f, 1f));
        payloadDetailPanelText.transform.SetParent(payloadPanel, false);
        payloadDetailPanelText.fontSize = 10;
        payloadDetailPanelText.lineSpacing = 0.88f;
        payloadDetailPanelText.color = new Color(0.96f, 0.99f, 1f, 1f);
        ApplyCyberText(payloadDetailPanelText, new Color(0.03f, 0.1f, 0.16f, 0.95f), new Vector2(1f, -1f));
        SetAnchors(payloadDetailPanelText.rectTransform, new Vector2(0.43f, 0.06f), new Vector2(0.965f, 0.92f));
    }

    private static void ApplyCyberText(Text text, Color effectColor, Vector2 distance)
    {
        if (text == null)
            return;

        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = effectColor;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;

        Shadow shadow = FindExactShadow(text);
        if (shadow == null)
            shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;
    }

    private static Shadow FindExactShadow(Text text)
    {
        Shadow[] shadows = text.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                return shadows[i];
        }

        return null;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void RefreshDepthTierSelector()
    {
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        int selected = manager.SelectedThreatTierNumber;
        int highest = manager.HighestUnlockedThreatTierNumber;
        ThreatTier tier = manager.SelectedThreatTier;

        if (usingSourceLayoutTrialDepthButtons)
        {
            RefreshSourceLayoutDepthButtons(selected, highest);
            return;
        }

        if (depthTierTitleText != null)
            depthTierTitleText.text = "DEPTH_SELECT.exe";
        if (depthTierDetailText != null)
            depthTierDetailText.text = "SELECT THREAT AVATAR / ROUTE DEPTH / REWARD MODEL";
        if (depthTierAvatarImage != null)
        {
            depthTierAvatarImage.sprite = ResolveDepthTierSprite(selected);
            depthTierAvatarImage.enabled = depthTierAvatarImage.sprite != null;
            depthTierAvatarImage.color = Color.white;
        }
        if (depthTierSelectedSummaryText != null)
            depthTierSelectedSummaryText.text = $"SELECTED: {selected}F / THREAT T{selected:00}\nENEMY BAND LV {ThreatTierRules.MinLevel(tier):00}-{ThreatTierRules.MaxLevel(tier):00}";
        if (depthTierRewardSummaryText != null)
        {
            int rewardPercent = manager.IsRunActive
                ? Mathf.RoundToInt(manager.currentRewardMultiplier * 100f)
                : ThreatTierRules.RewardMultiplierPercent(tier, manager.HighestUnlockedThreatTier);
            depthTierRewardSummaryText.text = $"REWARD MULTIPLIER\nx{rewardPercent:000}% COMPUTE BOOST";
        }

        if (depthTierButtons == null)
            return;

        for (int i = 0; i < depthTierButtons.Length; i++)
        {
            Button button = depthTierButtons[i];
            if (button == null)
                continue;

            int buttonTier = i + ThreatTierRules.MinTier;
            bool unlocked = buttonTier <= highest;
            bool isSelected = buttonTier == selected;
            button.interactable = unlocked && !manager.IsRunActive;

            CyberFrameGraphic frame = depthTierButtonFrames != null && i < depthTierButtonFrames.Length
                ? depthTierButtonFrames[i]
                : button.GetComponent<CyberFrameGraphic>();
            Color accent = AccentColorForTier(buttonTier);
            if (frame != null)
            {
                frame.FillColor = isSelected
                    ? new Color(accent.r, accent.g, accent.b, 0.18f)
                    : new Color(0.007f, 0.018f, 0.032f, unlocked ? 0.86f : 0.54f);
                frame.BorderColor = new Color(accent.r, accent.g, accent.b, isSelected ? 0.98f : (unlocked ? 0.58f : 0.20f));
                frame.AccentColor = new Color(accent.r, accent.g, accent.b, isSelected ? 1f : 0.44f);
            }

            CyberButtonFeedback feedback = depthTierButtonFeedbacks != null && i < depthTierButtonFeedbacks.Length
                ? depthTierButtonFeedbacks[i]
                : button.GetComponent<CyberButtonFeedback>();
            if (feedback != null)
                feedback.Selected = isSelected;

            Image spriteImage = depthTierSpriteImages != null && i < depthTierSpriteImages.Length
                ? depthTierSpriteImages[i]
                : button.transform.Find("PseudoSprite")?.GetComponent<Image>();
            if (spriteImage != null)
            {
                spriteImage.sprite = ResolveDepthTierSprite(buttonTier);
                spriteImage.enabled = spriteImage.sprite != null;
                spriteImage.color = unlocked ? Color.white : new Color(0.45f, 0.48f, 0.55f, 0.52f);
            }

            Text label = depthTierButtonLabels != null && i < depthTierButtonLabels.Length
                ? depthTierButtonLabels[i]
                : button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = unlocked ? $"{buttonTier}F" : "--";
                label.color = isSelected
                    ? Color.white
                    : (unlocked ? new Color(0.96f, 0.99f, 1f, 1f) : new Color(0.48f, 0.54f, 0.60f, 1f));
            }

            Text codeLabel = depthTierButtonCodeLabels != null && i < depthTierButtonCodeLabels.Length
                ? depthTierButtonCodeLabels[i]
                : button.transform.Find("TierCode")?.GetComponent<Text>();
            if (codeLabel != null)
            {
                codeLabel.text = unlocked ? $"T0{buttonTier}" : "LOCK";
                codeLabel.color = unlocked ? accent : new Color(0.48f, 0.54f, 0.60f, 0.78f);
            }
        }
    }

    private void RefreshSourceLayoutDepthButtons(int selected, int highest)
    {
        NormalizeSourceLayoutDepthButtons();

        if (depthTierAvatarImage != null)
        {
            depthTierAvatarImage.sprite = ResolveDepthTierSprite(selected);
            depthTierAvatarImage.enabled = depthTierAvatarImage.sprite != null;
            depthTierAvatarImage.color = SourceLayoutDepthSpriteColor(selected);
        }

        if (depthTierButtons == null)
            return;

        for (int i = 0; i < depthTierButtons.Length; i++)
        {
            Button button = depthTierButtons[i];
            if (button == null)
                continue;

            int buttonTier = i + ThreatTierRules.MinTier;
            bool unlocked = buttonTier <= highest;
            bool isSelected = buttonTier == selected;
            Color accent = AccentColorForTier(buttonTier);
            button.interactable = unlocked && manager != null && !manager.IsRunActive;

            Color frameColor = isSelected
                ? CyberUiTheme.WithAlpha(CyberUiTheme.Primary, 0.96f)
                : (unlocked ? CyberUiTheme.WithAlpha(CyberUiTheme.Primary, 0.54f) : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.40f));
            Graphic targetGraphic = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
            if (targetGraphic != null)
                targetGraphic.color = frameColor;

            Graphic buttonFill = button.transform.Find("ButtonFill")?.GetComponent<Graphic>();
            if (buttonFill != null)
            {
                buttonFill.color = isSelected
                    ? new Color(0.010f, 0.045f, 0.060f, 0.62f)
                    : new Color(0.001f, 0.004f, 0.010f, 0.88f);
            }

            Graphic tierNotch = button.transform.Find("TierNotch")?.GetComponent<Graphic>();
            if (tierNotch != null)
            {
                tierNotch.color = unlocked
                    ? CyberUiTheme.WithAlpha(accent, isSelected ? 1f : 0.82f)
                    : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.42f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = frameColor;
            colors.highlightedColor = unlocked
                ? Color.Lerp(frameColor, Color.white, 0.22f)
                : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.40f);
            colors.pressedColor = unlocked
                ? Color.Lerp(frameColor, CyberUiTheme.Selected, 0.40f)
                : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.36f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.32f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            Text label = depthTierButtonLabels != null && i < depthTierButtonLabels.Length
                ? depthTierButtonLabels[i]
                : button.transform.Find("Text")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = unlocked ? $"{buttonTier}F" : "--";
                label.color = isSelected
                    ? Color.white
                    : (unlocked ? CyberUiTheme.TextPrimary : CyberUiTheme.WithAlpha(CyberUiTheme.TextSecondary, 0.58f));
            }

            Text codeLabel = depthTierButtonCodeLabels != null && i < depthTierButtonCodeLabels.Length
                ? depthTierButtonCodeLabels[i]
                : button.transform.Find("TierCode")?.GetComponent<Text>();
            if (codeLabel != null)
                codeLabel.gameObject.SetActive(false);

            Text recommendationLabel = depthTierRecommendationLabels != null && i < depthTierRecommendationLabels.Length
                ? depthTierRecommendationLabels[i]
                : button.transform.Find("RecommendedLevel")?.GetComponent<Text>();
            if (recommendationLabel == null)
            {
                recommendationLabel = EnsureDepthTierRecommendationLabel(button.transform, buttonTier);
                if (depthTierRecommendationLabels != null && i < depthTierRecommendationLabels.Length)
                    depthTierRecommendationLabels[i] = recommendationLabel;
            }

            if (recommendationLabel != null)
            {
                recommendationLabel.gameObject.SetActive(true);
                recommendationLabel.text = BuildDepthTierRecommendationText(buttonTier);
                recommendationLabel.color = isSelected
                    ? CyberUiTheme.WithAlpha(CyberUiTheme.Primary, 1f)
                    : (unlocked ? CyberUiTheme.WithAlpha(CyberUiTheme.TextPrimary, 0.94f) : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.58f));
            }

            Transform rail = button.transform.Find("SelectedRail");
            if (rail != null)
                rail.gameObject.SetActive(false);
            SetChildrenActive(button.transform, "SelectedRail", false);
        }
    }

    private void RefreshSourceLayoutBossRoutes()
    {
        if (sourceLayoutBossRouteButtons == null || manager == null)
            return;

        string selectedCode = NormalizeBossRouteCode(manager.SelectedBossSpeciesCodeName);
        if (!string.Equals(sourceLayoutBossRouteLastSelectedCode, selectedCode, StringComparison.OrdinalIgnoreCase))
        {
            sourceLayoutBossRouteLastSelectedCode = selectedCode;
            sourceLayoutBossRouteSelectionFlashStartTime = Time.unscaledTime;
        }

        bool locked = manager.IsRunActive;
        int count = Mathf.Min(sourceLayoutBossRouteButtons.Length, BossRouteSpecies.Length);
        for (int i = 0; i < count; i++)
        {
            Button button = sourceLayoutBossRouteButtons[i];
            if (button == null)
                continue;

            bool isSelected = string.Equals(
                NormalizeBossRouteCode(BossRouteSpecies[i]),
                selectedCode,
                StringComparison.OrdinalIgnoreCase);
            Color accent = BossRouteAccentColor(i);
            button.interactable = !locked;

            Graphic frame = CachedGraphic(sourceLayoutBossRouteFrames, i);
            if (frame != null)
            {
                frame.color = isSelected
                    ? CyberUiTheme.WithAlpha(CyberUiTheme.Selected, locked ? 0.78f : 0.96f)
                    : CyberUiTheme.WithAlpha(Color.Lerp(CyberUiTheme.RoomPurple, CyberUiTheme.Primary, 0.16f), locked ? 0.28f : 0.48f);
            }

            Graphic fill = CachedGraphic(sourceLayoutBossRouteFills, i);
            if (fill != null)
            {
                fill.color = isSelected
                    ? new Color(0.006f, 0.022f, 0.032f, 0.48f)
                    : new Color(0.006f, 0.010f, 0.022f, locked ? 0.74f : 0.64f);
            }

            Graphic activeFrame = CachedGraphic(sourceLayoutBossRouteActiveFrames, i);
            if (activeFrame != null)
                activeFrame.gameObject.SetActive(false);

            Graphic shadow = CachedGraphic(sourceLayoutBossRouteShadows, i);
            if (shadow != null)
                shadow.gameObject.SetActive(false);

            RefreshBossRouteSelectionFrame(i, isSelected, accent, locked);

            Graphic portrait = CachedGraphic(sourceLayoutBossRoutePortraits, i);
            if (portrait != null)
            {
                portrait.color = isSelected
                    ? Color.white
                    : CyberUiTheme.WithAlpha(Color.Lerp(new Color(0.58f, 0.62f, 0.72f, 1f), accent, 0.10f), locked ? 0.42f : 0.72f);
            }
            ApplyBossRouteIdleFrame(i, isSelected);

            Graphic portraitBackdrop = CachedGraphic(sourceLayoutBossRoutePortraitBackdrops, i);
            if (portraitBackdrop != null)
            {
                portraitBackdrop.color = isSelected
                    ? new Color(0.004f, 0.020f, 0.028f, 0.70f)
                    : new Color(0.004f, 0.004f, 0.012f, locked ? 0.82f : 0.74f);
            }

            Graphic notch = CachedGraphic(sourceLayoutBossRouteSignalNotches, i);
            if (notch != null)
                notch.color = CyberUiTheme.WithAlpha(accent, isSelected ? 0.78f : (locked ? 0.24f : 0.44f));

            CyberImageButtonFeedback feedback = sourceLayoutBossRouteFeedbacks != null && i < sourceLayoutBossRouteFeedbacks.Length
                ? sourceLayoutBossRouteFeedbacks[i]
                : button.GetComponent<CyberImageButtonFeedback>();
            if (feedback != null)
                feedback.enabled = false;
            button.transform.localScale = Vector3.one;

            Text code = CachedText(sourceLayoutBossRouteCodes, i);
            Color codeColor = CyberUiTheme.WithAlpha(CyberUiTheme.TextSecondary, locked && !isSelected ? 0.54f : 0.96f);
            if (code != null)
                code.color = codeColor;
            SetBossRouteBitmapText(
                CachedBitmapText(sourceLayoutBossRouteBitmapCodes, i),
                code != null && !string.IsNullOrWhiteSpace(code.text) ? code.text : $"R{i + 1:00}",
                codeColor);

            Text label = CachedText(sourceLayoutBossRouteLabels, i);
            Color labelColor = isSelected
                ? CyberUiTheme.TextPrimary
                : CyberUiTheme.WithAlpha(CyberUiTheme.TextPrimary, locked ? 0.62f : 0.94f);
            if (label != null)
            {
                label.text = BossRouteSpecies[i].ToUpperInvariant();
                label.color = labelColor;
            }
            SetBossRouteBitmapText(
                CachedBitmapText(sourceLayoutBossRouteBitmapLabels, i),
                BossRouteSpecies[i].ToUpperInvariant(),
                labelColor);

            Text elementTag = CachedText(sourceLayoutBossRouteElementTags, i);
            Color readableAccent = Color.Lerp(accent, CyberUiTheme.TextPrimary, 0.46f);
            Color elementColor = CyberUiTheme.WithAlpha(readableAccent, isSelected ? 1f : (locked ? 0.68f : 0.98f));
            if (elementTag != null)
                elementTag.color = elementColor;
            SetBossRouteBitmapText(
                CachedBitmapText(sourceLayoutBossRouteBitmapElementTags, i),
                elementTag != null && !string.IsNullOrWhiteSpace(elementTag.text) ? elementTag.text : "--",
                elementColor);

            Text status = CachedText(sourceLayoutBossRouteStatuses, i);
            string statusText = isSelected ? "TARGET" : (locked ? "LOCK" : "READY");
            Color statusColor = isSelected
                ? CyberUiTheme.Selected
                : CyberUiTheme.WithAlpha(CyberUiTheme.TextPrimary, locked ? 0.68f : 0.98f);
            if (status != null)
            {
                status.text = statusText;
                status.color = statusColor;
            }
            SetBossRouteBitmapText(CachedBitmapText(sourceLayoutBossRouteBitmapStatuses, i), statusText, statusColor);

            Transform rail = sourceLayoutBossRouteSelectedRails != null && i < sourceLayoutBossRouteSelectedRails.Length
                ? sourceLayoutBossRouteSelectedRails[i]
                : button.transform.Find("SelectedRail");
            if (rail != null)
                rail.gameObject.SetActive(false);
        }
    }

    private void RefreshBossRouteSelectionFrame(int index, bool isSelected, Color accent, bool locked)
    {
        RectTransform frameRoot = sourceLayoutBossRouteSelectionFrames != null && index >= 0 && index < sourceLayoutBossRouteSelectionFrames.Length
            ? sourceLayoutBossRouteSelectionFrames[index]
            : null;
        if (frameRoot == null)
            return;

        frameRoot.gameObject.SetActive(isSelected);
        if (!isSelected)
            return;

        float elapsed = Time.unscaledTime - sourceLayoutBossRouteSelectionFlashStartTime;
        bool flashActive = elapsed >= 0f && elapsed < BossRouteSelectionFlashSeconds;
        bool flashOn = flashActive && Mathf.FloorToInt(elapsed / BossRouteSelectionFlashStepSeconds) % 2 == 0;
        Color cyanBlue = Color.Lerp(CyberUiTheme.Primary, CyberUiTheme.Selected, 0.34f);
        Color selectedColor = flashOn ? Color.Lerp(cyanBlue, Color.white, 0.55f) : cyanBlue;
        float lineAlpha = locked ? 0.62f : (flashOn ? 1f : 0.96f);
        Image selectionPanel = sourceLayoutBossRouteSelectionPanels != null && index >= 0 && index < sourceLayoutBossRouteSelectionPanels.Length
            ? sourceLayoutBossRouteSelectionPanels[index]
            : frameRoot.GetComponent<Image>();
        Color panelColor = CyberUiTheme.WithAlpha(
            flashOn
                ? Color.Lerp(CyberUiTheme.Primary, Color.white, 0.14f)
                : Color.Lerp(CyberUiTheme.Primary, CyberUiTheme.Background, 0.50f),
            locked ? 0.28f : (flashOn ? 0.54f : 0.40f));
        SetImageColor(selectionPanel, panelColor);

        Color lineColor = CyberUiTheme.WithAlpha(selectedColor, lineAlpha);
        Color cornerColor = CyberUiTheme.WithAlpha(selectedColor, locked ? 0.56f : (flashOn ? 0.98f : 0.86f));
        SetSelectionShapeColor(CachedImage(sourceLayoutBossRouteSelectionTopBars, index), lineColor);
        SetSelectionShapeColor(CachedImage(sourceLayoutBossRouteSelectionBottomBars, index), lineColor);
        SetSelectionShapeColor(CachedImage(sourceLayoutBossRouteSelectionLeftBars, index), lineColor);
        SetSelectionShapeColor(CachedImage(sourceLayoutBossRouteSelectionRightBars, index), lineColor);
        SetSelectionShapeColor(CachedImage(sourceLayoutBossRouteSelectionTopLeftCorners, index), cornerColor);
        SetSelectionShapeColor(CachedImage(sourceLayoutBossRouteSelectionBottomRightCorners, index), cornerColor);
    }

    private static void SetImageColor(Image image, Color color)
    {
        if (image != null)
            image.color = color;
    }

    private static void SetSelectionShapeColor(Image image, Color color)
    {
        if (image == null)
            return;

        image.gameObject.SetActive(color.a > 0f);
        image.color = color;
    }

    private void ApplyBossRouteIdleFrame(int index, bool isSelected)
    {
        Image portraitImage = CachedImage(sourceLayoutBossRoutePortraitImages, index);
        Sprite[] frames = sourceLayoutBossRouteIdleFrames != null && index >= 0 && index < sourceLayoutBossRouteIdleFrames.Length
            ? sourceLayoutBossRouteIdleFrames[index]
            : null;
        if (portraitImage == null ||
            frames == null ||
            frames.Length == 0 ||
            sourceLayoutBossRouteIdleTimers == null ||
            sourceLayoutBossRouteIdleFrameIndices == null ||
            index >= sourceLayoutBossRouteIdleTimers.Length ||
            index >= sourceLayoutBossRouteIdleFrameIndices.Length)
        {
            return;
        }

        if (!isSelected || frames.Length == 1)
        {
            sourceLayoutBossRouteIdleTimers[index] = 0f;
            sourceLayoutBossRouteIdleFrameIndices[index] = 0;
            portraitImage.sprite = frames[0];
            return;
        }

        float frameSeconds = sourceLayoutBossRouteIdleFrameSeconds != null && index < sourceLayoutBossRouteIdleFrameSeconds.Length
            ? sourceLayoutBossRouteIdleFrameSeconds[index]
            : 1f / BossRouteFallbackIdleFps;
        frameSeconds = Mathf.Max(0.04f, frameSeconds);

        sourceLayoutBossRouteIdleTimers[index] += Time.unscaledDeltaTime;
        while (sourceLayoutBossRouteIdleTimers[index] >= frameSeconds)
        {
            sourceLayoutBossRouteIdleTimers[index] -= frameSeconds;
            sourceLayoutBossRouteIdleFrameIndices[index] = (sourceLayoutBossRouteIdleFrameIndices[index] + 1) % frames.Length;
        }

        portraitImage.sprite = frames[sourceLayoutBossRouteIdleFrameIndices[index]];
    }

    private static Graphic CachedGraphic(Graphic[] graphics, int index)
    {
        return graphics != null && index >= 0 && index < graphics.Length ? graphics[index] : null;
    }

    private static Image CachedImage(Image[] images, int index)
    {
        return images != null && index >= 0 && index < images.Length ? images[index] : null;
    }

    private static Text CachedText(Text[] texts, int index)
    {
        return texts != null && index >= 0 && index < texts.Length ? texts[index] : null;
    }

    private static CyberBitmapTextGraphic CachedBitmapText(CyberBitmapTextGraphic[] texts, int index)
    {
        return texts != null && index >= 0 && index < texts.Length ? texts[index] : null;
    }

    private static void SetBossRouteBitmapText(CyberBitmapTextGraphic bitmapText, string value, Color color)
    {
        if (bitmapText == null)
            return;

        bitmapText.Text = value;
        bitmapText.color = color;
    }

    private static Color BossRouteAccentColor(int index)
    {
        Color purple = CyberUiTheme.RoomPurple;
        switch (index)
        {
            case 0:
                return CyberUiTheme.Selected;
            case 1:
                return Color.Lerp(purple, CyberUiTheme.Reward, 0.42f);
            case 2:
                return Color.Lerp(purple, CyberUiTheme.Primary, 0.58f);
            case 3:
                return Color.Lerp(purple, CyberUiTheme.Danger, 0.50f);
            case 4:
                return Color.Lerp(purple, CyberUiTheme.Success, 0.36f);
            case 5:
            default:
                return Color.Lerp(purple, CyberUiTheme.Primary, 0.34f);
        }
    }

    private static string NormalizeBossRouteCode(string codeName)
    {
        if (string.IsNullOrWhiteSpace(codeName))
            return BossRouteSpecies[0].ToUpperInvariant();

        string trimmed = codeName.Trim();
        for (int i = 0; i < BossRouteSpecies.Length; i++)
        {
            if (string.Equals(trimmed, BossRouteSpecies[i], StringComparison.OrdinalIgnoreCase))
                return BossRouteSpecies[i].ToUpperInvariant();
        }

        return trimmed.ToUpperInvariant();
    }

    private static Color SourceLayoutDepthSpriteColor(int tier)
    {
        Color accent = AccentColorForTier(tier);
        return CyberUiTheme.WithAlpha(Color.Lerp(accent, CyberUiTheme.TextPrimary, 0.18f), 0.92f);
    }

    private void WireDepthTierButtons()
    {
        if (depthTierButtons == null)
            return;

        if (depthTierButtonActions == null || depthTierButtonActions.Length != depthTierButtons.Length)
        {
            depthTierButtonActions = new UnityEngine.Events.UnityAction[depthTierButtons.Length];
            for (int i = 0; i < depthTierButtonActions.Length; i++)
            {
                int tier = i + ThreatTierRules.MinTier;
                depthTierButtonActions[i] = () => SelectDepthTier(tier);
            }
        }

        for (int i = 0; i < depthTierButtons.Length; i++)
            WireButton(depthTierButtons[i], depthTierButtonActions[i]);
    }

    private void UnwireDepthTierButtons()
    {
        if (depthTierButtons == null || depthTierButtonActions == null)
            return;

        int count = Mathf.Min(depthTierButtons.Length, depthTierButtonActions.Length);
        for (int i = 0; i < count; i++)
            UnwireButton(depthTierButtons[i], depthTierButtonActions[i]);
    }

    private void WireBossRouteButtons()
    {
        if (sourceLayoutBossRouteButtons == null)
            return;

        if (sourceLayoutBossRouteActions == null || sourceLayoutBossRouteActions.Length != sourceLayoutBossRouteButtons.Length)
        {
            sourceLayoutBossRouteActions = new UnityEngine.Events.UnityAction[sourceLayoutBossRouteButtons.Length];
            for (int i = 0; i < sourceLayoutBossRouteActions.Length; i++)
            {
                string speciesCodeName = i < BossRouteSpecies.Length ? BossRouteSpecies[i] : string.Empty;
                sourceLayoutBossRouteActions[i] = () => SelectBossRoute(speciesCodeName);
            }
        }

        int count = Mathf.Min(sourceLayoutBossRouteButtons.Length, sourceLayoutBossRouteActions.Length);
        for (int i = 0; i < count; i++)
            WireButton(sourceLayoutBossRouteButtons[i], sourceLayoutBossRouteActions[i]);
    }

    private void UnwireBossRouteButtons()
    {
        if (sourceLayoutBossRouteButtons == null || sourceLayoutBossRouteActions == null)
            return;

        int count = Mathf.Min(sourceLayoutBossRouteButtons.Length, sourceLayoutBossRouteActions.Length);
        for (int i = 0; i < count; i++)
            UnwireButton(sourceLayoutBossRouteButtons[i], sourceLayoutBossRouteActions[i]);
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnwireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private static void EnsureStarterParty(GameManager targetManager, AlgoMonData starterData)
    {
        if (targetManager == null || targetManager.party == null)
            return;

        if (targetManager.party.Count == 0)
        {
            AlgoMonData starterSpecies = starterData != null
                ? starterData
                : FindPartyCandidate(targetManager, "Sortex");
            TryAddPartyMember(targetManager, starterSpecies, 0);
        }

        int targetCount = Mathf.Min(MinimumPlayablePartySize, GameManager.MaxPartySize);
        while (targetManager.party.Count < targetCount)
        {
            AlgoMonData reserveSpecies = FindReserveCandidate(targetManager);
            if (!TryAddPartyMember(targetManager, reserveSpecies, targetManager.party.Count))
                break;
        }
    }

    private static bool TryAddPartyMember(GameManager targetManager, AlgoMonData species, int slotIndex)
    {
        if (targetManager == null || species == null || targetManager.party == null)
            return false;
        if (targetManager.party.Count >= GameManager.MaxPartySize)
            return false;
        if (PartyContainsSpecies(targetManager, species))
            return false;

        var member = new AlgoMonInstance
        {
            data = species,
            nickname = species.codeName,
            level = StarterLevel,
            iv_Battery = Mathf.Clamp(180 - slotIndex * 8, 1, 255),
            iv_ClockSpeed = Mathf.Clamp(165 + slotIndex * 5, 1, 255),
            iv_ComputingPower = Mathf.Clamp(150 + slotIndex * 4, 1, 255),
            iv_Throughput = Mathf.Clamp(145 + slotIndex * 6, 1, 255),
            iv_Firewall = Mathf.Clamp(130 + slotIndex * 7, 1, 255),
            iv_Encryption = Mathf.Clamp(135 + slotIndex * 7, 1, 255)
        };
        member.EnsureKnownSkillsFromLearnset();
        return targetManager.AddToParty(member);
    }

    private static AlgoMonData FindReserveCandidate(GameManager targetManager)
    {
        for (int i = 0; i < PreferredReserveSpecies.Length; i++)
        {
            AlgoMonData preferred = FindPartyCandidate(targetManager, PreferredReserveSpecies[i]);
            if (preferred != null)
                return preferred;
        }

        return FindPartyCandidate(targetManager, null);
    }

    private static AlgoMonData FindPartyCandidate(GameManager targetManager, string preferredCodeName)
    {
        List<AlgoMonData> candidates = LoadPartyCandidateSpecies();
        if (!string.IsNullOrWhiteSpace(preferredCodeName))
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                AlgoMonData candidate = candidates[i];
                if (candidate != null &&
                    !PartyContainsSpecies(targetManager, candidate) &&
                    string.Equals(NormalizedCodeName(candidate), preferredCodeName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            AlgoMonData candidate = candidates[i];
            if (candidate != null && !PartyContainsSpecies(targetManager, candidate))
                return candidate;
        }

        return null;
    }

    private static List<AlgoMonData> LoadPartyCandidateSpecies()
    {
        var candidates = new List<AlgoMonData>();
        EncounterSpeciesCatalog catalog = Resources.Load<EncounterSpeciesCatalog>(EncounterSpeciesCatalogResourcePath);
        if (catalog != null)
        {
            AlgoMonData[] catalogSpecies = catalog.GetSpecies();
            for (int i = 0; i < catalogSpecies.Length; i++)
                AddUniqueCandidate(candidates, catalogSpecies[i]);
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AlgoMonData", new[] { AlgoMonAssetSearchFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AddUniqueCandidate(candidates, AssetDatabase.LoadAssetAtPath<AlgoMonData>(path));
        }
#endif

        candidates.Sort((a, b) => string.Compare(NormalizedCodeName(a), NormalizedCodeName(b), StringComparison.Ordinal));
        return candidates;
    }

    private static void AddUniqueCandidate(List<AlgoMonData> candidates, AlgoMonData candidate)
    {
        if (candidate == null)
            return;

        string codeName = NormalizedCodeName(candidate);
        for (int i = 0; i < candidates.Count; i++)
        {
            AlgoMonData existing = candidates[i];
            if (existing == candidate ||
                string.Equals(NormalizedCodeName(existing), codeName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        candidates.Add(candidate);
    }

    private static bool PartyContainsSpecies(GameManager targetManager, AlgoMonData species)
    {
        if (targetManager == null || targetManager.party == null || species == null)
            return false;

        string codeName = NormalizedCodeName(species);
        for (int i = 0; i < targetManager.party.Count; i++)
        {
            AlgoMonInstance mon = targetManager.party[i];
            if (mon == null || mon.data == null)
                continue;
            if (mon.data == species ||
                string.Equals(NormalizedCodeName(mon.data), codeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizedCodeName(AlgoMonData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.codeName))
            return string.Empty;
        return data.codeName.Trim();
    }

    private static string FormatClock(float elapsed)
    {
        int totalSeconds = Mathf.FloorToInt(elapsed);
        int hours = 20 + totalSeconds / 3600;
        int minutes = (totalSeconds / 60) % 60;
        int seconds = totalSeconds % 60;
        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }
}
