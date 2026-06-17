/*
Script Audit:
- Purpose: Runtime-loadable lookup of UI assets keyed by their original asset path, so standalone builds can resolve sprites/textures that the editor resolves through AssetDatabase.
- Attached GameObject: None; ScriptableObject asset stored under Assets/_AlgoMon/Resources.
- Main responsibilities: Hold path-to-asset entries, build a case-insensitive dictionary on first lookup, expose typed static finders.
- Important variables: entries, ResourcePath, FindSprite/FindTexture/FindText.
- Inputs: Asset paths requested by UI controllers (MainTerminalController, GridLinkTransition, NicoBitmapFontReference).
- Outputs or effects: Returns the baked asset or null; null keeps the existing flat-color fallbacks working.
- Testing notes: Rebuild via AlgoMon > Build > Rebuild Runtime Asset Catalogs, then verify the settings sliders and transitions render styled in a standalone build.
*/
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeUiAssetCatalog", menuName = "AlgoMon/Runtime UI Asset Catalog")]
// Defense note: RuntimeUiAssetCatalog stores lookup data so runtime systems can find the right assets.
public class RuntimeUiAssetCatalog : ScriptableObject
{
    [System.Serializable]
    // Defense note: Entry is the main entry type used by this part of the project.
    public class Entry
    {
        public string path;
        public Object asset;
    }

    private const string ResourcePath = "RuntimeUiAssetCatalog";

    [SerializeField] private Entry[] entries;

    private static RuntimeUiAssetCatalog instance;
    private static bool searched;

    private Dictionary<string, Object> lookup;

    // Defense note: Finds the sprite reference used by this component.
    public static Sprite FindSprite(string assetPath) => Find(assetPath) as Sprite;

    // Defense note: Finds the texture reference used by this component.
    public static Texture2D FindTexture(string assetPath) => Find(assetPath) as Texture2D;

    // Defense note: Finds the text reference used by this component.
    public static TextAsset FindText(string assetPath) => Find(assetPath) as TextAsset;

    // Defense note: Runs the find helper used by this script.
    public static Object Find(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        if (instance == null && !searched)
        {
            instance = Resources.Load<RuntimeUiAssetCatalog>(ResourcePath);
            searched = true;
        }

        return instance != null ? instance.Lookup(assetPath) : null;
    }

    // Defense note: Runs the lookup helper used by this script.
    private Object Lookup(string assetPath)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<string, Object>(System.StringComparer.OrdinalIgnoreCase);
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    Entry entry = entries[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.path) && entry.asset != null)
                        lookup[entry.path] = entry.asset;
                }
            }
        }

        return lookup.TryGetValue(assetPath, out Object asset) ? asset : null;
    }

    // Defense note: Updates the entries for editor state or visual value.
    public void SetEntriesForEditor(Entry[] value)
    {
        entries = value;
        lookup = null;
    }
}
