using UnityEngine;

public enum ThreatTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier4 = 4,
    Tier5 = 5
}

public enum EncounterDepthBand
{
    None,
    Early,
    Middle,
    Late,
    Boss
}

public static class ThreatTierRules
{
    public const int MinTier = 1;
    public const int MaxTier = 5;
    public const int LevelsPerTier = 10;
    public const int SprintLevelCap = 50;

    private const float RewardPenaltyPerTierBelowMax = 0.15f;
    private const float MinimumLowerTierRewardMultiplier = 0.40f;
    private const int MaxPlayerLevelCorrection = 1;
    private const int PlayerCorrectionStep = 8;

    public static ThreatTier ClampTier(int tier)
    {
        return (ThreatTier)Mathf.Clamp(tier, MinTier, MaxTier);
    }

    public static int ToInt(ThreatTier tier)
    {
        return Mathf.Clamp((int)tier, MinTier, MaxTier);
    }

    public static ThreatTier ClampSelectableTier(int requestedTier, int highestUnlockedTier)
    {
        int highest = ToInt(ClampTier(highestUnlockedTier));
        return ClampTier(Mathf.Min(requestedTier, highest));
    }

    public static bool CanEnterTier(int requestedTier, int highestUnlockedTier)
    {
        int requested = ToInt(ClampTier(requestedTier));
        int highest = ToInt(ClampTier(highestUnlockedTier));
        return requested <= highest;
    }

    public static int MinLevel(ThreatTier tier)
    {
        return (ToInt(tier) - 1) * LevelsPerTier + 1;
    }

    public static int MaxLevel(ThreatTier tier)
    {
        int sprintCap = Mathf.Min(SprintLevelCap, AlgoMonInstance.MAX_LEVEL);
        return Mathf.Min(ToInt(tier) * LevelsPerTier, sprintCap);
    }

    public static float RewardMultiplier(ThreatTier selectedTier, ThreatTier highestUnlockedTier)
    {
        int gap = Mathf.Max(0, ToInt(highestUnlockedTier) - ToInt(selectedTier));
        float multiplier = 1f - gap * RewardPenaltyPerTierBelowMax;
        return Mathf.Clamp(multiplier, MinimumLowerTierRewardMultiplier, 1f);
    }

    public static int RewardMultiplierPercent(ThreatTier selectedTier, ThreatTier highestUnlockedTier)
    {
        return Mathf.RoundToInt(RewardMultiplier(selectedTier, highestUnlockedTier) * 100f);
    }

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

    public static int EncounterLevel(ThreatTier tier, NodeType nodeType, int nodeLayer, int randomBonus)
    {
        return EncounterLevel(tier, nodeType, nodeLayer, Mathf.Max(nodeLayer + 1, 1), 0, randomBonus);
    }

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

    public static bool IsEncounterNode(NodeType nodeType)
    {
        return nodeType == NodeType.Combat ||
               nodeType == NodeType.Hacker ||
               nodeType == NodeType.Elite ||
               nodeType == NodeType.Boss;
    }

    private static float DepthProgress(int nodeLayer, int bossLayer)
    {
        if (bossLayer <= 1)
            return 0f;
        return Mathf.Clamp01((float)Mathf.Max(0, nodeLayer) / bossLayer);
    }

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

    private static int PlayerLevelCorrection(ThreatTier tier, int partyAverageLevel)
    {
        if (partyAverageLevel <= 0)
            return 0;

        int bandCenter = Mathf.RoundToInt((MinLevel(tier) + MaxLevel(tier)) * 0.5f);
        int delta = partyAverageLevel - bandCenter;
        return Mathf.Clamp(Mathf.RoundToInt(delta / (float)PlayerCorrectionStep), -MaxPlayerLevelCorrection, MaxPlayerLevelCorrection);
    }

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
