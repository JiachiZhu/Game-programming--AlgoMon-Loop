using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a single captured AlgoMon with its individual stats.
///
/// Hardware / Software separation:
///   IV  = Hardware upper limit. Set on capture. Only raised via gene merging
///         in the Lab (Greedy algorithm: IV_child = Max(IV_A, IV_B)).
///   EXP = Software progress. Raised by winning battles. Determines what
///         percentage of the IV ceiling is currently unlocked.
///
/// Actual stat formula:
///   actualStat = Floor(iv * (level / MAX_LEVEL))
/// </summary>
[Serializable]
public class AlgoMonInstance
{
    public const int MAX_LEVEL = ThreatTierRules.SprintLevelCap;
    public const int FusionCopiesForEvolution = 3;

    public AlgoMonData data;
    public string nickname;
    public RewardDataQuality dataQuality = RewardDataQuality.Base;
    public string battleFormName = "Base";

    [Header("Gene Lab")]
    public string instanceId;
    public int talentSeed;
    public bool isFavorite;
    [Range(0, FusionCopiesForEvolution)] public int fusedBaseCopies;
    public List<string> fusionSourceInstanceIds = new List<string>();

    [Header("Hardware IVs — Upper Limits")]
    [Range(1, 255)] public int iv_Battery;
    [Range(1, 255)] public int iv_ClockSpeed;
    [Range(1, 255)] public int iv_ComputingPower;
    [Range(1, 255)] public int iv_Throughput;
    [Range(1, 255)] public int iv_Firewall;
    [Range(1, 255)] public int iv_Encryption;

    [Header("Software Progress")]
    [Range(1, MAX_LEVEL)] public int level = 1;
    public int exp = 0;
    public int expToNextLevel => ExpRequiredForNextLevel(level);

    [Header("Known Skills")]
    [Tooltip("Skills currently loaded into this AlgoMon's active slots. " +
             "Populated from data.learnset on level-up. Max 4 slots.")]
    public List<SkillData> knownSkills = new List<SkillData>();
    public const int MaxSkillSlots = 4;

    [Header("Source")]
    [Tooltip("True when this instance points at ScriptableObject data created at runtime. " +
             "Transient instances are battle-safe but should not be persisted into Payload.")]
    public bool usesTransientData;

    public string SpeciesCodeName
    {
        get
        {
            return data != null && !string.IsNullOrWhiteSpace(data.codeName)
                ? data.codeName.Trim()
                : string.Empty;
        }
    }

    public string FormName
    {
        get
        {
            return string.IsNullOrWhiteSpace(battleFormName)
                ? "Base"
                : battleFormName.Trim();
        }
    }

    public bool IsEvolvedForm
    {
        get { return string.Equals(FormName, "Evolved", StringComparison.OrdinalIgnoreCase); }
    }

    public bool IsBaseForm
    {
        get { return !IsEvolvedForm; }
    }

    public static int ExpRequiredForNextLevel(int currentLevel)
    {
        int clampedLevel = Mathf.Clamp(currentLevel, 1, MAX_LEVEL - 1);

        if (clampedLevel <= 10)
            return 60 + clampedLevel * 14;

        if (clampedLevel <= 30)
        {
            int midLevel = clampedLevel - 10;
            return 200 + midLevel * 35 + midLevel * midLevel * 4;
        }

        int lateLevel = clampedLevel - 30;
        return 2500 + lateLevel * 120 + lateLevel * lateLevel * 18;
    }

    public int FusionProgress
    {
        get { return Mathf.Clamp(fusedBaseCopies, 0, FusionCopiesForEvolution); }
    }

    public int RemainingFusionCopies
    {
        get { return Mathf.Max(0, FusionCopiesForEvolution - FusionProgress); }
    }

    public bool CanEvolve
    {
        get { return IsBaseForm && FusionProgress >= FusionCopiesForEvolution; }
    }

    public string FusionProgressText
    {
        get { return $"{FusionProgress}/{FusionCopiesForEvolution}"; }
    }

    public void EnsurePersistentRuntimeState()
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            instanceId = Guid.NewGuid().ToString("N");
        if (fusionSourceInstanceIds == null)
            fusionSourceInstanceIds = new List<string>();
        if (string.IsNullOrWhiteSpace(battleFormName))
            battleFormName = "Base";

        battleFormName = IsEvolvedForm ? "Evolved" : "Base";
        fusedBaseCopies = FusionProgress;
        level = Mathf.Clamp(level, 1, MAX_LEVEL);
    }

    /// <summary>
    /// Checks data.learnset for any skill that unlocks at exactly the current level
    /// and adds it to knownSkills if a slot is available.
    /// Returns the list of newly learned skills (for UI prompt / replace flow).
    /// </summary>
    public List<SkillData> CheckLearnsetAtCurrentLevel()
    {
        var newSkills = new List<SkillData>();
        if (data == null) return newSkills;

        foreach (LearnsetEntry entry in data.learnset)
        {
            if (entry.skill == null) continue;
            if (entry.unlockLevel != level) continue;
            if (knownSkills.Contains(entry.skill)) continue;

            newSkills.Add(entry.skill);
            if (knownSkills.Count < MaxSkillSlots)
                knownSkills.Add(entry.skill);
        }
        return newSkills;
    }

    /// <summary>
    /// Fills empty active skill slots from every learnset entry already unlocked.
    /// Useful when a runtime instance is created from a species asset.
    /// </summary>
    public void EnsureKnownSkillsFromLearnset()
    {
        if (data == null || data.learnset == null)
            return;

        if (knownSkills == null)
            knownSkills = new List<SkillData>();

        foreach (LearnsetEntry entry in data.learnset)
        {
            if (knownSkills.Count >= MaxSkillSlots)
                return;
            if (entry.skill == null || entry.unlockLevel > level)
                continue;
            if (knownSkills.Contains(entry.skill))
                continue;

            knownSkills.Add(entry.skill);
        }
    }

    /// <summary>
    /// Copies persistent capture data without sharing mutable runtime lists.
    /// ScriptableObject references remain shared read-only blueprints.
    /// </summary>
    public AlgoMonInstance Clone()
    {
        EnsurePersistentRuntimeState();
        return new AlgoMonInstance
        {
            data = data,
            nickname = nickname,
            dataQuality = dataQuality,
            battleFormName = battleFormName,
            instanceId = Guid.NewGuid().ToString("N"),
            talentSeed = talentSeed,
            isFavorite = isFavorite,
            fusedBaseCopies = fusedBaseCopies,
            fusionSourceInstanceIds = fusionSourceInstanceIds != null
                ? new List<string>(fusionSourceInstanceIds)
                : new List<string>(),
            iv_Battery = iv_Battery,
            iv_ClockSpeed = iv_ClockSpeed,
            iv_ComputingPower = iv_ComputingPower,
            iv_Throughput = iv_Throughput,
            iv_Firewall = iv_Firewall,
            iv_Encryption = iv_Encryption,
            level = level,
            exp = exp,
            usesTransientData = usesTransientData,
            knownSkills = knownSkills != null
                ? new List<SkillData>(knownSkills)
                : new List<SkillData>()
        };
    }

    // --- Computed Stats (数值) ---
    // A live stat is shaped by four inputs the player can reason about:
    //   talent (IV ceiling) · species base (种族值) · level (等级) · evolution (进化).
    // Species base gives every unit a non-zero floor (so freshly captured level-1
    // bodies still read on the radar), the IV term grows the stat with level, and
    // evolving applies a flat multiplier on top.
    public const float EvolvedStatMultiplier = 1.15f;

    public int Battery        => Calc(iv_Battery, BaseStat(b => b.baseBattery));
    public int ClockSpeed     => Calc(iv_ClockSpeed, BaseStat(b => b.baseClockSpeed));
    public int ComputingPower => Calc(iv_ComputingPower, BaseStat(b => b.baseComputingPower));
    public int Throughput     => Calc(iv_Throughput, BaseStat(b => b.baseThroughput));
    public int Firewall       => Calc(iv_Firewall, BaseStat(b => b.baseFirewall));
    public int Encryption     => Calc(iv_Encryption, BaseStat(b => b.baseEncryption));

    private const int DefaultSpeciesBase = 100;

    private int BaseStat(System.Func<AlgoMonData, int> selector)
    {
        if (data == null)
            return DefaultSpeciesBase;
        int value = selector(data);
        return value > 0 ? value : DefaultSpeciesBase;
    }

    private int Calc(int iv, int speciesBase)
    {
        float levelFactor = Mathf.Clamp01(level / (float)MAX_LEVEL);
        float evolution = IsEvolvedForm ? EvolvedStatMultiplier : 1f;
        float baseContribution = speciesBase * (0.5f + 0.5f * levelFactor);
        float talentContribution = iv * levelFactor;
        return Mathf.Max(1, Mathf.RoundToInt((baseContribution + talentContribution) * evolution));
    }

    /// <summary>
    /// Greedy IV inheritance used in the Gene Lab.
    /// Child takes the best hardware ceiling from either parent per dimension.
    /// </summary>
    public static AlgoMonInstance Merge(AlgoMonInstance a, AlgoMonInstance b, AlgoMonData childData)
    {
        var child = new AlgoMonInstance
        {
            data             = childData,
            nickname         = childData.codeName,
            battleFormName   = "Base",
            instanceId       = Guid.NewGuid().ToString("N"),
            talentSeed       = a != null ? a.talentSeed : 0,
            iv_Battery       = Mathf.Max(a.iv_Battery,       b.iv_Battery),
            iv_ClockSpeed    = Mathf.Max(a.iv_ClockSpeed,    b.iv_ClockSpeed),
            iv_ComputingPower= Mathf.Max(a.iv_ComputingPower, b.iv_ComputingPower),
            iv_Throughput    = Mathf.Max(a.iv_Throughput,    b.iv_Throughput),
            iv_Firewall      = Mathf.Max(a.iv_Firewall,      b.iv_Firewall),
            iv_Encryption    = Mathf.Max(a.iv_Encryption,    b.iv_Encryption),
            level            = 1,
            exp              = 0
        };
        child.EnsureKnownSkillsFromLearnset();
        return child;
    }

    public static AlgoMonInstance CreateRewardBase(AlgoMonData species, RewardDataQuality quality, int seed)
    {
        if (species == null)
            return null;

        var rng = new System.Random(seed);
        int min = quality == RewardDataQuality.HighQualityBase ? 122 : 88;
        int max = quality == RewardDataQuality.HighQualityBase ? 218 : 184;
        int batteryBonus = quality == RewardDataQuality.HighQualityBase ? 14 : 8;

        var mon = new AlgoMonInstance
        {
            data = species,
            nickname = !string.IsNullOrWhiteSpace(species.codeName) ? species.codeName.Trim() : species.name,
            dataQuality = quality == RewardDataQuality.None ? RewardDataQuality.Base : quality,
            battleFormName = "Base",
            instanceId = Guid.NewGuid().ToString("N"),
            talentSeed = seed,
            fusedBaseCopies = 0,
            usesTransientData = false,
            level = 1,
            exp = 0,
            iv_Battery = RollTalent(rng, min, max, batteryBonus),
            iv_ClockSpeed = RollTalent(rng, min, max, 0),
            iv_ComputingPower = RollTalent(rng, min, max, 0),
            iv_Throughput = RollTalent(rng, min, max, 0),
            iv_Firewall = RollTalent(rng, min, max, 0),
            iv_Encryption = RollTalent(rng, min, max, 0)
        };
        mon.EnsureKnownSkillsFromLearnset();
        return mon;
    }

    public void FuseFrom(AlgoMonInstance material)
    {
        if (material == null)
            return;

        EnsurePersistentRuntimeState();
        material.EnsurePersistentRuntimeState();

        iv_Battery = Mathf.Max(iv_Battery, material.iv_Battery);
        iv_ClockSpeed = Mathf.Max(iv_ClockSpeed, material.iv_ClockSpeed);
        iv_ComputingPower = Mathf.Max(iv_ComputingPower, material.iv_ComputingPower);
        iv_Throughput = Mathf.Max(iv_Throughput, material.iv_Throughput);
        iv_Firewall = Mathf.Max(iv_Firewall, material.iv_Firewall);
        iv_Encryption = Mathf.Max(iv_Encryption, material.iv_Encryption);
        if (material.level > level)
        {
            level = material.level;
            exp = material.exp;
        }
        else if (material.level == level)
        {
            exp = Mathf.Max(exp, material.exp);
        }

        level = Mathf.Clamp(level, 1, MAX_LEVEL);
        fusedBaseCopies = Mathf.Clamp(
            fusedBaseCopies + 1 + material.FusionProgress,
            0,
            FusionCopiesForEvolution);

        if (material.dataQuality > dataQuality)
            dataQuality = material.dataQuality;

        RecordFusionSource(material.instanceId);
        if (material.fusionSourceInstanceIds != null)
        {
            for (int i = 0; i < material.fusionSourceInstanceIds.Count; i++)
                RecordFusionSource(material.fusionSourceInstanceIds[i]);
        }
    }

    public bool Evolve()
    {
        EnsurePersistentRuntimeState();
        if (!CanEvolve)
            return false;

        battleFormName = "Evolved";
        return true;
    }

    private void RecordFusionSource(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return;
        if (fusionSourceInstanceIds == null)
            fusionSourceInstanceIds = new List<string>();
        if (!fusionSourceInstanceIds.Contains(sourceId))
            fusionSourceInstanceIds.Add(sourceId);
    }

    private static int RollTalent(System.Random rng, int min, int max, int bonus)
    {
        if (rng == null)
            return Mathf.Clamp(min + bonus, 1, 255);

        return Mathf.Clamp(rng.Next(min, max + 1) + bonus, 1, 255);
    }

    /// <summary>Adds EXP and handles level-up.</summary>
    public void GainExp(int amount)
    {
        if (level >= MAX_LEVEL) return;
        exp += amount;
        while (exp >= expToNextLevel && level < MAX_LEVEL)
        {
            exp -= expToNextLevel;
            level++;
        }
    }
}
