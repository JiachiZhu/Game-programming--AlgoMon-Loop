/*
Script Audit:
- Purpose: Procedurally generates one run route graph from a seed.
- Attached GameObject: None; GameManager and GridMapController call this as a normal C# class.
- Main responsibilities: Create start/intermediate/boss nodes, assign node types, connect layers, and retry until validation passes.
- Important variables: settings, rng, StartNodeId, BossNodeId.
- Inputs: Seed integer and GridGenerationSettings.
- Outputs or effects: Returns a validated GridGraph or throws an error if generation fails.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Generate multiple seeds and confirm every graph has a reachable boss and forward-only connections.
*/
using System;
using System.Collections.Generic;
using System.Linq;

// Defense note: GridGenerator generates grid data from settings or seeds.
public sealed class GridGenerator
{
    private const string StartNodeId = "start";
    private const string BossNodeId = "boss";
    private static readonly NodeType[] PreferredVarietyNodeTypes =
    {
        NodeType.Combat,
        NodeType.Hacker,
        NodeType.Elite,
        NodeType.Shop,
        NodeType.Reboot
    };

    private GridGenerationSettings settings;
    private System.Random rng;

    // Defense note: Initializes the GridGenerator instance and its default runtime state.
    public GridGenerator()
        : this(new GridGenerationSettings())
    {
    }

    // Defense note: Initializes the GridGenerator instance and its default runtime state.
    public GridGenerator(GridGenerationSettings settings)
    {
        this.settings = settings != null
            ? settings.CloneNormalized()
            : new GridGenerationSettings().CloneNormalized();
    }

    // Defense note: Runs the generate helper used by this script.
    public GridGraph Generate(int seed)
    {
        rng = new System.Random(seed);
        GridValidationResult lastResult = null;

        for (int attempt = 0; attempt < settings.maxGenerationAttempts; attempt++)
        {
            GridGraph graph = BuildCandidate(seed);
            GridValidationResult result = GridValidator.Validate(graph);
            if (result.IsValid)
                return graph;

            lastResult = result;
        }

        string reason = lastResult != null
            ? string.Join("; ", lastResult.errors)
            : "unknown validation failure";
        throw new InvalidOperationException($"Grid generation failed after {settings.maxGenerationAttempts} attempts: {reason}");
    }

    // Defense note: Builds the candidate data or UI structure.
    private GridGraph BuildCandidate(int seed)
    {
        var graph = new GridGraph
        {
            seed = seed,
            startNodeId = StartNodeId,
            bossNodeId = BossNodeId
        };

        int[] layerSizes = GenerateLayerSizes();
        CreateNodes(graph, layerSizes);
        EnsureHackerNode(graph);
        ConnectLayers(graph);
        return graph;
    }

    // Defense note: Generates the layer sizes content from current settings.
    private int[] GenerateLayerSizes()
    {
        int[] sizes = new int[settings.totalLayers];
        sizes[0] = 1;
        sizes[settings.totalLayers - 1] = 1;

        for (int layer = 1; layer < settings.totalLayers - 1; layer++)
        {
            int maxBySettings = settings.maxIntermediateNodes;
            int maxByIncomingCapacity = Math.Max(1, sizes[layer - 1] * settings.maxOutgoingEdges);
            int maxForLayer = Math.Min(maxBySettings, maxByIncomingCapacity);
            int minForLayer = Math.Min(settings.minIntermediateNodes, maxForLayer);
            sizes[layer] = NextInclusive(minForLayer, maxForLayer);
        }

        if (settings.forceAllNodeTypesForVisualAudit)
            TryEnsurePreferredVarietyNodeBudget(sizes);

        return sizes;
    }

    // Defense note: Adds only enough distributed slots for type variety when the route has room for them.
    private void TryEnsurePreferredVarietyNodeBudget(int[] sizes)
    {
        if (sizes == null || sizes.Length <= 3)
            return;

        int targetIntermediateNodes = Math.Min(
            PreferredVarietyNodeTypes.Length,
            (sizes.Length - 2) * settings.maxIntermediateNodes);
        while (CountIntermediateNodes(sizes) < targetIntermediateNodes)
        {
            int layer = PickLayerWithNodeCapacity(sizes);
            if (layer < 0)
                return;

            sizes[layer]++;
        }
    }

    // Defense note: Counts generated route nodes between the start and boss layers.
    private static int CountIntermediateNodes(int[] sizes)
    {
        int total = 0;
        if (sizes == null)
            return total;

        for (int layer = 1; layer < sizes.Length - 1; layer++)
            total += sizes[layer];
        return total;
    }

    // Defense note: Picks a layer that can accept another node without exceeding incoming edge capacity.
    private int PickLayerWithNodeCapacity(int[] sizes)
    {
        int bestLayer = -1;
        int bestNodeCount = int.MaxValue;
        for (int layer = 1; layer < sizes.Length - 1; layer++)
        {
            int maxBySettings = settings.maxIntermediateNodes;
            int maxByIncomingCapacity = Math.Max(1, sizes[layer - 1] * settings.maxOutgoingEdges);
            int maxForLayer = Math.Min(maxBySettings, maxByIncomingCapacity);
            if (sizes[layer] >= maxForLayer)
                continue;

            if (sizes[layer] < bestNodeCount)
            {
                bestLayer = layer;
                bestNodeCount = sizes[layer];
            }
        }

        return bestLayer;
    }

    // Defense note: Creates the nodes object used by the scene or runtime.
    private void CreateNodes(GridGraph graph, int[] layerSizes)
    {
        graph.nodes.Add(new GridNode(StartNodeId, 0, 0, NodeType.Start));

        for (int layer = 1; layer < layerSizes.Length - 1; layer++)
        {
            for (int index = 0; index < layerSizes[layer]; index++)
            {
                string id = $"L{layer}N{index}";
                graph.nodes.Add(new GridNode(id, layer, index, RollEncounterNodeType()));
            }
        }

        int bossLayer = layerSizes.Length - 1;
        graph.nodes.Add(new GridNode(BossNodeId, bossLayer, 0, NodeType.Boss));

        if (settings.forceAllNodeTypesForVisualAudit)
            ApplyPreferredNodeTypeVariety(graph);
    }

    // Defense note: Nudges generated routes toward type variety without changing the route shape.
    private void ApplyPreferredNodeTypeVariety(GridGraph graph)
    {
        if (graph == null || graph.nodes == null)
            return;

        List<GridNode> candidates = graph.nodes
            .Where(IsSelectableRouteNode)
            .ToList();
        if (candidates.Count == 0)
            return;

        Dictionary<NodeType, int> counts = BuildTypeCounts(candidates);
        List<NodeType> missingTypes = Shuffled(PreferredVarietyNodeTypes)
            .Where(type => counts[type] == 0)
            .ToList();
        if (missingTypes.Count == 0)
            return;

        List<GridNode> replacementCandidates = Shuffled(candidates);
        for (int i = 0; i < missingTypes.Count; i++)
        {
            GridNode replacement = TakeReplacementCandidate(replacementCandidates, counts);
            if (replacement == null)
                return;

            if (counts.ContainsKey(replacement.nodeType))
                counts[replacement.nodeType]--;
            replacement.nodeType = missingTypes[i];
            counts[missingTypes[i]]++;
        }
    }

    // Defense note: Returns whether this route node can receive a normal generated type.
    private static bool IsSelectableRouteNode(GridNode node)
    {
        return node != null &&
               node.nodeType != NodeType.Start &&
               node.nodeType != NodeType.Boss;
    }

    // Defense note: Builds the generated route type counts for variety balancing.
    private static Dictionary<NodeType, int> BuildTypeCounts(List<GridNode> nodes)
    {
        var counts = new Dictionary<NodeType, int>();
        for (int i = 0; i < PreferredVarietyNodeTypes.Length; i++)
            counts[PreferredVarietyNodeTypes[i]] = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            GridNode node = nodes[i];
            if (node != null && counts.ContainsKey(node.nodeType))
                counts[node.nodeType]++;
        }

        return counts;
    }

    // Defense note: Finds a duplicate or non-preferred generated node that can be repurposed for a missing type.
    private static GridNode TakeReplacementCandidate(
        List<GridNode> candidates,
        Dictionary<NodeType, int> counts)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            GridNode node = candidates[i];
            if (node == null)
                continue;

            bool canReplace = !counts.ContainsKey(node.nodeType) || counts[node.nodeType] > 1;
            if (!canReplace)
                continue;

            candidates.RemoveAt(i);
            return node;
        }

        return null;
    }

    // Defense note: Ensures the hacker node dependency or state exists before use.
    public static bool EnsureHackerNode(GridGraph graph, bool preferFirstSelectableLayer = false)
    {
        if (graph == null || graph.nodes == null)
            return false;

        if (!preferFirstSelectableLayer && HasHackerNode(graph))
            return false;

        int bossLayer = graph.MaxLayer();
        if (preferFirstSelectableLayer && !HasHackerNodeInLayer(graph, 1))
        {
            GridNode early = BestHackerCandidate(graph, 1, bossLayer, true);
            if (early != null)
            {
                early.nodeType = NodeType.Hacker;
                return true;
            }
        }

        if (HasHackerNode(graph))
            return false;

        GridNode fallback = BestHackerCandidate(graph, Math.Max(1, bossLayer / 2), bossLayer, false);
        if (fallback == null)
            return false;

        fallback.nodeType = NodeType.Hacker;
        return true;
    }

    // Defense note: Returns whether hacker node exists or is active.
    private static bool HasHackerNode(GridGraph graph)
    {
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            GridNode node = graph.nodes[i];
            if (node != null && node.nodeType == NodeType.Hacker)
                return true;
        }

        return false;
    }

    // Defense note: Returns whether hacker node in layer exists or is active.
    private static bool HasHackerNodeInLayer(GridGraph graph, int layer)
    {
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            GridNode node = graph.nodes[i];
            if (node != null && node.layer == layer && node.nodeType == NodeType.Hacker)
                return true;
        }

        return false;
    }

    // Defense note: Runs the best hacker candidate helper used by this script.
    private static GridNode BestHackerCandidate(
        GridGraph graph,
        int preferredLayer,
        int bossLayer,
        bool requirePreferredLayer)
    {
        GridNode fallback = null;
        int fallbackScore = int.MaxValue;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            GridNode node = graph.nodes[i];
            if (node == null || node.layer <= 0 || node.layer >= bossLayer)
                continue;
            if (requirePreferredLayer && node.layer != preferredLayer)
                continue;
            if (!CanConvertToHacker(node.nodeType))
                continue;

            int score = HackerCandidateTypeScore(node.nodeType) * 100
                + Math.Abs(node.layer - preferredLayer);
            if (fallback == null || score < fallbackScore)
            {
                fallback = node;
                fallbackScore = score;
            }
        }

        return fallback;
    }

    // Defense note: Checks whether convert to hacker is currently allowed.
    private static bool CanConvertToHacker(NodeType nodeType)
    {
        return nodeType != NodeType.Start &&
               nodeType != NodeType.Boss &&
               nodeType != NodeType.Hacker;
    }

    // Defense note: Runs the hacker candidate type score helper used by this script.
    private static int HackerCandidateTypeScore(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Combat:
                return 0;
            case NodeType.Elite:
                return 1;
            case NodeType.Shop:
            case NodeType.Reboot:
                return 2;
            default:
                return 3;
        }
    }

    // Defense note: Runs the connect layers helper used by this script.
    private void ConnectLayers(GridGraph graph)
    {
        int finalLayer = settings.totalLayers - 1;
        for (int layer = 0; layer < finalLayer; layer++)
        {
            List<GridNode> parents = graph.NodesInLayer(layer);
            List<GridNode> children = graph.NodesInLayer(layer + 1);
            ConnectAdjacentLayers(parents, children);
        }
    }

    // Defense note: Runs the connect adjacent layers helper used by this script.
    private void ConnectAdjacentLayers(List<GridNode> parents, List<GridNode> children)
    {
        if (parents.Count == 0 || children.Count == 0)
            return;

        var edges = new Dictionary<GridNode, HashSet<string>>();
        for (int i = 0; i < parents.Count; i++)
            edges[parents[i]] = new HashSet<string>();

        List<GridNode> shuffledChildren = Shuffled(children);
        for (int i = 0; i < shuffledChildren.Count; i++)
        {
            GridNode parent = PickParentWithCapacity(parents, edges);
            AddEdge(edges, parent, shuffledChildren[i]);
        }

        int requiredMin = Math.Min(settings.minOutgoingEdges, children.Count);

        for (int i = 0; i < parents.Count; i++)
        {
            GridNode parent = parents[i];
            while (edges[parent].Count < requiredMin)
                AddEdge(edges, parent, PickRandom(children));
        }

        for (int i = 0; i < parents.Count; i++)
        {
            GridNode parent = parents[i];
            int targetCount = NextInclusive(requiredMin, settings.maxOutgoingEdges);
            targetCount = Math.Min(targetCount, children.Count);

            while (edges[parent].Count < targetCount)
                AddEdge(edges, parent, PickRandom(children));
        }

        for (int i = 0; i < parents.Count; i++)
        {
            GridNode parent = parents[i];
            parent.outgoingNodeIds = edges[parent]
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }
    }

    // Defense note: Runs the pick parent with capacity helper used by this script.
    private GridNode PickParentWithCapacity(
        List<GridNode> parents,
        Dictionary<GridNode, HashSet<string>> edges)
    {
        var candidates = new List<GridNode>();
        for (int i = 0; i < parents.Count; i++)
        {
            GridNode parent = parents[i];
            if (edges[parent].Count < settings.maxOutgoingEdges)
                candidates.Add(parent);
        }

        if (candidates.Count == 0)
            return PickRandom(parents);

        return PickRandom(candidates);
    }

    // Defense note: Adds the edge entry into the target collection or UI.
    private static void AddEdge(Dictionary<GridNode, HashSet<string>> edges, GridNode parent, GridNode child)
    {
        if (parent == null || child == null)
            return;

        edges[parent].Add(child.id);
    }

    // Defense note: Runs the roll encounter node type helper used by this script.
    private NodeType RollEncounterNodeType()
    {
        int totalWeight = settings.combatWeight
            + settings.hackerWeight
            + settings.eliteWeight
            + settings.shopWeight
            + settings.rebootWeight;

        if (totalWeight <= 0)
            return NodeType.Combat;

        int roll = rng.Next(totalWeight);
        if (roll < settings.combatWeight)
            return NodeType.Combat;
        roll -= settings.combatWeight;

        if (roll < settings.hackerWeight)
            return NodeType.Hacker;
        roll -= settings.hackerWeight;

        if (roll < settings.eliteWeight)
            return NodeType.Elite;
        roll -= settings.eliteWeight;

        if (roll < settings.shopWeight)
            return NodeType.Shop;

        return NodeType.Reboot;
    }

    // Defense note: Runs the shuffled helper used by this script.
    private List<T> Shuffled<T>(IReadOnlyList<T> source)
    {
        var result = new List<T>(source);
        for (int i = result.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.Next(i + 1);
            T temp = result[i];
            result[i] = result[swapIndex];
            result[swapIndex] = temp;
        }

        return result;
    }

    // Defense note: Runs the pick random helper used by this script.
    private T PickRandom<T>(IReadOnlyList<T> items)
    {
        return items[rng.Next(items.Count)];
    }

    // Defense note: Runs the next inclusive helper used by this script.
    private int NextInclusive(int min, int max)
    {
        if (max <= min)
            return min;
        return rng.Next(min, max + 1);
    }

}
