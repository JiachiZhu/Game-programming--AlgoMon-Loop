/*
Script Audit:
- Purpose: Creates deterministic enemy AlgoMon instances for Grid encounter nodes.
- Attached GameObject: None; this is a static factory used by GameManager.
- Main responsibilities: Pick species, create fallback species when needed, assign enemy level/stats/name/form, create Hacker parties, and load encounter species assets.
- Important variables: BaseIvFloor, IvPerLayer, IvPerEncounterGrade, IvPerThreatTier, EncounterSpeciesCatalogResourcePath.
- Inputs: Run seed, selected GridNode, and ThreatTier.
- Outputs or effects: Returns an AlgoMonInstance for the current battle opponent.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Enter the same node with the same seed and confirm the generated enemy is consistent.
*/
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Creates deterministic runtime opponents for route-map encounters.
/// GameManager owns the run state; this factory owns encounter balancing rules.
/// </summary>
// Defense note: EncounterFactory creates runtime encounter objects from configured data.
public static class EncounterFactory
{
    private const string AlgoMonAssetSearchFolder = "Assets/_AlgoMon/ScriptableObjects/AlgoMons";
    private const string EncounterSpeciesCatalogResourcePath = "EncounterSpeciesCatalog";

    private const int LevelRandomExclusiveMax = 3;

    private const int BaseIvFloor = 138;
    private const int IvPerLayer = 7;
    private const int IvPerEncounterGrade = 18;
    private const int IvPerThreatTier = 10;
    private const int TierStatBonus = 4;

    private const int BatteryIvBonus = 20;
    private const int BatteryIvSpread = 14;
    private const int SpeedIvSpread = 18;
    private const int OffenseIvSpread = 16;
    private const int DefenseIvSpread = 16;
    private const int HackerBasePartySize = 2;
    private const int HackerMaxPartySize = 3;

    // Defense note: Runs the create helper used by this script.
    public static AlgoMonInstance Create(int runSeed, GridNode node)
    {
        return Create(runSeed, node, ThreatTier.Tier1);
    }

    // Defense note: Runs the create helper used by this script.
    public static AlgoMonInstance Create(int runSeed, GridNode node, ThreatTier threatTier)
    {
        return Create(runSeed, node, threatTier, null);
    }

    // Defense note: Runs the create helper used by this script.
    public static AlgoMonInstance Create(int runSeed, GridNode node, ThreatTier threatTier, string preferredBossSpeciesCodeName)
    {
        return Create(runSeed, node, threatTier, 0, 1, preferredBossSpeciesCodeName);
    }

    // Defense note: Creates the party object used by the scene or runtime.
    public static List<AlgoMonInstance> CreateParty(int runSeed, GridNode node, ThreatTier threatTier)
    {
        return CreateParty(runSeed, node, threatTier, null);
    }

    // Defense note: Creates the party object used by the scene or runtime.
    public static List<AlgoMonInstance> CreateParty(int runSeed, GridNode node, ThreatTier threatTier, string preferredBossSpeciesCodeName)
    {
        var party = new List<AlgoMonInstance>();
        if (node == null)
            return party;

        int partySize = node.nodeType == NodeType.Hacker
            ? HackerPartySize(node)
            : 1;

        for (int i = 0; i < partySize; i++)
        {
            AlgoMonInstance member = Create(runSeed, node, threatTier, i, partySize, preferredBossSpeciesCodeName);
            if (member != null)
                party.Add(member);
        }

        return party;
    }

    // Defense note: Runs the create helper used by this script.
    private static AlgoMonInstance Create(
        int runSeed,
        GridNode node,
        ThreatTier threatTier,
        int partySlot,
        int partySize,
        string preferredBossSpeciesCodeName)
    {
        if (node == null)
            return null;

        string partyHash = partySlot <= 0 ? string.Empty : $":P{partySlot}";
        string bossHash = node.nodeType == NodeType.Boss && !string.IsNullOrWhiteSpace(preferredBossSpeciesCodeName)
            ? $":B{NormalizeSpeciesKey(preferredBossSpeciesCodeName).ToUpperInvariant()}"
            : string.Empty;
        string hashKey = $"{runSeed}:{node.id}:{node.nodeType}:T{ThreatTierRules.ToInt(threatTier)}{partyHash}{bossHash}";
        int hash = StableHash(hashKey);
        var rng = new System.Random(hash);
        int encounterGrade = EncounterGrade(node.nodeType);
        int threatIndex = ThreatTierRules.ToInt(threatTier) - 1;
        int baseIv = BaseIvFloor + node.layer * IvPerLayer + encounterGrade * IvPerEncounterGrade + threatIndex * IvPerThreatTier;
        AlgoMonData species = PickEncounterSpecies(node, hash, preferredBossSpeciesCodeName, out bool usesTransientData);

        var opponent = new AlgoMonInstance
        {
            data = species,
            nickname = BuildOpponentName(species, node, partySlot, partySize),
            battleFormName = BattleFormName(node),
            usesTransientData = usesTransientData,
            level = node.encounterLevel > 0
                ? node.encounterLevel
                : FallbackEncounterLevel(threatTier, node, rng),
            iv_Battery = RollEncounterStat(rng, baseIv + BatteryIvBonus, BatteryIvSpread),
            iv_ClockSpeed = RollEncounterStat(rng, baseIv, SpeedIvSpread),
            iv_ComputingPower = RollEncounterStat(rng, baseIv + encounterGrade * TierStatBonus, OffenseIvSpread),
            iv_Throughput = RollEncounterStat(rng, baseIv + encounterGrade * TierStatBonus, OffenseIvSpread),
            iv_Firewall = RollEncounterStat(rng, baseIv, DefenseIvSpread),
            iv_Encryption = RollEncounterStat(rng, baseIv, DefenseIvSpread)
        };

        opponent.EnsureKnownSkillsFromLearnset();
        return opponent;
    }

    // Defense note: Runs the hacker party size helper used by this script.
    private static int HackerPartySize(GridNode node)
    {
        if (node == null || node.nodeType != NodeType.Hacker)
            return 1;

        return node.depthBand == EncounterDepthBand.Late || node.dangerRating >= 4
            ? HackerMaxPartySize
            : HackerBasePartySize;
    }

    // Defense note: Runs the encounter grade helper used by this script.
    private static int EncounterGrade(NodeType type)
    {
        switch (type)
        {
            case NodeType.Boss:
                return 3;
            case NodeType.Elite:
                return 2;
            default:
                return 1;
        }
    }

    // Defense note: Runs the roll encounter stat helper used by this script.
    private static int RollEncounterStat(System.Random rng, int baseValue, int spread)
    {
        return Mathf.Clamp(baseValue + rng.Next(-spread, spread + 1), 1, 255);
    }

    // Defense note: Runs the pick encounter species helper used by this script.
    private static AlgoMonData PickEncounterSpecies(GridNode node, int hash, string preferredBossSpeciesCodeName, out bool usesTransientData)
    {
        AlgoMonData[] pool = LoadEncounterSpecies();
        if (pool.Length == 0)
        {
            usesTransientData = true;
            return CreateFallbackSpecies(node, hash, preferredBossSpeciesCodeName);
        }

        usesTransientData = false;
        if (node != null && node.nodeType == NodeType.Boss)
        {
            AlgoMonData preferredBoss = FindSpeciesByCodeName(pool, preferredBossSpeciesCodeName);
            if (preferredBoss != null)
                return preferredBoss;
        }

        int index = Mathf.Abs(hash) % pool.Length;
        return pool[index];
    }

    // Defense note: Finds the species by code name reference used by this component.
    private static AlgoMonData FindSpeciesByCodeName(AlgoMonData[] pool, string codeName)
    {
        string normalized = NormalizeSpeciesKey(codeName);
        if (pool == null || pool.Length == 0 || string.IsNullOrEmpty(normalized))
            return null;

        for (int i = 0; i < pool.Length; i++)
        {
            AlgoMonData species = pool[i];
            if (species != null &&
                string.Equals(NormalizeSpeciesKey(species.codeName), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return species;
            }
        }

        return null;
    }

    // Defense note: Loads the encounter species asset or data needed at runtime.
    private static AlgoMonData[] LoadEncounterSpecies()
    {
        EncounterSpeciesCatalog catalog = Resources.Load<EncounterSpeciesCatalog>(EncounterSpeciesCatalogResourcePath);
        if (catalog != null)
        {
            AlgoMonData[] catalogSpecies = catalog.GetSpecies();
            if (catalogSpecies.Length > 0)
                return catalogSpecies;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AlgoMonData", new[] { AlgoMonAssetSearchFolder });
        var species = new List<AlgoMonData>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AlgoMonData data = AssetDatabase.LoadAssetAtPath<AlgoMonData>(path);
            if (data != null)
                species.Add(data);
        }

        species.Sort((a, b) => string.Compare(a.codeName, b.codeName, StringComparison.Ordinal));
        return species.ToArray();
#else
        return new AlgoMonData[0];
#endif
    }

    // Defense note: Creates the fallback species object used by the scene or runtime.
    private static AlgoMonData CreateFallbackSpecies(GridNode node, int hash, string preferredBossSpeciesCodeName)
    {
        AlgoMonData data = ScriptableObject.CreateInstance<AlgoMonData>();
        string preferredBossName = NormalizeSpeciesKey(preferredBossSpeciesCodeName);
        data.codeName = node != null && node.nodeType == NodeType.Boss && !string.IsNullOrEmpty(preferredBossName)
            ? preferredBossName
            : $"{node.nodeType} AlgoMon";
        data.description = "Runtime encounter generated from TheGrid.";
        data.elementType = (ElementType)(Mathf.Abs(hash) % Enum.GetValues(typeof(ElementType)).Length);
        data.learnset = new LearnsetEntry[0];
        return data;
    }

    // Defense note: Runs the normalize species key helper used by this script.
    private static string NormalizeSpeciesKey(string codeName)
    {
        return string.IsNullOrWhiteSpace(codeName) ? string.Empty : codeName.Trim();
    }

    // Defense note: Builds the opponent name data or UI structure.
    private static string BuildOpponentName(AlgoMonData species, GridNode node, int partySlot, int partySize)
    {
        string speciesName = species != null && !string.IsNullOrWhiteSpace(species.codeName)
            ? species.codeName.Trim()
            : $"{node.nodeType} AlgoMon";

        switch (node.nodeType)
        {
            case NodeType.Hacker:
                return speciesName;
            case NodeType.Boss:
                return $"{speciesName} Prime";
            case NodeType.Elite:
                return $"{speciesName} Elite";
            default:
                return speciesName;
        }
    }

    // Defense note: Runs the battle form name helper used by this script.
    private static string BattleFormName(GridNode node)
    {
        return node != null && node.nodeType == NodeType.Boss ? "Evolved" : "Base";
    }

    // Defense note: Runs the fallback encounter level helper used by this script.
    private static int FallbackEncounterLevel(ThreatTier threatTier, GridNode node, System.Random rng)
    {
        int assumedBossLayer = Mathf.Max(node != null ? node.layer + 3 : 3, 3);
        int nodeLayer = node != null ? node.layer : 1;
        NodeType nodeType = node != null ? node.nodeType : NodeType.Combat;
        return ThreatTierRules.EncounterLevel(
            threatTier,
            nodeType,
            nodeLayer,
            assumedBossLayer,
            0,
            rng.Next(0, LevelRandomExclusiveMax));
    }

    // Defense note: Runs the stable hash helper used by this script.
    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ value[i]) * 16777619;
            return hash & int.MaxValue;
        }
    }
}
