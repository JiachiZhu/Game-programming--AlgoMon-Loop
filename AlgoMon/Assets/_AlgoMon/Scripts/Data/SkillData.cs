using UnityEngine;

/// <summary>
/// ScriptableObject defining a single skill (data instruction).
/// </summary>
[CreateAssetMenu(fileName = "New Skill", menuName = "AlgoMon/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    [TextArea] public string description;

    [Header("Classification")]
    public InstructionType instructionType;  // A / S / D — used for ASD counter check
    public DamageType      damageType;       // Computing Power or Throughput
    public ElementType     elementType;      // determines element chart multiplier

    [Header("Values")]
    public int   basePower;
    public int   cpCost;

    [Header("Turn Priority")]
    [Tooltip("+1 = first-strike (acts before priority 0), 0 = normal, -1 = last-strike (acts after priority 0). " +
             "Skill priority is overridden by ASD counter result (ForceAfter).")]
    public int priority = 0;

    [Header("ASD Counter Interaction")]
    [Tooltip("Whether this skill participates in the ASD rock-paper-scissors check.\n" +
             "If false, turn order is determined by speed/priority only.\n" +
             "All Defense skills MUST set this to true.")]
    public bool canCounter = false;

    [Tooltip("None    : no special effect on counter win (opponent delayed, CP consumed).\n" +
             "Nullify : opponent skill cancelled, CP not consumed, turn wasted.\n" +
             "Block   : opponent attack reduced by counterBlockPercent.\n" +
             "SelfBuff: apply additional buff to self (counterSelfStatus x counterBonusValue).")]
    public CounterSuccessType counterSuccessType = CounterSuccessType.None;

    [Tooltip("Damage multiplier applied to THIS skill's output when it wins the ASD counter. 1 = no bonus.")]
    public float counterSuccessMultiplier = 1f;

    [Header("Counter Effect Parameters")]
    [Tooltip("Block only: fraction of opponent's incoming damage absorbed. 0.7 = absorbs 70%.")]
    [Range(0f, 1f)] public float counterBlockPercent = 0f;

    [Tooltip("SelfBuff only: which status is applied (or amplified) on counter win.")]
    public StatusType counterSelfStatus;

    [Tooltip("SelfBuff only: additional stacks added to counterSelfStatus on counter win.")]
    public int counterBonusValue = 0;

    [Tooltip("How long the counter-triggered status lasts.\n" +
             "Permanent    = survives swaps, lasts until battle end.\n" +
             "WhileOnField = no turn limit, but cleared when swapped out.\n" +
             "Turns        = lasts counterStatusDuration turns, cleared on swap.")]
    public StatusDurationType counterStatusDurationType = StatusDurationType.Permanent;

    [Tooltip("Only used when counterStatusDurationType = Turns. Number of turns the status lasts.")]
    public int counterStatusDuration = 1;

    [Tooltip("Counter win: drain this many CP from the opponent. 0 = no drain.")]
    public int counterCPDrain = 0;

    [Tooltip("Counter win: reduce ALL own skill CP costs by this amount for counterStatusDuration turns. 0 = no discount.")]
    public int counterCPDiscount = 0;

    [Tooltip("Counter win: permanently reduce THIS skill's cpCost by this amount (min 0). 0 = no reduction.")]
    public int counterPermanentCPCostReduce = 0;

    [Header("On-Hit Effects")]
    [Tooltip("On damage dealt: steal this many CP from the opponent. 0 = no steal.")]
    public int cpDrain = 0;

    [Tooltip("On damage dealt: reduce opponent's Firewall by this fraction. 0.2 = shred 20%. 0 = no shred.")]
    [Range(0f, 1f)] public float onHitFirewallShred = 0f;

    [Tooltip("How long the Firewall shred lasts.\n" +
             "Permanent = lasts until battle end (survives swaps).\n" +
             "WhileOnField = lasts while shredded unit stays on field.\n" +
             "Turns = lasts onHitFirewallShredDuration turns.")]
    public StatusDurationType onHitFirewallShredDurationType = StatusDurationType.WhileOnField;

    [Tooltip("Only used when onHitFirewallShredDurationType = Turns.")]
    public int onHitFirewallShredDuration = 1;

    [Header("On-Hit Status")]
    [Tooltip("Apply this status to the opponent on hit. Leave at default if no status is applied.")]
    public StatusType onHitStatus;

    [Tooltip("Number of stacks of onHitStatus to apply. 0 = no status applied.")]
    public int onHitStatusStacks = 0;

    [Tooltip("How long the on-hit status lasts.\n" +
             "Permanent = lasts until battle end (survives swaps).\n" +
             "WhileOnField = lasts while target stays on field.\n" +
             "Turns = lasts onHitStatusDuration turns.")]
    public StatusDurationType onHitStatusDurationType = StatusDurationType.Turns;

    [Tooltip("Only used when onHitStatusDurationType = Turns.")]
    public int onHitStatusDuration = 1;
}
