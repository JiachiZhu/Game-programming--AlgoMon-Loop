using UnityEngine;

/// <summary>
/// ScriptableObject defining a single skill (data instruction).
/// </summary>
[CreateAssetMenu(fileName = "New Skill", menuName = "AlgoMon/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    [TextArea] public string description;

    [Header("Classification")]
    public InstructionType instructionType;  // A / S / D — used for ASD counter check
    public DamageType      damageType;       // Computing Power or Throughput
    public ElementType     elementType;      // determines element chart multiplier

    [Header("Values")]
    public int   basePower;
    public int   cpCost;

    [Header("Counter Success Effect")]
    [Tooltip("Damage multiplier applied when this skill wins the ASD counter. 1 = no bonus.")]
    public float counterSuccessMultiplier = 1f;
}
