using UnityEngine;

/// <summary>
/// ScriptableObject template defining an AlgoMon species.
/// Acts as a shared read-only blueprint (like a Pokedex entry).
/// Individual stat ceilings (IVs) and growth are stored in AlgoMonInstance.
/// </summary>
[CreateAssetMenu(fileName = "New AlgoMon", menuName = "AlgoMon/AlgoMon Data")]
// Defense note: AlgoMonData is a data definition object that designers configure in Unity.
public class AlgoMonData : ScriptableObject
{
    [Header("Identity")]
    public string codeName;
    [TextArea] public string description;
    public Sprite portrait;
    public ElementType elementType;

    [Header("Base Stats (种族值 / species base — BST 600)")]
    [Tooltip("Per-species hardware baseline. Combined with talent (IV), level and evolution by AlgoMonInstance to produce the live 数值. The six values should total 600 (BattleDesign.md §9) and be distributed to match the species role.")]
    [Range(1, 160)] public int baseBattery = 100;
    [Range(1, 160)] public int baseClockSpeed = 100;
    [Range(1, 160)] public int baseComputingPower = 100;
    [Range(1, 160)] public int baseThroughput = 100;
    [Range(1, 160)] public int baseFirewall = 100;
    [Range(1, 160)] public int baseEncryption = 100;

    [Header("Battle Presentation")]
    [Tooltip("Optional species/form-specific battle animation profile. " +
             "If empty, BattleSpriteAnimator uses its generic fallback motion.")]
    public BattleAnimationProfile battleAnimationProfile;

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
