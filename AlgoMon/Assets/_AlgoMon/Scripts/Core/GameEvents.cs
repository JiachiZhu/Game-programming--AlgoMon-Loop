/// <summary>
/// Central registry of all game-wide event structs.
/// Each struct is a plain data container — no logic, no dependencies.
/// </summary>

// --- Battle Events ---

// Defense note: DamageEvent is a lightweight event payload passed through EventBus.
public struct DamageEvent
{
    public string AttackerId;
    public string TargetId;
    public int Amount;
    public DamageType DmgType;
    public ElementType SkillElement;
    public ElementType TargetElement;
    public float ElementMultiplier;
}

// Defense note: CounterEvent is a lightweight event payload passed through EventBus.
public struct CounterEvent
{
    public string CounterId;    // unit that won the ASD counter
    public string CounteredId;  // unit that lost
    public bool CounterHasDamage;
    public bool CounteredHasDamage;
    // True when the loser's action gets nullified: it will never emit a
    // BattleActionEvent, so the presentation must not arm a suppression for it.
    public bool CounteredCancelled;
    public InstructionType CounterInstructionType;
    public InstructionType CounteredInstructionType;
}

// Defense note: BattleEndEvent is a lightweight event payload passed through EventBus.
public struct BattleEndEvent
{
    public bool PlayerWon;
}

/// <summary>
/// Raised when a unit's Subroutine (passive) fires. The presentation layer pops
/// an above-sprite callout so the player sees the passive act, not just a log line.
/// </summary>
// Defense note: SubroutineTriggeredEvent is a lightweight event payload passed through EventBus.
public struct SubroutineTriggeredEvent
{
    public string OwnerId;          // presentation id of the unit whose subroutine fired
    public string SubroutineName;
}

// Defense note: BattleActionEvent is a lightweight event payload passed through EventBus.
public struct BattleActionEvent
{
    public string ActorId;
    public string ActorName;
    public string TargetId;
    public string SkillName;
    public InstructionType InstructionType;
    public bool WonCounter;
    public bool WasCountered;
}

// Defense note: UnitFaintedEvent is a lightweight event payload passed through EventBus.
public struct UnitFaintedEvent
{
    public string UnitId;
}

// Defense note: StatusAppliedEvent is a lightweight event payload passed through EventBus.
public struct StatusAppliedEvent
{
    public string SourceId;
    public string TargetId;
    public StatusType Status;
    public int Stacks;
    public StatusDurationType DurationType;
    public int Duration;
}

// Defense note: BattleFeedbackEvent is a lightweight event payload passed through EventBus.
public struct BattleFeedbackEvent
{
    public string TargetId;
    public BattleFeedbackType Type;
    public int Amount;
    public string Label;
}

// --- Navigation Events ---

// Defense note: NodeSelectedEvent is a lightweight event payload passed through EventBus.
public struct NodeSelectedEvent
{
    public string NodeId;
    public NodeType Type;
    public GridNode Node;
    public bool WasVisited;
    public bool IsFirstVisit;
    public bool ReturnedToStart;
}

// Defense note: SceneTransitionEvent is a lightweight event payload passed through EventBus.
public struct SceneTransitionEvent
{
    public GameScene Destination;
}

// --- Enums ---

// Defense note: DamageType defines the valid damage type options used by the gameplay systems.
public enum DamageType { None, Computing, Throughput }

// Defense note: BattleFeedbackType defines the valid battle feedback type options used by the gameplay systems.
public enum BattleFeedbackType { Damage, Heal, CPGain, CPDrain, Status, Counter }

/// <summary>
/// Who receives a status effect — the user (Self) or the opponent.
/// Used by baseStatusTarget, and by BattleManager when interpreting
/// counterSelfStatus on skills marked as opponent-targeted in their description.
/// </summary>
// Defense note: StatusTarget defines the valid status target options used by the gameplay systems.
public enum StatusTarget { Self, Opponent }

/// <summary>
/// How long a status effect lasts.
///   Permanent   — survives AlgoMon swaps; removed only at battle end.
///   WhileOnField — cleared immediately when the AlgoMon is swapped out;
///                  no turn countdown while on field.
///   Turns        — counts down each turn; also cleared on swap.
///                  Use StatusDuration field to set the turn count.
/// </summary>
// Defense note: StatusDurationType defines the valid status duration type options used by the gameplay systems.
public enum StatusDurationType { Permanent, WhileOnField, Turns }
// Defense note: NodeType defines the valid node type options used by the gameplay systems.
public enum NodeType
{
    Combat,
    Elite,
    Rest, // Legacy value kept for serialized data compatibility; new graphs do not generate Rest nodes.
    Shop,
    Boss,
    Start,
    Reboot,
    Hacker
}
// Defense note: GameScene defines the valid game scene options used by the gameplay systems.
public enum GameScene  { MainTerminal, TheGrid, TheArena, RunResult, TheLab }
// Defense note: RunOutcome defines the valid run outcome options used by the gameplay systems.
public enum RunOutcome { None, Victory, Defeat }

/// <summary>
/// All persistent status conditions that can be applied to an AlgoMon.
/// Stack values and durations are tracked by BattleManager at runtime.
///
/// Stacking model (additive percentage per layer):
///   Burn    — each layer deals 2% max-Battery damage at round end, then halves. No stack cap.
///   Leech   — each layer steals 3% max-Battery HP per turn from target to user. Max 3 layers.
///   Freeze  — each layer reduces ClockSpeed by 15% and adds +1 CP cost
///             to normal skills. Recharge remains free so the player cannot
///             be locked out of CP recovery.
///             Max 3 layers; cleared by swap or special skills.
///   Ensnare — target cannot swap out for duration turns.
///   Concurrent — next skill can execute twice; each execution pays CP separately.
///   BufferLoad — next skill CP cost -4 (min 0); max 1; clears after activation.
///   Backup  — (removed from Redundant Backup; reserved for future use)
///
/// When adding a new temporary debuff, update
/// BattleStatusSet.ClearTemporaryDebuffs so cleanse effects can remove it.
///
/// Legacy placeholders (from initial design, may be repurposed):
///   Overclock, Throttle, Corrupted
/// </summary>
// Defense note: StatusType defines the valid status type options used by the gameplay systems.
public enum StatusType
{
    // --- Active debuffs ---
    Burn,           // 2% max-HP damage per layer at round end, then stacks halve; no cap
    Freeze,         // -15% ClockSpeed/layer and +1 CP cost/layer to normal skills, max 3 layers
    Leech,          // 3% max-HP stolen/turn per layer (heals caster), max 3 layers
    Ensnare,        // cannot swap out AlgoMon for N turns

    // --- Self buffs (one-shot, clear on trigger) ---
    Concurrent,     // next skill can fire twice if CP remains for the repeat
    BufferLoad,     // next skill CP cost -4 (min 0), max 1

    // --- Stat buffs (additive %, stacks persist until battle end) ---
    ComputingUp,    // Computing Power +12% per stack
    ThroughputUp,   // Throughput +12% per stack
    FirewallUp,     // Firewall +10% per stack
    EncryptionUp,   // Encryption +10% per stack

    // --- Legacy / reserved ---
    Overclock,      // placeholder — speed boost (to be fully defined)
    Throttle,       // placeholder — speed reduction (may merge with Freeze)
    Corrupted,      // placeholder — general debuff state
}

/// <summary>
/// The three stances in the ASD combat triangle.
///   Attack  beats Status   (A > S)
///   Status  beats Defense  (S > D)
///   Defense beats Attack   (D > A)
/// </summary>
// Defense note: InstructionType defines the valid instruction type options used by the gameplay systems.
public enum InstructionType { Attack, Status, Defense }

/// <summary>
/// Six elemental types plus Normal (neutral). Effectiveness is resolved via a 6x6 matrix
/// in CombatResolver. Strong = x1.5, Neutral = x1.0, Weak = x0.75.
/// Normal-type skills are neutral against all elements (no entry in ElementChart needed).
///
/// Stated advantages:
///   Water > Fire,  Fire > Grass,  Grass > Water  (triangle)
///   Electric > Water,  Ground > Electric          (chain)
///   Ice > Grass,  Fire > Ice                      (extras)
/// </summary>
// Defense note: ElementType defines the valid element type options used by the gameplay systems.
public enum ElementType    { Normal, Water, Fire, Grass, Ice, Electric, Ground }

/// <summary>
/// Conditions that activate a species' built-in Subroutine (passive ability).
/// BattleManager checks these each time the relevant moment occurs.
/// </summary>
// Defense note: SubroutineTrigger defines the valid subroutine trigger options used by the gameplay systems.
public enum SubroutineTrigger
{
    OnBattleStart,      // activates once when the battle begins
    OnTurnStart,        // activates at the start of this unit's turn
    OnCounterWin,       // activates when this unit wins an ASD counter
    OnCounterLose,      // activates when this unit loses an ASD counter
    OnDamageTaken,      // activates after this unit takes damage
    OnAllyFainted,      // activates when a party ally is shut down
    OnLowBattery,       // activates when Battery drops below 25%
}
