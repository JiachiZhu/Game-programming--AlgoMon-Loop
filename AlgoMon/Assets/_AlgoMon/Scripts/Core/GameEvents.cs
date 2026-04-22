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

public enum DamageType     { Computing, Throughput }
public enum StatusType     { Overclock, Throttle, Corrupted }
public enum NodeType       { Combat, Elite, Rest, Shop, Boss }
public enum GameScene      { MainTerminal, TheGrid, TheArena, TheLab }

/// <summary>
/// The three stances in the ASD combat triangle.
///   Attack  beats Status   (A > S)
///   Status  beats Defense  (S > D)
///   Defense beats Attack   (D > A)
/// </summary>
public enum InstructionType { Attack, Status, Defense }

/// <summary>
/// Six elemental types. Effectiveness is resolved via a 6x6 matrix
/// in CombatResolver. Strong = x1.5, Neutral = x1.0, Weak = x0.75.
///
/// Stated advantages:
///   Water > Fire,  Fire > Grass,  Grass > Water  (triangle)
///   Electric > Water,  Ground > Electric          (chain)
///   Ice > Grass,  Fire > Ice                      (extras)
/// </summary>
public enum ElementType    { Water, Fire, Grass, Ice, Electric, Ground }

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
