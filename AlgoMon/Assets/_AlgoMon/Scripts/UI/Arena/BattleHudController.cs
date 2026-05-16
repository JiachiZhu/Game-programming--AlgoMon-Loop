using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime API surface for the battle HUD. BattleManager (#15) drives every
/// visible value in TheArena through this class — names, HP, CP, status, skill
/// button labels, and the round / state header.
///
/// The HUD layout itself is created by BattleHudPreviewBuilder. This controller
/// just caches references to the already-built children (by name) and exposes
/// a clean API on top.
///
/// Attach to the same GameObject as the Canvas_Arena (the preview builder does
/// this automatically at the end of RebuildHud). Bind() is called once in Start
/// and re-callable if the layout is rebuilt at runtime.
/// </summary>
[DisallowMultipleComponent]
public class BattleHudController : MonoBehaviour
{
    public enum Side          { Player, Enemy }
    public enum ActionButton  { Recharge, Bag, Switch, Flee }

    public event Action<int>          SkillSlotClicked;
    public event Action<ActionButton> ActionClicked;

    public bool IsBound { get; private set; }

    private const int MaxSkillSlots = 4;
    private const int MaxCP         = 10;

    // Default text shown in the Skill Details panel when no button is hovered.
    private const string DefaultSkillDetailTitle = "Skill Details";
    private const string DefaultSkillDetailBody  = "Ready.";

    // CP dot palette — kept in sync with BattleHudPreviewBuilder so live updates
    // look identical to the freshly-built HUD.
    private static readonly Color32 CPDotActive   = new Color32( 73, 181, 255, 255);
    private static readonly Color32 CPDotInactive = new Color32( 48,  57,  66, 255);

    // Placeholder hover bodies for the default Sortex loadout baked into the
    // prefab. Keyed by skill name so layout edits that change slot order still
    // pick up the right description. BattleManager replaces these via
    // SetSkillSlot(SkillData) once the real loadout is live.
    private static readonly System.Collections.Generic.Dictionary<string, string> DefaultSkillHoverBodies =
        new System.Collections.Generic.Dictionary<string, string>
    {
        { "Volt Array",      "CP 4 | PWR 50\nReliable Electric attack.\nNo counter effect." },
        { "Faraday Cage",    "CP 2 | Counter\nDefense skill. Reduces incoming damage when it wins the matchup." },
        { "Auto-Tuning",     "CP 2\nStatus skill. Raises Computing Power." },
        { "Hyper-Threading", "CP 2\nStatus skill. Next skill fires twice." },
    };

    // --- Top bar refs ---
    private Text roundText;
    private Text battleStateText;

    // --- Per-side refs ---
    private struct CombatantRefs
    {
        public Text    NameText;
        public Text    LevelText;
        public Text    BatteryValueText;
        public Image   BatteryFill;
        public Image[] CPDots;
        public Text    StatusText;
    }
    private CombatantRefs player;
    private CombatantRefs enemy;

    // --- Skill button refs (index 0..3) ---
    private readonly Button[] skillButtons     = new Button[MaxSkillSlots];
    private readonly Text[]   skillNameTexts   = new Text  [MaxSkillSlots];
    private readonly Text[]   skillCPTexts     = new Text  [MaxSkillSlots];
    private readonly Text[]   skillPowerTexts  = new Text  [MaxSkillSlots];
    private readonly Text[]   skillCounterTexts= new Text  [MaxSkillSlots];

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

    private void Start()
    {
        if (!IsBound)
            Bind();
    }

    private void OnDestroy()
    {
        UnhookButtons();
    }

    /// <summary>
    /// Re-resolve all child references and re-wire button click events. Safe
    /// to call multiple times — old listeners are removed before re-adding.
    /// Children that don't exist (e.g. PowerTag on a Status skill button) are
    /// silently skipped; later API calls on those slots become no-ops.
    /// </summary>
    public void Bind()
    {
        UnhookButtons();

        // Canvas_Arena's children all live under SafeArea.
        roundText        = FindText("SafeArea/TopBar/RoundText");
        battleStateText  = FindText("SafeArea/TopBar/BattleStateText");

        player = BindCombatant("SafeArea/CombatLayer/PlayerCombatantPanel");
        enemy  = BindCombatant("SafeArea/CombatLayer/EnemyCombatantPanel");

        for (int i = 0; i < MaxSkillSlots; i++)
        {
            int slot = i;
            string root = $"SafeArea/CommandPanel/SkillPanel/SkillGrid/SkillButton_{i + 1}";
            skillButtons[i]       = Find<Button>(root);
            skillNameTexts[i]     = FindText($"{root}/SkillNameText");
            skillCPTexts[i]       = FindTextDeep(root, "CPTag/Text");
            skillPowerTexts[i]    = FindTextDeep(root, "PowerTag/Text");
            skillCounterTexts[i]  = FindTextDeep(root, "CounterTag/Text");

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

        skillDetailTitle = FindText("SafeArea/CommandPanel/ActionPanel/SkillDetailPanel/TitleText");
        skillDetailBody  = FindText("SafeArea/CommandPanel/ActionPanel/SkillDetailPanel/BodyText");

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
        // Mirrors the placeholder copy that BattleHudPreviewBuilder originally
        // installed. BattleManager can override at any time via SetActionHover.
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
        enter.callback.AddListener(_ => SetSkillDetail(titleGetter(), bodyGetter()));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => SetSkillDetail(DefaultSkillDetailTitle, DefaultSkillDetailBody));
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
        if (refs.BatteryValueText != null)
            refs.BatteryValueText.text = $"{current} / {max}";

        if (refs.BatteryFill != null)
        {
            float ratio = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
            RectTransform rt = refs.BatteryFill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(ratio, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    public void SetCP(Side side, int current, int max)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        if (refs.CPDots == null) return;

        int litCap = Mathf.Min(current, max);
        for (int i = 0; i < refs.CPDots.Length; i++)
        {
            if (refs.CPDots[i] == null) continue;
            refs.CPDots[i].color = i < litCap ? CPDotActive : CPDotInactive;
        }
    }

    public void SetStatus(Side side, string statusText)
    {
        ref CombatantRefs refs = ref RefsFor(side);
        if (refs.StatusText != null) refs.StatusText.text = statusText;
    }

    /// <summary>
    /// Populates the existing tags on a skill button from a SkillData asset.
    /// Only fills tags that were created by the preview builder for that button;
    /// adding new tags (e.g. PWR on a button originally built without one)
    /// requires re-running BattleHudPreviewBuilder.RebuildHud().
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
        if (skillCPTexts[index]    != null) skillCPTexts[index].text         = $"CP {skill.cpCost}";

        if (skillPowerTexts[index] != null)
            skillPowerTexts[index].text = skill.basePower > 0 ? $"PWR {skill.basePower}" : string.Empty;

        if (skillCounterTexts[index] != null)
        {
            bool showsCounter = skill.canCounter && skill.instructionType == InstructionType.Defense;
            skillCounterTexts[index].text = showsCounter ? "Counter" : string.Empty;
        }

        // Hover preview follows the skill currently in the slot.
        skillHoverTitles[index] = skill.skillName;
        skillHoverBodies[index] = string.IsNullOrEmpty(skill.description) ? string.Empty : skill.description;
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
        if (skillNameTexts[index]    != null) skillNameTexts[index].text       = "—";
        if (skillCPTexts[index]      != null) skillCPTexts[index].text         = string.Empty;
        if (skillPowerTexts[index]   != null) skillPowerTexts[index].text      = string.Empty;
        if (skillCounterTexts[index] != null) skillCounterTexts[index].text    = string.Empty;
        skillHoverTitles[index] = string.Empty;
        skillHoverBodies[index] = string.Empty;
    }

    public void ClearAllSkillSlots()
    {
        for (int i = 0; i < MaxSkillSlots; i++)
            ClearSkillSlot(i);
    }

    /// <summary>
    /// Writes the right-hand Skill Details panel. Note: hover previews (wired
    /// up in Bind()) will overwrite this on the next pointer enter / exit.
    /// To hold a persistent value, BattleManager should also call SetSkillHover
    /// / SetActionHover for the relevant buttons so hover restores to the
    /// expected text instead of the placeholder default.
    /// </summary>
    public void SetSkillDetail(string title, string body)
    {
        if (skillDetailTitle != null) skillDetailTitle.text = title;
        if (skillDetailBody  != null) skillDetailBody.text  = body;
    }

    // ----- Internals -----

    private ref CombatantRefs RefsFor(Side side)
    {
        if (side == Side.Player) return ref player;
        return ref enemy;
    }

    private CombatantRefs BindCombatant(string root)
    {
        var refs = new CombatantRefs
        {
            NameText         = FindText($"{root}/NameText"),
            LevelText        = FindText($"{root}/LevelText"),
            BatteryValueText = FindText($"{root}/BatteryBar/ValueText"),
            BatteryFill      = Find<Image>($"{root}/BatteryBar/Fill"),
            StatusText       = FindText($"{root}/StatusRow/StatusText"),
            CPDots           = new Image[MaxCP],
        };

        Transform cpRow = transform.Find($"{root}/CPDots");
        if (cpRow != null)
        {
            for (int i = 0; i < MaxCP; i++)
            {
                Transform dot = cpRow.Find($"CP_{i + 1:00}");
                refs.CPDots[i] = dot != null ? dot.GetComponent<Image>() : null;
            }
        }

        return refs;
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
    }

    private static bool IndexInRange(int index) => index >= 0 && index < MaxSkillSlots;

    private T Find<T>(string path) where T : Component
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    private Text FindText(string path) => Find<Text>(path);

    private Text FindTextDeep(string root, string subPath)
    {
        Transform rootT = transform.Find(root);
        if (rootT == null) return null;
        Transform t = rootT.Find(subPath);
        return t != null ? t.GetComponent<Text>() : null;
    }
}
