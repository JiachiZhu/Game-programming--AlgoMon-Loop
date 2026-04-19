using System;
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
    public const int MAX_LEVEL = 100;

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
