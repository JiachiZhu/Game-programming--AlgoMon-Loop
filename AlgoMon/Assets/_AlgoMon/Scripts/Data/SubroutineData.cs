using UnityEngine;

/// <summary>
/// ScriptableObject defining a species' built-in passive ability (Subroutine).
///
/// A Subroutine is a background process hardwired into the AlgoMon's firmware.
/// It cannot be chosen or replaced — it activates automatically when its
/// trigger condition is met during battle.
///
/// BattleManager is responsible for checking triggers and applying effects.
/// This class mirrors SkillData's counter-effect fields so BattleManager can
/// resolve passive effects with the same target/status/duration semantics.
/// </summary>
[CreateAssetMenu(fileName = "New Subroutine", menuName = "AlgoMon/Subroutine Data")]
public class SubroutineData : ScriptableObject
{
    [Header("Identity")]
    public string subroutineName;
    [TextArea] public string description;

    [Header("Trigger")]
    [Tooltip("The battle condition that activates this subroutine.")]
    public SubroutineTrigger trigger;

    // =========================================================================
    // Triggered Effect -> OPPONENT
    // Same meaning as SkillData's Counter Win -> OPPONENT section.
    // =========================================================================

    [Header("Triggered Effect -> OPPONENT")]

    [Tooltip("Steal this many CP from the opponent when this subroutine triggers.")]
    public int drainOpponentCP = 0;

    [Tooltip("Reduce opponent's Firewall by this fraction when this subroutine triggers.\n0.2 = shred 20%.")]
    [Range(0f, 1f)]
    public float shredOpponentFirewall = 0f;
    public StatusDurationType firewallShredDurationType = StatusDurationType.WhileOnField;
    [Tooltip("Only used when firewallShredDurationType = Turns.")]
    public int firewallShredDuration = 1;

    [Tooltip("Status applied to the OPPONENT when this subroutine triggers. Stacks = 0 = no effect; the StatusType value is ignored.")]
    public StatusType applyToOpponent;
    public int opponentStatusStacks = 0;
    public StatusDurationType opponentStatusDurationType = StatusDurationType.Turns;
    [Tooltip("Only used when opponentStatusDurationType = Turns.")]
    public int opponentStatusDuration = 1;

    [Tooltip("Force the opponent to act absolutely last next turn.")]
    public bool forceOpponentLast = false;

    // =========================================================================
    // Triggered Effect -> SELF
    // Same meaning as SkillData's Counter Win -> SELF section.
    // =========================================================================

    [Header("Triggered Effect -> SELF")]

    [Tooltip("Status applied to SELF when this subroutine triggers. Stacks = 0 = no effect; the StatusType value is ignored.")]
    public StatusType applyToSelf;
    public int selfStatusStacks = 0;
    public StatusDurationType selfStatusDurationType = StatusDurationType.Turns;
    [Tooltip("Only used when selfStatusDurationType = Turns.")]
    public int selfStatusDuration = 1;

    [Tooltip("Reduce ALL own skill CP costs by this amount when this subroutine triggers.")]
    public int selfCPDiscount = 0;
    public StatusDurationType cpDiscountDurationType = StatusDurationType.Turns;
    [Tooltip("Only used when cpDiscountDurationType = Turns.")]
    public int cpDiscountDuration = 2;

    [Tooltip("Add this value to the NEXT action's skill priority.")]
    public int nextPriorityBonus = 0;

    [Tooltip("Add this value to the NEXT action's skill basePower.")]
    public int nextBasePowerBonus = 0;

    [Tooltip("Heal self by this fraction of max Battery.\n0.08 = restore 8% HP.")]
    [Range(0f, 1f)]
    public float selfHealPercent = 0f;

    [Tooltip("Clear ALL temporary negative statuses from self.")]
    public bool clearsOwnDebuffs = false;

    /// <summary>Human-readable trigger label for this subroutine, e.g. "ON LOW BATTERY (&lt;25%)".</summary>
    public string TriggerLabel => LabelFor(trigger);

    /// <summary>
    /// Maps a SubroutineTrigger to a player-facing label. Shared by the Payload
    /// inspector card, the battle HUD badge, and the subroutine-fired banner so
    /// the wording stays consistent everywhere a subroutine is surfaced.
    /// </summary>
    public static string LabelFor(SubroutineTrigger trigger)
    {
        switch (trigger)
        {
            case SubroutineTrigger.OnBattleStart: return "ON BATTLE START";
            case SubroutineTrigger.OnTurnStart:   return "ON TURN START";
            case SubroutineTrigger.OnCounterWin:  return "ON COUNTER WIN";
            case SubroutineTrigger.OnCounterLose: return "ON COUNTER LOSS";
            case SubroutineTrigger.OnDamageTaken: return "ON DAMAGE TAKEN";
            case SubroutineTrigger.OnAllyFainted: return "WHEN ALLY SHUT DOWN";
            case SubroutineTrigger.OnLowBattery:  return "ON LOW BATTERY (<25%)";
            default: return trigger.ToString().ToUpperInvariant();
        }
    }
}
