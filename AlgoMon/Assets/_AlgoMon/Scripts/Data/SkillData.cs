using UnityEngine;

/// <summary>
/// ScriptableObject defining a single skill (data instruction).
/// Full implementation in a later sprint.
/// </summary>
[CreateAssetMenu(fileName = "New Skill", menuName = "AlgoMon/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    [TextArea] public string description;

    [Header("Classification")]
    public InstructionType type;
    public DamageType damageType;

    [Header("Values")]
    public int basePower;
    public int cpCost;
}

public enum InstructionType { Attack, Status, Defense }
