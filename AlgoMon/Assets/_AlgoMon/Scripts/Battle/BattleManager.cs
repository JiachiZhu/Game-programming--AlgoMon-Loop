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
/// Party switching, bag items, and ally-faint Subroutine hooks are intentionally
/// left for follow-up battle issues.
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    private const int MaxCP = 10;
    private const int MaxSkillSlots = 4;
    private const float CounterPriorityBase = 1000000f;
    private const int ForceLastPriorityPenalty = -10000;

    private enum BattlePhase
    {
        WaitingForPlayer,
        Resolving,
        BattleOver
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
        public BattleAction(BattleUnit actor, BattleUnit target, SkillData skill)
        {
            Actor = actor;
            Target = target;
            Skill = skill;
        }

        public BattleUnit Actor { get; }
        public BattleUnit Target { get; }
        public SkillData Skill { get; }
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
    [SerializeField, Min(0f)] private float damageLineDelay = 0.75f;
    [Tooltip("Pause before the round closes and player input is re-enabled.")]
    [SerializeField, Min(0f)] private float roundFinishedDelay = 0.8f;

    private readonly TurnQueue turnQueue = new TurnQueue();
    private readonly List<ScriptableObject> transientData = new List<ScriptableObject>();
    private readonly List<string> battleLogLines = new List<string>();

    private BattleUnit player;
    private BattleUnit enemy;
    private BattlePhase phase = BattlePhase.WaitingForPlayer;
    private int currentRound = 1;
    private bool battleEndPublished;
    private Coroutine activeResolution;

    public int CurrentRound => currentRound;
    public bool IsBattleOver => phase == BattlePhase.BattleOver;
    public int PlayerBattery => player != null ? player.CurrentBattery : 0;
    public int EnemyBattery => enemy != null ? enemy.CurrentBattery : 0;
    public int PlayerCP => player != null ? player.CurrentCP : 0;
    public int EnemyCP => enemy != null ? enemy.CurrentCP : 0;

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

        player = CreateUnit(playerConfig, ResolveRunPlayerInstance());
        enemy = CreateUnit(enemyConfig, ResolveRunOpponentInstance());
        RegisterPresentationCombatants();
        currentRound = 1;
        battleEndPublished = false;
        phase = BattlePhase.Resolving;

        EmitLog("Battle started.");
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
        if (phase != BattlePhase.WaitingForPlayer || player == null || enemy == null)
            return;

        SkillData playerSkill = player.GetSkill(slotIndex);
        if (playerSkill == null)
        {
            SetDetail("Skill Details", "No skill is loaded in this slot.");
            return;
        }

        if (IsDefenseOnCooldown(player, playerSkill))
        {
            SetDetail(SkillName(playerSkill), "Defense is cooling down this round.");
            return;
        }

        if (!CanPay(player, playerSkill))
        {
            int cost = EffectiveSkillCost(player, playerSkill);
            SetDetail(SkillName(playerSkill), $"{player.Name} needs {cost} CP.");
            return;
        }

        SkillData enemySkill = ChooseEnemySkill();
        StartRoundResolution(playerSkill, enemySkill);
    }

    public void ResolveRecharge()
    {
        if (phase != BattlePhase.WaitingForPlayer || player == null || enemy == null)
            return;

        if (rechargeSkill == null)
        {
            SetDetail("Recharge", "Recharge skill asset is not assigned.");
            return;
        }

        StartRoundResolution(rechargeSkill, ChooseEnemySkill());
    }

    private void StartRoundResolution(SkillData playerSkill, SkillData enemySkill)
    {
        StopActiveResolution();

        IEnumerator round = ResolveRoundCoroutine(playerSkill, enemySkill);
        if (UsesInstantResolution)
        {
            RunImmediate(round);
            activeResolution = null;
            return;
        }

        activeResolution = StartCoroutine(round);
    }

    private bool UsesInstantResolution =>
        logLineDelay <= 0f && damageLineDelay <= 0f && roundFinishedDelay <= 0f;

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
        hud.SkillSlotClicked += HandleSkillSlotClicked;
        hud.ActionClicked += HandleActionClicked;
    }

    private AlgoMonInstance ResolveRunPlayerInstance()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.party == null || manager.party.Count == 0)
            return null;

        return manager.party[0];
    }

    private AlgoMonInstance ResolveRunOpponentInstance()
    {
        GameManager manager = GameManager.Instance;
        return manager != null ? manager.currentOpponent : null;
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

        presentation.RegisterCombatants(player.Instance.nickname, enemy.Instance.nickname);
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

    private void HandleActionClicked(BattleHudController.ActionButton button)
    {
        if (phase != BattlePhase.WaitingForPlayer)
            return;

        switch (button)
        {
            case BattleHudController.ActionButton.Recharge:
                ResolveRecharge();
                break;

            case BattleHudController.ActionButton.Bag:
                SetDetail("Bag", "Not yet implemented.");
                break;

            case BattleHudController.ActionButton.Switch:
                SetDetail("Switch", "Not yet implemented.");
                break;

            case BattleHudController.ActionButton.Flee:
                FinishBattle(false, $"{player.Name} fled from the battle.");
                break;
        }
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

    private IEnumerator ResolveRoundCoroutine(SkillData playerSkill, SkillData enemySkill)
    {
        if (playerSkill == null || enemySkill == null)
            yield break;

        phase = BattlePhase.Resolving;
        player.StatusText = "Ready";
        enemy.StatusText = "Ready";

        BattleAction playerAction = new BattleAction(player, enemy, playerSkill);
        BattleAction enemyAction = new BattleAction(enemy, player, enemySkill);
        playerAction.DefenderInstructionType = enemySkill.instructionType;
        enemyAction.DefenderInstructionType = playerSkill.instructionType;

        EmitLog($"-- Round {currentRound} --");
        EmitLog($"{player.Name} commits {SkillName(playerSkill)}.");
        EmitLog($"{enemy.Name} commits {SkillName(enemySkill)}.");
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

        yield return ResolveCounterCoroutine(playerAction, enemyAction);
        TryFinishBattle();
        if (phase == BattlePhase.BattleOver)
        {
            RefreshHud();
            activeResolution = null;
            yield break;
        }

        QueueActions(playerAction, enemyAction);

        while (!turnQueue.IsEmpty && phase != BattlePhase.BattleOver)
        {
            AlgoMonInstance next = turnQueue.Dequeue();
            BattleAction action = ActionFor(next, playerAction, enemyAction);
            yield return ExecuteActionCoroutine(action);
            if (phase != BattlePhase.BattleOver && action.WonCounter)
                yield return ApplySubroutineTriggerCoroutine(action.Actor, action.Target, SubroutineTrigger.OnCounterWin);
            TryFinishBattle();
        }

        if (phase != BattlePhase.BattleOver)
        {
            yield return ResolveEndOfRoundStatusesCoroutine();
        }

        if (phase != BattlePhase.BattleOver)
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

    private IEnumerator ResolveCounterCoroutine(BattleAction playerAction, BattleAction enemyAction)
    {
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

        EventBus.Publish(new CounterEvent
        {
            CounterId = winner.Actor.Instance.nickname,
            CounteredId = loser.Actor.Instance.nickname,
            CounterHasDamage = winner.Skill.damageType != DamageType.None,
            CounteredHasDamage = loser.Skill.damageType != DamageType.None && !winner.Skill.counterNullifies
        });

        EmitLog($"{winner.Actor.Name}'s {SkillName(winner.Skill)} wins the ASD check.");
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

        if (winner.Skill.counterNullifies)
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

        if (playerAction.WonCounter)
        {
            float priority = CounterPriority(playerAction);
            turnQueue.Enqueue(playerAction.Actor.Instance, priority);
            turnQueue.ForceAfter(enemyAction.Actor.Instance, playerAction.Actor.Instance, priority);
            return;
        }

        if (enemyAction.WonCounter)
        {
            float priority = CounterPriority(enemyAction);
            turnQueue.Enqueue(enemyAction.Actor.Instance, priority);
            turnQueue.ForceAfter(playerAction.Actor.Instance, enemyAction.Actor.Instance, priority);
            return;
        }

        turnQueue.Enqueue(playerAction.Actor.Instance, EffectivePriority(playerAction));
        turnQueue.Enqueue(enemyAction.Actor.Instance, EffectivePriority(enemyAction));
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
        if (ReferenceEquals(instance, playerAction.Actor.Instance))
            return playerAction;
        return enemyAction;
    }

    private IEnumerator ExecuteActionCoroutine(BattleAction action)
    {
        if (action == null || action.Actor.CurrentBattery <= 0)
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
        if (phase == BattlePhase.BattleOver || action.Actor.CurrentBattery <= 0)
            yield break;

        int cost = EffectiveSkillCost(action.Actor, action.Skill);
        if (!SpendCP(action.Actor, cost))
        {
            action.Actor.StatusText = "No CP";
            EmitLog($"{action.Actor.Name} lacks {cost} CP for {SkillName(action.Skill)}.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
            yield break;
        }

        if (action.Skill.instructionType == InstructionType.Defense)
            action.Actor.LastDefenseRound = currentRound;

        action.BasePowerBonus = action.Actor.Statuses.BasePowerBonus(currentRound);
        int repeatCount = action.Actor.Statuses.SkillRepeatCount(currentRound);
        action.Actor.Statuses.ConsumeSkillUseModifiers(currentRound);

        EmitLog($"{action.Actor.Name} uses {SkillName(action.Skill)}.");
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

        for (int repeat = 0; repeat < repeatCount && phase != BattlePhase.BattleOver; repeat++)
        {
            if (repeat > 0)
            {
                EmitLog($"{SkillName(action.Skill)} repeats from Concurrent.");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }

            yield return ResolveSkillEffectCoroutine(action);
            TryFinishBattle();
            if (action.Target.CurrentBattery <= 0)
                yield break;
        }

        if (action.WonCounter && action.Skill.counterRecast && phase != BattlePhase.BattleOver && action.Target.CurrentBattery > 0)
        {
            EmitLog($"{SkillName(action.Skill)} recasts from counter momentum.");
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
                action.FinalDamageMultiplier,
                action.BasePowerBonus);

            int previousBattery = action.Target.CurrentBattery;
            action.Target.CurrentBattery = Mathf.Max(0, action.Target.CurrentBattery - damage);
            if (damage > 0)
                action.Target.StatusText = "Hit";
            EmitLog($"{action.Target.Name} takes {damage} damage.");
            RefreshHud();
            if (damageLineDelay > 0f)
                yield return new WaitForSeconds(damageLineDelay);

            if (damage > 0)
            {
                yield return ApplyDamageTakenTriggersCoroutine(action.Target, action.Actor, previousBattery, true);
                TryFinishBattle();
                if (phase == BattlePhase.BattleOver)
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
            SourceId = source.Instance.nickname,
            TargetId = target.Instance.nickname,
            Status = status,
            Stacks = result.AddedStacks,
            DurationType = result.DurationType,
            Duration = result.Duration
        });

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
        if (phase == BattlePhase.BattleOver)
            yield break;

        if (enemy != null && enemy.CurrentBattery > 0)
            yield return TickUnitStatusesCoroutine(enemy);
        TryFinishBattle();
        if (phase == BattlePhase.BattleOver)
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
        if (phase == BattlePhase.BattleOver)
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
                if (phase == BattlePhase.BattleOver)
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
        return unit.Statuses.ApplyToStats(BattleStats.From(unit.Instance));
    }

    private int EffectiveSkillCost(BattleUnit unit, SkillData skill)
    {
        if (unit == null || skill == null)
            return 0;
        int reducedBaseCost = Mathf.Max(0, skill.cpCost - unit.CostReductionFor(skill, currentRound));
        return unit.Statuses.EffectiveSkillCost(reducedBaseCost, currentRound);
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

    private static bool SpendCP(BattleUnit unit, int amount)
    {
        if (amount <= 0)
            return true;
        if (unit.CurrentCP < amount)
            return false;

        unit.CurrentCP -= amount;
        EventBus.Publish(new BattleFeedbackEvent
        {
            TargetId = unit.Instance.nickname,
            Type = BattleFeedbackType.CPDrain,
            Amount = amount,
            Label = $"-{amount} CP"
        });
        return true;
    }

    private static int GainCP(BattleUnit unit, int amount)
    {
        int before = unit.CurrentCP;
        unit.CurrentCP = Mathf.Clamp(unit.CurrentCP + Mathf.Max(0, amount), 0, MaxCP);
        int restored = unit.CurrentCP - before;
        if (restored > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = unit.Instance.nickname,
                Type = BattleFeedbackType.CPGain,
                Amount = restored,
                Label = $"+{restored} CP"
            });
        }
        return restored;
    }

    private static int DrainCP(BattleUnit from, BattleUnit to, int amount)
    {
        int drained = Mathf.Min(from.CurrentCP, Mathf.Max(0, amount));
        from.CurrentCP -= drained;
        to.CurrentCP = Mathf.Clamp(to.CurrentCP + drained, 0, MaxCP);
        if (drained > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = from.Instance.nickname,
                Type = BattleFeedbackType.CPDrain,
                Amount = drained,
                Label = $"-{drained} CP"
            });
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = to.Instance.nickname,
                Type = BattleFeedbackType.CPGain,
                Amount = drained,
                Label = $"+{drained} CP"
            });
        }
        return drained;
    }

    private static int DealStatusDamage(BattleUnit unit, int amount)
    {
        int actual = Mathf.Min(unit.CurrentBattery, Mathf.Max(0, amount));
        unit.CurrentBattery = Mathf.Max(0, unit.CurrentBattery - actual);
        if (actual > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = unit.Instance.nickname,
                Type = BattleFeedbackType.Damage,
                Amount = actual,
                Label = $"-{actual}"
            });
        }
        return actual;
    }

    private static int HealBattery(BattleUnit unit, int amount)
    {
        int before = unit.CurrentBattery;
        unit.CurrentBattery = Mathf.Clamp(unit.CurrentBattery + Mathf.Max(0, amount), 0, unit.MaxBattery);
        int restored = unit.CurrentBattery - before;
        if (restored > 0)
        {
            EventBus.Publish(new BattleFeedbackEvent
            {
                TargetId = unit.Instance.nickname,
                Type = BattleFeedbackType.Heal,
                Amount = restored,
                Label = $"+{restored}"
            });
        }
        return restored;
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

    private void TryFinishBattle()
    {
        if (phase == BattlePhase.BattleOver)
            return;

        if (enemy.CurrentBattery <= 0)
        {
            enemy.StatusText = "Offline";
            FinishBattle(true, $"{enemy.Name} is offline. Victory.");
            return;
        }

        if (player.CurrentBattery <= 0)
        {
            player.StatusText = "Offline";
            FinishBattle(false, $"{player.Name} is offline. Defeat.");
        }
    }

    private void FinishBattle(bool playerWon, string message)
    {
        phase = BattlePhase.BattleOver;
        EmitLog(message);

        if (!battleEndPublished)
        {
            if (playerWon)
                TryExtractDefeatedEnemy();

            EventBus.Publish(new BattleEndEvent { PlayerWon = playerWon });
            battleEndPublished = true;
        }

        RefreshHud();
    }

    private void TryExtractDefeatedEnemy()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || enemy == null)
            return;

        // Sprint 3 v1 extracts every defeated asset-backed encounter, including Boss.
        if (manager.TryRegisterCapture(enemy.Instance, out AlgoMonInstance captured))
        {
            EmitLog($"EXTRACTED: {DisplayNameFor(captured)} added to Payload.");
            return;
        }

        EmitLog("EXTRACTION SKIPPED: encounter data is not persistent.");
    }

    private void RefreshHud()
    {
        if (hud == null || player == null || enemy == null)
            return;

        hud.SetRound(currentRound);
        hud.SetBattleState(PhaseLabel());

        hud.SetCombatant(BattleHudController.Side.Player, player.Name, player.DisplayLevel);
        hud.SetBattery(BattleHudController.Side.Player, player.CurrentBattery, player.MaxBattery);
        hud.SetCP(BattleHudController.Side.Player, player.CurrentCP, MaxCP);
        hud.SetStatus(BattleHudController.Side.Player, $"Status: {FormatUnitStatus(player)}");

        hud.SetCombatant(BattleHudController.Side.Enemy, enemy.Name, enemy.DisplayLevel);
        hud.SetBattery(BattleHudController.Side.Enemy, enemy.CurrentBattery, enemy.MaxBattery);
        hud.SetCP(BattleHudController.Side.Enemy, enemy.CurrentCP, MaxCP);
        hud.SetStatus(BattleHudController.Side.Enemy, $"Status: {FormatUnitStatus(enemy)}");

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
            hud.SetSkillSlotAvailable(i, phase == BattlePhase.WaitingForPlayer && CanUseSkill(player, skill));
        }

        bool canAct = phase == BattlePhase.WaitingForPlayer;
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Recharge, canAct);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Bag, canAct);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Switch, canAct);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Flee, canAct);

        hud.SetActionHover(BattleHudController.ActionButton.Recharge, "Recharge", "+5 CP\nSpend the turn to restore CP.");
        hud.SetActionHover(BattleHudController.ActionButton.Bag, "Bag", "Not yet implemented.");
        hud.SetActionHover(BattleHudController.ActionButton.Switch, "Switch", "Not yet implemented.");
        hud.SetActionHover(BattleHudController.ActionButton.Flee, "Flee", "End this battle immediately.");
    }

    private string PhaseLabel()
    {
        switch (phase)
        {
            case BattlePhase.WaitingForPlayer:
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
        line.Append($"CP {cost}");
        if (cost != Mathf.Max(0, skill.cpCost))
            line.Append($" (base {Mathf.Max(0, skill.cpCost)})");

        if (skill.basePower > 0)
            line.Append($" | PWR {skill.basePower}");
        if (skill.canCounter)
            line.Append(" | Counter");

        string body = string.IsNullOrWhiteSpace(skill.description)
            ? string.Empty
            : skill.description.Trim();

        if (IsDefenseOnCooldown(unit, skill))
            return $"{line}\nDefense is cooling down this round.\n{body}".Trim();

        if (unit.CurrentCP < cost)
        {
            int missing = cost - unit.CurrentCP;
            return $"{line}\nNeeds {missing} more CP.\n{body}".Trim();
        }

        return string.IsNullOrEmpty(body)
            ? line.ToString()
            : $"{line}\n{body}";
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

    private void SetDetail(string title, string body)
    {
        if (hud != null)
            hud.SetSkillDetail(title, body);
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
            hud.SetSkillDetail("Battle Log", string.Join("\n", battleLogLines));
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
