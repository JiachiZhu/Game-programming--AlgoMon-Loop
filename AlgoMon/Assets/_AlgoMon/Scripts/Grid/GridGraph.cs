using System;
using System.Collections.Generic;

/// <summary>
/// Data-only generated DAG for one run through TheGrid.
/// </summary>
[Serializable]
public class GridGraph
{
    public int seed;
    public string startNodeId;
    public string bossNodeId;
    public List<GridNode> nodes = new List<GridNode>();

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
