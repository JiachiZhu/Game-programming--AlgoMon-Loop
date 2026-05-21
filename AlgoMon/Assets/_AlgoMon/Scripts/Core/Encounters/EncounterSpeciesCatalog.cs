using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-loadable encounter species list.
/// Put one catalog asset under a Resources folder so builds do not depend on
/// editor-only AssetDatabase scans.
/// </summary>
[CreateAssetMenu(fileName = "EncounterSpeciesCatalog", menuName = "AlgoMon/Encounter Species Catalog")]
public class EncounterSpeciesCatalog : ScriptableObject
{
    [SerializeField] private AlgoMonData[] species;

    public AlgoMonData[] GetSpecies()
    {
        if (species == null || species.Length == 0)
            return new AlgoMonData[0];

        var valid = new List<AlgoMonData>(species.Length);
        for (int i = 0; i < species.Length; i++)
        {
            if (species[i] != null)
                valid.Add(species[i]);
        }

        valid.Sort((a, b) => string.CompareOrdinal(a.codeName, b.codeName));
        return valid.ToArray();
    }

    public void SetSpeciesForEditor(AlgoMonData[] value)
    {
        species = value;
    }
}
