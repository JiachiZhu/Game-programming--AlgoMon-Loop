using UnityEngine;

/// <summary>
/// ScriptableObject defining a single skill (data instruction).
///
/// Effects are organized into three clear groups:
///   Base Effect   — the primary effect when this skill is used (any instruction type)
///   Counter Win   — what happens when this skill wins the ASD check, split by target
///   On Hit        — what happens when this skill deals damage, split by target
///
/// All effects explicitly state whether they target SELF or OPPONENT.
/// BattleManager reads these fields directly — no "custom handling" needed
/// except for fields marked [BattleManager: special].
/// </summary>
[CreateAssetMenu(fileName = "New Skill", menuName = "AlgoMon/Skill Data")]
public class SkillData : ScriptableObject
{
    // =========================================================================
    // Identity
    // =========================================================================

    [Header("Identity")]
    public string skillName;
    [TextArea] public string description;

    // =========================================================================
    // Classification
    // =========================================================================

    [Header("Classification")]
    public InstructionType instructionType;  // A / S / D
    public DamageType      damageType;       // None for Defense/Status; Computing or Throughput for Attack
    public ElementType     elementType;

    // =========================================================================
    // Values
    // =========================================================================

    [Header("Values")]
    public int   basePower;
    public int   cpCost;

    [Tooltip("If true, this skill is always available to every AlgoMon without occupying a skill slot.\n" +
             "BattleManager offers it as an extra option each turn. (e.g. Recharge)")]
    public bool isUniversal = false;

    [Tooltip("Restore this many CP to SELF when this skill is used. 0 = no CP recovery.\n" +
             "Applied before damage/status resolution. (e.g. Recharge restores 5 CP)")]
    public int baseHealCPAmount = 0;

    [Header("Turn Priority")]
    [Tooltip("+1 = first-strike (acts before priority 0).\n0 = normal.\n-1 = last-strike.\nASD counter winner overrides all priority tiers.")]
    public int priority = 0;

    // =========================================================================
    // Base Skill Effect  (Status skills)
    // Primary effect executed when the skill is used, before ASD resolution.
    // Leave baseStatusStacks = 0 if this skill has no status-based primary effect.
    // =========================================================================

    [Header("Base Effect")]
    [Tooltip("Who receives the primary status effect.\n" +
             "Self = apply to user when this skill is executed.\n" +
             "Opponent = apply to target (Attack: on hit; Status/Defense: on use).")]
    public StatusTarget baseStatusTarget = StatusTarget.Self;

    public StatusType baseStatus;

    [Tooltip("Number of stacks to apply as the primary effect. 0 = no base status effect.")]
    public int baseStatusStacks = 0;

    public StatusDurationType baseStatusDurationType = StatusDurationType.Turns;

    [Tooltip("Only used when baseStatusDurationType = Turns.")]
    public int baseStatusDuration = 3;

    // =========================================================================
    // ASD Counter — Participation
    // =========================================================================

    [Header("ASD Counter — Participation")]
    [Tooltip("If true, this skill participates in the ASD rock-paper-scissors check.\nAll Defense skills MUST be true.\nAttack/Status skills opt in per design.")]
    public bool canCounter = false;

    // =========================================================================
    // Counter Win → OPPONENT
    // Applied to the opponent when THIS skill wins the ASD counter check.
    // =========================================================================

    [Header("Counter Win → OPPONENT")]

    [Tooltip("Cancel the opponent's skill entirely.\nTheir CP is NOT consumed and their turn is wasted.")]
    public bool counterNullifies = false;

    [Tooltip("Steal this many CP from the opponent on counter win.")]
    public int counterDrainOpponentCP = 0;

    [Tooltip("Reduce opponent's Firewall by this fraction on counter win.\n0.2 = shred 20%.")]
    [Range(0f, 1f)]
    public float counterShredOpponentFirewall = 0f;
    public StatusDurationType counterFirewallShredDurationType = StatusDurationType.WhileOnField;
    [Tooltip("Only used when counterFirewallShredDurationType = Turns.")]
    public int counterFirewallShredDuration = 1;

    [Tooltip("Status applied to the OPPONENT on counter win. Stacks = 0 = no effect.")]
    public StatusType counterApplyToOpponent;
    public int counterOpponentStatusStacks = 0;
    public StatusDurationType counterOpponentStatusDurationType = StatusDurationType.Turns;
    [Tooltip("Only used when counterOpponentStatusDurationType = Turns.")]
    public int counterOpponentStatusDuration = 1;

    [Tooltip("Force the opponent to act absolutely last next turn.\n(e.g. Absolute Zero Crash — injects priority -10000 on opponent)")]
    public bool counterForceOpponentLast = false;

    // =========================================================================
    // Counter Win → SELF
    // Applied to THIS AlgoMon when it wins the ASD counter check.
    // =========================================================================

    [Header("Counter Win → SELF")]

    [Tooltip("Absorb this fraction of the opponent's incoming attack damage.\n0.7 = take only 30% of their damage. Used by Defense skills.")]
    [Range(0f, 1f)]
    public float counterBlockPercent = 0f;

    [Tooltip("Damage multiplier applied to THIS skill's output when it wins the counter.\n1 = no bonus. 1.5 = 50% more damage.")]
    public float counterSelfDamageMultiplier = 1f;

    [Tooltip("Status applied to SELF on counter win. Stacks = 0 = no effect.")]
    public StatusType counterApplyToSelf;
    public int counterSelfStatusStacks = 0;
    public StatusDurationType counterSelfStatusDurationType = StatusDurationType.Turns;
    [Tooltip("Only used when counterSelfStatusDurationType = Turns.")]
    public int counterSelfStatusDuration = 1;

    [Tooltip("Re-cast THIS skill once at 0 CP immediately after winning the counter.\n(e.g. Ignite Loop)")]
    public bool counterRecast = false;

    [Tooltip("Reduce ALL own skill CP costs by this amount after counter win.")]
    public int counterSelfCPDiscount = 0;
    public StatusDurationType counterCPDiscountDurationType = StatusDurationType.Turns;
    [Tooltip("Only used when counterCPDiscountDurationType = Turns.")]
    public int counterCPDiscountDuration = 2;

    [Tooltip("Permanently reduce THIS skill's cpCost by this amount (min 0) on counter win.\n(e.g. Deep Web Tsunami — cpCost −2 permanently)")]
    public int counterPermanentCPReduce = 0;

    [Tooltip("Add this value to the NEXT action's skill priority after counter win.\n(e.g. Short Circuit — next attack gets priority +1)")]
    public int counterNextPriorityBonus = 0;

    [Tooltip("Add this value to the NEXT action's skill basePower after counter win.\n(e.g. Short Circuit — next attack gets basePower +10)")]
    public int counterNextBasePowerBonus = 0;

    [Tooltip("Heal self by this fraction of max Battery on counter win.\n0.08 = restore 8% HP. (e.g. Safe Mode)")]
    [Range(0f, 1f)]
    public float counterSelfHealPercent = 0f;

    [Tooltip("Clear ALL temporary negative statuses from self on counter win.\n(e.g. Safe Mode — removes Burn, Freeze, Leech etc.)")]
    public bool counterClearsOwnDebuffs = false;

    // =========================================================================
    // On Hit → OPPONENT
    // Applied to the opponent when THIS skill deals damage (damage > 0).
    // Does not fire if the skill is blocked, cancelled, or deals 0 damage.
    // =========================================================================

    [Header("On Hit → OPPONENT (when damage > 0)")]

    [Tooltip("Steal this many CP from the opponent when damage is dealt.")]
    public int onHitDrainOpponentCP = 0;

    [Tooltip("Reduce opponent's Firewall by this fraction on hit.\n0.2 = shred 20%.")]
    [Range(0f, 1f)]
    public float onHitShredOpponentFirewall = 0f;
    public StatusDurationType onHitFirewallShredDurationType = StatusDurationType.WhileOnField;
    [Tooltip("Only used when onHitFirewallShredDurationType = Turns.")]
    public int onHitFirewallShredDuration = 1;

    [Tooltip("Status applied to the OPPONENT on hit. Stacks = 0 = no effect.")]
    public StatusType onHitApplyToOpponent;
    public int onHitOpponentStatusStacks = 0;
    public StatusDurationType onHitOpponentStatusDurationType = StatusDurationType.Turns;
    [Tooltip("Only used when onHitOpponentStatusDurationType = Turns.")]
    public int onHitOpponentStatusDuration = 1;
}
