/*
Script Audit:
- Purpose: Provides the runtime API for all TheArena HUD display and button input.
- Attached GameObject: BattleHud prefab root or TheArena Canvas_Arena object.
- Main responsibilities: Bind UI references, raise skill/action click events, update names/levels/HP/CP/status, render skill/switch slots, show hover details, animate CP/HP and the battle announcer.
- Important variables: SkillSlotClicked, ActionClicked, player, enemy, skillButtons, action buttons, skillHoverTitles, skillHoverBodies, announcerTitleText, roundSandclockImage.
- Inputs: Button clicks, hover events, BattleManager state updates, SkillData, and UI sprites/fonts.
- Outputs or effects: Updates visible HUD text/images and sends player choices back to BattleManager.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: In battle, verify all four skill slots, Recharge/Bag/Switch/Flee buttons, HP/CP bars, hover details, and announcer updates.
*/
using System;
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
    public enum ActionButton  { Recharge, Bag, Switch, Flee }

    public event Action<int>          SkillSlotClicked;
    public event Action<ActionButton> ActionClicked;
    public event Action               PostBattleContinueClicked;

    public bool IsBound { get; private set; }

    private const int MaxSkillSlots = 4;
    private const int MaxCP         = 10;
    private const string PlayerTurnStateText = "Player turn";
    private const string AnnouncerPanelResourcePath = "UI/BattleAnnouncer_GreenPanel";
    private const string AnnouncerFontResourcePath = "Fonts/NicoBold-Regular";
    private const string SkillButtonFrameResourcePath = "UI/SkillFrame/scifi_inventory01_box_back";
    private const string SkillInsetFrameResourcePath = "UI/SkillFrame/scifi_inventory01_box";
    private const string SkillTagFrameResourcePath = "UI/SkillFrame/scifi_inventory02_box_select01";
    private const string SkillPanelBackdropResourcePath = "UI/SkillFrame/inventory_example_02_four_rows_soft";
    private const string ElementIconResourcePrefix = "UI/Elements/Element_";

    // Default text shown in the Skill Details panel when no button is hovered.
    private const string DefaultSkillDetailTitle = "Skill Details";
    private const string DefaultSkillDetailBody  = "Ready.";

    // CP dot palette used by both prefab defaults and live HUD updates.
    private static readonly Color32 CPDotActive   = new Color32(120, 235, 244, 255);
    private static readonly Color32 CPDotInactive = new Color32(120, 235, 244,   0);
    private static readonly Color32 PlayerBatteryFillColor = new Color32( 70, 221, 233, 255);
    private static readonly Color32 EnemyBatteryFillColor  = new Color32(242, 131,  92, 255);
    private static readonly Vector2 BatteryFillFallbackAnchorMin = new Vector2(0.30f, 0.02f);
    private static readonly Vector2 BatteryFillFallbackAnchorMax = new Vector2(0.82f, 0.98f);
    private static readonly Vector2 BatteryFillInsetMin = new Vector2(0f, 0f);
    private static readonly Vector2 BatteryFillInsetMax = new Vector2(1f, 1f);

    [Header("Resource Animation")]
    [SerializeField, Min(0f)] private float batteryLerpSpeed = 8f;
    [SerializeField, Min(0f)] private float cpLerpSpeed = 12f;

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
    [SerializeField] private bool autoCreateAnnouncer = true;
    [SerializeField, Min(0f)] private float announcerPulseSeconds = 0.18f;

    [Header("Skill Slot Skin")]
    [SerializeField] private Sprite skillButtonFrameSprite;
    [SerializeField] private Sprite skillInsetFrameSprite;
    [SerializeField] private Sprite skillTagFrameSprite;
    [SerializeField] private Sprite skillPanelBackdropSprite;

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
        public Color32 BatteryFillColor;
        public RectTransform BatteryFrameRect;
        public Image[] CPDots;
        public Text    CPValueText;
        public Text    StatusText;
    }
    private CombatantRefs player;
    private CombatantRefs enemy;

    private struct CombatantDisplayState
    {
        public bool BatteryInitialized;
        public int TargetBattery;
        public int TargetBatteryMax;
        public float DisplayBattery;

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
    private readonly Image[]  skillElementBadges     = new Image[MaxSkillSlots];
    private readonly Image[]  skillElementIconImages = new Image[MaxSkillSlots];
    private readonly Text[]   skillElementTexts      = new Text [MaxSkillSlots];
    private readonly Sprite[] elementIconSprites      = new Sprite[7];
    private readonly GameObject[] skillCPTagObjects      = new GameObject[MaxSkillSlots];
    private readonly GameObject[] skillPowerTagObjects   = new GameObject[MaxSkillSlots];
    private readonly GameObject[] skillCounterTagObjects = new GameObject[MaxSkillSlots];

    // --- Action button refs ---
    private Button rechargeButton;
    private Button bagButton;
    private Button switchButton;
    private Button fleeButton;

    // --- Hover content for the Skill Details panel (per skill / action slot) ---
    // Lambda listeners aren't serialized into the HUD prefab, so the hover wiring
    // is rebuilt every Bind(); these arrays back the text it displays on enter.
    private readonly string[] skillHoverTitles  = new string[MaxSkillSlots];
    private readonly string[] skillHoverBodies  = new string[MaxSkillSlots];
    private readonly string[] actionHoverTitles = new string[4];
    private readonly string[] actionHoverBodies = new string[4];

    // --- Skill details panel ---
    private Text skillDetailTitle;
    private Text skillDetailBody;
    private string restingDetailTitle = DefaultSkillDetailTitle;
    private string restingDetailBody  = DefaultSkillDetailBody;

    // Runtime-created overlay shown after a node is cleared. BattleManager
    // controls the content and waits for the Continue click before scene travel.
    private GameObject postBattlePanel;
    private Text postBattleTitleText;
    private Text postBattleBodyText;
    private Button postBattleContinueButton;

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
        ApplyRoundSandclockState();
        EnsureBattleAnnouncer();
        EnsurePostBattlePanel();
        HidePostBattlePanel();

        player = BindCombatant("SafeArea/CombatLayer/PlayerCombatantPanel");
        enemy  = BindCombatant("SafeArea/CombatLayer/EnemyCombatantPanel");
        playerDisplay = default;
        enemyDisplay = default;
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
                skillButtons[i].onClick.AddListener(() => SkillSlotClicked?.Invoke(slot));
        }

        rechargeButton = Find<Button>("SafeArea/CommandPanel/ActionPanel/ActionGrid/RechargeButton");
        bagButton      = Find<Button>("SafeArea/CommandPanel/ActionPanel/ActionGrid/BagButton");
        switchButton   = Find<Button>("SafeArea/CommandPanel/ActionPanel/ActionGrid/SwitchButton");
        fleeButton     = Find<Button>("SafeArea/CommandPanel/ActionPanel/ActionGrid/FleeButton");

        if (rechargeButton != null) rechargeButton.onClick.AddListener(() => ActionClicked?.Invoke(ActionButton.Recharge));
        if (bagButton      != null) bagButton.onClick.AddListener     (() => ActionClicked?.Invoke(ActionButton.Bag));
        if (switchButton   != null) switchButton.onClick.AddListener  (() => ActionClicked?.Invoke(ActionButton.Switch));
        if (fleeButton     != null) fleeButton.onClick.AddListener    (() => ActionClicked?.Invoke(ActionButton.Flee));

        EnsureSkillPanelPresentation();

        skillDetailTitle = FindText("SafeArea/CommandPanel/ActionPanel/SkillDetailPanel/TitleText");
        skillDetailBody  = FindText("SafeArea/CommandPanel/ActionPanel/SkillDetailPanel/BodyText");
        WriteSkillDetail(restingDetailTitle, restingDetailBody);

        WireHoverPreviews();

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
                () => skillHoverBodies[slot] ?? string.Empty);
        }

        ApplyActionHoverDefaults();

        WireHoverInternal(rechargeButton, () => actionHoverTitles[0], () => actionHoverBodies[0]);
        WireHoverInternal(bagButton,      () => actionHoverTitles[1], () => actionHoverBodies[1]);
        WireHoverInternal(switchButton,   () => actionHoverTitles[2], () => actionHoverBodies[2]);
        WireHoverInternal(fleeButton,     () => actionHoverTitles[3], () => actionHoverBodies[3]);
    }

    private void ApplyActionHoverDefaults()
    {
        // Mirrors the placeholder copy baked into BattleHud.prefab.
        // BattleManager can override at any time via SetActionHover.
        if (string.IsNullOrEmpty(actionHoverTitles[0])) { actionHoverTitles[0] = "Recharge"; actionHoverBodies[0] = "+5 CP\nSpend the turn to restore CP."; }
        if (string.IsNullOrEmpty(actionHoverTitles[1])) { actionHoverTitles[1] = "Bag";      actionHoverBodies[1] = "Open battle items."; }
        if (string.IsNullOrEmpty(actionHoverTitles[2])) { actionHoverTitles[2] = "Switch";   actionHoverBodies[2] = "Change the active AlgoMon."; }
        if (string.IsNullOrEmpty(actionHoverTitles[3])) { actionHoverTitles[3] = "Flee";     actionHoverBodies[3] = "Attempt to escape from battle."; }
    }

    private void WireHoverInternal(Button button, Func<string> titleGetter, Func<string> bodyGetter)
    {
        if (button == null) return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        trigger.triggers.Clear();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => WriteSkillDetail(titleGetter(), bodyGetter()));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => WriteSkillDetail(restingDetailTitle, restingDetailBody));
        trigger.triggers.Add(exit);
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
        EnsureBattleAnnouncer();
        if (announcerTitleText != null)
            announcerTitleText.text = string.IsNullOrWhiteSpace(title) ? "BATTLE" : title.Trim().ToUpperInvariant();
        if (announcerBodyText != null)
            announcerBodyText.text = body ?? string.Empty;

        announcerPulseTimer = announcerPulseSeconds;
        ApplyAnnouncerFrameColor(1f);
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

    public void SetBattery(Side side, int current, int max)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        ref CombatantDisplayState display = ref DisplayFor(side);
        int safeMax = Mathf.Max(0, max);
        int safeCurrent = safeMax <= 0 ? 0 : Mathf.Clamp(current, 0, safeMax);

        if (refs.BatteryValueText != null)
            refs.BatteryValueText.text = $"{safeCurrent}/{safeMax}";

        if (!display.BatteryInitialized)
        {
            display.DisplayBattery = safeCurrent;
            display.BatteryInitialized = true;
        }

        display.TargetBattery = safeCurrent;
        display.TargetBatteryMax = safeMax;
        display.DisplayBattery = Mathf.Clamp(display.DisplayBattery, 0f, safeMax);
        ApplyBatteryVisual(refs, display.DisplayBattery, safeMax);
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
        ApplyCPVisual(refs, display.DisplayCP, safeMax);
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
        if (skillNameTexts[index]  != null) skillNameTexts[index].text       = skill.skillName;

        SetTag(skillCPTagObjects[index], skillCPTexts[index], true, $"CP {skill.cpCost}");

        bool showsPower = skill.basePower > 0;
        SetTag(skillPowerTagObjects[index], skillPowerTexts[index], showsPower,
            showsPower ? $"BP {skill.basePower}" : string.Empty);

        bool showsCounter = skill.canCounter && skill.instructionType == InstructionType.Defense;
        SetTag(skillCounterTagObjects[index], skillCounterTexts[index], showsCounter,
            showsCounter ? "Counter" : string.Empty);
        SetSkillSlotBadges(index, skill);

        // Hover preview follows the skill currently in the slot.
        skillHoverTitles[index] = skill.skillName;
        skillHoverBodies[index] = string.IsNullOrEmpty(skill.description) ? string.Empty : skill.description;
    }

    public void SetSwitchSlot(
        int index,
        string displayName,
        string levelText,
        string batteryText,
        string stateText,
        bool available)
    {
        if (!IndexInRange(index)) return;

        if (skillButtons[index] != null)
            skillButtons[index].interactable = available;
        if (skillNameTexts[index] != null)
            skillNameTexts[index].text = string.IsNullOrWhiteSpace(displayName) ? "-" : displayName;

        SetTag(skillCPTagObjects[index], skillCPTexts[index], !string.IsNullOrWhiteSpace(levelText), levelText);
        SetTag(skillPowerTagObjects[index], skillPowerTexts[index], !string.IsNullOrWhiteSpace(batteryText), batteryText);
        SetTag(skillCounterTagObjects[index], skillCounterTexts[index], !string.IsNullOrWhiteSpace(stateText), stateText);
        SetSkillSlotBadges(index, null);

        skillHoverTitles[index] = string.IsNullOrWhiteSpace(displayName) ? "Switch" : displayName;
        skillHoverBodies[index] = $"{levelText}\n{batteryText}\n{stateText}".Trim();
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
            case ActionButton.Bag:      return bagButton;
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
            case ActionButton.Bag:      return 1;
            case ActionButton.Switch:   return 2;
            case ActionButton.Flee:     return 3;
            default:                    return -1;
        }
    }

    public void ClearSkillSlot(int index)
    {
        if (!IndexInRange(index)) return;
        if (skillButtons[index]      != null) skillButtons[index].interactable = false;
        if (skillNameTexts[index]    != null) skillNameTexts[index].text       = "-";
        SetTag(skillCPTagObjects[index], skillCPTexts[index], false, string.Empty);
        SetTag(skillPowerTagObjects[index], skillPowerTexts[index], false, string.Empty);
        SetTag(skillCounterTagObjects[index], skillCounterTexts[index], false, string.Empty);
        SetSkillSlotBadges(index, null);
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
        UpdateResourceDisplay(player, ref playerDisplay);
        UpdateResourceDisplay(enemy, ref enemyDisplay);
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
        if (announcerTitleText != null && announcerBodyText != null)
            return;

        Transform root = FindTransform("SafeArea/BattleAnnouncer");
        if (root == null && autoCreateAnnouncer)
            root = CreateBattleAnnouncer();
        if (root == null)
            return;

        if (announcerFrame == null)
            announcerFrame = root.GetComponent<Image>();
        ConfigureAnnouncerFrame();
        if (announcerTitleText == null)
            announcerTitleText = FindText("SafeArea/BattleAnnouncer/TitleText");
        if (announcerBodyText == null)
            announcerBodyText = FindText("SafeArea/BattleAnnouncer/BodyText");

        ApplyAnnouncerFrameColor(0f);
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
        ConfigureFramedImage(frame, AnnouncerPanelSprite(), new Color(0.018f, 0.045f, 0.062f, 0.96f));

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
        ConfigureFramedImage(buttonImage, SkillTagFrameSprite(), new Color(0.08f, 0.50f, 0.34f, 0.92f));
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
        root.anchorMin = new Vector2(0.30f, 0.855f);
        root.anchorMax = new Vector2(0.70f, 0.985f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

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

    private void EnsureSkillPanelPresentation()
    {
        Image panelImage = Find<Image>("SafeArea/CommandPanel/SkillPanel");
        if (panelImage == null)
            return;

        Sprite backdrop = SkillPanelBackdropSprite();
        if (backdrop == null)
            return;

        bool needsPanelApply = !skillPanelPresentationApplied || panelImage.sprite != backdrop || panelImage.color != Color.white;
        if (!needsPanelApply && skillPanelSlotLayoutApplied)
            return;

        if (needsPanelApply)
        {
            panelImage.sprite = backdrop;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = false;
            panelImage.color = Color.white;
            panelImage.raycastTarget = false;
            skillPanelSlotLayoutApplied = false;
        }

        Outline outline = panelImage.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

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
            new Vector2(0.018f, 0.748f),
            new Vector2(0.018f, 0.524f),
            new Vector2(0.018f, 0.299f),
            new Vector2(0.018f, 0.048f),
        };
        Vector2[] rowMaxes =
        {
            new Vector2(0.982f, 0.952f),
            new Vector2(0.982f, 0.728f),
            new Vector2(0.982f, 0.503f),
            new Vector2(0.982f, 0.272f),
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
                buttonImage.sprite = null;
                buttonImage.color = new Color(0f, 0f, 0f, 0f);
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
            buttonOutline.enabled = false;

        Transform typeStrip = root.Find("TypeStrip");
        if (typeStrip != null)
            typeStrip.gameObject.SetActive(false);

        RectTransform nameRect = skillNameTexts[index] != null
            ? skillNameTexts[index].GetComponent<RectTransform>()
            : null;
        if (nameRect != null)
        {
            SetStretchRect(nameRect, new Vector2(hasPanelBackdrop ? 0.245f : 0.145f, 0.10f), new Vector2(0.515f, 0.90f));
            ConfigureSkillNameText(skillNameTexts[index], hasPanelBackdrop);
        }

        skillInstructionFrames[index] = EnsureChildImage(root, "InstructionFrame");
        RectTransform instructionRect = skillInstructionFrames[index].rectTransform;
        if (hasPanelBackdrop)
            SetStretchRect(instructionRect, new Vector2(0.050f, 0.10f), new Vector2(0.205f, 0.90f));
        else
            SetFixedLeftRect(instructionRect, 10f, 42f);
        if (hasPanelBackdrop)
            ConfigureTransparentImage(skillInstructionFrames[index]);
        else
            ConfigureFramedImage(skillInstructionFrames[index], SkillInsetFrameSprite(), new Color(0.10f, 0.48f, 0.28f, 0.78f));
        skillInstructionFrames[index].transform.SetAsFirstSibling();

        skillInstructionTexts[index] = EnsureChildText(skillInstructionFrames[index].transform, "InstructionText", hasPanelBackdrop ? 20 : 23, TextAnchor.MiddleCenter);
        skillInstructionTexts[index].fontStyle = FontStyle.Bold;
        skillInstructionTexts[index].color = Color.white;
        EnsureShadow(skillInstructionTexts[index].gameObject, new Color(0f, 0f, 0f, 0.75f), new Vector2(1.2f, -1.2f));

        skillElementBadges[index] = EnsureChildImage(root, "ElementBadge");
        SetStretchRect(skillElementBadges[index].rectTransform, new Vector2(0.535f, 0.27f), new Vector2(0.595f, 0.73f));
        if (hasPanelBackdrop)
            ConfigureTransparentImage(skillElementBadges[index]);
        else
            ConfigureFramedImage(skillElementBadges[index], SkillInsetFrameSprite(), new Color(0.35f, 0.75f, 0.90f, 0.88f));

        skillElementIconImages[index] = EnsureChildImage(skillElementBadges[index].transform, "ElementIcon");
        SetStretchRect(skillElementIconImages[index].rectTransform, Vector2.zero, Vector2.one);
        skillElementIconImages[index].raycastTarget = false;

        skillElementTexts[index] = EnsureChildText(skillElementBadges[index].transform, "ElementText", 13, TextAnchor.MiddleCenter);
        skillElementTexts[index].fontStyle = FontStyle.Bold;
        skillElementTexts[index].color = Color.white;
        EnsureShadow(skillElementTexts[index].gameObject, new Color(0f, 0f, 0f, 0.70f), new Vector2(1f, -1f));

        ConfigureSkillTag(skillCPTagObjects[index], skillCPTexts[index], new Vector2(0.625f, 0.20f), new Vector2(0.715f, 0.80f));
        ConfigureSkillTag(skillPowerTagObjects[index], skillPowerTexts[index], new Vector2(0.735f, 0.20f), new Vector2(0.845f, 0.80f));
        ConfigureSkillTag(skillCounterTagObjects[index], skillCounterTexts[index], new Vector2(0.735f, 0.20f), new Vector2(0.965f, 0.80f));
    }

    private void ConfigureSkillNameText(Text text, bool hasPanelBackdrop)
    {
        if (text == null)
            return;

        text.font = AnnouncerFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = hasPanelBackdrop ? 17 : Mathf.Max(17, text.fontSize);
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 11;
        text.resizeTextMaxSize = hasPanelBackdrop ? 17 : Mathf.Max(17, text.fontSize);
        text.color = new Color(0.96f, 1f, 1f, 1f);
        EnsureShadow(text.gameObject, new Color(0f, 0f, 0f, 0.70f), new Vector2(1f, -1f));
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
        ConfigureFramedImage(image, SkillTagFrameSprite(), new Color(0.10f, 0.45f, 0.30f, 0.64f));

        if (text == null)
            return;

        text.font = AnnouncerFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = Mathf.Max(12, text.fontSize);
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = Mathf.Max(13, text.fontSize);
        text.color = new Color(0.92f, 1f, 0.94f, 1f);
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

        if (!visible)
            return;

        if (skillInstructionTexts[index] != null)
        {
            skillInstructionTexts[index].text = InstructionShortLabel(skill.instructionType);
            skillInstructionTexts[index].color = InstructionTextColor(skill.instructionType);
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

    private Sprite ElementIconSprite(ElementType elementType)
    {
        int index = (int)elementType;
        if (index < 0 || index >= elementIconSprites.Length)
            return null;

        if (elementIconSprites[index] == null)
            elementIconSprites[index] = Resources.Load<Sprite>($"{ElementIconResourcePrefix}{elementType}");
        return elementIconSprites[index];
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
            case InstructionType.Attack:  return new Color(1f, 0.58f, 0.38f, 1f);
            case InstructionType.Defense: return new Color(0.62f, 0.92f, 1f, 1f);
            case InstructionType.Status:  return new Color(0.82f, 0.72f, 1f, 1f);
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

        Sprite panelSprite = AnnouncerPanelSprite();
        if (panelSprite != null)
        {
            announcerFrame.sprite = panelSprite;
            announcerFrame.type = Image.Type.Sliced;
            announcerFrame.pixelsPerUnitMultiplier = 2.5f;
            announcerFrame.color = Color.white;
            return;
        }

        announcerFrame.color = announcerFrameBaseColor;
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

    private void UpdateResourceDisplay(CombatantRefs refs, ref CombatantDisplayState display)
    {
        if (display.BatteryInitialized)
        {
            display.DisplayBattery = SmoothTo(
                display.DisplayBattery,
                display.TargetBattery,
                batteryLerpSpeed,
                Time.deltaTime);
            ApplyBatteryVisual(refs, display.DisplayBattery, display.TargetBatteryMax);
        }

        if (display.CPInitialized)
        {
            display.DisplayCP = SmoothTo(
                display.DisplayCP,
                display.TargetCP,
                cpLerpSpeed,
                Time.deltaTime);
            ApplyCPVisual(refs, display.DisplayCP, display.TargetCPMax);
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

    private static void ApplyBatteryVisual(CombatantRefs refs, float current, int max)
    {
        if (refs.BatteryFill == null)
            return;

        float ratio = max <= 0 ? 0f : Mathf.Clamp01(current / max);
        RectTransform rt = refs.BatteryFill.rectTransform;
        if (refs.BatteryFrameRect != null && rt.parent == refs.BatteryFrameRect.parent)
        {
            Vector2 frameMin = refs.BatteryFrameRect.anchorMin;
            Vector2 frameMax = refs.BatteryFrameRect.anchorMax;
            Vector2 frameSize = frameMax - frameMin;
            rt.anchorMin = frameMin + Vector2.Scale(frameSize, BatteryFillInsetMin);
            rt.anchorMax = frameMin + Vector2.Scale(frameSize, BatteryFillInsetMax);
        }
        else
        {
            rt.anchorMin = BatteryFillFallbackAnchorMin;
            rt.anchorMax = BatteryFillFallbackAnchorMax;
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (refs.BatteryFrameRect != null && rt.parent == refs.BatteryFrameRect.parent)
            rt.SetSiblingIndex(Mathf.Min(rt.parent.childCount - 1, refs.BatteryFrameRect.GetSiblingIndex() + 1));

        refs.BatteryFill.color = refs.BatteryFill.sprite == null ? refs.BatteryFillColor : Color.white;
        refs.BatteryFill.type = Image.Type.Filled;
        refs.BatteryFill.fillMethod = Image.FillMethod.Horizontal;
        refs.BatteryFill.fillOrigin = 0;
        refs.BatteryFill.fillAmount = ratio;
    }

    private static void ApplyCPVisual(CombatantRefs refs, float current, int max)
    {
        if (refs.CPDots == null)
            return;

        float filledSegments = refs.CPDots.Length <= 0 || max <= 0
            ? 0f
            : Mathf.Clamp01(current / max) * refs.CPDots.Length;

        for (int i = 0; i < refs.CPDots.Length; i++)
        {
            Image dot = refs.CPDots[i];
            if (dot == null)
                continue;

            float fill = Mathf.Clamp01(filledSegments - i);
            dot.color = Color.Lerp(CPDotInactive, CPDotActive, fill);
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
            BatteryFillColor = root.Contains("Enemy") ? EnemyBatteryFillColor : PlayerBatteryFillColor,
            BatteryFrameRect = Find<RectTransform>($"{root}/BatteryBar/BatteryFrame"),
            CPValueText      = FindText($"{root}/CPDots/CPValueText"),
            StatusText       = FindText($"{root}/StatusRow/StatusText"),
            CPDots           = new Image[0],
        };

        Transform cpRow = transform.Find($"{root}/CPDots");
        if (cpRow != null)
            refs.CPDots = FindCPDots(cpRow);

        return refs;
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
        if (bagButton      != null) bagButton.onClick.RemoveAllListeners();
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

    private void WriteSkillDetail(string title, string body)
    {
        if (skillDetailTitle != null) skillDetailTitle.text = title ?? string.Empty;
        if (skillDetailBody  != null) skillDetailBody.text  = body  ?? string.Empty;
    }

    private static void SetTag(GameObject tagObject, Text text, bool visible, string value)
    {
        if (text != null) text.text = value;
        if (tagObject != null) tagObject.SetActive(visible);
    }

    private Text FindTextDeep(string root, string subPath)
    {
        Transform rootT = transform.Find(root);
        if (rootT == null) return null;
        Transform t = rootT.Find(subPath);
        return t != null ? t.GetComponent<Text>() : null;
    }
}
