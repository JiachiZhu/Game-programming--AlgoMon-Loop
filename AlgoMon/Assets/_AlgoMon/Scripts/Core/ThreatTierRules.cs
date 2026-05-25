using UnityEngine;

public enum ThreatTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier4 = 4,
    Tier5 = 5
}

public static class ThreatTierRules
{
    public const int MinTier = 1;
    public const int MaxTier = 5;
    public const int LevelsPerTier = 10;
    public const int SprintLevelCap = 50;

    private const float RewardPenaltyPerTierBelowMax = 0.15f;
    private const float MinimumLowerTierRewardMultiplier = 0.40f;

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

    public static int EncounterLevel(ThreatTier tier, NodeType nodeType, int nodeLayer, int randomBonus)
    {
        int minLevel = MinLevel(tier);
        int maxLevel = MaxLevel(tier);

        if (nodeType == NodeType.Boss)
            return maxLevel;

        int typeOffset = nodeType == NodeType.Elite ? 3 : 0;
        int layerOffset = Mathf.Max(0, nodeLayer);
        int level = minLevel + layerOffset + typeOffset + Mathf.Clamp(randomBonus, 0, 2);
        return Mathf.Clamp(level, minLevel, maxLevel);
    }
}
