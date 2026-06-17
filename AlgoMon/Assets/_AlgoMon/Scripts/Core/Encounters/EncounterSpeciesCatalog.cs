using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-loadable encounter species list.
/// Put one catalog asset under a Resources folder so builds do not depend on
/// editor-only AssetDatabase scans.
/// </summary>
[CreateAssetMenu(fileName = "EncounterSpeciesCatalog", menuName = "AlgoMon/Encounter Species Catalog")]
// Defense note: EncounterSpeciesCatalog stores lookup data so runtime systems can find the right assets.
public class EncounterSpeciesCatalog : ScriptableObject
{
    [SerializeField] private AlgoMonData[] species;

    // Defense note: Retrieves the species value used by this system.
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

    // Defense note: Updates the species for editor state or visual value.
    public void SetSpeciesForEditor(AlgoMonData[] value)
    {
        species = value;
    }
}
