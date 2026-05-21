using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Singleton that owns all cross-scene game state.
/// Persists across scene loads via DontDestroyOnLoad.
///
/// Responsibilities:
///   - Payload: the full warehouse of all captured AlgoMons (no size cap,
///     sorted via QuickSort in the Lab)
///   - Party: the active squad taken into a run (max 4 slots)
///   - Track current run state (active node, current opponent)
///   - Drive scene transitions via EventBus
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Payload — Full Warehouse (all captured AlgoMons)")]
    public List<AlgoMonInstance> payload = new List<AlgoMonInstance>();

    [Header("Party — Active Squad (max 4 for current run)")]
    public List<AlgoMonInstance> party = new List<AlgoMonInstance>();
    public const int MaxPartySize = 4;

    [Header("Run State")]
    public string currentNodeId;
    public int currentRunSeed;
    public GridGraph currentRunGraph;
    public List<string> visitedNodeIds = new List<string>();
    public AlgoMonInstance currentOpponent;
    public bool IsRunActive { get; private set; }

    [Header("Run Result")]
    public RunOutcome pendingRunOutcome = RunOutcome.None;
    public int completedRunSeed;
    public string completedRunNodeId;
    public NodeType completedRunNodeType;
    public int completedRunVisitedCount;

    // ----------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GameManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject managerObject = new GameObject(nameof(GameManager));
        return managerObject.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SubscribePersistentEvents();
    }

    private void OnDestroy()
    {
        UnsubscribePersistentEvents();
    }

    // ----------------------------------------------------------------
    // Payload management (warehouse — no cap)

    public void AddToPayload(AlgoMonInstance mon)
    {
        payload.Add(mon);
    }

    public void RemoveFromPayload(AlgoMonInstance mon)
    {
        payload.Remove(mon);
    }

    // ----------------------------------------------------------------
    // Party management (active squad — max 4)

    public bool AddToParty(AlgoMonInstance mon)
    {
        if (party.Count >= MaxPartySize) return false;
        party.Add(mon);
        return true;
    }

    public void RemoveFromParty(AlgoMonInstance mon)
    {
        party.Remove(mon);
    }

    // ----------------------------------------------------------------
    // Run lifecycle

    public void BeginRun()
    {
        int seed = (int)(DateTime.UtcNow.Ticks & int.MaxValue);
        BeginRun(seed);
    }

    public void BeginRun(int seed)
    {
        BeginRun(seed, null);
    }

    public void BeginRun(int seed, GridGenerationSettings gridSettings)
    {
        ClearRunResult();

        GridGraph graph = new GridGenerator(gridSettings).Generate(seed);

        IsRunActive = true;
        currentRunSeed = seed;
        currentRunGraph = graph;
        currentNodeId = graph.startNodeId;
        visitedNodeIds.Clear();
        visitedNodeIds.Add(currentNodeId);
        currentOpponent = null;
    }

    public void EndRun()
    {
        IsRunActive = false;
        currentRunSeed = 0;
        currentRunGraph = null;
        currentNodeId = string.Empty;
        visitedNodeIds.Clear();
        currentOpponent = null;
    }

    public void ClearRunResult()
    {
        pendingRunOutcome = RunOutcome.None;
        completedRunSeed = 0;
        completedRunNodeId = string.Empty;
        completedRunNodeType = NodeType.Start;
        completedRunVisitedCount = 0;
    }

    public bool TrySelectRunNode(string nodeId)
    {
        if (!IsNodeAvailable(nodeId))
            return false;

        if (!visitedNodeIds.Contains(nodeId))
            visitedNodeIds.Add(nodeId);

        currentNodeId = nodeId;
        return true;
    }

    public List<string> GetAvailableNodeIds()
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(currentNodeId))
            return new List<string>();

        GridNode current = currentRunGraph.GetNode(currentNodeId);
        if (current == null || current.outgoingNodeIds == null)
            return new List<string>();

        var available = new List<string>(current.outgoingNodeIds);
        if (current.nodeType == NodeType.Reboot &&
            !string.IsNullOrEmpty(currentRunGraph.startNodeId) &&
            !available.Contains(currentRunGraph.startNodeId))
        {
            available.Add(currentRunGraph.startNodeId);
        }

        return available;
    }

    public bool IsNodeAvailable(string nodeId)
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(nodeId))
            return false;
        if (currentRunGraph.GetNode(nodeId) == null)
            return false;

        List<string> available = GetAvailableNodeIds();
        return available.Contains(nodeId);
    }

    public bool IsNodeVisited(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && visitedNodeIds.Contains(nodeId);
    }

    public bool TryRegisterCapture(AlgoMonInstance mon, out AlgoMonInstance captured)
    {
        captured = null;
        if (!CanPersistCapture(mon))
            return false;

        captured = mon.Clone();
        captured.usesTransientData = false;
        captured.EnsureKnownSkillsFromLearnset();
        AddToPayload(captured);
        return true;
    }

    public AlgoMonInstance RegisterCapture(AlgoMonInstance mon)
    {
        TryRegisterCapture(mon, out AlgoMonInstance captured);
        return captured;
    }

    private static bool CanPersistCapture(AlgoMonInstance mon)
    {
        if (mon == null || mon.data == null || mon.usesTransientData)
            return false;

#if UNITY_EDITOR
        return AssetDatabase.Contains(mon.data);
#else
        return true;
#endif
    }

    private void OnNodeSelected(NodeSelectedEvent e)
    {
        if (!IsRunActive || e.Node == null)
            return;

        if (!IsEncounterNode(e.Type))
        {
            currentOpponent = null;
            return;
        }

        currentOpponent = EncounterFactory.Create(currentRunSeed, e.Node);
        GoTo(GameScene.TheArena);
    }

    private void OnBattleEnd(BattleEndEvent e)
    {
        if (!IsRunActive)
            return;

        GridNode completedNode = CurrentRunNode();
        currentOpponent = null;

        if (e.PlayerWon && completedNode != null && completedNode.nodeType != NodeType.Boss)
        {
            GoTo(GameScene.TheGrid);
            return;
        }

        RecordRunResult(e.PlayerWon ? RunOutcome.Victory : RunOutcome.Defeat, completedNode);
        EndRun();
        GoTo(GameScene.RunResult);
    }

    private GridNode CurrentRunNode()
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(currentNodeId))
            return null;

        return currentRunGraph.GetNode(currentNodeId);
    }

    private void RecordRunResult(RunOutcome outcome, GridNode completedNode)
    {
        pendingRunOutcome = outcome;
        completedRunSeed = currentRunSeed;
        completedRunNodeId = completedNode != null ? completedNode.id : currentNodeId;
        completedRunNodeType = completedNode != null ? completedNode.nodeType : NodeType.Start;
        completedRunVisitedCount = visitedNodeIds != null ? visitedNodeIds.Count : 0;
    }

    private static bool IsEncounterNode(NodeType type)
    {
        return type == NodeType.Combat || type == NodeType.Elite || type == NodeType.Boss;
    }

    // ----------------------------------------------------------------
    // Scene transitions

    private void SubscribePersistentEvents()
    {
        EventBus.Unsubscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Unsubscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Unsubscribe<BattleEndEvent>(OnBattleEnd);

        EventBus.Subscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Subscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Subscribe<BattleEndEvent>(OnBattleEnd);
    }

    private void UnsubscribePersistentEvents()
    {
        EventBus.Unsubscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Unsubscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Unsubscribe<BattleEndEvent>(OnBattleEnd);
    }

    private void OnSceneTransition(SceneTransitionEvent e)
    {
        EventBus.Clear();
        SubscribePersistentEvents();
        SceneManager.LoadScene(e.Destination.ToString());
    }

    /// <summary>Convenience wrapper so other systems don't need to know scene names.</summary>
    public static void GoTo(GameScene destination)
    {
        EventBus.Publish(new SceneTransitionEvent { Destination = destination });
    }
}
