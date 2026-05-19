using System.Collections.Generic;
using System.Linq;

public static class GridValidator
{
    public static GridValidationResult Validate(GridGraph graph)
    {
        var result = new GridValidationResult();
        if (graph == null)
        {
            result.AddError("Graph is null.");
            return result;
        }

        if (graph.nodes == null || graph.nodes.Count == 0)
        {
            result.AddError("Graph has no nodes.");
            return result;
        }

        Dictionary<string, GridNode> byId = BuildNodeLookup(graph, result);
        if (!byId.ContainsKey(graph.startNodeId))
            result.AddError("Start node id is missing or does not exist.");
        if (!byId.ContainsKey(graph.bossNodeId))
            result.AddError("Boss node id is missing or does not exist.");

        ValidateSpecialNodes(graph, byId, result);
        ValidateEdges(byId, result);
        ValidateReachability(graph, byId, result);

        return result;
    }

    public static bool IsBossReachable(GridGraph graph)
    {
        if (graph == null || string.IsNullOrEmpty(graph.bossNodeId))
            return false;

        HashSet<string> reachable = GetReachableNodeIds(graph);
        return reachable.Contains(graph.bossNodeId);
    }

    public static HashSet<string> GetReachableNodeIds(GridGraph graph)
    {
        var reachable = new HashSet<string>();
        if (graph == null || graph.nodes == null || string.IsNullOrEmpty(graph.startNodeId))
            return reachable;

        Dictionary<string, GridNode> byId = graph.nodes
            .Where(node => node != null && !string.IsNullOrEmpty(node.id))
            .GroupBy(node => node.id)
            .ToDictionary(group => group.Key, group => group.First());

        if (!byId.ContainsKey(graph.startNodeId))
            return reachable;

        var queue = new Queue<string>();
        queue.Enqueue(graph.startNodeId);
        reachable.Add(graph.startNodeId);

        while (queue.Count > 0)
        {
            string currentId = queue.Dequeue();
            GridNode current = byId[currentId];
            if (current.outgoingNodeIds == null)
                continue;

            for (int i = 0; i < current.outgoingNodeIds.Count; i++)
            {
                string targetId = current.outgoingNodeIds[i];
                if (string.IsNullOrEmpty(targetId) || !byId.ContainsKey(targetId))
                    continue;
                if (reachable.Add(targetId))
                    queue.Enqueue(targetId);
            }
        }

        return reachable;
    }

    private static Dictionary<string, GridNode> BuildNodeLookup(GridGraph graph, GridValidationResult result)
    {
        var byId = new Dictionary<string, GridNode>();
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            GridNode node = graph.nodes[i];
            if (node == null)
            {
                result.AddError($"Node at index {i} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.id))
            {
                result.AddError($"Node at index {i} has no id.");
                continue;
            }

            if (byId.ContainsKey(node.id))
            {
                result.AddError($"Duplicate node id: {node.id}.");
                continue;
            }

            byId.Add(node.id, node);
        }

        return byId;
    }

    private static void ValidateSpecialNodes(
        GridGraph graph,
        Dictionary<string, GridNode> byId,
        GridValidationResult result)
    {
        int startCount = byId.Values.Count(node => node.nodeType == NodeType.Start);
        if (startCount != 1)
            result.AddError($"Expected exactly one Start node, found {startCount}.");

        int bossCount = byId.Values.Count(node => node.nodeType == NodeType.Boss);
        if (bossCount != 1)
            result.AddError($"Expected exactly one Boss node, found {bossCount}.");

        if (byId.TryGetValue(graph.startNodeId, out GridNode start))
        {
            if (start.layer != 0)
                result.AddError("Start node must be in layer 0.");
            if (start.nodeType != NodeType.Start)
                result.AddError("startNodeId must point to the Start node.");
        }

        if (byId.TryGetValue(graph.bossNodeId, out GridNode boss))
        {
            int maxLayer = byId.Values.Max(node => node.layer);
            if (boss.layer != maxLayer)
                result.AddError("Boss node must be in the final layer.");
            if (boss.nodeType != NodeType.Boss)
                result.AddError("bossNodeId must point to the Boss node.");
            if (boss.outgoingNodeIds != null && boss.outgoingNodeIds.Count > 0)
                result.AddError("Boss node must not have outgoing edges.");
        }
    }

    private static void ValidateEdges(Dictionary<string, GridNode> byId, GridValidationResult result)
    {
        foreach (GridNode node in byId.Values)
        {
            if (node.outgoingNodeIds == null)
                continue;

            var seenTargets = new HashSet<string>();
            for (int i = 0; i < node.outgoingNodeIds.Count; i++)
            {
                string targetId = node.outgoingNodeIds[i];
                if (string.IsNullOrWhiteSpace(targetId))
                {
                    result.AddError($"Node {node.id} has an empty outgoing edge.");
                    continue;
                }

                if (!seenTargets.Add(targetId))
                    result.AddError($"Node {node.id} has duplicate outgoing edge to {targetId}.");

                if (!byId.TryGetValue(targetId, out GridNode target))
                {
                    result.AddError($"Node {node.id} points to missing node {targetId}.");
                    continue;
                }

                if (target.layer <= node.layer)
                    result.AddError($"Node {node.id} has non-forward edge to {targetId}.");
            }
        }
    }

    private static void ValidateReachability(
        GridGraph graph,
        Dictionary<string, GridNode> byId,
        GridValidationResult result)
    {
        HashSet<string> reachable = GetReachableNodeIds(graph);
        if (!reachable.Contains(graph.bossNodeId))
            result.AddError("Boss node is not reachable from Start.");

        foreach (GridNode node in byId.Values)
        {
            if (node.id == graph.startNodeId)
                continue;
            if (!reachable.Contains(node.id))
                result.AddError($"Node {node.id} is not reachable from Start.");
        }

        foreach (string nodeId in reachable)
        {
            GridNode node = byId[nodeId];
            if (node.id == graph.bossNodeId)
                continue;
            if (node.outgoingNodeIds == null || node.outgoingNodeIds.Count == 0)
                result.AddError($"Reachable non-final node {node.id} has no outgoing edge.");
        }
    }
}
