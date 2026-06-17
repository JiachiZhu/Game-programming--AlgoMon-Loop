/*
Script Audit:
- Purpose: Stores tunable settings for procedural route-map generation.
- Attached GameObject: None; this serializable class is embedded in GridMapController debug settings or passed to GridGenerator.
- Main responsibilities: Define layer count, node counts, outgoing edge limits, generation attempts, and node type weights.
- Important variables: totalLayers, minIntermediateNodes, maxIntermediateNodes, minOutgoingEdges, maxOutgoingEdges, combatWeight, hackerWeight, eliteWeight, shopWeight, rebootWeight.
- Inputs: Inspector values or code-created settings.
- Outputs or effects: CloneNormalized returns safe values used by GridGenerator.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Change weights and layer counts, then generate a run and confirm the map shape changes but remains valid.
*/
using System;
using UnityEngine;

/// <summary>
/// Tunable parameters for Sprint 3 route-map generation.
/// totalLayers includes the Start layer and the Boss layer.
/// </summary>
[Serializable]
// Defense note: GridGenerationSettings is the main grid generation settings type used by this part of the project.
public class GridGenerationSettings
{
    public const int RuntimeMaxIntermediateNodesPerLayer = 3;

    [Min(3)] public int totalLayers = 7;
    [Min(1)] public int minIntermediateNodes = 1;
    [Min(1)] public int maxIntermediateNodes = RuntimeMaxIntermediateNodesPerLayer;
    [Min(1)] public int minOutgoingEdges = 1;
    [Min(1)] public int maxOutgoingEdges = 3;
    [Min(1)] public int maxGenerationAttempts = 10;

    [Header("Node Type Weights")]
    [Min(0)] public int combatWeight = 60;
    [Min(0)] public int hackerWeight = 10;
    [Min(0)] public int eliteWeight = 15;
    [Min(0)] public int shopWeight = 10;
    [Min(0)] public int rebootWeight = 5;

    [Header("Node Type Variety")]
    [Tooltip("Keeps the old serialized field name, but now only nudges generated nodes toward type variety without forcing layer size.")]
    public bool forceAllNodeTypesForVisualAudit = true;

    // Defense note: Runs the clone for threat tier helper used by this script.
    public static GridGenerationSettings CloneForThreatTier(GridGenerationSettings source, int threatTier, int seed)
    {
        GridGenerationSettings clone = source != null
            ? source.CloneNormalized()
            : new GridGenerationSettings().CloneNormalized();

        clone.totalLayers = TotalLayersForThreatTier(threatTier, seed);
        clone.maxIntermediateNodes = Mathf.Min(clone.maxIntermediateNodes, RuntimeMaxIntermediateNodesPerLayer);
        return clone;
    }

    // Defense note: Runs the total layers for threat tier helper used by this script.
    public static int TotalLayersForThreatTier(int threatTier, int seed)
    {
        int min = MinTotalLayersForThreatTier(threatTier);
        int max = MaxTotalLayersForThreatTier(threatTier);
        if (max <= min)
            return min;

        int span = max - min + 1;
        int hash = seed ^ (threatTier * 73856093);
        return min + ((hash & int.MaxValue) % span);
    }

    // Defense note: Runs the min total layers for threat tier helper used by this script.
    public static int MinTotalLayersForThreatTier(int threatTier)
    {
        int tier = Mathf.Clamp(threatTier, ThreatTierRules.MinTier, ThreatTierRules.MaxTier);
        switch (tier)
        {
            case 1:
                return 3;
            case 2:
                return 4;
            case 3:
                return 5;
            case 4:
                return 5;
            default:
                return 7;
        }
    }

    // Defense note: Runs the max total layers for threat tier helper used by this script.
    public static int MaxTotalLayersForThreatTier(int threatTier)
    {
        int tier = Mathf.Clamp(threatTier, ThreatTierRules.MinTier, ThreatTierRules.MaxTier);
        switch (tier)
        {
            case 1:
                return 4;
            case 2:
                return 5;
            case 3:
                return 6;
            case 4:
                return 7;
            default:
                return 7;
        }
    }

    // Defense note: Runs the total layer range label helper used by this script.
    public static string TotalLayerRangeLabel(int threatTier)
    {
        int min = MinTotalLayersForThreatTier(threatTier);
        int max = MaxTotalLayersForThreatTier(threatTier);
        return min == max ? $"{min}" : $"{min}-{max}";
    }

    // Defense note: Runs the clone normalized helper used by this script.
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
            rebootWeight = Mathf.Max(0, rebootWeight),
            forceAllNodeTypesForVisualAudit = forceAllNodeTypesForVisualAudit
        };
    }
}
