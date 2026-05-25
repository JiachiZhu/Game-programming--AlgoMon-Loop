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
            return playerExp > 0 ||
                   algoMonExp > 0 ||
                   compute > 0 ||
                   baseDataGranted ||
                   evolutionDataGranted;
        }
    }

    public EncounterReward Clone()
    {
        return (EncounterReward)MemberwiseClone();
    }

    public string ToBattleLogLine()
    {
        if (!HasAnyGrant)
            return "REWARD: none.";

        string line = $"REWARD: USER +{playerExp} EXP | ALGOMON +{algoMonExp} EXP | COMPUTE +{compute}";
        if (baseDataGranted)
            line += $" | DATA {FormatQuality(baseDataQuality)}";
        else if (shouldGrantBaseData)
            line += " | DATA SKIPPED";

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
    public int playerExp;
    public int algoMonExp;
    public int compute;
    public int baseDataCount;
    public int highQualityBaseDataCount;
    public int evolutionDataCount;

    public void Reset()
    {
        playerExp = 0;
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

        playerExp += reward.playerExp;
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
            playerExp = playerExp,
            algoMonExp = algoMonExp,
            compute = compute,
            baseDataCount = baseDataCount,
            highQualityBaseDataCount = highQualityBaseDataCount,
            evolutionDataCount = evolutionDataCount
        };
    }

    public string ToCompactDisplay()
    {
        return $"USER EXP +{playerExp} | ALGOMON EXP +{algoMonExp}\n" +
               $"COMPUTE +{compute} | DATA +{baseDataCount} | EVO +{evolutionDataCount}";
    }
}

public static class EncounterRewardCalculator
{
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
                    playerExp = 25 + level * 2,
                    algoMonExp = 45 + level * 3,
                    compute = 18 + danger * 4
                };
            case NodeType.Elite:
                return new EncounterReward
                {
                    playerExp = 20 + level * 2,
                    algoMonExp = 35 + level * 3,
                    compute = 12 + danger * 3
                };
            case NodeType.Boss:
                return new EncounterReward
                {
                    playerExp = 50 + level * 3,
                    algoMonExp = 75 + level * 4,
                    compute = 30 + danger * 5,
                    shouldGrantBaseData = true,
                    baseDataQuality = RewardDataQuality.HighQualityBase,
                    shouldGrantEvolutionData = true
                };
            case NodeType.Combat:
            default:
                return new EncounterReward
                {
                    playerExp = 10 + level,
                    algoMonExp = 20 + level * 2,
                    compute = 4 + danger,
                    shouldGrantBaseData = true,
                    baseDataQuality = RewardDataQuality.Base
                };
        }
    }

    private static void ApplyMultiplier(EncounterReward reward, float rewardMultiplier)
    {
        float multiplier = Mathf.Max(0f, rewardMultiplier);
        reward.playerExp = Mathf.Max(0, Mathf.RoundToInt(reward.playerExp * multiplier));
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
