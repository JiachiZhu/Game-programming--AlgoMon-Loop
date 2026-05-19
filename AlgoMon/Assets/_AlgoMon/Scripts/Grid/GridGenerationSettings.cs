using System;
using UnityEngine;

/// <summary>
/// Tunable parameters for Sprint 3 route-map generation.
/// totalLayers includes the Start layer and the Boss layer.
/// </summary>
[Serializable]
public class GridGenerationSettings
{
    [Min(3)] public int totalLayers = 7;
    [Min(1)] public int minIntermediateNodes = 1;
    [Min(1)] public int maxIntermediateNodes = 4;
    [Min(1)] public int minOutgoingEdges = 1;
    [Min(1)] public int maxOutgoingEdges = 3;
    [Min(1)] public int maxGenerationAttempts = 10;

    [Header("Node Type Weights")]
    [Min(0)] public int combatWeight = 70;
    [Min(0)] public int eliteWeight = 15;
    [Min(0)] public int restWeight = 10;
    [Min(0)] public int shopWeight = 5;

    public GridGenerationSettings CloneNormalized()
    {
        return new GridGenerationSettings
        {
            totalLayers = Mathf.Max(3, totalLayers),
            minIntermediateNodes = Mathf.Max(1, minIntermediateNodes),
            maxIntermediateNodes = Mathf.Max(Mathf.Max(1, minIntermediateNodes), maxIntermediateNodes),
            minOutgoingEdges = Mathf.Max(1, minOutgoingEdges),
            maxOutgoingEdges = Mathf.Max(Mathf.Max(1, minOutgoingEdges), maxOutgoingEdges),
            maxGenerationAttempts = Mathf.Max(1, maxGenerationAttempts),
            combatWeight = Mathf.Max(0, combatWeight),
            eliteWeight = Mathf.Max(0, eliteWeight),
            restWeight = Mathf.Max(0, restWeight),
            shopWeight = Mathf.Max(0, shopWeight)
        };
    }
}
