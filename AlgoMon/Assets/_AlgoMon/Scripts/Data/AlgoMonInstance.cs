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

    public AlgoMonData data;
    public string nickname;

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
    public int expToNextLevel => level * level * 4;

    [Header("Known Skills")]
    [Tooltip("Skills currently loaded into this AlgoMon's active slots. " +
             "Populated from data.learnset on level-up. Max 4 slots.")]
    public List<SkillData> knownSkills = new List<SkillData>();
    public const int MaxSkillSlots = 4;

    [Header("Source")]
    [Tooltip("True when this instance points at ScriptableObject data created at runtime. " +
             "Transient instances are battle-safe but should not be persisted into Payload.")]
    public bool usesTransientData;

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
        return new AlgoMonInstance
        {
            data = data,
            nickname = nickname,
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

    // --- Computed Stats ---
    public int Battery        => Calc(iv_Battery);
    public int ClockSpeed     => Calc(iv_ClockSpeed);
    public int ComputingPower => Calc(iv_ComputingPower);
    public int Throughput     => Calc(iv_Throughput);
    public int Firewall       => Calc(iv_Firewall);
    public int Encryption     => Calc(iv_Encryption);

    private int Calc(int iv) => Mathf.FloorToInt(iv * ((float)level / MAX_LEVEL));

    /// <summary>
    /// Greedy IV inheritance used in the Gene Lab.
    /// Child takes the best hardware ceiling from either parent per dimension.
    /// </summary>
    public static AlgoMonInstance Merge(AlgoMonInstance a, AlgoMonInstance b, AlgoMonData childData)
    {
        return new AlgoMonInstance
        {
            data             = childData,
            nickname         = childData.codeName,
            iv_Battery       = Mathf.Max(a.iv_Battery,       b.iv_Battery),
            iv_ClockSpeed    = Mathf.Max(a.iv_ClockSpeed,    b.iv_ClockSpeed),
            iv_ComputingPower= Mathf.Max(a.iv_ComputingPower, b.iv_ComputingPower),
            iv_Throughput    = Mathf.Max(a.iv_Throughput,    b.iv_Throughput),
            iv_Firewall      = Mathf.Max(a.iv_Firewall,      b.iv_Firewall),
            iv_Encryption    = Mathf.Max(a.iv_Encryption,    b.iv_Encryption),
            level            = 1,
            exp              = 0
        };
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
