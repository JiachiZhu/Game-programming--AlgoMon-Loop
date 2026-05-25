using System;
using System.Collections.Generic;

/// <summary>
/// Serializable route-map node. Edges are stored as forward outgoing ids only.
/// </summary>
[Serializable]
public class GridNode
{
    public string id;
    public int layer;
    public int indexInLayer;
    public NodeType nodeType;
    public EncounterDepthBand depthBand = EncounterDepthBand.None;
    public int encounterLevel;
    public int dangerRating;
    public List<string> outgoingNodeIds = new List<string>();

    public GridNode()
    {
    }

    public GridNode(string id, int layer, int indexInLayer, NodeType nodeType)
    {
        this.id = id;
        this.layer = layer;
        this.indexInLayer = indexInLayer;
        this.nodeType = nodeType;
    }
}
