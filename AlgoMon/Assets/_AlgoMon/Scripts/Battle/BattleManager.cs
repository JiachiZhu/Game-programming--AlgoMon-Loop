/*
Script Audit:
- Purpose: Controls the complete TheArena battle loop for one encounter.
- Attached GameObject: TheArena scene GameObject named BattleManager.
- Main responsibilities: Build player/enemy parties, handle HUD input, choose enemy actions, resolve switching, ASD counters, turn order, CP, damage, statuses, subroutines, battle end, and rewards.
- Important variables: hud, presentation, rechargeSkill, playerConfig, enemyConfig, turnQueue, playerParty, enemyParty, player, enemy, phase, currentRound, battleLogLines.
- Inputs: Skill/action clicks from BattleHudController, party and opponent data from GameManager, SkillData assets, SubroutineData assets, and timing settings.
- Outputs or effects: Updates HUD and presentation, publishes battle events, changes battle state, grants defeated enemy rewards, and sends BattleEndEvent.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Test skill use, Recharge, Switch, Flee, ASD counter wins/losses, status effects, victory rewards, defeat flow, and scene transitions.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Core battle loop for TheArena.
///
/// Scope through issue #17:
/// - player chooses an action through BattleHudController
/// - enemy chooses a simple deterministic skill
/// - ASD counter check determines hard turn-order overrides
/// - skill priority and ClockSpeed break normal turn order
/// - CP is spent / restored
/// - basic damage is resolved and Battery is reduced until one side is offline
/// - status effects are applied, modify runtime stats / CP costs, and tick at round end
/// - Defense skills enter a one-round cooldown after successful execution
/// - SubroutineData triggers are applied at supported battle timing hooks
/// - former special counter skills use generic SkillData fields
///
/// Bag items (HUD button removed for now) and ally-faint Subroutine hooks are
/// intentionally left for follow-up battle issues.
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    private const int MaxCP = 10;
    private const int MaxSkillSlots = 4;
    private const float CounterPriorityBase = 1000000f;
    private const int ForceLastPriorityPenalty = -10000;
    private const float HackerSwitchBatteryPercent = 0.35f;
    private const float HackerSwitchMatchupImprovement = 0.25f;
    private const string PlayerPresentationId = "Player";
    private const string EnemyPresentationId = "Enemy";
    private static readonly Dictionary<string, Sprite> switchPortraitSpriteCache = new Dictionary<string, Sprite>();

    private enum BattlePhase
    {
        WaitingForPlayer,
        Resolving,
        BattleOver
    }

    private enum BattleActionKind
    {
        Skill,
        Switch
    }

    [Serializable]
    public class BattleCombatantConfig
    {
        public string displayName = "AlgoMon";
        [Range(1, AlgoMonInstance.MAX_LEVEL)] public int displayLevel = 1;
        public ElementType elementType = ElementType.Normal;

        [Header("Runtime Stats")]
        [Min(1)] public int maxBattery = 100;
        [Min(1)] public int clockSpeed = 50;
        [Min(1)] public int computingPower = 50;
        [Min(1)] public int throughput = 50;
        [Min(1)] public int firewall = 50;
        [Min(1)] public int encryption = 50;
        [Range(0, MaxCP)] public int startingCP = 5;

        [Header("Passive")]
        public SubroutineData subroutine;

        [Header("Active Skills")]
        public SkillData[] skills = new SkillData[MaxSkillSlots];
    }

    private sealed class BattleUnit
    {
        public BattleUnit(BattleCombatantConfig config, AlgoMonInstance instance)
        {
            Config = config;
            Instance = instance;
            // BattleUnit is per-battle runtime state; route-map damage does not persist between encounters.
            CurrentBattery = instance.Battery;
            CurrentCP = Mathf.Clamp(config.startingCP, 0, MaxCP);
            StatusText = "Ready";
        }

        public BattleCombatantConfig Config { get; }
        public AlgoMonInstance Instance { get; }
        public int CurrentBattery { get; set; }
        public int CurrentCP { get; set; }
        public string StatusText { get; set; }
        public int LastDefenseRound { get; set; } = int.MinValue;
        public bool LowBatterySubroutineTriggered { get; set; }
        public BattleStatusSet Statuses { get; } = new BattleStatusSet();
        private struct PermanentCostReduction
        {
            public int Amount;
            public int AppliedRound;
        }

        private readonly Dictionary<SkillData, PermanentCostReduction> permanentCostReductions =
            new Dictionary<SkillData, PermanentCostReduction>();

        public string Name => Config.displayName;
        public int DisplayLevel => Mathf.Clamp(Config.displayLevel, 1, AlgoMonInstance.MAX_LEVEL);
        public int MaxBattery => Instance.Battery;

        public SkillData GetSkill(int index)
        {
            if (Config.skills == null) return null;
            if (index < 0 || index >= Config.skills.Length) return null;
            return Config.skills[index];
        }

        public int CostReductionFor(SkillData skill, int currentRound)
        {
            if (skill == null)
                return 0;
            if (!permanentCostReductions.TryGetValue(skill, out PermanentCostReduction reduction))
                return 0;
            return reduction.AppliedRound < currentRound ? reduction.Amount : 0;
        }

        public int ApplyPermanentCostReduction(SkillData skill, int amount, int currentRound)
        {
            if (skill == null || amount <= 0)
                return 0;

            int before = permanentCostReductions.TryGetValue(skill, out PermanentCostReduction reduction)
                ? reduction.Amount
                : 0;
            int after = Mathf.Min(Mathf.Max(0, skill.cpCost), before + amount);
            permanentCostReductions[skill] = new PermanentCostReduction
            {
                Amount = after,
                AppliedRound = currentRound
            };
            return after - before;
        }
    }

    private sealed class BattleAction
    {
        private BattleAction(BattleActionKind kind, BattleUnit actor, BattleUnit target, SkillData skill, BattleUnit switchTarget)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Skill = skill;
            SwitchTarget = switchTarget;
        }

        public static BattleAction SkillAction(BattleUnit actor, BattleUnit target, SkillData skill)
        {
            return new BattleAction(BattleActionKind.Skill, actor, target, skill, null);
        }

        public static BattleAction SwitchAction(BattleUnit actor, BattleUnit switchTarget)
        {
            return new BattleAction(BattleActionKind.Switch, actor, null, null, switchTarget);
        }

        public BattleActionKind Kind { get; }
        public BattleUnit Actor { get; }
        public BattleUnit Target { get; set; }
        public SkillData Skill { get; }
        public BattleUnit SwitchTarget { get; }
        public bool IsSkill => Kind == BattleActionKind.Skill;
        public bool IsSwitch => Kind == BattleActionKind.Switch;
        public bool WonCounter { get; set; }
        public bool WasCountered { get; set; }
        public bool Cancelled { get; set; }
        public float FinalDamageMultiplier { get; set; } = 1f;
        public int BasePowerBonus { get; set; }
        public InstructionType DefenderInstructionType { get; set; } = InstructionType.Attack;
    }

    private struct BattleEffectBundle
    {
        public int DrainOpponentCP;
        public float ShredOpponentFirewall;
        public StatusDurationType FirewallShredDurationType;
        public int FirewallShredDuration;
        public StatusType ApplyToOpponent;
        public int OpponentStatusStacks;
        public StatusDurationType OpponentStatusDurationType;
        public int OpponentStatusDuration;
        public bool ForceOpponentLast;
        public StatusType ApplyToSelf;
        public int SelfStatusStacks;
        public StatusDurationType SelfStatusDurationType;
        public int SelfStatusDuration;
        public StatusType ApplyToSelfSecondary;
        public int SelfSecondaryStatusStacks;
        public StatusDurationType SelfSecondaryStatusDurationType;
        public int SelfSecondaryStatusDuration;
        public int SelfCPDiscount;
        public StatusDurationType CPDiscountDurationType;
        public int CPDiscountDuration;
        public SkillData PermanentCPReduceSkill;
        public int PermanentCPReduce;
        public int NextPriorityBonus;
        public int NextBasePowerBonus;
        public float SelfHealPercent;
        public bool ClearsOwnDebuffs;

        public static BattleEffectBundle FromCounterSkill(SkillData skill)
        {
            if (skill == null)
                return default;

            return new BattleEffectBundle
            {
                DrainOpponentCP = skill.counterDrainOpponentCP,
                ShredOpponentFirewall = skill.counterShredOpponentFirewall,
                FirewallShredDurationType = skill.counterFirewallShredDurationType,
                FirewallShredDuration = skill.counterFirewallShredDuration,
                ApplyToOpponent = skill.counterApplyToOpponent,
                OpponentStatusStacks = skill.counterOpponentStatusStacks,
                OpponentStatusDurationType = skill.counterOpponentStatusDurationType,
                OpponentStatusDuration = skill.counterOpponentStatusDuration,
                ForceOpponentLast = skill.counterForceOpponentLast,
                ApplyToSelf = skill.counterApplyToSelf,
                SelfStatusStacks = skill.counterSelfStatusStacks,
                SelfStatusDurationType = skill.counterSelfStatusDurationType,
                SelfStatusDuration = skill.counterSelfStatusDuration,
                ApplyToSelfSecondary = skill.counterApplyToSelfSecondary,
                SelfSecondaryStatusStacks = skill.counterSelfSecondaryStatusStacks,
                SelfSecondaryStatusDurationType = skill.counterSelfSecondaryStatusDurationType,
                SelfSecondaryStatusDuration = skill.counterSelfSecondaryStatusDuration,
                SelfCPDiscount = skill.counterSelfCPDiscount,
                CPDiscountDurationType = skill.counterCPDiscountDurationType,
                CPDiscountDuration = skill.counterCPDiscountDuration,
                PermanentCPReduceSkill = skill,
                PermanentCPReduce = skill.counterPermanentCPReduce,
                NextPriorityBonus = skill.counterNextPriorityBonus,
                NextBasePowerBonus = skill.counterNextBasePowerBonus,
                SelfHealPercent = skill.counterSelfHealPercent,
                ClearsOwnDebuffs = skill.counterClearsOwnDebuffs
            };
        }

        public static BattleEffectBundle FromSubroutine(SubroutineData subroutine)
        {
            if (subroutine == null)
                return default;

            return new BattleEffectBundle
            {
                DrainOpponentCP = subroutine.drainOpponentCP,
                ShredOpponentFirewall = subroutine.shredOpponentFirewall,
                FirewallShredDurationType = subroutine.firewallShredDurationType,
                FirewallShredDuration = subroutine.firewallShredDuration,
                ApplyToOpponent = subroutine.applyToOpponent,
                OpponentStatusStacks = subroutine.opponentStatusStacks,
                OpponentStatusDurationType = subroutine.opponentStatusDurationType,
                OpponentStatusDuration = subroutine.opponentStatusDuration,
                ForceOpponentLast = subroutine.forceOpponentLast,
                ApplyToSelf = subroutine.applyToSelf,
                SelfStatusStacks = subroutine.selfStatusStacks,
                SelfStatusDurationType = subroutine.selfStatusDurationType,
                SelfStatusDuration = subroutine.selfStatusDuration,
                SelfCPDiscount = subroutine.selfCPDiscount,
                CPDiscountDurationType = subroutine.cpDiscountDurationType,
                CPDiscountDuration = subroutine.cpDiscountDuration,
                NextPriorityBonus = subroutine.nextPriorityBonus,
                NextBasePowerBonus = subroutine.nextBasePowerBonus,
                SelfHealPercent = subroutine.selfHealPercent,
                ClearsOwnDebuffs = subroutine.clearsOwnDebuffs
            };
        }
    }

    [SerializeField] private BattleHudController hud;
    [SerializeField] private BattlePresentationController presentation;
    [Tooltip("Extra pause after the faint/defeat animation finishes before the victory panel or defeat result appears, so the KO has a beat to land.")]
    [SerializeField, Min(0f)] private float postFaintResultDelay = 0.2f;
    [SerializeField] private SkillData rechargeSkill;

    [Header("Player")]
    [SerializeField] private BattleCombatantConfig playerConfig = new BattleCombatantConfig
    {
        displayName = "Sortex",
        displayLevel = 14,
        elementType = ElementType.Electric,
        maxBattery = 165,
        clockSpeed = 74,
        computingPower = 96,
        throughput = 45,
        firewall = 52,
        encryption = 42,
        startingCP = MaxCP,
        skills = new SkillData[MaxSkillSlots]
    };

    [Header("Enemy")]
    [SerializeField] private BattleCombatantConfig enemyConfig = new BattleCombatantConfig
    {
        displayName = "Cachelon",
        displayLevel = 12,
        elementType = ElementType.Ice,
        maxBattery = 180,
        clockSpeed = 54,
        computingPower = 40,
        throughput = 88,
        firewall = 48,
        encryption = 72,
        startingCP = MaxCP,
        skills = new SkillData[MaxSkillSlots]
    };

    [Header("Battle Log Pacing")]
    [Tooltip("Most recent lines kept on screen in the Skill Details panel.")]
    [SerializeField, Min(1)] private int battleLogLineCount = 6;
    [Tooltip("Pause after a normal narration line (skill use, counter result, CP drain, etc.).")]
    [SerializeField, Min(0f)] private float logLineDelay = 0.45f;
    [Tooltip("Pause after the damage line so the player can see the battery drop.")]
    [SerializeField, Min(0f)] private float damageLineDelay = 1.2f;
    [Tooltip("Pause between the first unit's action and the second unit's action within the same round.")]
    [SerializeField, Min(0f)] private float actionTransitionDelay = 1.0f;
    [Tooltip("Pause before the round closes and player input is re-enabled.")]
    [SerializeField, Min(0f)] private float roundFinishedDelay = 0.8f;

    private readonly TurnQueue turnQueue = new TurnQueue();
    private readonly List<ScriptableObject> transientData = new List<ScriptableObject>();
    private readonly List<string> battleLogLines = new List<string>();
    private readonly List<BattleUnit> playerParty = new List<BattleUnit>();
    private readonly List<BattleUnit> enemyParty = new List<BattleUnit>();

    private BattleUnit player;
    private BattleUnit enemy;
    private BattlePhase phase = BattlePhase.WaitingForPlayer;
    private int currentRound = 1;
    private bool selectingSwitchTarget;
    private bool forcePlayerSwitchTarget;
    private bool battleEndPublished;
    private bool waitingForPostBattleContinue;
    private bool pendingBattleEndPlayerWon;
    private bool finishingBattle;
    private readonly HashSet<BattleUnit> faintPublishedUnits = new HashSet<BattleUnit>();
    private string activeActionAnnouncementLine;
    private Coroutine activeResolution;

    public int CurrentRound => currentRound;
    public bool IsBattleOver => phase == BattlePhase.BattleOver;
    public int PlayerBattery => player != null ? player.CurrentBattery : 0;
    public int EnemyBattery => enemy != null ? enemy.CurrentBattery : 0;
    public int PlayerCP => player != null ? player.CurrentCP : 0;
    public int EnemyCP => enemy != null ? enemy.CurrentCP : 0;
    private bool AwaitingForcedPlayerSwitch =>
        phase == BattlePhase.WaitingForPlayer && selectingSwitchTarget && forcePlayerSwitchTarget;

    private void Awake()
    {
        if (hud == null)
            hud = FindObjectOfType<BattleHudController>();
        if (presentation == null)
            presentation = FindObjectOfType<BattlePresentationController>();
    }

    private void Start()
    {
        BindHud();
        StartBattle();
    }

    private void OnDisable()
    {
        StopActiveResolution();
    }

    private void OnDestroy()
    {
        StopActiveResolution();

        if (hud != null)
        {
            hud.SkillSlotClicked -= HandleSkillSlotClicked;
            hud.ActionClicked -= HandleActionClicked;
            hud.PostBattleContinueClicked -= HandlePostBattleContinueClicked;
        }

        for (int i = 0; i < transientData.Count; i++)
        {
            if (transientData[i] != null)
                DestroyTransientObject(transientData[i]);
        }
        transientData.Clear();
    }

    [ContextMenu("Restart Battle")]
    public void StartBattle()
    {
        StopActiveResolution();

        IEnumerator battleStart = StartBattleCoroutine();
        if (UsesInstantResolution)
        {
            RunImmediate(battleStart);
            activeResolution = null;
            return;
        }

        activeResolution = StartCoroutine(battleStart);
    }

    private IEnumerator StartBattleCoroutine()
    {
        StopActiveResolution();
        DestroyTransientData();
        battleLogLines.Clear();
        activeActionAnnouncementLine = null;
        selectingSwitchTarget = false;
        forcePlayerSwitchTarget = false;
        waitingForPostBattleContinue = false;
        pendingBattleEndPlayerWon = false;
        finishingBattle = false;
        if (hud != null)
            hud.HidePostBattlePanel();

        BuildBattleParties();
        player = FirstAvailableUnit(playerParty);
        enemy = FirstAvailableUnit(enemyParty);
        RegisterPresentationCombatants();
        currentRound = 1;
        battleEndPublished = false;
        faintPublishedUnits.Clear();
        phase = BattlePhase.Resolving;

        EmitLog("Battle started.");
        EmitRunBuffSummary();
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

        yield return ApplySubroutineTriggerCoroutine(player, enemy, SubroutineTrigger.OnBattleStart);
        yield return ApplySubroutineTriggerCoroutine(enemy, player, SubroutineTrigger.OnBattleStart);

        phase = BattlePhase.WaitingForPlayer;
        EmitLog("Choose a skill.");
        RefreshHud();
        activeResolution = null;
    }

    public void ResolvePlayerSkill(int slotIndex)
    {
        if (selectingSwitchTarget)
        {
            ResolvePlayerSwitch(slotIndex);
            return;
        }

        if (phase != BattlePhase.WaitingForPlayer || player == null || enemy == null)
            return;

        SkillData playerSkill = player.GetSkill(slotIndex);
        if (playerSkill == null)
        {
            RejectAction("TRACE", "No skill is loaded in this slot.", false);
            return;
        }

        if (IsDefenseOnCooldown(player, playerSkill))
        {
            RejectAction(SkillName(playerSkill), "Defense is cooling down this round.", false);
            return;
        }

        if (!CanPay(player, playerSkill))
        {
            int cost = EffectiveSkillCost(player, playerSkill);
            RejectAction(SkillName(playerSkill), $"{player.Name} needs {cost} CP.", false);
            return;
        }

        StartRoundResolution(
            BattleAction.SkillAction(player, enemy, playerSkill),
            ChooseEnemyAction());
    }

    public void ResolveRecharge()
    {
        if (phase != BattlePhase.WaitingForPlayer || player == null || enemy == null)
            return;

        if (forcePlayerSwitchTarget)
        {
            RejectAction("Switch Required", $"{player.Name} is offline. Choose a reserve AlgoMon.");
            return;
        }

        selectingSwitchTarget = false;

        if (rechargeSkill == null)
        {
            RejectAction("Recharge", "Recharge skill asset is not assigned.", false);
            return;
        }

        StartRoundResolution(
            BattleAction.SkillAction(player, enemy, rechargeSkill),
            ChooseEnemyAction());
    }

    private void StartRoundResolution(BattleAction playerAction, BattleAction enemyAction)
    {
        StopActiveResolution();

        IEnumerator round = ResolveRoundCoroutine(playerAction, enemyAction);
        if (UsesInstantResolution)
        {
            RunImmediate(round);
            activeResolution = null;
            return;
        }

        activeResolution = StartCoroutine(round);
    }

    private void StartForcedPlayerSwitch(BattleUnit switchTarget)
    {
        StopActiveResolution();

        IEnumerator forcedSwitch = ResolveForcedPlayerSwitchCoroutine(switchTarget);
        if (UsesInstantResolution)
        {
            RunImmediate(forcedSwitch);
            activeResolution = null;
            return;
        }

        activeResolution = StartCoroutine(forcedSwitch);
    }

    private IEnumerator ResolveForcedPlayerSwitchCoroutine(BattleUnit switchTarget)
    {
        if (player == null || enemy == null || switchTarget == null)
        {
            activeResolution = null;
            yield break;
        }

        phase = BattlePhase.Resolving;
        turnQueue.Clear();
        yield return ExecuteSwitchCoroutine(BattleAction.SwitchAction(player, switchTarget), true);

        if (phase != BattlePhase.BattleOver)
        {
            currentRound++;
            phase = BattlePhase.WaitingForPlayer;
            EmitLog("Awaiting next instruction.");
            SetDetail("TRACE", "Choose a skill.");
            RefreshHud();
        }

        activeResolution = null;
    }

    private bool UsesInstantResolution =>
        logLineDelay <= 0f && damageLineDelay <= 0f && actionTransitionDelay <= 0f && roundFinishedDelay <= 0f;

    private static void RunImmediate(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);

        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            if (!current.MoveNext())
            {
                stack.Pop();
                continue;
            }

            if (current.Current is IEnumerator nested)
                stack.Push(nested);
        }
    }

    private void StopActiveResolution()
    {
        if (activeResolution != null)
        {
            StopCoroutine(activeResolution);
            activeResolution = null;
        }
    }

    private void BindHud()
    {
        if (hud == null)
            return;

        hud.Bind();
        hud.SkillSlotClicked -= HandleSkillSlotClicked;
        hud.ActionClicked -= HandleActionClicked;
        hud.PostBattleContinueClicked -= HandlePostBattleContinueClicked;
        hud.SkillSlotClicked += HandleSkillSlotClicked;
        hud.ActionClicked += HandleActionClicked;
        hud.PostBattleContinueClicked += HandlePostBattleContinueClicked;
    }

    private void BuildBattleParties()
    {
        playerParty.Clear();
        enemyParty.Clear();

        GameManager manager = GameManager.Instance;
        if (manager != null && manager.party != null)
        {
            for (int i = 0; i < manager.party.Count && i < GameManager.MaxPartySize; i++)
            {
                AlgoMonInstance instance = manager.party[i];
                if (instance != null)
                    playerParty.Add(CreateUnit(playerConfig, instance));
            }
        }

        if (playerParty.Count == 0)
            playerParty.Add(CreateFallbackUnit(playerConfig));

        List<AlgoMonInstance> runOpponents = ResolveRunOpponentParty();
        if (runOpponents != null)
        {
            for (int i = 0; i < runOpponents.Count; i++)
            {
                if (runOpponents[i] != null)
                    enemyParty.Add(CreateUnit(enemyConfig, runOpponents[i]));
            }
        }

        if (enemyParty.Count == 0)
            enemyParty.Add(CreateUnit(enemyConfig, ResolveRunOpponentInstance()));
    }

    private static BattleUnit FirstAvailableUnit(List<BattleUnit> party)
    {
        if (party == null || party.Count == 0)
            return null;

        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null && party[i].CurrentBattery > 0)
                return party[i];
        }

        return party[0];
    }

    private AlgoMonInstance ResolveRunOpponentInstance()
    {
        GameManager manager = GameManager.Instance;
        return manager != null ? manager.currentOpponent : null;
    }

    private List<AlgoMonInstance> ResolveRunOpponentParty()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.currentOpponentParty == null || manager.currentOpponentParty.Count == 0)
            return null;

        return manager.currentOpponentParty;
    }

    private BattleUnit CreateUnit(BattleCombatantConfig config, AlgoMonInstance runtimeInstance)
    {
        if (runtimeInstance != null && runtimeInstance.data != null)
            return new BattleUnit(BuildRuntimeConfig(config, runtimeInstance), runtimeInstance);

        return CreateFallbackUnit(config);
    }

    private BattleUnit CreateFallbackUnit(BattleCombatantConfig config)
    {
        AlgoMonData data = ScriptableObject.CreateInstance<AlgoMonData>();
        data.codeName = config.displayName;
        data.elementType = config.elementType;
        data.subroutine = config.subroutine;
        transientData.Add(data);

        var instance = new AlgoMonInstance
        {
            data = data,
            nickname = config.displayName,
            usesTransientData = true,
            level = AlgoMonInstance.MAX_LEVEL,
            iv_Battery = ClampStat(config.maxBattery),
            iv_ClockSpeed = ClampStat(config.clockSpeed),
            iv_ComputingPower = ClampStat(config.computingPower),
            iv_Throughput = ClampStat(config.throughput),
            iv_Firewall = ClampStat(config.firewall),
            iv_Encryption = ClampStat(config.encryption),
            knownSkills = new List<SkillData>(MaxSkillSlots)
        };

        if (config.skills != null)
        {
            for (int i = 0; i < config.skills.Length && i < MaxSkillSlots; i++)
            {
                if (config.skills[i] != null)
                    instance.knownSkills.Add(config.skills[i]);
            }
        }

        return new BattleUnit(config, instance);
    }

    private static BattleCombatantConfig BuildRuntimeConfig(BattleCombatantConfig fallback, AlgoMonInstance instance)
    {
        instance.EnsureKnownSkillsFromLearnset();
        string displayName = DisplayNameFor(instance);
        if (string.IsNullOrWhiteSpace(instance.nickname))
            instance.nickname = displayName;

        var config = new BattleCombatantConfig
        {
            displayName = displayName,
            displayLevel = Mathf.Clamp(instance.level, 1, AlgoMonInstance.MAX_LEVEL),
            elementType = instance.data.elementType,
            maxBattery = Mathf.Max(1, instance.Battery),
            clockSpeed = Mathf.Max(1, instance.ClockSpeed),
            computingPower = Mathf.Max(1, instance.ComputingPower),
            throughput = Mathf.Max(1, instance.Throughput),
            firewall = Mathf.Max(1, instance.Firewall),
            encryption = Mathf.Max(1, instance.Encryption),
            startingCP = fallback != null ? fallback.startingCP : 5,
            subroutine = instance.data.subroutine,
            skills = new SkillData[MaxSkillSlots]
        };

        bool hasRuntimeSkills = false;
        if (instance.knownSkills != null)
        {
            for (int i = 0; i < instance.knownSkills.Count && i < MaxSkillSlots; i++)
            {
                config.skills[i] = instance.knownSkills[i];
                if (instance.knownSkills[i] != null)
                    hasRuntimeSkills = true;
            }
        }

        if (!hasRuntimeSkills && fallback != null && fallback.skills != null)
        {
            for (int i = 0; i < fallback.skills.Length && i < MaxSkillSlots; i++)
                config.skills[i] = fallback.skills[i];
        }

        return config;
    }

    private void RegisterPresentationCombatants()
    {
        if (presentation == null || player == null || enemy == null)
            return;

        presentation.RegisterCombatants(
            PlayerPresentationId,
            EnemyPresentationId,
            player.Instance.data != null ? player.Instance.data.battleAnimationProfile : null,
            enemy.Instance.data != null ? enemy.Instance.data.battleAnimationProfile : null,
            player.Instance.data != null ? player.Instance.data.codeName : null,
            enemy.Instance.data != null ? enemy.Instance.data.codeName : null,
            player.Instance.battleFormName,
            enemy.Instance.battleFormName);
    }

    /// <summary>
    /// Re-registers only the side that changed (a switch / send-next), so the
    /// opponent — which did not change — does not replay its entry animation.
    /// </summary>
    private void RegisterPresentationCombatant(bool playerSide)
    {
        if (presentation == null || player == null || enemy == null)
            return;

        BattleUnit unit = playerSide ? player : enemy;
        if (unit?.Instance == null)
            return;

        presentation.RegisterCombatantSide(
            playerSide,
            playerSide ? PlayerPresentationId : EnemyPresentationId,
            unit.Instance.data != null ? unit.Instance.data.battleAnimationProfile : null,
            unit.Instance.data != null ? unit.Instance.data.codeName : null,
            unit.Instance.battleFormName);
    }

    private string PresentationIdFor(BattleUnit unit)
    {
        if (unit == null)
            return string.Empty;
        if (ReferenceEquals(unit, player))
            return PlayerPresentationId;
        if (ReferenceEquals(unit, enemy))
            return EnemyPresentationId;
        return string.Empty;
    }

    private static int ClampStat(int value) => Mathf.Clamp(value, 1, 255);

    private static string DisplayNameFor(AlgoMonInstance instance)
    {
        if (instance == null)
            return "AlgoMon";
        if (!string.IsNullOrWhiteSpace(instance.nickname))
            return instance.nickname.Trim();
        if (instance.data != null && !string.IsNullOrWhiteSpace(instance.data.codeName))
            return instance.data.codeName.Trim();
        return "AlgoMon";
    }

    private void HandleSkillSlotClicked(int slotIndex)
    {
        ResolvePlayerSkill(slotIndex);
    }

    /// <summary>
    /// Player clicked something whose gameplay condition is not met (cooldown,
    /// CP shortage, fainted target, trapped, ...). One glitch SFX + the detail
    /// line; refreshHud mirrors what each call site did before the SFX existed.
    /// </summary>
    private void RejectAction(string title, string body, bool refreshHud = true)
    {
        AudioManager.Instance?.PlayUiSfx(UiSfx.Invalid);
        SetDetail(title, body);
        if (refreshHud)
            RefreshHud();
    }

    private void HandleActionClicked(BattleHudController.ActionButton button)
    {
        if (phase != BattlePhase.WaitingForPlayer)
            return;

        if (forcePlayerSwitchTarget)
        {
            RejectAction("Switch Required", $"{player.Name} is offline. Choose a reserve AlgoMon.");
            return;
        }

        switch (button)
        {
            case BattleHudController.ActionButton.Recharge:
                ResolveRecharge();
                break;

            case BattleHudController.ActionButton.Switch:
                ToggleSwitchSelection();
                break;

            case BattleHudController.ActionButton.Flee:
                selectingSwitchTarget = false;
                FinishBattle(false, $"{player.Name} fled from the battle.");
                break;
        }
    }

    private void ToggleSwitchSelection()
    {
        if (selectingSwitchTarget)
        {
            if (forcePlayerSwitchTarget)
            {
                RejectAction("Switch Required", $"{player.Name} is offline. Choose a reserve AlgoMon.");
                return;
            }

            selectingSwitchTarget = false;
            SetDetail("TRACE", "Choose a skill.");
            RefreshHud();
            return;
        }

        if (!CanSwitchOut(player))
        {
            RejectAction("Switch", $"{player.Name} cannot switch right now.");
            return;
        }

        if (!HasSwitchTarget(playerParty, player))
        {
            RejectAction("Switch", "No reserve AlgoMon is ready.");
            return;
        }

        selectingSwitchTarget = true;
        SetDetail("Switch", "Choose a reserve AlgoMon.");
        RefreshHud();
    }

    private void ResolvePlayerSwitch(int partyIndex)
    {
        if (phase != BattlePhase.WaitingForPlayer || player == null || enemy == null)
            return;

        bool forcedSwitch = forcePlayerSwitchTarget;
        if (!TryGetSwitchTarget(playerParty, player, partyIndex, forcedSwitch, out BattleUnit switchTarget, out string reason))
        {
            RejectAction("Switch", reason);
            return;
        }

        selectingSwitchTarget = false;
        forcePlayerSwitchTarget = false;

        if (forcedSwitch)
        {
            StartForcedPlayerSwitch(switchTarget);
            return;
        }

        StartRoundResolution(
            BattleAction.SwitchAction(player, switchTarget),
            ChooseEnemyAction());
    }

    private BattleAction ChooseEnemyAction()
    {
        if (TryChooseEnemySwitchTarget(out BattleUnit switchTarget))
            return BattleAction.SwitchAction(enemy, switchTarget);

        return BattleAction.SkillAction(enemy, player, ChooseEnemySkill());
    }

    private SkillData ChooseEnemySkill()
    {
        SkillData bestAttack = null;
        SkillData bestFallback = null;

        for (int i = 0; i < MaxSkillSlots; i++)
        {
            SkillData skill = enemy.GetSkill(i);
            if (skill == null || !CanUseSkill(enemy, skill))
                continue;

            if (bestFallback == null)
                bestFallback = skill;

            if (skill.damageType != DamageType.None)
            {
                if (bestAttack == null ||
                    skill.priority > bestAttack.priority ||
                    (skill.priority == bestAttack.priority && skill.basePower > bestAttack.basePower))
                {
                    bestAttack = skill;
                }
            }
        }

        if (bestAttack != null)
            return bestAttack;
        if (bestFallback != null)
            return bestFallback;
        return rechargeSkill;
    }

    private bool TryChooseEnemySwitchTarget(out BattleUnit switchTarget)
    {
        switchTarget = null;

        if (!IsHackerBattle() || !CanSwitchOut(enemy) || !HasSwitchTarget(enemyParty, enemy))
            return false;

        bool lowBattery = enemy.CurrentBattery <= HackerSwitchBatteryThreshold(enemy);
        bool poorMatchup = IsPoorMatchup(enemy, player);
        if (!lowBattery && !poorMatchup)
            return false;

        return TryGetBestSwitchTarget(enemyParty, enemy, player, lowBattery, out switchTarget);
    }

    private bool TryGetBestSwitchTarget(
        List<BattleUnit> party,
        BattleUnit activeUnit,
        BattleUnit opponent,
        bool lowBattery,
        out BattleUnit switchTarget)
    {
        switchTarget = null;
        if (party == null || activeUnit == null || opponent == null)
            return false;

        float activeScore = MatchupScore(activeUnit, opponent);
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < party.Count; i++)
        {
            BattleUnit candidate = party[i];
            if (candidate == null ||
                ReferenceEquals(candidate, activeUnit) ||
                candidate.CurrentBattery <= 0)
            {
                continue;
            }

            float candidateScore = MatchupScore(candidate, opponent);
            candidateScore += BatteryFraction(candidate) * 0.15f;
            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                switchTarget = candidate;
            }
        }

        return switchTarget != null &&
               (lowBattery || bestScore > activeScore + HackerSwitchMatchupImprovement);
    }

    private static bool IsPoorMatchup(BattleUnit activeUnit, BattleUnit opponent)
    {
        if (activeUnit == null || opponent == null)
            return false;

        float outgoing = BestOutgoingElementMultiplier(activeUnit, opponent);
        float incoming = BestOutgoingElementMultiplier(opponent, activeUnit);
        return outgoing <= 0.75f || incoming >= 1.5f;
    }

    private static float MatchupScore(BattleUnit candidate, BattleUnit opponent)
    {
        if (candidate == null || opponent == null)
            return 0f;

        float outgoing = BestOutgoingElementMultiplier(candidate, opponent);
        float incoming = BestOutgoingElementMultiplier(opponent, candidate);
        return outgoing - incoming;
    }

    private static float BestOutgoingElementMultiplier(BattleUnit attacker, BattleUnit defender)
    {
        if (attacker == null || defender == null || defender.Instance?.data == null)
            return 1f;

        float best = 1f;
        for (int i = 0; i < MaxSkillSlots; i++)
        {
            SkillData skill = attacker.GetSkill(i);
            if (skill == null || skill.damageType == DamageType.None)
                continue;

            best = Mathf.Max(
                best,
                CombatResolver.GetElementMultiplier(skill.elementType, defender.Instance.data.elementType));
        }

        return best;
    }

    private static float BatteryFraction(BattleUnit unit)
    {
        return unit != null && unit.MaxBattery > 0
            ? Mathf.Clamp01(unit.CurrentBattery / (float)unit.MaxBattery)
            : 0f;
    }

    private static int HackerSwitchBatteryThreshold(BattleUnit unit)
    {
        return unit != null
            ? Mathf.Max(1, Mathf.CeilToInt(unit.MaxBattery * HackerSwitchBatteryPercent))
            : 0;
    }

    private static bool CanSwitchOut(BattleUnit unit)
    {
        return unit != null &&
               unit.CurrentBattery > 0 &&
               unit.Statuses.GetStacks(StatusType.Ensnare) <= 0;
    }

    private static bool HasSwitchTarget(List<BattleUnit> party, BattleUnit activeUnit)
    {
        if (!CanSwitchOut(activeUnit) || party == null)
            return false;

        return HasAvailableReserve(party, activeUnit);
    }

    private static bool HasAvailableReserve(List<BattleUnit> party, BattleUnit activeUnit)
    {
        if (party == null)
            return false;

        for (int i = 0; i < party.Count; i++)
        {
            BattleUnit unit = party[i];
            if (unit != null &&
                !ReferenceEquals(unit, activeUnit) &&
                unit.CurrentBattery > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSwitchTarget(
        List<BattleUnit> party,
        BattleUnit activeUnit,
        int partyIndex,
        bool allowOfflineActive,
        out BattleUnit switchTarget,
        out string reason)
    {
        switchTarget = null;

        bool activeCanSwitch = CanSwitchOut(activeUnit);
        bool activeIsOffline = activeUnit != null && activeUnit.CurrentBattery <= 0;
        if (!activeCanSwitch && !(allowOfflineActive && activeIsOffline))
        {
            reason = activeUnit != null && activeUnit.Statuses.GetStacks(StatusType.Ensnare) > 0
                ? $"{activeUnit.Name} is ensnared and cannot switch."
                : "The active AlgoMon cannot switch.";
            return false;
        }

        if (party == null || partyIndex < 0 || partyIndex >= party.Count)
        {
            reason = "No party slot is loaded there.";
            return false;
        }

        switchTarget = party[partyIndex];
        if (switchTarget == null)
        {
            reason = "No AlgoMon is loaded there.";
            return false;
        }

        if (ReferenceEquals(switchTarget, activeUnit))
        {
            reason = $"{switchTarget.Name} is already active.";
            return false;
        }

        if (switchTarget.CurrentBattery <= 0)
        {
            reason = $"{switchTarget.Name} is offline.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private IEnumerator ResolveRoundCoroutine(BattleAction playerAction, BattleAction enemyAction)
    {
        if (!IsValidDeclaredAction(playerAction) || !IsValidDeclaredAction(enemyAction))
        {
            phase = BattlePhase.WaitingForPlayer;
            SetDetail("Battle", "No valid action was declared.");
            RefreshHud();
            activeResolution = null;
            yield break;
        }

        phase = BattlePhase.Resolving;
        player.StatusText = "Ready";
        enemy.StatusText = "Ready";

        EmitLog($"-- Round {currentRound} --");
        EmitLog($"{playerAction.Actor.Name} commits {ActionName(playerAction)}.");
        EmitLog($"{enemyAction.Actor.Name} commits {ActionName(enemyAction)}.");
        Announce(
            $"Round {currentRound}",
            $"{playerAction.Actor.Name}: {ActionName(playerAction)}  |  {enemyAction.Actor.Name}: {ActionName(enemyAction)}");
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

        yield return ResolveSwitchesCoroutine(playerAction, enemyAction);
        RetargetSkillActions(playerAction, enemyAction);
        SetDefenderInstructionTypes(playerAction, enemyAction);

        yield return ResolveCounterCoroutine(playerAction, enemyAction);
        TryFinishBattle();
        if (phase == BattlePhase.BattleOver)
        {
            RefreshHud();
            activeResolution = null;
            yield break;
        }
        if (AwaitingForcedPlayerSwitch)
        {
            RefreshHud();
            activeResolution = null;
            yield break;
        }

        QueueActions(playerAction, enemyAction);

        while (!turnQueue.IsEmpty && phase == BattlePhase.Resolving)
        {
            AlgoMonInstance next = turnQueue.Dequeue();
            BattleAction action = ActionFor(next, playerAction, enemyAction);
            if (action == null)
                continue;
            yield return ExecuteActionCoroutine(action);
            if (phase == BattlePhase.Resolving && action.WonCounter)
                yield return ApplySubroutineTriggerCoroutine(action.Actor, action.Target, SubroutineTrigger.OnCounterWin);
            TryFinishBattle();
            if (AwaitingForcedPlayerSwitch)
                break;
            if (phase == BattlePhase.Resolving && !turnQueue.IsEmpty && actionTransitionDelay > 0f)
                yield return new WaitForSeconds(actionTransitionDelay);
        }
        activeActionAnnouncementLine = null;

        if (phase == BattlePhase.Resolving)
        {
            yield return ResolveEndOfRoundStatusesCoroutine();
        }

        if (phase == BattlePhase.Resolving)
        {
            if (roundFinishedDelay > 0f)
                yield return new WaitForSeconds(roundFinishedDelay);
            currentRound++;
            phase = BattlePhase.WaitingForPlayer;
            EmitLog("Awaiting next instruction.");
        }

        RefreshHud();
        activeResolution = null;
    }

    private static bool IsValidDeclaredAction(BattleAction action)
    {
        if (action == null || action.Actor == null)
            return false;
        if (action.IsSwitch)
            return action.SwitchTarget != null;
        return action.Skill != null;
    }

    private static string ActionName(BattleAction action)
    {
        if (action == null)
            return "None";
        if (action.IsSwitch)
            return action.SwitchTarget != null ? $"Switch to {action.SwitchTarget.Name}" : "Switch";
        return SkillName(action.Skill);
    }

    private IEnumerator ResolveSwitchesCoroutine(BattleAction playerAction, BattleAction enemyAction)
    {
        if (playerAction != null && playerAction.IsSwitch)
            yield return ExecuteSwitchCoroutine(playerAction, true);

        if (enemyAction != null && enemyAction.IsSwitch)
            yield return ExecuteSwitchCoroutine(enemyAction, false);
    }

    private IEnumerator ExecuteSwitchCoroutine(BattleAction action, bool playerSide)
    {
        if (action == null || !action.IsSwitch || action.Actor == null || action.SwitchTarget == null)
            yield break;

        BattleUnit previous = action.Actor;
        BattleUnit next = action.SwitchTarget;
        if (ReferenceEquals(previous, next))
            yield break;

        int cleared = previous.Statuses.ClearSwapLimitedEffects();
        previous.StatusText = previous.CurrentBattery <= 0 ? "Offline" : "Benched";
        next.StatusText = "Switched in";

        if (playerSide)
            player = next;
        else
            enemy = next;

        RegisterPresentationCombatant(playerSide);
        EmitLog($"{previous.Name} switches out. {next.Name} enters.");
        if (cleared > 0)
            EmitLog($"{previous.Name}'s temporary effects clear on switch.");
        Announce("Switch", $"{previous.Name} -> {next.Name}");
        RefreshHud();
        if (presentation != null)
            yield return presentation.PlaySwitchReveal(playerSide ? PlayerPresentationId : EnemyPresentationId);
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);
    }

    private void RetargetSkillActions(BattleAction playerAction, BattleAction enemyAction)
    {
        if (playerAction != null && playerAction.IsSkill)
            playerAction.Target = enemy;
        if (enemyAction != null && enemyAction.IsSkill)
            enemyAction.Target = player;
    }

    private static void SetDefenderInstructionTypes(BattleAction playerAction, BattleAction enemyAction)
    {
        if (playerAction == null || enemyAction == null)
            return;

        if (playerAction.IsSkill && enemyAction.IsSkill)
        {
            playerAction.DefenderInstructionType = enemyAction.Skill.instructionType;
            enemyAction.DefenderInstructionType = playerAction.Skill.instructionType;
        }
    }

    private IEnumerator ResolveCounterCoroutine(BattleAction playerAction, BattleAction enemyAction)
    {
        if (playerAction == null || enemyAction == null || !playerAction.IsSkill || !enemyAction.IsSkill)
            yield break;

        bool playerCounters = CanCounter(playerAction.Skill, enemyAction.Skill);
        bool enemyCounters = CanCounter(enemyAction.Skill, playerAction.Skill);

        if (playerCounters == enemyCounters)
            yield break;

        BattleAction winner = playerCounters ? playerAction : enemyAction;
        BattleAction loser = playerCounters ? enemyAction : playerAction;

        winner.WonCounter = true;
        loser.WasCountered = true;
        winner.Actor.StatusText = "Counter";
        loser.Actor.StatusText = "Delayed";

        // Counter cut-in: flash + letterbox banner over a quick status flourish from
        // the winner, giving clear "counter!" feedback before the clash plays.
        BattleHudController.Side winnerSide =
            playerCounters ? BattleHudController.Side.Player : BattleHudController.Side.Enemy;
        string winnerPresentationId = PresentationIdFor(winner.Actor);
        if (hud != null)
        {
            Sprite[] statusFrames = null;
            float statusFps = 12f;
            if (presentation != null)
                presentation.TryGetStatusFrames(winnerPresentationId, out statusFrames, out statusFps);
            hud.PlayCounterBanner(winnerSide, winner.Actor.Name, statusFrames, statusFps);
        }
        if (presentation != null)
            yield return presentation.PlayCounterCutInFlourish(winnerPresentationId);

        EventBus.Publish(new CounterEvent
        {
            CounterId = PresentationIdFor(winner.Actor),
            CounteredId = PresentationIdFor(loser.Actor),
            CounterHasDamage = winner.Skill.damageType != DamageType.None,
            CounteredHasDamage = loser.Skill.damageType != DamageType.None && !winner.Skill.counterNullifies,
            CounteredCancelled = winner.Skill.counterNullifies && !IsRechargeSkill(loser.Skill),
            CounterInstructionType = winner.Skill.instructionType,
            CounteredInstructionType = loser.Skill.instructionType
        });

        AudioManager.Instance?.PlayCounterSfx();

        string counterSummary = SkillDetailTextFormatter.BuildCounterSummary(winner.Skill);
        if (counterSummary.StartsWith("Counter:", StringComparison.OrdinalIgnoreCase))
            counterSummary = counterSummary.Substring("Counter:".Length).Trim();

        EmitLog(string.IsNullOrWhiteSpace(counterSummary)
            ? $"{winner.Actor.Name}'s {SkillName(winner.Skill)} counters."
            : $"{winner.Actor.Name}'s {SkillName(winner.Skill)} counters: {counterSummary}");
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

        // Recharge is the only 0-CP action, so nullifying it would soft-lock a
        // player at 0 CP (their recharge gets cancelled every turn). It still
        // loses the ASD check and takes the hit, but its CP restore always lands.
        if (winner.Skill.counterNullifies && !IsRechargeSkill(loser.Skill))
        {
            loser.Cancelled = true;
            loser.Actor.StatusText = "Nullified";
            EmitLog($"{loser.Actor.Name}'s {SkillName(loser.Skill)} is nullified.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        if (winner.Skill.counterBlockPercent > 0f)
        {
            float damageTaken = 1f - Mathf.Clamp01(winner.Skill.counterBlockPercent);
            loser.FinalDamageMultiplier *= damageTaken;
            EmitLog($"{winner.Actor.Name} blocks {Mathf.RoundToInt(winner.Skill.counterBlockPercent * 100f)}% of incoming damage.");
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        yield return ApplyCounterEffectsCoroutine(winner, loser);

        yield return ApplySubroutineTriggerCoroutine(loser.Actor, winner.Actor, SubroutineTrigger.OnCounterLose);
    }

    private static bool CanCounter(SkillData attackerSkill, SkillData defenderSkill)
    {
        if (attackerSkill == null || defenderSkill == null)
            return false;
        return attackerSkill.canCounter &&
               CombatResolver.IsCounter(attackerSkill.instructionType, defenderSkill.instructionType);
    }

    private IEnumerator ApplyCounterEffectsCoroutine(BattleAction winner, BattleAction loser)
    {
        yield return ApplyBattleEffectBundleCoroutine(
            winner.Actor,
            loser.Actor,
            BattleEffectBundle.FromCounterSkill(winner.Skill));
    }

    private IEnumerator ApplySubroutineTriggerCoroutine(
        BattleUnit owner,
        BattleUnit opponent,
        SubroutineTrigger trigger)
    {
        SubroutineData subroutine = owner?.Instance?.data?.subroutine;
        if (owner == null || opponent == null || owner.CurrentBattery <= 0)
            yield break;
        if (subroutine == null || subroutine.trigger != trigger)
            yield break;

        EmitLog($"{owner.Name}'s {SubroutineName(subroutine)} activates.");
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

        yield return ApplyBattleEffectBundleCoroutine(
            owner,
            opponent,
            BattleEffectBundle.FromSubroutine(subroutine));
    }

    private IEnumerator ApplyBattleEffectBundleCoroutine(
        BattleUnit owner,
        BattleUnit opponent,
        BattleEffectBundle effects)
    {
        if (effects.DrainOpponentCP > 0)
        {
            int drained = DrainCP(opponent, owner, effects.DrainOpponentCP);
            if (drained > 0)
            {
                EmitLog($"{owner.Name} drains {drained} CP.");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }
        }

        if (effects.ShredOpponentFirewall > 0f)
        {
            opponent.Statuses.ApplyFirewallShred(
                effects.ShredOpponentFirewall,
                effects.FirewallShredDurationType,
                effects.FirewallShredDuration,
                currentRound);

            EmitLog($"{opponent.Name}'s Firewall is shredded by {Mathf.RoundToInt(effects.ShredOpponentFirewall * 100f)}%.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        yield return ApplyStatusCoroutine(
            owner,
            opponent,
            effects.ApplyToOpponent,
            effects.OpponentStatusStacks,
            effects.OpponentStatusDurationType,
            effects.OpponentStatusDuration);

        if (effects.ForceOpponentLast)
        {
            opponent.Statuses.ApplyNextPriorityBonus(ForceLastPriorityPenalty, currentRound);
            EmitLog($"{opponent.Name} is forced to act last next round.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        yield return ApplyStatusCoroutine(
            owner,
            owner,
            effects.ApplyToSelf,
            effects.SelfStatusStacks,
            effects.SelfStatusDurationType,
            effects.SelfStatusDuration);

        yield return ApplyStatusCoroutine(
            owner,
            owner,
            effects.ApplyToSelfSecondary,
            effects.SelfSecondaryStatusStacks,
            effects.SelfSecondaryStatusDurationType,
            effects.SelfSecondaryStatusDuration);

        if (effects.SelfCPDiscount > 0)
        {
            owner.Statuses.ApplyCPDiscount(
                effects.SelfCPDiscount,
                effects.CPDiscountDurationType,
                effects.CPDiscountDuration,
                currentRound);

            EmitLog($"{owner.Name}'s skill CP costs fall by {effects.SelfCPDiscount}.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        if (effects.PermanentCPReduceSkill != null && effects.PermanentCPReduce > 0)
        {
            int reduced = owner.ApplyPermanentCostReduction(
                effects.PermanentCPReduceSkill,
                effects.PermanentCPReduce,
                currentRound);
            if (reduced > 0)
            {
                EmitLog($"Future {SkillName(effects.PermanentCPReduceSkill)} casts cost {reduced} less CP for {owner.Name}.");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }
        }

        if (effects.NextPriorityBonus != 0)
        {
            owner.Statuses.ApplyNextPriorityBonus(effects.NextPriorityBonus, currentRound);
            EmitLog($"{owner.Name}'s next action priority changes by {FormatSigned(effects.NextPriorityBonus)}.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        if (effects.NextBasePowerBonus != 0)
        {
            owner.Statuses.ApplyNextBasePowerBonus(effects.NextBasePowerBonus, currentRound);
            EmitLog($"{owner.Name}'s next action gains {FormatSigned(effects.NextBasePowerBonus)} base power.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        if (effects.SelfHealPercent > 0f)
        {
            int heal = Mathf.Max(1, Mathf.RoundToInt(owner.MaxBattery * effects.SelfHealPercent));
            int restored = HealBattery(owner, heal);
            if (restored > 0)
            {
                EmitLog($"{owner.Name} restores {restored} Battery.");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }
        }

        if (effects.ClearsOwnDebuffs)
        {
            int removed = owner.Statuses.ClearTemporaryDebuffs();
            if (removed > 0)
            {
                EmitLog($"{owner.Name} clears {removed} temporary debuff(s).");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }
        }
    }

    private void QueueActions(BattleAction playerAction, BattleAction enemyAction)
    {
        turnQueue.Clear();

        if (playerAction != null && enemyAction != null &&
            playerAction.IsSkill && enemyAction.IsSkill &&
            playerAction.WonCounter)
        {
            float priority = CounterPriority(playerAction);
            turnQueue.Enqueue(playerAction.Actor.Instance, priority);
            turnQueue.ForceAfter(enemyAction.Actor.Instance, playerAction.Actor.Instance, priority);
            return;
        }

        if (playerAction != null && enemyAction != null &&
            playerAction.IsSkill && enemyAction.IsSkill &&
            enemyAction.WonCounter)
        {
            float priority = CounterPriority(enemyAction);
            turnQueue.Enqueue(enemyAction.Actor.Instance, priority);
            turnQueue.ForceAfter(playerAction.Actor.Instance, enemyAction.Actor.Instance, priority);
            return;
        }

        EnqueueSkillAction(playerAction);
        EnqueueSkillAction(enemyAction);
    }

    private void EnqueueSkillAction(BattleAction action)
    {
        if (action != null && action.IsSkill)
            turnQueue.Enqueue(action.Actor.Instance, EffectivePriority(action));
    }

    private float CounterPriority(BattleAction action)
    {
        return CounterPriorityBase + EffectivePriority(action);
    }

    private float EffectivePriority(BattleAction action)
    {
        int skillPriority = action.Skill != null ? action.Skill.priority : 0;
        skillPriority += action.Actor.Statuses.PriorityBonus(currentRound);
        return skillPriority * 10000f + EffectiveStats(action.Actor).ClockSpeed;
    }

    private BattleAction ActionFor(AlgoMonInstance instance, BattleAction playerAction, BattleAction enemyAction)
    {
        if (playerAction != null && playerAction.IsSkill && ReferenceEquals(instance, playerAction.Actor.Instance))
            return playerAction;
        if (enemyAction != null && enemyAction.IsSkill && ReferenceEquals(instance, enemyAction.Actor.Instance))
            return enemyAction;
        return null;
    }

    private IEnumerator ExecuteActionCoroutine(BattleAction action)
    {
        if (action == null || !action.IsSkill || action.Actor.CurrentBattery <= 0)
            yield break;

        if (action.Cancelled)
        {
            EmitLog($"{action.Actor.Name}'s turn is cancelled.");
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
            yield break;
        }

        yield return ApplySubroutineTriggerCoroutine(action.Actor, action.Target, SubroutineTrigger.OnTurnStart);
        TryFinishBattle();
        if (phase != BattlePhase.Resolving || action.Actor.CurrentBattery <= 0)
            yield break;

        int cost = EffectiveSkillCost(action.Actor, action.Skill);
        action.BasePowerBonus = action.Actor.Statuses.BasePowerBonus(currentRound);
        int repeatCount = action.Actor.Statuses.SkillRepeatCount(currentRound);
        bool consumedSkillUseModifiers = false;

        for (int repeat = 0; repeat < repeatCount && phase == BattlePhase.Resolving; repeat++)
        {
            if (!SpendCP(action.Actor, cost))
            {
                action.Actor.StatusText = "No CP";
                if (repeat == 0)
                    EmitLog($"{action.Actor.Name} lacks {cost} CP for {SkillName(action.Skill)}.");
                else
                    EmitLog($"{SkillName(action.Skill)} cannot repeat; {action.Actor.Name} lacks {cost} CP.");

                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
                if (repeat == 0)
                    yield break;
                break;
            }

            if (!consumedSkillUseModifiers)
            {
                if (action.Skill.instructionType == InstructionType.Defense)
                    action.Actor.LastDefenseRound = currentRound;

                action.Actor.Statuses.ConsumeSkillUseModifiers(currentRound);
                consumedSkillUseModifiers = true;
            }

            if (repeat == 0)
                EmitLog($"{action.Actor.Name} uses {SkillName(action.Skill)}.");
            else
                EmitLog($"{SkillName(action.Skill)} repeats from Concurrent.");

            if (repeat == 0 && hud != null)
            {
                BattleHudController.Side actorSide =
                    IsPlayerUnit(action.Actor) ? BattleHudController.Side.Player : BattleHudController.Side.Enemy;
                hud.PlayActionBanner(actorSide, cost, action.Actor.Name, SkillName(action.Skill));
            }

            EventBus.Publish(new BattleActionEvent
            {
                ActorId = PresentationIdFor(action.Actor),
                ActorName = action.Actor.Name,
                TargetId = PresentationIdFor(action.Target),
                SkillName = SkillName(action.Skill),
                InstructionType = action.Skill.instructionType,
                WonCounter = action.WonCounter,
                WasCountered = action.WasCountered
            });
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);

            yield return ResolveSkillEffectCoroutine(action);
            TryFinishBattle();
            if (phase != BattlePhase.Resolving || action.Target.CurrentBattery <= 0)
                yield break;
        }

        if (action.WonCounter && action.Skill.counterRecast && phase == BattlePhase.Resolving && action.Target.CurrentBattery > 0)
        {
            EmitLog($"{SkillName(action.Skill)} recasts from counter momentum.");

            // Re-emit the action so the recast strike replays the attacker's lunge +
            // VFX (+ SFX), not just damage. It is a FRESH strike, not the countering
            // hit itself, so WonCounter=false keeps it out of the counter-cut-in
            // suppression. Without this the recast only ran damage resolution: the
            // target flinched but the attacker showed no second attack effect.
            EventBus.Publish(new BattleActionEvent
            {
                ActorId = PresentationIdFor(action.Actor),
                ActorName = action.Actor.Name,
                TargetId = PresentationIdFor(action.Target),
                SkillName = SkillName(action.Skill),
                InstructionType = action.Skill.instructionType,
                WonCounter = false,
                WasCountered = false
            });
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);

            yield return ResolveSkillEffectCoroutine(action);
            TryFinishBattle();
        }
    }

    private IEnumerator ResolveSkillEffectCoroutine(BattleAction action)
    {
        if (action.Skill.baseHealCPAmount > 0)
        {
            int restored = GainCP(action.Actor, action.Skill.baseHealCPAmount);
            if (restored > 0)
            {
                EmitLog($"{action.Actor.Name} restores {restored} CP.");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }
        }

        yield return ApplyBaseStatusCoroutine(action);

        if (action.Skill.damageType != DamageType.None && action.Target.CurrentBattery > 0)
        {
            int damage = CombatResolver.ResolveDamage(
                action.Actor.Instance,
                action.Target.Instance,
                EffectiveStats(action.Actor),
                EffectiveStats(action.Target),
                action.Skill,
                action.DefenderInstructionType,
                action.WonCounter,
                RunAdjustedDamageMultiplier(action),
                action.BasePowerBonus,
                PresentationIdFor(action.Actor),
                PresentationIdFor(action.Target));

            int previousBattery = action.Target.CurrentBattery;
            action.Target.CurrentBattery = Mathf.Max(0, action.Target.CurrentBattery - damage);
            if (damage > 0)
                action.Target.StatusText = "Hit";
            EmitLog($"{action.Target.Name} takes {damage} damage.");
            RefreshHud();
            float damagePause = DirectDamagePauseSeconds(action);
            if (damagePause > 0f)
                yield return new WaitForSeconds(damagePause);

            if (damage > 0)
            {
                yield return ApplyDamageTakenTriggersCoroutine(action.Target, action.Actor, previousBattery, true);
                TryFinishBattle();
                if (phase != BattlePhase.Resolving)
                    yield break;
                if (action.Target.CurrentBattery <= 0)
                    yield break;
            }

            if (damage > 0 && action.Skill.onHitDrainOpponentCP > 0)
            {
                int drained = DrainCP(action.Target, action.Actor, action.Skill.onHitDrainOpponentCP);
                if (drained > 0)
                {
                    EmitLog($"{action.Actor.Name} drains {drained} CP on hit.");
                    RefreshHud();
                    if (logLineDelay > 0f)
                        yield return new WaitForSeconds(logLineDelay);
                }
            }

            if (damage > 0 && action.Skill.onHitShredOpponentFirewall > 0f)
            {
                action.Target.Statuses.ApplyFirewallShred(
                    action.Skill.onHitShredOpponentFirewall,
                    action.Skill.onHitFirewallShredDurationType,
                    action.Skill.onHitFirewallShredDuration,
                    currentRound);

                EmitLog($"{action.Target.Name}'s Firewall is shredded by {Mathf.RoundToInt(action.Skill.onHitShredOpponentFirewall * 100f)}% on hit.");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }

            if (damage > 0)
            {
                yield return ApplyStatusCoroutine(
                    action.Actor,
                    action.Target,
                    action.Skill.onHitApplyToOpponent,
                    action.Skill.onHitOpponentStatusStacks,
                    action.Skill.onHitOpponentStatusDurationType,
                    action.Skill.onHitOpponentStatusDuration);
            }
        }
        else
        {
            yield return NarrateNonDamageSkill(action);
        }
    }

    /// <summary>
    /// Emits narration for a Status / Defense skill after its runtime status
    /// effects have already been applied.
    /// </summary>
    private IEnumerator NarrateNonDamageSkill(BattleAction action)
    {
        SkillData skill = action.Skill;

        if (skill.instructionType == InstructionType.Defense)
        {
            EmitLog($"{action.Actor.Name} braces with {SkillName(skill)}.");
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }

        if (skill.instructionType == InstructionType.Status)
            EmitLog($"{action.Actor.Name} runs {SkillName(skill)}.");
    }

    private float DirectDamagePauseSeconds(BattleAction action)
    {
        float pause = damageLineDelay;
        if (presentation != null && action != null && action.Actor != null && action.Target != null)
        {
            pause = Mathf.Max(
                pause,
                presentation.ExpectedDamageFeedbackRemaining(
                    PresentationIdFor(action.Actor),
                    PresentationIdFor(action.Target)));
        }

        return pause;
    }

    private IEnumerator ApplyBaseStatusCoroutine(BattleAction action)
    {
        SkillData skill = action.Skill;
        if (skill.baseStatusStacks <= 0)
            yield break;

        BattleUnit target = skill.baseStatusTarget == StatusTarget.Self
            ? action.Actor
            : action.Target;

        yield return ApplyStatusCoroutine(
            action.Actor,
            target,
            skill.baseStatus,
            skill.baseStatusStacks,
            skill.baseStatusDurationType,
            skill.baseStatusDuration);
    }

    private IEnumerator ApplyStatusCoroutine(
        BattleUnit source,
        BattleUnit target,
        StatusType status,
        int stacks,
        StatusDurationType durationType,
        int duration)
    {
        if (source == null || target == null || stacks <= 0)
            yield break;

        int before = target.Statuses.GetStacks(status);
        BattleStatusSet.StatusApplyResult result = target.Statuses.ApplyStatus(
            status,
            stacks,
            durationType,
            duration,
            currentRound,
            source.Instance);

        int after = result.FinalStacks;
        if (result.AddedStacks <= 0 && after == before)
            yield break;

        EventBus.Publish(new StatusAppliedEvent
        {
            SourceId = PresentationIdFor(source),
            TargetId = PresentationIdFor(target),
            Status = status,
            Stacks = result.AddedStacks,
            DurationType = result.DurationType,
            Duration = result.Duration
        });

        AudioManager.Instance?.PlayStatusSfx(IsBuffStatus(status));

        string stackPart = after == 1 ? "1 stack" : $"{after} stacks";
        EmitLog($"{target.Name} gains {status} ({stackPart}).");
        target.StatusText = status.ToString();
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);
    }

    private IEnumerator ResolveEndOfRoundStatusesCoroutine()
    {
        if (player != null && player.CurrentBattery > 0)
            yield return TickUnitStatusesCoroutine(player);
        TryFinishBattle();
        if (phase != BattlePhase.Resolving)
            yield break;

        if (enemy != null && enemy.CurrentBattery > 0)
            yield return TickUnitStatusesCoroutine(enemy);
        TryFinishBattle();
        if (phase != BattlePhase.Resolving)
            yield break;

        TickDurations(player);
        TickDurations(enemy);
        RefreshHud();
    }

    private IEnumerator ApplyDamageTakenTriggersCoroutine(
        BattleUnit damaged,
        BattleUnit opponent,
        int previousBattery,
        bool directDamage)
    {
        if (damaged == null || opponent == null || previousBattery <= damaged.CurrentBattery)
            yield break;

        if (directDamage)
            yield return ApplySubroutineTriggerCoroutine(damaged, opponent, SubroutineTrigger.OnDamageTaken);

        TryFinishBattle();
        if (phase != BattlePhase.Resolving)
            yield break;

        yield return ApplyLowBatterySubroutineCoroutine(damaged, opponent, previousBattery);
    }

    private IEnumerator ApplyLowBatterySubroutineCoroutine(
        BattleUnit owner,
        BattleUnit opponent,
        int previousBattery)
    {
        if (owner == null ||
            opponent == null ||
            owner.LowBatterySubroutineTriggered ||
            owner.CurrentBattery <= 0 ||
            previousBattery <= owner.CurrentBattery ||
            !HasSubroutineTrigger(owner, SubroutineTrigger.OnLowBattery))
        {
            yield break;
        }

        int threshold = LowBatteryThreshold(owner);
        if (previousBattery <= threshold || owner.CurrentBattery > threshold)
            yield break;

        owner.LowBatterySubroutineTriggered = true;
        yield return ApplySubroutineTriggerCoroutine(owner, opponent, SubroutineTrigger.OnLowBattery);
    }

    private IEnumerator TickUnitStatusesCoroutine(BattleUnit unit)
    {
        int burnStacks = unit.Statuses.GetStacks(StatusType.Burn);
        if (burnStacks > 0)
        {
            int previousBattery = unit.CurrentBattery;
            int damage = Mathf.Max(1, Mathf.RoundToInt(unit.MaxBattery * unit.Statuses.BurnDamagePerLayer * burnStacks));
            int actualDamage = DealStatusDamage(unit, damage);
            int nextStacks = burnStacks / 2;
            unit.Statuses.SetStacks(StatusType.Burn, nextStacks);

            EmitLog($"{unit.Name} takes {actualDamage} Burn damage ({burnStacks} -> {nextStacks} stacks).");
            unit.StatusText = "Burn";
            RefreshHud();
            if (damageLineDelay > 0f)
                yield return new WaitForSeconds(damageLineDelay);

            if (actualDamage > 0)
            {
                yield return ApplyDamageTakenTriggersCoroutine(unit, OpponentFor(unit), previousBattery, false);
                TryFinishBattle();
                if (phase != BattlePhase.Resolving)
                    yield break;
            }
        }

        int leechStacks = unit.Statuses.GetStacks(StatusType.Leech);
        if (unit.CurrentBattery > 0 && leechStacks > 0)
        {
            int previousBattery = unit.CurrentBattery;
            int damage = Mathf.Max(1, Mathf.RoundToInt(unit.MaxBattery * unit.Statuses.LeechDamagePerLayer * leechStacks));
            int actualDamage = DealStatusDamage(unit, damage);
            BattleUnit source = UnitFor(unit.Statuses.GetSource(StatusType.Leech));
            int restored = source != null ? HealBattery(source, actualDamage) : 0;

            if (source != null && restored > 0)
                EmitLog($"{unit.Name} loses {actualDamage} Battery to Leech; {source.Name} restores {restored}.");
            else
                EmitLog($"{unit.Name} loses {actualDamage} Battery to Leech.");

            unit.StatusText = "Leech";
            RefreshHud();
            if (damageLineDelay > 0f)
                yield return new WaitForSeconds(damageLineDelay);

            if (actualDamage > 0)
            {
                yield return ApplyDamageTakenTriggersCoroutine(unit, OpponentFor(unit), previousBattery, false);
                TryFinishBattle();
                if (phase != BattlePhase.Resolving)
                    yield break;
            }
        }
    }

    private static bool HasSubroutineTrigger(BattleUnit unit, SubroutineTrigger trigger)
    {
        return unit?.Instance?.data?.subroutine != null &&
               unit.Instance.data.subroutine.trigger == trigger;
    }

    private static int LowBatteryThreshold(BattleUnit unit)
    {
        return Mathf.Max(1, Mathf.CeilToInt(unit.MaxBattery * 0.25f));
    }

    private void TickDurations(BattleUnit unit)
    {
        if (unit == null)
            return;

        List<string> expired = unit.Statuses.TickDurations(currentRound);
        for (int i = 0; i < expired.Count; i++)
            EmitLog($"{unit.Name}'s {expired[i]} expires.");
    }

    private BattleStats EffectiveStats(BattleUnit unit)
    {
        BattleStats stats = unit.Statuses.ApplyToStats(BattleStats.From(unit.Instance));
        if (IsPlayerUnit(unit))
            stats.ClockSpeed = BattleStats.ApplyPercent(stats.ClockSpeed, PlayerRunClockSpeedMultiplier());
        return stats;
    }

    private int EffectiveSkillCost(BattleUnit unit, SkillData skill)
    {
        if (unit == null || skill == null)
            return 0;
        int reducedBaseCost = Mathf.Max(0, skill.cpCost - unit.CostReductionFor(skill, currentRound));
        if (IsPlayerUnit(unit))
            reducedBaseCost = Mathf.Max(0, reducedBaseCost - PlayerRunSkillCostReduction());
        if (IsRechargeSkill(skill))
            return reducedBaseCost;
        return unit.Statuses.EffectiveSkillCost(reducedBaseCost, currentRound);
    }

    private static bool IsRechargeSkill(SkillData skill)
    {
        return skill != null &&
               skill.isUniversal &&
               skill.cpCost <= 0 &&
               skill.baseHealCPAmount > 0;
    }

    private void EmitRunBuffSummary()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.currentRunBuffs == null || manager.currentRunBuffs.Count == 0)
            return;

        EmitLog("RUN BUFFS:");
        EmitLog(manager.CurrentRunBuffSummary());
    }

    private float RunAdjustedDamageMultiplier(BattleAction action)
    {
        if (action == null)
            return 1f;

        float multiplier = action.FinalDamageMultiplier;
        GameManager manager = GameManager.Instance;
        if (manager == null)
            return multiplier;

        if (IsPlayerUnit(action.Actor))
            multiplier *= manager.PlayerRunOutgoingDamageMultiplier;
        if (IsPlayerUnit(action.Target))
            multiplier *= manager.PlayerRunIncomingDamageMultiplier;

        return multiplier;
    }

    private int PlayerRunSkillCostReduction()
    {
        GameManager manager = GameManager.Instance;
        return manager != null ? manager.PlayerRunSkillCostReduction : 0;
    }

    private float PlayerRunClockSpeedMultiplier()
    {
        GameManager manager = GameManager.Instance;
        return manager != null ? manager.PlayerRunClockSpeedMultiplier : 1f;
    }

    private bool IsPlayerUnit(BattleUnit unit)
    {
        return unit != null && playerParty.Contains(unit);
    }

    private bool CanPay(BattleUnit unit, SkillData skill)
    {
        if (unit == null || skill == null)
            return false;
        return unit.CurrentCP >= EffectiveSkillCost(unit, skill);
    }

    private bool CanUseSkill(BattleUnit unit, SkillData skill)
    {
        return unit != null &&
               skill != null &&
               !IsDefenseOnCooldown(unit, skill) &&
               CanPay(unit, skill);
    }

    private bool IsDefenseOnCooldown(BattleUnit unit, SkillData skill)
    {
        if (unit == null || skill == null || skill.instructionType != InstructionType.Defense)
            return false;
        return unit.LastDefenseRound == currentRound - 1;
    }

    private bool SpendCP(BattleUnit unit, int amount)
    {
        if (amount <= 0)
            return true;
        if (unit.CurrentCP < amount)
            return false;

        unit.CurrentCP -= amount;
        EventBus.Publish(new BattleFeedbackEvent
        {
            TargetId = PresentationIdFor(unit),
            Type = BattleFeedbackType.CPDrain,
            Amount = amount,
            Label = $"-{amount} CP"
        });
        return true;
    }

    private int GainCP(BattleUnit unit, int amount)
    {
        int before = unit.CurrentCP;
        unit.CurrentCP = Mathf.Clamp(unit.CurrentCP + Mathf.Max(0, amount), 0, MaxCP);
        int restored = unit.CurrentCP - before;
        if (restored > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = PresentationIdFor(unit),
                Type = BattleFeedbackType.CPGain,
                Amount = restored,
                Label = $"+{restored} CP"
            });
            AudioManager.Instance?.PlayStatusSfx(true); // charge = positive
        }
        return restored;
    }

    private int DrainCP(BattleUnit from, BattleUnit to, int amount)
    {
        int drained = Mathf.Min(from.CurrentCP, Mathf.Max(0, amount));
        from.CurrentCP -= drained;
        to.CurrentCP = Mathf.Clamp(to.CurrentCP + drained, 0, MaxCP);
        if (drained > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = PresentationIdFor(from),
                Type = BattleFeedbackType.CPDrain,
                Amount = drained,
                Label = $"-{drained} CP"
            });
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = PresentationIdFor(to),
                Type = BattleFeedbackType.CPGain,
                Amount = drained,
                Label = $"+{drained} CP"
            });
        }
        return drained;
    }

    private int DealStatusDamage(BattleUnit unit, int amount)
    {
        int actual = Mathf.Min(unit.CurrentBattery, Mathf.Max(0, amount));
        unit.CurrentBattery = Mathf.Max(0, unit.CurrentBattery - actual);
        if (actual > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = PresentationIdFor(unit),
                Type = BattleFeedbackType.Damage,
                Amount = actual,
                Label = $"-{actual}"
            });
        }
        return actual;
    }

    private int HealBattery(BattleUnit unit, int amount)
    {
        int before = unit.CurrentBattery;
        unit.CurrentBattery = Mathf.Clamp(unit.CurrentBattery + Mathf.Max(0, amount), 0, unit.MaxBattery);
        int restored = unit.CurrentBattery - before;
        if (restored > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = PresentationIdFor(unit),
                Type = BattleFeedbackType.Heal,
                Amount = restored,
                Label = $"+{restored}"
            });
            AudioManager.Instance?.PlayStatusSfx(true); // heal = positive
        }
        return restored;
    }

    // Positive (buff) statuses use the charge/heal cue; everything else (Burn,
    // Freeze, Leech, Ensnare, Throttle, Corrupted) uses the debuff cue.
    private static bool IsBuffStatus(StatusType status)
    {
        switch (status)
        {
            case StatusType.ComputingUp:
            case StatusType.ThroughputUp:
            case StatusType.FirewallUp:
            case StatusType.EncryptionUp:
            case StatusType.Concurrent:
            case StatusType.BufferLoad:
            case StatusType.Overclock:
                return true;
            default:
                return false;
        }
    }

    private BattleUnit UnitFor(AlgoMonInstance instance)
    {
        if (instance == null)
            return null;
        if (player != null && ReferenceEquals(player.Instance, instance))
            return player;
        if (enemy != null && ReferenceEquals(enemy.Instance, instance))
            return enemy;
        return null;
    }

    private BattleUnit OpponentFor(BattleUnit unit)
    {
        if (unit == null)
            return null;
        if (ReferenceEquals(unit, player))
            return enemy;
        if (ReferenceEquals(unit, enemy))
            return player;
        return null;
    }

    private bool IsHackerBattle()
    {
        return CurrentEncounterNodeType() == NodeType.Hacker;
    }

    private string EnemyTrainerLabel()
    {
        return IsHackerBattle() ? "Hacker" : "Enemy";
    }

    private static NodeType CurrentEncounterNodeType()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.currentRunGraph == null || string.IsNullOrEmpty(manager.currentNodeId))
            return NodeType.Combat;

        GridNode node = manager.currentRunGraph.GetNode(manager.currentNodeId);
        return node != null ? node.nodeType : NodeType.Combat;
    }

    private void TryFinishBattle()
    {
        if (phase == BattlePhase.BattleOver || AwaitingForcedPlayerSwitch)
            return;

        if (enemy.CurrentBattery <= 0)
        {
            enemy.StatusText = "Offline";
            PublishFaint(enemy);
            if (TrySendNextEnemy())
                return;

            FinishBattle(true, $"{enemy.Name} is offline. Victory.");
            return;
        }

        if (player.CurrentBattery <= 0)
        {
            player.StatusText = "Offline";
            PublishFaint(player);
            if (TryPromptPlayerForcedSwitch())
                return;

            FinishBattle(false, "All party AlgoMons are offline. Defeat.");
        }
    }

    private bool TryPromptPlayerForcedSwitch()
    {
        if (!HasAvailableReserve(playerParty, player))
            return false;

        forcePlayerSwitchTarget = true;
        selectingSwitchTarget = true;
        phase = BattlePhase.WaitingForPlayer;
        turnQueue.Clear();
        activeActionAnnouncementLine = null;

        EmitLog($"{player.Name} is offline.");
        EmitLog("Choose a reserve AlgoMon.");
        SetDetail("Switch Required", $"{player.Name} is offline. Choose a reserve AlgoMon.");
        Announce("Switch Required", "Choose a reserve AlgoMon.");
        RefreshHud();
        return true;
    }

    private bool TrySendNextEnemy()
    {
        BattleUnit previous = enemy;
        BattleUnit next = FirstAvailableReserve(enemyParty, previous);
        if (next == null)
            return false;

        next.StatusText = "Ready";
        enemy = next;
        RegisterPresentationCombatant(false);

        EmitLog($"{previous.Name} is offline.");
        EmitLog($"{EnemyTrainerLabel()} sends out {next.Name}.");
        Announce($"{EnemyTrainerLabel()} Switch", $"{next.Name} enters the battle.");
        RefreshHud();
        return true;
    }

    private static BattleUnit FirstAvailableReserve(List<BattleUnit> party, BattleUnit activeUnit)
    {
        if (party == null)
            return null;

        for (int i = 0; i < party.Count; i++)
        {
            BattleUnit unit = party[i];
            if (unit != null &&
                !ReferenceEquals(unit, activeUnit) &&
                unit.CurrentBattery > 0)
            {
                return unit;
            }
        }

        return null;
    }

    private void PublishFaint(BattleUnit unit)
    {
        if (unit == null)
            return;

        if (!faintPublishedUnits.Add(unit))
            return;

        EventBus.Publish(new UnitFaintedEvent { UnitId = PresentationIdFor(unit) });
    }

    private void FinishBattle(bool playerWon, string message)
    {
        if (battleEndPublished || waitingForPostBattleContinue || finishingBattle)
            return;

        phase = BattlePhase.BattleOver;
        EmitLog(message);

        EncounterReward reward = null;
        if (playerWon)
            reward = TryGrantDefeatedEnemyReward();

        RefreshHud();

        // The losing AlgoMon's faint animation was just published; hold the result
        // (victory panel or defeat hand-off) until that KO animation has played out
        // instead of snapping the panel up the instant Battery hits zero.
        finishingBattle = true;
        StartCoroutine(ShowBattleResultAfterFaint(playerWon, message, reward));
    }

    private IEnumerator ShowBattleResultAfterFaint(bool playerWon, string message, EncounterReward reward)
    {
        float wait = postFaintResultDelay;
        if (presentation != null)
        {
            string faintedId = playerWon ? EnemyPresentationId : PlayerPresentationId;
            wait += presentation.ExpectedFaintRemaining(faintedId);
        }

        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        if (playerWon)
        {
            waitingForPostBattleContinue = true;
            pendingBattleEndPlayerWon = true;
            ShowPostBattleRewardSummary(message, reward);
        }
        else
        {
            PublishBattleEnd(playerWon);
        }
    }

    private void HandlePostBattleContinueClicked()
    {
        if (!waitingForPostBattleContinue)
            return;

        waitingForPostBattleContinue = false;
        if (hud != null)
            hud.HidePostBattlePanel();

        PublishBattleEnd(pendingBattleEndPlayerWon);
    }

    private void PublishBattleEnd(bool playerWon)
    {
        if (battleEndPublished)
            return;

        EventBus.Publish(new BattleEndEvent { PlayerWon = playerWon });
        battleEndPublished = true;
    }

    private EncounterReward TryGrantDefeatedEnemyReward()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || enemy == null)
            return null;

        EncounterReward reward = manager.GrantCurrentEncounterReward(enemy.Instance);
        if (reward != null && reward.HasAnyGrant)
        {
            EmitLog(reward.ToBattleLogLine());
            return reward;
        }

        EmitLog("REWARD SKIPPED: encounter data is not rewardable.");
        return reward;
    }

    private void ShowPostBattleRewardSummary(string resultLine, EncounterReward reward)
    {
        string body = BuildPostBattleRewardBody(resultLine, reward);
        SetDetail("Node Cleared", body);
        Announce("Rewards", "Rewards uploaded.");

        if (hud != null)
            hud.ShowPostBattlePanel("Node Cleared", body, "Terminal");
    }

    private static string BuildPostBattleRewardBody(string resultLine, EncounterReward reward)
    {
        var builder = new StringBuilder();

        if (reward == null || !reward.HasAnyGrant)
        {
            builder.AppendLine("NO REWARDS");
        }
        else
        {
            builder.AppendLine($"{reward.sourceNodeType.ToString().ToUpperInvariant()}  T{reward.threatTier}  LV {reward.encounterLevel}");
            builder.AppendLine($"EXP +{reward.algoMonExp}    CREDITS +{reward.compute}");

            if (reward.baseDataGranted)
                builder.AppendLine($"FORM DATA +1  {EncounterReward.FormatQuality(reward.baseDataQuality)}");
            else if (reward.shouldGrantBaseData)
                builder.AppendLine("FORM DATA --");

            if (reward.evolutionDataGranted)
                builder.AppendLine("EVOLUTION DATA +1");
            else if (reward.shouldGrantEvolutionData)
                builder.AppendLine("EVOLUTION DATA --");

            if (!string.IsNullOrWhiteSpace(reward.speciesCodeName))
                builder.AppendLine(reward.speciesCodeName.ToUpperInvariant());
        }

        return builder.ToString();
    }

    private void RefreshHud()
    {
        if (hud == null || player == null || enemy == null)
            return;

        // Hide the skill bar / action buttons / top bar while a round resolves so
        // the attack animations play unobstructed; status cards stay visible.
        hud.SetActionUiHidden(phase != BattlePhase.WaitingForPlayer);

        hud.SetRound(currentRound);
        hud.SetBattleState(PhaseLabel());
        hud.SetRoundSandclockActive(phase == BattlePhase.WaitingForPlayer);

        hud.SetCombatant(BattleHudController.Side.Player, player.Name, player.DisplayLevel);
        hud.SetCombatantElement(BattleHudController.Side.Player, UnitElementType(player));
        hud.SetBattery(BattleHudController.Side.Player, player.CurrentBattery, player.MaxBattery);
        hud.SetCP(BattleHudController.Side.Player, player.CurrentCP, MaxCP);
        hud.SetStatusChips(BattleHudController.Side.Player, BuildStatusChips(player));

        hud.SetCombatant(BattleHudController.Side.Enemy, enemy.Name, enemy.DisplayLevel);
        hud.SetCombatantElement(BattleHudController.Side.Enemy, UnitElementType(enemy));
        hud.SetBattery(BattleHudController.Side.Enemy, enemy.CurrentBattery, enemy.MaxBattery);
        hud.SetCP(BattleHudController.Side.Enemy, enemy.CurrentCP, MaxCP);
        hud.SetStatusChips(BattleHudController.Side.Enemy, BuildStatusChips(enemy));

        bool canAct = phase == BattlePhase.WaitingForPlayer;
        // Skill slots preview their element matchup against the active enemy;
        // runs every refresh so enemy switches re-evaluate the chips.
        hud.SetOpposingElement(UnitElementType(enemy));
        if (selectingSwitchTarget && canAct)
            RenderSwitchSlots();
        else
            RenderSkillSlots(canAct);

        bool switchRequired = forcePlayerSwitchTarget && selectingSwitchTarget;
        bool canSwitch = canAct && (switchRequired
            ? HasAvailableReserve(playerParty, player)
            : HasSwitchTarget(playerParty, player));
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Recharge, canAct && !switchRequired);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Switch, canSwitch && !switchRequired);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Flee, canAct && !switchRequired);

        hud.SetActionHover(BattleHudController.ActionButton.Recharge, "Recharge", "+5 CP\nSpend the turn to restore CP.");
        hud.SetActionHover(
            BattleHudController.ActionButton.Switch,
            switchRequired ? "Switch Required" : selectingSwitchTarget ? "Cancel Switch" : "Switch",
            switchRequired
                ? "Choose a reserve AlgoMon to continue."
                : selectingSwitchTarget
                ? "Return to skill selection."
                : canSwitch
                    ? "Choose a reserve AlgoMon. Switching resolves before every skill."
                    : "No reserve AlgoMon is ready.");
        hud.SetActionHover(BattleHudController.ActionButton.Flee, "Flee", "End this battle immediately.");
    }

    private void RenderSkillSlots(bool canAct)
    {
        for (int i = 0; i < MaxSkillSlots; i++)
        {
            SkillData skill = player.GetSkill(i);
            if (skill == null)
            {
                hud.ClearSkillSlot(i);
                continue;
            }

            hud.SetSkillSlot(i, skill);
            hud.SetSkillHover(i, SkillName(skill), BuildSkillHover(player, skill));
            hud.SetSkillCPCost(i, EffectiveSkillCost(player, skill));
            hud.SetSkillSlotAvailable(i, canAct && CanUseSkill(player, skill));
        }
    }

    private void RenderSwitchSlots()
    {
        for (int i = 0; i < MaxSkillSlots; i++)
        {
            if (playerParty == null || i >= playerParty.Count || playerParty[i] == null)
            {
                hud.ClearSkillSlot(i);
                continue;
            }

            BattleUnit unit = playerParty[i];
            bool isActive = ReferenceEquals(unit, player);
            bool available = (forcePlayerSwitchTarget || CanSwitchOut(player)) && !isActive && unit.CurrentBattery > 0;
            string state = isActive
                ? "ACTIVE"
                : unit.CurrentBattery <= 0
                    ? "OFFLINE"
                    : "READY";
            ElementType elementType = unit.Instance != null && unit.Instance.data != null
                ? unit.Instance.data.elementType
                : unit.Config.elementType;

            hud.SetSwitchSlot(
                i,
                unit.Name,
                elementType,
                unit.DisplayLevel,
                unit.CurrentBattery,
                unit.MaxBattery,
                unit.CurrentCP,
                MaxCP,
                state,
                FormatUnitStatus(unit),
                SwitchPortraitFor(unit),
                available);
        }
    }

    private static ElementType UnitElementType(BattleUnit unit)
    {
        if (unit == null)
            return ElementType.Normal;
        return unit.Instance != null && unit.Instance.data != null
            ? unit.Instance.data.elementType
            : unit.Config.elementType;
    }

    private static Sprite SwitchPortraitFor(BattleUnit unit)
    {
        AlgoMonInstance instance = unit != null ? unit.Instance : null;
        AlgoMonData data = instance != null ? instance.data : null;
        if (data == null)
            return null;

        string cacheKey = $"{data.codeName}|{instance.battleFormName}";
        if (switchPortraitSpriteCache.TryGetValue(cacheKey, out Sprite cachedSprite))
            return cachedSprite;

        if (data.portrait != null)
        {
            switchPortraitSpriteCache[cacheKey] = data.portrait;
            return data.portrait;
        }

        BattleAnimationProfile profile = BattleAnimationProfileLoader.TryLoadProfile(data.codeName, instance.battleFormName);
        if (profile == null)
            profile = data.battleAnimationProfile;

        Sprite sprite = FirstSprite(profile != null ? profile.idle : null);
        if (sprite != null)
        {
            switchPortraitSpriteCache[cacheKey] = sprite;
            return sprite;
        }
        sprite = FirstSprite(profile != null ? profile.entry : null);
        if (sprite != null)
        {
            switchPortraitSpriteCache[cacheKey] = sprite;
            return sprite;
        }
        sprite = FirstSprite(profile != null ? profile.status : null);
        if (sprite != null)
        {
            switchPortraitSpriteCache[cacheKey] = sprite;
            return sprite;
        }

        sprite = FirstSprite(profile != null ? profile.attack : null);
        switchPortraitSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private static Sprite FirstSprite(BattleAnimationClipData clip)
    {
        if (clip == null || clip.frames == null)
            return null;

        for (int i = 0; i < clip.frames.Length; i++)
        {
            if (clip.frames[i] != null)
                return clip.frames[i];
        }

        return null;
    }

    private string PhaseLabel()
    {
        switch (phase)
        {
            case BattlePhase.WaitingForPlayer:
                if (forcePlayerSwitchTarget)
                    return "Choose replacement";
                return "Player turn";
            case BattlePhase.Resolving:
                return "Resolving";
            case BattlePhase.BattleOver:
                if (enemy != null && enemy.CurrentBattery <= 0)
                    return "Victory";
                if (player != null && player.CurrentBattery <= 0)
                    return "Defeat";
                return "Battle over";
            default:
                return string.Empty;
        }
    }

    private string BuildSkillHover(BattleUnit unit, SkillData skill)
    {
        var line = new StringBuilder();
        int cost = EffectiveSkillCost(unit, skill);
        line.Append($"{skill.instructionType} | {skill.elementType} | CP {cost}");
        if (cost != Mathf.Max(0, skill.cpCost))
            line.Append($" (base {Mathf.Max(0, skill.cpCost)})");
        if (unit != null && unit.Statuses.SkillRepeatCount(currentRound) > 1)
            line.Append(cost > 0 ? $" | Concurrent: +{cost} CP for repeat" : " | Concurrent: free repeat");

        if (skill.basePower > 0)
            line.Append($" | BP {skill.basePower}");
        if (skill.canCounter)
            line.Append(" | Counter-ready");

        string counterSummary = SkillDetailTextFormatter.BuildCounterSummary(skill);
        string body = SkillDetailTextFormatter.BuildReadableDescription(skill);

        if (IsDefenseOnCooldown(unit, skill))
            return BuildSkillHoverBody(line.ToString(), "Defense is cooling down this round.", counterSummary, body);

        if (unit.CurrentCP < cost)
        {
            int missing = cost - unit.CurrentCP;
            return BuildSkillHoverBody(line.ToString(), $"Needs {missing} more CP.", counterSummary, body);
        }

        return BuildSkillHoverBody(line.ToString(), counterSummary, body);
    }

    private static string BuildSkillHoverBody(string metaLine, params string[] sections)
    {
        return SkillDetailTextFormatter.BuildBody(metaLine, sections);
    }

    private static string SkillName(SkillData skill)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.skillName))
            return "Skill";
        return skill.skillName.Trim();
    }

    private static string SubroutineName(SubroutineData subroutine)
    {
        if (subroutine == null || string.IsNullOrWhiteSpace(subroutine.subroutineName))
            return "Subroutine";
        return subroutine.subroutineName.Trim();
    }

    private static string FormatSigned(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    private static string FormatUnitStatus(BattleUnit unit)
    {
        string summary = unit.Statuses.BuildSummary();
        if (string.IsNullOrEmpty(summary))
            return unit.StatusText;
        if (unit.StatusText == "Ready")
            return summary;
        return $"{unit.StatusText} | {summary}";
    }

    /// <summary>
    /// Structured version of FormatUnitStatus for the HUD's chip row: the
    /// transient state first (READY / HIT / ...), then one chip per active
    /// status with its stacks, then the timed modifiers BuildSummary prints.
    /// </summary>
    private static List<BattleHudController.StatusChip> BuildStatusChips(BattleUnit unit)
    {
        var chips = new List<BattleHudController.StatusChip>(8);
        if (unit == null)
            return chips;

        BattleStatusSet statuses = unit.Statuses;

        // The transient state chip leads, but if the state names a stacking
        // status that also gets its own count chip below (e.g. "Leech" while 3
        // stacks are active), skip it so the row doesn't show "LCH" + "LCH 3".
        bool stateIsTrackedStack =
            System.Enum.TryParse(unit.StatusText, out StatusType stateStatus) &&
            statuses.GetStacks(stateStatus) > 0;
        if (!stateIsTrackedStack)
            chips.Add(StateChip(unit.StatusText));

        AddStackChip(chips, statuses, StatusType.Burn, "BRN", BattleHudController.StatusChipTone.Harm, false);
        AddStackChip(chips, statuses, StatusType.Freeze, "FRZ", BattleHudController.StatusChipTone.Harm, false);
        AddStackChip(chips, statuses, StatusType.Leech, "LCH", BattleHudController.StatusChipTone.Harm, false);
        AddStackChip(chips, statuses, StatusType.Ensnare, "SNR", BattleHudController.StatusChipTone.Harm, false);
        AddStackChip(chips, statuses, StatusType.Concurrent, "X2", BattleHudController.StatusChipTone.Buff, false);
        AddStackChip(chips, statuses, StatusType.BufferLoad, "CP -4", BattleHudController.StatusChipTone.Buff, false);
        AddStackChip(chips, statuses, StatusType.ComputingUp, "CPU", BattleHudController.StatusChipTone.Buff, true);
        AddStackChip(chips, statuses, StatusType.ThroughputUp, "TP", BattleHudController.StatusChipTone.Buff, true);
        AddStackChip(chips, statuses, StatusType.FirewallUp, "FW", BattleHudController.StatusChipTone.Buff, true);
        AddStackChip(chips, statuses, StatusType.EncryptionUp, "ENC", BattleHudController.StatusChipTone.Buff, true);
        AddStackChip(chips, statuses, StatusType.Overclock, "PRI", BattleHudController.StatusChipTone.Buff, true);

        if (statuses.CPDiscountAmount > 0)
            chips.Add(new BattleHudController.StatusChip(
                $"CP -{statuses.CPDiscountAmount}", BattleHudController.StatusChipTone.Buff));
        if (statuses.FirewallShredAmount > 0f)
            chips.Add(new BattleHudController.StatusChip(
                $"FW -{Mathf.RoundToInt(statuses.FirewallShredAmount * 100f)}%", BattleHudController.StatusChipTone.Harm));
        if (statuses.NextPriorityBonusAmount != 0)
            chips.Add(SignedChip("PRI", statuses.NextPriorityBonusAmount));
        if (statuses.NextBasePowerBonusAmount != 0)
            chips.Add(SignedChip("PWR", statuses.NextBasePowerBonusAmount));

        return chips;
    }

    private static void AddStackChip(
        List<BattleHudController.StatusChip> chips,
        BattleStatusSet statuses,
        StatusType status,
        string label,
        BattleHudController.StatusChipTone tone,
        bool showSignedStacks)
    {
        int stacks = statuses.GetStacks(status);
        if (stacks <= 0)
            return;

        string text = showSignedStacks
            ? $"{label} +{stacks}"
            : stacks > 1 ? $"{label} {stacks}" : label;
        chips.Add(new BattleHudController.StatusChip(text, tone));
    }

    private static BattleHudController.StatusChip SignedChip(string label, int amount)
    {
        string text = amount > 0 ? $"{label} +{amount}" : $"{label} {amount}";
        BattleHudController.StatusChipTone tone = amount > 0
            ? BattleHudController.StatusChipTone.Buff
            : BattleHudController.StatusChipTone.Harm;
        return new BattleHudController.StatusChip(text, tone);
    }

    /// <summary>
    /// Maps the transient BattleUnit.StatusText states to a short chip.
    /// Unknown values (e.g. a freshly applied StatusType name) fall back to a
    /// gray informational chip with the raw text uppercased.
    /// </summary>
    private static BattleHudController.StatusChip StateChip(string state)
    {
        string s = string.IsNullOrWhiteSpace(state) ? "Ready" : state.Trim();
        switch (s)
        {
            case "Ready":        return new BattleHudController.StatusChip("READY", BattleHudController.StatusChipTone.Ready);
            case "Counter":      return new BattleHudController.StatusChip("COUNTER", BattleHudController.StatusChipTone.Ready);
            case "Hit":          return new BattleHudController.StatusChip("HIT", BattleHudController.StatusChipTone.Harm);
            case "Delayed":      return new BattleHudController.StatusChip("DELAYED", BattleHudController.StatusChipTone.Harm);
            case "Nullified":    return new BattleHudController.StatusChip("NULLED", BattleHudController.StatusChipTone.Harm);
            case "No CP":        return new BattleHudController.StatusChip("NO CP", BattleHudController.StatusChipTone.Harm);
            case "Offline":      return new BattleHudController.StatusChip("OFFLINE", BattleHudController.StatusChipTone.Harm);
            case "Burn":         return new BattleHudController.StatusChip("BRN", BattleHudController.StatusChipTone.Harm);
            case "Leech":        return new BattleHudController.StatusChip("LCH", BattleHudController.StatusChipTone.Harm);
            case "Freeze":       return new BattleHudController.StatusChip("FRZ", BattleHudController.StatusChipTone.Harm);
            case "Ensnare":      return new BattleHudController.StatusChip("SNR", BattleHudController.StatusChipTone.Harm);
            case "Switched in":  return new BattleHudController.StatusChip("SWAP IN", BattleHudController.StatusChipTone.Info);
            case "Benched":      return new BattleHudController.StatusChip("BENCHED", BattleHudController.StatusChipTone.Info);
            case "Concurrent":   return new BattleHudController.StatusChip("X2", BattleHudController.StatusChipTone.Buff);
            case "BufferLoad":   return new BattleHudController.StatusChip("CP -4", BattleHudController.StatusChipTone.Buff);
            case "ComputingUp":  return new BattleHudController.StatusChip("CPU UP", BattleHudController.StatusChipTone.Buff);
            case "ThroughputUp": return new BattleHudController.StatusChip("TP UP", BattleHudController.StatusChipTone.Buff);
            case "FirewallUp":   return new BattleHudController.StatusChip("FW UP", BattleHudController.StatusChipTone.Buff);
            case "EncryptionUp": return new BattleHudController.StatusChip("ENC UP", BattleHudController.StatusChipTone.Buff);
            default:             return new BattleHudController.StatusChip(s.ToUpperInvariant(), BattleHudController.StatusChipTone.Info);
        }
    }

    private void SetDetail(string title, string body)
    {
        if (hud != null)
            hud.SetSkillDetail(title, body);
    }

    private void Announce(string title, string body)
    {
        if (hud != null)
            hud.SetBattleAnnouncement(title, body);
    }

    /// <summary>
    /// Appends a single line to the rolling battle log and pushes the
    /// accumulated buffer to the Skill Details panel. Called from coroutines
    /// between waits so the player sees narration scroll in real time.
    /// </summary>
    private void EmitLog(string line)
    {
        if (string.IsNullOrEmpty(line))
            return;

        battleLogLines.Add(line);
        while (battleLogLines.Count > battleLogLineCount)
            battleLogLines.RemoveAt(0);

        if (hud != null)
        {
            hud.SetSkillDetail("Battle Log", string.Join("\n", battleLogLines));
            hud.SetBattleAnnouncement(AnnouncementTitleFor(line), AnnouncementBodyFor(line));
        }
    }

    private string AnnouncementBodyFor(string line)
    {
        if (IsActionStartAnnouncementLine(line))
        {
            activeActionAnnouncementLine = line;
            return line;
        }

        if (ShouldClearActionAnnouncement(line))
            activeActionAnnouncementLine = null;

        if (!string.IsNullOrEmpty(activeActionAnnouncementLine) && ShouldPairWithActiveAction(line))
            return $"{activeActionAnnouncementLine}\n{line}";

        return line;
    }

    private static string AnnouncementTitleFor(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return "Battle";
        if (line.StartsWith("-- Round", StringComparison.Ordinal))
            return "Round";
        if (line.IndexOf(" uses ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf(" repeats ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf(" recasts ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf(" commits ", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Action";
        if (line.IndexOf("takes", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("gains", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("restores", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("drains", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("blocks", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Effect";
        if (line.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("ASD check", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Counter";
        if (line.IndexOf("lacks", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("cannot repeat", StringComparison.OrdinalIgnoreCase) >= 0)
            return "No CP";
        if (line.IndexOf("Awaiting", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Choose", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Input";

        return "Battle";
    }

    private static bool IsActionStartAnnouncementLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return line.IndexOf(" uses ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf(" repeats ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf(" recasts ", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldPairWithActiveAction(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;
        if (IsActionStartAnnouncementLine(line) || ShouldClearActionAnnouncement(line))
            return false;

        return line.IndexOf("takes", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("gains", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("restores", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("drains", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("blocks", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("shredded", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("braces", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("runs", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("forced", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("clears", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("expires", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldClearActionAnnouncement(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        return line.StartsWith("-- Round", StringComparison.Ordinal) ||
            line.IndexOf(" commits ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Awaiting", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Choose", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Battle started", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("lacks", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("cannot repeat", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void DestroyTransientData()
    {
        for (int i = 0; i < transientData.Count; i++)
        {
            if (transientData[i] != null)
                DestroyTransientObject(transientData[i]);
        }
        transientData.Clear();
    }

    private static void DestroyTransientObject(ScriptableObject data)
    {
        if (Application.isPlaying)
            Destroy(data);
        else
            DestroyImmediate(data);
    }
}
