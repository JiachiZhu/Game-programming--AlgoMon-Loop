using System;
using UnityEngine;

public enum RewardDataQuality
{
    None,
    Base,
    HighQualityBase
}

[Serializable]
public class EncounterReward
{
    public NodeType sourceNodeType;
    public int threatTier;
    public int encounterLevel;
    public int rewardMultiplierPercent = 100;
    public bool calculated;
    // Legacy save field retained for older serialized data; player-facing rewards use AlgoMon EXP only.
    public int playerExp;
    public int algoMonExp;
    public int compute;
    public bool shouldGrantBaseData;
    public bool baseDataGranted;
    public RewardDataQuality baseDataQuality = RewardDataQuality.None;
    public bool shouldGrantEvolutionData;
    public bool evolutionDataGranted;
    public string speciesCodeName;

    public bool HasAnyGrant
    {
        get
        {
            return calculated ||
                   algoMonExp > 0 ||
                   compute > 0 ||
                   baseDataGranted ||
                   evolutionDataGranted;
        }
    }

    public EncounterReward Clone()
    {
        // All fields are value types or strings; switch to a deep copy if reference fields are added.
        return (EncounterReward)MemberwiseClone();
    }

    public string ToBattleLogLine()
    {
        if (!HasAnyGrant)
            return "REWARD: none.";

        string line = $"REWARD: ALGOMON +{algoMonExp} EXP | CREDITS +{compute}";
        if (baseDataGranted)
            line += $" | BASE FORM {FormatQuality(baseDataQuality)}";
        else if (shouldGrantBaseData)
            line += " | BASE FORM SKIPPED";

        if (evolutionDataGranted)
            line += " | EVOLUTION DATA +1";

        return line;
    }

    public static string FormatQuality(RewardDataQuality quality)
    {
        switch (quality)
        {
            case RewardDataQuality.HighQualityBase:
                return "HIGH";
            case RewardDataQuality.Base:
                return "BASE";
            default:
                return "NONE";
        }
    }
}

[Serializable]
public class RunRewardSummary
{
    // Legacy save field retained for older serialized data; no longer displayed or accumulated.
    public int playerExp;
    public int algoMonExp;
    public int compute;
    public int baseDataCount;
    public int highQualityBaseDataCount;
    public int evolutionDataCount;

    public void Reset()
    {
        algoMonExp = 0;
        compute = 0;
        baseDataCount = 0;
        highQualityBaseDataCount = 0;
        evolutionDataCount = 0;
    }

    public void Add(EncounterReward reward)
    {
        if (reward == null)
            return;

        algoMonExp += reward.algoMonExp;
        compute += reward.compute;
        if (reward.baseDataGranted)
        {
            baseDataCount++;
            if (reward.baseDataQuality == RewardDataQuality.HighQualityBase)
                highQualityBaseDataCount++;
        }
        if (reward.evolutionDataGranted)
            evolutionDataCount++;
    }

    public RunRewardSummary Clone()
    {
        return new RunRewardSummary
        {
            algoMonExp = algoMonExp,
            compute = compute,
            baseDataCount = baseDataCount,
            highQualityBaseDataCount = highQualityBaseDataCount,
            evolutionDataCount = evolutionDataCount
        };
    }

    public string ToCompactDisplay()
    {
        string baseLine = highQualityBaseDataCount > 0
            ? $"BASE FORM +{baseDataCount} (HIGH {highQualityBaseDataCount})"
            : $"BASE FORM +{baseDataCount}";

        return $"ALGOMON EXP +{algoMonExp}\n" +
               $"CREDITS +{compute} | {baseLine}";
    }
}

public static class EncounterRewardCalculator
{
    // First-pass tuning numbers; final economy balance belongs to Sprint 6.
    public static EncounterReward Build(
        GridNode node,
        AlgoMonInstance defeatedOpponent,
        ThreatTier threatTier,
        float rewardMultiplier)
    {
        NodeType nodeType = node != null ? node.nodeType : NodeType.Combat;
        int level = node != null && node.encounterLevel > 0
            ? node.encounterLevel
            : (defeatedOpponent != null ? defeatedOpponent.level : ThreatTierRules.MinLevel(threatTier));
        int danger = node != null && node.dangerRating > 0 ? node.dangerRating : ThreatTierRules.ToInt(threatTier);

        EncounterReward reward = BaseRewardFor(nodeType, level, danger);
        reward.sourceNodeType = nodeType;
        reward.threatTier = ThreatTierRules.ToInt(threatTier);
        reward.encounterLevel = Mathf.Clamp(level, 1, AlgoMonInstance.MAX_LEVEL);
        reward.rewardMultiplierPercent = Mathf.RoundToInt(Mathf.Max(0f, rewardMultiplier) * 100f);
        reward.speciesCodeName = SpeciesCodeName(defeatedOpponent);
        ApplyMultiplier(reward, rewardMultiplier);
        reward.calculated = true;
        return reward;
    }

    private static EncounterReward BaseRewardFor(NodeType nodeType, int level, int danger)
    {
        level = Mathf.Clamp(level, 1, AlgoMonInstance.MAX_LEVEL);
        danger = Mathf.Clamp(danger, 1, ThreatTierRules.MaxTier);

        switch (nodeType)
        {
            case NodeType.Hacker:
                return new EncounterReward
                {
                    algoMonExp = 45 + level * 3,
                    compute = 18 + danger * 4
                };
            case NodeType.Elite:
                return new EncounterReward
                {
                    algoMonExp = 35 + level * 3,
                    compute = 12 + danger * 3
                };
            case NodeType.Boss:
                return new EncounterReward
                {
                    algoMonExp = 75 + level * 4,
                    compute = 30 + danger * 5,
                    shouldGrantBaseData = true,
                    baseDataQuality = RewardDataQuality.HighQualityBase
                };
            case NodeType.Combat:
            default:
                return new EncounterReward
                {
                    algoMonExp = 20 + level * 2,
                    compute = 4 + danger
                };
        }
    }

    private static void ApplyMultiplier(EncounterReward reward, float rewardMultiplier)
    {
        float multiplier = Mathf.Max(0f, rewardMultiplier);
        reward.algoMonExp = Mathf.Max(0, Mathf.RoundToInt(reward.algoMonExp * multiplier));
        reward.compute = Mathf.Max(0, Mathf.RoundToInt(reward.compute * multiplier));
    }

    private static string SpeciesCodeName(AlgoMonInstance defeatedOpponent)
    {
        if (defeatedOpponent != null &&
            defeatedOpponent.data != null &&
            !string.IsNullOrWhiteSpace(defeatedOpponent.data.codeName))
        {
            return defeatedOpponent.data.codeName.Trim();
        }

        return "UNKNOWN";
    }
}
