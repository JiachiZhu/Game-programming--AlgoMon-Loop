using UnityEngine;

// Defense note: ThreatTier defines the valid threat tier options used by the gameplay systems.
public enum ThreatTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier4 = 4,
    Tier5 = 5
}

// Defense note: EncounterDepthBand defines the valid encounter depth band options used by the gameplay systems.
public enum EncounterDepthBand
{
    None,
    Early,
    Middle,
    Late,
    Boss
}

// Defense note: ThreatTierRules is the main threat tier rules type used by this part of the project.
public static class ThreatTierRules
{
    public const int MinTier = 1;
    public const int MaxTier = 5;
    public const int LevelsPerTier = 10;
    public const int SprintLevelCap = 50;

    private const int MaxPlayerLevelCorrection = 1;
    private const int PlayerCorrectionStep = 8;

    // Defense note: Runs the clamp tier helper used by this script.
    public static ThreatTier ClampTier(int tier)
    {
        return (ThreatTier)Mathf.Clamp(tier, MinTier, MaxTier);
    }

    // Defense note: Runs the to int helper used by this script.
    public static int ToInt(ThreatTier tier)
    {
        return Mathf.Clamp((int)tier, MinTier, MaxTier);
    }

    // Defense note: Runs the clamp selectable tier helper used by this script.
    public static ThreatTier ClampSelectableTier(int requestedTier, int highestUnlockedTier)
    {
        return ClampTier(requestedTier);
    }

    // Defense note: Checks whether enter tier is currently allowed.
    public static bool CanEnterTier(int requestedTier, int highestUnlockedTier)
    {
        int requested = ToInt(ClampTier(requestedTier));
        return requested >= MinTier && requested <= MaxTier;
    }

    // Defense note: Runs the min level helper used by this script.
    public static int MinLevel(ThreatTier tier)
    {
        return (ToInt(tier) - 1) * LevelsPerTier + 1;
    }

    // Defense note: Runs the max level helper used by this script.
    public static int MaxLevel(ThreatTier tier)
    {
        int sprintCap = Mathf.Min(SprintLevelCap, AlgoMonInstance.MAX_LEVEL);
        return Mathf.Min(ToInt(tier) * LevelsPerTier, sprintCap);
    }

    // Defense note: Runs the reward multiplier helper used by this script.
    public static float RewardMultiplier(ThreatTier selectedTier, ThreatTier highestUnlockedTier)
    {
        return 1f;
    }

    // Defense note: Runs the reward multiplier percent helper used by this script.
    public static int RewardMultiplierPercent(ThreatTier selectedTier, ThreatTier highestUnlockedTier)
    {
        return Mathf.RoundToInt(RewardMultiplier(selectedTier, highestUnlockedTier) * 100f);
    }

    // Defense note: Applies the difficulty to graph change to gameplay or UI state.
    public static void ApplyDifficultyToGraph(GridGraph graph, ThreatTier tier, int partyAverageLevel)
    {
        if (graph == null || graph.nodes == null)
            return;

        int bossLayer = graph.MaxLayer();
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            GridNode node = graph.nodes[i];
            if (node == null)
                continue;

            node.depthBand = DepthBand(node.nodeType, node.layer, bossLayer);
            node.encounterLevel = IsEncounterNode(node.nodeType)
                ? EncounterLevel(tier, node.nodeType, node.layer, bossLayer, partyAverageLevel)
                : 0;
            node.dangerRating = IsEncounterNode(node.nodeType)
                ? DangerRating(tier, node.nodeType, node.depthBand)
                : 0;
        }
    }

    // Defense note: Runs the encounter level helper used by this script.
    public static int EncounterLevel(
        ThreatTier tier,
        NodeType nodeType,
        int nodeLayer,
        int bossLayer,
        int partyAverageLevel,
        int randomBonus = 0)
    {
        int minLevel = MinLevel(tier);
        int maxLevel = MaxLevel(tier);

        if (nodeType == NodeType.Boss)
            return maxLevel;

        int nonBossMax = Mathf.Max(minLevel, maxLevel - 1);
        float depth = DepthProgress(nodeLayer, bossLayer);
        int depthLevel = minLevel + Mathf.RoundToInt((nonBossMax - minLevel) * depth);
        int level = depthLevel
            + EncounterTypeOffset(nodeType)
            + PlayerLevelCorrection(tier, partyAverageLevel)
            + Mathf.Clamp(randomBonus, 0, 1);

        return Mathf.Clamp(level, minLevel, nonBossMax);
    }

    // Defense note: Runs the depth band helper used by this script.
    public static EncounterDepthBand DepthBand(NodeType nodeType, int nodeLayer, int bossLayer)
    {
        if (nodeType == NodeType.Boss)
            return EncounterDepthBand.Boss;
        if (!IsEncounterNode(nodeType))
            return EncounterDepthBand.None;

        float progress = DepthProgress(nodeLayer, bossLayer);
        if (progress < 0.34f)
            return EncounterDepthBand.Early;
        if (progress < 0.67f)
            return EncounterDepthBand.Middle;
        return EncounterDepthBand.Late;
    }

    // Defense note: Returns whether this value is encounter node.
    public static bool IsEncounterNode(NodeType nodeType)
    {
        return nodeType == NodeType.Combat ||
               nodeType == NodeType.Hacker ||
               nodeType == NodeType.Elite ||
               nodeType == NodeType.Boss;
    }

    // Defense note: Runs the depth progress helper used by this script.
    private static float DepthProgress(int nodeLayer, int bossLayer)
    {
        if (bossLayer <= 1)
            return 0f;
        return Mathf.Clamp01((float)Mathf.Max(0, nodeLayer) / bossLayer);
    }

    // Defense note: Runs the encounter type offset helper used by this script.
    private static int EncounterTypeOffset(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Combat:
                return -1;
            case NodeType.Hacker:
                return 1;
            case NodeType.Elite:
                return 2;
            default:
                return 0;
        }
    }

    // Defense note: Plays the er level correction animation, audio, or feedback.
    private static int PlayerLevelCorrection(ThreatTier tier, int partyAverageLevel)
    {
        if (partyAverageLevel <= 0)
            return 0;

        int bandCenter = Mathf.RoundToInt((MinLevel(tier) + MaxLevel(tier)) * 0.5f);
        int delta = partyAverageLevel - bandCenter;
        return Mathf.Clamp(Mathf.RoundToInt(delta / (float)PlayerCorrectionStep), -MaxPlayerLevelCorrection, MaxPlayerLevelCorrection);
    }

    // Defense note: Runs the danger rating helper used by this script.
    private static int DangerRating(ThreatTier tier, NodeType nodeType, EncounterDepthBand depthBand)
    {
        if (nodeType == NodeType.Boss)
            return MaxTier;

        int rating = ToInt(tier) - 1;
        switch (depthBand)
        {
            case EncounterDepthBand.Middle:
                rating += 1;
                break;
            case EncounterDepthBand.Late:
                rating += 2;
                break;
        }

        if (nodeType == NodeType.Hacker || nodeType == NodeType.Elite)
            rating += 1;

        return Mathf.Clamp(rating, 1, MaxTier);
    }
}
