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

    [Header("Subroutine — Passive Ability")]
    [Tooltip("This species' hardwired passive. Triggers automatically in battle. " +
             "Assign a SubroutineData asset. Effect logic is handled by BattleManager.")]
    public SubroutineData subroutine;

    [Header("Learnset")]
    [Tooltip("All skills this species can learn, paired with the level they unlock at. " +
             "Skills with unlockLevel = 1 are available from capture. " +
             "BattleManager / LevelUpHandler populates AlgoMonInstance.knownSkills from this list.")]
    public LearnsetEntry[] learnset;
}
