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
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private const string PanelButtonSpriteRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD/Buttons/Blue";
    private const string PanelButtonNormalSpritePath = PanelButtonSpriteRoot + "/ButtonE_Unpressed.png";
    private const string PanelButtonHighlightedSpritePath = PanelButtonSpriteRoot + "/ButtonE_Unpressed.png";
    private const string PanelButtonPressedSpritePath = PanelButtonSpriteRoot + "/ButtonF_Pressed.png";
    private const string PanelButtonHoverGlowSpritePath = PanelButtonSpriteRoot + "/ButtonStone_Highlighted.png";
    private const string PanelFrameSpriteRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal/Inspector";
    private const string SquadPanelFrameSpritePath = PanelFrameSpriteRoot + "/PanelFrame01.png";
    private const string PayloadInspectorPanelSpritePath = PanelFrameSpriteRoot + "/PanelFrame03.png";
    private const string MonsterDisplayPanelSpritePath = PanelFrameSpriteRoot + "/PanelFrame03.png";
    private const string InspectorExpBarFillSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/Components/CyberpunkHUD/progress_fill_striped_texture_tint.png";
    private const string InspectorExpBarUnderSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/health_bar_under.png";
    private const string TerminalToggleOnSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/toggle_on.png";
    private const string TerminalToggleOffSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/toggle_off.png";
    private const string SliderTrackSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/slider_track_bg.png";
    private const string SliderFillSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/slider_fill_highlight.png";
    private const string SliderHandleSpritePath = "Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/slider_handle.png";
    private const float PanelButtonPixelsPerUnit = 100f;
    private const float TerminalZoomFitPadding = 0.985f;
    private const string TerminalZoomPlayerPrefsKey = "AlgoMon.MainTerminal.TerminalZoomMode";
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
    private const float DepthRecommendationBitmapScale = 0.62f;
    private const int DepthRouteLayerFontSize = 12;
    private const float SystemStatusBitmapScale = 0.82f;
    private const float BossRouteMetaBitmapScale = 0.58f;
    private const float TmpReadableOutlineWidth = 0.085f;
    private static readonly Vector2 SourceLayoutBossRouteFallbackSize = new Vector2(118f, 232f);
    private static readonly Vector2 SourceLayoutBossRouteSelectionPadding = new Vector2(16f, 10f);
    private static readonly Vector2 BossRouteSelectionCornerSize = new Vector2(20f, 18f);
    private static readonly Color TmpReadableOutlineColor = new Color(0.001f, 0.010f, 0.018f, 0.96f);
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
    [SerializeField] private Button payloadPreviousButton;
    [SerializeField] private Button payloadNextButton;
    [SerializeField] private Button geneLabFuseButton;
    [SerializeField] private Button geneLabEvolveButton;
    [SerializeField] private RectTransform depthTierPanel;
    [SerializeField] private Text depthTierTitleText;
    [SerializeField] private Text depthTierDetailText;
    [SerializeField] private Button[] depthTierButtons;
    [SerializeField] private bool unlockAllThreatTiersForVerticalSlice = true;

    [Header("Visual Assets")]
    [SerializeField] private Sprite[] depthTierPseudoSprites;
    [SerializeField] private Sprite backArrowSprite;
    [SerializeField] private Sprite panelButtonNormalSprite;
    [SerializeField] private Sprite panelButtonHighlightedSprite;
    [SerializeField] private Sprite panelButtonPressedSprite;

    [Header("Payload Storage Slots (PRO Cyberpunk HUD-derived)")]
    [SerializeField] private Sprite slotNormalSprite;
    [SerializeField] private Sprite slotSelectedSprite;
    [SerializeField] private Sprite slotEpicSprite;
    [SerializeField] private Sprite slotLegendarySprite;

    [Header("Payload Inspector (game-programming assets)")]
    [SerializeField] private Sprite monsterBaseOvalSprite;
    [SerializeField] private Sprite talentBarFillSprite;
    [SerializeField] private Sprite payloadInspectorPanelSprite;
    [SerializeField] private Sprite squadPanelBackgroundSprite;
    [SerializeField] private Sprite monsterDisplayPanelSprite;

    [Header("Starter Fallback")]
    [SerializeField] private AlgoMonData fallbackStarter;

    private GameManager manager;
    private float bootTime;
    private Font defaultFont;
    private int selectedPayloadIndex = -1;
    private int geneLabFusionSecondIndex = -1;
    private bool showingGeneLabPanel;
    private bool geneLabRouteSelectionMode;
    private string geneLabActionMessage = string.Empty;
    private string geneLabSkillMessage = string.Empty;
    private string selectedGeneLabSpeciesCode = string.Empty;
    private string payloadSkillMessage = string.Empty;
    private SkillData pendingPayloadSkillReplacement;
    private Transform sourceLayoutVisual;
    private bool sourceLayoutStaticLabelBitmapsReady;
    private RectTransform[] menuContentGroups;
    private RectTransform sectionViewRoot;
    private Text sectionTitleText;
    private Button sectionBackButton;
    private bool inSectionView;

    private const int PayloadGridColumns = 5;
    private const int PayloadGridRows = 4;
    private const int PayloadGridCellCount = PayloadGridColumns * PayloadGridRows;
    private const int InitialPayloadFillCount = PayloadGridCellCount;
    private RectTransform payloadGridRoot;
    private Image[] payloadCellFrames;
    private Image[] payloadCellSprites;
    private Text[] payloadCellLabels;
    private Image[] payloadCellFavoriteMarkers;
    private Button[] payloadCellButtons;
    private UnityEngine.Events.UnityAction[] payloadCellActions;
    private int[] payloadCellPayloadIndices;
    private readonly List<AlgoMonInstance> payloadDisplayOrder = new List<AlgoMonInstance>();
    private int payloadPage;
    private int hoveredPayloadCellIndex = -1;
    private Button payloadPrevPageButton;
    private Button payloadNextPageButton;
    private Text payloadPageLabel;
    private RectTransform geneLabPanelRoot;
    private RectTransform geneLabRouteSelectionRoot;
    private RectTransform geneLabBenchRoot;
    private Text geneLabRoutePromptText;
    private Button[] geneLabSpeciesButtons;
    private Image[] geneLabSpeciesFrames;
    private Text[] geneLabSpeciesLabels;
    private Text[] geneLabSpeciesMetaLabels;
    private Image[] geneLabRoutePortraitImages;
    private Sprite[][] geneLabRouteIdleFrames;
    private float[] geneLabRouteIdleFrameSeconds;
    private float[] geneLabRouteIdleTimers;
    private int[] geneLabRouteIdleFrameIndices;
    private Button[] geneLabMiniPayloadButtons;
    private Image[] geneLabMiniPayloadFrames;
    private Image[] geneLabMiniPayloadSprites;
    private Text[] geneLabMiniPayloadLabels;
    private TextMeshProUGUI[] geneLabMiniPayloadBitmapLabels;
    private int[] geneLabMiniPayloadIndices;
    private Button geneLabMiniPayloadPrevButton;
    private Button geneLabMiniPayloadNextButton;
    private Text geneLabMiniPayloadPageLabel;
    private int geneLabPayloadPage;
    private Text geneLabFusionText;
    private TextMeshProUGUI geneLabFusionBitmapText;
    private RectTransform geneLabFusionTalentRoot;
    private Text geneLabFusionTalentCaptionText;
    private TextMeshProUGUI geneLabFusionTalentCaptionBitmapText;
    private Image[] geneLabFusionTargetTalentFills;
    private Image[] geneLabFusionMaterialTalentFills;
    private Image[] geneLabFusionProjectedTalentFills;
    private Text[] geneLabFusionTargetTalentValues;
    private Text[] geneLabFusionMaterialTalentValues;
    private Text[] geneLabFusionProjectedTalentValues;
    private Image[] geneLabFusionPortraitImages;
    private Text[] geneLabFusionNameTexts;
    private Text[] geneLabFusionMetaTexts;
    private Sprite[][] geneLabFusionIdleFrames;
    private float[] geneLabFusionIdleFps;
    private float[] geneLabFusionIdleTimers;
    private int[] geneLabFusionIdleFrameIndices;
    private string[] geneLabFusionIdleKeys;
    private Button geneLabPreviousRecordButton;
    private Button geneLabNextRecordButton;
    private Button geneLabFuseActionButton;
    private Button geneLabEvolveActionButton;
    private RectTransform exitPanelRoot;
    private Text exitPanelStatusText;
    private Button exitReturnButton;
    private Button exitConfirmButton;
    private RectTransform settingsPanelRoot;
    private Button terminalZoomToggleButton;
    private Image terminalZoomToggleImage;
    private Text terminalZoomStatusText;
    private Slider musicVolumeSlider;
    private Text musicVolumeValueText;
    private Slider sfxVolumeSlider;
    private Text sfxVolumeValueText;
    private Text menuTrackNameText;
    private RectTransform terminalZoomBlackoutRoot;
    private Image terminalZoomBlackoutImage;
    private bool terminalZoomModeEnabled;
    private bool terminalBaseRectCaptured;
    private int terminalBaseSiblingIndex = -1;
    private Vector2 terminalBaseAnchorMin;
    private Vector2 terminalBaseAnchorMax;
    private Vector2 terminalBaseAnchoredPosition;
    private Vector2 terminalBaseSizeDelta;
    private Vector2 terminalBaseOffsetMin;
    private Vector2 terminalBaseOffsetMax;
    private Vector2 terminalBasePivot;
    private Vector2 terminalBaseRectSize;
    private Vector3 terminalBaseLocalScale = Vector3.one;
    private float terminalZoomAppliedScale = 1f;

    private static readonly string[] StatAxisLabels = { "BAT", "CLK", "CPU", "THR", "FWL", "ENC" };
    private static readonly Color GeneLabTargetTalentColor = new Color(0.20f, 1f, 0.95f, 0.96f);
    private static readonly Color GeneLabMaterialTalentColor = new Color(1f, 0.34f, 0.88f, 0.94f);
    private static readonly Color GeneLabProjectedTalentColor = new Color(1f, 0.86f, 0.30f, 0.98f);
    private const float RadarMaxStat = 450f;
    private const int GeneLabMiniPayloadCellCount = 8;
    private const int SkillSwapLearnCount = 6;
    private Button inspectorSquadButton;
    private Text inspectorSquadButtonLabel;
    private Button inspectorViewSquadButton;
    private Button inspectorFavoriteButton;
    private Text inspectorFavoriteButtonLabel;
    private Image inspectorFavoriteStar;
    private Button inspectorSkillsButton;
    private Text inspectorSkillsButtonLabel;
    private RectTransform squadPanelRoot;
    private Text squadPanelTitle;
    private Image[] squadSlotPortraits;
    private Text[] squadSlotLabels;
    private Text[] squadSlotLeadBadges;
    private Button[] squadSlotLeadButtons;
    private Button[] squadSlotActionButtons;
    private Text[] squadSlotActionLabels;
    private bool squadReplaceMode;
    private AlgoMonInstance squadReplaceIncoming;
    private Image inspectorPortraitImage;
    private Text inspectorNameText;
    private Image inspectorExpFill;
    private Text inspectorExpText;
    private RadarChartGraphic inspectorRadar;
    private RectTransform inspectorRadarRoot;
    private Text[] inspectorRadarLabels;
    private RectTransform inspectorTalentRoot;
    private Image[] inspectorTalentFills;
    private Text[] inspectorTalentValues;

    // Dedicated skill swap/learn popup (built once, opened from the SKILLS button).
    private RectTransform skillSwapPanelRoot;
    private Text skillSwapTitle;
    private Text skillSwapMessageText;
    private SkillCardRefs[] skillSwapLoadoutCards;
    private SkillCardRefs[] skillSwapLearnCards;
    private LearnsetEntry[] skillSwapLearnEntries;
    private int skillSwapSelectedSlot = -1;
    private Text skillSwapDetailName;
    private Image skillSwapDetailBadge;
    private Image skillSwapDetailBadgeIcon;
    private Text skillSwapDetailBadgeLetter;
    private Image skillSwapDetailElementIcon;
    private Image skillSwapDetailCPChip;
    private Text skillSwapDetailCPText;
    private Image skillSwapDetailPowerChip;
    private Text skillSwapDetailPowerText;
    private Image skillSwapDetailCounterChip;
    private Text skillSwapDetailCounterText;
    private Text skillSwapDetailBody;

    /// <summary>Child references for one battle-style skill card in the swap popup.</summary>
    private sealed class SkillCardRefs
    {
        public RectTransform Root;
        public Button Button;
        public Image Frame;
        public CanvasGroup Group;
        public Image InstructionBadge;
        public Image InstructionIcon;
        public Text InstructionLetter;
        public Text NameText;
        public Image ElementIcon;
        public Image CPChip;
        public Text CPText;
        public Image PowerChip;
        public Text PowerText;
        public Image CounterChip;
        public Text CounterText;
        public Text StateText;
        public Image SlotChip;
        public Text SlotChipText;
        public Image Glow;
        public Image HoverGlow;
    }
    private Sprite[] inspectorIdleFrames;
    private float inspectorIdleFps;
    private float inspectorIdleTimer;
    private int inspectorIdleFrame;
    private string inspectorIdleKey;
    private static Sprite cachedPanelButtonNormalSprite;
    private static Sprite cachedPanelButtonHighlightedSprite;
    private static Sprite cachedPanelButtonPressedSprite;
    private static Sprite cachedPanelButtonHoverGlowSprite;
    private static Sprite cachedPayloadInspectorPanelSprite;
    private static Sprite cachedSquadPanelBackgroundSprite;
    private static Sprite cachedMonsterDisplayPanelSprite;
    private static Sprite cachedInspectorExpBarFillSprite;
    private static Sprite cachedInspectorExpBarUnderSprite;
    private static Sprite cachedTerminalToggleOnSprite;
    private static Sprite cachedTerminalToggleOffSprite;
    private static Sprite cachedSliderTrackSprite;
    private static Sprite cachedSliderFillSprite;
    private static Sprite cachedSliderHandleSprite;
    private static Sprite cachedFilledStarSprite;
    private static Sprite cachedHollowStarSprite;
    private static Sprite cachedSkillCardFrameSprite;
    private static Sprite cachedSkillSelectFrameSprite;
    private static Sprite cachedSkillSwapPanelSprite;
    private static Sprite cachedSkillChipFrameSprite;
    private readonly Sprite[] skillSwapElementIcons = new Sprite[System.Enum.GetValues(typeof(ElementType)).Length];
    private readonly Sprite[] skillSwapInstructionIcons = new Sprite[System.Enum.GetValues(typeof(InstructionType)).Length];
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
    private TextMeshProUGUI[] sourceLayoutBossRouteBitmapLabels;
    private TextMeshProUGUI[] sourceLayoutBossRouteBitmapCodes;
    private TextMeshProUGUI[] sourceLayoutBossRouteBitmapElementTags;
    private TextMeshProUGUI[] sourceLayoutBossRouteBitmapStatuses;
    private Transform[] sourceLayoutBossRouteSelectedRails;
    private string sourceLayoutBossRouteLastSelectedCode;
    private float sourceLayoutBossRouteSelectionFlashStartTime = -999f;
    private static Sprite bossRouteSelectionBarSprite;
    private static Sprite bossRouteSelectionPanelSprite;
    private static Font bossRouteDefaultFont;
    private static TMP_FontAsset tmpMirrorFontAsset;

    private void Awake()
    {
        defaultFont = ResolveTerminalDefaultFont();
        manager = GameManager.EnsureInstance();
        terminalZoomModeEnabled = PlayerPrefs.GetInt(TerminalZoomPlayerPrefsKey, 0) == 1;
        EnsureThreatTierAccess(manager);
        EnsureStarterParty(manager, fallbackStarter);
        ConfigureCrispCanvas();
        EnsureHudWidgets();
        ApplyTerminalZoomMode();
        HideLegacySceneButtonVisuals();
        NormalizeMainTerminalFonts();
        RefreshRunOverview();
    }

    private void OnEnable()
    {
        WireButton(enterGridButton, StartRun);
        WireButton(geneLabButton, ShowGeneLab);
        WireButton(payloadButton, ShowPayloadBox);
        WireButton(systemLogButton, ShowSystemLogPlaceholder);
        WireButton(settingsButton, ShowSettingsPanel);
        WireButton(exitButton, ShowExitPanel);
        WireButton(sourceLayoutEnterGridButton, StartRun);
        WireButton(sourceLayoutGeneLabButton, ShowGeneLab);
        WireButton(sourceLayoutPayloadButton, ShowPayloadBox);
        WireButton(sourceLayoutSettingsButton, ShowSettingsPanel);
        WireButton(sourceLayoutExitButton, ShowExitPanel);
        WireButton(launchProtocolButton, StartRun);
        WireButton(payloadPreviousButton, SelectPreviousPayload);
        WireButton(payloadNextButton, SelectNextPayload);
        WireButton(geneLabFuseButton, FuseSelectedPayload);
        WireButton(geneLabEvolveButton, EvolveSelectedPayload);
        WireButton(sectionBackButton, ExitSectionView);
        WireDepthTierButtons();
        WireBossRouteButtons();
    }

    private void OnDisable()
    {
        UnwireButton(enterGridButton, StartRun);
        UnwireButton(geneLabButton, ShowGeneLab);
        UnwireButton(payloadButton, ShowPayloadBox);
        UnwireButton(systemLogButton, ShowSystemLogPlaceholder);
        UnwireButton(settingsButton, ShowSettingsPanel);
        UnwireButton(exitButton, ShowExitPanel);
        UnwireButton(sourceLayoutEnterGridButton, StartRun);
        UnwireButton(sourceLayoutGeneLabButton, ShowGeneLab);
        UnwireButton(sourceLayoutPayloadButton, ShowPayloadBox);
        UnwireButton(sourceLayoutSettingsButton, ShowSettingsPanel);
        UnwireButton(sourceLayoutExitButton, ShowExitPanel);
        UnwireButton(launchProtocolButton, StartRun);
        UnwireButton(payloadPreviousButton, SelectPreviousPayload);
        UnwireButton(payloadNextButton, SelectNextPayload);
        UnwireButton(geneLabFuseButton, FuseSelectedPayload);
        UnwireButton(geneLabEvolveButton, EvolveSelectedPayload);
        UnwireButton(sectionBackButton, ExitSectionView);
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

        TickInspectorIdle();
        TickGeneLabFusionIdle();
        RefreshRunOverview();
    }

    private void StartRun()
    {
        if (GridLinkTransition.IsActive)
            return;

        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        EnsureStarterParty(manager, fallbackStarter);
        RefreshRunOverview();
        SetModule("ENTER_GRID", "LINK STATE:", "DIGITAL HANDSHAKE", "Route graph handshake active. Loading grid node map...");
        // Silence the menu music so the grid-link impact lands clean; the grid
        // track fades in on scene load.
        AudioManager.Instance?.FadeOutMusic();
        AudioManager.Instance?.PlayUiSfx(UiSfx.Impact);
        GridLinkTransition.Play(
            () =>
            {
                manager = manager != null ? manager : GameManager.EnsureInstance();
                if (manager == null)
                    return;

                EnsureThreatTierAccess(manager);
                EnsureStarterParty(manager, fallbackStarter);
                manager.BeginRun();
            },
            () => GameManager.GoTo(GameScene.TheGrid));
    }

    private void SelectDepthTier(int tier)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        if (manager.IsRunActive)
        {
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            SetModule("ENTER_GRID", "RUN ACTIVE:", "DEPTH TIER LOCKED", BuildDepthTierDetail(manager));
            RefreshRunOverview();
            return;
        }

        if (manager.TrySetSelectedThreatTier(tier))
        {
            SetModule("ENTER_GRID", "DEPTH TIER:", $"DEPTH {tier}F ROUTE SELECTED", BuildDepthTierDetail(manager));
        }
        else
        {
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            SetModule("ENTER_GRID", "LOCKED:", $"DEPTH {tier}F UNAVAILABLE", BuildDepthTierDetail(manager));
        }

        RefreshRunOverview();
    }

    private void SelectBossRoute(string speciesCodeName)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        if (manager.IsRunActive)
        {
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            SetModule("BOSS_TARGET", "RUN ACTIVE:", "BOSS TARGET LOCKED", BuildBossTargetDetail(manager));
            RefreshRunOverview();
            return;
        }

        if (manager.TrySetSelectedBossSpecies(speciesCodeName))
        {
            string selected = manager.SelectedBossSpeciesCodeName.ToUpperInvariant();
            SetModule("BOSS_TARGET", "TARGET:", $"{selected} PRIME CONFIRMED", BuildBossTargetDetail(manager));
        }
        else
        {
            // Previously failed with no feedback at all (silent UX bug).
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            SetModule("BOSS_TARGET", "REJECTED:", "UNKNOWN BOSS SPECIES", BuildBossTargetDetail(manager));
        }

        RefreshRunOverview();
    }

    private void ShowGeneLab()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (string.IsNullOrWhiteSpace(selectedGeneLabSpeciesCode))
            selectedGeneLabSpeciesCode = NormalizeBossRouteCode(manager != null ? manager.SelectedBossSpeciesCodeName : BossRouteSpecies[0]);
        geneLabRouteSelectionMode = true;
        showingGeneLabPanel = true;
        geneLabActionMessage = "Select a boss gene pool.";
        EnterSectionView("GENE LAB");
        HidePayloadPanel();
        ShowPayloadGrid(false);
        ShowGeneLabPanel(true);
        ShowExitPanelRoot(false);
        ShowSettingsPanelRoot(false);
        RefreshGeneLabModule();
    }

    private void ShowPayloadBox()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        int payloadCount = manager != null && manager.payload != null ? manager.payload.Count : 0;
        if (payloadCount > 0)
            selectedPayloadIndex = Mathf.Clamp(selectedPayloadIndex < 0 ? payloadCount - 1 : selectedPayloadIndex, 0, payloadCount - 1);

        showingGeneLabPanel = false;
        geneLabActionMessage = string.Empty;
        geneLabSkillMessage = string.Empty;
        geneLabFusionSecondIndex = -1;
        payloadSkillMessage = string.Empty;
        pendingPayloadSkillReplacement = null;
        EnterSectionView("PAYLOAD");
        SetModule(
            "PAYLOAD_BOX",
            "PAYLOAD:",
            $"{payloadCount} BASE FORM RECORD(S) STORED.",
            BuildPayloadPreview(manager));
        HidePayloadPanel();
        ShowGeneLabPanel(false);
        ShowExitPanelRoot(false);
        ShowSettingsPanelRoot(false);
        CloseSquadPanel();
        ShowPayloadGrid(true);
        RenderPayloadGrid(manager);
    }

    private void EnterSectionView(string title)
    {
        inSectionView = true;

        if (menuContentGroups != null)
        {
            for (int i = 0; i < menuContentGroups.Length; i++)
            {
                if (menuContentGroups[i] != null)
                    menuContentGroups[i].gameObject.SetActive(false);
            }
        }

        if (sectionTitleText != null)
            sectionTitleText.text = title;

        if (sectionViewRoot != null)
            sectionViewRoot.gameObject.SetActive(true);
    }

    private void ExitSectionView()
    {
        if (showingGeneLabPanel && !geneLabRouteSelectionMode)
        {
            geneLabRouteSelectionMode = true;
            geneLabActionMessage = "Select another boss gene pool.";
            geneLabSkillMessage = string.Empty;
            RefreshGeneLabModule();
            return;
        }

        inSectionView = false;
        showingGeneLabPanel = false;
        geneLabRouteSelectionMode = false;
        geneLabActionMessage = string.Empty;
        geneLabSkillMessage = string.Empty;
        geneLabFusionSecondIndex = -1;

        HidePayloadPanel();
        ShowPayloadGrid(false);
        ShowGeneLabPanel(false);
        ShowExitPanelRoot(false);
        ShowSettingsPanelRoot(false);
        CloseSquadPanel();

        if (sectionViewRoot != null)
            sectionViewRoot.gameObject.SetActive(false);

        if (menuContentGroups != null)
        {
            for (int i = 0; i < menuContentGroups.Length; i++)
            {
                if (menuContentGroups[i] != null)
                    menuContentGroups[i].gameObject.SetActive(true);
            }
        }
    }

    private void RefreshGeneLabModule()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        int payloadCount = manager != null && manager.payload != null ? manager.payload.Count : 0;
        if (selectedPayloadIndex >= payloadCount)
            selectedPayloadIndex = -1;
        if (geneLabFusionSecondIndex >= payloadCount)
            geneLabFusionSecondIndex = -1;

        showingGeneLabPanel = true;
        if (geneLabRouteSelectionMode)
        {
            SetModule(
                "GENE_LAB",
                "GENE LAB:",
                "SELECT BOSS GENE POOL.",
                BuildGeneLabRouteSelectDetail(manager));
        }
        else
        {
            SetModule(
                "GENE_LAB",
                "GENE LAB:",
                "FUSION WORKBENCH ONLINE.",
                BuildGeneLabModuleDetail(manager));
        }
        RenderGeneLabPanel(manager);
    }

    private void SelectPreviousPayload()
    {
        MovePayloadSelection(-1);
    }

    private void SelectNextPayload()
    {
        MovePayloadSelection(1);
    }

    private void MovePayloadSelection(int direction)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        int payloadCount = manager != null && manager.payload != null ? manager.payload.Count : 0;
        if (payloadCount <= 0)
            return;

        selectedPayloadIndex = (selectedPayloadIndex + direction + payloadCount) % payloadCount;
        if (showingGeneLabPanel)
        {
            geneLabActionMessage = GeneLabSelectionStatus(manager);
            RefreshGeneLabModule();
        }
        else
        {
            ShowPayloadBox();
        }
    }

    private void SelectGeneLabSpecies(string speciesCodeName)
    {
        SelectGeneLabSpecies(speciesCodeName, true);
    }

    private void SelectGeneLabSpecies(string speciesCodeName, bool refresh)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        selectedGeneLabSpeciesCode = NormalizeBossRouteCode(speciesCodeName);
        geneLabRouteSelectionMode = false;
        geneLabSkillMessage = string.Empty;

        if (manager != null && !manager.IsRunActive)
            manager.TrySetSelectedBossSpecies(selectedGeneLabSpeciesCode);

        selectedPayloadIndex = -1;
        geneLabFusionSecondIndex = -1;
        FocusGeneLabMiniPayloadPageOn(manager, selectedGeneLabSpeciesCode, selectedPayloadIndex);
        geneLabActionMessage = GeneLabSelectionStatus(manager);
        if (refresh)
            RefreshGeneLabModule();
    }

    private void MoveGeneLabTarget(int direction)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null || manager.payload == null || manager.payload.Count == 0)
            return;

        string speciesCode = SelectedGeneLabSpeciesCode(manager);
        int current = selectedPayloadIndex >= 0 ? selectedPayloadIndex : BestGeneLabTargetIndexForSpecies(manager, speciesCode);
        int count = manager.payload.Count;
        for (int step = 1; step <= count; step++)
        {
            int index = (current + direction * step + count) % count;
            if (PayloadMatchesSpecies(PayloadAt(manager, index), speciesCode))
            {
                selectedPayloadIndex = index;
                if (geneLabFusionSecondIndex == selectedPayloadIndex)
                    geneLabFusionSecondIndex = -1;
                FocusGeneLabMiniPayloadPageOn(manager, speciesCode, selectedPayloadIndex);
                geneLabSkillMessage = string.Empty;
                geneLabActionMessage = GeneLabSelectionStatus(manager);
                RefreshGeneLabModule();
                return;
            }
        }
    }

    private void FuseSelectedPayload()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        AlgoMonInstance selected = SelectedPayloadMon(manager);
        if (!manager.CanFusePayload(selectedPayloadIndex, geneLabFusionSecondIndex, out string message))
        {
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
            geneLabActionMessage = message;
            RefreshGeneLabModule();
            return;
        }

        if (manager.TryFusePayload(selectedPayloadIndex, geneLabFusionSecondIndex, out message))
        {
            if (selected != null && manager.payload != null)
                selectedPayloadIndex = Mathf.Max(0, manager.payload.IndexOf(selected));
            geneLabFusionSecondIndex = -1;
        }

        geneLabActionMessage = message;
        geneLabSkillMessage = string.Empty;
        RefreshGeneLabModule();
    }

    private void EvolveSelectedPayload()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        if (!manager.TryEvolvePayload(selectedPayloadIndex, out string message))
            AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
        geneLabActionMessage = message;
        geneLabSkillMessage = string.Empty;
        geneLabFusionSecondIndex = -1;
        RefreshGeneLabModule();
    }

    private void ShowSystemLogPlaceholder()
    {
        SetModule("SYSTEM_LOG", "LOG:", "GRID MODULE READY.", "Start Run will initialize a fresh route graph.");
    }

    private void ShowSettingsPanel()
    {
        showingGeneLabPanel = false;
        geneLabActionMessage = string.Empty;
        geneLabSkillMessage = string.Empty;
        geneLabFusionSecondIndex = -1;
        payloadSkillMessage = string.Empty;
        pendingPayloadSkillReplacement = null;
        EnterSectionView("SETTINGS");
        HidePayloadPanel();
        ShowPayloadGrid(false);
        ShowGeneLabPanel(false);
        ShowExitPanelRoot(false);
        ShowSettingsPanelRoot(true);
        CloseSquadPanel();
        SetModule(
            "SETTINGS",
            "DISPLAY:",
            terminalZoomModeEnabled ? "TERMINAL ZOOM ENABLED." : "TERMINAL ZOOM DISABLED.",
            "Toggle terminal zoom when you want a closer UI pass.");
        RenderSettingsPanel();
    }

    private void ShowExitPanel()
    {
        showingGeneLabPanel = false;
        geneLabActionMessage = string.Empty;
        geneLabSkillMessage = string.Empty;
        geneLabFusionSecondIndex = -1;
        EnterSectionView("EXIT");
        HidePayloadPanel();
        ShowPayloadGrid(false);
        ShowGeneLabPanel(false);
        ShowSettingsPanelRoot(false);
        ShowExitPanelRoot(true);
        SetModule("EXIT_SYSTEM", "SESSION:", "EXIT PROTOCOL READY.", "Return to the terminal or close the current build session.");
        RenderExitPanel();
    }

    private void ReturnFromExitPanel()
    {
        ExitSectionView();
        SetModule("ENTER_GRID", "DEPTH TIER:", "GRID LINK READY", BuildDepthTierDetail(manager));
    }

    private void ConfirmExit()
    {
        if (exitPanelStatusText != null)
            exitPanelStatusText.text = "EXIT SIGNAL SENT.";

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetModule(string moduleId, string warning, string headline, string detail)
    {
        if (moduleText != null)
            moduleText.text = $"MODULE_ID: {moduleId}";
        if (warningText != null)
            warningText.text = warning;
        if (detailText != null)
            detailText.text = $"{headline}\n\n{detail}";
        if (moduleId != "PAYLOAD_BOX" && moduleId != "GENE_LAB" && moduleId != "EXIT_SYSTEM")
        {
            showingGeneLabPanel = false;
            HidePayloadPanel();
            ShowGeneLabPanel(false);
            ShowExitPanelRoot(false);
        }
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
            int evolvableCount = manager.EvolvablePayloadCount();
            string runStatus = manager.IsRunActive ? "ACTV" : "STBY";
            statsText.text =
                $"RUN// {runStatus} DEPTH {manager.SelectedThreatTierNumber:00}F\n" +
                $"CREDITS// {manager.computeBalance:0000}\n" +
                $"PAYLOAD// {payloadCount:00}\n" +
                $"GENE// {evolvableCount:00}\n" +
                $"BOSS// {manager.SelectedBossSpeciesCodeName.ToUpperInvariant()}\n" +
                $"SQUAD// {PartyCount(manager):00}/{GameManager.MaxPartySize:00}";
        }

        if (!inSectionView)
        {
            RefreshDepthTierSelector();
            RefreshSourceLayoutBossRoutes();
            RefreshLaunchProtocolText();
        }

        if (inSectionView && !showingGeneLabPanel && payloadGridRoot != null && payloadGridRoot.gameObject.activeSelf)
        {
            RenderPayloadGrid(manager);
        }
        else if (inSectionView && showingGeneLabPanel && geneLabPanelRoot != null && geneLabPanelRoot.gameObject.activeSelf)
        {
            RenderGeneLabPanel(manager);
        }
        else if (payloadPanel != null && payloadPanel.gameObject.activeSelf)
        {
            if (showingGeneLabPanel)
                RenderGeneLabPanel(manager);
            else
                RenderPayloadPanel(manager);
        }
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
        ThreatTier tier = targetManager.SelectedThreatTier;

        return $"DEPTH {selected}F\nROUTE {GridGenerationSettings.TotalLayerRangeLabel(selected)} LAYERS\nENEMY LV {ThreatTierRules.MinLevel(tier):00}-{ThreatTierRules.MaxLevel(tier):00}";
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
            return "No base-form AlgoMon records yet.\nClear a selected boss route to archive that species here.";

        const int maxVisible = 8;
        var builder = new StringBuilder("BASE FORM PAYLOAD CACHE");
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
        string form = FormLabel(mon);
        return $"{slot:00}// {name.ToUpperInvariant()} {form} L{mon.level:00} [{ShortElement(element)}] BAT{mon.Battery:00} CPU{mon.ComputingPower:00} TP{mon.Throughput:00}";
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
                payloadDetailPanelText.text = "Clear a selected boss route to archive that boss species' base form here.";
            SetPayloadPanelControls(false, false, false, false);
            return;
        }

        selectedPayloadIndex = Mathf.Clamp(selectedPayloadIndex, 0, targetManager.payload.Count - 1);
        AlgoMonInstance selected = targetManager.payload[selectedPayloadIndex];
        SetPayloadPortrait(ResolvePayloadSprite(selected), DisplayNameFor(selected).ToUpperInvariant());

        if (payloadListText != null)
            payloadListText.text = BuildPayloadList(targetManager, selectedPayloadIndex);
        if (payloadDetailPanelText != null)
            payloadDetailPanelText.text = BuildPayloadDetail(selected);
        SetPayloadPanelControls(targetManager.payload.Count > 1, targetManager.payload.Count > 1, false, false);
    }

    private void RenderGeneLabPanel(GameManager targetManager)
    {
        if (geneLabPanelRoot != null)
        {
            geneLabPanelRoot.gameObject.SetActive(true);
            RenderGeneLabDashboard(targetManager);
            return;
        }

        if (payloadPanel == null)
            return;

        payloadPanel.gameObject.SetActive(true);
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
        {
            selectedPayloadIndex = -1;
            SetPayloadPortrait(null, "GENE LAB");
            if (payloadListText != null)
                payloadListText.text = "GENE LAB INDEX\n-- EMPTY --";
            if (payloadDetailPanelText != null)
                payloadDetailPanelText.text = "No base forms available.\nClear a selected boss route to add a base unit.";
            SetPayloadPanelControls(false, false, false, false);
            return;
        }

        selectedPayloadIndex = Mathf.Clamp(selectedPayloadIndex, 0, targetManager.payload.Count - 1);
        AlgoMonInstance selected = targetManager.payload[selectedPayloadIndex];
        SetPayloadPortrait(ResolvePayloadSprite(selected), DisplayNameFor(selected).ToUpperInvariant());

        bool canFuse = targetManager.CanFusePayload(selectedPayloadIndex, geneLabFusionSecondIndex, out _);
        bool canEvolve = targetManager.CanEvolvePayload(selectedPayloadIndex, out _);

        if (payloadListText != null)
            payloadListText.text = BuildPayloadList(targetManager, selectedPayloadIndex);
        if (payloadDetailPanelText != null)
            payloadDetailPanelText.text = BuildGeneLabDetail(targetManager, selectedPayloadIndex, geneLabFusionSecondIndex, geneLabActionMessage);

        SetPayloadPanelControls(
            targetManager.payload.Count > 1,
            targetManager.payload.Count > 1,
            canFuse,
            canEvolve);
    }

    private void RenderGeneLabDashboard(GameManager targetManager)
    {
        string speciesCode = SelectedGeneLabSpeciesCode(targetManager);
        if (geneLabRouteSelectionMode)
        {
            ShowGeneLabRouteSelection(true);
            ShowGeneLabBench(false);
            RefreshGeneLabSpeciesButtons(targetManager, speciesCode);
            if (geneLabRoutePromptText != null)
                geneLabRoutePromptText.text = BuildGeneLabRouteSelectDetail(targetManager);
            return;
        }

        ShowGeneLabRouteSelection(false);
        ShowGeneLabBench(true);

        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
        {
            selectedPayloadIndex = -1;
            geneLabFusionSecondIndex = -1;
            RefreshGeneLabMiniPayload(targetManager, speciesCode);
            if (geneLabFusionText != null)
                geneLabFusionText.text = "SELECT UNIT 1 // CLEAR THIS BOSS ROUTE TO ARCHIVE BASE UNIT";
            RenderGeneLabFusionDisplays(null, -1, null, -1);
            RenderGeneLabFusionTalentBars(null, null);
            SetPanelButtonState(geneLabPreviousRecordButton, true, false);
            SetPanelButtonState(geneLabNextRecordButton, true, false);
            SetPanelButtonState(geneLabFuseActionButton, true, false);
            SetPanelButtonState(geneLabEvolveActionButton, true, false);
            return;
        }

        if (!PayloadMatchesSpecies(PayloadAt(targetManager, selectedPayloadIndex), speciesCode))
            selectedPayloadIndex = -1;
        if (!PayloadMatchesSpecies(PayloadAt(targetManager, geneLabFusionSecondIndex), speciesCode) ||
            geneLabFusionSecondIndex == selectedPayloadIndex)
        {
            geneLabFusionSecondIndex = -1;
        }
        RefreshGeneLabMiniPayload(targetManager, speciesCode);

        AlgoMonInstance selected = PayloadAt(targetManager, selectedPayloadIndex);
        AlgoMonInstance material = PayloadAt(targetManager, geneLabFusionSecondIndex);
        bool canFuse = targetManager.CanFusePayload(selectedPayloadIndex, geneLabFusionSecondIndex, out string fuseBlockReason);
        bool canEvolve = selected != null && targetManager.CanEvolvePayload(selectedPayloadIndex, out _);
        bool canCycle = CountPayloadForSpecies(targetManager, speciesCode) > 1;
        RenderGeneLabFusionDisplays(selected, selectedPayloadIndex, material, geneLabFusionSecondIndex);
        RenderGeneLabFusionTalentBars(selected, material);

        if (geneLabFusionText != null)
            geneLabFusionText.text = selected != null
                ? BuildGeneLabFusionStatus(selected, material, canFuse, canEvolve, fuseBlockReason, geneLabActionMessage)
                : "SELECT UNIT 1 FROM THE LEFT PAYLOAD POOL";

        SetPanelButtonState(geneLabPreviousRecordButton, true, canCycle);
        SetPanelButtonState(geneLabNextRecordButton, true, canCycle);
        SetPanelButtonState(geneLabFuseActionButton, true, canFuse);
        SetPanelButtonState(geneLabEvolveActionButton, true, canEvolve);
    }

    private void HidePayloadPanel()
    {
        if (payloadPanel != null)
            payloadPanel.gameObject.SetActive(false);
    }

    private void BuildPayloadDisplayOrder(GameManager targetManager)
    {
        payloadDisplayOrder.Clear();
        if (targetManager == null || targetManager.payload == null)
            return;

        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            if (mon != null && targetManager.IsInParty(mon))
                payloadDisplayOrder.Add(mon);
        }
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            if (mon != null && !targetManager.IsInParty(mon) && mon.isFavorite)
                payloadDisplayOrder.Add(mon);
        }
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            if (mon != null && !targetManager.IsInParty(mon) && !mon.isFavorite)
                payloadDisplayOrder.Add(mon);
        }
    }

    private Sprite ResolvePayloadSlotSprite(GameManager targetManager, AlgoMonInstance mon, bool selected)
    {
        if (selected && slotSelectedSprite != null)
            return slotSelectedSprite;
        if (mon != null && targetManager != null && targetManager.IsInParty(mon) && slotLegendarySprite != null)
            return slotLegendarySprite;
        if (mon != null && mon.IsEvolvedForm && slotEpicSprite != null)
            return slotEpicSprite;
        return slotNormalSprite;
    }

    private void RenderPayloadGrid(GameManager targetManager)
    {
        if (payloadCellFrames == null)
            return;

        BuildPayloadDisplayOrder(targetManager);

        AlgoMonInstance selectedMon = SelectedPayloadMon(targetManager);

        int cellsPerPage = payloadCellFrames.Length;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(payloadDisplayOrder.Count / (float)cellsPerPage));
        payloadPage = Mathf.Clamp(payloadPage, 0, pageCount - 1);
        int pageStart = payloadPage * cellsPerPage;

        for (int cell = 0; cell < payloadCellFrames.Length; cell++)
        {
            int orderIndex = pageStart + cell;
            AlgoMonInstance mon = orderIndex < payloadDisplayOrder.Count ? payloadDisplayOrder[orderIndex] : null;
            int payloadIndex = mon != null && targetManager != null && targetManager.payload != null
                ? targetManager.payload.IndexOf(mon)
                : -1;
            payloadCellPayloadIndices[cell] = payloadIndex;

            bool isSelected = mon != null && ReferenceEquals(mon, selectedMon);
            bool isParty = mon != null && targetManager != null && targetManager.IsInParty(mon);
            bool isFavorite = mon != null && mon.isFavorite;
            if (payloadCellFrames[cell] != null)
            {
                payloadCellFrames[cell].rectTransform.localScale = Vector3.one;
                payloadCellFrames[cell].sprite = ResolvePayloadSlotSprite(targetManager, mon, isSelected);
                payloadCellFrames[cell].color = mon == null
                    ? new Color(1f, 1f, 1f, 0.32f)
                    : isFavorite && !isParty && !isSelected
                        ? new Color(1f, 0.88f, 0.42f, 1f)
                        : Color.white;
            }

            bool showHover = hoveredPayloadCellIndex == cell && payloadIndex >= 0;
            if (payloadCellFrames[cell] != null)
            {
                payloadCellFrames[cell].rectTransform.localScale = showHover
                    ? new Vector3(1.035f, 1.035f, 1f)
                    : Vector3.one;
                if (showHover)
                {
                    payloadCellFrames[cell].color = isFavorite
                        ? new Color(1f, 0.92f, 0.52f, 1f)
                        : new Color(0.76f, 1f, 0.98f, 1f);
                }
            }

            if (payloadCellSprites[cell] != null)
            {
                Sprite monSprite = ResolvePayloadSprite(mon);
                payloadCellSprites[cell].sprite = monSprite;
                payloadCellSprites[cell].enabled = monSprite != null;
            }

            if (payloadCellLabels[cell] != null)
            {
                payloadCellLabels[cell].text = mon != null
                    ? $"{DisplayNameFor(mon).ToUpperInvariant()}\nL{mon.level:00}"
                    : string.Empty;
                payloadCellLabels[cell].color = isFavorite && !isParty
                    ? new Color(1f, 0.88f, 0.42f, 1f)
                    : new Color(0.86f, 1f, 0.96f, 1f);
            }

            if (payloadCellFavoriteMarkers != null && payloadCellFavoriteMarkers[cell] != null)
            {
                payloadCellFavoriteMarkers[cell].gameObject.SetActive(isFavorite);
                payloadCellFavoriteMarkers[cell].color = isParty
                    ? new Color(1f, 0.78f, 0.3f, 1f)
                    : new Color(1f, 0.88f, 0.28f, 1f);
            }

            if (payloadCellButtons[cell] != null)
            {
                payloadCellButtons[cell].transition = Selectable.Transition.None;
                payloadCellButtons[cell].interactable = mon != null;
            }
        }

        if (payloadPageLabel != null)
            payloadPageLabel.text = $"PAGE {payloadPage + 1}/{pageCount}";
        if (payloadPrevPageButton != null)
            payloadPrevPageButton.interactable = payloadPage > 0;
        if (payloadNextPageButton != null)
            payloadNextPageButton.interactable = payloadPage < pageCount - 1;

        RenderPayloadDetailStub(selectedMon, targetManager);
    }

    private static float ExpProgressFor(AlgoMonInstance mon)
    {
        if (mon == null)
            return 0f;
        if (mon.level >= AlgoMonInstance.MAX_LEVEL)
            return 1f;

        int required = Mathf.Max(1, mon.expToNextLevel);
        return Mathf.Clamp01(mon.exp / (float)required);
    }

    private void RenderPayloadDetailStub(AlgoMonInstance mon, GameManager targetManager)
    {
        SetInspectorIdle(mon);

        if (mon == null)
        {
            if (inspectorNameText != null)
                inspectorNameText.text = "SELECT A UNIT";
            UpdateInspectorExp(null);
            if (inspectorRadar != null)
                inspectorRadar.SetValues(new float[StatAxisLabels.Length]);
            if (inspectorTalentFills != null)
            {
                for (int i = 0; i < inspectorTalentFills.Length; i++)
                {
                    if (inspectorTalentFills[i] != null)
                        inspectorTalentFills[i].fillAmount = 0f;
                    if (inspectorTalentValues[i] != null)
                        inspectorTalentValues[i].text = "--";
                }
            }
            UpdateRadarLabelText(null);
            PositionRadarLabels();
            UpdateSquadButton(null, targetManager);
            UpdateFavoriteButton(null);
            UpdateInspectorSkillPanel(null);
            return;
        }

        string form = mon.IsEvolvedForm ? "EVOLVED" : "BASE";
        string party = targetManager != null && targetManager.IsInParty(mon) ? "IN SQUAD" : "STORED";
        if (inspectorNameText != null)
            inspectorNameText.text = $"{DisplayNameFor(mon).ToUpperInvariant()}  L{mon.level:00}\n{form} / {party}";
        UpdateInspectorExp(mon);

        int[] stats = { mon.Battery, mon.ClockSpeed, mon.ComputingPower, mon.Throughput, mon.Firewall, mon.Encryption };
        int[] ivs = { mon.iv_Battery, mon.iv_ClockSpeed, mon.iv_ComputingPower, mon.iv_Throughput, mon.iv_Firewall, mon.iv_Encryption };

        if (inspectorRadar != null)
        {
            float[] norm = new float[StatAxisLabels.Length];
            for (int i = 0; i < norm.Length; i++)
                norm[i] = Mathf.Clamp01(stats[i] / RadarMaxStat);
            inspectorRadar.SetValues(norm);
        }
        UpdateRadarLabelText(stats);
        PositionRadarLabels();

        if (inspectorTalentFills != null)
        {
            for (int i = 0; i < inspectorTalentFills.Length; i++)
            {
                if (inspectorTalentFills[i] != null)
                    inspectorTalentFills[i].fillAmount = Mathf.Clamp01(ivs[i] / 255f);
                if (inspectorTalentValues[i] != null)
                    inspectorTalentValues[i].text = ivs[i].ToString();
            }
        }

        UpdateSquadButton(mon, targetManager);
        UpdateFavoriteButton(mon);
        UpdateInspectorSkillPanel(mon);
    }

    private void UpdateInspectorExp(AlgoMonInstance mon)
    {
        if (inspectorExpFill != null)
            inspectorExpFill.fillAmount = ExpProgressFor(mon);

        if (inspectorExpText == null)
            return;

        if (mon == null)
        {
            inspectorExpText.text = "EXP --/--";
            inspectorExpText.color = new Color(0.50f, 0.74f, 0.76f, 0.72f);
            return;
        }

        if (mon.level >= AlgoMonInstance.MAX_LEVEL)
        {
            inspectorExpText.text = "EXP MAX";
            inspectorExpText.color = new Color(0.84f, 1f, 0.76f, 1f);
            return;
        }

        inspectorExpText.text = $"EXP {mon.exp}/{Mathf.Max(1, mon.expToNextLevel)}";
        inspectorExpText.color = new Color(0.72f, 1f, 0.92f, 1f);
    }

    // Keeps the SKILLS button enabled only for a real unit, and live-refreshes the
    // popup if it is already open when the inspector re-renders (selection change).
    private void UpdateInspectorSkillPanel(AlgoMonInstance mon)
    {
        if (inspectorSkillsButton != null)
            inspectorSkillsButton.interactable = mon != null;
        if (inspectorSkillsButtonLabel != null)
            inspectorSkillsButtonLabel.text = "SKILLS";

        if (mon == null)
        {
            pendingPayloadSkillReplacement = null;
            skillSwapSelectedSlot = -1;
            if (skillSwapPanelRoot != null && skillSwapPanelRoot.gameObject.activeSelf)
                CloseSkillSwapPanel();
            return;
        }

        if (skillSwapPanelRoot != null && skillSwapPanelRoot.gameObject.activeSelf)
            RenderSkillSwapPanel();
    }

    private void OpenSkillSwapPanel()
    {
        if (skillSwapPanelRoot == null)
            return;
        if (!EnsureSkillSwapPanelBuilt())
            return;
        manager = manager != null ? manager : GameManager.EnsureInstance();
        pendingPayloadSkillReplacement = null;
        skillSwapSelectedSlot = -1;
        payloadSkillMessage = string.Empty;
        skillSwapPanelRoot.gameObject.SetActive(true);
        skillSwapPanelRoot.SetAsLastSibling();
        RenderSkillSwapPanel();

        // Prime the data board with the first loaded skill until something is hovered.
        AlgoMonInstance mon = SelectedPayloadMon(manager);
        SkillData first = mon != null && mon.knownSkills != null && mon.knownSkills.Count > 0 ? mon.knownSkills[0] : null;
        ShowSkillSwapDetail(first, first != null ? UnlockLevelFor(mon, first) : 0);
    }

    private void CloseSkillSwapPanel()
    {
        pendingPayloadSkillReplacement = null;
        skillSwapSelectedSlot = -1;
        payloadSkillMessage = string.Empty;
        if (skillSwapPanelRoot != null)
            skillSwapPanelRoot.gameObject.SetActive(false);
    }

    private bool EnsureSkillSwapPanelBuilt()
    {
        if (skillSwapPanelRoot == null)
            return false;
        if (skillSwapLoadoutCards != null && skillSwapLearnCards != null && skillSwapLearnEntries != null)
            return true;

        Transform parent = skillSwapPanelRoot.parent;
        if (Application.isPlaying)
            Destroy(skillSwapPanelRoot.gameObject);
        else
            DestroyImmediate(skillSwapPanelRoot.gameObject);

        skillSwapPanelRoot = null;
        if (parent == null)
            return false;

        EnsureSkillSwapPanel(parent);
        return skillSwapPanelRoot != null && skillSwapLoadoutCards != null && skillSwapLearnCards != null && skillSwapLearnEntries != null;
    }

    private void EnsureSkillSwapPanel(Transform parent)
    {
        skillSwapPanelRoot = CreateRect("SkillSwapPanel", parent);
        SetAnchors(skillSwapPanelRoot, Vector2.zero, Vector2.one);

        Image backdrop = skillSwapPanelRoot.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.72f);
        backdrop.raycastTarget = true;
        Button backdropButton = skillSwapPanelRoot.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(CloseSkillSwapPanel);

        // Solid procedural frame: its visible border IS the rect, so the header,
        // cards and detail panel all sit unambiguously inside the box.
        RectTransform box = CreateRect("SkillSwapBox", skillSwapPanelRoot);
        SetAnchors(box, new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.93f));
        Image boxBg = box.gameObject.AddComponent<Image>();
        boxBg.sprite = SkillSwapPanelSprite();
        boxBg.type = Image.Type.Sliced;
        boxBg.pixelsPerUnitMultiplier = 0.55f;
        boxBg.color = Color.white;
        boxBg.raycastTarget = true;

        skillSwapTitle = CreateText("SkillSwapTitle", box, 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.7f, 0.98f, 1f, 1f));
        ApplyCrispCyberText(skillSwapTitle, new Color(0f, 0.14f, 0.2f, 1f));
        SetAnchors(skillSwapTitle.rectTransform, new Vector2(0.035f, 0.908f), new Vector2(0.700f, 0.974f));
        skillSwapTitle.text = "SKILL LOADOUT";

        Button closeButton = FindOrCreatePanelButton("SkillSwapCloseButton", box, "CLOSE", new Vector2(0.836f, 0.906f), new Vector2(0.964f, 0.972f));
        SetPanelButtonLabelSize(closeButton, 15);
        closeButton.onClick.AddListener(CloseSkillSwapPanel);

        skillSwapMessageText = CreateText("SkillSwapMessage", box, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.84f, 1f, 0.96f, 1f));
        ApplyCrispCyberText(skillSwapMessageText, new Color(0f, 0.12f, 0.18f, 0.9f));
        SetAnchors(skillSwapMessageText.rectTransform, new Vector2(0.035f, 0.848f), new Vector2(0.964f, 0.904f));

        Text loadoutLabel = CreateText("SkillSwapLoadoutLabel", box, 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.6f, 0.92f, 1f, 0.92f));
        ApplyCrispCyberText(loadoutLabel, new Color(0f, 0.1f, 0.16f, 0.9f));
        SetAnchors(loadoutLabel.rectTransform, new Vector2(0.035f, 0.798f), new Vector2(0.964f, 0.846f));
        loadoutLabel.text = "ACTIVE LOADOUT";

        skillSwapLoadoutCards = new SkillCardRefs[AlgoMonInstance.MaxSkillSlots];
        int slots = AlgoMonInstance.MaxSkillSlots;
        const float loadoutLeft = 0.035f;
        const float loadoutRight = 0.964f;
        const float loadoutGap = 0.011f;
        float loadoutSpan = (loadoutRight - loadoutLeft) / slots;
        for (int i = 0; i < slots; i++)
        {
            float x0 = loadoutLeft + i * loadoutSpan + loadoutGap * 0.5f;
            float x1 = loadoutLeft + (i + 1) * loadoutSpan - loadoutGap * 0.5f;
            int captured = i;
            SkillCardRefs card = BuildSkillCard("SkillSwapLoadout_" + i, box, new Vector2(x0, 0.624f), new Vector2(x1, 0.794f), true, captured);
            card.Button.onClick.AddListener(() => OnSkillSwapLoadoutClicked(captured));
            skillSwapLoadoutCards[i] = card;
        }

        Text learnLabel = CreateText("SkillSwapLearnLabel", box, 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.6f, 0.92f, 1f, 0.92f));
        ApplyCrispCyberText(learnLabel, new Color(0f, 0.1f, 0.16f, 0.9f));
        SetAnchors(learnLabel.rectTransform, new Vector2(0.035f, 0.570f), new Vector2(0.575f, 0.618f));
        learnLabel.text = "LEARNABLE SKILLS";

        // Left column: learnable list. Right column: skill data panel fed by hover.
        skillSwapLearnCards = new SkillCardRefs[SkillSwapLearnCount];
        skillSwapLearnEntries = new LearnsetEntry[SkillSwapLearnCount];
        const float learnTop = 0.560f;
        const float learnBottom = 0.035f;
        float rowSpan = (learnTop - learnBottom) / SkillSwapLearnCount;
        for (int i = 0; i < SkillSwapLearnCount; i++)
        {
            float yMax = learnTop - i * rowSpan;
            float yMin = learnTop - (i + 1) * rowSpan;
            int captured = i;
            SkillCardRefs card = BuildSkillCard("SkillSwapLearn_" + i, box, new Vector2(0.035f, yMin + 0.005f), new Vector2(0.575f, yMax - 0.005f), false, captured);
            card.Button.onClick.AddListener(() => OnSkillSwapLearnClicked(captured));
            skillSwapLearnCards[i] = card;
        }

        BuildSkillSwapDetailPanel(box);

        skillSwapPanelRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Battle-style skill card. compact = loadout tile (slot chip, centred name);
    /// otherwise a wide learnable row (state badge right). Hover drives the same
    /// scale/glow feedback as battle skill slots and feeds the detail panel.
    /// </summary>
    private SkillCardRefs BuildSkillCard(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, bool compact, int hoverIndex)
    {
        RectTransform root = CreateRect(objectName, parent);
        SetAnchors(root, anchorMin, anchorMax);

        Image frame = root.gameObject.AddComponent<Image>();
        frame.sprite = SkillCardFrameSprite();
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.25f;
        frame.color = Color.white;
        frame.raycastTarget = true;

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;

        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();

        // Gold selection ring (armed slot / staged replace) — separate from hover.
        Image glow = CreateImage("Glow", root, new Color(1f, 0.86f, 0.36f, 0f));
        glow.sprite = SkillSwapSelectFrameSprite();
        glow.type = glow.sprite != null && glow.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        glow.raycastTarget = false;
        SetAnchors(glow.rectTransform, new Vector2(-0.020f, -0.045f), new Vector2(1.020f, 1.045f));
        glow.gameObject.SetActive(false);

        Image badge = CreateImage("InstrBadge", root, Color.white);
        badge.sprite = SkillCardFrameSprite();
        badge.type = Image.Type.Sliced;
        badge.pixelsPerUnitMultiplier = 2.4f;
        badge.raycastTarget = false;

        Image instrIcon = CreateImage("InstrIcon", badge.transform, Color.white);
        instrIcon.preserveAspect = true;
        instrIcon.raycastTarget = false;
        SetAnchors(instrIcon.rectTransform, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f));

        Text instrLetter = CreateText("InstrLetter", badge.transform, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        ApplyCyberText(instrLetter, new Color(0f, 0f, 0f, 0.75f), new Vector2(1f, -1f));
        SetAnchors(instrLetter.rectTransform, Vector2.zero, Vector2.one);

        Text nameText = CreateText("Name", root, compact ? 15 : 17, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.9f, 1f, 0.98f, 1f));
        ApplyCrispCyberText(nameText, new Color(0f, 0.12f, 0.18f, 0.95f));
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.resizeTextForBestFit = true;
        nameText.resizeTextMinSize = 8;
        nameText.resizeTextMaxSize = compact ? 15 : 17;

        Image elementIcon = CreateImage("ElementIcon", root, Color.white);
        elementIcon.preserveAspect = true;
        elementIcon.raycastTarget = false;

        Image cpChip = CreateSkillTagChip(root, "CPChip", SkillTagCPTint, out Text cpText);
        Image powerChip = CreateSkillTagChip(root, "PowerChip", SkillTagPowerTint, out Text powerText);
        Image counterChip = CreateSkillTagChip(root, "CounterChip", SkillTagCounterTint, out Text counterText);

        Text state = CreateText("State", root, compact ? 11 : 13, FontStyle.Bold, compact ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight, new Color(0.64f, 0.84f, 0.90f, 0.94f));
        ApplyCrispCyberText(state, new Color(0f, 0.12f, 0.18f, 0.9f));

        Image slotChip = null;
        Text slotChipText = null;
        if (compact)
        {
            // Slot number wears the same corner chip as battle hotkeys.
            slotChip = CreateSkillTagChip(root, "SlotChip", new Color(0.62f, 0.93f, 1f, 1f), out slotChipText);
            SetAnchors(slotChip.rectTransform, new Vector2(0.815f, 0.700f), new Vector2(0.952f, 0.930f));
        }

        if (compact)
        {
            SetAnchors(badge.rectTransform, new Vector2(0.048f, 0.585f), new Vector2(0.220f, 0.935f));
            SetAnchors(elementIcon.rectTransform, new Vector2(0.255f, 0.610f), new Vector2(0.385f, 0.915f));
            nameText.alignment = TextAnchor.MiddleCenter;
            SetAnchors(nameText.rectTransform, new Vector2(0.050f, 0.310f), new Vector2(0.950f, 0.560f));
            SetAnchors(cpChip.rectTransform, new Vector2(0.085f, 0.065f), new Vector2(0.330f, 0.270f));
            SetAnchors(powerChip.rectTransform, new Vector2(0.365f, 0.065f), new Vector2(0.610f, 0.270f));
            SetAnchors(counterChip.rectTransform, new Vector2(0.645f, 0.065f), new Vector2(0.800f, 0.270f));
            state.gameObject.SetActive(false);
        }
        else
        {
            SetAnchors(badge.rectTransform, new Vector2(0.014f, 0.150f), new Vector2(0.066f, 0.850f));
            // Short name band: best-fit shrinks long names onto a single line
            // instead of wrapping (same trick as the battle skill slots).
            SetAnchors(nameText.rectTransform, new Vector2(0.082f, 0.300f), new Vector2(0.390f, 0.700f));
            SetAnchors(elementIcon.rectTransform, new Vector2(0.398f, 0.180f), new Vector2(0.446f, 0.820f));
            SetAnchors(cpChip.rectTransform, new Vector2(0.464f, 0.180f), new Vector2(0.560f, 0.820f));
            SetAnchors(powerChip.rectTransform, new Vector2(0.576f, 0.180f), new Vector2(0.672f, 0.820f));
            SetAnchors(counterChip.rectTransform, new Vector2(0.688f, 0.180f), new Vector2(0.752f, 0.820f));
            SetAnchors(state.rectTransform, new Vector2(0.764f, 0.100f), new Vector2(0.985f, 0.900f));
        }

        // Cyan hover ring driven by the shared battle feedback component.
        Image hoverGlow = CreateImage("HoverGlow", root, new Color(0.45f, 0.95f, 1f, 0f));
        hoverGlow.sprite = SkillSwapSelectFrameSprite();
        hoverGlow.type = hoverGlow.sprite != null && hoverGlow.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        hoverGlow.raycastTarget = false;
        SetAnchors(hoverGlow.rectTransform, new Vector2(-0.012f, -0.030f), new Vector2(1.012f, 1.030f));

        glow.transform.SetAsLastSibling();
        hoverGlow.transform.SetAsLastSibling();

        BattleHudButtonFeedback feedback = root.gameObject.AddComponent<BattleHudButtonFeedback>();
        feedback.Configure(button, compact ? 1.035f : 1.015f, 0.965f);
        feedback.SetOverlay(hoverGlow, new Color(0.45f, 0.95f, 1f, 1f), 0.42f, 0.72f, 0.58f);

        bool isLoadout = compact;
        EventTrigger trigger = root.gameObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => OnSkillSwapCardHover(isLoadout, hoverIndex));
        trigger.triggers.Add(enter);

        return new SkillCardRefs
        {
            Root = root,
            Button = button,
            Frame = frame,
            Group = group,
            InstructionBadge = badge,
            InstructionIcon = instrIcon,
            InstructionLetter = instrLetter,
            NameText = nameText,
            ElementIcon = elementIcon,
            CPChip = cpChip,
            CPText = cpText,
            PowerChip = powerChip,
            PowerText = powerText,
            CounterChip = counterChip,
            CounterText = counterText,
            StateText = state,
            SlotChip = slotChip,
            SlotChipText = slotChipText,
            Glow = glow,
            HoverGlow = hoverGlow
        };
    }

    private Image CreateSkillTagChip(Transform parent, string objectName, Color tint, out Text text)
    {
        Image chip = CreateImage(objectName, parent, tint);
        chip.sprite = SkillChipFrameSprite();
        chip.type = Image.Type.Sliced;
        chip.pixelsPerUnitMultiplier = 2.4f;
        chip.raycastTarget = false;

        text = CreateText("Text", chip.transform, 12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        ApplyCyberText(text, new Color(0f, 0f, 0f, 0.7f), new Vector2(1f, -1f));
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = 14;
        SetAnchors(text.rectTransform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.98f));
        return chip;
    }

    /// <summary>Right-hand skill data board, fed by hovering any card (battle detail-panel style).</summary>
    private void BuildSkillSwapDetailPanel(Transform box)
    {
        Text caption = CreateText("SkillSwapDetailLabel", box, 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.6f, 0.92f, 1f, 0.92f));
        ApplyCrispCyberText(caption, new Color(0f, 0.1f, 0.16f, 0.9f));
        SetAnchors(caption.rectTransform, new Vector2(0.595f, 0.570f), new Vector2(0.964f, 0.618f));
        caption.text = "SKILL DATA";

        RectTransform detail = CreateRect("SkillSwapDetail", box);
        SetAnchors(detail, new Vector2(0.595f, 0.035f), new Vector2(0.964f, 0.560f));
        Image bg = detail.gameObject.AddComponent<Image>();
        bg.sprite = SkillCardFrameSprite();
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 1.1f;
        bg.color = Color.white;
        bg.raycastTarget = false;

        skillSwapDetailName = CreateText("DetailName", detail, 19, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        ApplyCrispCyberText(skillSwapDetailName, new Color(0f, 0.12f, 0.18f, 0.95f));
        skillSwapDetailName.resizeTextForBestFit = true;
        skillSwapDetailName.resizeTextMinSize = 10;
        skillSwapDetailName.resizeTextMaxSize = 19;
        SetAnchors(skillSwapDetailName.rectTransform, new Vector2(0.055f, 0.866f), new Vector2(0.945f, 0.972f));
        skillSwapDetailName.text = "SKILL DATA";

        skillSwapDetailBadge = CreateImage("DetailInstrBadge", detail, Color.white);
        skillSwapDetailBadge.sprite = SkillCardFrameSprite();
        skillSwapDetailBadge.type = Image.Type.Sliced;
        skillSwapDetailBadge.pixelsPerUnitMultiplier = 2.4f;
        skillSwapDetailBadge.raycastTarget = false;
        SetAnchors(skillSwapDetailBadge.rectTransform, new Vector2(0.055f, 0.738f), new Vector2(0.160f, 0.852f));

        skillSwapDetailBadgeIcon = CreateImage("DetailInstrIcon", skillSwapDetailBadge.transform, Color.white);
        skillSwapDetailBadgeIcon.preserveAspect = true;
        skillSwapDetailBadgeIcon.raycastTarget = false;
        SetAnchors(skillSwapDetailBadgeIcon.rectTransform, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f));

        skillSwapDetailBadgeLetter = CreateText("DetailInstrLetter", skillSwapDetailBadge.transform, 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        ApplyCyberText(skillSwapDetailBadgeLetter, new Color(0f, 0f, 0f, 0.75f), new Vector2(1f, -1f));
        SetAnchors(skillSwapDetailBadgeLetter.rectTransform, Vector2.zero, Vector2.one);

        skillSwapDetailElementIcon = CreateImage("DetailElementIcon", detail, Color.white);
        skillSwapDetailElementIcon.preserveAspect = true;
        skillSwapDetailElementIcon.raycastTarget = false;
        SetAnchors(skillSwapDetailElementIcon.rectTransform, new Vector2(0.185f, 0.745f), new Vector2(0.275f, 0.845f));

        skillSwapDetailCPChip = CreateSkillTagChip(detail, "DetailCPChip", SkillTagCPTint, out skillSwapDetailCPText);
        SetAnchors(skillSwapDetailCPChip.rectTransform, new Vector2(0.305f, 0.738f), new Vector2(0.460f, 0.852f));
        skillSwapDetailPowerChip = CreateSkillTagChip(detail, "DetailPowerChip", SkillTagPowerTint, out skillSwapDetailPowerText);
        SetAnchors(skillSwapDetailPowerChip.rectTransform, new Vector2(0.480f, 0.738f), new Vector2(0.635f, 0.852f));
        skillSwapDetailCounterChip = CreateSkillTagChip(detail, "DetailCounterChip", SkillTagCounterTint, out skillSwapDetailCounterText);
        SetAnchors(skillSwapDetailCounterChip.rectTransform, new Vector2(0.655f, 0.738f), new Vector2(0.790f, 0.852f));

        skillSwapDetailBody = CreateText("DetailBody", detail, 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.86f, 0.97f, 1f, 0.96f));
        ApplyCrispCyberText(skillSwapDetailBody, new Color(0f, 0.12f, 0.18f, 0.9f));
        skillSwapDetailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        skillSwapDetailBody.verticalOverflow = VerticalWrapMode.Truncate;
        skillSwapDetailBody.lineSpacing = 1.02f;
        SetAnchors(skillSwapDetailBody.rectTransform, new Vector2(0.055f, 0.045f), new Vector2(0.945f, 0.715f));

        ShowSkillSwapDetail(null, 0);
    }

    /// <summary>Fills the data board with rich-text detail (same formatter as the battle HUD).</summary>
    private void ShowSkillSwapDetail(SkillData skill, int unlockLevel)
    {
        if (skillSwapDetailName == null)
            return;

        bool has = skill != null;
        if (skillSwapDetailBadge != null) skillSwapDetailBadge.gameObject.SetActive(has);
        if (skillSwapDetailElementIcon != null) skillSwapDetailElementIcon.gameObject.SetActive(has);
        if (skillSwapDetailCPChip != null) skillSwapDetailCPChip.gameObject.SetActive(has);
        if (skillSwapDetailPowerChip != null) skillSwapDetailPowerChip.gameObject.SetActive(has && skill.basePower > 0);
        if (skillSwapDetailCounterChip != null) skillSwapDetailCounterChip.gameObject.SetActive(has && skill.canCounter);

        if (!has)
        {
            skillSwapDetailName.text = "SKILL DATA";
            if (skillSwapDetailBody != null)
                skillSwapDetailBody.text = "HOVER A SKILL TO VIEW ITS DATA.";
            return;
        }

        skillSwapDetailName.text = SkillDisplayName(skill).ToUpperInvariant();

        InstructionType instruction = skill.instructionType;
        Color accent = InstructionAccentColor(instruction);
        Sprite instructionIcon = SkillSwapInstructionIcon(instruction);
        if (skillSwapDetailBadgeIcon != null)
        {
            skillSwapDetailBadgeIcon.sprite = instructionIcon;
            skillSwapDetailBadgeIcon.enabled = instructionIcon != null;
            skillSwapDetailBadgeIcon.color = instructionIcon != null ? Color.Lerp(Color.white, accent, 0.40f) : Color.clear;
        }
        if (skillSwapDetailBadgeLetter != null)
        {
            skillSwapDetailBadgeLetter.text = InstructionLetterFor(instruction);
            skillSwapDetailBadgeLetter.color = accent;
            skillSwapDetailBadgeLetter.enabled = instructionIcon == null;
        }
        Sprite elementIcon = SkillSwapElementIcon(skill.elementType);
        if (skillSwapDetailElementIcon != null)
        {
            skillSwapDetailElementIcon.sprite = elementIcon;
            skillSwapDetailElementIcon.enabled = elementIcon != null;
        }
        if (skillSwapDetailCPText != null) skillSwapDetailCPText.text = $"CP {Mathf.Max(0, skill.cpCost)}";
        if (skillSwapDetailPowerText != null) skillSwapDetailPowerText.text = $"BP {skill.basePower}";
        if (skillSwapDetailCounterText != null) skillSwapDetailCounterText.text = "CNT";

        var meta = new StringBuilder();
        meta.Append(skill.instructionType);
        meta.Append(" | ");
        meta.Append(skill.elementType);
        if (unlockLevel > 1)
            meta.Append($" | Unlock L{unlockLevel:00}");

        if (skillSwapDetailBody != null)
            skillSwapDetailBody.text = SkillDetailTextFormatter.BuildBody(
                meta.ToString(),
                SkillDetailTextFormatter.BuildCounterSummary(skill),
                SkillDetailTextFormatter.BuildReadableDescription(skill));
    }

    private void OnSkillSwapCardHover(bool isLoadout, int index)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        AlgoMonInstance mon = SelectedPayloadMon(manager);
        if (mon == null)
            return;

        if (isLoadout)
        {
            SkillData skill = mon.knownSkills != null && index >= 0 && index < mon.knownSkills.Count
                ? mon.knownSkills[index]
                : null;
            if (skill != null)
                ShowSkillSwapDetail(skill, UnlockLevelFor(mon, skill));
        }
        else if (skillSwapLearnEntries != null && index >= 0 && index < skillSwapLearnEntries.Length)
        {
            LearnsetEntry entry = skillSwapLearnEntries[index];
            if (entry.skill != null)
                ShowSkillSwapDetail(entry.skill, entry.unlockLevel);
        }
    }

    private static int UnlockLevelFor(AlgoMonInstance mon, SkillData skill)
    {
        if (mon == null || mon.data == null || mon.data.learnset == null || skill == null)
            return 0;

        foreach (LearnsetEntry entry in mon.data.learnset)
        {
            if (entry.skill == skill)
                return entry.unlockLevel;
        }
        return 0;
    }

    private void RenderSkillSwapPanel()
    {
        if (skillSwapPanelRoot == null)
            return;
        if (!EnsureSkillSwapPanelBuilt())
            return;

        manager = manager != null ? manager : GameManager.EnsureInstance();
        AlgoMonInstance mon = SelectedPayloadMon(manager);
        if (mon != null)
            EnsureKnownSkillList(mon);

        if (skillSwapTitle != null)
            skillSwapTitle.text = mon != null
                ? $"SKILL LOADOUT  ·  {DisplayNameFor(mon).ToUpperInvariant()}  L{mon.level:00}"
                : "SKILL LOADOUT";

        if (skillSwapMessageText != null)
        {
            if (mon == null)
            {
                skillSwapMessageText.text = "SELECT A UNIT";
                skillSwapMessageText.color = new Color(0.72f, 0.90f, 0.96f, 0.88f);
            }
            else
            {
                string loadout = $"{KnownSkillCount(mon)}/{AlgoMonInstance.MaxSkillSlots} LOADED";
                bool hasPrompt = !string.IsNullOrWhiteSpace(payloadSkillMessage);
                string msg = hasPrompt
                    ? payloadSkillMessage.Trim()
                    : skillSwapSelectedSlot >= 0
                        ? $"SLOT {skillSwapSelectedSlot + 1} ARMED - CHOOSE A SKILL"
                        : pendingPayloadSkillReplacement != null
                            ? $"{SkillDisplayName(pendingPayloadSkillReplacement).ToUpperInvariant()} ARMED - CHOOSE A SLOT"
                            : "CHOOSE A SKILL OR ARM A SLOT";
                bool armed = skillSwapSelectedSlot >= 0 || pendingPayloadSkillReplacement != null;
                skillSwapMessageText.color = armed || hasPrompt
                    ? new Color(1f, 0.82f, 0.34f, 1f)
                    : new Color(0.72f, 0.90f, 0.96f, 0.90f);
                skillSwapMessageText.text = $"{loadout}   ·   {msg}";
            }
        }

        for (int i = 0; i < skillSwapLoadoutCards.Length; i++)
        {
            SkillData skill = (mon != null && mon.knownSkills != null && i < mon.knownSkills.Count)
                ? mon.knownSkills[i]
                : null;
            ApplyLoadoutCard(skillSwapLoadoutCards[i], i, skill, skillSwapSelectedSlot == i);
        }

        LearnsetEntry[] learnset = mon != null && mon.data != null ? mon.data.learnset : null;
        int learnsetIndex = 0;
        for (int row = 0; row < skillSwapLearnCards.Length; row++)
        {
            LearnsetEntry entry = NextValidLearnsetEntry(learnset, ref learnsetIndex);
            skillSwapLearnEntries[row] = entry;
            ApplyLearnCard(skillSwapLearnCards[row], mon, entry);
        }
    }

    private void ApplyLoadoutCard(SkillCardRefs card, int slotIndex, SkillData skill, bool armed)
    {
        if (card == null)
            return;

        card.Root.gameObject.SetActive(true);
        card.Button.interactable = true;
        bool filled = skill != null;
        if (card.Group != null)
            card.Group.alpha = filled ? 1f : 0.62f;

        if (card.SlotChipText != null)
            card.SlotChipText.text = (slotIndex + 1).ToString();
        if (card.SlotChip != null)
            card.SlotChip.color = armed ? new Color(1f, 0.86f, 0.36f, 1f) : new Color(0.62f, 0.93f, 1f, 1f);

        if (filled)
            FillSkillCardContent(card, skill);
        else
            ClearSkillCardContent(card, "EMPTY");

        if (card.Glow != null)
        {
            card.Glow.gameObject.SetActive(armed);
            card.Glow.color = new Color(1f, 0.86f, 0.36f, armed ? 0.9f : 0f);
        }
        if (card.Frame != null)
            card.Frame.color = armed ? new Color(1f, 0.92f, 0.66f, 1f) : Color.white;
    }

    private void ApplyLearnCard(SkillCardRefs card, AlgoMonInstance mon, LearnsetEntry entry)
    {
        if (card == null)
            return;

        SkillData skill = entry.skill;
        if (skill == null || mon == null)
        {
            card.Root.gameObject.SetActive(false);
            return;
        }

        bool known = mon.knownSkills != null && mon.knownSkills.Contains(skill);
        bool locked = entry.unlockLevel > mon.level;
        bool pending = pendingPayloadSkillReplacement == skill;

        card.Root.gameObject.SetActive(true);
        card.Button.interactable = !locked;

        FillSkillCardContent(card, skill);

        if (card.StateText != null)
        {
            card.StateText.text = pending
                ? "ARMED"
                : skillSwapSelectedSlot >= 0 && !known && !locked
                    ? $"TO SLOT {skillSwapSelectedSlot + 1}"
                    : known
                        ? $"LOADED L{entry.unlockLevel:00}"
                        : locked
                            ? $"LOCKED L{entry.unlockLevel:00}"
                            : $"{SkillLearnState(mon, entry).Trim()} L{entry.unlockLevel:00}";
            card.StateText.color = pending
                ? new Color(1f, 0.82f, 0.34f, 1f)
                : skillSwapSelectedSlot >= 0 && !known && !locked
                    ? new Color(0.55f, 1f, 0.62f, 1f)
                    : known
                        ? new Color(0.52f, 0.74f, 0.78f, 0.82f)
                        : locked
                            ? new Color(0.54f, 0.50f, 0.56f, 0.78f)
                            : new Color(0.55f, 1f, 0.62f, 1f);
        }

        if (card.Group != null)
            card.Group.alpha = locked ? 0.34f : known ? 0.58f : 1f;

        if (card.Glow != null)
        {
            card.Glow.gameObject.SetActive(pending);
            card.Glow.color = pending
                ? new Color(1f, 0.86f, 0.36f, 0.9f)
                : new Color(0f, 0f, 0f, 0f);
        }
        if (card.Frame != null)
            card.Frame.color = pending
                ? new Color(1f, 0.92f, 0.66f, 1f)
                : known
                    ? new Color(0.54f, 0.74f, 0.78f, 0.72f)
                    : locked
                        ? new Color(0.48f, 0.45f, 0.52f, 0.62f)
                        : Color.white;
    }

    private void FillSkillCardContent(SkillCardRefs card, SkillData skill)
    {
        InstructionType instruction = skill.instructionType;
        Color accent = InstructionAccentColor(instruction);
        Sprite instructionIcon = SkillSwapInstructionIcon(instruction);

        if (card.InstructionBadge != null)
            card.InstructionBadge.gameObject.SetActive(true);
        if (card.InstructionIcon != null)
        {
            card.InstructionIcon.sprite = instructionIcon;
            card.InstructionIcon.enabled = instructionIcon != null;
            card.InstructionIcon.color = instructionIcon != null ? Color.Lerp(Color.white, accent, 0.40f) : Color.clear;
        }
        if (card.InstructionLetter != null)
        {
            card.InstructionLetter.text = InstructionLetterFor(instruction);
            card.InstructionLetter.color = accent;
            card.InstructionLetter.enabled = instructionIcon == null;
        }
        if (card.NameText != null)
        {
            card.NameText.text = SkillDisplayName(skill).ToUpperInvariant();
            card.NameText.color = new Color(0.9f, 1f, 0.98f, 1f);
        }
        if (card.ElementIcon != null)
        {
            Sprite elementIcon = SkillSwapElementIcon(skill.elementType);
            card.ElementIcon.sprite = elementIcon;
            card.ElementIcon.enabled = elementIcon != null;
            card.ElementIcon.color = Color.white;
        }

        // Battle-style tag chips: CP always, BP only for damaging skills, C for counters.
        if (card.CPChip != null)
            card.CPChip.gameObject.SetActive(true);
        if (card.CPText != null)
            card.CPText.text = $"CP {Mathf.Max(0, skill.cpCost)}";
        if (card.PowerChip != null)
            card.PowerChip.gameObject.SetActive(skill.basePower > 0);
        if (card.PowerText != null)
            card.PowerText.text = $"BP {skill.basePower}";
        if (card.CounterChip != null)
            card.CounterChip.gameObject.SetActive(skill.canCounter);
        if (card.CounterText != null)
            card.CounterText.text = "C";
    }

    private void ClearSkillCardContent(SkillCardRefs card, string placeholder)
    {
        if (card.InstructionBadge != null)
            card.InstructionBadge.gameObject.SetActive(false);
        if (card.InstructionIcon != null)
            card.InstructionIcon.enabled = false;
        if (card.InstructionLetter != null)
            card.InstructionLetter.enabled = false;
        if (card.ElementIcon != null)
            card.ElementIcon.enabled = false;
        if (card.CPChip != null)
            card.CPChip.gameObject.SetActive(false);
        if (card.PowerChip != null)
            card.PowerChip.gameObject.SetActive(false);
        if (card.CounterChip != null)
            card.CounterChip.gameObject.SetActive(false);
        if (card.NameText != null)
        {
            card.NameText.text = placeholder;
            card.NameText.color = new Color(0.6f, 0.76f, 0.82f, 0.8f);
        }
    }

    // Tap a learnable card: respects the level gate, then fills a free slot, drops
    // into an armed slot, or stages itself for a replace when the loadout is full.
    private void OnSkillSwapLearnClicked(int index)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        AlgoMonInstance mon = SelectedPayloadMon(manager);
        if (mon == null || skillSwapLearnEntries == null || index < 0 || index >= skillSwapLearnEntries.Length)
            return;

        LearnsetEntry entry = skillSwapLearnEntries[index];
        SkillData skill = entry.skill;
        if (skill == null)
            return;

        EnsureKnownSkillList(mon);
        string skillName = SkillDisplayName(skill).ToUpperInvariant();

        // Level gate — cannot learn before the unit reaches the unlock level.
        if (entry.unlockLevel > mon.level)
        {
            pendingPayloadSkillReplacement = null;
            skillSwapSelectedSlot = -1;
            payloadSkillMessage = $"{skillName} UNLOCKS AT L{entry.unlockLevel:00}";
            RenderSkillSwapPanel();
            return;
        }

        int knownIndex = mon.knownSkills.IndexOf(skill);

        // A loadout slot is armed → drop this skill into it (swap / fill).
        if (skillSwapSelectedSlot >= 0)
        {
            if (knownIndex >= 0 && knownIndex != skillSwapSelectedSlot)
                payloadSkillMessage = $"{skillName} ALREADY LOADED";
            else
                ApplySkillToSlot(mon, skillSwapSelectedSlot, skill);
            skillSwapSelectedSlot = -1;
            pendingPayloadSkillReplacement = null;
            RenderSkillSwapPanel();
            RenderPayloadGrid(manager);
            return;
        }

        if (knownIndex >= 0)
        {
            pendingPayloadSkillReplacement = null;
            payloadSkillMessage = $"{skillName} ALREADY LOADED";
            RenderSkillSwapPanel();
            return;
        }

        if (mon.knownSkills.Count < AlgoMonInstance.MaxSkillSlots)
        {
            mon.knownSkills.Add(skill);
            pendingPayloadSkillReplacement = null;
            payloadSkillMessage = $"{skillName} LOADED";
            RenderSkillSwapPanel();
            RenderPayloadGrid(manager);
            return;
        }

        // Loadout full: stage this skill and ask which slot to overwrite.
        pendingPayloadSkillReplacement = skill;
        payloadSkillMessage = "TAP A LOADOUT SLOT TO REPLACE";
        RenderSkillSwapPanel();
    }

    // Tap a loadout slot: receives a staged skill, or arms the slot for the next pick.
    private void OnSkillSwapLoadoutClicked(int slot)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        AlgoMonInstance mon = SelectedPayloadMon(manager);
        if (mon == null || slot < 0 || slot >= AlgoMonInstance.MaxSkillSlots)
            return;

        EnsureKnownSkillList(mon);

        if (pendingPayloadSkillReplacement != null)
        {
            if (mon.knownSkills.Contains(pendingPayloadSkillReplacement))
                payloadSkillMessage = $"{SkillDisplayName(pendingPayloadSkillReplacement).ToUpperInvariant()} ALREADY LOADED";
            else
                ApplySkillToSlot(mon, slot, pendingPayloadSkillReplacement);
            pendingPayloadSkillReplacement = null;
            skillSwapSelectedSlot = -1;
            RenderSkillSwapPanel();
            RenderPayloadGrid(manager);
            return;
        }

        bool filled = slot < mon.knownSkills.Count;
        if (!filled)
        {
            skillSwapSelectedSlot = -1;
            payloadSkillMessage = "TAP A LEARNABLE SKILL TO LOAD";
            RenderSkillSwapPanel();
            return;
        }

        // Toggle this filled slot as the target for the next learnable pick.
        skillSwapSelectedSlot = skillSwapSelectedSlot == slot ? -1 : slot;
        payloadSkillMessage = skillSwapSelectedSlot >= 0
            ? "TAP A LEARNABLE SKILL FOR THIS SLOT"
            : string.Empty;
        RenderSkillSwapPanel();
    }

    private void ApplySkillToSlot(AlgoMonInstance mon, int slot, SkillData skill)
    {
        string newName = SkillDisplayName(skill).ToUpperInvariant();
        if (slot >= 0 && slot < mon.knownSkills.Count)
        {
            SkillData old = mon.knownSkills[slot];
            mon.knownSkills[slot] = skill;
            payloadSkillMessage = old != null
                ? $"{SkillDisplayName(old).ToUpperInvariant()} -> {newName}"
                : $"{newName} LOADED";
        }
        else if (mon.knownSkills.Count < AlgoMonInstance.MaxSkillSlots)
        {
            mon.knownSkills.Add(skill);
            payloadSkillMessage = $"{newName} LOADED";
        }
    }

    private static void EnsureKnownSkillList(AlgoMonInstance mon)
    {
        mon.EnsurePersistentRuntimeState();
        if (mon.knownSkills == null)
            mon.knownSkills = new List<SkillData>();
        mon.knownSkills.RemoveAll(s => s == null);
    }

    private void ToggleSelectedFavorite()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        AlgoMonInstance mon = SelectedPayloadMon(manager);
        if (mon == null)
            return;

        mon.isFavorite = !mon.isFavorite;
        FocusPayloadPageOn(mon, manager);
        RenderPayloadGrid(manager);
    }

    private void FocusPayloadPageOn(AlgoMonInstance mon, GameManager targetManager)
    {
        if (mon == null || targetManager == null || payloadCellFrames == null || payloadCellFrames.Length == 0)
            return;

        BuildPayloadDisplayOrder(targetManager);
        int orderIndex = payloadDisplayOrder.IndexOf(mon);
        if (orderIndex >= 0)
            payloadPage = orderIndex / payloadCellFrames.Length;
    }

    private void ToggleSelectedSquad()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        AlgoMonInstance mon = SelectedPayloadMon(manager);
        if (mon == null)
            return;

        if (manager.IsInParty(mon))
        {
            manager.RemoveFromParty(mon);
            RenderPayloadGrid(manager);
        }
        else if (manager.party != null && manager.party.Count < GameManager.MaxPartySize)
        {
            manager.AddToParty(mon);
            RenderPayloadGrid(manager);
        }
        else
        {
            // Squad full: open the replace picker so the player swaps one out.
            OpenSquadPanel(true, mon);
        }
    }

    private void EnsureSquadPanel(Transform parent)
    {
        squadPanelRoot = CreateRect("SquadPanel", parent);
        SetAnchors(squadPanelRoot, Vector2.zero, Vector2.one);

        Image backdrop = squadPanelRoot.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.66f);
        backdrop.raycastTarget = true;
        Button backdropButton = squadPanelRoot.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(CloseSquadPanel);

        RectTransform box = CreateRect("SquadPanelBox", squadPanelRoot);
        SetAnchors(box, new Vector2(0.07f, 0.18f), new Vector2(0.93f, 0.84f));
        Image boxBg = box.gameObject.AddComponent<Image>();
        ApplyPanelFrameBackground(
            boxBg,
            ResolveSquadPanelBackgroundSprite(),
            new Color(0.02f, 0.05f, 0.09f, 0.99f),
            false);
        boxBg.raycastTarget = true;

        squadPanelTitle = CreateText("SquadTitle", box, 24, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.7f, 0.98f, 1f, 1f));
        ApplyCrispCyberText(squadPanelTitle, new Color(0f, 0.14f, 0.2f, 1f));
        SetAnchors(squadPanelTitle.rectTransform, new Vector2(0.05f, 0.765f), new Vector2(0.78f, 0.875f));
        squadPanelTitle.text = "ACTIVE SQUAD";

        Button closeButton = FindOrCreatePanelButton("SquadCloseButton", box, "CLOSE", new Vector2(0.800f, 0.755f), new Vector2(0.965f, 0.885f));
        SetPanelButtonLabelSize(closeButton, 15);
        closeButton.onClick.AddListener(CloseSquadPanel);

        int max = GameManager.MaxPartySize;
        squadSlotPortraits = new Image[max];
        squadSlotLabels = new Text[max];
        squadSlotLeadBadges = new Text[max];
        squadSlotLeadButtons = new Button[max];
        squadSlotActionButtons = new Button[max];
        squadSlotActionLabels = new Text[max];

        float slotW = 0.92f / max;
        const float gap = 0.018f;
        for (int i = 0; i < max; i++)
        {
            float x0 = 0.04f + i * slotW + gap * 0.5f;
            float x1 = 0.04f + (i + 1) * slotW - gap * 0.5f;
            RectTransform slot = CreateRect("SquadSlot_" + i, box);
            SetAnchors(slot, new Vector2(x0, 0.060f), new Vector2(x1, 0.735f));
            Image slotBg = slot.gameObject.AddComponent<Image>();
            ApplyPanelFrameBackground(
                slotBg,
                ResolveMonsterDisplayPanelSprite(),
                new Color(0.05f, 0.12f, 0.2f, 0.9f),
                false);
            slotBg.raycastTarget = false;

            Image portrait = CreateImage("SquadSlotPortrait_" + i, slot, Color.white);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            SetAnchors(portrait.rectTransform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.84f));
            squadSlotPortraits[i] = portrait;

            Text badge = CreateText("SquadSlotLead_" + i, slot, 16, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.78f, 0.3f, 1f));
            ApplyCyberText(badge, new Color(0.1f, 0.05f, 0f, 1f), new Vector2(1f, -1f));
            SetAnchors(badge.rectTransform, new Vector2(0.08f, 0.760f), new Vector2(0.76f, 0.860f));
            badge.text = "#1 LEAD";
            squadSlotLeadBadges[i] = badge;

            Text label = CreateText("SquadSlotLabel_" + i, slot, 17, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 1f, 0.96f, 1f));
            ApplyCrispCyberText(label, new Color(0f, 0.12f, 0.18f, 0.95f));
            SetAnchors(label.rectTransform, new Vector2(0.02f, 0.325f), new Vector2(0.98f, 0.440f));
            squadSlotLabels[i] = label;

            int captured = i;
            Button leadButton = FindOrCreatePanelButton("SquadSlotLeadBtn_" + i, slot, "SET #1", new Vector2(0.08f, 0.175f), new Vector2(0.92f, 0.325f));
            SetPanelButtonLabelSize(leadButton, 14);
            leadButton.onClick.AddListener(() => SquadSetLead(captured));
            squadSlotLeadButtons[i] = leadButton;

            Button actionButton = FindOrCreatePanelButton("SquadSlotActionBtn_" + i, slot, "REMOVE", new Vector2(0.08f, 0.030f), new Vector2(0.92f, 0.175f));
            SetPanelButtonLabelSize(actionButton, 15);
            actionButton.onClick.AddListener(() => SquadSlotAction(captured));
            squadSlotActionButtons[i] = actionButton;
            Transform al = actionButton.transform.Find("Text");
            squadSlotActionLabels[i] = al != null ? al.GetComponent<Text>() : null;
        }

        squadPanelRoot.gameObject.SetActive(false);
    }

    private void OpenSquadPanel(bool replaceMode, AlgoMonInstance incoming)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (squadPanelRoot == null)
            return;

        squadReplaceMode = replaceMode;
        squadReplaceIncoming = incoming;
        squadPanelRoot.gameObject.SetActive(true);
        squadPanelRoot.SetAsLastSibling();
        RenderSquadPanel();
    }

    private void CloseSquadPanel()
    {
        squadReplaceMode = false;
        squadReplaceIncoming = null;
        if (squadPanelRoot != null)
            squadPanelRoot.gameObject.SetActive(false);
    }

    private void RenderSquadPanel()
    {
        if (squadPanelRoot == null)
            return;

        manager = manager != null ? manager : GameManager.EnsureInstance();
        List<AlgoMonInstance> party = manager.party;
        int max = GameManager.MaxPartySize;

        if (squadPanelTitle != null)
        {
            squadPanelTitle.text = squadReplaceMode && squadReplaceIncoming != null
                ? $"SQUAD FULL - REPLACE WHO WITH {DisplayNameFor(squadReplaceIncoming).ToUpperInvariant()}?"
                : "ACTIVE SQUAD  (#1 = LEAD)";
        }

        for (int i = 0; i < max; i++)
        {
            AlgoMonInstance mon = (party != null && i < party.Count) ? party[i] : null;

            if (squadSlotPortraits[i] != null)
            {
                Sprite s = ResolvePayloadSprite(mon);
                squadSlotPortraits[i].sprite = s;
                squadSlotPortraits[i].enabled = s != null;
            }
            if (squadSlotLabels[i] != null)
                squadSlotLabels[i].text = mon != null ? $"{DisplayNameFor(mon).ToUpperInvariant()}\nL{mon.level:00}" : "EMPTY";
            if (squadSlotLeadBadges[i] != null)
                squadSlotLeadBadges[i].gameObject.SetActive(mon != null && i == 0);
            if (squadSlotLeadButtons[i] != null)
                squadSlotLeadButtons[i].gameObject.SetActive(!squadReplaceMode && mon != null && i > 0);
            if (squadSlotActionButtons[i] != null)
            {
                bool filled = mon != null;
                squadSlotActionButtons[i].gameObject.SetActive(filled);
                squadSlotActionButtons[i].interactable = filled;
                if (squadSlotActionLabels[i] != null)
                    squadSlotActionLabels[i].text = squadReplaceMode ? "REPLACE" : "REMOVE";
            }
        }
    }

    private void SquadSetLead(int index)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        List<AlgoMonInstance> party = manager.party;
        if (party == null || index <= 0 || index >= party.Count)
            return;

        AlgoMonInstance mon = party[index];
        party.RemoveAt(index);
        party.Insert(0, mon);
        RenderSquadPanel();
        RenderPayloadGrid(manager);
    }

    private void SquadSlotAction(int index)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        List<AlgoMonInstance> party = manager.party;
        if (party == null || index < 0 || index >= party.Count)
            return;

        if (squadReplaceMode)
        {
            if (squadReplaceIncoming != null && !manager.IsInParty(squadReplaceIncoming))
            {
                if (manager.TryReplacePartyMember(index, squadReplaceIncoming))
                {
                    CloseSquadPanel();
                    RenderPayloadGrid(manager);
                }
            }
            return;
        }

        manager.RemoveFromParty(party[index]);
        RenderSquadPanel();
        RenderPayloadGrid(manager);
    }

    private void UpdateSquadButton(AlgoMonInstance mon, GameManager targetManager)
    {
        if (inspectorSquadButton == null)
            return;

        if (mon == null || targetManager == null)
        {
            inspectorSquadButton.interactable = false;
            if (inspectorSquadButtonLabel != null)
            {
                inspectorSquadButtonLabel.text = "--";
                inspectorSquadButtonLabel.color = new Color(0.56f, 0.68f, 0.72f, 1f);
            }
            return;
        }

        int count = targetManager.party != null ? targetManager.party.Count : 0;
        int max = GameManager.MaxPartySize;

        if (targetManager.IsInParty(mon))
        {
            inspectorSquadButton.interactable = true;
            if (inspectorSquadButtonLabel != null)
            {
                inspectorSquadButtonLabel.text = "REMOVE FROM SQUAD";
                inspectorSquadButtonLabel.color = new Color(1f, 0.66f, 0.76f, 1f);
            }
        }
        else if (count >= max)
        {
            inspectorSquadButton.interactable = false;
            if (inspectorSquadButtonLabel != null)
            {
                inspectorSquadButtonLabel.text = "SQUAD FULL";
                inspectorSquadButtonLabel.color = new Color(0.56f, 0.68f, 0.72f, 1f);
            }
        }
        else
        {
            inspectorSquadButton.interactable = true;
            if (inspectorSquadButtonLabel != null)
            {
                inspectorSquadButtonLabel.text = "ADD TO SQUAD";
                inspectorSquadButtonLabel.color = new Color(0.82f, 1f, 0.94f, 1f);
            }
        }
    }

    private void UpdateFavoriteButton(AlgoMonInstance mon)
    {
        if (inspectorFavoriteButton == null)
            return;

        bool hasMon = mon != null;
        inspectorFavoriteButton.interactable = hasMon;

        if (inspectorFavoriteStar == null)
            return;

        bool favorite = hasMon && mon.isFavorite;
        // Hollow star = not favorite, filled star = favorite.
        inspectorFavoriteStar.sprite = StarSprite(favorite);
        inspectorFavoriteStar.color = !hasMon
            ? new Color(0.46f, 0.58f, 0.62f, 0.75f)
            : favorite
                ? new Color(1f, 0.82f, 0.32f, 1f)
                : new Color(0.78f, 0.94f, 0.98f, 0.95f);
    }

    private void SetInspectorIdle(AlgoMonInstance mon)
    {
        string nextIdleKey = InspectorIdleKey(mon);
        if (nextIdleKey == inspectorIdleKey)
            return;

        inspectorIdleKey = nextIdleKey;
        inspectorIdleFrame = 0;
        inspectorIdleTimer = 0f;
        inspectorIdleFrames = null;
        inspectorIdleFps = 8f;

        if (inspectorPortraitImage == null)
            return;

        if (mon == null)
        {
            inspectorPortraitImage.enabled = false;
            ApplyInspectorPortraitScale(1f);
            return;
        }

        // Match battle: prefer the form-aware idle clip so evolved bodies animate
        // with their evolved frames, and honor the per-species visualScaleMultiplier
        // so framing-heavy sprites (e.g. Nullbyte) read at a consistent size.
        float scale = 1f;
        BattleAnimationProfile profile = ResolveInspectorProfile(mon);
        if (profile != null)
        {
            if (profile.visualScaleMultiplier > 0f)
                scale = profile.visualScaleMultiplier;
            if (profile.idle != null && profile.idle.HasFrames)
            {
                inspectorIdleFrames = profile.idle.frames;
                inspectorIdleFps = Mathf.Max(1f, profile.idle.fps);
            }
        }

        Sprite first = inspectorIdleFrames != null && inspectorIdleFrames.Length > 0 && inspectorIdleFrames[0] != null
            ? inspectorIdleFrames[0]
            : ResolvePayloadSprite(mon);
        inspectorPortraitImage.sprite = first;
        inspectorPortraitImage.enabled = first != null;
        ApplyInspectorPortraitScale(scale);
    }

    private static string InspectorIdleKey(AlgoMonInstance mon)
    {
        if (mon == null)
            return string.Empty;

        mon.EnsurePersistentRuntimeState();
        return $"{mon.instanceId}:{mon.SpeciesCodeName}:{mon.FormName}";
    }

    private BattleAnimationProfile ResolveInspectorProfile(AlgoMonInstance mon)
    {
        if (mon == null)
            return null;

        string code = mon.SpeciesCodeName;
        string form = mon.IsEvolvedForm ? "Evolved" : "Base";
        BattleAnimationProfile loadedProfile = BattleAnimationProfileLoader.TryLoadProfile(code, form);
        if (loadedProfile != null)
            return loadedProfile;

        return mon.data != null ? mon.data.battleAnimationProfile : null;
    }

    private void ApplyInspectorPortraitScale(float scale)
    {
        if (inspectorPortraitImage == null)
            return;
        inspectorPortraitImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private void UpdateRadarLabelText(int[] stats)
    {
        if (inspectorRadarLabels == null)
            return;
        for (int i = 0; i < inspectorRadarLabels.Length; i++)
        {
            if (inspectorRadarLabels[i] == null)
                continue;
            inspectorRadarLabels[i].text = stats != null ? $"{StatAxisLabels[i]} {stats[i]}" : StatAxisLabels[i];
        }
    }

    private void PositionRadarLabels()
    {
        if (inspectorRadarLabels == null || inspectorRadar == null || inspectorRadarRoot == null)
            return;
        Rect r = inspectorRadarRoot.rect;
        float radius = Mathf.Min(r.width, r.height) * 0.5f * inspectorRadar.FillScale;
        if (radius <= 1f)
            return;
        for (int i = 0; i < inspectorRadarLabels.Length; i++)
        {
            if (inspectorRadarLabels[i] == null)
                continue;
            Vector2 dir = inspectorRadar.AxisDirection(i);
            inspectorRadarLabels[i].rectTransform.anchoredPosition = dir * (radius + 22f);
        }
    }

    private void TickInspectorIdle()
    {
        if (!inSectionView || showingGeneLabPanel)
            return;
        if (payloadGridRoot == null || !payloadGridRoot.gameObject.activeInHierarchy)
            return;

        PositionRadarLabels();

        if (inspectorPortraitImage == null || inspectorIdleFrames == null || inspectorIdleFrames.Length < 2)
            return;

        inspectorIdleTimer += Time.unscaledDeltaTime;
        float secondsPerFrame = 1f / inspectorIdleFps;
        if (inspectorIdleTimer >= secondsPerFrame)
        {
            inspectorIdleTimer -= secondsPerFrame;
            inspectorIdleFrame = (inspectorIdleFrame + 1) % inspectorIdleFrames.Length;
            Sprite frame = inspectorIdleFrames[inspectorIdleFrame];
            if (frame != null)
                inspectorPortraitImage.sprite = frame;
        }
    }

    private void OnPayloadCellClicked(int cellIndex)
    {
        if (payloadCellPayloadIndices == null || cellIndex < 0 || cellIndex >= payloadCellPayloadIndices.Length)
            return;

        int payloadIndex = payloadCellPayloadIndices[cellIndex];
        if (payloadIndex < 0)
            return;

        if (selectedPayloadIndex != payloadIndex)
        {
            payloadSkillMessage = string.Empty;
            pendingPayloadSkillReplacement = null;
        }

        selectedPayloadIndex = payloadIndex;
        manager = manager != null ? manager : GameManager.EnsureInstance();
        RenderPayloadGrid(manager);
    }

    private void ConfigurePayloadCellHover(Button button, int cellIndex)
    {
        if (button == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => SetPayloadCellHover(cellIndex, true));
        trigger.triggers.Add(enter);

        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ =>
        {
            SetPayloadCellHover(cellIndex, true);
            OnPayloadCellClicked(cellIndex);
        });
        trigger.triggers.Add(down);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => SetPayloadCellHover(cellIndex, false));
        trigger.triggers.Add(exit);
    }

    private void SetPayloadCellHover(int cellIndex, bool hovered)
    {
        if (payloadCellPayloadIndices == null)
            return;
        if (cellIndex < 0 || cellIndex >= payloadCellPayloadIndices.Length)
            return;

        if (hovered)
            hoveredPayloadCellIndex = cellIndex;
        else if (hoveredPayloadCellIndex == cellIndex)
            hoveredPayloadCellIndex = -1;

        bool show = hovered && payloadCellPayloadIndices[cellIndex] >= 0;
        if (payloadCellFrames != null && cellIndex < payloadCellFrames.Length && payloadCellFrames[cellIndex] != null)
        {
            payloadCellFrames[cellIndex].rectTransform.localScale = show
                ? new Vector3(1.035f, 1.035f, 1f)
                : Vector3.one;
        }
    }

    private void ShowPayloadGrid(bool show)
    {
        if (payloadGridRoot != null)
            payloadGridRoot.gameObject.SetActive(show);
        if (inspectorViewSquadButton != null)
            inspectorViewSquadButton.gameObject.SetActive(show);
    }

    private void ShowGeneLabPanel(bool show)
    {
        if (geneLabPanelRoot != null)
            geneLabPanelRoot.gameObject.SetActive(show);
    }

    private void ShowGeneLabRouteSelection(bool show)
    {
        if (geneLabRouteSelectionRoot != null)
            geneLabRouteSelectionRoot.gameObject.SetActive(show);
    }

    private void ShowGeneLabBench(bool show)
    {
        if (geneLabBenchRoot != null)
            geneLabBenchRoot.gameObject.SetActive(show);
    }

    private void ShowExitPanelRoot(bool show)
    {
        if (exitPanelRoot != null)
            exitPanelRoot.gameObject.SetActive(show);
    }

    private void ShowSettingsPanelRoot(bool show)
    {
        if (settingsPanelRoot != null)
            settingsPanelRoot.gameObject.SetActive(show);
    }

    private void ToggleTerminalZoomMode()
    {
        terminalZoomModeEnabled = !terminalZoomModeEnabled;
        PlayerPrefs.SetInt(TerminalZoomPlayerPrefsKey, terminalZoomModeEnabled ? 1 : 0);
        PlayerPrefs.Save();
        AudioManager.Instance?.PlayUiSfx(terminalZoomModeEnabled ? UiSfx.ZoomEnable : UiSfx.ZoomDisable);
        ApplyTerminalZoomMode();
        RenderSettingsPanel();
        SetModule(
            "SETTINGS",
            "DISPLAY:",
            terminalZoomModeEnabled ? "TERMINAL ZOOM ENABLED." : "TERMINAL ZOOM DISABLED.",
            "Toggle terminal zoom when you want a closer UI pass.");
    }

    private void ApplyTerminalZoomMode()
    {
        // Keyboard ambience follows the typing character: it animates when zoom is
        // off, and is hidden in zoom mode. Drive the loop before any early-out below.
        AudioManager.Instance?.SetKeyboardLoopActive(!terminalZoomModeEnabled);

        Transform zoomTarget = sourceLayoutVisual != null
            ? sourceLayoutVisual
            : FindSourceLayoutTrialVisual();
        if (zoomTarget == null)
            return;

        sourceLayoutVisual = zoomTarget;
        RectTransform zoomRect = zoomTarget as RectTransform;
        if (zoomRect == null)
        {
            zoomTarget.localScale = terminalBaseLocalScale;
            return;
        }

        CaptureTerminalBaseRect(zoomRect);
        EnsureTerminalZoomBlackout(zoomRect);

        if (!terminalZoomModeEnabled)
        {
            SetTerminalZoomBlackoutVisible(false, zoomRect);
            RestoreTerminalBaseRect(zoomRect);
            terminalZoomAppliedScale = 1f;
            return;
        }

        SetTerminalZoomBlackoutVisible(true, zoomRect);
        RectTransform parentRect = zoomRect.parent as RectTransform;
        Vector2 parentSize = parentRect != null
            ? parentRect.rect.size
            : new Vector2(Screen.width, Screen.height);
        Bounds contentBounds = CalculateTerminalZoomContentBounds(zoomRect);
        Vector2 contentSize = new Vector2(
            Mathf.Max(1f, contentBounds.size.x),
            Mathf.Max(1f, contentBounds.size.y));

        float fitScale = Mathf.Min(parentSize.x / contentSize.x, parentSize.y / contentSize.y) * TerminalZoomFitPadding;
        if (float.IsNaN(fitScale) || float.IsInfinity(fitScale) || fitScale <= 0f)
            fitScale = 1f;

        terminalZoomAppliedScale = fitScale;
        float xSign = terminalBaseLocalScale.x < 0f ? -1f : 1f;
        float ySign = terminalBaseLocalScale.y < 0f ? -1f : 1f;
        zoomRect.anchorMin = new Vector2(0.5f, 0.5f);
        zoomRect.anchorMax = new Vector2(0.5f, 0.5f);
        zoomRect.pivot = new Vector2(0.5f, 0.5f);
        zoomRect.sizeDelta = terminalBaseRectSize;
        Vector2 boundsCenter = contentBounds.center;
        zoomRect.anchoredPosition = -boundsCenter * fitScale;
        zoomRect.localScale = new Vector3(
            xSign * fitScale,
            ySign * fitScale,
            terminalBaseLocalScale.z);
    }

    private void CaptureTerminalBaseRect(RectTransform zoomRect)
    {
        if (terminalBaseRectCaptured || zoomRect == null)
            return;

        terminalBaseAnchorMin = zoomRect.anchorMin;
        terminalBaseAnchorMax = zoomRect.anchorMax;
        terminalBaseAnchoredPosition = zoomRect.anchoredPosition;
        terminalBaseSizeDelta = zoomRect.sizeDelta;
        terminalBaseOffsetMin = zoomRect.offsetMin;
        terminalBaseOffsetMax = zoomRect.offsetMax;
        terminalBasePivot = zoomRect.pivot;
        terminalBaseRectSize = zoomRect.rect.size;
        terminalBaseLocalScale = zoomRect.localScale;
        terminalBaseSiblingIndex = zoomRect.GetSiblingIndex();
        terminalBaseRectCaptured = true;
    }

    private void RestoreTerminalBaseRect(RectTransform zoomRect)
    {
        if (!terminalBaseRectCaptured || zoomRect == null)
            return;

        zoomRect.anchorMin = terminalBaseAnchorMin;
        zoomRect.anchorMax = terminalBaseAnchorMax;
        zoomRect.pivot = terminalBasePivot;
        zoomRect.anchoredPosition = terminalBaseAnchoredPosition;
        zoomRect.sizeDelta = terminalBaseSizeDelta;
        zoomRect.offsetMin = terminalBaseOffsetMin;
        zoomRect.offsetMax = terminalBaseOffsetMax;
        zoomRect.localScale = terminalBaseLocalScale;
        if (terminalBaseSiblingIndex >= 0)
            zoomRect.SetSiblingIndex(Mathf.Clamp(terminalBaseSiblingIndex, 0, zoomRect.parent.childCount - 1));
    }

    private Bounds CalculateTerminalZoomContentBounds(RectTransform zoomRect)
    {
        RectTransform shell = zoomRect != null
            ? zoomRect.Find("Trial_MainMenuOuterShell") as RectTransform
            : null;
        if (shell != null)
            return CalculateRectBoundsInRoot(zoomRect, shell);

        Bounds combined = new Bounds(Vector3.zero, new Vector3(
            Mathf.Max(1f, terminalBaseRectSize.x),
            Mathf.Max(1f, terminalBaseRectSize.y),
            0f));
        bool hasBounds = false;
        if (zoomRect == null)
            return combined;

        RectTransform[] rects = zoomRect.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect == zoomRect || !rect.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = CalculateRectBoundsInRoot(zoomRect, rect);
            if (!hasBounds)
            {
                combined = bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(bounds.min);
                combined.Encapsulate(bounds.max);
            }
        }

        return hasBounds ? combined : combined;
    }

    private static Bounds CalculateRectBoundsInRoot(RectTransform root, RectTransform target)
    {
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 point = toRoot.MultiplyPoint3x4(corners[i]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
        return bounds;
    }

    private void EnsureTerminalZoomBlackout(RectTransform zoomRect)
    {
        RectTransform parentRect = zoomRect != null ? zoomRect.parent as RectTransform : null;
        if (parentRect == null)
            return;

        if (terminalZoomBlackoutRoot != null && terminalZoomBlackoutRoot.parent != parentRect)
            terminalZoomBlackoutRoot.SetParent(parentRect, false);

        if (terminalZoomBlackoutRoot == null)
        {
            terminalZoomBlackoutRoot = CreateRect("TerminalZoomBlackout", parentRect);
            terminalZoomBlackoutImage = terminalZoomBlackoutRoot.gameObject.AddComponent<Image>();
            terminalZoomBlackoutImage.raycastTarget = false;
        }

        terminalZoomBlackoutRoot.anchorMin = Vector2.zero;
        terminalZoomBlackoutRoot.anchorMax = Vector2.one;
        terminalZoomBlackoutRoot.offsetMin = Vector2.zero;
        terminalZoomBlackoutRoot.offsetMax = Vector2.zero;
        if (terminalZoomBlackoutImage != null)
            terminalZoomBlackoutImage.color = Color.black;
    }

    private void SetTerminalZoomBlackoutVisible(bool visible, RectTransform zoomRect)
    {
        if (terminalZoomBlackoutRoot == null)
            return;

        terminalZoomBlackoutRoot.gameObject.SetActive(visible);
        if (!visible || zoomRect == null)
            return;

        terminalZoomBlackoutRoot.SetAsLastSibling();
        zoomRect.SetAsLastSibling();
    }

    private void RenderSettingsPanel()
    {
        if (terminalZoomToggleImage != null)
        {
            terminalZoomToggleImage.sprite = ResolveTerminalToggleSprite(terminalZoomModeEnabled);
            terminalZoomToggleImage.color = terminalZoomModeEnabled
                ? new Color(0.66f, 1f, 0.88f, 1f)
                : new Color(0.58f, 0.76f, 0.80f, 0.94f);
        }

        if (terminalZoomStatusText != null)
        {
            terminalZoomStatusText.text = terminalZoomModeEnabled
                ? $"ON  // FIT {Mathf.RoundToInt(terminalZoomAppliedScale * 100f)}%"
                : "OFF // SCALE 100%";
            terminalZoomStatusText.color = terminalZoomModeEnabled
                ? new Color(0.70f, 1f, 0.90f, 1f)
                : new Color(0.64f, 0.82f, 0.86f, 0.92f);
        }

        AudioManager audio = AudioManager.Instance;
        if (musicVolumeSlider != null)
        {
            float musicVol = audio != null ? audio.MusicVolume : musicVolumeSlider.value;
            musicVolumeSlider.SetValueWithoutNotify(musicVol);
            if (musicVolumeValueText != null)
                musicVolumeValueText.text = Mathf.RoundToInt(musicVol * 100f) + "%";
        }
        if (sfxVolumeSlider != null)
        {
            float sfxVol = audio != null ? audio.SfxVolume : sfxVolumeSlider.value;
            sfxVolumeSlider.SetValueWithoutNotify(sfxVol);
            if (sfxVolumeValueText != null)
                sfxVolumeValueText.text = Mathf.RoundToInt(sfxVol * 100f) + "%";
        }
        if (menuTrackNameText != null)
        {
            menuTrackNameText.text = audio != null && audio.MenuTrackCount > 0
                ? audio.GetMenuTrackName(audio.SelectedMenuTrackIndex).ToUpperInvariant()
                : "NO TRACKS";
        }
    }

    private void RenderExitPanel()
    {
        if (exitPanelStatusText != null)
        {
            exitPanelStatusText.text =
                "SESSION CONTROL\n" +
                "RETURN keeps the terminal open.\n" +
                "QUIT closes the current play session.";
        }

        if (exitReturnButton != null)
            exitReturnButton.interactable = true;
        if (exitConfirmButton != null)
            exitConfirmButton.interactable = true;
    }

    private void RenderGeneLabFusionDisplays(AlgoMonInstance target, int targetIndex, AlgoMonInstance material, int materialIndex)
    {
        SetGeneLabFusionDisplay(0, target, targetIndex);
        SetGeneLabFusionDisplay(1, material, materialIndex);
    }

    private void RenderGeneLabFusionTalentBars(AlgoMonInstance target, AlgoMonInstance material)
    {
        if (geneLabFusionTalentRoot == null)
            return;

        if (geneLabFusionTalentCaptionText != null)
        {
            geneLabFusionTalentCaptionText.text = material != null
                ? "TALENT MERGE // UNIT 1 + UNIT 2 -> RESULT"
                : "TALENT MERGE // SELECT UNIT 2";
        }

        for (int i = 0; i < StatAxisLabels.Length; i++)
        {
            int targetValue = TalentValueAt(target, i);
            int materialValue = TalentValueAt(material, i);
            int projectedValue = target != null
                ? (material != null ? Mathf.Max(targetValue, materialValue) : targetValue)
                : 0;

            SetGeneLabTalentFill(geneLabFusionTargetTalentFills, i, target != null ? targetValue : 0, GeneLabTargetTalentColor);
            SetGeneLabTalentFill(geneLabFusionMaterialTalentFills, i, material != null ? materialValue : 0, GeneLabMaterialTalentColor);
            SetGeneLabTalentFill(geneLabFusionProjectedTalentFills, i, target != null ? projectedValue : 0, GeneLabProjectedTalentColor);

            SetGeneLabTalentValue(geneLabFusionTargetTalentValues, i, target != null ? targetValue.ToString("000") : "---");
            SetGeneLabTalentValue(geneLabFusionMaterialTalentValues, i, material != null ? materialValue.ToString("000") : "---");
            SetGeneLabTalentValue(geneLabFusionProjectedTalentValues, i, target != null ? projectedValue.ToString("000") : "---");
        }
    }

    private static void SetGeneLabTalentFill(Image[] fills, int index, int value, Color color)
    {
        if (fills == null || index < 0 || index >= fills.Length || fills[index] == null)
            return;

        fills[index].fillAmount = Mathf.Clamp01(value / 255f);
        fills[index].color = color;
    }

    private static void SetGeneLabTalentValue(Text[] values, int index, string text)
    {
        if (values == null || index < 0 || index >= values.Length || values[index] == null)
            return;

        values[index].text = text;
    }

    private void SetGeneLabFusionDisplay(int slot, AlgoMonInstance mon, int payloadIndex)
    {
        if (geneLabFusionNameTexts == null || slot < 0 || slot >= geneLabFusionNameTexts.Length)
            return;

        if (mon == null)
        {
            if (geneLabFusionNameTexts[slot] != null)
                geneLabFusionNameTexts[slot].text = slot == 0
                    ? "NO UNIT 1\nSELECT LEFT"
                    : "NO UNIT 2\nSELECT LEFT";
            if (geneLabFusionMetaTexts != null && geneLabFusionMetaTexts[slot] != null)
                geneLabFusionMetaTexts[slot].text = "--";
            if (geneLabFusionPortraitImages != null && geneLabFusionPortraitImages[slot] != null)
            {
                geneLabFusionPortraitImages[slot].sprite = null;
                geneLabFusionPortraitImages[slot].enabled = false;
            }
            ClearGeneLabFusionIdle(slot);
            return;
        }

        mon.EnsurePersistentRuntimeState();
        if (geneLabFusionNameTexts[slot] != null)
        {
            geneLabFusionNameTexts[slot].text =
                $"{DisplayNameFor(mon).ToUpperInvariant()}\n" +
                $"L{mon.level:00}  {FormLabel(mon)}";
        }
        if (geneLabFusionMetaTexts != null && geneLabFusionMetaTexts[slot] != null)
        {
            geneLabFusionMetaTexts[slot].text = $"FUSED {mon.FusionProgressText}";
        }

        SetGeneLabFusionIdle(slot, mon);
    }

    private void SetGeneLabFusionIdle(int slot, AlgoMonInstance mon)
    {
        if (geneLabFusionPortraitImages == null || slot < 0 || slot >= geneLabFusionPortraitImages.Length)
            return;

        Image portrait = geneLabFusionPortraitImages[slot];
        if (portrait == null)
            return;

        string key = mon != null
            ? $"{mon.instanceId}:{mon.SpeciesCodeName}:{FormLabel(mon)}"
            : string.Empty;
        if (geneLabFusionIdleKeys != null &&
            slot < geneLabFusionIdleKeys.Length &&
            string.Equals(geneLabFusionIdleKeys[slot], key, StringComparison.Ordinal))
        {
            return;
        }

        if (geneLabFusionIdleKeys != null && slot < geneLabFusionIdleKeys.Length)
            geneLabFusionIdleKeys[slot] = key;
        if (geneLabFusionIdleTimers != null && slot < geneLabFusionIdleTimers.Length)
            geneLabFusionIdleTimers[slot] = 0f;
        if (geneLabFusionIdleFrameIndices != null && slot < geneLabFusionIdleFrameIndices.Length)
            geneLabFusionIdleFrameIndices[slot] = 0;

        BattleAnimationProfile profile = ResolveInspectorProfile(mon);
        Sprite[] frames = profile != null && profile.idle != null && profile.idle.HasFrames
            ? profile.idle.frames
            : null;
        if (geneLabFusionIdleFrames != null && slot < geneLabFusionIdleFrames.Length)
            geneLabFusionIdleFrames[slot] = frames ?? Array.Empty<Sprite>();
        if (geneLabFusionIdleFps != null && slot < geneLabFusionIdleFps.Length)
            geneLabFusionIdleFps[slot] = profile != null && profile.idle != null ? Mathf.Max(1f, profile.idle.fps) : 0f;

        Sprite first = frames != null && frames.Length > 0 ? frames[0] : ResolvePayloadSprite(mon);
        portrait.sprite = first;
        portrait.enabled = first != null;
        portrait.color = Color.white;
    }

    private void ClearGeneLabFusionIdle(int slot)
    {
        if (geneLabFusionIdleKeys != null && slot >= 0 && slot < geneLabFusionIdleKeys.Length)
            geneLabFusionIdleKeys[slot] = string.Empty;
        if (geneLabFusionIdleFrames != null && slot >= 0 && slot < geneLabFusionIdleFrames.Length)
            geneLabFusionIdleFrames[slot] = Array.Empty<Sprite>();
        if (geneLabFusionIdleTimers != null && slot >= 0 && slot < geneLabFusionIdleTimers.Length)
            geneLabFusionIdleTimers[slot] = 0f;
        if (geneLabFusionIdleFrameIndices != null && slot >= 0 && slot < geneLabFusionIdleFrameIndices.Length)
            geneLabFusionIdleFrameIndices[slot] = 0;
    }

    private void TickGeneLabFusionIdle()
    {
        if (!inSectionView || !showingGeneLabPanel || geneLabRouteSelectionMode)
            return;
        if (geneLabFusionPortraitImages == null || geneLabFusionIdleFrames == null)
            return;

        int count = Mathf.Min(geneLabFusionPortraitImages.Length, geneLabFusionIdleFrames.Length);
        for (int i = 0; i < count; i++)
        {
            Image portrait = geneLabFusionPortraitImages[i];
            Sprite[] frames = geneLabFusionIdleFrames[i];
            if (portrait == null || frames == null || frames.Length < 2)
                continue;

            float fps = geneLabFusionIdleFps != null && i < geneLabFusionIdleFps.Length
                ? geneLabFusionIdleFps[i]
                : BossRouteFallbackIdleFps;
            float secondsPerFrame = 1f / Mathf.Max(1f, fps);
            geneLabFusionIdleTimers[i] += Time.unscaledDeltaTime;
            while (geneLabFusionIdleTimers[i] >= secondsPerFrame)
            {
                geneLabFusionIdleTimers[i] -= secondsPerFrame;
                geneLabFusionIdleFrameIndices[i] = (geneLabFusionIdleFrameIndices[i] + 1) % frames.Length;
            }

            portrait.sprite = frames[geneLabFusionIdleFrameIndices[i]];
            portrait.enabled = portrait.sprite != null;
        }
    }

    private void RefreshGeneLabMiniPayload(GameManager targetManager, string speciesCode)
    {
        if (geneLabMiniPayloadButtons == null)
            return;

        int total = CountPayloadForSpecies(targetManager, speciesCode);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)GeneLabMiniPayloadCellCount));
        geneLabPayloadPage = Mathf.Clamp(geneLabPayloadPage, 0, pageCount - 1);
        int pageStart = geneLabPayloadPage * GeneLabMiniPayloadCellCount;

        for (int cell = 0; cell < GeneLabMiniPayloadCellCount; cell++)
        {
            int payloadIndex = PayloadIndexForSpeciesAtOrder(targetManager, speciesCode, pageStart + cell);
            AlgoMonInstance mon = PayloadAt(targetManager, payloadIndex);
            bool selected = payloadIndex >= 0 && payloadIndex == selectedPayloadIndex;
            bool second = payloadIndex >= 0 && payloadIndex == geneLabFusionSecondIndex;
            geneLabMiniPayloadIndices[cell] = payloadIndex;

            if (geneLabMiniPayloadFrames[cell] != null)
            {
                geneLabMiniPayloadFrames[cell].sprite = ResolvePayloadSlotSprite(targetManager, mon, selected || second);
                geneLabMiniPayloadFrames[cell].color = mon == null
                    ? new Color(1f, 1f, 1f, 0.24f)
                    : selected
                        ? new Color(0.72f, 1f, 0.98f, 1f)
                        : second
                            ? new Color(1f, 0.56f, 0.96f, 1f)
                            : Color.white;
            }

            if (geneLabMiniPayloadSprites[cell] != null)
            {
                Sprite sprite = ResolvePayloadSprite(mon);
                geneLabMiniPayloadSprites[cell].sprite = sprite;
                geneLabMiniPayloadSprites[cell].enabled = sprite != null;
            }

            if (geneLabMiniPayloadLabels[cell] != null)
            {
                string role = selected ? "U1 " : second ? "U2 " : string.Empty;
                geneLabMiniPayloadLabels[cell].text = mon != null
                    ? $"{role}{DisplayNameFor(mon).ToUpperInvariant()}\nF{mon.FusionProgressText}"
                    : string.Empty;
            }

            if (geneLabMiniPayloadButtons[cell] != null)
                geneLabMiniPayloadButtons[cell].interactable = payloadIndex >= 0;
        }

        if (geneLabMiniPayloadPageLabel != null)
            geneLabMiniPayloadPageLabel.text = $"PAGE {geneLabPayloadPage + 1}/{pageCount}";
        if (geneLabMiniPayloadPrevButton != null)
            geneLabMiniPayloadPrevButton.interactable = geneLabPayloadPage > 0;
        if (geneLabMiniPayloadNextButton != null)
            geneLabMiniPayloadNextButton.interactable = geneLabPayloadPage < pageCount - 1;
    }

    private void ChangeGeneLabMiniPayloadPage(int delta)
    {
        geneLabPayloadPage += delta;
        RefreshGeneLabModule();
    }

    private void OnGeneLabMiniPayloadClicked(int cellIndex)
    {
        if (geneLabMiniPayloadIndices == null || cellIndex < 0 || cellIndex >= geneLabMiniPayloadIndices.Length)
            return;

        manager = manager != null ? manager : GameManager.EnsureInstance();
        int payloadIndex = geneLabMiniPayloadIndices[cellIndex];
        if (payloadIndex < 0)
            return;

        string speciesCode = SelectedGeneLabSpeciesCode(manager);
        if (payloadIndex == selectedPayloadIndex)
        {
            selectedPayloadIndex = -1;
            geneLabFusionSecondIndex = -1;
            geneLabActionMessage = "UNIT 1 cleared. Select UNIT 1 to start a new fusion pair.";
        }
        else if (selectedPayloadIndex < 0 ||
            !PayloadMatchesSpecies(PayloadAt(manager, selectedPayloadIndex), speciesCode))
        {
            selectedPayloadIndex = payloadIndex;
            if (geneLabFusionSecondIndex == selectedPayloadIndex)
                geneLabFusionSecondIndex = -1;
            geneLabActionMessage = "UNIT 1 selected. Select UNIT 2 from the same gene pool.";
        }
        else if (payloadIndex == geneLabFusionSecondIndex)
        {
            geneLabFusionSecondIndex = -1;
            geneLabActionMessage = "UNIT 2 cleared. Select another UNIT 2.";
        }
        else
        {
            geneLabFusionSecondIndex = payloadIndex;
            geneLabActionMessage = "UNIT 2 selected. Fusion pair is ready if both units are valid.";
        }

        FocusGeneLabMiniPayloadPageOn(manager, speciesCode, payloadIndex);
        RefreshGeneLabModule();
    }

    private void SetPayloadPanelControls(bool showPrevious, bool showNext, bool showFuse, bool showEvolve)
    {
        SetPanelButtonState(payloadPreviousButton, showPrevious, showPrevious);
        SetPanelButtonState(payloadNextButton, showNext, showNext);
        SetPanelButtonState(geneLabFuseButton, showFuse, showFuse);
        SetPanelButtonState(geneLabEvolveButton, showEvolve, showEvolve);
    }

    private static void SetPanelButtonState(Button button, bool visible, bool interactable)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(visible);
        button.interactable = interactable;
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
                ? $"{DisplayNameFor(mon).ToUpperInvariant()} {FormLabel(mon)} F{mon.FusionProgressText}"
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

        mon.EnsurePersistentRuntimeState();
        AlgoMonData data = mon.data;
        string codeName = data != null && !string.IsNullOrWhiteSpace(data.codeName) ? data.codeName.Trim() : DisplayNameFor(mon);
        string element = data != null ? data.elementType.ToString().ToUpperInvariant() : "NORMAL";
        string subroutine = data != null && data.subroutine != null && !string.IsNullOrWhiteSpace(data.subroutine.subroutineName)
            ? data.subroutine.subroutineName.Trim()
            : "NONE";

        var builder = new StringBuilder();
        builder.AppendLine($"{DisplayNameFor(mon).ToUpperInvariant()}");
        builder.AppendLine($"CODE: {codeName.ToUpperInvariant()}  ELEMENT: {element}");
        builder.AppendLine($"FORM: {FormLabel(mon)}  FUSION: {mon.FusionProgressText}");
        builder.AppendLine($"LV {mon.level:00}/{AlgoMonInstance.MAX_LEVEL}  EXP {mon.exp}/{mon.expToNextLevel}");
        builder.AppendLine($"DATA QUALITY: {EncounterReward.FormatQuality(mon.dataQuality)}");
        builder.AppendLine($"SUBROUTINE: {subroutine.ToUpperInvariant()}");
        builder.AppendLine();
        builder.AppendLine("ACTIVE STATS");
        builder.AppendLine($"BAT {mon.Battery:000}  SPD {mon.ClockSpeed:000}");
        builder.AppendLine($"CPU {mon.ComputingPower:000}  TP  {mon.Throughput:000}");
        builder.AppendLine($"FW  {mon.Firewall:000}  ENC {mon.Encryption:000}");
        builder.AppendLine();
        builder.AppendLine("TALENTS / HARDWARE IV");
        builder.AppendLine($"BAT {mon.iv_Battery:000}  SPD {mon.iv_ClockSpeed:000}");
        builder.AppendLine($"CPU {mon.iv_ComputingPower:000}  TP  {mon.iv_Throughput:000}");
        builder.AppendLine($"FW  {mon.iv_Firewall:000}  ENC {mon.iv_Encryption:000}");
        builder.AppendLine();
        builder.AppendLine("SKILLS");
        AppendPayloadSkills(builder, mon);

        AppendSubroutineSection(builder, data);

        if (data != null && !string.IsNullOrWhiteSpace(data.description))
        {
            builder.AppendLine();
            builder.AppendLine("PROFILE");
            builder.Append(data.description.Trim());
        }

        return builder.ToString();
    }

    private static void AppendSubroutineSection(StringBuilder builder, AlgoMonData data)
    {
        SubroutineData sub = data != null ? data.subroutine : null;
        if (sub == null || string.IsNullOrWhiteSpace(sub.subroutineName))
            return;

        builder.AppendLine();
        builder.AppendLine("SUBROUTINE / PASSIVE");
        builder.AppendLine($"{sub.subroutineName.Trim().ToUpperInvariant()}  ::  {sub.TriggerLabel}");
        builder.Append(!string.IsNullOrWhiteSpace(sub.description)
            ? sub.description.Trim()
            : "Hardwired passive. Activates automatically in battle.");
    }

    private static string BuildGeneLabPreview(GameManager targetManager, int unit1Index, int unit2Index, string status)
    {
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
            return "No base-form bodies in payload.\nClear the selected boss route to add the matching initial form.";

        AlgoMonInstance selected = PayloadAt(targetManager, unit1Index);
        if (selected == null)
            return "UNIT 1: NONE\nUNIT 2: NONE\nClick a payload unit to begin pairing.";

        selected.EnsurePersistentRuntimeState();
        AlgoMonInstance unit2 = PayloadAt(targetManager, unit2Index);
        string unit2Line = unit2 != null
            ? $"UNIT 2: #{unit2Index + 1:00} {DisplayNameFor(unit2).ToUpperInvariant()} {FormLabel(unit2)}"
            : "UNIT 2: NONE";
        string evolveLine = selected.CanEvolve
            ? "EVOLUTION: READY"
            : selected.IsEvolvedForm
                ? "EVOLUTION: COMPLETE"
                : $"EVOLUTION: NEED {selected.RemainingFusionCopies} MORE";

        var builder = new StringBuilder();
        builder.AppendLine($"UNIT 1: #{unit1Index + 1:00} {DisplayNameFor(selected).ToUpperInvariant()} {FormLabel(selected)}");
        builder.AppendLine(unit2Line);
        builder.AppendLine($"FUSION: {selected.FusionProgressText}  {evolveLine}");
        if (!string.IsNullOrWhiteSpace(status))
            builder.Append($"STATUS: {status}");
        return builder.ToString();
    }

    private string BuildGeneLabModuleDetail(GameManager targetManager)
    {
        string speciesCode = SelectedGeneLabSpeciesCode(targetManager);
        AlgoMonInstance selected = PayloadAt(targetManager, selectedPayloadIndex);
        if (!PayloadMatchesSpecies(selected, speciesCode))
            selected = null;

        var builder = new StringBuilder();
        builder.AppendLine($"CATEGORY: {speciesCode}");
        if (selected != null)
        {
            builder.Append(BuildGeneLabPreview(targetManager, selectedPayloadIndex, geneLabFusionSecondIndex, geneLabActionMessage));
        }
        else
        {
            builder.AppendLine("UNIT 1: NONE");
            builder.AppendLine("UNIT 2: NONE");
            builder.AppendLine("FUSION: WAITING FOR MATCHING BASE FORM");
        }

        if (!string.IsNullOrWhiteSpace(geneLabSkillMessage))
        {
            builder.AppendLine();
            builder.Append($"SKILL: {geneLabSkillMessage.Trim()}");
        }

        return builder.ToString();
    }

    private static string BuildGeneLabRouteSelectDetail(GameManager targetManager)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SELECT ONE BOSS STRAIN TO OPEN ITS FUSION WORKBENCH.");
        builder.Append(targetManager == null
            ? "PAYLOAD DATA UNAVAILABLE."
            : "EACH ROUTE SHOWS BASE / FUSION / EVOLUTION READY COUNTS.");

        return builder.ToString();
    }

    private void RefreshGeneLabSpeciesButtons(GameManager targetManager, string selectedCode)
    {
        if (geneLabSpeciesButtons == null)
            return;

        bool locked = targetManager != null && targetManager.IsRunActive;
        int count = Mathf.Min(geneLabSpeciesButtons.Length, BossRouteSpecies.Length);
        for (int i = 0; i < count; i++)
        {
            string speciesCode = NormalizeBossRouteCode(BossRouteSpecies[i]);
            bool selected = string.Equals(speciesCode, selectedCode, StringComparison.OrdinalIgnoreCase);
            int speciesUnitCount = CountPayloadForSpecies(targetManager, speciesCode);

            Button button = geneLabSpeciesButtons[i];
            if (button != null)
                button.interactable = !locked;

            Image frame = geneLabSpeciesFrames != null && i < geneLabSpeciesFrames.Length
                ? geneLabSpeciesFrames[i]
                : (button != null ? button.GetComponent<Image>() : null);
            if (frame != null)
            {
                frame.color = selected
                    ? new Color(0.72f, 1f, 0.98f, 1f)
                    : new Color(1f, 1f, 1f, locked ? 0.42f : 0.82f);
            }

            Text label = geneLabSpeciesLabels != null && i < geneLabSpeciesLabels.Length
                ? geneLabSpeciesLabels[i]
                : null;
            if (label != null)
            {
                label.text = speciesCode;
                label.color = selected
                    ? new Color(0.98f, 1f, 1f, 1f)
                    : new Color(0.82f, 1f, 0.96f, locked ? 0.58f : 0.94f);
            }

            Text meta = geneLabSpeciesMetaLabels != null && i < geneLabSpeciesMetaLabels.Length
                ? geneLabSpeciesMetaLabels[i]
                : null;
            if (meta != null)
            {
                meta.text = $"x{speciesUnitCount}";
                meta.color = selected
                    ? new Color(0.45f, 1f, 0.95f, 1f)
                    : new Color(0.55f, 0.95f, 1f, locked ? 0.48f : 0.82f);
            }

            ApplyGeneLabRouteIdleFrame(i, !locked);
        }
    }

    private string BuildGeneLabSpeciesSummary(GameManager targetManager, string speciesCode, AlgoMonInstance selected)
    {
        int totalCount = CountPayloadForSpecies(targetManager, speciesCode);
        int baseCount = CountBasePayloadForSpecies(targetManager, speciesCode);
        int fuseReady = CountFusionReadyForSpecies(targetManager, speciesCode);
        int evolveReady = CountEvolvableForSpecies(targetManager, speciesCode);
        string lockState = targetManager != null && targetManager.IsRunActive ? "LOCKED DURING RUN" : "ONLINE";

        var builder = new StringBuilder();
        builder.AppendLine($"CATEGORY: {speciesCode} PRIME  [{lockState}]");
        builder.AppendLine($"PAYLOAD: {totalCount:00} RECORDS / {baseCount:00} BASE UNITS");
        builder.AppendLine($"READY: FUSE {fuseReady:00} / EVOLVE {evolveReady:00}");
        if (selected != null)
        {
            selected.EnsurePersistentRuntimeState();
            builder.Append($"UNIT 1: {DisplayNameFor(selected).ToUpperInvariant()}  {FormLabel(selected)}  FUSED {selected.FusionProgressText}");
        }
        else
        {
            builder.Append("UNIT 1: NO PAYLOAD RECORD SELECTED");
        }

        return builder.ToString();
    }

    private static string BuildGeneLabDetail(
        GameManager targetManager,
        int selectedIndex,
        int materialIndex,
        string status)
    {
        AlgoMonInstance target = PayloadAt(targetManager, selectedIndex);
        if (target == null)
            return "Select a valid payload record.";

        target.EnsurePersistentRuntimeState();
        var builder = new StringBuilder();
        builder.AppendLine("GENE LAB UNIT 1");
        builder.AppendLine($"{DisplayNameFor(target).ToUpperInvariant()}  {FormLabel(target)}");
        builder.AppendLine($"SPECIES: {target.SpeciesCodeName.ToUpperInvariant()}");
        builder.AppendLine($"FUSED COUNT: {target.FusionProgressText}  QUALITY: {EncounterReward.FormatQuality(target.dataQuality)}");
        builder.AppendLine($"EVOLUTION: {EvolutionStatus(target)}");
        builder.AppendLine();
        builder.AppendLine("CURRENT TALENTS");
        AppendTalentLine(builder, target);

        AlgoMonInstance material = PayloadAt(targetManager, materialIndex);
        if (material != null)
        {
            material.EnsurePersistentRuntimeState();
            builder.AppendLine();
            builder.AppendLine("GENE LAB UNIT 2");
            builder.AppendLine($"{materialIndex + 1:00}// {DisplayNameFor(material).ToUpperInvariant()}  {FormLabel(material)}");
            builder.AppendLine($"FUSION VALUE: +{1 + material.FusionProgress} BASE COPY");
            builder.AppendLine($"LEVEL AFTER FUSION: L{Mathf.Max(target.level, material.level):00}");
            builder.AppendLine("PROJECTED TALENTS");
            AppendProjectedTalentLine(builder, target, material);
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("GENE LAB UNIT 2");
            builder.AppendLine("Select another same-species base form.");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            builder.AppendLine();
            builder.AppendLine("ACTION STATUS");
            builder.Append(status.Trim());
        }

        return builder.ToString();
    }

    private static string BuildGeneLabFusionStatus(
        AlgoMonInstance target,
        AlgoMonInstance material,
        bool canFuse,
        bool canEvolve,
        string fuseBlockReason,
        string status)
    {
        if (target == null)
            return "SELECT UNIT 1 // NO PAYLOAD RECORD SELECTED";

        target.EnsurePersistentRuntimeState();
        string actionStatus = CompactGeneLabActionStatus(status);
        if (!string.IsNullOrEmpty(actionStatus))
            return actionStatus;

        if (canEvolve)
            return $"EVOLVE READY // U1 {DisplayNameFor(target).ToUpperInvariant()} // F{target.FusionProgressText}";

        if (material == null)
            return $"SELECT U2 // NEED {target.RemainingFusionCopies} SAME-SPECIES BASE FUSION(S)";

        material.EnsurePersistentRuntimeState();
        if (!canFuse)
        {
            string reason = string.IsNullOrWhiteSpace(fuseBlockReason)
                ? "PAIR IS NOT READY"
                : CompactFusionBlockReason(fuseBlockReason);
            return $"BLOCKED // {reason.ToUpperInvariant()}";
        }

        int projectedFusion = Mathf.Clamp(
            target.FusionProgress + 1 + material.FusionProgress,
            0,
            AlgoMonInstance.FusionCopiesForEvolution);
        int projectedLevel = Mathf.Max(target.level, material.level);
        int remaining = Mathf.Max(0, AlgoMonInstance.FusionCopiesForEvolution - projectedFusion);
        string unlock = remaining == 0
            ? "EVOLVE READY"
            : $"NEED {remaining}";

        return $"READY // U1 + U2 -> U1 // L{projectedLevel:00} // F{projectedFusion}/{AlgoMonInstance.FusionCopiesForEvolution} // {unlock}";
    }

    private static string CompactFusionBlockReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "Pair is not ready";

        string lower = reason.ToLowerInvariant();
        if (lower.Contains("active squad"))
            return "Remove U1/U2 from squad";
        if (lower.Contains("different"))
            return "Pick two different units";
        if (lower.Contains("base-form") || lower.Contains("base form"))
            return "Base forms only";
        if (lower.Contains("same species"))
            return "Same species only";
        if (lower.Contains("ready to evolve"))
            return "U1 can evolve";

        return reason.Trim();
    }

    private static string CompactGeneLabActionStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return string.Empty;

        string trimmed = status.Trim();
        if (trimmed.IndexOf("fused", StringComparison.OrdinalIgnoreCase) >= 0)
            return "FUSION COMPLETE // U1 UPDATED";
        if (trimmed.IndexOf("evolved", StringComparison.OrdinalIgnoreCase) >= 0)
            return "EVOLUTION COMPLETE";

        bool isActionResult =
            trimmed.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            trimmed.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isActionResult)
            return string.Empty;

        return trimmed.Length > 56
            ? trimmed.Substring(0, 56).Trim().ToUpperInvariant() + "..."
            : trimmed.ToUpperInvariant();
    }

    private string GeneLabSelectionStatus(GameManager targetManager)
    {
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
            return "Gene Lab is waiting for boss base-form records.";
        if (targetManager.IsRunActive)
            return "Gene Lab is locked during active runs.";

        if (selectedPayloadIndex < 0)
            return "Select UNIT 1 from the payload pool.";

        AlgoMonInstance selected = PayloadAt(targetManager, selectedPayloadIndex);
        if (selected == null)
            return "Select a valid UNIT 1 record.";

        selected.EnsurePersistentRuntimeState();
        if (selected.IsEvolvedForm)
            return "UNIT 1 is already evolved.";
        if (targetManager.CanEvolvePayload(selectedPayloadIndex, out _))
            return "Evolution is ready.";

        if (geneLabFusionSecondIndex < 0)
            return "Select UNIT 2 from the same gene pool.";
        if (targetManager.CanFusePayload(selectedPayloadIndex, geneLabFusionSecondIndex, out string reason))
            return "UNIT 1 and UNIT 2 are ready to fuse.";
        return reason;
    }

    private string SelectedGeneLabSpeciesCode(GameManager targetManager)
    {
        string fallback = targetManager != null ? targetManager.SelectedBossSpeciesCodeName : BossRouteSpecies[0];
        if (string.IsNullOrWhiteSpace(selectedGeneLabSpeciesCode))
            selectedGeneLabSpeciesCode = NormalizeBossRouteCode(fallback);
        else
            selectedGeneLabSpeciesCode = NormalizeBossRouteCode(selectedGeneLabSpeciesCode);

        return selectedGeneLabSpeciesCode;
    }

    private int BestGeneLabTargetIndexForSpecies(GameManager targetManager, string speciesCode)
    {
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
            return -1;

        speciesCode = NormalizeBossRouteCode(speciesCode);
        if (PayloadMatchesSpecies(PayloadAt(targetManager, selectedPayloadIndex), speciesCode))
            return selectedPayloadIndex;

        int firstFuseReady = -1;
        int firstStoredBase = -1;
        int firstStoredAny = -1;
        int firstBase = -1;
        int firstAny = -1;
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            if (!PayloadMatchesSpecies(mon, speciesCode))
                continue;

            bool inParty = mon != null && targetManager.IsInParty(mon);
            if (firstAny < 0)
                firstAny = i;
            if (!inParty && firstStoredAny < 0)
                firstStoredAny = i;
            if (mon != null && mon.IsBaseForm && firstBase < 0)
                firstBase = i;
            if (mon != null && mon.IsBaseForm && !inParty && firstStoredBase < 0)
                firstStoredBase = i;
            if (targetManager.CanEvolvePayload(i, out _))
                return i;
            if (firstFuseReady < 0 && targetManager.FirstFusionCandidateIndexFor(i) >= 0)
                firstFuseReady = i;
        }

        if (firstFuseReady >= 0)
            return firstFuseReady;
        if (firstStoredBase >= 0)
            return firstStoredBase;
        if (firstBase >= 0)
            return firstBase;
        if (firstStoredAny >= 0)
            return firstStoredAny;
        return firstAny;
    }

    private static bool PayloadMatchesSpecies(AlgoMonInstance mon, string speciesCode)
    {
        if (mon == null)
            return false;

        string monCode = NormalizeBossRouteCode(mon.SpeciesCodeName);
        return string.Equals(monCode, NormalizeBossRouteCode(speciesCode), StringComparison.OrdinalIgnoreCase);
    }

    private static int CountPayloadForSpecies(GameManager targetManager, string speciesCode)
    {
        if (targetManager == null || targetManager.payload == null)
            return 0;

        int count = 0;
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            if (PayloadMatchesSpecies(targetManager.payload[i], speciesCode))
                count++;
        }

        return count;
    }

    private static int PayloadIndexForSpeciesAtOrder(GameManager targetManager, string speciesCode, int order)
    {
        if (targetManager == null || targetManager.payload == null || order < 0)
            return -1;

        int matchIndex = 0;
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            if (!PayloadMatchesSpecies(targetManager.payload[i], speciesCode))
                continue;

            if (matchIndex == order)
                return i;

            matchIndex++;
        }

        return -1;
    }

    private void FocusGeneLabMiniPayloadPageOn(GameManager targetManager, string speciesCode, int payloadIndex)
    {
        if (targetManager == null || targetManager.payload == null || payloadIndex < 0)
            return;

        int order = 0;
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            if (!PayloadMatchesSpecies(mon, speciesCode))
                continue;

            if (i == payloadIndex)
            {
                geneLabPayloadPage = order / GeneLabMiniPayloadCellCount;
                return;
            }

            order++;
        }
    }

    private static int CountBasePayloadForSpecies(GameManager targetManager, string speciesCode)
    {
        if (targetManager == null || targetManager.payload == null)
            return 0;

        int count = 0;
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            if (PayloadMatchesSpecies(mon, speciesCode) && mon.IsBaseForm)
                count++;
        }

        return count;
    }

    private static int CountFusionReadyForSpecies(GameManager targetManager, string speciesCode)
    {
        if (targetManager == null || targetManager.payload == null)
            return 0;

        int count = 0;
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            if (PayloadMatchesSpecies(targetManager.payload[i], speciesCode) &&
                targetManager.FirstFusionCandidateIndexFor(i) >= 0)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountEvolvableForSpecies(GameManager targetManager, string speciesCode)
    {
        if (targetManager == null || targetManager.payload == null)
            return 0;

        int count = 0;
        for (int i = 0; i < targetManager.payload.Count; i++)
        {
            if (PayloadMatchesSpecies(targetManager.payload[i], speciesCode) &&
                targetManager.CanEvolvePayload(i, out _))
            {
                count++;
            }
        }

        return count;
    }

    private static int KnownSkillCount(AlgoMonInstance mon)
    {
        if (mon == null || mon.knownSkills == null)
            return 0;

        int count = 0;
        for (int i = 0; i < mon.knownSkills.Count; i++)
        {
            if (mon.knownSkills[i] != null)
                count++;
        }

        return count;
    }

    private static LearnsetEntry NextValidLearnsetEntry(LearnsetEntry[] learnset, ref int startIndex)
    {
        if (learnset == null)
            return default(LearnsetEntry);

        while (startIndex < learnset.Length)
        {
            LearnsetEntry entry = learnset[startIndex];
            startIndex++;
            if (entry.skill != null)
                return entry;
        }

        return default(LearnsetEntry);
    }

    private static string SkillLearnState(AlgoMonInstance mon, LearnsetEntry entry)
    {
        if (mon != null && mon.knownSkills != null && mon.knownSkills.Contains(entry.skill))
            return "KNOWN";
        if (mon == null || entry.unlockLevel > mon.level)
            return "LOCKD";
        return KnownSkillCount(mon) >= AlgoMonInstance.MaxSkillSlots ? "FULL " : "READY";
    }

    private static string SkillDisplayName(SkillData skill)
    {
        if (skill == null)
            return "Skill";
        return !string.IsNullOrWhiteSpace(skill.skillName) ? skill.skillName.Trim() : skill.name;
    }

    private AlgoMonInstance SelectedPayloadMon(GameManager targetManager)
    {
        return PayloadAt(targetManager, selectedPayloadIndex);
    }

    private static AlgoMonInstance PayloadAt(GameManager targetManager, int index)
    {
        if (targetManager == null || targetManager.payload == null || index < 0 || index >= targetManager.payload.Count)
            return null;
        return targetManager.payload[index];
    }

    private static string FormLabel(AlgoMonInstance mon)
    {
        if (mon == null)
            return "BASE";

        mon.EnsurePersistentRuntimeState();
        return mon.IsEvolvedForm ? "EVOLVED" : "BASE";
    }

    private static string EvolutionStatus(AlgoMonInstance mon)
    {
        if (mon == null)
            return "UNKNOWN";
        if (mon.IsEvolvedForm)
            return "COMPLETE";
        if (mon.CanEvolve)
            return "READY";
        return $"NEED {mon.RemainingFusionCopies} MORE";
    }

    private static void AppendTalentLine(StringBuilder builder, AlgoMonInstance mon)
    {
        builder.AppendLine($"BAT {mon.iv_Battery:000}  SPD {mon.iv_ClockSpeed:000}");
        builder.AppendLine($"CPU {mon.iv_ComputingPower:000}  TP  {mon.iv_Throughput:000}");
        builder.AppendLine($"FW  {mon.iv_Firewall:000}  ENC {mon.iv_Encryption:000}");
    }

    private static void AppendProjectedTalentLine(StringBuilder builder, AlgoMonInstance target, AlgoMonInstance material)
    {
        builder.AppendLine($"BAT {Mathf.Max(target.iv_Battery, material.iv_Battery):000}  SPD {Mathf.Max(target.iv_ClockSpeed, material.iv_ClockSpeed):000}");
        builder.AppendLine($"CPU {Mathf.Max(target.iv_ComputingPower, material.iv_ComputingPower):000}  TP  {Mathf.Max(target.iv_Throughput, material.iv_Throughput):000}");
        builder.AppendLine($"FW  {Mathf.Max(target.iv_Firewall, material.iv_Firewall):000}  ENC {Mathf.Max(target.iv_Encryption, material.iv_Encryption):000}");
    }

    private static int TalentValueAt(AlgoMonInstance mon, int statIndex)
    {
        if (mon == null)
            return 0;

        switch (statIndex)
        {
            case 0:
                return mon.iv_Battery;
            case 1:
                return mon.iv_ClockSpeed;
            case 2:
                return mon.iv_ComputingPower;
            case 3:
                return mon.iv_Throughput;
            case 4:
                return mon.iv_Firewall;
            case 5:
                return mon.iv_Encryption;
            default:
                return 0;
        }
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

#if UNITY_EDITOR
        string codeName = PayloadSpriteName(mon.data.codeName);
        if (!string.IsNullOrEmpty(codeName))
        {
            string form = mon != null && mon.IsEvolvedForm ? "Evolved" : "Base";
            string path = $"Assets/_AlgoMon/Sprites/{codeName.ToUpperInvariant()}/{codeName}_{form}.png";
            Sprite formSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (formSprite != null)
                return formSprite;

            string basePath = $"Assets/_AlgoMon/Sprites/{codeName.ToUpperInvariant()}/{codeName}_Base.png";
            Sprite baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(basePath);
            if (baseSprite != null)
                return baseSprite;
        }
#endif

        Sprite catalogSprite = ResolvePayloadSpriteFromCatalog(mon);
        if (catalogSprite != null)
            return catalogSprite;

        return mon.data.portrait;
    }

    private static Sprite ResolvePayloadSpriteFromCatalog(AlgoMonInstance mon)
    {
        string codeName = PayloadSpriteName(mon.data.codeName);
        if (string.IsNullOrEmpty(codeName))
            return null;

        string folder = codeName.ToUpperInvariant();
        string form = mon.IsEvolvedForm ? "Evolved" : "Base";
        return RuntimeUiAssetCatalog.FindSprite($"Assets/_AlgoMon/Sprites/{folder}/{codeName}_{form}.png") ??
               RuntimeUiAssetCatalog.FindSprite($"Assets/_AlgoMon/Sprites/{folder}/{codeName}_Base.png");
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
            ApplySourceLayoutStaticLabelBitmaps();
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

        if (useSourceLayoutTrialVisual)
            EnsureSectionView();
    }

    private void EnsureSectionView()
    {
        Transform visual = FindSourceLayoutTrialVisual();
        if (visual == null)
            return;

        sourceLayoutVisual = visual;

        menuContentGroups = new[]
        {
            visual.Find("Trial_SystemTitle") as RectTransform,
            visual.Find("Trial_MenuPanel") as RectTransform,
            visual.Find("Trial_DepthSelect") as RectTransform,
            visual.Find("Trial_BossRouteSelector") as RectTransform
        };

        sectionViewRoot = CreateRect("Trial_SectionView", visual);
        RectTransform innerBackground = visual.Find("Trial_TerminalInnerBackground") as RectTransform;
        if (innerBackground != null)
        {
            sectionViewRoot.anchorMin = innerBackground.anchorMin;
            sectionViewRoot.anchorMax = innerBackground.anchorMax;
            sectionViewRoot.pivot = innerBackground.pivot;
            sectionViewRoot.anchoredPosition = innerBackground.anchoredPosition;
            sectionViewRoot.sizeDelta = innerBackground.sizeDelta;
        }
        else
        {
            sectionViewRoot.anchorMin = new Vector2(0.05f, 0.08f);
            sectionViewRoot.anchorMax = new Vector2(0.97f, 0.925f);
            sectionViewRoot.offsetMin = Vector2.zero;
            sectionViewRoot.offsetMax = Vector2.zero;
        }
        sectionViewRoot.SetAsLastSibling();

        sectionBackButton = CreateSectionBackButton(sectionViewRoot);

        sectionTitleText = CreateText("SectionTitle", sectionViewRoot, 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.82f, 1f, 1f, 1f));
        ApplyCyberText(sectionTitleText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1.2f, -1.2f));
        SetAnchors(sectionTitleText.rectTransform, new Vector2(0.185f, 0.80f), new Vector2(0.82f, 0.87f));

        if (payloadPanel != null)
        {
            payloadPanel.SetParent(sectionViewRoot, false);
            SetAnchors(payloadPanel, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.84f));
            Image panelBackground = payloadPanel.GetComponent<Image>();
            if (panelBackground != null)
                panelBackground.raycastTarget = true;
        }

        EnsurePayloadGrid(sectionViewRoot);
        EnsureGeneLabPanel(sectionViewRoot);
        EnsureExitPanel(sectionViewRoot);
        EnsureSettingsPanel(sectionViewRoot);
        ShowGeneLabPanel(false);
        ShowExitPanelRoot(false);
        ShowSettingsPanelRoot(false);
        ApplyTerminalZoomMode();

        sectionViewRoot.gameObject.SetActive(false);
    }

    private void EnsurePayloadGrid(Transform parent)
    {
        payloadGridRoot = CreateRect("PayloadGrid", parent);
        SetAnchors(payloadGridRoot, new Vector2(0.02f, 0.0f), new Vector2(0.94f, 0.80f));

        RectTransform gridArea = CreateRect("StorageGridArea", payloadGridRoot);
        SetAnchors(gridArea, new Vector2(0f, 0.16f), new Vector2(0.60f, 1f));

        GridLayoutGroup layout = gridArea.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(140f, 140f);
        layout.spacing = new Vector2(10f, 10f);
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = PayloadGridColumns;

        payloadCellFrames = new Image[PayloadGridCellCount];
        payloadCellSprites = new Image[PayloadGridCellCount];
        payloadCellLabels = new Text[PayloadGridCellCount];
        payloadCellFavoriteMarkers = new Image[PayloadGridCellCount];
        payloadCellButtons = new Button[PayloadGridCellCount];
        payloadCellActions = new UnityEngine.Events.UnityAction[PayloadGridCellCount];
        payloadCellPayloadIndices = new int[PayloadGridCellCount];

        for (int i = 0; i < PayloadGridCellCount; i++)
        {
            RectTransform cell = CreateRect("PayloadCell_" + i, gridArea);

            Image frame = cell.gameObject.AddComponent<Image>();
            frame.sprite = slotNormalSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.raycastTarget = true;
            payloadCellFrames[i] = frame;

            Button button = cell.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;
            payloadCellButtons[i] = button;
            int cellIndex = i;
            button.onClick.AddListener(() => OnPayloadCellClicked(cellIndex));
            payloadCellPayloadIndices[i] = -1;
            ConfigurePayloadCellHover(button, cellIndex);

            Image sprite = CreateImage("CellSprite", cell, Color.white);
            sprite.preserveAspect = true;
            sprite.raycastTarget = false;
            SetAnchors(sprite.rectTransform, new Vector2(0.18f, 0.24f), new Vector2(0.82f, 0.84f));
            payloadCellSprites[i] = sprite;

            Text label = CreateText("CellLabel", cell, 16, FontStyle.Bold, TextAnchor.LowerCenter, new Color(0.86f, 1f, 0.96f, 1f));
            ApplyCyberText(label, new Color(0f, 0.12f, 0.18f, 0.95f), new Vector2(1f, -1f));
            SetAnchors(label.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.24f));
            payloadCellLabels[i] = label;
            CreateBitmapTextMirror(label, 0.62f);

            Image favoriteMarker = CreateImage("CellFavoriteMarker", cell, new Color(1f, 0.86f, 0.28f, 1f));
            favoriteMarker.sprite = StarSprite(true);
            favoriteMarker.type = Image.Type.Simple;
            favoriteMarker.preserveAspect = true;
            favoriteMarker.raycastTarget = false;
            SetAnchors(favoriteMarker.rectTransform, new Vector2(0.700f, 0.700f), new Vector2(0.940f, 0.950f));
            Outline favoriteOutline = favoriteMarker.gameObject.AddComponent<Outline>();
            favoriteOutline.effectColor = new Color(0.12f, 0.06f, 0f, 0.92f);
            favoriteOutline.effectDistance = new Vector2(1.2f, -1.2f);
            favoriteMarker.gameObject.SetActive(false);
            payloadCellFavoriteMarkers[i] = favoriteMarker;
        }

        EnsurePayloadPageNav(payloadGridRoot);
        EnsurePayloadDetailStrips(payloadGridRoot);
        EnsureSquadPanel(parent);
        EnsureSkillSwapPanel(parent);
    }

    private void EnsureGeneLabPanel(Transform parent)
    {
        if (geneLabPanelRoot != null)
        {
            geneLabPanelRoot.SetParent(parent, false);
            return;
        }

        geneLabPanelRoot = CreateRect("GeneLabPanel", parent);
        SetAnchors(geneLabPanelRoot, new Vector2(0.02f, 0.0f), new Vector2(0.96f, 0.80f));

        Image background = geneLabPanelRoot.gameObject.AddComponent<Image>();
        ApplyPanelFrameBackground(
            background,
            ResolvePayloadInspectorPanelSprite(),
            new Color(0.006f, 0.012f, 0.026f, 0.82f));
        background.raycastTarget = true;

        geneLabRouteSelectionRoot = CreateRect("GeneLabRouteSelection", geneLabPanelRoot);
        SetAnchors(geneLabRouteSelectionRoot, Vector2.zero, Vector2.one);

        geneLabRoutePromptText = CreateText("GeneLabRoutePrompt", geneLabRouteSelectionRoot, 16, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.84f, 1f, 1f, 1f));
        geneLabRoutePromptText.lineSpacing = 0.92f;
        ApplyCrispCyberText(geneLabRoutePromptText, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(geneLabRoutePromptText.rectTransform, new Vector2(0.055f, 0.850f), new Vector2(0.940f, 0.940f));
        geneLabRoutePromptText.text = "SELECT BOSS GENE POOL";

        if (!TryBuildGeneLabBossRouteClone(geneLabRouteSelectionRoot))
            BuildGeneLabFallbackRouteSelection(geneLabRouteSelectionRoot);

        geneLabBenchRoot = CreateRect("GeneLabBench", geneLabPanelRoot);
        SetAnchors(geneLabBenchRoot, Vector2.zero, Vector2.one);

        Image miniPayloadPanel = CreateImage("GeneLabMiniPayloadPanel", geneLabBenchRoot, new Color(0.012f, 0.026f, 0.046f, 0.82f));
        ApplyPanelFrameBackground(miniPayloadPanel, ResolveMonsterDisplayPanelSprite(), new Color(0.012f, 0.026f, 0.046f, 0.82f));
        SetAnchors(miniPayloadPanel.rectTransform, new Vector2(0.045f, 0.098f), new Vector2(0.340f, 0.950f));

        Text miniTitle = CreateText("GeneLabMiniPayloadTitle", miniPayloadPanel.rectTransform, 18, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.64f, 0.98f, 1f, 1f));
        ApplyCrispCyberText(miniTitle, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(miniTitle.rectTransform, new Vector2(0.070f, 0.865f), new Vector2(0.930f, 0.945f));
        miniTitle.text = "PAYLOAD UNITS";
        CreateBitmapTextMirror(miniTitle, 0.82f);

        RectTransform miniGrid = CreateRect("GeneLabMiniPayloadGrid", miniPayloadPanel.rectTransform);
        SetAnchors(miniGrid, new Vector2(0.055f, 0.150f), new Vector2(0.945f, 0.875f));

        geneLabMiniPayloadButtons = new Button[GeneLabMiniPayloadCellCount];
        geneLabMiniPayloadFrames = new Image[GeneLabMiniPayloadCellCount];
        geneLabMiniPayloadSprites = new Image[GeneLabMiniPayloadCellCount];
        geneLabMiniPayloadLabels = new Text[GeneLabMiniPayloadCellCount];
        geneLabMiniPayloadBitmapLabels = new TextMeshProUGUI[GeneLabMiniPayloadCellCount];
        geneLabMiniPayloadIndices = new int[GeneLabMiniPayloadCellCount];

        for (int i = 0; i < GeneLabMiniPayloadCellCount; i++)
        {
            int column = i % 2;
            int row = i / 2;
            float xMin = column * 0.5f + 0.012f;
            float xMax = xMin + 0.476f;
            float rowHeight = 0.25f;
            float yMax = 1f - row * rowHeight - 0.012f;
            float yMin = yMax - rowHeight + 0.020f;
            RectTransform cell = CreateRect("GeneLabMiniPayloadCell_" + i, miniGrid);
            SetAnchors(cell, new Vector2(xMin, yMin), new Vector2(xMax, yMax));

            Image frame = cell.gameObject.AddComponent<Image>();
            frame.sprite = slotNormalSprite;
            frame.preserveAspect = true;
            frame.raycastTarget = true;
            frame.color = Color.white;
            geneLabMiniPayloadFrames[i] = frame;

            Button button = cell.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;
            int cellIndex = i;
            button.onClick.AddListener(() => OnGeneLabMiniPayloadClicked(cellIndex));
            geneLabMiniPayloadButtons[i] = button;
            geneLabMiniPayloadIndices[i] = -1;

            Image portrait = CreateImage("Portrait", cell, Color.white);
            portrait.preserveAspect = true;
            SetAnchors(portrait.rectTransform, new Vector2(0.14f, 0.28f), new Vector2(0.86f, 0.88f));
            geneLabMiniPayloadSprites[i] = portrait;

            Text label = CreateText("Label", cell, 12, FontStyle.Bold, TextAnchor.LowerCenter, new Color(0.94f, 1f, 0.98f, 1f));
            label.lineSpacing = 0.88f;
            ApplyCrispCyberText(label, new Color(0f, 0.12f, 0.18f, 0.95f));
            SetAnchors(label.rectTransform, new Vector2(0.03f, 0.018f), new Vector2(0.97f, 0.300f));
            geneLabMiniPayloadLabels[i] = label;
            geneLabMiniPayloadBitmapLabels[i] = CreateBitmapTextMirror(label, 0.48f);
        }

        geneLabMiniPayloadPrevButton = FindOrCreatePanelButton("GeneLabMiniPayloadPrev", miniPayloadPanel.rectTransform, "<", new Vector2(0.070f, 0.040f), new Vector2(0.280f, 0.125f));
        SetPanelButtonLabelSize(geneLabMiniPayloadPrevButton, 16);
        geneLabMiniPayloadPrevButton.onClick.AddListener(() => ChangeGeneLabMiniPayloadPage(-1));

        geneLabMiniPayloadNextButton = FindOrCreatePanelButton("GeneLabMiniPayloadNext", miniPayloadPanel.rectTransform, ">", new Vector2(0.720f, 0.040f), new Vector2(0.930f, 0.125f));
        SetPanelButtonLabelSize(geneLabMiniPayloadNextButton, 16);
        geneLabMiniPayloadNextButton.onClick.AddListener(() => ChangeGeneLabMiniPayloadPage(1));

        geneLabMiniPayloadPageLabel = CreateText("GeneLabMiniPayloadPage", miniPayloadPanel.rectTransform, 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.78f, 1f, 1f, 1f));
        ApplyCrispCyberText(geneLabMiniPayloadPageLabel, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(geneLabMiniPayloadPageLabel.rectTransform, new Vector2(0.300f, 0.040f), new Vector2(0.700f, 0.125f));
        CreateBitmapTextMirror(geneLabMiniPayloadPageLabel, 0.58f);

        Image fusionPanel = CreateImage("GeneLabFusionPanel", geneLabBenchRoot, new Color(0.010f, 0.020f, 0.040f, 0.82f));
        ApplyPanelFrameBackground(fusionPanel, ResolvePayloadInspectorPanelSprite(), new Color(0.010f, 0.020f, 0.040f, 0.82f));
        SetAnchors(fusionPanel.rectTransform, new Vector2(0.382f, 0.175f), new Vector2(0.965f, 0.950f));

        geneLabFusionPortraitImages = new Image[2];
        geneLabFusionNameTexts = new Text[2];
        geneLabFusionMetaTexts = new Text[2];
        geneLabFusionIdleFrames = new Sprite[2][];
        geneLabFusionIdleFps = new float[2];
        geneLabFusionIdleTimers = new float[2];
        geneLabFusionIdleFrameIndices = new int[2];
        geneLabFusionIdleKeys = new string[2];
        BuildGeneLabFusionDisplay(fusionPanel.rectTransform, 0, "UNIT 1", new Vector2(0.040f, 0.575f), new Vector2(0.485f, 0.940f));
        BuildGeneLabFusionDisplay(fusionPanel.rectTransform, 1, "UNIT 2", new Vector2(0.515f, 0.575f), new Vector2(0.960f, 0.940f));

        geneLabFusionText = CreateText("GeneLabFusionText", fusionPanel.rectTransform, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.95f, 1f, 1f, 1f));
        geneLabFusionText.lineSpacing = 0.90f;
        geneLabFusionText.resizeTextForBestFit = false;
        ApplyCrispCyberText(geneLabFusionText, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(geneLabFusionText.rectTransform, new Vector2(0.045f, 0.480f), new Vector2(0.955f, 0.565f));
        geneLabFusionBitmapText = CreateBitmapTextMirror(geneLabFusionText, 0.58f);

        BuildGeneLabFusionTalentBars(fusionPanel.rectTransform);

        geneLabPreviousRecordButton = FindOrCreatePanelButton("GeneLabPrevTargetButton", geneLabBenchRoot, "< UNIT 1", new Vector2(0.385f, 0.095f), new Vector2(0.515f, 0.155f));
        SetPanelButtonLabelSize(geneLabPreviousRecordButton, 13);
        geneLabPreviousRecordButton.onClick.AddListener(() => MoveGeneLabTarget(-1));

        geneLabNextRecordButton = FindOrCreatePanelButton("GeneLabNextTargetButton", geneLabBenchRoot, "UNIT 1 >", new Vector2(0.525f, 0.095f), new Vector2(0.655f, 0.155f));
        SetPanelButtonLabelSize(geneLabNextRecordButton, 13);
        geneLabNextRecordButton.onClick.AddListener(() => MoveGeneLabTarget(1));

        geneLabFuseActionButton = FindOrCreatePanelButton("GeneLabFuseActionButton", geneLabBenchRoot, "FUSE", new Vector2(0.675f, 0.095f), new Vector2(0.805f, 0.155f));
        SetPanelButtonLabelSize(geneLabFuseActionButton, 14);
        geneLabFuseActionButton.onClick.AddListener(FuseSelectedPayload);

        geneLabEvolveActionButton = FindOrCreatePanelButton("GeneLabEvolveActionButton", geneLabBenchRoot, "EVOLVE", new Vector2(0.835f, 0.095f), new Vector2(0.965f, 0.155f));
        SetPanelButtonLabelSize(geneLabEvolveActionButton, 14);
        geneLabEvolveActionButton.onClick.AddListener(EvolveSelectedPayload);
    }

    private void BuildGeneLabFusionTalentBars(Transform parent)
    {
        geneLabFusionTalentRoot = CreateRect("GeneLabFusionTalentBars", parent);
        SetAnchors(geneLabFusionTalentRoot, new Vector2(0.045f, 0.075f), new Vector2(0.955f, 0.470f));

        geneLabFusionTalentCaptionText = CreateText(
            "GeneLabFusionTalentCaption",
            geneLabFusionTalentRoot,
            14,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            new Color(0.66f, 0.96f, 1f, 0.96f));
        ApplyCrispCyberText(geneLabFusionTalentCaptionText, new Color(0f, 0.10f, 0.16f, 0.9f));
        SetAnchors(geneLabFusionTalentCaptionText.rectTransform, new Vector2(0f, 0.890f), new Vector2(1f, 1f));
        geneLabFusionTalentCaptionText.text = "TALENT MERGE";
        geneLabFusionTalentCaptionBitmapText = CreateBitmapTextMirror(geneLabFusionTalentCaptionText, 0.66f);

        BuildGeneLabTalentHeader(geneLabFusionTalentRoot, "UNIT 1", new Vector2(0.095f, 0.810f), new Vector2(0.345f, 0.895f), GeneLabTargetTalentColor);
        BuildGeneLabTalentHeader(geneLabFusionTalentRoot, "UNIT 2", new Vector2(0.375f, 0.810f), new Vector2(0.625f, 0.895f), GeneLabMaterialTalentColor);
        BuildGeneLabTalentHeader(geneLabFusionTalentRoot, "RESULT", new Vector2(0.655f, 0.810f), new Vector2(0.905f, 0.895f), GeneLabProjectedTalentColor);

        int rows = StatAxisLabels.Length;
        geneLabFusionTargetTalentFills = new Image[rows];
        geneLabFusionMaterialTalentFills = new Image[rows];
        geneLabFusionProjectedTalentFills = new Image[rows];
        geneLabFusionTargetTalentValues = new Text[rows];
        geneLabFusionMaterialTalentValues = new Text[rows];
        geneLabFusionProjectedTalentValues = new Text[rows];

        float rowTop = 0.790f;
        float rowSpan = rowTop / rows;
        for (int i = 0; i < rows; i++)
        {
            float yMax = rowTop - i * rowSpan;
            float yMin = rowTop - (i + 1) * rowSpan;
            RectTransform row = CreateRect("GeneLabFusionTalentRow_" + i, geneLabFusionTalentRoot);
            SetAnchors(row, new Vector2(0f, yMin + 0.006f), new Vector2(1f, yMax - 0.006f));

            Text label = CreateText("Label", row, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.86f, 1f, 0.96f, 1f));
            ApplyCrispCyberText(label, new Color(0f, 0.10f, 0.16f, 0.9f));
            SetAnchors(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.080f, 1f));
            label.text = StatAxisLabels[i];
            CreateBitmapTextMirror(label, 0.62f);

            geneLabFusionTargetTalentFills[i] = BuildGeneLabTalentBar(row, "Target", new Vector2(0.095f, 0.160f), new Vector2(0.345f, 0.840f), GeneLabTargetTalentColor);
            geneLabFusionMaterialTalentFills[i] = BuildGeneLabTalentBar(row, "Material", new Vector2(0.375f, 0.160f), new Vector2(0.625f, 0.840f), GeneLabMaterialTalentColor);
            geneLabFusionProjectedTalentFills[i] = BuildGeneLabTalentBar(row, "Result", new Vector2(0.655f, 0.160f), new Vector2(0.905f, 0.840f), GeneLabProjectedTalentColor);

            geneLabFusionTargetTalentValues[i] = BuildGeneLabTalentValue(row, "Unit1Value", new Vector2(0.095f, 0f), new Vector2(0.345f, 1f));
            geneLabFusionMaterialTalentValues[i] = BuildGeneLabTalentValue(row, "Unit2Value", new Vector2(0.375f, 0f), new Vector2(0.625f, 1f));
            geneLabFusionProjectedTalentValues[i] = BuildGeneLabTalentValue(row, "ResultValue", new Vector2(0.655f, 0f), new Vector2(0.905f, 1f));
        }
    }

    private void BuildGeneLabTalentHeader(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        Text header = CreateText("GeneLabTalentHeader_" + text, parent, 13, FontStyle.Bold, TextAnchor.MiddleCenter, color);
        ApplyCrispCyberText(header, new Color(0f, 0.10f, 0.16f, 0.9f));
        SetAnchors(header.rectTransform, anchorMin, anchorMax);
        header.text = text;
        CreateBitmapTextMirror(header, 0.56f);
    }

    private Image BuildGeneLabTalentBar(RectTransform row, string name, Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
    {
        Image barBg = CreateImage(name + "TalentBarBg", row, new Color(0.025f, 0.080f, 0.120f, 0.88f));
        SetAnchors(barBg.rectTransform, anchorMin, anchorMax);

        Image fill = CreateImage(name + "TalentBarFill", barBg.rectTransform, fillColor);
        fill.sprite = talentBarFillSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        fill.preserveAspect = false;
        SetAnchors(fill.rectTransform, Vector2.zero, Vector2.one);
        return fill;
    }

    private Text BuildGeneLabTalentValue(RectTransform row, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        Vector2 plateMin = new Vector2(Mathf.Clamp01(anchorMin.x + 0.024f), 0.190f);
        Vector2 plateMax = new Vector2(Mathf.Clamp01(anchorMax.x - 0.024f), 0.810f);
        Image backplate = CreateImage(name + "Readback", row, new Color(0.001f, 0.014f, 0.024f, 0.68f));
        backplate.raycastTarget = false;
        SetAnchors(backplate.rectTransform, plateMin, plateMax);

        Text value = CreateText(name, row, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.98f, 0.82f, 1f));
        ApplyCrispCyberText(value, new Color(0f, 0.03f, 0.05f, 1f));
        value.resizeTextForBestFit = false;
        SetAnchors(value.rectTransform, anchorMin, anchorMax);
        value.text = "---";
        CreateBitmapTextMirror(value, 0.68f);
        return value;
    }

    private void BuildGeneLabFusionDisplay(Transform parent, int slot, string title, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform panel = CreateRect("GeneLabFusionDisplay_" + slot, parent);
        SetAnchors(panel, anchorMin, anchorMax);

        Image panelImage = panel.gameObject.AddComponent<Image>();
        ApplyPanelFrameBackground(
            panelImage,
            ResolveMonsterDisplayPanelSprite(),
            new Color(0.018f, 0.040f, 0.065f, 0.88f),
            false);
        panelImage.raycastTarget = false;

        Color slotAccent = slot == 0 ? GeneLabTargetTalentColor : GeneLabMaterialTalentColor;
        Text titleText = CreateText("Title", panel, 15, FontStyle.Bold, TextAnchor.UpperLeft, slotAccent);
        ApplyCrispCyberText(titleText, new Color(0f, 0.10f, 0.16f, 0.9f));
        SetAnchors(titleText.rectTransform, new Vector2(0.150f, 0.690f), new Vector2(0.940f, 0.835f));
        titleText.text = title;
        CreateBitmapTextMirror(titleText, 0.64f);

        Image ovalBase = CreateImage("OvalBase", panel, new Color(1f, 1f, 1f, 0.90f));
        ovalBase.sprite = monsterBaseOvalSprite;
        ovalBase.preserveAspect = false;
        ovalBase.enabled = monsterBaseOvalSprite != null;
        SetAnchors(ovalBase.rectTransform, new Vector2(0.220f, 0.230f), new Vector2(0.780f, 0.350f));

        Image portrait = CreateImage("Portrait", panel, Color.white);
        portrait.preserveAspect = true;
        SetAnchors(portrait.rectTransform, new Vector2(0.170f, 0.300f), new Vector2(0.830f, 0.810f));
        geneLabFusionPortraitImages[slot] = portrait;

        Text nameText = CreateText("Name", panel, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 1f, 0.96f, 1f));
        nameText.lineSpacing = 0.84f;
        ApplyCrispCyberText(nameText, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(nameText.rectTransform, new Vector2(0.040f, 0.070f), new Vector2(0.960f, 0.255f));
        geneLabFusionNameTexts[slot] = nameText;
        CreateBitmapTextMirror(nameText, 0.62f);

        Text metaText = CreateText("Meta", panel, 14, FontStyle.Bold, TextAnchor.UpperRight, Color.Lerp(slotAccent, Color.white, 0.5f));
        ApplyCrispCyberText(metaText, new Color(0f, 0.10f, 0.16f, 0.9f));
        SetAnchors(metaText.rectTransform, new Vector2(0.400f, 0.690f), new Vector2(0.885f, 0.835f));
        geneLabFusionMetaTexts[slot] = metaText;
        CreateBitmapTextMirror(metaText, 0.52f);
    }

    private bool TryBuildGeneLabBossRouteClone(Transform parent)
    {
        Transform visual = FindSourceLayoutTrialVisual();
        RectTransform source = visual != null ? visual.Find("Trial_BossRouteSelector") as RectTransform : null;
        if (source == null)
            return false;

        RectTransform clone = Instantiate(source.gameObject, parent, false).GetComponent<RectTransform>();
        clone.name = "GeneLabBossRouteSelector";
        clone.gameObject.SetActive(true);
        SetAnchors(clone, new Vector2(-0.018f, -0.010f), new Vector2(1.018f, 0.875f));
        clone.anchoredPosition = Vector2.zero;
        clone.sizeDelta = Vector2.zero;
        clone.localScale = new Vector3(1.075f, 1.075f, 1f);
        SetChildActive(clone, "BossRouteTopRail", false);

        int count = BossRouteSpecies.Length;
        geneLabSpeciesButtons = new Button[count];
        geneLabSpeciesFrames = new Image[count];
        geneLabSpeciesLabels = new Text[count];
        geneLabSpeciesMetaLabels = new Text[count];
        geneLabRoutePortraitImages = new Image[count];
        geneLabRouteIdleFrames = new Sprite[count][];
        geneLabRouteIdleFrameSeconds = new float[count];
        geneLabRouteIdleTimers = new float[count];
        geneLabRouteIdleFrameIndices = new int[count];

        bool foundAny = false;
        for (int i = 0; i < count; i++)
        {
            string speciesCode = BossRouteSpecies[i];
            Button button = FindChildButton(clone, "BossRoute_" + speciesCode.ToUpperInvariant());
            if (button == null)
                continue;

            foundAny = true;
            button.onClick.RemoveAllListeners();
            string capturedSpeciesCode = speciesCode;
            button.onClick.AddListener(() => SelectGeneLabSpecies(capturedSpeciesCode));
            button.interactable = true;
            button.transform.localScale = Vector3.one;

            SetChildrenActive(button.transform, "SelectedRail", false);
            SetChildrenActive(button.transform, "CleanSelectionFrame", false);
            SetChildrenActive(button.transform, "ActiveDigitalFrame", false);
            SetChildrenActive(button.transform, "HoverGlow", false);

            geneLabSpeciesButtons[i] = button;
            geneLabSpeciesFrames[i] = button.targetGraphic as Image;
            geneLabSpeciesLabels[i] = button.transform.Find("Label")?.GetComponent<Text>();
            geneLabSpeciesMetaLabels[i] = button.transform.Find("RouteStatus")?.GetComponent<Text>();
            RebindClonedBitmapMirror(geneLabSpeciesLabels[i]);
            RebindClonedBitmapMirror(geneLabSpeciesMetaLabels[i]);
            geneLabRoutePortraitImages[i] = button.transform.Find("BossPortraitMask/BossSprite")?.GetComponent<Image>();
            CacheGeneLabRouteIdleFrames(i, geneLabRoutePortraitImages[i]);
        }

        clone.gameObject.SetActive(foundAny);
        return foundAny;
    }

    private void BuildGeneLabFallbackRouteSelection(Transform parent)
    {
        RectTransform grid = CreateRect("GeneLabFallbackRouteGrid", parent);
        SetAnchors(grid, new Vector2(0.060f, 0.080f), new Vector2(0.940f, 0.820f));

        int count = BossRouteSpecies.Length;
        geneLabSpeciesButtons = new Button[count];
        geneLabSpeciesFrames = new Image[count];
        geneLabSpeciesLabels = new Text[count];
        geneLabSpeciesMetaLabels = new Text[count];
        geneLabRoutePortraitImages = new Image[count];
        geneLabRouteIdleFrames = new Sprite[count][];
        geneLabRouteIdleFrameSeconds = new float[count];
        geneLabRouteIdleTimers = new float[count];
        geneLabRouteIdleFrameIndices = new int[count];

        for (int i = 0; i < count; i++)
        {
            int column = i % 3;
            int row = i / 3;
            float xMin = column / 3f + 0.012f;
            float xMax = xMin + 0.300f;
            float yMax = 1f - row * 0.5f - 0.025f;
            float yMin = yMax - 0.440f;
            string speciesCode = BossRouteSpecies[i];
            Button button = FindOrCreatePanelButton(
                "GeneLabFallbackRoute_" + speciesCode.ToUpperInvariant(),
                grid,
                speciesCode.ToUpperInvariant(),
                new Vector2(xMin, yMin),
                new Vector2(xMax, yMax));
            SetPanelButtonLabelSize(button, 16);
            button.onClick.AddListener(() => SelectGeneLabSpecies(speciesCode));
            Image portrait = CreateImage("BossPortrait", button.transform, Color.white);
            portrait.preserveAspect = true;
            SetAnchors(portrait.rectTransform, new Vector2(0.120f, 0.300f), new Vector2(0.880f, 0.780f));

            geneLabSpeciesButtons[i] = button;
            geneLabSpeciesFrames[i] = button.GetComponent<Image>();
            geneLabSpeciesLabels[i] = button.transform.Find("Text")?.GetComponent<Text>();
            geneLabRoutePortraitImages[i] = portrait;
            CacheGeneLabRouteIdleFrames(i, portrait);

            Text meta = CreateText("Meta", button.transform, 10, FontStyle.Bold, TextAnchor.LowerCenter, new Color(0.55f, 0.95f, 1f, 0.92f));
            ApplyCrispCyberText(meta, new Color(0f, 0.10f, 0.16f, 0.9f));
            SetAnchors(meta.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.36f));
            geneLabSpeciesMetaLabels[i] = meta;
        }
    }

    private void CacheGeneLabRouteIdleFrames(int index, Image portraitImage)
    {
        if (index < 0 || geneLabRouteIdleFrames == null || index >= geneLabRouteIdleFrames.Length)
            return;

        float secondsPerFrame;
        Sprite[] frames = LoadBossRouteIdleFrames(BossRouteSpecies[index], out secondsPerFrame);
        if ((frames == null || frames.Length == 0) && portraitImage != null && portraitImage.sprite != null)
            frames = new[] { portraitImage.sprite };
        geneLabRouteIdleFrames[index] = frames ?? Array.Empty<Sprite>();
        geneLabRouteIdleFrameSeconds[index] = secondsPerFrame;
        geneLabRouteIdleTimers[index] = 0f;
        geneLabRouteIdleFrameIndices[index] = 0;

        if (portraitImage != null && geneLabRouteIdleFrames[index].Length > 0)
        {
            portraitImage.sprite = geneLabRouteIdleFrames[index][0];
            portraitImage.enabled = true;
            portraitImage.color = Color.white;
        }
    }

    private void ApplyGeneLabRouteIdleFrame(int index, bool animate)
    {
        Image portraitImage = geneLabRoutePortraitImages != null && index >= 0 && index < geneLabRoutePortraitImages.Length
            ? geneLabRoutePortraitImages[index]
            : null;
        Sprite[] frames = geneLabRouteIdleFrames != null && index >= 0 && index < geneLabRouteIdleFrames.Length
            ? geneLabRouteIdleFrames[index]
            : null;
        if (portraitImage == null || frames == null || frames.Length == 0)
            return;

        if (!animate || frames.Length == 1)
        {
            geneLabRouteIdleTimers[index] = 0f;
            geneLabRouteIdleFrameIndices[index] = 0;
            portraitImage.sprite = frames[0];
            portraitImage.enabled = true;
            return;
        }

        float frameSeconds = geneLabRouteIdleFrameSeconds != null && index < geneLabRouteIdleFrameSeconds.Length
            ? geneLabRouteIdleFrameSeconds[index]
            : 1f / BossRouteFallbackIdleFps;
        frameSeconds = Mathf.Max(0.04f, frameSeconds);

        geneLabRouteIdleTimers[index] += Time.unscaledDeltaTime;
        while (geneLabRouteIdleTimers[index] >= frameSeconds)
        {
            geneLabRouteIdleTimers[index] -= frameSeconds;
            geneLabRouteIdleFrameIndices[index] = (geneLabRouteIdleFrameIndices[index] + 1) % frames.Length;
        }

        portraitImage.sprite = frames[geneLabRouteIdleFrameIndices[index]];
        portraitImage.enabled = portraitImage.sprite != null;
    }

    private void EnsureExitPanel(Transform parent)
    {
        if (exitPanelRoot != null)
        {
            exitPanelRoot.SetParent(parent, false);
            return;
        }

        exitPanelRoot = CreateRect("ExitPanel", parent);
        SetAnchors(exitPanelRoot, new Vector2(0.200f, 0.180f), new Vector2(0.800f, 0.690f));

        Image background = exitPanelRoot.gameObject.AddComponent<Image>();
        ApplyPanelFrameBackground(
            background,
            ResolveSquadPanelBackgroundSprite(),
            new Color(0.006f, 0.012f, 0.026f, 0.88f));
        background.raycastTarget = true;

        Text title = CreateText("ExitPanelTitle", exitPanelRoot, 24, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.82f, 1f, 1f, 1f));
        ApplyCrispCyberText(title, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(title.rectTransform, new Vector2(0.070f, 0.720f), new Vector2(0.920f, 0.890f));
        title.text = "EXIT TERMINAL";

        exitPanelStatusText = CreateText("ExitPanelStatus", exitPanelRoot, 20, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.84f, 1f, 0.96f, 1f));
        exitPanelStatusText.lineSpacing = 0.90f;
        ApplyCrispCyberText(exitPanelStatusText, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(exitPanelStatusText.rectTransform, new Vector2(0.070f, 0.300f), new Vector2(0.920f, 0.710f));

        exitReturnButton = FindOrCreatePanelButton("ExitReturnButton", exitPanelRoot, "RETURN", new Vector2(0.100f, 0.110f), new Vector2(0.425f, 0.255f));
        SetPanelButtonLabelSize(exitReturnButton, 18);
        exitReturnButton.onClick.AddListener(ReturnFromExitPanel);

        exitConfirmButton = FindOrCreatePanelButton("ExitConfirmButton", exitPanelRoot, "QUIT", new Vector2(0.575f, 0.110f), new Vector2(0.900f, 0.255f));
        SetPanelButtonLabelSize(exitConfirmButton, 18);
        exitConfirmButton.onClick.AddListener(ConfirmExit);
    }

    private void EnsureSettingsPanel(Transform parent)
    {
        if (settingsPanelRoot != null)
        {
            settingsPanelRoot.SetParent(parent, false);
            return;
        }

        settingsPanelRoot = CreateRect("SettingsPanel", parent);
        SetAnchors(settingsPanelRoot, new Vector2(0.180f, 0.080f), new Vector2(0.820f, 0.800f));

        Image background = settingsPanelRoot.gameObject.AddComponent<Image>();
        ApplyPanelFrameBackground(
            background,
            ResolveSquadPanelBackgroundSprite(),
            new Color(0.006f, 0.012f, 0.026f, 0.88f));
        background.raycastTarget = true;

        Text title = CreateText("SettingsPanelTitle", settingsPanelRoot, 25, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.82f, 1f, 1f, 1f));
        ApplyCrispCyberText(title, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(title.rectTransform, new Vector2(0.070f, 0.910f), new Vector2(0.920f, 0.985f));
        title.text = "SETTINGS";

        BuildVolumeSliderRow(
            "MusicVolume",
            "MUSIC",
            new Vector2(0.070f, 0.785f),
            new Vector2(0.930f, 0.895f),
            AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.7f,
            OnMusicVolumeSliderChanged,
            out musicVolumeSlider,
            out musicVolumeValueText);

        BuildVolumeSliderRow(
            "SfxVolume",
            "SFX",
            new Vector2(0.070f, 0.660f),
            new Vector2(0.930f, 0.770f),
            AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.8f,
            OnSfxVolumeSliderChanged,
            out sfxVolumeSlider,
            out sfxVolumeValueText);

        BuildMenuTrackRow(new Vector2(0.070f, 0.535f), new Vector2(0.930f, 0.645f));

        RectTransform zoomRow = CreateCyberPanel(
            "TerminalZoomRow",
            settingsPanelRoot,
            new Color(0.010f, 0.038f, 0.052f, 0.76f),
            new Color(0.18f, 0.90f, 0.88f, 0.78f),
            new Color(0.88f, 0.34f, 0.76f, 0.58f),
            12f,
            true);
        SetAnchors(zoomRow, new Vector2(0.070f, 0.345f), new Vector2(0.930f, 0.510f));

        Text zoomLabel = CreateText("TerminalZoomLabel", zoomRow, 19, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.84f, 1f, 0.98f, 1f));
        ApplyCrispCyberText(zoomLabel, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(zoomLabel.rectTransform, new Vector2(0.045f, 0.420f), new Vector2(0.560f, 0.830f));
        zoomLabel.text = "TERMINAL ZOOM";

        Text zoomHint = CreateText("TerminalZoomHint", zoomRow, 13, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.58f, 0.90f, 0.94f, 0.90f));
        zoomHint.lineSpacing = 0.92f;
        ApplyCrispCyberText(zoomHint, new Color(0f, 0.10f, 0.14f, 0.92f));
        SetAnchors(zoomHint.rectTransform, new Vector2(0.045f, 0.105f), new Vector2(0.560f, 0.420f));
        zoomHint.text = "Center and fit the terminal for UI tuning.";

        RectTransform toggleRect = CreateRect("TerminalZoomToggle", zoomRow);
        SetAnchors(toggleRect, new Vector2(0.635f, 0.235f), new Vector2(0.915f, 0.780f));

        terminalZoomToggleImage = toggleRect.gameObject.AddComponent<Image>();
        terminalZoomToggleImage.sprite = ResolveTerminalToggleSprite(terminalZoomModeEnabled);
        terminalZoomToggleImage.type = Image.Type.Simple;
        terminalZoomToggleImage.preserveAspect = true;
        terminalZoomToggleImage.raycastTarget = true;

        terminalZoomToggleButton = toggleRect.gameObject.AddComponent<Button>();
        // The toggle plays its own ZoomEnable/ZoomDisable pair — skip the generic click.
        toggleRect.gameObject.AddComponent<SuppressUiClickSfx>();
        terminalZoomToggleButton.targetGraphic = terminalZoomToggleImage;
        terminalZoomToggleButton.transition = Selectable.Transition.ColorTint;
        ColorBlock toggleColors = terminalZoomToggleButton.colors;
        toggleColors.normalColor = Color.white;
        toggleColors.highlightedColor = new Color(0.78f, 1f, 0.96f, 1f);
        toggleColors.pressedColor = new Color(1f, 0.62f, 0.88f, 1f);
        toggleColors.selectedColor = toggleColors.highlightedColor;
        terminalZoomToggleButton.colors = toggleColors;
        terminalZoomToggleButton.onClick.AddListener(ToggleTerminalZoomMode);

        terminalZoomStatusText = CreateText("TerminalZoomStatus", zoomRow, 15, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.70f, 1f, 0.90f, 1f));
        ApplyCrispCyberText(terminalZoomStatusText, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(terminalZoomStatusText.rectTransform, new Vector2(0.575f, 0.040f), new Vector2(0.930f, 0.220f));

        Text note = CreateText("SettingsPanelNote", settingsPanelRoot, 13, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.70f, 0.92f, 0.96f, 0.88f));
        note.lineSpacing = 0.95f;
        ApplyCrispCyberText(note, new Color(0f, 0.10f, 0.14f, 0.92f));
        SetAnchors(note.rectTransform, new Vector2(0.075f, 0.060f), new Vector2(0.920f, 0.285f));
        note.text = "Volume and track choices save automatically. Zoom is a display-only editor aid that fits the terminal to the screen.";

        RenderSettingsPanel();
    }

    private void BuildVolumeSliderRow(
        string idPrefix,
        string label,
        Vector2 rowAnchorMin,
        Vector2 rowAnchorMax,
        float value,
        UnityEngine.Events.UnityAction<float> onChanged,
        out Slider slider,
        out Text valueText)
    {
        RectTransform row = CreateCyberPanel(
            idPrefix + "Row",
            settingsPanelRoot,
            new Color(0.010f, 0.038f, 0.052f, 0.76f),
            new Color(0.18f, 0.90f, 0.88f, 0.78f),
            new Color(0.88f, 0.34f, 0.76f, 0.58f),
            12f,
            true);
        SetAnchors(row, rowAnchorMin, rowAnchorMax);

        Text rowLabel = CreateText(idPrefix + "Label", row, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.84f, 1f, 0.98f, 1f));
        ApplyCrispCyberText(rowLabel, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(rowLabel.rectTransform, new Vector2(0.045f, 0.20f), new Vector2(0.270f, 0.80f));
        rowLabel.text = label;

        slider = CreateCyberSlider(
            idPrefix + "Slider",
            row,
            new Vector2(0.285f, 0.285f),
            new Vector2(0.820f, 0.715f),
            value,
            onChanged);

        valueText = CreateText(idPrefix + "Value", row, 15, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.70f, 1f, 0.90f, 1f));
        ApplyCrispCyberText(valueText, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(valueText.rectTransform, new Vector2(0.835f, 0.20f), new Vector2(0.965f, 0.80f));
        valueText.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private Slider CreateCyberSlider(
        string objectName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float value,
        UnityEngine.Events.UnityAction<float> onChanged)
    {
        RectTransform root = CreateRect(objectName, parent);
        SetAnchors(root, anchorMin, anchorMax);

        Image trackImage = root.gameObject.AddComponent<Image>();
        trackImage.sprite = ResolveSliderTrackSprite();
        trackImage.type = Image.Type.Simple;
        trackImage.color = new Color(0.40f, 0.78f, 0.84f, 0.70f);
        trackImage.raycastTarget = true;

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        RectTransform fillArea = CreateRect("Fill Area", root);
        SetAnchors(fillArea, new Vector2(0f, 0f), new Vector2(1f, 1f));
        RectTransform fill = CreateRect("Fill", fillArea);
        SetAnchors(fill, new Vector2(0f, 0f), new Vector2(1f, 1f));
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = ResolveSliderFillSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.color = new Color(0.42f, 1f, 0.92f, 0.95f);
        fillImage.raycastTarget = false;
        slider.fillRect = fill;

        const float handleHalfWidth = 10f;
        RectTransform handleArea = CreateRect("Handle Slide Area", root);
        SetAnchors(handleArea, new Vector2(0f, 0f), new Vector2(1f, 1f));
        handleArea.offsetMin = new Vector2(handleHalfWidth, 0f);
        handleArea.offsetMax = new Vector2(-handleHalfWidth, 0f);
        RectTransform handle = CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(handleHalfWidth * 2f, 0f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = ResolveSliderHandleSprite();
        handleImage.type = Image.Type.Simple;
        handleImage.preserveAspect = true;
        handleImage.color = Color.white;
        handleImage.raycastTarget = true;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;

        slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        if (onChanged != null)
            slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    private void BuildMenuTrackRow(Vector2 rowAnchorMin, Vector2 rowAnchorMax)
    {
        RectTransform row = CreateCyberPanel(
            "MenuTrackRow",
            settingsPanelRoot,
            new Color(0.010f, 0.038f, 0.052f, 0.76f),
            new Color(0.18f, 0.90f, 0.88f, 0.78f),
            new Color(0.88f, 0.34f, 0.76f, 0.58f),
            12f,
            true);
        SetAnchors(row, rowAnchorMin, rowAnchorMax);

        Text rowLabel = CreateText("MenuTrackLabel", row, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.84f, 1f, 0.98f, 1f));
        ApplyCrispCyberText(rowLabel, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(rowLabel.rectTransform, new Vector2(0.045f, 0.20f), new Vector2(0.270f, 0.80f));
        rowLabel.text = "TRACK";

        Button prevButton = FindOrCreatePanelButton("MenuTrackPrevButton", row, "<", new Vector2(0.285f, 0.16f), new Vector2(0.385f, 0.84f));
        SetPanelButtonLabelSize(prevButton, 20);
        prevButton.onClick.AddListener(() => StepMenuTrack(-1));

        menuTrackNameText = CreateText("MenuTrackName", row, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.70f, 1f, 0.90f, 1f));
        ApplyCrispCyberText(menuTrackNameText, new Color(0f, 0.12f, 0.18f, 0.95f));
        SetAnchors(menuTrackNameText.rectTransform, new Vector2(0.400f, 0.20f), new Vector2(0.820f, 0.80f));

        Button nextButton = FindOrCreatePanelButton("MenuTrackNextButton", row, ">", new Vector2(0.835f, 0.16f), new Vector2(0.935f, 0.84f));
        SetPanelButtonLabelSize(nextButton, 20);
        nextButton.onClick.AddListener(() => StepMenuTrack(1));
    }

    private void StepMenuTrack(int direction)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null || audio.MenuTrackCount == 0)
            return;

        audio.SetMenuTrack(audio.SelectedMenuTrackIndex + direction);
        RenderSettingsPanel();
    }

    private void OnMusicVolumeSliderChanged(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
        if (musicVolumeValueText != null)
            musicVolumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void OnSfxVolumeSliderChanged(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
        if (sfxVolumeValueText != null)
            sfxVolumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private static Sprite ResolveSliderTrackSprite() => LoadUiSprite(SliderTrackSpritePath, ref cachedSliderTrackSprite);
    private static Sprite ResolveSliderFillSprite() => LoadUiSprite(SliderFillSpritePath, ref cachedSliderFillSprite);
    private static Sprite ResolveSliderHandleSprite() => LoadUiSprite(SliderHandleSpritePath, ref cachedSliderHandleSprite);

    private void EnsurePayloadPageNav(Transform parent)
    {
        RectTransform pageNav = CreateRect("PayloadPageNav", parent);
        SetAnchors(pageNav, new Vector2(0f, 0.01f), new Vector2(0.60f, 0.10f));

        payloadPrevPageButton = FindOrCreatePanelButton("PayloadPrevPageButton", pageNav, "< PREV", new Vector2(0.06f, 0.05f), new Vector2(0.34f, 0.95f));
        SetPanelButtonLabelSize(payloadPrevPageButton, 20);
        payloadPrevPageButton.onClick.AddListener(() => ChangePayloadPage(-1));

        payloadNextPageButton = FindOrCreatePanelButton("PayloadNextPageButton", pageNav, "NEXT >", new Vector2(0.66f, 0.05f), new Vector2(0.94f, 0.95f));
        SetPanelButtonLabelSize(payloadNextPageButton, 20);
        payloadNextPageButton.onClick.AddListener(() => ChangePayloadPage(1));

        payloadPageLabel = CreateText("PayloadPageLabel", pageNav, 20, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.78f, 1f, 1f, 1f));
        ApplyCyberText(payloadPageLabel, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1f, -1f));
        SetAnchors(payloadPageLabel.rectTransform, new Vector2(0.35f, 0f), new Vector2(0.65f, 1f));
        payloadPageLabel.text = "PAGE 1/1";
    }

    private void ChangePayloadPage(int delta)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        payloadPage += delta;
        RenderPayloadGrid(manager);
    }

    private void EnsurePayloadDetailStrips(Transform parent)
    {
        RectTransform detailArea = CreateRect("UnitDetailArea", parent);
        SetAnchors(detailArea, new Vector2(0.595f, 0f), new Vector2(1f, 1.070f));

        Image backing = detailArea.gameObject.AddComponent<Image>();
        ApplyPanelFrameBackground(
            backing,
            ResolvePayloadInspectorPanelSprite(),
            new Color(0.006f, 0.012f, 0.026f, 0.72f));
        backing.raycastTarget = false;

        Text header = CreateText("DetailHeader", detailArea, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.62f, 0.95f, 1f, 1f));
        ApplyCrispCyberText(header, new Color(0f, 0.12f, 0.18f, 0.95f));
        header.horizontalOverflow = HorizontalWrapMode.Overflow;
        header.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchors(header.rectTransform, new Vector2(0.070f, 0.872f), new Vector2(0.292f, 0.948f));
        header.text = "INSPECTOR";

        // Favorite toggle is now a compact square star icon (hollow = not favorite,
        // filled = favorite) instead of the old "FAV" / "FAV*" text button.
        inspectorFavoriteButton = FindOrCreatePanelButton("InspectorFavoriteButton", detailArea, string.Empty, new Vector2(0.300f, 0.872f), new Vector2(0.366f, 0.948f));
        inspectorFavoriteButton.onClick.AddListener(ToggleSelectedFavorite);
        Transform favoriteLabel = inspectorFavoriteButton.transform.Find("Text");
        inspectorFavoriteButtonLabel = favoriteLabel != null ? favoriteLabel.GetComponent<Text>() : null;
        if (inspectorFavoriteButtonLabel != null)
            inspectorFavoriteButtonLabel.text = string.Empty;
        inspectorFavoriteStar = CreateImage("FavoriteStar", inspectorFavoriteButton.transform, new Color(0.82f, 1f, 0.94f, 1f));
        inspectorFavoriteStar.sprite = StarSprite(false);
        inspectorFavoriteStar.type = Image.Type.Simple;
        inspectorFavoriteStar.preserveAspect = true;
        inspectorFavoriteStar.raycastTarget = false;
        SetAnchors(inspectorFavoriteStar.rectTransform, new Vector2(0.16f, 0.13f), new Vector2(0.84f, 0.87f));
        inspectorFavoriteStar.transform.SetAsLastSibling();

        inspectorSkillsButton = FindOrCreatePanelButton("InspectorSkillsButton", detailArea, "SKILLS", new Vector2(0.378f, 0.872f), new Vector2(0.560f, 0.948f));
        SetPanelButtonLabelSize(inspectorSkillsButton, 14);
        inspectorSkillsButton.onClick.AddListener(OpenSkillSwapPanel);
        Transform skillsLabel = inspectorSkillsButton.transform.Find("Text");
        inspectorSkillsButtonLabel = skillsLabel != null ? skillsLabel.GetComponent<Text>() : null;

        // SQUAD (view squad/formation) button lives next to the top PAYLOAD title instead
        // of the inspector row; toggled with the payload grid so it is payload-only.
        inspectorViewSquadButton = FindOrCreatePanelButton("InspectorViewSquadButton", sectionViewRoot, "SQUAD", new Vector2(0.345f, 0.795f), new Vector2(0.470f, 0.872f));
        SetPanelButtonLabelSize(inspectorViewSquadButton, 14);
        inspectorViewSquadButton.onClick.AddListener(() => OpenSquadPanel(false, null));
        inspectorViewSquadButton.gameObject.SetActive(false);

        inspectorSquadButton = FindOrCreatePanelButton("InspectorSquadButton", detailArea, "ADD TO SQUAD", new Vector2(0.572f, 0.872f), new Vector2(0.970f, 0.948f));
        SetPanelButtonLabelSize(inspectorSquadButton, 15);
        inspectorSquadButton.onClick.AddListener(ToggleSelectedSquad);
        Transform squadLabel = inspectorSquadButton.transform.Find("Text");
        inspectorSquadButtonLabel = squadLabel != null ? squadLabel.GetComponent<Text>() : null;

        BuildInspectorPortrait(detailArea);
        BuildInspectorName(detailArea);
        BuildInspectorRadar(detailArea);
        BuildInspectorTalentBars(detailArea);
    }

    private void BuildInspectorPortrait(Transform detailArea)
    {
        RectTransform band = CreateRect("InspectorPortraitBand", detailArea);
        SetAnchors(band, new Vector2(0f, 0.675f), new Vector2(1f, 0.875f));
        band.SetAsFirstSibling();

        Image ovalBase = CreateImage("InspectorOvalBase", band, new Color(1f, 1f, 1f, 0.95f));
        ovalBase.sprite = monsterBaseOvalSprite;
        ovalBase.preserveAspect = false;
        ovalBase.enabled = monsterBaseOvalSprite != null;
        SetAnchors(ovalBase.rectTransform, new Vector2(0.055f, 0.02f), new Vector2(0.350f, 0.240f));

        inspectorPortraitImage = CreateImage("InspectorPortrait", band, Color.white);
        inspectorPortraitImage.preserveAspect = true;
        SetAnchors(inspectorPortraitImage.rectTransform, new Vector2(0.040f, 0.080f), new Vector2(0.365f, 1f));
    }

    private void BuildInspectorName(Transform detailArea)
    {
        inspectorNameText = CreateText("InspectorName", detailArea, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.84f, 1f, 1f, 1f));
        inspectorNameText.lineSpacing = 0.86f;
        ApplyCrispCyberText(inspectorNameText, new Color(0f, 0.14f, 0.22f, 1f));
        SetAnchors(inspectorNameText.rectTransform, new Vector2(0.390f, 0.785f), new Vector2(0.955f, 0.865f));
        inspectorNameText.text = "SELECT A UNIT";

        RectTransform expClip = CreateRect("InspectorExpClip", detailArea);
        SetAnchors(expClip, new Vector2(0.390f, 0.715f), new Vector2(0.950f, 0.790f));
        RectMask2D expMask = expClip.gameObject.AddComponent<RectMask2D>();
        expMask.padding = Vector4.zero;

        Sprite expFillSprite = ResolveInspectorExpBarFillSprite();
        Image expTrack = CreateImage("InspectorExpTrack", expClip, new Color(0.08f, 0.42f, 0.48f, 0.48f));
        expTrack.sprite = expFillSprite;
        expTrack.type = Image.Type.Simple;
        expTrack.preserveAspect = false;
        expTrack.raycastTarget = false;
        SetAnchors(expTrack.rectTransform, new Vector2(0.035f, 0.350f), new Vector2(0.965f, 0.650f));

        inspectorExpFill = CreateImage("InspectorExpFill", expClip, new Color(0.46f, 1f, 0.76f, 1f));
        inspectorExpFill.sprite = expFillSprite;
        inspectorExpFill.type = Image.Type.Filled;
        inspectorExpFill.fillMethod = Image.FillMethod.Horizontal;
        inspectorExpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        inspectorExpFill.fillAmount = 0f;
        inspectorExpFill.preserveAspect = false;
        inspectorExpFill.raycastTarget = false;
        SetAnchors(inspectorExpFill.rectTransform, new Vector2(0.035f, 0.350f), new Vector2(0.965f, 0.650f));

        Sprite expUnder = ResolveInspectorExpBarUnderSprite();
        Image expUnderlay = CreateImage("InspectorExpUnder", expClip, new Color(0.70f, 1f, 0.96f, 0.96f));
        expUnderlay.sprite = expUnder;
        expUnderlay.type = Image.Type.Simple;
        expUnderlay.preserveAspect = false;
        expUnderlay.raycastTarget = false;
        expUnderlay.enabled = expUnder != null;
        SetAnchors(expUnderlay.rectTransform, new Vector2(-0.220f, -0.035f), new Vector2(1.000f, 1.035f));
        expUnderlay.transform.SetAsLastSibling();

        inspectorExpText = CreateText("InspectorExpText", detailArea, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.72f, 1f, 0.92f, 1f));
        ApplyCrispCyberText(inspectorExpText, new Color(0f, 0.12f, 0.14f, 1f));
        SetAnchors(inspectorExpText.rectTransform, new Vector2(0.430f, 0.665f), new Vector2(0.950f, 0.710f));
        inspectorExpText.text = "EXP --/--";
    }

    private void BuildInspectorRadar(Transform detailArea)
    {
        inspectorRadarRoot = CreateRect("InspectorRadar", detailArea);
        SetAnchors(inspectorRadarRoot, new Vector2(0.16f, 0.360f), new Vector2(0.84f, 0.615f));

        inspectorRadar = inspectorRadarRoot.gameObject.AddComponent<RadarChartGraphic>();
        inspectorRadar.color = Color.white;
        inspectorRadar.raycastTarget = false;

        inspectorRadarLabels = new Text[StatAxisLabels.Length];
        for (int i = 0; i < StatAxisLabels.Length; i++)
        {
            Text label = CreateText("RadarLabel_" + i, inspectorRadarRoot, 17, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.74f, 0.98f, 1f, 1f));
            ApplyCrispCyberText(label, new Color(0f, 0.1f, 0.16f, 0.95f));
            RectTransform lr = label.rectTransform;
            lr.anchorMin = new Vector2(0.5f, 0.5f);
            lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(84f, 30f);
            inspectorRadarLabels[i] = label;
        }
    }

    private void BuildInspectorTalentBars(Transform detailArea)
    {
        RectTransform talentRoot = CreateRect("InspectorTalentBars", detailArea);
        inspectorTalentRoot = talentRoot;
        SetAnchors(talentRoot, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.295f));

        Text caption = CreateText("TalentCaption", talentRoot, 15, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.6f, 0.92f, 1f, 0.9f));
        ApplyCrispCyberText(caption, new Color(0f, 0.1f, 0.16f, 0.9f));
        SetAnchors(caption.rectTransform, new Vector2(0f, 0.9f), new Vector2(1f, 1f));
        caption.text = "TALENT HARDWARE CEILING";

        int rows = StatAxisLabels.Length;
        inspectorTalentFills = new Image[rows];
        inspectorTalentValues = new Text[rows];

        float rowTop = 0.88f;
        float rowSpan = rowTop / rows;
        for (int i = 0; i < rows; i++)
        {
            float yMax = rowTop - i * rowSpan;
            float yMin = rowTop - (i + 1) * rowSpan;
            float pad = rowSpan * 0.16f;

            RectTransform row = CreateRect("TalentRow_" + i, talentRoot);
            SetAnchors(row, new Vector2(0f, yMin + pad), new Vector2(1f, yMax - pad));

            Text name = CreateText("TalentName", row, 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.8f, 1f, 1f, 1f));
            ApplyCrispCyberText(name, new Color(0f, 0.1f, 0.16f, 0.9f));
            SetAnchors(name.rectTransform, new Vector2(0f, 0f), new Vector2(0.18f, 1f));
            name.text = StatAxisLabels[i];

            Image barBg = CreateImage("TalentBarBg", row, new Color(0.03f, 0.10f, 0.16f, 0.9f));
            SetAnchors(barBg.rectTransform, new Vector2(0.20f, 0.12f), new Vector2(0.82f, 0.88f));

            Image fill = CreateImage("TalentBarFill", barBg.rectTransform, Color.white);
            fill.sprite = talentBarFillSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.preserveAspect = false;
            SetAnchors(fill.rectTransform, Vector2.zero, Vector2.one);
            inspectorTalentFills[i] = fill;

            Text value = CreateText("TalentValue", row, 16, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.86f, 1f, 0.96f, 1f));
            ApplyCrispCyberText(value, new Color(0f, 0.1f, 0.16f, 0.9f));
            SetAnchors(value.rectTransform, new Vector2(0.84f, 0f), new Vector2(1f, 1f));
            value.text = "0";
            inspectorTalentValues[i] = value;
        }
    }

    private Button CreateSectionBackButton(Transform parent)
    {
        RectTransform buttonRect = CreateRect("SectionBackButton", parent);
        SetAnchors(buttonRect, new Vector2(0.03f, 0.80f), new Vector2(0.16f, 0.87f));

        Image hitArea = buttonRect.gameObject.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = hitArea;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.70f, 1f, 0.95f, 1f);
        colors.pressedColor = new Color(1f, 0.58f, 0.78f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Image arrow = CreateImage("BackArrow", buttonRect, new Color(0.82f, 1f, 1f, 1f));
        arrow.sprite = backArrowSprite;
        arrow.enabled = backArrowSprite != null;
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        SetAnchors(arrow.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.34f, 0.82f));
        arrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        Text label = CreateText("BackLabel", buttonRect, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.86f, 1f, 0.96f, 1f));
        label.text = backArrowSprite != null ? "BACK" : "< BACK";
        SetAnchors(label.rectTransform, new Vector2(0.38f, 0f), new Vector2(0.97f, 1f));

        return button;
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
        sourceLayoutBossRouteBitmapLabels = new TextMeshProUGUI[count];
        sourceLayoutBossRouteBitmapCodes = new TextMeshProUGUI[count];
        sourceLayoutBossRouteBitmapElementTags = new TextMeshProUGUI[count];
        sourceLayoutBossRouteBitmapStatuses = new TextMeshProUGUI[count];
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
        if (sourceLayoutBossRouteBitmapLabels != null && index < sourceLayoutBossRouteBitmapLabels.Length)
            sourceLayoutBossRouteBitmapLabels[index] = EnsureBossRouteBitmapText(button.transform, "Label", BossRouteLabelBitmapScale);
        if (sourceLayoutBossRouteBitmapCodes != null && index < sourceLayoutBossRouteBitmapCodes.Length)
            sourceLayoutBossRouteBitmapCodes[index] = EnsureBossRouteBitmapText(button.transform, "RouteCode", BossRouteCodeBitmapScale);
        if (sourceLayoutBossRouteBitmapElementTags != null && index < sourceLayoutBossRouteBitmapElementTags.Length)
            sourceLayoutBossRouteBitmapElementTags[index] = EnsureBossRouteBitmapText(button.transform, "ElementTag", BossRouteElementBitmapScale);
        if (sourceLayoutBossRouteBitmapStatuses != null && index < sourceLayoutBossRouteBitmapStatuses.Length)
            sourceLayoutBossRouteBitmapStatuses[index] = EnsureBossRouteBitmapText(button.transform, "RouteStatus", BossRouteStatusBitmapScale);
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

        BattleAnimationProfile profile = BattleAnimationProfileLoader.TryLoadProfile(speciesCodeName, "Evolved");
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
        if (bossRouteSelectionBarSprite == null)
        {
            bossRouteSelectionBarSprite =
                RuntimeUiAssetCatalog.FindSprite("Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD/Grid/White/SelectorThick_Focus.png") ??
                RuntimeUiAssetCatalog.FindSprite("Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD/Selectors/Square_Select.png");
        }
        return bossRouteSelectionBarSprite;
    }

    private static Sprite BossRouteSelectionPanelSprite()
    {
        if (bossRouteSelectionPanelSprite != null)
            return bossRouteSelectionPanelSprite;

#if UNITY_EDITOR
        bossRouteSelectionPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossRouteSelectionPanelSpritePath);
#endif
        if (bossRouteSelectionPanelSprite == null)
            bossRouteSelectionPanelSprite = RuntimeUiAssetCatalog.FindSprite(BossRouteSelectionPanelSpritePath);
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
            bossRouteDefaultFont = ResolveTerminalDefaultFont();

        return bossRouteDefaultFont;
    }

    private TextMeshProUGUI CreateBitmapTextMirror(Text sourceText, float fontScale)
    {
        if (sourceText == null)
            return null;

        return EnableBitmapMirror(sourceText, fontScale, sourceText.alignment);
    }

    private void RebindClonedBitmapMirror(Text legacyText)
    {
        if (legacyText == null)
            return;

        TmpLegacyTextMirror mirror = legacyText.GetComponentInChildren<TmpLegacyTextMirror>(true);
        if (mirror == null)
            return;

        mirror.SourceText = legacyText;
        mirror.HideSourceText = true;
    }

    private TextMeshProUGUI EnsureBossRouteBitmapText(Transform buttonRoot, string legacyTextPath, float fontScale)
    {
        Transform legacyTextTransform = buttonRoot != null ? buttonRoot.Find(legacyTextPath) : null;
        if (legacyTextTransform == null)
            return null;

        Text legacyText = legacyTextTransform.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.enabled = false;
            legacyText.raycastTarget = false;
        }

        ConfigureBossRouteLabelBackplate(legacyTextTransform, string.Equals(legacyTextPath, "Label", StringComparison.Ordinal));

        RectTransform bitmapRect = GetOrCreateChildRect(legacyTextTransform, legacyTextPath + "Bitmap");
        bitmapRect.anchorMin = Vector2.zero;
        bitmapRect.anchorMax = Vector2.one;
        bitmapRect.pivot = new Vector2(0.5f, 0.5f);
        bitmapRect.anchoredPosition = Vector2.zero;
        bitmapRect.sizeDelta = Vector2.zero;
        bitmapRect.gameObject.SetActive(true);

        return ConfigureTmpMirror(bitmapRect, legacyText, TextAnchor.MiddleCenter, fontScale);
    }

    private void ConfigureBossRouteLabelBackplate(Transform legacyTextTransform, bool visible)
    {
        if (legacyTextTransform == null)
            return;

        Transform existing = legacyTextTransform.Find("ReadableBackplate");
        if (!visible)
        {
            if (existing != null)
                existing.gameObject.SetActive(false);
            return;
        }

        Image backplate = FindOrCreateImage(
            "ReadableBackplate",
            legacyTextTransform,
            new Color(0.001f, 0.012f, 0.022f, 0.64f));
        backplate.gameObject.SetActive(true);
        backplate.raycastTarget = false;
        SetAnchors(backplate.rectTransform, new Vector2(0.030f, 0.050f), new Vector2(0.970f, 0.950f));
        backplate.transform.SetAsFirstSibling();
    }

    private void ApplySourceLayoutStaticLabelBitmaps()
    {
        if (sourceLayoutStaticLabelBitmapsReady)
            return;

        Transform visual = FindSourceLayoutTrialVisual();
        if (visual == null)
            return;

        TextMeshProUGUI status = EnableBitmapMirror(
            visual.Find("Trial_SystemTitle/Status")?.GetComponent<Text>(),
            SystemStatusBitmapScale,
            TextAnchor.MiddleCenter);
        TextMeshProUGUI meta = EnableBitmapMirror(
            visual.Find("Trial_BossRouteSelector/BossRouteMeta")?.GetComponent<Text>(),
            BossRouteMetaBitmapScale,
            TextAnchor.MiddleCenter);

        sourceLayoutStaticLabelBitmapsReady = status != null && meta != null;
    }

    private TextMeshProUGUI EnableBitmapMirror(Text legacyText, float fontScale, TextAnchor alignment)
    {
        if (legacyText == null)
            return null;

        RectTransform bitmapRect = GetOrCreateChildRect(legacyText.transform, "Bitmap");
        bitmapRect.anchorMin = Vector2.zero;
        bitmapRect.anchorMax = Vector2.one;
        bitmapRect.pivot = new Vector2(0.5f, 0.5f);
        bitmapRect.offsetMin = Vector2.zero;
        bitmapRect.offsetMax = Vector2.zero;
        bitmapRect.localScale = Vector3.one;
        bitmapRect.gameObject.SetActive(true);

        return ConfigureTmpMirror(bitmapRect, legacyText, alignment, fontScale);
    }

    // Builds (or updates) a crisp TextMeshPro (SDF) graphic on `mirrorRect` that mirrors
    // the legacy Text. The legacy Text stays the data source but is hidden; TMP renders.
    private TextMeshProUGUI ConfigureTmpMirror(RectTransform mirrorRect, Text legacyText, TextAnchor alignment, float fontScale)
    {
        if (mirrorRect == null)
            return null;

        TextMeshProUGUI tmp = mirrorRect.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = mirrorRect.gameObject.AddComponent<TextMeshProUGUI>();

        TMP_FontAsset font = ResolveTmpFontAsset();
        if (font != null)
            tmp.font = font;

        float scale = Mathf.Clamp(fontScale, 0.98f, 1.08f);
        float maxSize = legacyText != null ? Mathf.Max(12f, legacyText.fontSize * scale) : 14f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = maxSize;
        tmp.fontSizeMin = Mathf.Max(11f, maxSize - 4f);
        tmp.alignment = ToTmpAlignment(alignment);
        tmp.enableWordWrapping = legacyText == null || legacyText.horizontalOverflow == HorizontalWrapMode.Wrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = false;
        tmp.raycastTarget = false;
        tmp.margin = Vector4.zero;
        tmp.color = legacyText != null ? legacyText.color : CyberUiTheme.TextPrimary;
        tmp.fontStyle = FontStyles.Normal;
        tmp.fontWeight = FontWeight.SemiBold;
        tmp.outlineColor = TmpReadableOutlineColor;
        tmp.outlineWidth = TmpReadableOutlineWidth;
        tmp.extraPadding = true;
        if (legacyText != null)
            tmp.text = legacyText.text;

        TmpLegacyTextMirror mirror = mirrorRect.GetComponent<TmpLegacyTextMirror>();
        if (mirror == null)
            mirror = mirrorRect.gameObject.AddComponent<TmpLegacyTextMirror>();
        mirror.SourceText = legacyText;
        mirror.HideSourceText = true;
        return tmp;
    }

    private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            case TextAnchor.MiddleCenter:
            default: return TextAlignmentOptions.Center;
        }
    }

    private static TMP_FontAsset ResolveTmpFontAsset()
    {
        if (tmpMirrorFontAsset != null)
            return tmpMirrorFontAsset;

        tmpMirrorFontAsset =
            Resources.Load<TMP_FontAsset>("Fonts/NicoBold SDF") ??
            Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return tmpMirrorFontAsset;
    }

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
        Font font = defaultFont != null ? defaultFont : ResolveTerminalDefaultFont();
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

    private void ConfigureCrispCanvas()
    {
        ConfigureCrispCanvases(GetComponentsInParent<Canvas>(true));
        ConfigureCrispCanvases(GetComponentsInChildren<Canvas>(true));
        ConfigureCrispCanvasScalers(GetComponentsInParent<CanvasScaler>(true));
        ConfigureCrispCanvasScalers(GetComponentsInChildren<CanvasScaler>(true));
    }

    private static void ConfigureCrispCanvases(Canvas[] canvases)
    {
        if (canvases == null)
            return;

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
                canvases[i].pixelPerfect = true;
        }
    }

    private static void ConfigureCrispCanvasScalers(CanvasScaler[] scalers)
    {
        if (scalers == null)
            return;

        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null)
                continue;

            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 100f;
        }
    }

    private static Font ResolveTerminalDefaultFont()
    {
        Font font = Resources.Load<Font>("Fonts/NicoBold-Regular");
        return font != null
            ? font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        text.alignByGeometry = true;
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
            int buttonTier = i + ThreatTierRules.MinTier;
            EnsureDepthTierRouteLayersLabel(button.transform, buttonTier);
            depthTierRecommendationLabels[i] = EnsureDepthTierRecommendationLabel(button.transform, buttonTier);
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

        RectTransform rect = recommendation.rectTransform;
        if (created || HasApproxAnchors(rect, new Vector2(-0.220f, -0.760f), new Vector2(1.220f, -0.470f)))
        {
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

        EnableBitmapMirror(recommendation, DepthRecommendationBitmapScale, TextAnchor.UpperCenter);

        return recommendation;
    }

    private Text EnsureDepthTierRouteLayersLabel(Transform buttonTransform, int tier)
    {
        if (buttonTransform == null)
            return null;

        Text routeLayers = buttonTransform.Find("RouteLayers")?.GetComponent<Text>();
        bool created = routeLayers == null;
        if (routeLayers == null)
            routeLayers = CreateText("RouteLayers", buttonTransform, DepthRouteLayerFontSize, FontStyle.Bold, TextAnchor.UpperCenter, CyberUiTheme.TextSecondary);

        routeLayers.font = defaultFont;
        routeLayers.fontSize = DepthRouteLayerFontSize;
        routeLayers.fontStyle = FontStyle.Bold;
        routeLayers.alignment = TextAnchor.UpperCenter;
        routeLayers.horizontalOverflow = HorizontalWrapMode.Overflow;
        routeLayers.verticalOverflow = VerticalWrapMode.Overflow;
        routeLayers.resizeTextForBestFit = false;
        routeLayers.raycastTarget = false;
        routeLayers.text = BuildDepthTierRouteLayersText(tier);

        RectTransform rect = routeLayers.rectTransform;
        if (created
            || HasApproxAnchors(rect, new Vector2(-0.220f, -0.460f), new Vector2(1.220f, -0.070f))
            || HasApproxAnchors(rect, new Vector2(-0.220f, 1.050f), new Vector2(1.220f, 1.330f))
            || HasApproxAnchors(rect, new Vector2(-0.220f, 0.980f), new Vector2(1.220f, 1.260f)))
        {
            rect.anchorMin = new Vector2(-0.220f, 0.920f);
            rect.anchorMax = new Vector2(1.220f, 1.200f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.localScale = Vector3.one;
        }

        Shadow shadow = FindExactShadow(routeLayers);
        if (shadow == null)
            shadow = routeLayers.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.84f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;

        TextMeshProUGUI bitmap = EnableBitmapMirror(routeLayers, DepthRecommendationBitmapScale, TextAnchor.UpperCenter);
        if (bitmap != null)
        {
            bitmap.enableAutoSizing = false;
            bitmap.fontSize = DepthRouteLayerFontSize;
            bitmap.fontSizeMin = DepthRouteLayerFontSize;
            bitmap.fontSizeMax = DepthRouteLayerFontSize;
            bitmap.ForceMeshUpdate();
        }

        return routeLayers;
    }

    private static string BuildDepthTierRecommendationText(int tier)
    {
        ThreatTier clamped = ThreatTierRules.ClampTier(tier);
        return $"REC LV {ThreatTierRules.MinLevel(clamped)}-{ThreatTierRules.MaxLevel(clamped)}";
    }

    private static string BuildDepthTierRouteLayersText(int tier)
    {
        return $"DEPTH {GridGenerationSettings.TotalLayerRangeLabel(tier)}";
    }

    private static bool HasApproxAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        const float epsilon = 0.001f;
        return rect != null
            && Vector2.SqrMagnitude(rect.anchorMin - anchorMin) <= epsilon
            && Vector2.SqrMagnitude(rect.anchorMax - anchorMax) <= epsilon;
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
        ApplyPanelFrameBackground(
            background,
            ResolvePayloadInspectorPanelSprite(),
            new Color(0.006f, 0.012f, 0.026f, 0.86f));
        SetAnchors(payloadPanel, new Vector2(0.515f, 0.465f), new Vector2(0.888f, 0.785f));

        Image sideLine = CreateImage("PayloadSideLine", payloadPanel, new Color(1f, 0.25f, 0.86f, 0.58f));
        SetAnchors(sideLine.rectTransform, new Vector2(0f, 0f), new Vector2(0.012f, 1f));

        Image portraitFrame = CreateImage("PayloadPortraitFrame", payloadPanel, new Color(0.02f, 0.036f, 0.064f, 0.92f));
        ApplyPanelFrameBackground(
            portraitFrame,
            ResolveMonsterDisplayPanelSprite(),
            new Color(0.02f, 0.036f, 0.064f, 0.92f));
        SetAnchors(portraitFrame.rectTransform, new Vector2(0.045f, 0.360f), new Vector2(0.38f, 0.88f));

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
        SetAnchors(payloadListText.rectTransform, new Vector2(0.045f, 0.035f), new Vector2(0.38f, 0.190f));

        payloadPreviousButton = payloadPreviousButton != null
            ? payloadPreviousButton
            : FindOrCreatePanelButton("PayloadPreviousButton", payloadPanel, "PREV", new Vector2(0.045f, 0.205f), new Vector2(0.205f, 0.280f));
        payloadNextButton = payloadNextButton != null
            ? payloadNextButton
            : FindOrCreatePanelButton("PayloadNextButton", payloadPanel, "NEXT", new Vector2(0.220f, 0.205f), new Vector2(0.380f, 0.280f));
        geneLabFuseButton = geneLabFuseButton != null
            ? geneLabFuseButton
            : FindOrCreatePanelButton("GeneLabFuseButton", payloadPanel, "FUSE", new Vector2(0.045f, 0.295f), new Vector2(0.205f, 0.345f));
        geneLabEvolveButton = geneLabEvolveButton != null
            ? geneLabEvolveButton
            : FindOrCreatePanelButton("GeneLabEvolveButton", payloadPanel, "EVOLVE", new Vector2(0.220f, 0.295f), new Vector2(0.380f, 0.345f));

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

    private Button FindOrCreatePanelButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ApplyPanelButtonSpriteStyle(button, image);

        Text text = FindOrCreateText(buttonObject.transform, "Text", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 1f, 0.96f, 1f));
        text.text = label;
        ConfigurePanelButtonText(text, 12);
        SetAnchors(text.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f));
        SetAnchors(button.GetComponent<RectTransform>(), anchorMin, anchorMax);
        EnsurePanelButtonFeedbackLayers(button, text);
        return button;
    }

    private void EnsurePanelButtonFeedbackLayers(Button button, Text label)
    {
        if (button == null)
            return;

        Transform parent = button.transform;

        Image hoverGlow = FindOrCreateImage("ButtonStoneHighlight", parent, Color.clear);
        hoverGlow.sprite = ResolvePanelButtonHoverGlowSprite();
        hoverGlow.type = Image.Type.Simple;
        hoverGlow.preserveAspect = false;
        hoverGlow.raycastTarget = false;
        SetAnchors(hoverGlow.rectTransform, Vector2.zero, Vector2.one);

        Transform legacyHoverGlow = parent.Find("HoverGlow");
        if (legacyHoverGlow != null)
            legacyHoverGlow.gameObject.SetActive(false);

        Image pressFeedback = FindOrCreateImage("PressFeedback", parent, Color.white);
        pressFeedback.sprite = ResolvePanelButtonPressedSprite();
        pressFeedback.type = Image.Type.Simple;
        pressFeedback.preserveAspect = false;
        pressFeedback.raycastTarget = false;
        pressFeedback.enabled = false;
        SetAnchors(pressFeedback.rectTransform, Vector2.zero, Vector2.one);

        if (label != null)
            label.transform.SetAsLastSibling();

        CyberImageButtonFeedback imageFeedback = button.GetComponent<CyberImageButtonFeedback>();
        if (imageFeedback != null)
            imageFeedback.enabled = false;

        PixelHudButtonFeedback pixelFeedback = button.GetComponent<PixelHudButtonFeedback>();
        if (pixelFeedback == null)
            pixelFeedback = button.gameObject.AddComponent<PixelHudButtonFeedback>();
        pixelFeedback.Configure(button, hoverGlow, pressFeedback);
    }

    private void ApplyPanelFrameBackground(Image image, Sprite sprite, Color fallbackColor, bool sliced = true)
    {
        if (image == null)
            return;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sliced && sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 1f;
            return;
        }

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = fallbackColor;
    }

    private Sprite ResolvePayloadInspectorPanelSprite()
    {
        return payloadInspectorPanelSprite != null
            ? payloadInspectorPanelSprite
            : LoadUiSprite(PayloadInspectorPanelSpritePath, ref cachedPayloadInspectorPanelSprite);
    }

    private Sprite ResolveSquadPanelBackgroundSprite()
    {
        return squadPanelBackgroundSprite != null
            ? squadPanelBackgroundSprite
            : LoadUiSprite(SquadPanelFrameSpritePath, ref cachedSquadPanelBackgroundSprite);
    }

    private Sprite ResolveMonsterDisplayPanelSprite()
    {
        return monsterDisplayPanelSprite != null
            ? monsterDisplayPanelSprite
            : LoadUiSprite(MonsterDisplayPanelSpritePath, ref cachedMonsterDisplayPanelSprite);
    }

    private static Sprite ResolveInspectorExpBarFillSprite()
    {
        return LoadUiSprite(InspectorExpBarFillSpritePath, ref cachedInspectorExpBarFillSprite);
    }

    private static Sprite ResolveInspectorExpBarUnderSprite()
    {
        return LoadUiSprite(InspectorExpBarUnderSpritePath, ref cachedInspectorExpBarUnderSprite);
    }

    private static Sprite ResolveTerminalToggleSprite(bool enabled)
    {
        return enabled
            ? LoadUiSprite(TerminalToggleOnSpritePath, ref cachedTerminalToggleOnSprite)
            : LoadUiSprite(TerminalToggleOffSpritePath, ref cachedTerminalToggleOffSprite);
    }

    private static Sprite LoadUiSprite(string assetPath, ref Sprite cache)
    {
        if (cache != null)
            return cache;

#if UNITY_EDITOR
        cache = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#endif
        if (cache == null)
            cache = RuntimeUiAssetCatalog.FindSprite(assetPath);
        return cache;
    }

    private void ApplyPanelButtonSpriteStyle(Button button, Image image)
    {
        if (button == null || image == null)
            return;

        Sprite normal = ResolvePanelButtonNormalSprite();
        Sprite highlighted = ResolvePanelButtonHighlightedSprite();
        Sprite pressed = ResolvePanelButtonPressedSprite();

        if (normal != null)
        {
            image.sprite = normal;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;

            SpriteState state = button.spriteState;
            state.highlightedSprite = highlighted != null ? highlighted : normal;
            state.pressedSprite = pressed != null ? pressed : normal;
            state.selectedSprite = state.highlightedSprite;
            state.disabledSprite = normal;
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.48f);
            button.colors = colors;
            return;
        }

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = new Color(0.025f, 0.115f, 0.145f, 0.92f);
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock fallbackColors = button.colors;
        fallbackColors.normalColor = Color.white;
        fallbackColors.highlightedColor = new Color(0.70f, 1f, 0.95f, 1f);
        fallbackColors.pressedColor = new Color(1f, 0.58f, 0.78f, 1f);
        fallbackColors.selectedColor = fallbackColors.highlightedColor;
        fallbackColors.disabledColor = new Color(0.34f, 0.42f, 0.45f, 0.42f);
        button.colors = fallbackColors;
    }

    private Sprite ResolvePanelButtonNormalSprite()
    {
        return panelButtonNormalSprite != null
            ? panelButtonNormalSprite
            : LoadPanelButtonSprite(PanelButtonNormalSpritePath, ref cachedPanelButtonNormalSprite);
    }

    private Sprite ResolvePanelButtonHighlightedSprite()
    {
        return panelButtonHighlightedSprite != null
            ? panelButtonHighlightedSprite
            : LoadPanelButtonSprite(PanelButtonHighlightedSpritePath, ref cachedPanelButtonHighlightedSprite);
    }

    private Sprite ResolvePanelButtonPressedSprite()
    {
        return panelButtonPressedSprite != null
            ? panelButtonPressedSprite
            : LoadPanelButtonSprite(PanelButtonPressedSpritePath, ref cachedPanelButtonPressedSprite);
    }

    private static Sprite ResolvePanelButtonHoverGlowSprite()
    {
        return LoadPanelButtonSprite(PanelButtonHoverGlowSpritePath, ref cachedPanelButtonHoverGlowSprite);
    }

    private static Sprite LoadPanelButtonSprite(string assetPath, ref Sprite cache)
    {
        if (cache != null)
            return cache;

#if UNITY_EDITOR
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#else
        Texture2D texture = null;
#endif
        if (texture == null)
            texture = RuntimeUiAssetCatalog.FindTexture(assetPath);
        if (texture != null)
        {
            cache = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                PanelButtonPixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            cache.name = texture.name;
        }
        return cache;
    }

    private static void SetPanelButtonLabelSize(Button button, int size)
    {
        if (button == null)
            return;
        Transform labelTransform = button.transform.Find("Text");
        Text label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
        if (label != null)
            ConfigurePanelButtonText(label, size);
    }

    private static void ConfigurePanelButtonText(Text label, int size)
    {
        if (label == null)
            return;

        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.alignByGeometry = true;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = false;
        label.lineSpacing = 0.88f;
        ApplyCrispCyberText(label, new Color(0f, 0.11f, 0.17f, 0.95f));
    }

    private static void ApplyCyberText(Text text, Color effectColor, Vector2 distance)
    {
        if (text == null)
            return;

        DisableOutline(text);

        Shadow shadow = FindExactShadow(text);
        if (shadow == null)
            shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = effectColor.a > 0f
            ? new Color(effectColor.r, effectColor.g, effectColor.b, Mathf.Min(effectColor.a, 0.78f))
            : new Color(0f, 0f, 0f, 0.78f);
        shadow.effectDistance = RoundTextEffectDistance(distance);
        shadow.useGraphicAlpha = true;
    }

    private static void ApplyCrispCyberText(Text text, Color effectColor)
    {
        if (text == null)
            return;

        DisableOutline(text);

        Shadow shadow = FindExactShadow(text);
        if (shadow == null)
            shadow = text.gameObject.AddComponent<Shadow>();
        shadow.enabled = false;
    }

    private static void DisableOutline(Text text)
    {
        Outline outline = text != null ? text.GetComponent<Outline>() : null;
        if (outline != null)
            outline.enabled = false;
    }

    private static Vector2 RoundTextEffectDistance(Vector2 distance)
    {
        float x = distance.x == 0f ? 0f : Mathf.Sign(distance.x);
        float y = distance.y == 0f ? 0f : Mathf.Sign(distance.y);
        return new Vector2(x, y);
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

    // =====================================================================
    // Skill-swap popup support: procedural star icons + battle-style card chrome
    // =====================================================================

    private const string SkillSwapElementIconPrefix = "UI/Elements/Element_";
    private const string SkillSwapInstructionIconPrefix = "UI/Instructions/Instruction_";
    private const string SkillSwapSelectFrameResourcePath = "UI/SkillFrame/scifi_inventory02_box_select01";

    /// <summary>
    /// Five-pointed star drawn procedurally (same approach as the battle HUD's
    /// triangle / chamfer sprites). filled = solid; otherwise a uniform-width outline.
    /// </summary>
    private static Sprite StarSprite(bool filled)
    {
        if (filled && cachedFilledStarSprite != null) return cachedFilledStarSprite;
        if (!filled && cachedHollowStarSprite != null) return cachedHollowStarSprite;

        const int size = 32;
        const int outline = 3; // outline thickness for the hollow variant
        float center = (size - 1) * 0.5f;
        float outerR = size * 0.47f;
        float innerR = outerR * 0.46f;

        var vx = new float[10];
        var vy = new float[10];
        for (int k = 0; k < 10; k++)
        {
            float ang = (90f + k * 36f) * Mathf.Deg2Rad; // first point at top
            float r = (k % 2 == 0) ? outerR : innerR;
            vx[k] = center + r * Mathf.Cos(ang);
            vy[k] = center + r * Mathf.Sin(ang);
        }

        var inside = new bool[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                inside[y * size + x] = PointInStarPolygon(x, y, vx, vy);

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool on = inside[y * size + x];
                if (on && !filled)
                    on = NearStarBoundary(inside, size, x, y, outline);
                texture.SetPixel(x, y, on ? Color.white : clear);
            }
        }
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = filled ? "FavoriteStarFilled" : "FavoriteStarHollow";
        if (filled) cachedFilledStarSprite = sprite; else cachedHollowStarSprite = sprite;
        return sprite;
    }

    private static bool PointInStarPolygon(float px, float py, float[] vx, float[] vy)
    {
        bool inside = false;
        int n = vx.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((vy[i] > py) != (vy[j] > py)) &&
                (px < (vx[j] - vx[i]) * (py - vy[i]) / (vy[j] - vy[i]) + vx[i]))
                inside = !inside;
        }
        return inside;
    }

    private static bool NearStarBoundary(bool[] inside, int size, int x, int y, int thickness)
    {
        for (int dy = -thickness; dy <= thickness; dy++)
        {
            for (int dx = -thickness; dx <= thickness; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                    return true; // star edge against the texture edge reads as outline
                if (!inside[ny * size + nx])
                    return true;
            }
        }
        return false;
    }

    private Sprite SkillSwapElementIcon(ElementType elementType)
    {
        int i = (int)elementType;
        if (i < 0 || i >= skillSwapElementIcons.Length) return null;
        if (skillSwapElementIcons[i] == null)
            skillSwapElementIcons[i] = Resources.Load<Sprite>($"{SkillSwapElementIconPrefix}{elementType}");
        return skillSwapElementIcons[i];
    }

    private Sprite SkillSwapInstructionIcon(InstructionType instructionType)
    {
        int i = (int)instructionType;
        if (i < 0 || i >= skillSwapInstructionIcons.Length) return null;
        if (skillSwapInstructionIcons[i] == null)
            skillSwapInstructionIcons[i] = Resources.Load<Sprite>($"{SkillSwapInstructionIconPrefix}{instructionType}");
        return skillSwapInstructionIcons[i];
    }

    private static Sprite SkillSwapSelectFrameSprite()
    {
        if (cachedSkillSelectFrameSprite == null)
            cachedSkillSelectFrameSprite = Resources.Load<Sprite>(SkillSwapSelectFrameResourcePath);
        return cachedSkillSelectFrameSprite;
    }

    private static Sprite SkillCardFrameSprite()
    {
        if (cachedSkillCardFrameSprite == null)
            cachedSkillCardFrameSprite = BuildChamferedSkillSprite(
                new Color(0.035f, 0.080f, 0.122f, 0.96f),
                new Color(0.10f, 0.33f, 0.46f, 1f),
                "SkillCardFrame");
        return cachedSkillCardFrameSprite;
    }

    private static Sprite BuildChamferedSkillSprite(Color fill, Color edge, string spriteName)
    {
        // Mirrors BattleHudController's chamfered panel chrome so payload skill
        // cards wear the same cyber-glass border as the battle skill slots.
        const int size = 28;
        const int chamfer = 5;
        const int border = 2;
        const int slice = chamfer + border;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int l = x, r = size - 1 - x, b = y, t = size - 1 - y;
                bool cut = (l + b < chamfer) || (r + b < chamfer) || (l + t < chamfer) || (r + t < chamfer);
                if (cut) { texture.SetPixel(x, y, clear); continue; }
                bool diagonalEdge = (l + b < chamfer + border) || (r + b < chamfer + border) ||
                                    (l + t < chamfer + border) || (r + t < chamfer + border);
                int straight = Mathf.Min(Mathf.Min(l, r), Mathf.Min(b, t));
                bool straightEdge = straight < border;
                texture.SetPixel(x, y, diagonalEdge || straightEdge ? edge : fill);
            }
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        sprite.name = spriteName;
        return sprite;
    }

    // Battle skill-tag border tints (mirrors BattleHudController's SkillTag* colors).
    private static readonly Color SkillTagCPTint      = new Color(1f, 0.70f, 0.24f, 1f);
    private static readonly Color SkillTagPowerTint   = new Color(1f, 0.62f, 0.40f, 1f);
    private static readonly Color SkillTagCounterTint = new Color(0.35f, 0.85f, 0.50f, 1f);

    private static Sprite SkillSwapPanelSprite()
    {
        if (cachedSkillSwapPanelSprite == null)
            cachedSkillSwapPanelSprite = BuildChamferedSkillSprite(
                new Color(0.014f, 0.034f, 0.056f, 0.985f),
                new Color(0.13f, 0.44f, 0.56f, 1f),
                "SkillSwapPanelFrame");
        return cachedSkillSwapPanelSprite;
    }

    /// <summary>White-bordered chamfer chip; Image.color tints the border (battle tag style).</summary>
    private static Sprite SkillChipFrameSprite()
    {
        if (cachedSkillChipFrameSprite == null)
            cachedSkillChipFrameSprite = BuildChamferedSkillSprite(
                new Color(0.045f, 0.075f, 0.10f, 0.94f),
                Color.white,
                "SkillSwapChipFrame");
        return cachedSkillChipFrameSprite;
    }

    private static Color InstructionAccentColor(InstructionType instructionType)
    {
        switch (instructionType)
        {
            case InstructionType.Attack:  return new Color(1.00f, 0.36f, 0.30f, 1f);
            case InstructionType.Defense: return new Color(0.38f, 0.86f, 1.00f, 1f);
            case InstructionType.Status:  return new Color(0.45f, 1.00f, 0.55f, 1f);
            default:                      return Color.white;
        }
    }

    private static string InstructionLetterFor(InstructionType instructionType)
    {
        switch (instructionType)
        {
            case InstructionType.Attack:  return "A";
            case InstructionType.Defense: return "D";
            case InstructionType.Status:  return "S";
            default:                      return "?";
        }
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
        int highest = ThreatTierRules.MaxTier;
        ThreatTier tier = manager.SelectedThreatTier;

        if (usingSourceLayoutTrialDepthButtons)
        {
            RefreshSourceLayoutDepthButtons(selected, highest);
            return;
        }

        if (depthTierTitleText != null)
            depthTierTitleText.text = "DEPTH_SELECT.exe";
        if (depthTierDetailText != null)
            depthTierDetailText.text = "SELECT ROUTE DEPTH / ENEMY BAND / BOSS TARGET";
        if (depthTierAvatarImage != null)
        {
            depthTierAvatarImage.sprite = ResolveDepthTierSprite(selected);
            depthTierAvatarImage.enabled = depthTierAvatarImage.sprite != null;
            depthTierAvatarImage.color = Color.white;
        }
        if (depthTierSelectedSummaryText != null)
            depthTierSelectedSummaryText.text = $"SELECTED DEPTH: {selected}F\nROUTE {GridGenerationSettings.TotalLayerRangeLabel(selected)} LAYERS / ENEMY LV {ThreatTierRules.MinLevel(tier):00}-{ThreatTierRules.MaxLevel(tier):00}";
        if (depthTierRewardSummaryText != null)
        {
            depthTierRewardSummaryText.text = "REWARDS\nALGOMON EXP / CREDITS / FORM DATA";
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

            Text routeLayersLabel = button.transform.Find("RouteLayers")?.GetComponent<Text>();
            if (routeLayersLabel == null)
                routeLayersLabel = EnsureDepthTierRouteLayersLabel(button.transform, buttonTier);
            if (routeLayersLabel != null)
            {
                routeLayersLabel.gameObject.SetActive(true);
                routeLayersLabel.text = BuildDepthTierRouteLayersText(buttonTier);
                routeLayersLabel.color = isSelected
                    ? CyberUiTheme.WithAlpha(CyberUiTheme.Selected, 0.98f)
                    : (unlocked ? CyberUiTheme.WithAlpha(CyberUiTheme.TextSecondary, 0.90f) : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.52f));
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

        ApplySourceLayoutStaticLabelBitmaps();

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

    private static TextMeshProUGUI CachedBitmapText(TextMeshProUGUI[] texts, int index)
    {
        return texts != null && index >= 0 && index < texts.Length ? texts[index] : null;
    }

    private static void SetBossRouteBitmapText(TextMeshProUGUI bitmapText, string value, Color color)
    {
        if (bitmapText == null)
            return;

        bitmapText.text = value;
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

        targetManager.EnsureRosterState();
        int partyCount = targetManager.party != null ? targetManager.party.Count : 0;
        int payloadCount = targetManager.payload != null ? targetManager.payload.Count : 0;
        if (partyCount > 0 || payloadCount > 0)
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

        EnsureInitialPayloadPage(targetManager);
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

    private static void EnsureInitialPayloadPage(GameManager targetManager)
    {
        if (targetManager == null || targetManager.payload == null)
            return;
        if (targetManager.payload.Count >= InitialPayloadFillCount)
            return;

        List<AlgoMonData> candidates = LoadPartyCandidateSpecies();
        if (candidates.Count == 0)
            return;

        int guard = InitialPayloadFillCount * 2;
        while (targetManager.payload.Count < InitialPayloadFillCount && guard-- > 0)
        {
            int payloadSlot = targetManager.payload.Count;
            AlgoMonData species = FindInitialPayloadSpecies(candidates, payloadSlot);
            if (species == null)
                break;

            AlgoMonInstance mon = CreateInitialPayloadMon(species, payloadSlot);
            if (mon == null)
                break;

            targetManager.AddToPayload(mon);
        }
    }

    private static AlgoMonData FindInitialPayloadSpecies(List<AlgoMonData> candidates, int payloadSlot)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        if (PreferredReserveSpecies.Length > 0)
        {
            string preferred = PreferredReserveSpecies[Mathf.Abs(payloadSlot) % PreferredReserveSpecies.Length];
            for (int i = 0; i < candidates.Count; i++)
            {
                AlgoMonData candidate = candidates[i];
                if (candidate != null &&
                    string.Equals(NormalizedCodeName(candidate), preferred, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return candidates[Mathf.Abs(payloadSlot) % candidates.Count];
    }

    private static AlgoMonInstance CreateInitialPayloadMon(AlgoMonData species, int payloadSlot)
    {
        int seed = InitialPayloadSeed(payloadSlot, species);
        AlgoMonInstance mon = AlgoMonInstance.CreateRewardBase(species, RewardDataQuality.Base, seed);
        if (mon == null)
            return null;

        mon.level = StarterLevel;
        mon.exp = 0;
        mon.battleFormName = "Base";
        mon.fusedBaseCopies = 0;
        mon.EnsurePersistentRuntimeState();
        mon.EnsureKnownSkillsFromLearnset();
        return mon;
    }

    private static int InitialPayloadSeed(int payloadSlot, AlgoMonData species)
    {
        unchecked
        {
            int hash = 0x4A11C0DE;
            hash = hash * 397 ^ payloadSlot;
            string codeName = NormalizedCodeName(species);
            for (int i = 0; i < codeName.Length; i++)
                hash = hash * 397 ^ codeName[i];
            return hash;
        }
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
