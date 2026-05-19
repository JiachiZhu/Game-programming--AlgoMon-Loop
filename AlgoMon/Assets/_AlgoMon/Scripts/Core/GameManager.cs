using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that owns all cross-scene game state.
/// Persists across scene loads via DontDestroyOnLoad.
///
/// Responsibilities:
///   - Payload: the full warehouse of all captured AlgoMons (no size cap,
///     sorted via QuickSort in the Lab)
///   - Party: the active squad taken into a run (max 6 slots)
///   - Track current run state (active node, current opponent)
///   - Drive scene transitions via EventBus
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Payload — Full Warehouse (all captured AlgoMons)")]
    public List<AlgoMonInstance> payload = new List<AlgoMonInstance>();

    [Header("Party — Active Squad (max 6 for current run)")]
    public List<AlgoMonInstance> party = new List<AlgoMonInstance>();
    public const int MaxPartySize = 6;

    [Header("Run State")]
    public string currentNodeId;
    public int currentRunSeed;
    public GridGraph currentRunGraph;
    public List<string> visitedNodeIds = new List<string>();
    public AlgoMonInstance currentOpponent;
    public bool IsRunActive { get; private set; }

    // ----------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EventBus.Subscribe<SceneTransitionEvent>(OnSceneTransition);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<SceneTransitionEvent>(OnSceneTransition);
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
    // Party management (active squad — max 6)

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

    public bool TrySelectRunNode(string nodeId)
    {
        if (!IsNodeAvailable(nodeId))
            return false;

        currentNodeId = nodeId;
        if (!visitedNodeIds.Contains(nodeId))
            visitedNodeIds.Add(nodeId);
        return true;
    }

    public List<string> GetAvailableNodeIds()
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(currentNodeId))
            return new List<string>();

        GridNode current = currentRunGraph.GetNode(currentNodeId);
        if (current == null || current.outgoingNodeIds == null)
            return new List<string>();

        return new List<string>(current.outgoingNodeIds);
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

    public AlgoMonInstance RegisterCapture(AlgoMonInstance mon)
    {
        if (mon == null)
            return null;

        AlgoMonInstance captured = mon.Clone();
        captured.EnsureKnownSkillsFromLearnset();
        AddToPayload(captured);
        return captured;
    }

    // ----------------------------------------------------------------
    // Scene transitions

    private void OnSceneTransition(SceneTransitionEvent e)
    {
        EventBus.Clear();
        EventBus.Subscribe<SceneTransitionEvent>(OnSceneTransition);
        SceneManager.LoadScene(e.Destination.ToString());
    }

    /// <summary>Convenience wrapper so other systems don't need to know scene names.</summary>
    public static void GoTo(GameScene destination)
    {
        EventBus.Publish(new SceneTransitionEvent { Destination = destination });
    }
}
