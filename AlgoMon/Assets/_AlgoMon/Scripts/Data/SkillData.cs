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
    [Tooltip("Block only: fraction of opponent's incoming damage absorbed. 0.7 = 70% reduced.")]
    [Range(0f, 1f)] public float counterBlockPercent = 0f;

    [Tooltip("SelfBuff only: which status is applied (or amplified) on counter win.")]
    public StatusType counterSelfStatus;

    [Tooltip("SelfBuff only: additional stacks or magnitude added to counterSelfStatus.")]
    public int counterBonusValue = 0;

    [Tooltip("SelfBuff only: duration in turns of the counter-triggered buff. 0 = this turn only.")]
    public int counterStatusDuration = 0;
}
