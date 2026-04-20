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
