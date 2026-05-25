using System;
using UnityEngine;

/// <summary>
/// One entry in a species' learnset — a skill paired with the level
/// at which this AlgoMon can learn it.
///
/// unlockLevel = 1  → available from the start of any run.
/// BattleManager / LevelUpHandler checks this list on every level-up
/// and adds the skill to AlgoMonInstance.knownSkills when the threshold
/// is reached (up to MaxSkillSlots; player chooses which to replace if full).
/// </summary>
[Serializable]
public struct LearnsetEntry
{
    public SkillData skill;

    [Tooltip("The level at which this skill becomes learnable. 1 = available from capture.")]
    [Range(1, AlgoMonInstance.MAX_LEVEL)]
    public int unlockLevel;
}
