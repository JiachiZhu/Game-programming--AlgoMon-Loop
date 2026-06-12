/*
Script Audit:
- Purpose: Runtime-loadable lookup of baked BattleAnimationProfile assets per species and form, so standalone builds can animate AlgoMons without editor-only sprite folder scans.
- Attached GameObject: None; ScriptableObject asset stored under Assets/_AlgoMon/Resources.
- Main responsibilities: Hold codeName+formName to profile entries and resolve them with the same normalization rules the editor loader uses (case-insensitive, Evolve/Evolved alias, empty form = Base).
- Important variables: entries, ResourcePath, Find.
- Inputs: Species codeName and battle form name from battle/menu controllers.
- Outputs or effects: Returns the baked profile or null; null keeps the existing portrait/static fallbacks working.
- Testing notes: Rebuild via AlgoMon > Build > Rebuild Runtime Asset Catalogs, then verify battle entry/attack/faint animations play in a standalone build.
*/
using UnityEngine;

[CreateAssetMenu(fileName = "BattleAnimationProfileCatalog", menuName = "AlgoMon/Battle Animation Profile Catalog")]
public class BattleAnimationProfileCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string codeName;
        public string formName;
        public BattleAnimationProfile profile;
    }

    private const string ResourcePath = "BattleAnimationProfileCatalog";

    [SerializeField] private Entry[] entries;

    private static BattleAnimationProfileCatalog instance;
    private static bool searched;

    public static BattleAnimationProfile Find(string codeName, string formName)
    {
        if (string.IsNullOrWhiteSpace(codeName))
            return null;

        if (instance == null && !searched)
        {
            instance = Resources.Load<BattleAnimationProfileCatalog>(ResourcePath);
            searched = true;
        }

        if (instance == null || instance.entries == null)
            return null;

        string code = codeName.Trim();
        string form = NormalizeForm(formName);
        Entry[] entries = instance.entries;
        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (entry == null || entry.profile == null)
                continue;

            if (string.Equals(entry.codeName, code, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeForm(entry.formName), form, System.StringComparison.OrdinalIgnoreCase))
            {
                return entry.profile;
            }
        }

        return null;
    }

    private static string NormalizeForm(string formName)
    {
        if (string.IsNullOrWhiteSpace(formName))
            return "Base";

        string trimmed = formName.Trim();
        if (string.Equals(trimmed, "Evolve", System.StringComparison.OrdinalIgnoreCase))
            return "Evolved";

        return trimmed;
    }

    public void SetEntriesForEditor(Entry[] value)
    {
        entries = value;
    }
}
