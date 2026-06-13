/*
Script Audit:
- Purpose: Provides the runtime API for all TheArena HUD display and button input.
- Attached GameObject: BattleHud prefab root or TheArena Canvas_Arena object.
- Main responsibilities: Bind UI references, raise skill/action click events, update names/levels/HP/CP/status, render skill/switch slots, show hover details, animate CP/HP and the battle announcer.
- Important variables: SkillSlotClicked, ActionClicked, player, enemy, skillButtons, action buttons, skillHoverTitles, skillHoverBodies, announcerTitleText, roundSandclockImage.
- Inputs: Button clicks, hover events, BattleManager state updates, SkillData, and UI sprites/fonts.
- Outputs or effects: Updates visible HUD text/images and sends player choices back to BattleManager.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: In battle, verify all four skill slots, Recharge/Switch/Flee icon buttons, HP/CP bars, hover details, and announcer updates.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime API surface for the battle HUD. BattleManager (#15) drives every
/// visible value in TheArena through this class: names, HP, CP, status, skill
/// button labels, and the round / state header.
///
/// The HUD layout lives in BattleHud.prefab. This controller caches references
/// to prefab children by stable node names and exposes a clean API on top.
///
/// Attach to the HUD prefab root (Canvas_Arena in TheArena). Bind() is called
/// once in Start and can be called again after intentional hierarchy changes.
/// </summary>
[DisallowMultipleComponent]
public class BattleHudController : MonoBehaviour
{
    public enum Side          { Player, Enemy }
    public enum ActionButton  { Recharge, Switch, Flee }

    /// <summary>
    /// Tone of a status chip on the combatant cards: green = ready/counter,
    /// blue = shields/buffs, red = damage/ailments, gray = informational.
    /// </summary>
    public enum StatusChipTone { Ready, Buff, Harm, Info }

    public struct StatusChip
    {
        public string Label;
        public StatusChipTone Tone;

        public StatusChip(string label, StatusChipTone tone)
        {
            Label = label;
            Tone = tone;
        }
    }

    public event Action<int>          SkillSlotClicked;
    public event Action<ActionButton> ActionClicked;
    public event Action               PostBattleContinueClicked;

    public bool IsBound { get; private set; }

    private const int MaxSkillSlots = 4;
    private const int MaxCP         = 10;
    private const int SkillNameMaxCharacters = 15;
    private const string PlayerTurnStateText = "Player turn";
    private const string AnnouncerPanelResourcePath = "UI/BattleAnnouncer_GreenPanel";
    private const string AnnouncerFontResourcePath = "Fonts/NicoBold-Regular";
    private const string SkillButtonFrameResourcePath = "UI/SkillFrame/scifi_inventory01_box_back";
    private const string SkillInsetFrameResourcePath = "UI/SkillFrame/scifi_inventory01_box";
    private const string SkillTagFrameResourcePath = "UI/SkillFrame/scifi_inventory02_box_select01";
    private const string SkillPanelBackdropResourcePath = "UI/SkillFrame/inventory_example_02_four_rows_soft";
    private const string ElementIconResourcePrefix = "UI/Elements/Element_";
    private const string InstructionIconResourcePrefix = "UI/Instructions/Instruction_";
    private const string SkillSelectFrameResourcePath = "UI/SkillFrame/scifi_inventory02_box_select01";
    private const string AnnouncementBannerResourcePath = "UI/Banners/TitleBanner";
    private const string ActionBannerPlayerResourcePath = "UI/Banners/TitleBannerDecoratorB_Blue";
    private const string ActionBannerEnemyResourcePath = "UI/Banners/TitleBannerDecoratorB_Red";
    private const string ZapIconResourcePath = "UI/Icons/zap";
    private const string SkillDetailPanelPath = "SafeArea/CommandPanel/SkillDetailPanel";
    private const string LegacySkillDetailPanelPath = "SafeArea/CommandPanel/ActionPanel/SkillDetailPanel";

    // Default text shown in the Skill Details panel when no button is hovered.
    private const string DefaultSkillDetailTitle = "SKILL TRACE";
    private const string DefaultSkillDetailBody  = "Hover a skill to inspect its full description.";

    // CP dot palette used by both prefab defaults and live HUD updates.
    private static readonly Color32 CPDotActive   = new Color32(120, 235, 244, 255);
    private static readonly Color32 CPDotInactive = new Color32(120, 235, 244,   0);

    // Battery feedback layers: warm-white afterimage where HP just was, plus
    // warning washes laid over the live fill once charge runs low.
    private static readonly Color BatteryGhostColor = new Color(1f, 0.93f, 0.84f, 0.85f);
    private static readonly Color BatteryWarnWash   = new Color(1f, 0.70f, 0.18f, 0.30f);
    private static readonly Color BatteryDangerWash = new Color(1f, 0.24f, 0.16f, 0.42f);
    private static readonly Color BatteryDangerText = new Color(1f, 0.55f, 0.50f, 1f);

    // Skill-slot element preview vs the current opponent, and CP affordability.
    private static readonly Color EffectivenessStrongColor = new Color(0.45f, 1f, 0.55f, 1f);
    private static readonly Color EffectivenessWeakColor   = new Color(1f, 0.25f, 0.22f, 1f);
    private static readonly Color SkillTagTextColor        = new Color(0.92f, 1f, 0.94f, 1f);
    private static readonly Color PowerTagTextColor        = new Color(1f, 0.72f, 0.55f, 1f);
    private static readonly Color CPShortfallColor         = new Color(1f, 0.42f, 0.42f, 1f);
    private static readonly Color HotkeyHintColor          = new Color(0.62f, 0.92f, 1f, 0.85f);
    private static readonly Color BatteryTickColor         = new Color(0.02f, 0.06f, 0.10f, 0.55f);
    private static Sprite whitePixelSprite;
    private static Sprite triangleUpSprite;
    private static Sprite triangleDownSprite;
    private static Sprite chipFrameSprite;

    // Status-chip palette: border colour tints the white-bordered chip frame,
    // text is a lighter sibling of the same hue. Green = ready/counter,
    // blue = shields/buffs, red = damage/ailments, gray = informational.
    // The skill tags reuse the same chip language (CP blue, PWR warm, CNT green).
    private static readonly Color ChipReadyBorder = new Color(0.30f, 0.85f, 0.45f, 1f);
    private static readonly Color ChipReadyText   = new Color(0.62f, 1f, 0.72f, 1f);
    private static readonly Color ChipBuffBorder  = new Color(0.30f, 0.62f, 1f, 1f);
    private static readonly Color ChipBuffText    = new Color(0.68f, 0.86f, 1f, 1f);
    private static readonly Color ChipHarmBorder  = new Color(1f, 0.32f, 0.30f, 1f);
    private static readonly Color ChipHarmText    = new Color(1f, 0.60f, 0.56f, 1f);
    private static readonly Color ChipInfoBorder  = new Color(0.55f, 0.66f, 0.72f, 1f);
    private static readonly Color ChipInfoText    = new Color(0.80f, 0.88f, 0.92f, 1f);
    private static readonly Color SkillTagCPBorder      = new Color(0.32f, 0.64f, 1f, 1f);
    private static readonly Color SkillTagPowerBorder   = new Color(1f, 0.62f, 0.40f, 1f);
    private static readonly Color SkillTagCounterBorder = new Color(0.35f, 0.85f, 0.50f, 1f);
    private const int MaxStatusChips = 5;
    private static readonly Vector2 AnnouncerAnchorMin = new Vector2(0.30f, 0.805f);
    private static readonly Vector2 AnnouncerAnchorMax = new Vector2(0.70f, 0.925f);

    // --- Unified HUD panel chrome (dark translucent fill + cyan pixel border) ---
    // A single procedural 9-slice sprite is shared by every battle panel so the
    // skill bar, top prompt, status cards, and log/action area read as one
    // cyber-glass family. Generated once and cached for the editor session.
    private static readonly Color HudPanelFill   = new Color(0.039f, 0.063f, 0.090f, 0.93f);
    private static readonly Color32 HudPanelBorder = new Color32(78, 206, 230, 255);
    private static readonly Color HudPanelGlow   = new Color(0.30f, 0.80f, 0.94f, 0.45f);
    private static Sprite hudPanelSprite;

    [Header("Resource Animation")]
    [SerializeField, Min(0f)] private float batteryLerpSpeed = 8f;
    [SerializeField, Min(0f)] private float cpLerpSpeed = 12f;
    // Damage afterimage: when battery drops, a pale ghost layer lingers at the
    // old value for a beat, then drains slowly so the size of the hit reads.
    [SerializeField, Min(0f)] private float batteryGhostHoldSeconds = 0.45f;
    [SerializeField, Min(0f)] private float batteryGhostDrainSpeed = 2.4f;
    // Low-battery warning thresholds (fraction of max battery).
    [SerializeField, Range(0f, 1f)] private float lowBatteryWarnRatio = 0.5f;
    [SerializeField, Range(0f, 1f)] private float lowBatteryDangerRatio = 0.25f;

    [Header("Round Prompt Animation")]
    [SerializeField] private Image roundSandclockImage;
    [SerializeField] private Sprite[] roundSandclockFrames = Array.Empty<Sprite>();
    [SerializeField, Min(0.01f)] private float roundSandclockFrameSeconds = 0.14f;

    [Header("Battle Announcer")]
    [SerializeField] private Text announcerTitleText;
    [SerializeField] private Text announcerBodyText;
    [SerializeField] private Image announcerFrame;
    [SerializeField] private Sprite announcerPanelSprite;
    [SerializeField] private Font announcerFont;
    private Font readableHudFont;
    [SerializeField] private bool autoCreateAnnouncer = true;
    [SerializeField, Min(0f)] private float announcerPulseSeconds = 0.18f;

    [Header("Skill Slot Skin")]
    [SerializeField] private Sprite skillButtonFrameSprite;
    [SerializeField] private Sprite skillInsetFrameSprite;
    [SerializeField] private Sprite skillTagFrameSprite;
    [SerializeField] private Sprite skillPanelBackdropSprite;
    private Sprite announcementBannerSprite;
    private Sprite actionBannerPlayerSprite;
    private Sprite actionBannerEnemySprite;
    private Sprite zapIconSprite;

    // Placeholder hover bodies for the default Sortex loadout baked into the
    // prefab. Keyed by skill name so layout edits that change slot order still
    // pick up the right description. BattleManager replaces these via
    // SetSkillSlot(SkillData) once the real loadout is live.
    private static readonly System.Collections.Generic.Dictionary<string, string> DefaultSkillHoverBodies =
        new System.Collections.Generic.Dictionary<string, string>
    {
        { "Volt Array",      "CP 4 | BP 50\nReliable Electric attack.\nNo counter effect." },
        { "Faraday Cage",    "CP 2 | Counter\nDefense skill. Reduces incoming damage when it wins the matchup." },
        { "Auto-Tuning",     "CP 2\nStatus skill. Raises Computing Power." },
        { "Hyper-Threading", "CP 2\nStatus skill. Next skill fires twice." },
    };

    // --- Top bar refs ---
    private Text roundText;
    private Text battleStateText;
    private bool roundSandclockPlaying;
    private float roundSandclockTimer;
    private int roundSandclockFrameIndex;
    private float announcerPulseTimer;
    private bool skillPanelPresentationApplied;
    private bool skillPanelSlotLayoutApplied;
    private readonly Color announcerFrameBaseColor = new Color(0.035f, 0.075f, 0.11f, 0.90f);
    private readonly Color announcerFramePulseColor = new Color(0.18f, 0.72f, 0.92f, 0.96f);

    // --- Per-side refs ---
    private struct CombatantRefs
    {
        public Text    NameText;
        public Text    LevelText;
        public Text    BatteryValueText;
        public Image   BatteryFill;
        public Image   BatteryGhost;
        public Image   BatteryWash;
        public Image   ElementIcon;
        public Image[] CPDots;
        public Text    CPValueText;
        public Text    StatusText;
        public Image[] StatusChips;
        public Text[]  StatusChipTexts;
        public Text    SubroutineLabel;
    }
    private CombatantRefs player;
    private CombatantRefs enemy;

    private struct CombatantDisplayState
    {
        public bool BatteryInitialized;
        public int TargetBattery;
        public int TargetBatteryMax;
        public float DisplayBattery;
        public float GhostBattery;
        public float GhostHoldTimer;

        public bool CPInitialized;
        public int TargetCP;
        public int TargetCPMax;
        public float DisplayCP;
    }
    private CombatantDisplayState playerDisplay;
    private CombatantDisplayState enemyDisplay;

    // --- Skill button refs (index 0..3) ---
    private readonly Button[] skillButtons     = new Button[MaxSkillSlots];
    private readonly Text[]   skillNameTexts   = new Text  [MaxSkillSlots];
    private readonly Text[]   skillCPTexts     = new Text  [MaxSkillSlots];
    private readonly Text[]   skillPowerTexts  = new Text  [MaxSkillSlots];
    private readonly Text[]   skillCounterTexts= new Text  [MaxSkillSlots];
    private readonly Image[]  skillInstructionFrames = new Image[MaxSkillSlots];
    private readonly Text[]   skillInstructionTexts  = new Text [MaxSkillSlots];
    private readonly Image[]  skillInstructionIcons  = new Image[MaxSkillSlots];
    private readonly Image[]  skillElementBadges     = new Image[MaxSkillSlots];
    private readonly Image[]  skillElementIconImages = new Image[MaxSkillSlots];
    private readonly Text[]   skillElementTexts      = new Text [MaxSkillSlots];
    private readonly Sprite[] elementIconSprites      = new Sprite[7];
    private readonly Sprite[] instructionIconSprites  = new Sprite[3];
    private Sprite skillSelectFrameSprite;
    private readonly GameObject[] skillCPTagObjects      = new GameObject[MaxSkillSlots];
    private readonly GameObject[] skillPowerTagObjects   = new GameObject[MaxSkillSlots];
    private readonly GameObject[] skillCounterTagObjects = new GameObject[MaxSkillSlots];
    private readonly Image[]      switchPortraitImages   = new Image[MaxSkillSlots];
    private readonly Image[]      switchElementChipIcons = new Image[MaxSkillSlots];
    private readonly SwitchSlotDetail[] switchSlotDetails = new SwitchSlotDetail[MaxSkillSlots];

    // Element identity strip + effectiveness preview + keyboard hints.
    private readonly Image[]      skillTypeStrips             = new Image[MaxSkillSlots];
    private readonly GameObject[] skillEffectivenessTagObjects = new GameObject[MaxSkillSlots];
    private readonly Image[]      skillEffectivenessIcons      = new Image[MaxSkillSlots];
    private readonly Text[]       skillEffectivenessTexts      = new Text[MaxSkillSlots];
    private readonly Text[]       skillHotkeyTexts             = new Text[MaxSkillSlots];
    private readonly ElementType[] skillSlotElements           = new ElementType[MaxSkillSlots];
    private readonly bool[]        skillSlotDealsDamage        = new bool[MaxSkillSlots];
    private readonly bool[]        skillSlotShowsMatchup       = new bool[MaxSkillSlots];
    private readonly bool[]        skillSlotIsSwitch           = new bool[MaxSkillSlots];
    private ElementType opposingElement = ElementType.Normal;
    private bool opposingElementKnown;

    private static readonly KeyCode[] SkillHotkeys =
        { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
    private static readonly KeyCode[] SkillHotkeysKeypad =
        { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4 };

    private struct SwitchSlotDetail
    {
        public bool HasData;
        public string DisplayName;
        public ElementType ElementType;
        public int Level;
        public int CurrentBattery;
        public int MaxBattery;
        public int CurrentCP;
        public int MaxCP;
        public string StateText;
        public string StatusSummary;
    }

    // --- Action button refs ---
    private Button rechargeButton;
    private Button switchButton;
    private Button fleeButton;

    // --- Hover content for the Skill Details panel (per skill / action slot) ---
    // Lambda listeners aren't serialized into the HUD prefab, so the hover wiring
    // is rebuilt every Bind(); these arrays back the text it displays on enter.
    private readonly string[] skillHoverTitles  = new string[MaxSkillSlots];
    private readonly string[] skillHoverBodies  = new string[MaxSkillSlots];
    private readonly string[] actionHoverTitles = new string[3];
    private readonly string[] actionHoverBodies = new string[3];

    // Per-side subroutine card text, shown in the skill detail panel when the
    // player hovers or clicks a combatant card. Pushed in by BattleManager.
    private struct SubroutineCardData { public bool Has; public string Title; public string Body; }
    private SubroutineCardData playerSubroutine;
    private SubroutineCardData enemySubroutine;
    private readonly int[]    skillCPCosts      = new int[MaxSkillSlots];
    private bool cpPreviewActive;
    private int cpPreviewCost;

    // --- Skill details panel ---
    private Transform skillDetailPanel;
    private Text skillDetailTitle;
    private Text skillDetailBody;
    private GameObject switchDetailRoot;
    private Image switchDetailElementIcon;
    private Text switchDetailNameText;
    private Text switchDetailMetaText;
    private Text switchDetailBatteryValueText;
    private Image switchDetailBatteryFill;
    private Text switchDetailCPValueText;
    private Image switchDetailCPFill;
    private Text switchDetailStatusText;
    private string restingDetailTitle = DefaultSkillDetailTitle;
    private string restingDetailBody  = DefaultSkillDetailBody;

    // Runtime-created overlay shown after a node is cleared. BattleManager
    // controls the content and waits for the Continue click before scene travel.
    private GameObject postBattlePanel;
    private Text postBattleTitleText;
    private Text postBattleBodyText;
    private Button postBattleContinueButton;

    // --- Combat-UI visibility + counter cut-in overlay (runtime-built) ---
    // While a round resolves the skill bar / action buttons / top bar fade out so
    // animations play clean; the status cards stay up. The counter cut-in is a
    // flash + translucent letterbox banner shown over the arena.
    private CanvasGroup commandPanelGroup;
    private CanvasGroup topBarGroup;
    private GameObject counterCutInRoot;
    private CanvasGroup counterCutInGroup;
    private Image counterFlashImage;
    private Image counterBand;
    private Image counterTopEdge;
    private Image counterBottomEdge;
    private Text counterBannerText;
    private Image counterStatusImage;
    private Sprite[] counterStatusFrames;
    private float counterStatusSecondsPerFrame;
    private Coroutine counterCutInRoutine;

    // Skill announcement banner (TitleBanner sprite) pinned to the acting side.
    private GameObject actionBannerRoot;
    private CanvasGroup actionBannerGroup;
    private RectTransform actionBannerRect;
    private Image actionBannerBg;
    private Text actionBannerText;
    private Coroutine actionBannerRoutine;

    private void Start()
    {
        if (!IsBound)
            Bind();
    }

    private void Update()
    {
        UpdateResourceDisplays();
        UpdateRoundSandclockAnimation();
        UpdateAnnouncerPulse();
        EnsureSkillPanelPresentation();
        HandleSkillHotkeys();
    }

    private void OnDestroy()
    {
        UnhookButtons();
    }

    /// <summary>
    /// Re-resolve all child references and re-wire button click events. Safe
    /// to call multiple times; old listeners are removed before re-adding.
    /// BattleHud.prefab keeps stable CP / BP / Counter tag roots on every
    /// skill slot, and SetSkillSlot toggles them from live SkillData.
    /// </summary>
    public void Bind()
    {
        UnhookButtons();

        // Canvas_Arena's children all live under SafeArea.
        roundText        = FindText("SafeArea/TopBar/RoundText");
        battleStateText  = FindText("SafeArea/TopBar/BattleStateText");
        if (roundSandclockImage == null)
            roundSandclockImage = Find<Image>("SafeArea/TopBar/RoundSandclock");
        ConfigureRoundSandclockLayout();
        ApplyRoundSandclockState();
        EnsureBattleAnnouncer();
        EnsurePostBattlePanel();
        HidePostBattlePanel();

        player = BindCombatant("SafeArea/CombatLayer/PlayerCombatantPanel");
        enemy  = BindCombatant("SafeArea/CombatLayer/EnemyCombatantPanel");
        playerDisplay = default;
        enemyDisplay = default;
        commandPanelGroup = EnsureCanvasGroup("SafeArea/CommandPanel");
        ApplyUnifiedPanelChrome();
        EnsureSkillPanelPresentation();

        for (int i = 0; i < MaxSkillSlots; i++)
        {
            int slot = i;
            string root = $"SafeArea/CommandPanel/SkillPanel/SkillGrid/SkillButton_{i + 1}";
            skillButtons[i]       = Find<Button>(root);
            skillNameTexts[i]     = FindText($"{root}/SkillNameText");
            skillCPTagObjects[i]      = FindTransform($"{root}/CPTag")?.gameObject;
            skillPowerTagObjects[i]   = FindTransform($"{root}/PowerTag")?.gameObject;
            skillCounterTagObjects[i] = FindTransform($"{root}/CounterTag")?.gameObject;
            skillCPTexts[i]       = FindTextDeep(root, "CPTag/Text");
            skillPowerTexts[i]    = FindTextDeep(root, "PowerTag/Text");
            skillCounterTexts[i]  = FindTextDeep(root, "CounterTag/Text");
            EnsureSkillSlotPresentation(i, FindTransform(root));

            if (skillButtons[i] != null)
                skillButtons[i].onClick.AddListener(() =>
                {
                    ClearCPPreview();
                    HideSkillDetailPanel();
                    SkillSlotClicked?.Invoke(slot);
                });
        }

        rechargeButton = Find<Button>("SafeArea/CommandPanel/ActionPanel/ActionGrid/RechargeButton");
        switchButton   = Find<Button>("SafeArea/CommandPanel/ActionPanel/ActionGrid/SwitchButton");
        fleeButton     = Find<Button>("SafeArea/CommandPanel/ActionPanel/ActionGrid/FleeButton");

        if (rechargeButton != null) rechargeButton.onClick.AddListener(() => ActionClicked?.Invoke(ActionButton.Recharge));
        if (switchButton   != null) switchButton.onClick.AddListener  (() => ActionClicked?.Invoke(ActionButton.Switch));
        if (fleeButton     != null) fleeButton.onClick.AddListener    (() => ActionClicked?.Invoke(ActionButton.Flee));

        ConfigureActionGridLayout();
        EnsureActionButtonPresentation(rechargeButton, "RECHARGE");
        EnsureActionButtonPresentation(switchButton, "SWITCH");
        EnsureActionButtonPresentation(fleeButton, "FLEE");

        EnsureSkillPanelPresentation();

        skillDetailPanel = FindSkillDetailPanelTransform();
        skillDetailTitle = FindSkillDetailText("TitleText");
        skillDetailBody  = FindSkillDetailText("BodyText");
        ConfigureSkillDetailPanel();
        ConfigureSkillDetailText();
        WriteSkillDetail(restingDetailTitle, restingDetailBody);
        HideSkillDetailPanel();

        WireHoverPreviews();
        WireCombatantSubroutine(Side.Player, "SafeArea/CombatLayer/PlayerCombatantPanel");
        WireCombatantSubroutine(Side.Enemy, "SafeArea/CombatLayer/EnemyCombatantPanel");

        IsBound = true;
    }

    /// <summary>
    /// (Re)wires hover preview behaviour on every skill / action button. Called
    /// from Bind(). Auto-initialises any unset hover slot from the button's
    /// current label so basic hover works out of the box; BattleManager can
    /// override per slot via SetSkillHover / SetActionHover.
    /// </summary>
    private void WireHoverPreviews()
    {
        for (int i = 0; i < MaxSkillSlots; i++)
        {
            int slot = i;
            if (string.IsNullOrEmpty(skillHoverTitles[i]) && skillNameTexts[i] != null)
                skillHoverTitles[i] = skillNameTexts[i].text;
            if (string.IsNullOrEmpty(skillHoverBodies[i]))
            {
                if (skillHoverTitles[i] != null && DefaultSkillHoverBodies.TryGetValue(skillHoverTitles[i], out string defaultBody))
                    skillHoverBodies[i] = defaultBody;
                else
                    skillHoverBodies[i] = string.Empty;
            }

            WireHoverInternal(
                skillButtons[i],
                () => skillHoverTitles[slot] ?? string.Empty,
                () => skillHoverBodies[slot] ?? string.Empty,
                () => ShowCPPreview(skillCPCosts[slot]),
                ClearCPPreview,
                () => ShowSkillSlotDetail(slot));
        }

        ApplyActionHoverDefaults();

        WireHoverInternal(rechargeButton, () => actionHoverTitles[0], () => actionHoverBodies[0]);
        WireHoverInternal(switchButton,   () => actionHoverTitles[1], () => actionHoverBodies[1]);
        WireHoverInternal(fleeButton,     () => actionHoverTitles[2], () => actionHoverBodies[2]);
    }

    private void ApplyActionHoverDefaults()
    {
        // Mirrors the placeholder copy baked into BattleHud.prefab.
        // BattleManager can override at any time via SetActionHover.
        if (string.IsNullOrEmpty(actionHoverTitles[0])) { actionHoverTitles[0] = "Recharge"; actionHoverBodies[0] = "+5 CP\nSpend the turn to restore CP."; }
        if (string.IsNullOrEmpty(actionHoverTitles[1])) { actionHoverTitles[1] = "Switch";   actionHoverBodies[1] = "Change the active AlgoMon."; }
        if (string.IsNullOrEmpty(actionHoverTitles[2])) { actionHoverTitles[2] = "Flee";     actionHoverBodies[2] = "Attempt to escape from battle."; }
    }

    // Action icon button palette: dark terminal glass, with each command tinted
    // by intent (Recharge=CP amber, Switch=system cyan, Flee=danger magenta).
    private static readonly Color ActionButtonFill         = new Color(0.035f, 0.060f, 0.090f, 0.96f);
    private static readonly Color ActionButtonFillHover    = new Color(0.050f, 0.105f, 0.140f, 0.98f);
    private static readonly Color ActionButtonFillPressed  = new Color(0.070f, 0.145f, 0.180f, 1f);
    private static readonly Color ActionButtonFillDisabled = new Color(0.045f, 0.050f, 0.062f, 0.88f);
    private static readonly Color ActionIconColor          = new Color(1f, 1f, 1f, 0.88f);
    private static readonly Color ActionIconDisabledColor  = new Color(1f, 1f, 1f, 0.26f);
    private static readonly Color SkillSlotBorderColor     = new Color(0.66f, 0.97f, 1f, 1f);

    /// <summary>
    /// Wires an icon action button (Recharge / Switch / Flee) with the shared
    /// hover/press feedback. The square button visuals (frame, strip, icon
    /// sprite) are authored in BattleHud.prefab; this only animates them.
    /// </summary>
    private void EnsureActionButtonPresentation(Button button, string label)
    {
        if (button == null)
            return;

        Transform root = button.transform;
        Image background = button.GetComponent<Image>();
        Transform iconTransform = root.Find("Icon");
        Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        Color accent = ActionAccentColor(label);

        // Icon rides the upper portion; a compact label pins the bottom so the
        // commands remain readable without becoming a second skill bar.
        if (icon != null)
            SetStretchRect(icon.rectTransform, new Vector2(0.21f, 0.34f), new Vector2(0.79f, 0.92f));

        Text caption = EnsureChildText(root, "Caption", 13, TextAnchor.MiddleCenter);
        SetStretchRect(caption.rectTransform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.29f));
        caption.text = label;
        caption.font = ReadableHudFont();
        caption.fontStyle = FontStyle.Bold;
        caption.fontSize = 13;
        caption.alignment = TextAnchor.MiddleCenter;
        caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        caption.verticalOverflow = VerticalWrapMode.Overflow;
        caption.resizeTextForBestFit = true;
        caption.resizeTextMinSize = 9;
        caption.resizeTextMaxSize = 14;
        caption.color = Color.Lerp(new Color(0.86f, 0.96f, 1f, 0.95f), accent, 0.18f);
        caption.raycastTarget = false;
        EnsureShadow(caption.gameObject, new Color(0f, 0f, 0f, 0.7f), new Vector2(1f, -1f));

        Image pressOverlay = EnsureChildImage(root, "PressFlash");
        SetStretchRect(pressOverlay.rectTransform, Vector2.zero, Vector2.one);
        ConfigureChromeChip(pressOverlay, 2.2f);
        pressOverlay.transform.SetAsLastSibling();

        BattleHudButtonFeedback feedback = button.GetComponent<BattleHudButtonFeedback>();
        if (feedback == null)
            feedback = button.gameObject.AddComponent<BattleHudButtonFeedback>();
        feedback.Configure(button, 1.06f, 0.93f);
        if (background != null)
        {
            feedback.SetBackground(
                background,
                Color.Lerp(ActionButtonFill, accent, 0.10f),
                Color.Lerp(ActionButtonFillHover, accent, 0.24f),
                Color.Lerp(ActionButtonFillPressed, accent, 0.36f),
                ActionButtonFillDisabled);
        }
        if (icon != null)
            feedback.SetIcon(icon, Color.Lerp(ActionIconColor, accent, 0.22f), Color.white, ActionIconDisabledColor);
        feedback.SetOverlay(pressOverlay, accent, 0.06f, 0.16f, 0.45f);
    }

    private static Color ActionAccentColor(string label)
    {
        string key = string.IsNullOrEmpty(label) ? string.Empty : label.ToUpperInvariant();
        switch (key)
        {
            case "RECHARGE": return new Color(1.00f, 0.61f, 0.21f, 1f);
            case "SWITCH":   return new Color(0.10f, 0.85f, 1.00f, 1f);
            case "FLEE":     return new Color(1.00f, 0.23f, 0.53f, 1f);
            default:         return new Color(0.55f, 1.00f, 0.94f, 1f);
        }
    }

    private void ConfigureActionGridLayout()
    {
        Transform gridTransform = FindTransform("SafeArea/CommandPanel/ActionPanel/ActionGrid");
        if (gridTransform == null)
            return;

        RectTransform gridRect = gridTransform.GetComponent<RectTransform>();
        SetStretchRect(gridRect, Vector2.zero, Vector2.one);

        GridLayoutGroup layout = gridTransform.GetComponent<GridLayoutGroup>();
        if (layout == null)
            layout = gridTransform.gameObject.AddComponent<GridLayoutGroup>();

        layout.enabled = true;
        layout.cellSize = new Vector2(88f, 88f);
        layout.spacing = new Vector2(0f, 9f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 1;
        layout.startAxis = GridLayoutGroup.Axis.Vertical;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = new RectOffset(0, 0, 0, 0);

        ConfigureActionButtonLayoutElement(rechargeButton);
        ConfigureActionButtonLayoutElement(switchButton);
        ConfigureActionButtonLayoutElement(fleeButton);
    }

    private static void ConfigureActionButtonLayoutElement(Button button)
    {
        if (button == null)
            return;

        LayoutElement element = button.GetComponent<LayoutElement>();
        if (element == null)
            element = button.gameObject.AddComponent<LayoutElement>();

        element.minWidth = 88f;
        element.minHeight = 88f;
        element.preferredWidth = 88f;
        element.preferredHeight = 88f;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;
    }

    private void WireHoverInternal(
        Button button,
        Func<string> titleGetter,
        Func<string> bodyGetter,
        Action onEnter = null,
        Action onExit = null,
        Action showOverride = null)
    {
        if (button == null) return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        trigger.triggers.Clear();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (showOverride != null)
                showOverride();
            else
                ShowSkillDetail(titleGetter(), bodyGetter());
            onEnter?.Invoke();
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            HideSkillDetailPanel();
            onExit?.Invoke();
        });
        trigger.triggers.Add(exit);
    }

    private void ShowCPPreview(int cost)
    {
        cpPreviewCost = Mathf.Clamp(cost, 0, MaxCP);
        cpPreviewActive = cpPreviewCost > 0;
        ApplyCPVisual(player, playerDisplay.DisplayCP, playerDisplay.TargetCPMax, true);
    }

    private void ClearCPPreview()
    {
        if (!cpPreviewActive && cpPreviewCost <= 0)
            return;

        cpPreviewActive = false;
        cpPreviewCost = 0;
        ApplyCPVisual(player, playerDisplay.DisplayCP, playerDisplay.TargetCPMax, true);
    }

    // ----- Public API -----

    public void SetRound(int round)
    {
        if (roundText != null) roundText.text = $"Round {round}";
    }

    public void SetBattleState(string text)
    {
        if (battleStateText != null) battleStateText.text = text;
        SetRoundSandclockActive(string.Equals(text, PlayerTurnStateText, StringComparison.OrdinalIgnoreCase));
    }

    public void SetBattleAnnouncement(string title, string body)
    {
        // No-op: the top announcer board was replaced by above-sprite action
        // callouts. Battle narration still streams into the Skill Details log.
        // Retained so existing BattleManager calls stay valid.
    }

    public void ShowPostBattlePanel(string title, string body, string continueLabel = "CONTINUE")
    {
        EnsurePostBattlePanel();
        if (postBattlePanel == null)
            return;

        if (postBattleTitleText != null)
            postBattleTitleText.text = string.IsNullOrWhiteSpace(title) ? "NODE CLEARED" : title.Trim().ToUpperInvariant();
        if (postBattleBodyText != null)
            postBattleBodyText.text = body ?? string.Empty;

        Text buttonText = postBattleContinueButton != null
            ? postBattleContinueButton.GetComponentInChildren<Text>(true)
            : null;
        if (buttonText != null)
            buttonText.text = string.IsNullOrWhiteSpace(continueLabel) ? "CONTINUE" : continueLabel.Trim().ToUpperInvariant();

        postBattlePanel.SetActive(true);
        postBattlePanel.transform.SetAsLastSibling();
    }

    public void HidePostBattlePanel()
    {
        if (postBattlePanel != null)
            postBattlePanel.SetActive(false);
    }

    /// <summary>
    /// Fades the skill bar, action buttons, and top round bar in/out so battle
    /// animations play unobstructed. The combatant status cards are intentionally
    /// left untouched so HP/CP/Battery changes stay visible during the hit.
    /// </summary>
    public void SetActionUiHidden(bool hidden)
    {
        if (commandPanelGroup == null)
            commandPanelGroup = EnsureCanvasGroup("SafeArea/CommandPanel");
        if (topBarGroup == null)
            topBarGroup = EnsureCanvasGroup("SafeArea/TopBar");

        ApplyGroupHidden(commandPanelGroup, hidden);
        ApplyGroupHidden(topBarGroup, hidden);
    }

    private CanvasGroup EnsureCanvasGroup(string path)
    {
        Transform target = FindTransform(path);
        if (target == null)
            return null;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private static void ApplyGroupHidden(CanvasGroup group, bool hidden)
    {
        if (group == null)
            return;

        group.alpha = hidden ? 0f : 1f;
        group.interactable = !hidden;
        group.blocksRaycasts = !hidden;
    }

    /// <summary>
    /// Shows the skill announcement banner ("[zap] {cp}CP  {Name} used {Skill}")
    /// on the acting side's half of the upper arena, dressed with the pixel
    /// TitleBanner sprite. Lives on the HUD root so it stays visible while the
    /// command bar / top bar fade out during the action.
    /// </summary>
    public void PlayActionBanner(Side side, int cp, string actorName, string skillName)
    {
        BuildActionBanner();
        if (actionBannerRoot == null)
            return;

        if (actionBannerBg != null)
            ConfigureFramedImage(actionBannerBg, ActionBannerSprite(side), new Color(0.02f, 0.05f, 0.08f, 0.85f));

        string who = string.IsNullOrWhiteSpace(actorName) ? string.Empty : actorName.Trim();
        string skill = string.IsNullOrWhiteSpace(skillName) ? string.Empty : skillName.Trim();
        string label = string.IsNullOrEmpty(skill)
            ? who
            : (string.IsNullOrEmpty(who) ? skill : $"{who} used {skill}");
        string cpPart = cp > 0 ? $"{cp}CP  " : string.Empty;
        if (actionBannerText != null)
            actionBannerText.text = cpPart + label;

        if (actionBannerRect != null)
            SetStretchRect(actionBannerRect, ActionBannerAnchorMin(side), ActionBannerAnchorMax(side));

        actionBannerRoot.transform.SetAsLastSibling();
        actionBannerRoot.SetActive(true);

        if (actionBannerRoutine != null)
            StopCoroutine(actionBannerRoutine);
        actionBannerRoutine = StartCoroutine(ActionBannerRoutine());
    }

    private void BuildActionBanner()
    {
        if (actionBannerRoot != null)
            return;

        actionBannerRoot = new GameObject("ActionBanner", typeof(RectTransform), typeof(CanvasGroup));
        actionBannerRect = actionBannerRoot.GetComponent<RectTransform>();
        actionBannerRect.SetParent(transform, false);
        SetStretchRect(actionBannerRect, ActionBannerAnchorMin(Side.Player), ActionBannerAnchorMax(Side.Player));
        actionBannerRoot.layer = gameObject.layer;
        actionBannerGroup = actionBannerRoot.GetComponent<CanvasGroup>();
        actionBannerGroup.blocksRaycasts = false;
        actionBannerGroup.interactable = false;
        actionBannerGroup.alpha = 0f;

        Image bg = EnsureChildImage(actionBannerRect, "Banner");
        actionBannerBg = bg;
        SetStretchRect(bg.rectTransform, Vector2.zero, Vector2.one);
        ConfigureFramedImage(bg, ActionBannerSprite(Side.Player), new Color(0.02f, 0.05f, 0.08f, 0.85f));
        EnsureGlow(bg.gameObject);

        Image zap = EnsureChildImage(bg.transform, "Zap");
        SetStretchRect(zap.rectTransform, new Vector2(0.045f, 0.24f), new Vector2(0.150f, 0.78f));
        Sprite zapSprite = ZapIconSprite();
        zap.sprite = zapSprite;
        zap.type = Image.Type.Simple;
        zap.preserveAspect = true;
        zap.raycastTarget = false;
        zap.enabled = zapSprite != null;
        zap.color = new Color(1f, 0.95f, 0.5f, 1f);

        actionBannerText = EnsureChildText(bg.transform, "Label", 24, TextAnchor.MiddleLeft);
        SetStretchRect(actionBannerText.rectTransform, new Vector2(0.175f, 0.08f), new Vector2(0.965f, 0.92f));
        actionBannerText.fontStyle = FontStyle.Bold;
        actionBannerText.color = new Color(0.86f, 0.97f, 1f, 1f);
        actionBannerText.raycastTarget = false;
        EnsureShadow(actionBannerText.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));
    }

    // Compact banner placed clear of the acting sprite. The player sprite sits
    // low-left (its body tops out around viewport y 0.55), so the player banner
    // rides just above it; the enemy sprite sits high-right (body up to ~0.80),
    // so the enemy banner drops to the ground strip below it.
    private static Vector2 ActionBannerAnchorMin(Side side)
    {
        return side == Side.Player ? new Vector2(0.050f, 0.585f) : new Vector2(0.560f, 0.360f);
    }

    private static Vector2 ActionBannerAnchorMax(Side side)
    {
        return side == Side.Player ? new Vector2(0.410f, 0.665f) : new Vector2(0.920f, 0.440f);
    }

    private IEnumerator ActionBannerRoutine()
    {
        const float inDuration = 0.16f;
        const float holdDuration = 1.0f;
        const float outDuration = 0.28f;

        Transform t = actionBannerRoot != null ? actionBannerRoot.transform : null;

        float elapsed = 0f;
        while (elapsed < inDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / inDuration);
            if (actionBannerGroup != null) actionBannerGroup.alpha = p;
            if (t != null) t.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, p);
            yield return null;
        }
        if (actionBannerGroup != null) actionBannerGroup.alpha = 1f;
        if (t != null) t.localScale = Vector3.one;

        yield return new WaitForSeconds(holdDuration);

        elapsed = 0f;
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / outDuration);
            if (actionBannerGroup != null) actionBannerGroup.alpha = 1f - p;
            yield return null;
        }

        if (actionBannerGroup != null) actionBannerGroup.alpha = 0f;
        if (actionBannerRoot != null) actionBannerRoot.SetActive(false);
        actionBannerRoutine = null;
    }

    /// <summary>
    /// Plays the counter cut-in: a quick full-screen flash plus a TitleBanner
    /// letterbox that replays the winner's Status animation as a portrait beside
    /// the "NAME / COUNTER!" text, giving clear "counter succeeded" feedback.
    /// </summary>
    public void PlayCounterBanner(Side side, string actorName, Sprite[] statusFrames, float statusFps)
    {
        BuildCounterCutIn();
        if (counterCutInRoot == null)
            return;

        ApplyCounterCutInSide(side);

        string name = string.IsNullOrWhiteSpace(actorName) ? string.Empty : actorName.Trim().ToUpperInvariant();
        if (counterBannerText != null)
            counterBannerText.text = string.IsNullOrEmpty(name) ? "COUNTER!" : $"{name}\nCOUNTER!";

        counterStatusFrames = statusFrames != null && statusFrames.Length > 0 ? statusFrames : null;
        counterStatusSecondsPerFrame = 1f / Mathf.Max(1f, statusFps);
        if (counterStatusImage != null)
        {
            bool hasFrames = counterStatusFrames != null;
            counterStatusImage.enabled = hasFrames;
            counterStatusImage.sprite = hasFrames ? counterStatusFrames[0] : null;
        }

        counterCutInRoot.transform.SetAsLastSibling();
        counterCutInRoot.SetActive(true);

        if (counterCutInRoutine != null)
            StopCoroutine(counterCutInRoutine);
        counterCutInRoutine = StartCoroutine(CounterCutInRoutine());
    }

    // Cyan family when the player counters, red family when the enemy does, so the
    // cut-in itself reads who won the ASD check at a glance.
    private static readonly Color32 CounterEdgePlayer = new Color32(78, 206, 230, 255);
    private static readonly Color32 CounterEdgeEnemy  = new Color32(232, 72, 60, 255);
    private static readonly Color CounterFlashPlayer = new Color(0.62f, 0.95f, 1f, 1f);
    private static readonly Color CounterFlashEnemy  = new Color(1f, 0.42f, 0.34f, 1f);

    /// <summary>
    /// Recolours the counter cut-in (edges, flash, band tint, label) to the
    /// winning side's palette: cyan for the player, red for the enemy.
    /// </summary>
    private void ApplyCounterCutInSide(Side side)
    {
        bool enemy = side == Side.Enemy;
        Color edge = enemy ? (Color)CounterEdgeEnemy : (Color)CounterEdgePlayer;

        if (counterTopEdge != null) counterTopEdge.color = edge;
        if (counterBottomEdge != null) counterBottomEdge.color = edge;

        if (counterFlashImage != null)
        {
            Color flash = enemy ? CounterFlashEnemy : CounterFlashPlayer;
            flash.a = counterFlashImage.color.a; // keep the routine-driven alpha
            counterFlashImage.color = flash;
        }

        if (counterBand != null && counterBand.sprite != null)
            counterBand.color = enemy ? new Color(1f, 0.62f, 0.58f, 1f) : Color.white;

        if (counterBannerText != null)
            counterBannerText.color = enemy ? new Color(1f, 0.82f, 0.5f, 1f) : new Color(1f, 0.95f, 0.55f, 1f);
    }

    private void BuildCounterCutIn()
    {
        if (counterCutInRoot != null)
            return;

        counterCutInRoot = new GameObject("CounterCutIn", typeof(RectTransform), typeof(CanvasGroup));
        var rootRect = counterCutInRoot.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        SetStretchRect(rootRect, Vector2.zero, Vector2.one);
        counterCutInRoot.layer = gameObject.layer;
        counterCutInGroup = counterCutInRoot.GetComponent<CanvasGroup>();
        counterCutInGroup.blocksRaycasts = false;
        counterCutInGroup.interactable = false;
        counterCutInGroup.alpha = 0f;

        counterFlashImage = EnsureChildImage(rootRect, "Flash");
        SetStretchRect(counterFlashImage.rectTransform, Vector2.zero, Vector2.one);
        counterFlashImage.sprite = null;
        counterFlashImage.color = new Color(0.62f, 0.95f, 1f, 0f);
        counterFlashImage.raycastTarget = false;

        Image band = EnsureChildImage(rootRect, "Band");
        counterBand = band;
        SetStretchRect(band.rectTransform, new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));
        ConfigureFramedImage(band, AnnouncementBannerSprite(), new Color(0.02f, 0.05f, 0.08f, 0.92f));
        EnsureGlow(band.gameObject);

        counterTopEdge = EnsureChildImage(band.transform, "TopEdge");
        SetStretchRect(counterTopEdge.rectTransform, new Vector2(0f, 0.955f), new Vector2(1f, 1f));
        counterTopEdge.sprite = null;
        counterTopEdge.color = (Color)HudPanelBorder;
        counterTopEdge.raycastTarget = false;

        counterBottomEdge = EnsureChildImage(band.transform, "BottomEdge");
        SetStretchRect(counterBottomEdge.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.045f));
        counterBottomEdge.sprite = null;
        counterBottomEdge.color = (Color)HudPanelBorder;
        counterBottomEdge.raycastTarget = false;

        counterStatusImage = EnsureChildImage(band.transform, "StatusPortrait");
        SetStretchRect(counterStatusImage.rectTransform, new Vector2(0.16f, 0.08f), new Vector2(0.34f, 0.94f));
        counterStatusImage.sprite = null;
        counterStatusImage.type = Image.Type.Simple;
        counterStatusImage.preserveAspect = true;
        counterStatusImage.raycastTarget = false;
        counterStatusImage.enabled = false;

        counterBannerText = EnsureChildText(band.transform, "Label", 40, TextAnchor.MiddleCenter);
        SetStretchRect(counterBannerText.rectTransform, new Vector2(0.36f, 0.04f), new Vector2(0.94f, 0.96f));
        counterBannerText.fontStyle = FontStyle.Bold;
        counterBannerText.color = new Color(1f, 0.95f, 0.55f, 1f);
        counterBannerText.raycastTarget = false;
        EnsureShadow(counterBannerText.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));
    }

    private IEnumerator CounterCutInRoutine()
    {
        const float inDuration = 0.12f;
        const float holdDuration = 0.70f;
        const float outDuration = 0.22f;

        float statusTimer = 0f;
        int statusIndex = 0;
        if (counterStatusImage != null && counterStatusFrames != null)
            counterStatusImage.sprite = counterStatusFrames[0];

        float elapsed = 0f;
        while (elapsed < inDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / inDuration);
            if (counterCutInGroup != null) counterCutInGroup.alpha = p;
            SetFlashAlpha(Mathf.Lerp(0f, 0.55f, p));
            AdvanceCounterStatus(ref statusTimer, ref statusIndex, Time.deltaTime);
            yield return null;
        }
        if (counterCutInGroup != null) counterCutInGroup.alpha = 1f;

        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.deltaTime;
            SetFlashAlpha(Mathf.Lerp(0.55f, 0f, Mathf.Clamp01(elapsed / holdDuration)));
            AdvanceCounterStatus(ref statusTimer, ref statusIndex, Time.deltaTime);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            if (counterCutInGroup != null)
                counterCutInGroup.alpha = 1f - Mathf.Clamp01(elapsed / outDuration);
            AdvanceCounterStatus(ref statusTimer, ref statusIndex, Time.deltaTime);
            yield return null;
        }

        if (counterCutInGroup != null) counterCutInGroup.alpha = 0f;
        if (counterCutInRoot != null) counterCutInRoot.SetActive(false);
        counterCutInRoutine = null;
    }

    private void AdvanceCounterStatus(ref float timer, ref int index, float dt)
    {
        if (counterStatusImage == null || counterStatusFrames == null || counterStatusFrames.Length < 2)
            return;

        timer += dt;
        if (timer < counterStatusSecondsPerFrame)
            return;

        timer -= counterStatusSecondsPerFrame;
        index = (index + 1) % counterStatusFrames.Length;
        Sprite frame = counterStatusFrames[index];
        if (frame != null)
            counterStatusImage.sprite = frame;
    }

    private void SetFlashAlpha(float alpha)
    {
        if (counterFlashImage == null)
            return;
        Color c = counterFlashImage.color;
        c.a = alpha;
        counterFlashImage.color = c;
    }

    public void SetRoundSandclockActive(bool active)
    {
        roundSandclockPlaying = active;
        if (active)
        {
            roundSandclockTimer = 0f;
            roundSandclockFrameIndex = 0;
        }
        ApplyRoundSandclockState();
    }

    public void SetCombatant(Side side, string name, int level)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        if (refs.NameText  != null) refs.NameText.text  = name;
        if (refs.LevelText != null) refs.LevelText.text = $"Lv. {level}";
    }

    /// <summary>
    /// Stores the subroutine (passive) text shown in the skill detail panel when
    /// the player hovers or clicks a combatant card. Pass a null/empty name to clear.
    /// </summary>
    public void SetSubroutine(Side side, string subroutineName, string triggerLabel, string description)
    {
        SubroutineCardData card = default;
        card.Has = !string.IsNullOrWhiteSpace(subroutineName);
        if (card.Has)
        {
            card.Title = $"{subroutineName.Trim().ToUpperInvariant()}  ·  SUBROUTINE";
            string trig = string.IsNullOrWhiteSpace(triggerLabel) ? string.Empty : $"TRIGGER: {triggerLabel}\n";
            string body = string.IsNullOrWhiteSpace(description) ? "Hardwired passive ability." : description.Trim();
            card.Body = trig + body;
        }

        if (side == Side.Player) playerSubroutine = card;
        else enemySubroutine = card;

        // Always-visible passive line on the card (name + trigger); the full
        // description remains available on the card hover/click detail.
        ref CombatantRefs refs = ref RefsFor(side);
        if (refs.SubroutineLabel != null)
        {
            refs.SubroutineLabel.text = card.Has
                ? $"PASSIVE: {subroutineName.Trim().ToUpperInvariant()}" +
                  (string.IsNullOrWhiteSpace(triggerLabel) ? string.Empty : $"  ({triggerLabel})")
                : string.Empty;
        }
    }

    private void WireCombatantSubroutine(Side side, string panelPath)
    {
        Transform panel = FindTransform(panelPath);
        if (panel == null)
            return;

        // The card must receive pointer events to surface its subroutine.
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.raycastTarget = true;

        EventTrigger trigger = panel.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = panel.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        trigger.triggers.Clear();

        AddCombatantTrigger(trigger, EventTriggerType.PointerEnter, () => ShowSubroutineDetail(side));
        AddCombatantTrigger(trigger, EventTriggerType.PointerClick, () => ShowSubroutineDetail(side));
        AddCombatantTrigger(trigger, EventTriggerType.PointerExit, HideSkillDetailPanel);
    }

    private static void AddCombatantTrigger(EventTrigger trigger, EventTriggerType type, Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void ShowSubroutineDetail(Side side)
    {
        SubroutineCardData card = side == Side.Player ? playerSubroutine : enemySubroutine;
        if (!card.Has)
            return;
        ShowSkillDetail(card.Title, card.Body);
    }

    public void SetBattery(Side side, int current, int max)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        ref CombatantDisplayState display = ref DisplayFor(side);
        int safeMax = Mathf.Max(0, max);
        int safeCurrent = safeMax <= 0 ? 0 : Mathf.Clamp(current, 0, safeMax);

        if (refs.BatteryValueText != null)
        {
            // Current value leads, "/max" recedes. In danger the whole string is
            // left plain so ApplyLowBatteryWarning's red tint covers all of it.
            bool dangerNow = safeMax > 0 && (float)safeCurrent / safeMax <= lowBatteryDangerRatio;
            refs.BatteryValueText.text = dangerNow
                ? $"{safeCurrent}/{safeMax}"
                : $"{safeCurrent}<color=#86B9C6>/{safeMax}</color>";
        }

        // Max change means a different unit is in the bar (switch-in): snap both
        // layers instead of ghosting the previous AlgoMon's value.
        if (!display.BatteryInitialized || display.TargetBatteryMax != safeMax)
        {
            display.DisplayBattery = safeCurrent;
            display.GhostBattery = safeCurrent;
            display.GhostHoldTimer = 0f;
            display.BatteryInitialized = true;
        }

        // Damage (target actually dropped): park the ghost at the value the bar
        // showed before the hit and hold briefly. RefreshHud re-sends the same
        // value every update, so compare against the previous target, not the
        // displayed value, or the hold would re-arm forever. Healing: the ghost
        // rides up with the bar.
        if (safeCurrent < display.TargetBattery)
        {
            display.GhostBattery = Mathf.Max(display.DisplayBattery, display.GhostBattery);
            display.GhostHoldTimer = batteryGhostHoldSeconds;
        }
        else if (safeCurrent >= display.GhostBattery)
        {
            display.GhostBattery = safeCurrent;
        }

        display.TargetBattery = safeCurrent;
        display.TargetBatteryMax = safeMax;
        display.DisplayBattery = Mathf.Clamp(display.DisplayBattery, 0f, safeMax);
        display.GhostBattery = Mathf.Clamp(display.GhostBattery, 0f, safeMax);
        ApplyBatteryVisual(refs, display.DisplayBattery, display.GhostBattery, safeMax);
    }

    public void SetCP(Side side, int current, int max)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        ref CombatantDisplayState display = ref DisplayFor(side);
        if (refs.CPDots == null) return;

        int safeMax = Mathf.Clamp(max, 0, MaxCP);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        if (refs.CPValueText != null)
            refs.CPValueText.text = $"{safeCurrent}/{safeMax}";

        if (!display.CPInitialized)
        {
            display.DisplayCP = safeCurrent;
            display.CPInitialized = true;
        }

        display.TargetCP = safeCurrent;
        display.TargetCPMax = safeMax;
        display.DisplayCP = Mathf.Clamp(display.DisplayCP, 0f, safeMax);
        ApplyCPVisual(refs, display.DisplayCP, safeMax, side == Side.Player);

        if (side == Side.Player)
            RefreshCPAffordability();
    }

    public void SetStatus(Side side, string statusText)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        if (refs.StatusText != null) refs.StatusText.text = statusText;
    }

    /// <summary>
    /// Populates the stable tag placeholders on a skill button from a SkillData
    /// asset. Every slot has CP / BP / Counter roots; this method fills and
    /// toggles them according to the skill's current data.
    /// </summary>
    public void SetSkillSlot(int index, SkillData skill)
    {
        if (!IndexInRange(index)) return;

        if (skill == null)
        {
            ClearSkillSlot(index);
            return;
        }

        if (skillButtons[index]    != null) skillButtons[index].interactable = true;
        ApplySkillSlotLayout(index);
        SetSwitchPortrait(index, null);
        SetSwitchElementChip(index, false, skill.elementType);
        switchSlotDetails[index] = default;
        if (skillNameTexts[index]  != null) skillNameTexts[index].text       = Ellipsize(skill.skillName, SkillNameMaxCharacters);
        skillCPCosts[index] = Mathf.Max(0, skill.cpCost);

        SetTag(skillCPTagObjects[index], skillCPTexts[index], true, $"CP {skill.cpCost}");

        bool showsPower = skill.basePower > 0;
        SetTag(skillPowerTagObjects[index], skillPowerTexts[index], showsPower,
            showsPower ? $"BP {skill.basePower}" : string.Empty);

        bool showsCounter = skill.canCounter && skill.instructionType == InstructionType.Defense;
        SetTag(skillCounterTagObjects[index], skillCounterTexts[index], showsCounter,
            showsCounter ? "CNT" : string.Empty);
        LayoutSkillTags(index, true, showsPower, showsCounter);
        SetSkillSlotBadges(index, skill);

        skillSlotElements[index] = skill.elementType;
        skillSlotDealsDamage[index] = skill.damageType != DamageType.None && skill.basePower > 0;
        skillSlotShowsMatchup[index] = skillSlotDealsDamage[index];
        skillSlotIsSwitch[index] = false;
        ApplySkillEffectiveness(index);
        RefreshCPAffordability();

        // Hover preview follows the skill currently in the slot.
        skillHoverTitles[index] = skill.skillName;
        skillHoverBodies[index] = BuildSkillDetailFallback(skill);
    }

    public void SetSwitchSlot(
        int index,
        string displayName,
        ElementType elementType,
        int level,
        int currentBattery,
        int maxBattery,
        int currentCP,
        int maxCP,
        string stateText,
        string statusSummary,
        Sprite portraitSprite,
        bool available,
        SubroutineData subroutine = null)
    {
        if (!IndexInRange(index)) return;

        if (skillButtons[index] != null)
            skillButtons[index].interactable = available;
        ApplySwitchSlotLayout(index);
        SetSwitchPortrait(index, portraitSprite);
        if (skillNameTexts[index] != null)
            skillNameTexts[index].text = string.IsNullOrWhiteSpace(displayName)
                ? "-"
                : Ellipsize(displayName, SkillNameMaxCharacters);

        string levelText = $"Lv {Mathf.Max(1, level)}";
        SetTag(skillCPTagObjects[index], skillCPTexts[index], true, ElementIconSprite(elementType) == null ? ElementSwitchLabel(elementType) : string.Empty);
        SetSwitchElementChip(index, true, elementType);
        SetTag(skillPowerTagObjects[index], skillPowerTexts[index], true, levelText);
        SetTag(skillCounterTagObjects[index], skillCounterTexts[index], !string.IsNullOrWhiteSpace(stateText), stateText);
        LayoutSwitchTags(index);
        SetSkillSlotBadges(index, null);
        SetTypeStrip(index, true, ElementBadgeColor(elementType));
        skillSlotElements[index] = elementType;
        skillSlotDealsDamage[index] = false;
        skillSlotShowsMatchup[index] = available;
        skillSlotIsSwitch[index] = true;
        ApplySkillEffectiveness(index);
        skillCPCosts[index] = 0;
        RefreshCPAffordability();
        switchSlotDetails[index] = new SwitchSlotDetail
        {
            HasData = true,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Switch" : displayName.Trim(),
            ElementType = elementType,
            Level = level,
            CurrentBattery = currentBattery,
            MaxBattery = maxBattery,
            CurrentCP = currentCP,
            MaxCP = maxCP,
            StateText = stateText,
            StatusSummary = statusSummary,
        };

        skillHoverTitles[index] = string.IsNullOrWhiteSpace(displayName) ? "Switch" : displayName;
        skillHoverBodies[index] = BuildSwitchDetailText(
            elementType,
            level,
            currentBattery,
            maxBattery,
            currentCP,
            maxCP,
            stateText,
            statusSummary,
            subroutine);
    }

    public void SetSkillSlotAvailable(int index, bool available)
    {
        if (!IndexInRange(index)) return;
        if (skillButtons[index] != null)
            skillButtons[index].interactable = available;
    }

    /// <summary>
    /// Override the hover preview content for a given skill slot. Useful when
    /// the displayed body should differ from `SkillData.description` (e.g. to
    /// show live CP cost, current Burn stacks, etc.).
    /// </summary>
    public void SetSkillHover(int index, string title, string body)
    {
        if (!IndexInRange(index)) return;
        skillHoverTitles[index] = title ?? string.Empty;
        skillHoverBodies[index] = body  ?? string.Empty;
    }

    public void SetSkillCPCost(int index, int cpCost)
    {
        if (!IndexInRange(index)) return;
        skillCPCosts[index] = Mathf.Max(0, cpCost);
        if (skillCPTexts[index] != null)
            skillCPTexts[index].text = $"CP {skillCPCosts[index]}";
        RefreshCPAffordability();
    }

    public void SetActionHover(ActionButton button, string title, string body)
    {
        int idx = ActionIndex(button);
        if (idx < 0) return;
        actionHoverTitles[idx] = title ?? string.Empty;
        actionHoverBodies[idx] = body  ?? string.Empty;
    }

    public void SetActionButtonAvailable(ActionButton button, bool available)
    {
        Button target = ButtonFor(button);
        if (target != null)
            target.interactable = available;
    }

    private Button ButtonFor(ActionButton button)
    {
        switch (button)
        {
            case ActionButton.Recharge: return rechargeButton;
            case ActionButton.Switch:   return switchButton;
            case ActionButton.Flee:     return fleeButton;
            default:                    return null;
        }
    }

    private static int ActionIndex(ActionButton button)
    {
        switch (button)
        {
            case ActionButton.Recharge: return 0;
            case ActionButton.Switch:   return 1;
            case ActionButton.Flee:     return 2;
            default:                    return -1;
        }
    }

    public void ClearSkillSlot(int index)
    {
        if (!IndexInRange(index)) return;
        if (skillButtons[index]      != null) skillButtons[index].interactable = false;
        ApplySkillSlotLayout(index);
        SetSwitchPortrait(index, null);
        SetSwitchElementChip(index, false, ElementType.Normal);
        switchSlotDetails[index] = default;
        if (skillNameTexts[index]    != null) skillNameTexts[index].text       = "-";
        SetTag(skillCPTagObjects[index], skillCPTexts[index], false, string.Empty);
        SetTag(skillPowerTagObjects[index], skillPowerTexts[index], false, string.Empty);
        SetTag(skillCounterTagObjects[index], skillCounterTexts[index], false, string.Empty);
        LayoutSkillTags(index, false, false, false);
        SetSkillSlotBadges(index, null);
        skillSlotDealsDamage[index] = false;
        skillSlotShowsMatchup[index] = false;
        skillSlotIsSwitch[index] = false;
        ApplySkillEffectiveness(index);
        skillCPCosts[index] = 0;
        RefreshCPAffordability();
        skillHoverTitles[index] = string.Empty;
        skillHoverBodies[index] = string.Empty;
    }

    public void ClearAllSkillSlots()
    {
        for (int i = 0; i < MaxSkillSlots; i++)
            ClearSkillSlot(i);
    }

    /// <summary>
    /// Writes the resting right-hand Skill Details panel. Hover previews
    /// temporarily replace this text and restore it on pointer exit, so battle
    /// logs and validation messages stay visible after the mouse moves away.
    /// </summary>
    public void SetSkillDetail(string title, string body)
    {
        restingDetailTitle = title ?? string.Empty;
        restingDetailBody  = body  ?? string.Empty;
        WriteSkillDetail(restingDetailTitle, restingDetailBody);
    }

    // ----- Internals -----

    private ref CombatantRefs RefsFor(Side side)
    {
        if (side == Side.Player) return ref player;
        return ref enemy;
    }

    private ref CombatantDisplayState DisplayFor(Side side)
    {
        if (side == Side.Player) return ref playerDisplay;
        return ref enemyDisplay;
    }

    private void UpdateResourceDisplays()
    {
        UpdateResourceDisplay(player, ref playerDisplay, true);
        UpdateResourceDisplay(enemy, ref enemyDisplay, false);
    }

    private void UpdateRoundSandclockAnimation()
    {
        if (roundSandclockImage == null)
            return;

        bool shouldPlay = ShouldRoundSandclockPlay();
        if (roundSandclockImage.gameObject.activeSelf != shouldPlay)
            roundSandclockImage.gameObject.SetActive(shouldPlay);

        if (!shouldPlay || roundSandclockFrames == null || roundSandclockFrames.Length == 0)
            return;

        roundSandclockTimer += Time.deltaTime;
        while (roundSandclockTimer >= roundSandclockFrameSeconds)
        {
            roundSandclockTimer -= roundSandclockFrameSeconds;
            roundSandclockFrameIndex = (roundSandclockFrameIndex + 1) % roundSandclockFrames.Length;
        }

        Sprite frame = roundSandclockFrames[roundSandclockFrameIndex];
        if (frame != null && roundSandclockImage.sprite != frame)
            roundSandclockImage.sprite = frame;
    }

    private void ApplyRoundSandclockState()
    {
        if (roundSandclockImage == null)
            return;

        roundSandclockImage.gameObject.SetActive(ShouldRoundSandclockPlay());
        if (roundSandclockFrames != null && roundSandclockFrames.Length > 0)
        {
            roundSandclockFrameIndex = Mathf.Clamp(roundSandclockFrameIndex, 0, roundSandclockFrames.Length - 1);
            roundSandclockImage.sprite = roundSandclockFrames[roundSandclockFrameIndex];
        }
    }

    private bool ShouldRoundSandclockPlay()
    {
        return roundSandclockPlaying ||
            (battleStateText != null && string.Equals(battleStateText.text, PlayerTurnStateText, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureBattleAnnouncer()
    {
        // The top announcer board is retired: action narration now appears as an
        // above-sprite callout (BattlePresentationController.SpawnActionCallout).
        // Hide any existing board so it never occupies the top of the screen.
        Transform root = FindTransform("SafeArea/BattleAnnouncer");
        if (root != null && root.gameObject.activeSelf)
            root.gameObject.SetActive(false);
    }

    private void EnsurePostBattlePanel()
    {
        Transform root = FindTransform("SafeArea/PostBattleRewardPanel");
        if (root == null)
            root = CreatePostBattlePanel();

        if (root == null)
            return;

        postBattlePanel = root.gameObject;
        postBattleTitleText = postBattleTitleText != null
            ? postBattleTitleText
            : FindText("SafeArea/PostBattleRewardPanel/TitleText");
        postBattleBodyText = postBattleBodyText != null
            ? postBattleBodyText
            : FindText("SafeArea/PostBattleRewardPanel/BodyText");
        postBattleContinueButton = postBattleContinueButton != null
            ? postBattleContinueButton
            : Find<Button>("SafeArea/PostBattleRewardPanel/ContinueButton");

        if (postBattleContinueButton != null)
        {
            postBattleContinueButton.onClick.RemoveListener(HandlePostBattleContinueClicked);
            postBattleContinueButton.onClick.AddListener(HandlePostBattleContinueClicked);
        }
    }

    private Transform CreatePostBattlePanel()
    {
        Transform safeArea = FindTransform("SafeArea");
        Transform parent = safeArea != null ? safeArea : transform;

        GameObject rootObject = new GameObject("PostBattleRewardPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootObject.layer = gameObject.layer;
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = new Vector2(0.30f, 0.28f);
        root.anchorMax = new Vector2(0.70f, 0.76f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

        Image frame = rootObject.GetComponent<Image>();
        // Use the shared cyber-glass chrome so the result panel matches the rest of
        // the battle HUD instead of the old standalone green announcer panel.
        ApplyPanelChrome(frame);

        postBattleTitleText = CreateAnnouncerText(
            "TitleText",
            root,
            24,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white,
            new Vector2(0.08f, 0.78f),
            new Vector2(0.92f, 0.94f));

        postBattleBodyText = CreateAnnouncerText(
            "BodyText",
            root,
            17,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            new Color(0.92f, 1f, 0.96f, 1f),
            new Vector2(0.10f, 0.27f),
            new Vector2(0.90f, 0.75f));

        GameObject buttonObject = new GameObject("ContinueButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = gameObject.layer;
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(root, false);
        SetStretchRect(buttonRect, new Vector2(0.32f, 0.08f), new Vector2(0.68f, 0.22f));

        Image buttonImage = buttonObject.GetComponent<Image>();
        ConfigureChromeChip(buttonImage, 2f);
        buttonImage.raycastTarget = true;

        postBattleContinueButton = buttonObject.GetComponent<Button>();
        postBattleContinueButton.targetGraphic = buttonImage;
        postBattleContinueButton.onClick.AddListener(HandlePostBattleContinueClicked);

        Text buttonText = CreateAnnouncerText(
            "Text",
            buttonRect,
            17,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white,
            Vector2.zero,
            Vector2.one);
        buttonText.raycastTarget = false;
        buttonText.text = "CONTINUE";

        return root;
    }

    private void HandlePostBattleContinueClicked()
    {
        PostBattleContinueClicked?.Invoke();
    }

    private Transform CreateBattleAnnouncer()
    {
        Transform safeArea = FindTransform("SafeArea");
        Transform parent = safeArea != null ? safeArea : transform;

        GameObject rootObject = new GameObject("BattleAnnouncer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        ConfigureBattleAnnouncerLayout(root);

        announcerFrame = rootObject.GetComponent<Image>();
        ConfigureAnnouncerFrame();

        announcerTitleText = CreateAnnouncerText(
            "TitleText",
            root,
            12,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white,
            new Vector2(0.05f, 0.63f),
            new Vector2(0.96f, 0.96f));

        announcerBodyText = CreateAnnouncerText(
            "BodyText",
            root,
            21,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0.96f, 1f, 1f, 1f),
            new Vector2(0.05f, 0.08f),
            new Vector2(0.95f, 0.70f));

        return root;
    }

    private void ConfigureRoundSandclockLayout()
    {
        if (roundSandclockImage == null)
            return;

        // Position/size of the round sandclock is authored in BattleHud.prefab now.
        // Code only keeps it on top of the top bar and sets non-layout flags.
        roundSandclockImage.rectTransform?.SetAsLastSibling();
        roundSandclockImage.raycastTarget = false;
        roundSandclockImage.preserveAspect = true;
    }

    private static void ConfigureBattleAnnouncerLayout(RectTransform root)
    {
        if (root == null)
            return;

        root.anchorMin = AnnouncerAnchorMin;
        root.anchorMax = AnnouncerAnchorMax;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
    }

    private Text CreateAnnouncerText(
        string objectName,
        Transform parent,
        int size,
        FontStyle style,
        TextAnchor alignment,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.font = AnnouncerFont();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(8, size - 7);
        text.resizeTextMaxSize = size;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(1.4f, -1.4f);
        return text;
    }

    private Font AnnouncerFont()
    {
        if (announcerFont == null)
            announcerFont = Resources.Load<Font>(AnnouncerFontResourcePath);
        if (announcerFont == null)
            announcerFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return announcerFont;
    }

    private Font ReadableHudFont()
    {
        if (readableHudFont == null)
            readableHudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (readableHudFont == null)
            readableHudFont = AnnouncerFont();
        return readableHudFont;
    }

    private static string Ellipsize(string value, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        if (maxCharacters <= 0 || trimmed.Length <= maxCharacters)
            return trimmed;
        if (maxCharacters <= 3)
            return trimmed.Substring(0, maxCharacters);

        return trimmed.Substring(0, maxCharacters - 3).TrimEnd() + "...";
    }

    private Sprite AnnouncerPanelSprite()
    {
        if (announcerPanelSprite == null)
            announcerPanelSprite = Resources.Load<Sprite>(AnnouncerPanelResourcePath);
        return announcerPanelSprite;
    }

    private Sprite SkillButtonFrameSprite()
    {
        if (skillButtonFrameSprite == null)
            skillButtonFrameSprite = Resources.Load<Sprite>(SkillButtonFrameResourcePath);
        return skillButtonFrameSprite;
    }

    private Sprite SkillInsetFrameSprite()
    {
        if (skillInsetFrameSprite == null)
            skillInsetFrameSprite = Resources.Load<Sprite>(SkillInsetFrameResourcePath);
        return skillInsetFrameSprite;
    }

    private Sprite SkillTagFrameSprite()
    {
        if (skillTagFrameSprite == null)
            skillTagFrameSprite = Resources.Load<Sprite>(SkillTagFrameResourcePath);
        return skillTagFrameSprite;
    }

    private Sprite SkillPanelBackdropSprite()
    {
        if (skillPanelBackdropSprite == null)
            skillPanelBackdropSprite = Resources.Load<Sprite>(SkillPanelBackdropResourcePath);
        return skillPanelBackdropSprite;
    }

    private Sprite AnnouncementBannerSprite()
    {
        if (announcementBannerSprite == null)
            announcementBannerSprite = Resources.Load<Sprite>(AnnouncementBannerResourcePath);
        return announcementBannerSprite;
    }

    /// <summary>
    /// Side-tinted action-callout banner: the blue TitleBanner decorator for the
    /// player, the red one for the enemy. Falls back to the shared TitleBanner if
    /// a decorator sprite is missing.
    /// </summary>
    private Sprite ActionBannerSprite(Side side)
    {
        if (side == Side.Player)
        {
            if (actionBannerPlayerSprite == null)
                actionBannerPlayerSprite = Resources.Load<Sprite>(ActionBannerPlayerResourcePath);
            return actionBannerPlayerSprite != null ? actionBannerPlayerSprite : AnnouncementBannerSprite();
        }

        if (actionBannerEnemySprite == null)
            actionBannerEnemySprite = Resources.Load<Sprite>(ActionBannerEnemyResourcePath);
        return actionBannerEnemySprite != null ? actionBannerEnemySprite : AnnouncementBannerSprite();
    }


    private Sprite ZapIconSprite()
    {
        if (zapIconSprite == null)
            zapIconSprite = Resources.Load<Sprite>(ZapIconResourcePath);
        return zapIconSprite;
    }

    private void EnsureSkillPanelPresentation()
    {
        Image panelImage = Find<Image>("SafeArea/CommandPanel/SkillPanel");
        if (panelImage == null)
            return;

        // SkillPanelBackdropSprite() is still consulted as the flag that drives
        // the transparent-slot layout below; the panel itself now wears the
        // shared cyber-glass chrome so the skill bar matches every other panel.
        Sprite backdrop = SkillPanelBackdropSprite();
        if (backdrop == null)
            return;

        Sprite chrome = HudPanelSprite();
        bool needsPanelApply = !skillPanelPresentationApplied || panelImage.sprite != chrome || panelImage.color != Color.white;
        if (!needsPanelApply && skillPanelSlotLayoutApplied)
            return;

        if (needsPanelApply)
        {
            ApplyPanelChrome(panelImage);
            panelImage.preserveAspect = false;
            panelImage.raycastTarget = false;
            // Skill bar position/size is authored in BattleHud.prefab now; edit it
            // there. The slots below anchor fractionally, so they follow the panel.
            skillPanelSlotLayoutApplied = false;
        }

        skillPanelPresentationApplied = true;
        if (!skillPanelSlotLayoutApplied)
        {
            ConfigureSkillGridForPanelBackdrop();

            bool allSlotsReady = true;
            for (int i = 0; i < MaxSkillSlots; i++)
            {
                if (skillButtons[i] != null)
                    EnsureSkillSlotPresentation(i, skillButtons[i].transform);
                else
                    allSlotsReady = false;
            }

            skillPanelSlotLayoutApplied = allSlotsReady;
        }
    }

    private void ConfigureSkillGridForPanelBackdrop()
    {
        Transform grid = FindTransform("SafeArea/CommandPanel/SkillPanel/SkillGrid");
        if (grid == null)
            return;

        RectTransform gridRect = grid.GetComponent<RectTransform>();
        SetStretchRect(gridRect, Vector2.zero, Vector2.one);

        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        Vector2[] rowMins =
        {
            new Vector2(0.024f, 0.520f),
            new Vector2(0.507f, 0.520f),
            new Vector2(0.024f, 0.060f),
            new Vector2(0.507f, 0.060f),
        };
        Vector2[] rowMaxes =
        {
            new Vector2(0.493f, 0.940f),
            new Vector2(0.976f, 0.940f),
            new Vector2(0.493f, 0.480f),
            new Vector2(0.976f, 0.480f),
        };

        for (int i = 0; i < MaxSkillSlots; i++)
        {
            if (skillButtons[i] == null)
                continue;

            RectTransform buttonRect = skillButtons[i].GetComponent<RectTransform>();
            if (buttonRect == null)
                continue;

            SetStretchRect(buttonRect, rowMins[i], rowMaxes[i]);
            buttonRect.localScale = Vector3.one;
            buttonRect.localRotation = Quaternion.identity;
        }
    }

    private void EnsureSkillSlotPresentation(int index, Transform root)
    {
        if (!IndexInRange(index) || root == null)
            return;

        bool hasPanelBackdrop = SkillPanelBackdropSprite() != null;
        Image buttonImage = root.GetComponent<Image>();
        if (buttonImage != null)
        {
            if (hasPanelBackdrop)
            {
                buttonImage.sprite = HudPanelSprite();
                buttonImage.type = Image.Type.Sliced;
                buttonImage.pixelsPerUnitMultiplier = 3f;
                buttonImage.color = new Color(0.82f, 1f, 1f, 0.94f);
                buttonImage.raycastTarget = true;
            }
            else
            {
                Sprite frame = SkillButtonFrameSprite();
                if (frame != null)
                {
                    buttonImage.sprite = frame;
                    buttonImage.type = Image.Type.Sliced;
                    buttonImage.pixelsPerUnitMultiplier = 1.5f;
                    buttonImage.color = Color.white;
                }
                else
                {
                    buttonImage.color = new Color(0.07f, 0.16f, 0.13f, 0.92f);
                }
            }
        }

        Outline buttonOutline = root.GetComponent<Outline>();
        if (buttonOutline != null && hasPanelBackdrop)
        {
            buttonOutline.effectColor = new Color(0.18f, 0.92f, 1f, 0.52f);
            buttonOutline.effectDistance = new Vector2(1.3f, -1.3f);
            buttonOutline.useGraphicAlpha = false;
            buttonOutline.enabled = true;
        }

        // Instruction identity strip: slim colour-coded edge on the row's left.
        // SetTypeStrip drives its colour/visibility from the slot's content.
        skillTypeStrips[index] = EnsureChildImage(root, "TypeStrip");
        SetStretchRect(skillTypeStrips[index].rectTransform, new Vector2(0.016f, 0.155f), new Vector2(0.034f, 0.845f));
        skillTypeStrips[index].sprite = null;
        skillTypeStrips[index].type = Image.Type.Simple;
        skillTypeStrips[index].raycastTarget = false;
        skillTypeStrips[index].gameObject.SetActive(false);

        RectTransform nameRect = skillNameTexts[index] != null
            ? skillNameTexts[index].GetComponent<RectTransform>()
            : null;
        if (nameRect != null)
        {
            // A short name band: limiting the height to roughly one line makes
            // best-fit shrink long names onto a single line instead of wrapping.
            // The element icon sits snug after the band, mockup-style.
            float nameLeft = hasPanelBackdrop ? 0.225f : 0.145f;
            float nameRight = hasPanelBackdrop ? 0.680f : 0.515f;
            float nameBottom = hasPanelBackdrop ? 0.500f : 0.10f;
            float nameTop = hasPanelBackdrop ? 0.890f : 0.90f;
            SetStretchRect(nameRect, new Vector2(nameLeft, nameBottom), new Vector2(nameRight, nameTop));
            ConfigureSkillNameText(skillNameTexts[index], hasPanelBackdrop);
        }

        switchPortraitImages[index] = EnsureChildImage(root, "SwitchPortrait");
        SetStretchRect(switchPortraitImages[index].rectTransform, new Vector2(0.045f, 0.170f), new Vector2(0.215f, 0.850f));
        switchPortraitImages[index].preserveAspect = true;
        switchPortraitImages[index].raycastTarget = false;
        switchPortraitImages[index].color = Color.white;
        switchPortraitImages[index].gameObject.SetActive(false);
        switchPortraitImages[index].transform.SetAsFirstSibling();

        skillInstructionFrames[index] = EnsureChildImage(root, "InstructionFrame");
        RectTransform instructionRect = skillInstructionFrames[index].rectTransform;
        if (hasPanelBackdrop)
        {
            // Compact square A/D/S badge wearing the shared cyan chrome.
            SetStretchRect(instructionRect, new Vector2(0.060f, 0.220f), new Vector2(0.185f, 0.815f));
            ConfigureChromeChip(skillInstructionFrames[index], 3f);
        }
        else
        {
            SetFixedLeftRect(instructionRect, 10f, 42f);
            ConfigureFramedImage(skillInstructionFrames[index], SkillInsetFrameSprite(), new Color(0.10f, 0.48f, 0.28f, 0.78f));
        }
        skillInstructionFrames[index].transform.SetAsFirstSibling();

        skillInstructionTexts[index] = EnsureChildText(skillInstructionFrames[index].transform, "InstructionText", hasPanelBackdrop ? 20 : 23, TextAnchor.MiddleCenter);
        skillInstructionTexts[index].fontStyle = FontStyle.Bold;
        skillInstructionTexts[index].color = Color.white;
        EnsureShadow(skillInstructionTexts[index].gameObject, new Color(0f, 0f, 0f, 0.75f), new Vector2(1.2f, -1.2f));

        // Sword / shield / status-swirl glyph layered over the badge frame. The
        // letter text stays as a fallback only when no icon resource is found.
        skillInstructionIcons[index] = EnsureChildImage(skillInstructionFrames[index].transform, "InstructionIcon");
        SetStretchRect(skillInstructionIcons[index].rectTransform, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f));
        skillInstructionIcons[index].type = Image.Type.Simple;
        skillInstructionIcons[index].preserveAspect = true;
        skillInstructionIcons[index].raycastTarget = false;
        skillInstructionIcons[index].transform.SetAsLastSibling();

        skillElementBadges[index] = EnsureChildImage(root, "ElementBadge");
        SetStretchRect(skillElementBadges[index].rectTransform, new Vector2(0.705f, 0.575f), new Vector2(0.775f, 0.850f));
        if (hasPanelBackdrop)
            ConfigureTransparentImage(skillElementBadges[index]);
        else
            ConfigureFramedImage(skillElementBadges[index], SkillInsetFrameSprite(), new Color(0.35f, 0.75f, 0.90f, 0.88f));

        skillElementIconImages[index] = EnsureChildImage(skillElementBadges[index].transform, "ElementIcon");
        SetStretchRect(skillElementIconImages[index].rectTransform, new Vector2(0.14f, 0.08f), new Vector2(0.86f, 0.92f));
        skillElementIconImages[index].preserveAspect = true;
        skillElementIconImages[index].raycastTarget = false;

        skillElementTexts[index] = EnsureChildText(skillElementBadges[index].transform, "ElementText", 13, TextAnchor.MiddleCenter);
        skillElementTexts[index].fontStyle = FontStyle.Bold;
        skillElementTexts[index].color = Color.white;
        EnsureShadow(skillElementTexts[index].gameObject, new Color(0f, 0f, 0f, 0.70f), new Vector2(1f, -1f));

        // Three tag wells: CP cost, BP (or BAT in switch mode), and the compact
        // counter "C" badge (state text in switch mode). Sized tall/wide enough
        // that the best-fit text stays readable at row scale.
        ConfigureSkillTag(skillCPTagObjects[index], skillCPTexts[index], new Vector2(0.225f, 0.125f), new Vector2(0.390f, 0.440f));
        ConfigureSkillTag(skillPowerTagObjects[index], skillPowerTexts[index], new Vector2(0.415f, 0.125f), new Vector2(0.605f, 0.440f));
        ConfigureSkillTag(skillCounterTagObjects[index], skillCounterTexts[index], new Vector2(0.630f, 0.125f), new Vector2(0.805f, 0.440f));
        if (skillPowerTexts[index] != null)
            skillPowerTexts[index].color = PowerTagTextColor;

        // Tag borders speak the chip colour language: CP cost blue, raw power
        // warm, counter green (mirrors the status chips on the combatant cards).
        TintSkillTag(skillCPTagObjects[index], SkillTagCPBorder);
        TintSkillTag(skillPowerTagObjects[index], SkillTagPowerBorder);
        TintSkillTag(skillCounterTagObjects[index], SkillTagCounterBorder);

        // Effectiveness preview vs the current opponent: a compact arrow keeps
        // matchup feedback readable without crowding the skill tags.
        Image effectivenessRoot;
        if (skillEffectivenessTagObjects[index] == null)
        {
            effectivenessRoot = EnsureChildImage(root, "EffectivenessTag");
            skillEffectivenessTagObjects[index] = effectivenessRoot.gameObject;
        }
        else
        {
            effectivenessRoot = skillEffectivenessTagObjects[index].GetComponent<Image>();
        }

        if (skillEffectivenessIcons[index] == null)
            skillEffectivenessIcons[index] = EnsureChildImage(skillEffectivenessTagObjects[index].transform, "Triangle");
        if (skillEffectivenessTexts[index] == null)
            skillEffectivenessTexts[index] = EnsureChildText(skillEffectivenessTagObjects[index].transform, "Text", 1, TextAnchor.MiddleCenter);

        PositionEffectivenessIndicator(index);
        ConfigureTransparentImage(effectivenessRoot);

        SetStretchRect(skillEffectivenessIcons[index].rectTransform, Vector2.zero, Vector2.one);
        skillEffectivenessIcons[index].type = Image.Type.Simple;
        skillEffectivenessIcons[index].preserveAspect = true;
        skillEffectivenessIcons[index].raycastTarget = false;

        skillEffectivenessTexts[index].gameObject.SetActive(false);
        ApplySkillEffectiveness(index);

        // Keyboard hint: small bordered square on the top-right edge; pressing
        // the matching 1-4 key clicks the slot (HandleSkillHotkeys).
        Image hotkeyChip = EnsureChildImage(root, "HotkeyChip");
        SetStretchRect(hotkeyChip.rectTransform, new Vector2(0.935f, 0.675f), new Vector2(0.985f, 0.930f));
        ConfigureChromeChip(hotkeyChip, 2.6f);
        skillHotkeyTexts[index] = EnsureChildText(hotkeyChip.transform, "Text", 11, TextAnchor.MiddleCenter);
        skillHotkeyTexts[index].text = (index + 1).ToString();
        skillHotkeyTexts[index].font = ReadableHudFont();
        skillHotkeyTexts[index].fontStyle = FontStyle.Bold;
        skillHotkeyTexts[index].color = HotkeyHintColor;
        skillHotkeyTexts[index].raycastTarget = false;
        if (skillCPTagObjects[index] != null)
        {
            switchElementChipIcons[index] = EnsureChildImage(skillCPTagObjects[index].transform, "SwitchElementIcon");
            SetStretchRect(switchElementChipIcons[index].rectTransform, new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.88f));
            switchElementChipIcons[index].type = Image.Type.Simple;
            switchElementChipIcons[index].preserveAspect = true;
            switchElementChipIcons[index].raycastTarget = false;
            switchElementChipIcons[index].gameObject.SetActive(false);
            switchElementChipIcons[index].transform.SetAsLastSibling();
        }

        // Hover / press feedback: a glowing cyber selection frame traces the row
        // border under the cursor (falls back to a chrome wash if the select
        // sprite is missing), plus a slight scale so the slot reads as live.
        Image hoverHighlight = EnsureChildImage(root, "HoverHighlight");
        SetStretchRect(hoverHighlight.rectTransform, new Vector2(-0.01f, -0.04f), new Vector2(1.01f, 1.04f));
        hoverHighlight.raycastTarget = false;
        Sprite selectFrame = SkillSelectFrameSprite();
        if (selectFrame != null)
        {
            hoverHighlight.sprite = selectFrame;
            hoverHighlight.type = Image.Type.Sliced;
            hoverHighlight.pixelsPerUnitMultiplier = 1.5f;
        }
        else
        {
            ConfigureChromeChip(hoverHighlight, 2.2f);
        }
        hoverHighlight.transform.SetAsLastSibling();

        // Dim the row to ~0.4 alpha when its skill is unavailable (e.g. not enough CP).
        CanvasGroup slotGroup = root.GetComponent<CanvasGroup>();
        if (slotGroup == null)
            slotGroup = root.gameObject.AddComponent<CanvasGroup>();

        BattleHudButtonFeedback slotFeedback = root.GetComponent<BattleHudButtonFeedback>();
        if (slotFeedback == null)
            slotFeedback = root.gameObject.AddComponent<BattleHudButtonFeedback>();
        slotFeedback.Configure(skillButtons[index], 1.02f, 0.975f);
        slotFeedback.SetOverlay(hoverHighlight, SkillSlotBorderColor, 0.85f, 1f, 0.6f);
        slotFeedback.SetDimGroup(slotGroup, 1f, 0.4f);
    }

    private void ApplySkillSlotLayout(int index)
    {
        if (!IndexInRange(index))
            return;

        bool hasPanelBackdrop = SkillPanelBackdropSprite() != null;
        RectTransform nameRect = skillNameTexts[index] != null
            ? skillNameTexts[index].GetComponent<RectTransform>()
            : null;
        if (nameRect != null)
        {
            float nameLeft = hasPanelBackdrop ? 0.225f : 0.145f;
            float nameRight = hasPanelBackdrop ? 0.680f : 0.515f;
            float nameBottom = hasPanelBackdrop ? 0.500f : 0.10f;
            float nameTop = hasPanelBackdrop ? 0.890f : 0.90f;
            SetStretchRect(nameRect, new Vector2(nameLeft, nameBottom), new Vector2(nameRight, nameTop));
            ConfigureSkillNameText(skillNameTexts[index], hasPanelBackdrop);
        }

        if (switchPortraitImages[index] != null)
            switchPortraitImages[index].gameObject.SetActive(false);
    }

    private void ApplySwitchSlotLayout(int index)
    {
        if (!IndexInRange(index))
            return;

        RectTransform nameRect = skillNameTexts[index] != null
            ? skillNameTexts[index].GetComponent<RectTransform>()
            : null;
        if (nameRect != null)
        {
            SetStretchRect(nameRect, new Vector2(0.255f, 0.555f), new Vector2(0.930f, 0.885f));
            ConfigureSwitchNameText(skillNameTexts[index]);
        }

        if (skillInstructionFrames[index] != null)
            skillInstructionFrames[index].gameObject.SetActive(false);
        if (skillElementBadges[index] != null)
            skillElementBadges[index].gameObject.SetActive(false);
    }

    private void SetSwitchPortrait(int index, Sprite sprite)
    {
        if (!IndexInRange(index) || switchPortraitImages[index] == null)
            return;

        switchPortraitImages[index].sprite = sprite;
        switchPortraitImages[index].enabled = sprite != null;
        switchPortraitImages[index].gameObject.SetActive(sprite != null);
        switchPortraitImages[index].color = Color.white;
    }

    private void SetSwitchElementChip(int index, bool visible, ElementType elementType)
    {
        if (!IndexInRange(index))
            return;

        Sprite icon = visible ? ElementIconSprite(elementType) : null;
        if (switchElementChipIcons[index] != null)
        {
            switchElementChipIcons[index].sprite = icon;
            switchElementChipIcons[index].color = Color.white;
            switchElementChipIcons[index].gameObject.SetActive(visible && icon != null);
        }

        if (skillCPTexts[index] != null)
            skillCPTexts[index].gameObject.SetActive(!visible || icon == null);
    }

    private void LayoutSkillTags(int index, bool showCP, bool showPower, bool showCounter)
    {
        if (!IndexInRange(index))
            return;

        float x = 0.225f;
        const float y0 = 0.125f;
        const float y1 = 0.440f;
        const float gap = 0.025f;
        const float defaultWidth = 0.165f;

        if (showCP)
            PositionSkillTag(skillCPTagObjects[index], ref x, defaultWidth, gap, y0, y1);
        else
            PositionSkillTag(skillCPTagObjects[index], new Vector2(0.225f, y0), new Vector2(0.390f, y1));

        if (showPower)
            PositionSkillTag(skillPowerTagObjects[index], ref x, 0.185f, gap, y0, y1);
        else
            PositionSkillTag(skillPowerTagObjects[index], new Vector2(0.415f, y0), new Vector2(0.605f, y1));

        if (showCounter)
            PositionSkillTag(skillCounterTagObjects[index], ref x, 0.175f, gap, y0, y1);
        else
            PositionSkillTag(skillCounterTagObjects[index], new Vector2(0.630f, y0), new Vector2(0.805f, y1));
    }

    private void LayoutSwitchTags(int index)
    {
        if (!IndexInRange(index))
            return;

        PositionSkillTag(skillCPTagObjects[index], new Vector2(0.255f, 0.150f), new Vector2(0.430f, 0.445f));
        PositionSkillTag(skillPowerTagObjects[index], new Vector2(0.455f, 0.150f), new Vector2(0.610f, 0.445f));
        PositionSkillTag(skillCounterTagObjects[index], new Vector2(0.635f, 0.150f), new Vector2(0.930f, 0.445f));
    }

    private static void PositionSkillTag(GameObject tagObject, ref float x, float width, float gap, float y0, float y1)
    {
        PositionSkillTag(tagObject, new Vector2(x, y0), new Vector2(x + width, y1));
        x += width + gap;
    }

    private static void PositionSkillTag(GameObject tagObject, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = tagObject != null ? tagObject.GetComponent<RectTransform>() : null;
        if (rect != null)
            SetStretchRect(rect, anchorMin, anchorMax);
    }

    private void ConfigureSkillNameText(Text text, bool hasPanelBackdrop)
    {
        if (text == null)
            return;

        text.font = ReadableHudFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = hasPanelBackdrop ? 18 : Mathf.Max(18, text.fontSize);
        text.alignment = TextAnchor.MiddleLeft;
        // Wrap (not Overflow) so best-fit shrinks long names to their box instead of
        // spilling rightward over the element icon (e.g. "Hyper-Threading").
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = hasPanelBackdrop ? 18 : Mathf.Max(18, text.fontSize);
        text.color = Color.white;
        EnsureShadow(text.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.4f, -1.4f));
    }

    private void ConfigureSwitchNameText(Text text)
    {
        if (text == null)
            return;

        text.font = ReadableHudFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 17;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 11;
        text.resizeTextMaxSize = 17;
        text.color = Color.white;
        EnsureShadow(text.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.4f, -1.4f));
    }

    private void ConfigureSkillTag(GameObject tagObject, Text text, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (tagObject == null)
            return;

        RectTransform rect = tagObject.GetComponent<RectTransform>();
        if (rect != null)
            SetStretchRect(rect, anchorMin, anchorMax);

        Image image = tagObject.GetComponent<Image>();
        if (image == null)
            image = tagObject.AddComponent<Image>();
        // Unified cyan pixel-tech chip (matches the A/D/S badge + panels).
        ConfigureChromeChip(image, 3f);
        image.raycastTarget = false;

        if (text == null)
            return;

        RectTransform textRect = text.GetComponent<RectTransform>();
        SetStretchRect(textRect, Vector2.zero, Vector2.one);
        if (textRect != null)
        {
            textRect.offsetMin = new Vector2(1f, 0f);
            textRect.offsetMax = new Vector2(-1f, 0f);
        }
        text.font = ReadableHudFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = Mathf.Max(12, text.fontSize);
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 9;
        text.resizeTextMaxSize = Mathf.Max(13, text.fontSize);
        text.color = SkillTagTextColor;
        text.raycastTarget = false;
        EnsureShadow(text.gameObject, new Color(0f, 0f, 0f, 0.65f), new Vector2(1f, -1f));
    }

    private void SetSkillSlotBadges(int index, SkillData skill)
    {
        if (!IndexInRange(index))
            return;

        bool visible = skill != null;
        if (skillInstructionFrames[index] != null)
            skillInstructionFrames[index].gameObject.SetActive(visible);
        if (skillElementBadges[index] != null)
            skillElementBadges[index].gameObject.SetActive(visible);
        if (skillElementIconImages[index] != null)
            skillElementIconImages[index].gameObject.SetActive(false);
        SetTypeStrip(index, visible, visible ? InstructionStripColor(skill.instructionType) : Color.white);

        if (!visible)
            return;

        Sprite instructionIcon = InstructionIconSprite(skill.instructionType);
        Color instructionColor = InstructionTextColor(skill.instructionType);
        if (skillInstructionFrames[index] != null)
            skillInstructionFrames[index].color = Color.Lerp(Color.white, instructionColor, 0.18f);

        if (skillInstructionIcons[index] != null)
        {
            skillInstructionIcons[index].sprite = instructionIcon;
            // A gentle tint toward the type colour keeps the badges colour-coded
            // (attack=orange, defense=cyan, status=violet) without muddying the glyph.
            skillInstructionIcons[index].color = instructionIcon != null
                ? Color.Lerp(Color.white, instructionColor, 0.45f)
                : new Color(1f, 1f, 1f, 0f);
            skillInstructionIcons[index].gameObject.SetActive(instructionIcon != null);
        }

        if (skillInstructionTexts[index] != null)
        {
            // Letter is the fallback when the icon resource is missing.
            skillInstructionTexts[index].text = InstructionShortLabel(skill.instructionType);
            skillInstructionTexts[index].fontSize = skill.instructionType == InstructionType.Status ? 17 : 20;
            skillInstructionTexts[index].resizeTextMinSize = 11;
            skillInstructionTexts[index].resizeTextMaxSize = skill.instructionType == InstructionType.Status ? 17 : 20;
            skillInstructionTexts[index].color = instructionColor;
            skillInstructionTexts[index].gameObject.SetActive(instructionIcon == null);
        }

        Sprite elementIcon = ElementIconSprite(skill.elementType);
        if (skillElementBadges[index] != null)
            skillElementBadges[index].color = elementIcon == null
                ? ElementBadgeColor(skill.elementType)
                : new Color(0f, 0f, 0f, 0f);

        if (skillElementIconImages[index] != null)
        {
            skillElementIconImages[index].sprite = elementIcon;
            skillElementIconImages[index].type = Image.Type.Simple;
            skillElementIconImages[index].color = Color.white;
            skillElementIconImages[index].gameObject.SetActive(elementIcon != null);
        }

        if (skillElementTexts[index] != null)
        {
            skillElementTexts[index].text = ElementShortLabel(skill.elementType);
            skillElementTexts[index].gameObject.SetActive(elementIcon == null);
        }
    }

    private void SetTypeStrip(int index, bool visible, Color strip)
    {
        if (!IndexInRange(index) || skillTypeStrips[index] == null)
            return;

        skillTypeStrips[index].gameObject.SetActive(visible);
        if (!visible)
            return;

        strip.a = 0.95f;
        skillTypeStrips[index].color = strip;
    }

    /// <summary>
    /// Tells the HUD which element the current opponent has so skill and switch
    /// slots can preview strong / weak matchups.
    /// BattleManager calls this from RefreshHud, so enemy switches re-evaluate.
    /// </summary>
    public void SetOpposingElement(ElementType elementType)
    {
        opposingElement = elementType;
        opposingElementKnown = true;
        for (int i = 0; i < MaxSkillSlots; i++)
            ApplySkillEffectiveness(i);
    }

    private void ApplySkillEffectiveness(int index)
    {
        if (!IndexInRange(index))
            return;

        GameObject tagObject = skillEffectivenessTagObjects[index];
        Image icon = skillEffectivenessIcons[index];
        if (tagObject == null || icon == null)
            return;

        PositionEffectivenessIndicator(index);

        if (!opposingElementKnown || !skillSlotShowsMatchup[index])
        {
            tagObject.SetActive(false);
            return;
        }

        float multiplier = CombatResolver.GetElementMultiplier(skillSlotElements[index], opposingElement);
        if (Mathf.Approximately(multiplier, 1f))
        {
            tagObject.SetActive(false);
            return;
        }

        bool strong = multiplier > 1f;
        tagObject.SetActive(true);
        icon.sprite = EffectivenessTriangleSprite(strong);
        icon.color = strong ? EffectivenessStrongColor : EffectivenessWeakColor;
        icon.preserveAspect = true;

        if (skillEffectivenessTexts[index] != null)
            skillEffectivenessTexts[index].gameObject.SetActive(false);
    }

    private void PositionEffectivenessIndicator(int index)
    {
        if (!IndexInRange(index) || skillEffectivenessTagObjects[index] == null)
            return;

        RectTransform rect = skillEffectivenessTagObjects[index].GetComponent<RectTransform>();
        if (rect == null)
            return;

        if (skillSlotIsSwitch[index])
            SetStretchRect(rect, new Vector2(0.865f, 0.555f), new Vector2(0.915f, 0.815f));
        else
            SetStretchRect(rect, new Vector2(0.815f, 0.575f), new Vector2(0.875f, 0.845f));
    }

    /// <summary>
    /// Recolours each slot's CP tag red while the player can't afford it, so a
    /// dimmed row also says WHY it is unavailable. Driven by the player-side
    /// CP value; switch slots keep cost 0 and never flag.
    /// </summary>
    private void RefreshCPAffordability()
    {
        int currentCP = playerDisplay.CPInitialized ? playerDisplay.TargetCP : int.MaxValue;
        for (int i = 0; i < MaxSkillSlots; i++)
        {
            if (skillCPTexts[i] == null)
                continue;

            bool shortfall = skillCPCosts[i] > currentCP;
            skillCPTexts[i].color = shortfall ? CPShortfallColor : SkillTagTextColor;
            TintSkillTag(skillCPTagObjects[i], shortfall ? ChipHarmBorder : SkillTagCPBorder);
        }
    }

    private static void TintSkillTag(GameObject tagObject, Color borderTint)
    {
        Image image = tagObject != null ? tagObject.GetComponent<Image>() : null;
        if (image == null)
            return;

        image.sprite = ChipFrameSprite();
        image.type = Image.Type.Sliced;
        image.color = borderTint;
    }

    private void HandleSkillHotkeys()
    {
        // Only while the command bar is visible/interactable (player's turn) and
        // no post-battle overlay is up. Keys 1-4 mirror clicking the slots, so
        // they also work for switch-target selection.
        if (commandPanelGroup == null || !commandPanelGroup.interactable || commandPanelGroup.alpha <= 0.5f)
            return;
        if (postBattlePanel != null && postBattlePanel.activeSelf)
            return;

        for (int i = 0; i < MaxSkillSlots; i++)
        {
            if (!Input.GetKeyDown(SkillHotkeys[i]) && !Input.GetKeyDown(SkillHotkeysKeypad[i]))
                continue;

            Button button = skillButtons[i];
            if (button != null && button.interactable && button.isActiveAndEnabled)
                button.onClick.Invoke();
            break;
        }
    }

    private Sprite ElementIconSprite(ElementType elementType)
    {
        int index = (int)elementType;
        if (index < 0 || index >= elementIconSprites.Length)
            return null;

        if (elementIconSprites[index] == null)
            elementIconSprites[index] = Resources.Load<Sprite>($"{ElementIconResourcePrefix}{elementType}");
        return elementIconSprites[index];
    }

    private Sprite InstructionIconSprite(InstructionType instructionType)
    {
        int index = (int)instructionType;
        if (index < 0 || index >= instructionIconSprites.Length)
            return null;

        if (instructionIconSprites[index] == null)
            instructionIconSprites[index] = Resources.Load<Sprite>($"{InstructionIconResourcePrefix}{instructionType}");
        return instructionIconSprites[index];
    }

    private Sprite SkillSelectFrameSprite()
    {
        if (skillSelectFrameSprite == null)
            skillSelectFrameSprite = Resources.Load<Sprite>(SkillSelectFrameResourcePath);
        return skillSelectFrameSprite;
    }

    private static string InstructionShortLabel(InstructionType instructionType)
    {
        switch (instructionType)
        {
            case InstructionType.Attack:  return "A";
            case InstructionType.Defense: return "D";
            case InstructionType.Status:  return "S";
            default:                      return "?";
        }
    }

    private static Color InstructionTextColor(InstructionType instructionType)
    {
        switch (instructionType)
        {
            case InstructionType.Attack:  return new Color(1.00f, 0.36f, 0.30f, 1f);
            case InstructionType.Defense: return new Color(0.38f, 0.86f, 1.00f, 1f);
            case InstructionType.Status:  return new Color(0.45f, 1.00f, 0.55f, 1f);
            default:                      return Color.white;
        }
    }

    private static Color InstructionStripColor(InstructionType instructionType)
    {
        switch (instructionType)
        {
            case InstructionType.Attack:  return new Color(1.00f, 0.18f, 0.12f, 1f);
            case InstructionType.Defense: return new Color(0.18f, 0.64f, 1.00f, 1f);
            case InstructionType.Status:  return new Color(0.28f, 0.95f, 0.38f, 1f);
            default:                      return Color.white;
        }
    }

    private static string ElementShortLabel(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Electric: return "EL";
            case ElementType.Water:    return "WA";
            case ElementType.Fire:     return "FI";
            case ElementType.Ground:   return "GR";
            case ElementType.Grass:    return "LE";
            case ElementType.Ice:      return "IC";
            case ElementType.Normal:   return "NO";
            default:                   return "--";
        }
    }

    private static string ElementSwitchLabel(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Electric: return "ELEC";
            case ElementType.Water:    return "WATR";
            case ElementType.Fire:     return "FIRE";
            case ElementType.Ground:   return "GRND";
            case ElementType.Grass:    return "GRAS";
            case ElementType.Ice:      return "ICE";
            case ElementType.Normal:   return "NORM";
            default:                   return "TYPE";
        }
    }

    private static Color ElementBadgeColor(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Electric: return new Color(1.00f, 0.82f, 0.15f, 1f);
            case ElementType.Water:    return new Color(0.18f, 0.58f, 0.95f, 1f);
            case ElementType.Fire:     return new Color(1.00f, 0.35f, 0.08f, 1f);
            case ElementType.Ground:   return new Color(0.55f, 0.36f, 0.20f, 1f);
            case ElementType.Grass:    return new Color(0.22f, 0.66f, 0.20f, 1f);
            case ElementType.Ice:      return new Color(0.40f, 0.88f, 1.00f, 1f);
            case ElementType.Normal:   return new Color(0.78f, 0.78f, 0.74f, 1f);
            default:                   return Color.white;
        }
    }

    private Image EnsureChildImage(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.layer = parent.gameObject.layer;
            child = childObject.transform;
            child.SetParent(parent, false);
        }
        else
        {
            child.gameObject.layer = parent.gameObject.layer;
        }

        Image image = child.GetComponent<Image>();
        if (image == null)
            image = child.gameObject.AddComponent<Image>();
        return image;
    }

    private Text EnsureChildText(Transform parent, string childName, int fontSize, TextAnchor alignment)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            childObject.layer = parent.gameObject.layer;
            child = childObject.transform;
            child.SetParent(parent, false);
        }
        else
        {
            child.gameObject.layer = parent.gameObject.layer;
        }

        RectTransform rect = child.GetComponent<RectTransform>();
        SetStretchRect(rect, Vector2.zero, Vector2.one);

        Text text = child.GetComponent<Text>();
        if (text == null)
            text = child.gameObject.AddComponent<Text>();
        text.font = AnnouncerFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(7, fontSize - 8);
        text.resizeTextMaxSize = fontSize;
        return text;
    }

    private static void ConfigureFramedImage(Image image, Sprite sprite, Color fallbackColor)
    {
        if (image == null)
            return;

        image.raycastTarget = false;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = Color.white;
            return;
        }

        image.sprite = null;
        image.color = fallbackColor;
    }

    private static void ConfigureTransparentImage(Image image)
    {
        if (image == null)
            return;

        image.raycastTarget = false;
        image.sprite = null;
        image.color = new Color(0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// Builds (and caches) the shared 9-slice HUD panel sprite: a dark
    /// translucent fill wrapped in a 2px cyan pixel border with chamfered
    /// corners. Purely procedural so no art asset or scene edit is needed and
    /// the look is identical across every battle panel.
    /// </summary>
    private static Sprite HudPanelSprite()
    {
        if (hudPanelSprite == null)
            hudPanelSprite = BuildChamferedPanelSprite(HudPanelFill, (Color)HudPanelBorder, "HudPanelChrome");
        return hudPanelSprite;
    }

    /// <summary>
    /// White-bordered sibling of the panel chrome for tintable chips: the
    /// Image colour multiplies through, so the border takes the tone colour
    /// while the dark fill stays dark. Drawn in code — no art asset needed.
    /// Bilinear-filtered: at chip scale the point-filtered texture rounds the
    /// border to a different pixel count per chip, making equal chips render
    /// with visibly different edge thickness.
    /// </summary>
    private static Sprite ChipFrameSprite()
    {
        if (chipFrameSprite == null)
            chipFrameSprite = BuildChamferedPanelSprite(
                new Color(0.16f, 0.20f, 0.24f, 0.94f), Color.white, "HudChipFrame", FilterMode.Bilinear);
        return chipFrameSprite;
    }

    private static Sprite BuildChamferedPanelSprite(Color fill, Color edge, string spriteName, FilterMode filterMode = FilterMode.Point)
    {
        const int size = 28;
        const int chamfer = 5; // diagonal corner cut, in pixels
        const int border = 2;  // straight + diagonal edge thickness
        const int slice = chamfer + border; // 9-slice margin keeps corners crisp

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int l = x;
                int r = size - 1 - x;
                int b = y;
                int t = size - 1 - y;

                // Chamfered corners: cut a diagonal wedge out of each corner.
                bool cut = (l + b < chamfer) || (r + b < chamfer) ||
                           (l + t < chamfer) || (r + t < chamfer);
                if (cut)
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                bool diagonalEdge = (l + b < chamfer + border) || (r + b < chamfer + border) ||
                                    (l + t < chamfer + border) || (r + t < chamfer + border);
                int straight = Mathf.Min(Mathf.Min(l, r), Mathf.Min(b, t));
                bool straightEdge = straight < border;

                texture.SetPixel(x, y, diagonalEdge || straightEdge ? edge : fill);
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

    /// <summary>
    /// Skins a panel background Image with the shared cyber-glass chrome and a
    /// soft cyan outline glow. Leaves raycast/interaction state untouched.
    /// </summary>
    private static void ApplyPanelChrome(Image image)
    {
        if (image == null)
            return;

        image.enabled = true;
        image.sprite = HudPanelSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.5f;
        image.color = Color.white;
        EnsureGlow(image.gameObject);
    }

    private static void ClearPanelChrome(Image image)
    {
        if (image == null)
            return;

        image.enabled = false;
        image.raycastTarget = false;
        Outline glow = image.GetComponent<Outline>();
        if (glow != null)
            glow.enabled = false;
    }

    private static void EnsureGlow(GameObject target)
    {
        if (target == null)
            return;

        Outline glow = target.GetComponent<Outline>();
        if (glow == null)
            glow = target.AddComponent<Outline>();
        glow.effectColor = HudPanelGlow;
        glow.effectDistance = new Vector2(2f, -2f);
        glow.useGraphicAlpha = false;
        glow.enabled = true;
    }

    private void ApplyUnifiedPanelChrome()
    {
        ApplyPanelChrome(Find<Image>("SafeArea/TopBar"));
        ApplyPanelChrome(Find<Image>("SafeArea/CombatLayer/PlayerCombatantPanel"));

        Image enemyPanel = Find<Image>("SafeArea/CombatLayer/EnemyCombatantPanel");
        ApplyPanelChrome(enemyPanel);
        // Position/size of the enemy card is authored in BattleHud.prefab now;
        // edit it there. Code only applies the chrome (sprite + glow).

        ClearPanelChrome(Find<Image>("SafeArea/CommandPanel/ActionPanel"));
        ApplyPanelChrome(FindSkillDetailPanelImage());

        // Give both status cards an identical inner treatment (framed pixel
        // energy bars + matching label chrome).
        StyleCombatantCard("SafeArea/CombatLayer/PlayerCombatantPanel");
        StyleCombatantCard("SafeArea/CombatLayer/EnemyCombatantPanel");
        // SkillPanel chrome is applied inside EnsureSkillPanelPresentation, and
        // the BattleAnnouncer inside ConfigureAnnouncerFrame, so their own
        // update paths don't overwrite it.
    }

    /// <summary>
    /// Skins a small inline element (A/D/S badge, CP/BP/Counter tag) with the
    /// same cyber-glass chrome as the panels, but with a thinner border so the
    /// chip stays legible at row scale.
    /// </summary>
    private static void ConfigureChromeChip(Image image, float pixelsPerUnitMultiplier)
    {
        if (image == null)
            return;

        image.raycastTarget = false;
        image.sprite = HudPanelSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        image.color = Color.white;
    }

    /// <summary>
    /// Applies the shared status-card treatment. The Battery frame + fill (sprite,
    /// colour, position, octagon nesting) are authored in BattleHud.prefab now;
    /// this only styles the CP well. Purely cosmetic; no value/logic touched.
    /// </summary>
    private void StyleCombatantCard(string root)
    {
        // CP: frame the well and flatten cells into solid pixel squares.
        Image cpInterior = Find<Image>($"{root}/CPDots/Interior");
        if (cpInterior != null)
            ConfigureChromeChip(cpInterior, 2.4f);

        Transform cpCells = FindTransform($"{root}/CPDots/CPCells");
        if (cpCells != null)
        {
            foreach (Transform cell in cpCells)
            {
                Image cellImage = cell.GetComponent<Image>();
                if (cellImage != null)
                {
                    cellImage.sprite = null;
                    cellImage.raycastTarget = false;
                }
            }
        }
    }

    private static void EnsureShadow(GameObject target, Color color, Vector2 distance)
    {
        if (target == null)
            return;

        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
            shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
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

    private static void SetFixedLeftRect(RectTransform rect, float left, float size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(size, size);
    }

    private void ConfigureAnnouncerFrame()
    {
        if (announcerFrame == null)
            return;

        // Top prompt now shares the unified dark cyber-glass chrome instead of
        // the old large green panel. The brief green pulse in
        // ApplyAnnouncerFrameColor is kept as the only small green accent.
        ApplyPanelChrome(announcerFrame);
    }

    private void UpdateAnnouncerPulse()
    {
        if (announcerFrame == null || announcerPulseSeconds <= 0f)
            return;

        if (announcerPulseTimer > 0f)
            announcerPulseTimer = Mathf.Max(0f, announcerPulseTimer - Time.deltaTime);

        float pulse = announcerPulseSeconds <= 0f ? 0f : announcerPulseTimer / announcerPulseSeconds;
        ApplyAnnouncerFrameColor(pulse);
    }

    private void ApplyAnnouncerFrameColor(float pulse)
    {
        if (announcerFrame == null)
            return;

        float clampedPulse = Mathf.Clamp01(pulse);
        if (announcerFrame.sprite != null)
        {
            Color pulseTint = new Color(0.78f, 1f, 0.72f, 1f);
            announcerFrame.color = Color.Lerp(Color.white, pulseTint, clampedPulse * 0.35f);
            return;
        }

        announcerFrame.color = Color.Lerp(announcerFrameBaseColor, announcerFramePulseColor, clampedPulse);
    }

    private void UpdateResourceDisplay(CombatantRefs refs, ref CombatantDisplayState display, bool allowCPPreview)
    {
        if (display.BatteryInitialized)
        {
            display.DisplayBattery = SmoothTo(
                display.DisplayBattery,
                display.TargetBattery,
                batteryLerpSpeed,
                Time.deltaTime);

            // Ghost afterimage: hold, then melt toward the live value.
            if (display.GhostHoldTimer > 0f)
                display.GhostHoldTimer = Mathf.Max(0f, display.GhostHoldTimer - Time.deltaTime);
            else if (display.GhostBattery > display.TargetBattery)
                display.GhostBattery = SmoothTo(
                    display.GhostBattery,
                    display.TargetBattery,
                    batteryGhostDrainSpeed,
                    Time.deltaTime);
            if (display.GhostBattery < display.DisplayBattery)
                display.GhostBattery = display.DisplayBattery;

            ApplyBatteryVisual(refs, display.DisplayBattery, display.GhostBattery, display.TargetBatteryMax);
        }

        if (display.CPInitialized)
        {
            display.DisplayCP = SmoothTo(
                display.DisplayCP,
                display.TargetCP,
                cpLerpSpeed,
                Time.deltaTime);
            ApplyCPVisual(refs, display.DisplayCP, display.TargetCPMax, allowCPPreview);
        }
    }

    private static float SmoothTo(float current, float target, float speed, float deltaTime)
    {
        if (speed <= 0f)
            return target;

        float t = 1f - Mathf.Exp(-speed * deltaTime);
        float value = Mathf.Lerp(current, target, t);
        return Mathf.Abs(value - target) <= 0.01f ? target : value;
    }

    private void ApplyBatteryVisual(CombatantRefs refs, float current, float ghost, int max)
    {
        // The fill's sprite, colour, Filled-mode and position are authored in the
        // prefab; only the dynamic clip amount is driven here. The ghost and
        // warning wash are runtime layers created in EnsureBatteryFeedbackLayers.
        float ratio = max <= 0 ? 0f : Mathf.Clamp01(current / max);
        float ghostRatio = max <= 0 ? 0f : Mathf.Clamp01(ghost / max);

        if (refs.BatteryFill != null)
            refs.BatteryFill.fillAmount = ratio;
        if (refs.BatteryGhost != null)
            refs.BatteryGhost.fillAmount = ghostRatio;

        ApplyLowBatteryWarning(refs, ratio);
    }

    private void ApplyLowBatteryWarning(CombatantRefs refs, float ratio)
    {
        bool danger = ratio <= lowBatteryDangerRatio;
        if (refs.BatteryWash != null)
        {
            Color wash;
            if (danger)
            {
                wash = BatteryDangerWash;
                wash.a *= 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f));
            }
            else if (ratio <= lowBatteryWarnRatio)
            {
                wash = BatteryWarnWash;
            }
            else
            {
                wash = new Color(0f, 0f, 0f, 0f);
            }

            refs.BatteryWash.color = wash;
            refs.BatteryWash.fillAmount = ratio;
        }

        if (refs.BatteryValueText != null)
            refs.BatteryValueText.color = danger ? BatteryDangerText : Color.white;
    }

    private void ApplyCPVisual(CombatantRefs refs, float current, int max, bool allowPreview = false)
    {
        if (refs.CPDots == null)
            return;

        float filledSegments = refs.CPDots.Length <= 0 || max <= 0
            ? 0f
            : Mathf.Clamp01(current / max) * refs.CPDots.Length;

        int previewStart = -1;
        int previewEnd = -1;
        if (allowPreview && cpPreviewActive && cpPreviewCost > 0 && max > 0 && refs.CPDots.Length > 0)
        {
            int filledCount = Mathf.Clamp(
                Mathf.RoundToInt((playerDisplay.TargetCP / (float)max) * refs.CPDots.Length),
                0,
                refs.CPDots.Length);
            int previewCount = Mathf.Min(cpPreviewCost, filledCount);
            previewStart = filledCount - previewCount;
            previewEnd = filledCount - 1;
        }

        for (int i = 0; i < refs.CPDots.Length; i++)
        {
            Image dot = refs.CPDots[i];
            if (dot == null)
                continue;

            float fill = Mathf.Clamp01(filledSegments - i);
            Color color = Color.Lerp(CPDotInactive, CPDotActive, fill);
            if (i >= previewStart && i <= previewEnd)
            {
                float pulse = Mathf.PingPong(Time.unscaledTime * 3.2f, 1f);
                color = CPDotActive;
                color.a = Mathf.Lerp(0.20f, 0.92f, pulse);
            }

            dot.color = color;
        }
    }

    private CombatantRefs BindCombatant(string root)
    {
        var refs = new CombatantRefs
        {
            NameText         = FindText($"{root}/NameText"),
            LevelText        = FindText($"{root}/LevelText"),
            BatteryValueText = FindText($"{root}/BatteryBar/ValueText"),
            BatteryFill      = Find<Image>($"{root}/BatteryBar/Fill"),
            CPValueText      = FindText($"{root}/CPDots/CPValueText"),
            StatusText       = FindText($"{root}/StatusRow/StatusText"),
            CPDots           = new Image[0],
        };

        Transform cpRow = transform.Find($"{root}/CPDots");
        if (cpRow != null)
            refs.CPDots = FindCPDots(cpRow);

        EnsureBatteryFeedbackLayers(ref refs);
        EnsureCombatantElementIcon(ref refs);
        EnsureStatusChipPool(ref refs);
        EnsureSubroutineLabel(ref refs, root);
        ConfigureCombatantTextHierarchy(refs);
        return refs;
    }

    /// <summary>
    /// Always-visible passive line on the combatant card, sitting in the empty
    /// band between the name and the battery bar. Shows the subroutine name and
    /// trigger at a glance; the full description stays on the card hover/click.
    /// </summary>
    private void EnsureSubroutineLabel(ref CombatantRefs refs, string root)
    {
        Transform panel = transform.Find(root);
        if (panel == null)
            return;

        Text label = EnsureChildText(panel, "SubroutineLabel", 14, TextAnchor.MiddleLeft);
        label.font = ReadableHudFont();
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(0.80f, 0.72f, 1f, 0.96f);
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 9;
        label.resizeTextMaxSize = 14;

        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.06f, 0.635f);
        rt.anchorMax = new Vector2(0.95f, 0.715f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        EnsureShadow(label.gameObject, new Color(0f, 0f, 0f, 0.7f), new Vector2(1f, -1f));

        refs.SubroutineLabel = label;
    }

    /// <summary>
    /// Builds the pooled status chips that replace the long status text:
    /// short tone-coloured labels (READY / FW +2 / BRN ...) laid out after the
    /// "Status" prefix. All runtime-built with the procedural chip frame.
    /// </summary>
    private void EnsureStatusChipPool(ref CombatantRefs refs)
    {
        if (refs.StatusText == null)
            return;

        Transform row = refs.StatusText.transform.parent;
        if (row == null)
            return;

        refs.StatusChips = new Image[MaxStatusChips];
        refs.StatusChipTexts = new Text[MaxStatusChips];
        for (int i = 0; i < MaxStatusChips; i++)
        {
            Image chip = EnsureChildImage(row, $"StatusChip_{i + 1}");
            chip.sprite = ChipFrameSprite();
            chip.type = Image.Type.Sliced;
            chip.pixelsPerUnitMultiplier = 2.2f;
            chip.raycastTarget = false;

            // Point anchors + explicit integer size (set per chip in
            // ApplyStatusChip) so every chip shares the exact same pixel rect;
            // fractional-height rects rasterized the border unevenly per chip.
            RectTransform chipRect = chip.rectTransform;
            chipRect.anchorMin = new Vector2(0f, 0.5f);
            chipRect.anchorMax = new Vector2(0f, 0.5f);
            chipRect.pivot = new Vector2(0f, 0.5f);
            chipRect.anchoredPosition = Vector2.zero;
            chipRect.sizeDelta = new Vector2(34f, 28f);

            Text label = EnsureChildText(chip.transform, "Text", 12, TextAnchor.MiddleCenter);
            label.font = ReadableHudFont();
            label.fontStyle = FontStyle.Bold;
            label.resizeTextMinSize = 8;
            label.raycastTarget = false;
            EnsureShadow(label.gameObject, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -1f));

            chip.gameObject.SetActive(false);
            refs.StatusChips[i] = chip;
            refs.StatusChipTexts[i] = label;
        }
    }

    /// <summary>
    /// Renders the combatant's status as short colour-coded chips after the
    /// "Status" label. Order is caller-defined; chips beyond the pool (or the
    /// row width) collapse into a gray "+n" overflow chip.
    /// </summary>
    public void SetStatusChips(Side side, IReadOnlyList<StatusChip> chips)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        if (refs.StatusChips == null || refs.StatusText == null)
            return;

        refs.StatusText.text = "Status";

        RectTransform rowRect = refs.StatusText.transform.parent as RectTransform;
        float rowWidth = rowRect != null && rowRect.rect.width > 1f ? rowRect.rect.width : 240f;
        float rowHeight = rowRect != null && rowRect.rect.height > 1f ? rowRect.rect.height : 33f;
        // One shared integer height for every chip in the row; fractional or
        // per-chip heights made the sliced border rasterize unevenly.
        float chipHeight = Mathf.Round(rowHeight * 0.92f);
        float x = Mathf.Round(MeasureTextWidth(refs.StatusText)) + 9f;

        int count = chips != null ? chips.Count : 0;
        int next = 0;
        for (int i = 0; i < count && next < MaxStatusChips; i++)
        {
            string label = chips[i].Label;
            if (string.IsNullOrWhiteSpace(label))
                continue;

            StatusChipTone tone = chips[i].Tone;
            int remainingAfter = CountValidChips(chips, i + 1);
            bool overflow = next == MaxStatusChips - 1 && remainingAfter > 0;
            if (overflow)
            {
                label = $"+{remainingAfter + 1}";
                tone = StatusChipTone.Info;
            }

            float width = ApplyStatusChip(refs.StatusChips[next], refs.StatusChipTexts[next], label, tone, x, chipHeight);
            if (x + width > rowWidth && next > 0)
            {
                refs.StatusChips[next].gameObject.SetActive(false);
                break;
            }

            x += width + 4f;
            next++;
            if (overflow)
                break;
        }

        for (int i = next; i < MaxStatusChips; i++)
        {
            if (refs.StatusChips[i] != null)
                refs.StatusChips[i].gameObject.SetActive(false);
        }
    }

    private static int CountValidChips(IReadOnlyList<StatusChip> chips, int startIndex)
    {
        int valid = 0;
        for (int i = startIndex; i < chips.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(chips[i].Label))
                valid++;
        }
        return valid;
    }

    private float ApplyStatusChip(Image chip, Text label, string text, StatusChipTone tone, float x, float height)
    {
        if (chip == null || label == null)
            return 0f;

        label.text = text;
        label.color = ChipTextColor(tone);
        chip.color = ChipBorderColor(tone);

        // Integer width/position so every chip lands on the same pixel grid.
        float width = Mathf.Round(Mathf.Max(26f, MeasureTextWidth(label) + 14f));
        RectTransform rect = chip.rectTransform;
        rect.anchoredPosition = new Vector2(Mathf.Round(x), 0f);
        rect.sizeDelta = new Vector2(width, height);
        chip.gameObject.SetActive(true);
        return width;
    }

    private static Color ChipBorderColor(StatusChipTone tone)
    {
        switch (tone)
        {
            case StatusChipTone.Ready: return ChipReadyBorder;
            case StatusChipTone.Buff:  return ChipBuffBorder;
            case StatusChipTone.Harm:  return ChipHarmBorder;
            default:                   return ChipInfoBorder;
        }
    }

    private static Color ChipTextColor(StatusChipTone tone)
    {
        switch (tone)
        {
            case StatusChipTone.Ready: return ChipReadyText;
            case StatusChipTone.Buff:  return ChipBuffText;
            case StatusChipTone.Harm:  return ChipHarmText;
            default:                   return ChipInfoText;
        }
    }

    private void EnsureCombatantElementIcon(ref CombatantRefs refs)
    {
        if (refs.NameText == null)
            return;

        refs.ElementIcon = EnsureChildImage(refs.NameText.transform, "ElementChip");
        refs.ElementIcon.type = Image.Type.Simple;
        refs.ElementIcon.preserveAspect = true;
        refs.ElementIcon.raycastTarget = false;
        refs.ElementIcon.color = Color.white;
        refs.ElementIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows the active AlgoMon's element icon just after its name on the
    /// status card. Placement measures the rendered name width, so it hugs
    /// short names and clamps inside the band for long ones.
    /// </summary>
    public void SetCombatantElement(Side side, ElementType elementType)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        if (refs.ElementIcon == null)
            return;

        Sprite icon = ElementIconSprite(elementType);
        refs.ElementIcon.sprite = icon;
        refs.ElementIcon.gameObject.SetActive(icon != null);
        if (icon == null)
            return;

        RectTransform nameRect = refs.NameText.rectTransform;
        float bandWidth = Mathf.Max(0f, nameRect.rect.width);
        float bandHeight = nameRect.rect.height;
        float iconSize = Mathf.Clamp(bandHeight * 0.78f, 14f, 30f);
        float nameWidth = MeasureTextWidth(refs.NameText);
        float x = Mathf.Min(nameWidth + 7f, Mathf.Max(0f, bandWidth - iconSize));

        RectTransform iconRect = refs.ElementIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = new Vector2(x, 0f);
    }

    /// <summary>
    /// Unconstrained preferred width of the text at its maximum font size.
    /// Best-fit may render long strings smaller than measured; callers clamp,
    /// so an overestimate only pins the icon to the band's right edge.
    /// </summary>
    private static float MeasureTextWidth(Text text)
    {
        if (text == null || string.IsNullOrEmpty(text.text) || text.font == null)
            return 0f;

        TextGenerationSettings settings = text.GetGenerationSettings(Vector2.zero);
        settings.scaleFactor = 1f;
        settings.resizeTextForBestFit = false;
        settings.fontSize = text.resizeTextForBestFit ? text.resizeTextMaxSize : text.fontSize;
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text.cachedTextGeneratorForLayout.GetPreferredWidth(text.text, settings);
    }

    /// <summary>
    /// Builds the two runtime layers around the prefab-authored battery fill:
    /// a ghost afterimage just beneath it (shows the chunk of HP that was lost)
    /// and a low-battery warning wash just above it. Both are plain filled
    /// rectangles matching the fill's rect, so the prefab stays untouched.
    /// </summary>
    private void EnsureBatteryFeedbackLayers(ref CombatantRefs refs)
    {
        if (refs.BatteryFill == null)
            return;

        RectTransform fillRect = refs.BatteryFill.rectTransform;
        Transform barRoot = fillRect.parent;
        Image.FillMethod fillMethod = refs.BatteryFill.type == Image.Type.Filled
            ? refs.BatteryFill.fillMethod
            : Image.FillMethod.Horizontal;
        int fillOrigin = refs.BatteryFill.type == Image.Type.Filled
            ? refs.BatteryFill.fillOrigin
            : 0;

        refs.BatteryGhost = EnsureChildImage(barRoot, "GhostFill");
        ConfigureBatteryLayer(refs.BatteryGhost, fillRect, fillMethod, fillOrigin, BatteryGhostColor);

        refs.BatteryWash = EnsureChildImage(barRoot, "LowBatteryWash");
        ConfigureBatteryLayer(refs.BatteryWash, fillRect, fillMethod, fillOrigin, new Color(0f, 0f, 0f, 0f));

        // Draw order: ghost beneath the live fill, wash directly above it.
        // Guarded moves keep the order stable when Bind() runs again.
        Transform ghost = refs.BatteryGhost.transform;
        Transform wash = refs.BatteryWash.transform;
        if (ghost.GetSiblingIndex() > fillRect.GetSiblingIndex())
            ghost.SetSiblingIndex(fillRect.GetSiblingIndex());
        if (wash.GetSiblingIndex() < fillRect.GetSiblingIndex())
            wash.SetSiblingIndex(fillRect.GetSiblingIndex());
        else if (wash.GetSiblingIndex() > fillRect.GetSiblingIndex() + 1)
            wash.SetSiblingIndex(fillRect.GetSiblingIndex() + 1);

        // Quarter ticks: children of the fill (its Filled clipping does not
        // affect children), so they line up with the bar at any inset.
        for (int i = 1; i <= 3; i++)
        {
            Image tick = EnsureChildImage(fillRect.transform, $"Tick_{i}");
            RectTransform tickRect = tick.rectTransform;
            float fraction = i * 0.25f;
            tickRect.anchorMin = new Vector2(fraction, 0f);
            tickRect.anchorMax = new Vector2(fraction, 1f);
            tickRect.pivot = new Vector2(0.5f, 0.5f);
            tickRect.anchoredPosition = Vector2.zero;
            tickRect.sizeDelta = new Vector2(2f, -3f);
            tick.sprite = WhitePixelSprite();
            tick.color = BatteryTickColor;
            tick.raycastTarget = false;
        }
    }

    private static void ConfigureBatteryLayer(
        Image layer,
        RectTransform fillRect,
        Image.FillMethod fillMethod,
        int fillOrigin,
        Color color)
    {
        if (layer == null || fillRect == null)
            return;

        RectTransform rect = layer.rectTransform;
        rect.anchorMin = fillRect.anchorMin;
        rect.anchorMax = fillRect.anchorMax;
        rect.pivot = fillRect.pivot;
        rect.anchoredPosition = fillRect.anchoredPosition;
        rect.sizeDelta = fillRect.sizeDelta;
        rect.localScale = fillRect.localScale;

        layer.sprite = WhitePixelSprite();
        layer.type = Image.Type.Filled;
        layer.fillMethod = fillMethod;
        layer.fillOrigin = fillOrigin;
        layer.fillAmount = 0f;
        layer.color = color;
        layer.raycastTarget = false;
    }

    /// <summary>
    /// 1x1 white sprite for the runtime battery layers. Image ignores
    /// Filled-mode clipping with a null sprite, so a real sprite is required.
    /// </summary>
    private static Sprite WhitePixelSprite()
    {
        if (whitePixelSprite != null)
            return whitePixelSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whitePixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        whitePixelSprite.name = "HudWhitePixel";
        return whitePixelSprite;
    }

    private static Sprite EffectivenessTriangleSprite(bool up)
    {
        Sprite cached = up ? triangleUpSprite : triangleDownSprite;
        if (cached != null)
            return cached;

        const int size = 24;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float t = up
                ? 1f - (float)y / (size - 1)
                : (float)y / (size - 1);
            float halfWidth = Mathf.Lerp(1f, center, t);
            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x - center) <= halfWidth;
                texture.SetPixel(x, y, inside ? Color.white : clear);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = up ? "EffectivenessTriangleUp" : "EffectivenessTriangleDown";
        if (up)
            triangleUpSprite = sprite;
        else
            triangleDownSprite = sprite;
        return sprite;
    }

    private void ConfigureCombatantTextHierarchy(CombatantRefs refs)
    {
        ConfigureCombatantText(refs.NameText, 23, 18, 24, TextAnchor.MiddleLeft, Color.white);
        ConfigureCombatantText(refs.LevelText, 16, 12, 17, TextAnchor.MiddleRight, new Color(0.84f, 0.90f, 0.98f, 0.92f));
        ConfigureCombatantText(refs.BatteryValueText, 18, 14, 19, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 1f));
        ConfigureCombatantText(refs.CPValueText, 17, 13, 18, TextAnchor.MiddleLeft, new Color(0.90f, 1f, 1f, 1f));
        ConfigureCombatantText(refs.StatusText, 14, 11, 15, TextAnchor.MiddleLeft, new Color(0.78f, 0.86f, 0.94f, 0.82f));
    }

    private void ConfigureCombatantText(
        Text text,
        int fontSize,
        int minSize,
        int maxSize,
        TextAnchor alignment,
        Color color)
    {
        if (text == null)
            return;

        text.font = ReadableHudFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.color = color;
        EnsureShadow(text.gameObject, new Color(0f, 0f, 0f, 0.78f), new Vector2(1.2f, -1.2f));
    }

    private static Image[] FindCPDots(Transform cpRow)
    {
        var dots = new System.Collections.Generic.List<Image>(MaxCP);
        for (int i = 0; i < MaxCP; i++)
        {
            Transform dot = FindChildRecursive(cpRow, $"CP_{i + 1:00}");
            Image image = dot != null ? dot.GetComponent<Image>() : null;
            if (image != null)
                dots.Add(image);
        }

        return dots.ToArray();
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform match = FindChildRecursive(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }

    private void UnhookButtons()
    {
        for (int i = 0; i < MaxSkillSlots; i++)
        {
            if (skillButtons[i] != null)
                skillButtons[i].onClick.RemoveAllListeners();
        }
        if (rechargeButton != null) rechargeButton.onClick.RemoveAllListeners();
        if (switchButton   != null) switchButton.onClick.RemoveAllListeners();
        if (fleeButton     != null) fleeButton.onClick.RemoveAllListeners();
        if (postBattleContinueButton != null)
            postBattleContinueButton.onClick.RemoveListener(HandlePostBattleContinueClicked);
    }

    private static bool IndexInRange(int index) => index >= 0 && index < MaxSkillSlots;

    private T Find<T>(string path) where T : Component
    {
        Transform t = FindTransform(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    private Transform FindTransform(string path) => transform.Find(path);

    private Text FindText(string path) => Find<Text>(path);

    private Transform FindSkillDetailPanelTransform()
    {
        Transform panel = FindTransform(SkillDetailPanelPath);
        return panel != null ? panel : FindTransform(LegacySkillDetailPanelPath);
    }

    private Image FindSkillDetailPanelImage()
    {
        Image image = Find<Image>(SkillDetailPanelPath);
        return image != null ? image : Find<Image>(LegacySkillDetailPanelPath);
    }

    private Text FindSkillDetailText(string childName)
    {
        Text text = FindText($"{SkillDetailPanelPath}/{childName}");
        return text != null ? text : FindText($"{LegacySkillDetailPanelPath}/{childName}");
    }

    private void WriteSkillDetail(string title, string body)
    {
        if (switchDetailRoot != null)
            switchDetailRoot.SetActive(false);
        if (skillDetailTitle != null) skillDetailTitle.gameObject.SetActive(true);
        if (skillDetailBody  != null) skillDetailBody.gameObject.SetActive(true);
        if (skillDetailTitle != null) skillDetailTitle.text = title ?? string.Empty;
        if (skillDetailBody  != null) skillDetailBody.text  = body  ?? string.Empty;
    }

    private void ShowSkillDetail(string title, string body)
    {
        WriteSkillDetail(title, body);
        BringSkillDetailPanelForward();
    }

    private void ShowSkillSlotDetail(int index)
    {
        if (IndexInRange(index) && switchSlotDetails[index].HasData)
        {
            ShowSwitchDetail(index);
            return;
        }

        ShowSkillDetail(skillHoverTitles[index] ?? string.Empty, skillHoverBodies[index] ?? string.Empty);
    }

    private void ShowSwitchDetail(int index)
    {
        if (!IndexInRange(index) || !switchSlotDetails[index].HasData)
            return;

        EnsureSwitchDetailPresentation();
        if (switchDetailRoot == null)
        {
            ShowSkillDetail(skillHoverTitles[index] ?? string.Empty, skillHoverBodies[index] ?? string.Empty);
            return;
        }

        SwitchSlotDetail detail = switchSlotDetails[index];
        int safeMaxBattery = Mathf.Max(1, detail.MaxBattery);
        int safeBattery = Mathf.Clamp(detail.CurrentBattery, 0, safeMaxBattery);
        int safeMaxCP = Mathf.Max(1, detail.MaxCP);
        int safeCP = Mathf.Clamp(detail.CurrentCP, 0, safeMaxCP);
        string state = string.IsNullOrWhiteSpace(detail.StateText) ? "READY" : detail.StateText.Trim();
        string status = string.IsNullOrWhiteSpace(detail.StatusSummary) ? "Ready" : detail.StatusSummary.Trim();

        if (skillDetailTitle != null) skillDetailTitle.gameObject.SetActive(false);
        if (skillDetailBody  != null) skillDetailBody.gameObject.SetActive(false);
        switchDetailRoot.SetActive(true);

        if (switchDetailNameText != null)
            switchDetailNameText.text = Ellipsize(detail.DisplayName, 22);
        if (switchDetailMetaText != null)
            switchDetailMetaText.text = $"Lv {Mathf.Max(1, detail.Level)} | {state}";
        if (switchDetailElementIcon != null)
        {
            Sprite icon = ElementIconSprite(detail.ElementType);
            switchDetailElementIcon.sprite = icon;
            switchDetailElementIcon.gameObject.SetActive(icon != null);
        }
        if (switchDetailBatteryValueText != null)
            switchDetailBatteryValueText.text = $"BATTERY {safeBattery}/{safeMaxBattery}";
        if (switchDetailBatteryFill != null)
            SetSwitchDetailBarFill(switchDetailBatteryFill, safeBattery / (float)safeMaxBattery);
        if (switchDetailCPValueText != null)
            switchDetailCPValueText.text = $"CP {safeCP}/{safeMaxCP}";
        if (switchDetailCPFill != null)
            SetSwitchDetailBarFill(switchDetailCPFill, safeCP / (float)safeMaxCP);
        if (switchDetailStatusText != null)
            switchDetailStatusText.text = $"Status: {status}";

        BringSkillDetailPanelForward();
    }

    private void ConfigureSkillDetailPanel()
    {
        if (skillDetailPanel == null)
            skillDetailPanel = FindSkillDetailPanelTransform();
        if (skillDetailPanel == null)
            return;

        skillDetailPanel.gameObject.SetActive(true);
        EnsureSwitchDetailPresentation();

        Graphic[] graphics = skillDetailPanel.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        CanvasGroup group = skillDetailPanel.GetComponent<CanvasGroup>();
        if (group == null)
            group = skillDetailPanel.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void EnsureSwitchDetailPresentation()
    {
        if (skillDetailPanel == null)
            skillDetailPanel = FindSkillDetailPanelTransform();
        if (skillDetailPanel == null)
            return;

        Transform rootTransform = skillDetailPanel.Find("SwitchDetailRoot");
        if (rootTransform == null)
        {
            GameObject rootObject = new GameObject("SwitchDetailRoot", typeof(RectTransform), typeof(CanvasRenderer));
            rootObject.layer = skillDetailPanel.gameObject.layer;
            rootObject.transform.SetParent(skillDetailPanel, false);
            rootTransform = rootObject.transform;
        }
        else
        {
            rootTransform.gameObject.layer = skillDetailPanel.gameObject.layer;
        }

        switchDetailRoot = rootTransform.gameObject;
        SetStretchRect(rootTransform as RectTransform, Vector2.zero, Vector2.one);
        switchDetailRoot.SetActive(false);

        Image iconFrame = EnsureChildImage(rootTransform, "ElementIconFrame");
        SetStretchRect(iconFrame.rectTransform, new Vector2(0.045f, 0.725f), new Vector2(0.155f, 0.930f));
        ConfigureChromeChip(iconFrame, 3f);

        switchDetailElementIcon = EnsureChildImage(iconFrame.transform, "ElementIcon");
        SetStretchRect(switchDetailElementIcon.rectTransform, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f));
        switchDetailElementIcon.type = Image.Type.Simple;
        switchDetailElementIcon.preserveAspect = true;
        switchDetailElementIcon.raycastTarget = false;

        switchDetailNameText = EnsureChildText(rootTransform, "NameText", 20, TextAnchor.MiddleLeft);
        SetStretchRect(switchDetailNameText.rectTransform, new Vector2(0.185f, 0.805f), new Vector2(0.955f, 0.940f));
        ConfigureSwitchDetailText(switchDetailNameText, 20, 14, 20, TextAnchor.MiddleLeft, Color.white);

        switchDetailMetaText = EnsureChildText(rootTransform, "MetaText", 14, TextAnchor.MiddleLeft);
        SetStretchRect(switchDetailMetaText.rectTransform, new Vector2(0.185f, 0.680f), new Vector2(0.955f, 0.800f));
        ConfigureSwitchDetailText(switchDetailMetaText, 14, 10, 14, TextAnchor.MiddleLeft, new Color(0.90f, 1f, 1f, 0.92f));

        switchDetailBatteryValueText = EnsureChildText(rootTransform, "BatteryValueText", 14, TextAnchor.MiddleLeft);
        SetStretchRect(switchDetailBatteryValueText.rectTransform, new Vector2(0.055f, 0.505f), new Vector2(0.955f, 0.605f));
        ConfigureSwitchDetailText(switchDetailBatteryValueText, 14, 10, 14, TextAnchor.MiddleLeft, new Color(1f, 0.74f, 0.34f, 1f));
        switchDetailBatteryFill = EnsureSwitchDetailBar(rootTransform, "BatteryBar", new Vector2(0.055f, 0.405f), new Vector2(0.955f, 0.492f), new Color(1f, 0.31f, 0.25f, 0.95f));

        switchDetailCPValueText = EnsureChildText(rootTransform, "CPValueText", 14, TextAnchor.MiddleLeft);
        SetStretchRect(switchDetailCPValueText.rectTransform, new Vector2(0.055f, 0.275f), new Vector2(0.955f, 0.375f));
        ConfigureSwitchDetailText(switchDetailCPValueText, 14, 10, 14, TextAnchor.MiddleLeft, new Color(0.52f, 0.95f, 1f, 1f));
        switchDetailCPFill = EnsureSwitchDetailBar(rootTransform, "CPBar", new Vector2(0.055f, 0.175f), new Vector2(0.955f, 0.262f), new Color(0.42f, 0.92f, 0.96f, 0.95f));

        switchDetailStatusText = EnsureChildText(rootTransform, "StatusText", 13, TextAnchor.MiddleLeft);
        SetStretchRect(switchDetailStatusText.rectTransform, new Vector2(0.055f, 0.035f), new Vector2(0.955f, 0.145f));
        ConfigureSwitchDetailText(switchDetailStatusText, 13, 9, 13, TextAnchor.MiddleLeft, new Color(0.86f, 0.93f, 1f, 0.90f));
    }

    private Image EnsureSwitchDetailBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
    {
        Image track = EnsureChildImage(parent, name);
        SetStretchRect(track.rectTransform, anchorMin, anchorMax);
        ConfigureChromeChip(track, 3f);
        track.color = new Color(0.10f, 0.16f, 0.18f, 0.95f);

        Image fill = EnsureChildImage(track.transform, "Fill");
        SetSwitchDetailBarFill(fill, 1f);
        fill.sprite = null;
        fill.type = Image.Type.Simple;
        fill.color = fillColor;
        fill.raycastTarget = false;
        fill.transform.SetAsFirstSibling();
        return fill;
    }

    private static void SetSwitchDetailBarFill(Image fill, float normalized)
    {
        if (fill == null)
            return;

        RectTransform rect = fill.rectTransform;
        float right = Mathf.Lerp(0.025f, 0.975f, Mathf.Clamp01(normalized));
        SetStretchRect(rect, new Vector2(0.025f, 0.20f), new Vector2(right, 0.800f));
        fill.enabled = right > 0.0251f;
    }

    private void ConfigureSwitchDetailText(
        Text text,
        int fontSize,
        int minSize,
        int maxSize,
        TextAnchor alignment,
        Color color)
    {
        if (text == null)
            return;

        text.font = ReadableHudFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.supportRichText = true;
        text.color = color;
        text.raycastTarget = false;
        EnsureShadow(text.gameObject, new Color(0f, 0f, 0f, 0.85f), new Vector2(1.1f, -1.1f));
    }

    private void HideSkillDetailPanel()
    {
        if (skillDetailPanel == null)
            skillDetailPanel = FindSkillDetailPanelTransform();
        if (skillDetailPanel == null)
            return;

        // Restore the resting content (battle log / validation text) before hiding
        // so the panel never re-appears showing stale hover text from a skill slot
        // or combatant card. Documented design: hover previews are transient.
        WriteSkillDetail(restingDetailTitle, restingDetailBody);

        CanvasGroup group = skillDetailPanel.GetComponent<CanvasGroup>();
        if (group == null)
            group = skillDetailPanel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void BringSkillDetailPanelForward()
    {
        if (skillDetailPanel == null)
            skillDetailPanel = FindSkillDetailPanelTransform();
        if (skillDetailPanel == null)
            return;

        skillDetailPanel.gameObject.SetActive(true);
        skillDetailPanel.SetAsLastSibling();

        CanvasGroup group = skillDetailPanel.GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 1f;
    }

    private void ConfigureSkillDetailText()
    {
        if (skillDetailTitle != null)
        {
            skillDetailTitle.font = ReadableHudFont();
            skillDetailTitle.fontStyle = FontStyle.Bold;
            skillDetailTitle.fontSize = 19;
            skillDetailTitle.alignment = TextAnchor.MiddleLeft;
            skillDetailTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            skillDetailTitle.verticalOverflow = VerticalWrapMode.Truncate;
            skillDetailTitle.resizeTextForBestFit = true;
            skillDetailTitle.resizeTextMinSize = 14;
            skillDetailTitle.resizeTextMaxSize = 19;
            skillDetailTitle.supportRichText = true;
            skillDetailTitle.color = Color.white;
            EnsureShadow(skillDetailTitle.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.4f, -1.4f));
        }

        if (skillDetailBody != null)
        {
            skillDetailBody.font = ReadableHudFont();
            skillDetailBody.fontStyle = FontStyle.Bold;
            skillDetailBody.fontSize = 15;
            skillDetailBody.alignment = TextAnchor.UpperLeft;
            skillDetailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            skillDetailBody.verticalOverflow = VerticalWrapMode.Truncate;
            skillDetailBody.resizeTextForBestFit = true;
            skillDetailBody.resizeTextMinSize = 11;
            skillDetailBody.resizeTextMaxSize = 15;
            skillDetailBody.lineSpacing = 1f;
            skillDetailBody.supportRichText = true;
            skillDetailBody.color = new Color(0.94f, 1f, 1f, 1f);
            EnsureShadow(skillDetailBody.gameObject, new Color(0f, 0f, 0f, 0.88f), new Vector2(1.3f, -1.3f));
        }
    }

    private static string BuildSkillDetailFallback(SkillData skill)
    {
        if (skill == null)
            return string.Empty;

        var meta = new StringBuilder();
        meta.Append(InstructionDetailLabel(skill.instructionType));
        meta.Append(" | ");
        meta.Append(ElementDetailLabel(skill.elementType));
        meta.Append(" | CP ");
        meta.Append(Mathf.Max(0, skill.cpCost));

        if (skill.basePower > 0)
        {
            meta.Append(" | BP ");
            meta.Append(skill.basePower);
        }

        if (skill.canCounter)
            meta.Append(" | Counter-ready");

        return SkillDetailTextFormatter.BuildBody(
            meta.ToString(),
            SkillDetailTextFormatter.BuildCounterSummary(skill),
            SkillDetailTextFormatter.BuildReadableDescription(skill));
    }

    private static string BuildSwitchDetailText(
        ElementType elementType,
        int level,
        int currentBattery,
        int maxBattery,
        int currentCP,
        int maxCP,
        string stateText,
        string statusSummary,
        SubroutineData subroutine = null)
    {
        int safeMaxBattery = Mathf.Max(1, maxBattery);
        int safeMaxCP = Mathf.Max(1, maxCP);
        int safeBattery = Mathf.Clamp(currentBattery, 0, safeMaxBattery);
        int safeCP = Mathf.Clamp(currentCP, 0, safeMaxCP);
        string state = string.IsNullOrWhiteSpace(stateText) ? "READY" : stateText.Trim();
        string status = string.IsNullOrWhiteSpace(statusSummary) ? "Ready" : statusSummary.Trim();

        string body = SkillDetailTextFormatter.BuildBody(
            $"{ElementDetailLabel(elementType)} | Lv {Mathf.Max(1, level)} | {state}",
            $"BATTERY <b>{safeBattery}/{safeMaxBattery}</b>\n{BuildCompactResourceBar(safeBattery, safeMaxBattery, 12)}",
            $"CP <b>{safeCP}/{safeMaxCP}</b>\n{BuildCompactResourceBar(safeCP, safeMaxCP, 10)}",
            $"Status: <b>{status}</b>");

        if (subroutine != null && !string.IsNullOrWhiteSpace(subroutine.subroutineName))
        {
            string desc = string.IsNullOrWhiteSpace(subroutine.description)
                ? "Hardwired passive ability."
                : subroutine.description.Trim();
            body += $"\n\n<b>SUBROUTINE</b>  {subroutine.subroutineName.Trim()} · {subroutine.TriggerLabel}\n{desc}";
        }

        return body;
    }

    private static string BuildCompactResourceBar(int current, int max, int segments)
    {
        int safeSegments = Mathf.Max(1, segments);
        int filled = max <= 0
            ? 0
            : Mathf.Clamp(Mathf.RoundToInt((current / (float)max) * safeSegments), 0, safeSegments);
        return "[" + new string('#', filled) + new string('-', safeSegments - filled) + "]";
    }

    private static string InstructionDetailLabel(InstructionType instructionType)
    {
        switch (instructionType)
        {
            case InstructionType.Attack:  return "Attack";
            case InstructionType.Defense: return "Defense";
            case InstructionType.Status:  return "Status";
            default:                      return "Instruction";
        }
    }

    private static string ElementDetailLabel(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Electric: return "Electric";
            case ElementType.Water:    return "Water";
            case ElementType.Fire:     return "Fire";
            case ElementType.Ground:   return "Ground";
            case ElementType.Grass:    return "Grass";
            case ElementType.Ice:      return "Ice";
            case ElementType.Normal:   return "Normal";
            default:                   return "Element";
        }
    }

    private static void SetTag(GameObject tagObject, Text text, bool visible, string value)
    {
        if (text != null) text.text = value;
        if (tagObject == null)
            return;

        tagObject.SetActive(true);
        CanvasGroup group = tagObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = tagObject.AddComponent<CanvasGroup>();

        group.alpha = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private Text FindTextDeep(string root, string subPath)
    {
        Transform rootT = transform.Find(root);
        if (rootT == null) return null;
        Transform t = rootT.Find(subPath);
        return t != null ? t.GetComponent<Text>() : null;
    }
}
