using System;
using System.Collections.Generic;

/// <summary>
/// Data-only generated DAG for one run through TheGrid.
/// </summary>
[Serializable]
// Defense note: GridGraph is the main grid graph type used by this part of the project.
public class GridGraph
{
    public int seed;
    public int threatTier = ThreatTierRules.MinTier;
    public int rewardMultiplierPercent = 100;
    public string startNodeId;
    public string bossNodeId;
    public List<GridNode> nodes = new List<GridNode>();

    // Defense note: Runs the max layer helper used by this script.
    public int MaxLayer()
    {
        int maxLayer = 0;
        if (nodes == null)
            return maxLayer;

        for (int i = 0; i < nodes.Count; i++)
        {
            GridNode node = nodes[i];
            if (node != null && node.layer > maxLayer)
                maxLayer = node.layer;
        }

        return maxLayer;
    }

    // Defense note: Retrieves the node value used by this system.
    public GridNode GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].id == nodeId)
                return nodes[i];
        }

        return null;
    }

    // Defense note: Runs the nodes in layer helper used by this script.
    public List<GridNode> NodesInLayer(int layer)
    {
        var result = new List<GridNode>();
        for (int i = 0; i < nodes.Count; i++)
        {
            GridNode node = nodes[i];
            if (node != null && node.layer == layer)
                result.Add(node);
        }

        result.Sort((a, b) => a.indexInLayer.CompareTo(b.indexInLayer));
        return result;
    }
}
