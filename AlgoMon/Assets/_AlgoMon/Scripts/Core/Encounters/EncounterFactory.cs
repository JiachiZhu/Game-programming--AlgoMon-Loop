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
public static class EncounterFactory
{
    private const string AlgoMonAssetSearchFolder = "Assets/_AlgoMon/ScriptableObjects/AlgoMons";
    private const string EncounterSpeciesCatalogResourcePath = "EncounterSpeciesCatalog";

    private const int BaseLevel = 10;
    private const int LevelPerLayer = 2;
    private const int LevelPerTier = 3;
    private const int LevelRandomExclusiveMax = 3;

    private const int BaseIvFloor = 138;
    private const int IvPerLayer = 7;
    private const int IvPerTier = 18;
    private const int TierStatBonus = 4;

    private const int BatteryIvBonus = 20;
    private const int BatteryIvSpread = 14;
    private const int SpeedIvSpread = 18;
    private const int OffenseIvSpread = 16;
    private const int DefenseIvSpread = 16;

    public static AlgoMonInstance Create(int runSeed, GridNode node)
    {
        if (node == null)
            return null;

        int hash = StableHash($"{runSeed}:{node.id}:{node.nodeType}");
        var rng = new System.Random(hash);
        int tier = EncounterTier(node.nodeType);
        int baseIv = BaseIvFloor + node.layer * IvPerLayer + tier * IvPerTier;
        AlgoMonData species = PickEncounterSpecies(node, hash, out bool usesTransientData);

        var opponent = new AlgoMonInstance
        {
            data = species,
            nickname = BuildOpponentName(species, node),
            usesTransientData = usesTransientData,
            level = Mathf.Clamp(
                BaseLevel + node.layer * LevelPerLayer + tier * LevelPerTier + rng.Next(0, LevelRandomExclusiveMax),
                1,
                AlgoMonInstance.MAX_LEVEL),
            iv_Battery = RollEncounterStat(rng, baseIv + BatteryIvBonus, BatteryIvSpread),
            iv_ClockSpeed = RollEncounterStat(rng, baseIv, SpeedIvSpread),
            iv_ComputingPower = RollEncounterStat(rng, baseIv + tier * TierStatBonus, OffenseIvSpread),
            iv_Throughput = RollEncounterStat(rng, baseIv + tier * TierStatBonus, OffenseIvSpread),
            iv_Firewall = RollEncounterStat(rng, baseIv, DefenseIvSpread),
            iv_Encryption = RollEncounterStat(rng, baseIv, DefenseIvSpread)
        };

        opponent.EnsureKnownSkillsFromLearnset();
        return opponent;
    }

    private static int EncounterTier(NodeType type)
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

    private static int RollEncounterStat(System.Random rng, int baseValue, int spread)
    {
        return Mathf.Clamp(baseValue + rng.Next(-spread, spread + 1), 1, 255);
    }

    private static AlgoMonData PickEncounterSpecies(GridNode node, int hash, out bool usesTransientData)
    {
        AlgoMonData[] pool = LoadEncounterSpecies();
        if (pool.Length == 0)
        {
            usesTransientData = true;
            return CreateFallbackSpecies(node, hash);
        }

        usesTransientData = false;
        int index = Mathf.Abs(hash) % pool.Length;
        return pool[index];
    }

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

    private static AlgoMonData CreateFallbackSpecies(GridNode node, int hash)
    {
        AlgoMonData data = ScriptableObject.CreateInstance<AlgoMonData>();
        data.codeName = $"{node.nodeType} AlgoMon";
        data.description = "Runtime encounter generated from TheGrid.";
        data.elementType = (ElementType)(Mathf.Abs(hash) % Enum.GetValues(typeof(ElementType)).Length);
        data.learnset = new LearnsetEntry[0];
        return data;
    }

    private static string BuildOpponentName(AlgoMonData species, GridNode node)
    {
        string speciesName = species != null && !string.IsNullOrWhiteSpace(species.codeName)
            ? species.codeName.Trim()
            : $"{node.nodeType} AlgoMon";

        switch (node.nodeType)
        {
            case NodeType.Boss:
                return $"{speciesName} Prime";
            case NodeType.Elite:
                return $"{speciesName} Elite";
            default:
                return speciesName;
        }
    }

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
