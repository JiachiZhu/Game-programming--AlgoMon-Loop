/// <summary>
/// Central registry of all game-wide event structs.
/// Each struct is a plain data container — no logic, no dependencies.
/// </summary>

// --- Battle Events ---

public struct DamageEvent
{
    public string TargetId;
    public int Amount;
    public DamageType Type;
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

public enum DamageType  { Computing, Throughput }
public enum StatusType  { Overclock, Throttle, Firewall, Corrupted }
public enum NodeType    { Combat, Elite, Rest, Shop, Boss }
public enum GameScene   { MainTerminal, TheGrid, TheArena, TheLab }
