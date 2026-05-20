using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GridGenerator
{
    private const string StartNodeId = "start";
    private const string BossNodeId = "boss";

    private GridGenerationSettings settings;
    private System.Random rng;

    public GridGenerator()
        : this(new GridGenerationSettings())
    {
    }

    public GridGenerator(GridGenerationSettings settings)
    {
        this.settings = settings != null
            ? settings.CloneNormalized()
            : new GridGenerationSettings().CloneNormalized();
    }

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
        ConnectLayers(graph);
        return graph;
    }

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

        return sizes;
    }

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
    }

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

    private static void AddEdge(Dictionary<GridNode, HashSet<string>> edges, GridNode parent, GridNode child)
    {
        if (parent == null || child == null)
            return;

        edges[parent].Add(child.id);
    }

    private NodeType RollEncounterNodeType()
    {
        int totalWeight = settings.combatWeight
            + settings.eliteWeight
            + settings.shopWeight
            + settings.rebootWeight;

        if (totalWeight <= 0)
            return NodeType.Combat;

        int roll = rng.Next(totalWeight);
        if (roll < settings.combatWeight)
            return NodeType.Combat;
        roll -= settings.combatWeight;

        if (roll < settings.eliteWeight)
            return NodeType.Elite;
        roll -= settings.eliteWeight;

        if (roll < settings.shopWeight)
            return NodeType.Shop;

        return NodeType.Reboot;
    }

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

    private T PickRandom<T>(IReadOnlyList<T> items)
    {
        return items[rng.Next(items.Count)];
    }

    private int NextInclusive(int min, int max)
    {
        if (max <= min)
            return min;
        return rng.Next(min, max + 1);
    }

}
