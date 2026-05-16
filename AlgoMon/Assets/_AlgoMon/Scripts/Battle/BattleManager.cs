using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Core battle loop for TheArena.
///
/// Scope for issue #15:
/// - player chooses an action through BattleHudController
/// - enemy chooses a simple deterministic skill
/// - ASD counter check determines hard turn-order overrides
/// - skill priority and ClockSpeed break normal turn order
/// - CP is spent / restored
/// - basic damage is resolved and Battery is reduced until one side is offline
///
/// Status ticking, defense cooldowns, party switching, bag items, and Subroutine
/// triggers are intentionally left for the follow-up battle issues.
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    private const int MaxCP = 10;
    private const int MaxSkillSlots = 4;
    private const float CounterPriorityBase = 1000000f;

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

        [Header("Active Skills")]
        public SkillData[] skills = new SkillData[MaxSkillSlots];
    }

    private sealed class BattleUnit
    {
        public BattleUnit(BattleCombatantConfig config, AlgoMonInstance instance)
        {
            Config = config;
            Instance = instance;
            CurrentBattery = instance.Battery;
            CurrentCP = Mathf.Clamp(config.startingCP, 0, MaxCP);
            StatusText = "Ready";
        }

        public BattleCombatantConfig Config { get; }
        public AlgoMonInstance Instance { get; }
        public int CurrentBattery { get; set; }
        public int CurrentCP { get; set; }
        public string StatusText { get; set; }

        public string Name => Config.displayName;
        public int DisplayLevel => Mathf.Clamp(Config.displayLevel, 1, AlgoMonInstance.MAX_LEVEL);
        public int MaxBattery => Instance.Battery;

        public SkillData GetSkill(int index)
        {
            if (Config.skills == null) return null;
            if (index < 0 || index >= Config.skills.Length) return null;
            return Config.skills[index];
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
        public InstructionType DefenderInstructionType { get; set; } = InstructionType.Attack;
    }

    [SerializeField] private BattleHudController hud;
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
        startingCP = 6,
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
        startingCP = 8,
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
        DestroyTransientData();
        battleLogLines.Clear();

        player = CreateUnit(playerConfig);
        enemy = CreateUnit(enemyConfig);
        currentRound = 1;
        battleEndPublished = false;
        phase = BattlePhase.WaitingForPlayer;

        RefreshHud();
        EmitLog("Battle started.");
        EmitLog("Choose a skill.");
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

        if (!CanPay(player, playerSkill))
        {
            SetDetail(SkillName(playerSkill), $"{player.Name} needs {playerSkill.cpCost} CP.");
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

    private BattleUnit CreateUnit(BattleCombatantConfig config)
    {
        AlgoMonData data = ScriptableObject.CreateInstance<AlgoMonData>();
        data.codeName = config.displayName;
        data.elementType = config.elementType;
        transientData.Add(data);

        var instance = new AlgoMonInstance
        {
            data = data,
            nickname = config.displayName,
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

    private static int ClampStat(int value) => Mathf.Clamp(value, 1, 255);

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
                SetDetail("Bag", "Items are not part of issue #15 yet.");
                break;

            case BattleHudController.ActionButton.Switch:
                SetDetail("Switch", "Party switching is not part of issue #15 yet.");
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
            if (skill == null || !CanPay(enemy, skill))
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

        QueueActions(playerAction, enemyAction);

        while (!turnQueue.IsEmpty && phase != BattlePhase.BattleOver)
        {
            AlgoMonInstance next = turnQueue.Dequeue();
            BattleAction action = ActionFor(next, playerAction, enemyAction);
            yield return ExecuteActionCoroutine(action);
            TryFinishBattle();
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
            CounteredId = loser.Actor.Instance.nickname
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
        if (winner.Skill.counterDrainOpponentCP > 0)
        {
            int drained = DrainCP(loser.Actor, winner.Actor, winner.Skill.counterDrainOpponentCP);
            if (drained > 0)
            {
                EmitLog($"{winner.Actor.Name} drains {drained} CP.");
                RefreshHud();
                if (logLineDelay > 0f)
                    yield return new WaitForSeconds(logLineDelay);
            }
        }

        if (winner.Skill.counterSelfHealPercent > 0f)
        {
            int heal = Mathf.Max(1, Mathf.RoundToInt(winner.Actor.MaxBattery * winner.Skill.counterSelfHealPercent));
            int restored = HealBattery(winner.Actor, heal);
            if (restored > 0)
            {
                EmitLog($"{winner.Actor.Name} restores {restored} Battery.");
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

    private static float CounterPriority(BattleAction action)
    {
        return CounterPriorityBase + EffectivePriority(action);
    }

    private static float EffectivePriority(BattleAction action)
    {
        int skillPriority = action.Skill != null ? action.Skill.priority : 0;
        return skillPriority * 10000f + action.Actor.Instance.ClockSpeed;
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

        int cost = Mathf.Max(0, action.Skill.cpCost);
        if (!SpendCP(action.Actor, cost))
        {
            action.Actor.StatusText = "No CP";
            EmitLog($"{action.Actor.Name} lacks CP for {SkillName(action.Skill)}.");
            RefreshHud();
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
            yield break;
        }

        EmitLog($"{action.Actor.Name} uses {SkillName(action.Skill)}.");
        RefreshHud();
        if (logLineDelay > 0f)
            yield return new WaitForSeconds(logLineDelay);

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

        if (action.Skill.damageType != DamageType.None && action.Target.CurrentBattery > 0)
        {
            int damage = CombatResolver.ResolveDamage(
                action.Actor.Instance,
                action.Target.Instance,
                action.Skill,
                action.DefenderInstructionType,
                action.WonCounter,
                action.FinalDamageMultiplier);

            action.Target.CurrentBattery = Mathf.Max(0, action.Target.CurrentBattery - damage);
            if (damage > 0)
                action.Target.StatusText = "Hit";
            EmitLog($"{action.Target.Name} takes {damage} damage.");
            RefreshHud();
            if (damageLineDelay > 0f)
                yield return new WaitForSeconds(damageLineDelay);

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
        }
        else
        {
            // Status / Defense skills don't deal damage in #15. Narrate intent
            // without claiming that status state has actually been applied.
            yield return NarrateNonDamageSkill(action);
        }
    }

    /// <summary>
    /// Emits one or more descriptive lines for a Status / Defense skill that
    /// dealt no damage. Reads SkillData but does not mutate combatant state
    /// beyond what already happened (CP spend, optional baseHeal). Real status
    /// and cooldown application lives in issues #16 and #17.
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

        if (skill.baseStatusStacks > 0)
        {
            string targetName = skill.baseStatusTarget == StatusTarget.Self
                ? action.Actor.Name
                : action.Target.Name;
            string stackPart = skill.baseStatusStacks == 1 ? "1 stack" : $"{skill.baseStatusStacks} stacks";
            EmitLog($"{SkillName(skill)} sets up {skill.baseStatus} pressure ({stackPart}) on {targetName}.");
            if (logLineDelay > 0f)
                yield return new WaitForSeconds(logLineDelay);
        }
    }

    private bool CanPay(BattleUnit unit, SkillData skill)
    {
        if (unit == null || skill == null)
            return false;
        return unit.CurrentCP >= Mathf.Max(0, skill.cpCost);
    }

    private static bool SpendCP(BattleUnit unit, int amount)
    {
        if (amount <= 0)
            return true;
        if (unit.CurrentCP < amount)
            return false;

        unit.CurrentCP -= amount;
        return true;
    }

    private static int GainCP(BattleUnit unit, int amount)
    {
        int before = unit.CurrentCP;
        unit.CurrentCP = Mathf.Clamp(unit.CurrentCP + Mathf.Max(0, amount), 0, MaxCP);
        return unit.CurrentCP - before;
    }

    private static int DrainCP(BattleUnit from, BattleUnit to, int amount)
    {
        int drained = Mathf.Min(from.CurrentCP, Mathf.Max(0, amount));
        from.CurrentCP -= drained;
        to.CurrentCP = Mathf.Clamp(to.CurrentCP + drained, 0, MaxCP);
        return drained;
    }

    private static int HealBattery(BattleUnit unit, int amount)
    {
        int before = unit.CurrentBattery;
        unit.CurrentBattery = Mathf.Clamp(unit.CurrentBattery + Mathf.Max(0, amount), 0, unit.MaxBattery);
        return unit.CurrentBattery - before;
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
            EventBus.Publish(new BattleEndEvent { PlayerWon = playerWon });
            battleEndPublished = true;
        }

        RefreshHud();
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
        hud.SetStatus(BattleHudController.Side.Player, $"Status: {player.StatusText}");

        hud.SetCombatant(BattleHudController.Side.Enemy, enemy.Name, enemy.DisplayLevel);
        hud.SetBattery(BattleHudController.Side.Enemy, enemy.CurrentBattery, enemy.MaxBattery);
        hud.SetCP(BattleHudController.Side.Enemy, enemy.CurrentCP, MaxCP);
        hud.SetStatus(BattleHudController.Side.Enemy, $"Status: {enemy.StatusText}");

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
            hud.SetSkillSlotAvailable(i, phase == BattlePhase.WaitingForPlayer && CanPay(player, skill));
        }

        bool canAct = phase == BattlePhase.WaitingForPlayer;
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Recharge, canAct);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Bag, canAct);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Switch, canAct);
        hud.SetActionButtonAvailable(BattleHudController.ActionButton.Flee, canAct);

        hud.SetActionHover(BattleHudController.ActionButton.Recharge, "Recharge", "+5 CP\nSpend the turn to restore CP.");
        hud.SetActionHover(BattleHudController.ActionButton.Bag, "Bag", "Items are not part of issue #15 yet.");
        hud.SetActionHover(BattleHudController.ActionButton.Switch, "Switch", "Party switching is not part of issue #15 yet.");
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

    private static string BuildSkillHover(BattleUnit unit, SkillData skill)
    {
        var line = new StringBuilder();
        line.Append($"CP {Mathf.Max(0, skill.cpCost)}");

        if (skill.basePower > 0)
            line.Append($" | PWR {skill.basePower}");
        if (skill.canCounter)
            line.Append(" | Counter");

        string body = string.IsNullOrWhiteSpace(skill.description)
            ? string.Empty
            : skill.description.Trim();

        if (unit.CurrentCP < skill.cpCost)
        {
            int missing = skill.cpCost - unit.CurrentCP;
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
