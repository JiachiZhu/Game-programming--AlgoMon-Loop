/*
Script Audit:
- Purpose: Stores tunable settings for procedural route-map generation.
- Attached GameObject: None; this serializable class is embedded in GridMapController debug settings or passed to GridGenerator.
- Main responsibilities: Define layer count, node counts, outgoing edge limits, generation attempts, and node type weights.
- Important variables: totalLayers, minIntermediateNodes, maxIntermediateNodes, minOutgoingEdges, maxOutgoingEdges, combatWeight, hackerWeight, eliteWeight, shopWeight, rebootWeight.
- Inputs: Inspector values or code-created settings.
- Outputs or effects: CloneNormalized returns safe values used by GridGenerator.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Change weights and layer counts, then generate a run and confirm the map shape changes but remains valid.
*/
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
    [Min(0)] public int combatWeight = 60;
    [Min(0)] public int hackerWeight = 10;
    [Min(0)] public int eliteWeight = 15;
    [Min(0)] public int shopWeight = 10;
    [Min(0)] public int rebootWeight = 5;

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
            hackerWeight = Mathf.Max(0, hackerWeight),
            eliteWeight = Mathf.Max(0, eliteWeight),
            shopWeight = Mathf.Max(0, shopWeight),
            rebootWeight = Mathf.Max(0, rebootWeight)
        };
    }
}
