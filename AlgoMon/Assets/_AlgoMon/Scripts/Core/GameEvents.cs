/// <summary>
/// Central registry of all game-wide event structs.
/// Each struct is a plain data container — no logic, no dependencies.
/// </summary>

// --- Battle Events ---

public struct DamageEvent
{
    public string AttackerId;
    public string TargetId;
    public int Amount;
    public DamageType DmgType;
    public ElementType SkillElement;
}

public struct CounterEvent
{
    public string CounterId;    // unit that won the ASD counter
    public string CounteredId;  // unit that lost
}

public struct BattleEndEvent
{
    public bool PlayerWon;
}

public struct StatusAppliedEvent
{
    public string TargetId;
    public StatusType Status;
    public int Duration;
}

// --- Navigation Events ---

public struct NodeSelectedEvent
{
    public string NodeId;
    public NodeType Type;
}

public struct SceneTransitionEvent
{
    public GameScene Destination;
}

// --- Enums ---

public enum DamageType { None, Computing, Throughput }

/// <summary>
/// How long a status effect lasts.
///   Permanent   — survives AlgoMon swaps; removed only at battle end.
///   WhileOnField — cleared immediately when the AlgoMon is swapped out;
///                  no turn countdown while on field.
///   Turns        — counts down each turn; also cleared on swap.
///                  Use StatusDuration field to set the turn count.
/// </summary>
public enum StatusDurationType { Permanent, WhileOnField, Turns }
public enum NodeType   { Combat, Elite, Rest, Shop, Boss }
public enum GameScene  { MainTerminal, TheGrid, TheArena, TheLab }

/// <summary>
/// All persistent status conditions that can be applied to an AlgoMon.
/// Stack values and durations are tracked by BattleManager at runtime.
///
/// Stacking model (additive percentage per layer):
///   Burn    — each layer deals 5% max-Battery damage per turn. Max 4 layers.
///   Leech   — each layer steals 5% max-Battery HP per turn from target to user. Max 3 layers.
///   Freeze  — each layer reduces ClockSpeed by 15%. Max 3 layers (-45% total).
///             Cleared only by turn-end roll; NOT cleared by Fire-type hits.
///   Ensnare — target cannot swap out for duration turns.
///   Concurrent — next skill executes twice (costs 2x CP); clears after activation.
///   BufferLoad — next skill CP cost -4 (min 0); clears after activation.
///   Backup  — (removed from Redundant Backup; reserved for future use)
///
/// Legacy placeholders (from initial design, may be repurposed):
///   Overclock, Throttle, Corrupted
/// </summary>
public enum StatusType
{
    // --- Active debuffs ---
    Burn,           // 5% max-HP damage/turn per layer, max 4 layers
    Freeze,         // -15% ClockSpeed/layer, max 3 layers; cleared by turn-end roll
    Leech,          // 5% max-HP stolen/turn per layer (heals caster), max 3 layers
    Ensnare,        // cannot swap out AlgoMon for N turns

    // --- Self buffs (one-shot, clear on trigger) ---
    Concurrent,     // next skill fires twice (uses 2x CP)
    BufferLoad,     // next skill CP cost -4 (min 0)

    // --- Stat buffs (additive %, stacks persist until battle end) ---
    ComputingUp,    // Computing Power +10% per stack
    ThroughputUp,   // Throughput +10% per stack
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
public enum ElementType    { Normal, Water, Fire, Grass, Ice, Electric, Ground }

/// <summary>
/// The additional effect that occurs when THIS skill wins the ASD counter check.
/// Counter effects are per-skill, not tied to instruction type — an Attack skill
/// may have Nullify, None, or any other effect depending on its design.
///
///   None     — no special counter effect; opponent acts after (ForceAfter), CP consumed.
///              counterSuccessMultiplier still applies to this skill's damage.
///   Nullify  — opponent's skill is fully cancelled; their CP is NOT consumed, turn wasted.
///   Block    — opponent's attack still executes but damage is reduced by counterBlockPercent.
///              Typical for Defense skills (all Defense skills must have canCounter = true).
///   SelfBuff — apply an additional buff to self on top of the skill's base effect.
///              Magnitude = counterBonusValue stacks/points of counterSelfStatus.
///
/// Note: ASD check only fires when the acting skill has canCounter = true AND
/// its instructionType wins against the opponent's instructionType (A>S, S>D, D>A).
/// If canCounter = false, turn order is resolved by speed/priority only.
/// </summary>
public enum CounterSuccessType { None, Nullify, Block, SelfBuff }

/// <summary>
/// Conditions that activate a species' built-in Subroutine (passive ability).
/// BattleManager checks these each time the relevant moment occurs.
/// </summary>
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

/// <summary>
/// Effects applied when a Subroutine triggers.
/// BattleManager maps these to concrete stat/priority modifications.
/// </summary>
public enum SubroutineEffect
{
    PriorityBoost,      // add +value to skill priority this turn
    ComputingBoost,     // multiply Computing Power by value% for one turn
    ThroughputBoost,    // multiply Throughput by value% for one turn
    FirewallBoost,      // multiply Firewall by value% for one turn
    EncryptionBoost,    // multiply Encryption by value% for one turn
    HealSelf,           // restore value% of max Battery
    ApplyStatus,        // apply a StatusType (use statusType field in SubroutineData)
}
