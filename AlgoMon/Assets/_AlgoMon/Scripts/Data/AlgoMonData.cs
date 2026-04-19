using UnityEngine;

/// <summary>
/// ScriptableObject template defining an AlgoMon species.
/// Acts as a shared read-only blueprint (like a Pokedex entry).
/// Individual stat ceilings (IVs) and growth are stored in AlgoMonInstance.
/// </summary>
[CreateAssetMenu(fileName = "New AlgoMon", menuName = "AlgoMon/AlgoMon Data")]
public class AlgoMonData : ScriptableObject
{
    [Header("Identity")]
    public string codeName;
    [TextArea] public string description;
    public Sprite portrait;

    [Header("Base Skill Pool")]
    public SkillData[] learnableSkills;
}
