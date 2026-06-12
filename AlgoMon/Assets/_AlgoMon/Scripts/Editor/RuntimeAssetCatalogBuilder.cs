/*
Script Audit:
- Purpose: Editor tool that bakes the runtime asset catalogs so standalone builds resolve the assets the editor loads through AssetDatabase.
- Attached GameObject: None; static editor menu command.
- Main responsibilities: Build BattleAnimationProfile assets per species/form from the sprite folders, bake the UI sprite/texture/bitmap-font path lookup, and save both catalog assets under Assets/_AlgoMon/Resources.
- Important variables: UiSpritePaths, UiTexturePaths, ProfileAssetFolder, ProfileCatalogPath, UiCatalogPath.
- Inputs: Sprite folders under Assets/_AlgoMon/Sprites, bitmap fonts under Assets/_AlgoMon/Fonts/NicoBitmap, and the UI paths mirrored from MainTerminalController and GridLinkTransition.
- Outputs or effects: Creates/updates BattleAnimationProfileCatalog.asset, RuntimeUiAssetCatalog.asset, and one BattleAnimationProfile asset per species form.
- Testing notes: Run AlgoMon > Build > Rebuild Runtime Asset Catalogs, check the console summary, then make a standalone build and verify battle animations and styled menus.
*/
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RuntimeAssetCatalogBuilder
{
    private const string SpriteRoot = "Assets/_AlgoMon/Sprites";
    private const string BitmapFontRoot = "Assets/_AlgoMon/Fonts/NicoBitmap";
    private const string ProfileAssetFolder = "Assets/_AlgoMon/ScriptableObjects/BattleAnimationProfiles";
    private const string ProfileCatalogPath = "Assets/_AlgoMon/Resources/BattleAnimationProfileCatalog.asset";
    private const string UiCatalogPath = "Assets/_AlgoMon/Resources/RuntimeUiAssetCatalog.asset";

    private const string MainTerminalSpriteRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal";
    private const string CyberHudSpriteRoot = MainTerminalSpriteRoot + "/CyberpunkHUD";
    private const string PixelHudSpriteRoot = MainTerminalSpriteRoot + "/PixelUIHUD";

    // Mirrors the sprite paths requested by MainTerminalController and GridLinkTransition.
    // A missing file here is fine: the loaders fall back to flat-color styling.
    private static readonly string[] UiSpritePaths =
    {
        MainTerminalSpriteRoot + "/Inspector/PanelFrame01.png",
        MainTerminalSpriteRoot + "/Inspector/PanelFrame03.png",
        MainTerminalSpriteRoot + "/Components/CyberpunkHUD/progress_fill_striped_texture_tint.png",
        CyberHudSpriteRoot + "/health_bar_under.png",
        CyberHudSpriteRoot + "/toggle_on.png",
        CyberHudSpriteRoot + "/toggle_off.png",
        CyberHudSpriteRoot + "/slider_track_bg.png",
        CyberHudSpriteRoot + "/slider_fill_highlight.png",
        CyberHudSpriteRoot + "/slider_handle.png",
        CyberHudSpriteRoot + "/panel_base_01_outer_shell.png",
        CyberHudSpriteRoot + "/hud_radar_frame.png",
        CyberHudSpriteRoot + "/deco_misc_03.png",
        CyberHudSpriteRoot + "/icon_skill_06.png",
        CyberHudSpriteRoot + "/progress_bar_striped_frame.png",
        CyberHudSpriteRoot + "/progress_fill_striped_texture.png",
        PixelHudSpriteRoot + "/Panels/Blue/PanelDigital.png",
        PixelHudSpriteRoot + "/Panels/White/FrameDigitalLarge.png",
        PixelHudSpriteRoot + "/Panels/White/PanelOutlined.png",
        PixelHudSpriteRoot + "/SkillTree/White/SkillSlotSharp.png",
        PixelHudSpriteRoot + "/SkillTree/White/SkillSlotRound.png",
        PixelHudSpriteRoot + "/SkillTree/White/ConnectorThinHorizontal.png",
        PixelHudSpriteRoot + "/SkillTree/White/ConnectorHorizontal.png",
        PixelHudSpriteRoot + "/Selectors/Reticle_Select.png",
        PixelHudSpriteRoot + "/Selectors/ChevronRight_Select.png",
        PixelHudSpriteRoot + "/Selectors/Square_Select.png",
        PixelHudSpriteRoot + "/Grid/White/SelectorEdge_Focus.png",
        PixelHudSpriteRoot + "/Grid/White/SelectorThick_Focus.png",
    };

    // Panel buttons are loaded as textures because MainTerminalController creates
    // sprites from them with a custom pixels-per-unit.
    private static readonly string[] UiTexturePaths =
    {
        PixelHudSpriteRoot + "/Buttons/Blue/ButtonE_Unpressed.png",
        PixelHudSpriteRoot + "/Buttons/Blue/ButtonF_Pressed.png",
        PixelHudSpriteRoot + "/Buttons/Blue/ButtonStone_Highlighted.png",
    };

    [MenuItem("AlgoMon/Build/Rebuild Runtime Asset Catalogs")]
    public static void RebuildAll()
    {
        int profileCount = RebuildBattleAnimationProfileCatalog();
        int uiCount = RebuildUiAssetCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Runtime asset catalogs rebuilt: {profileCount} battle animation profiles, {uiCount} UI assets.");
    }

    private static int RebuildBattleAnimationProfileCatalog()
    {
        EnsureFolder(ProfileAssetFolder);

        var entries = new List<BattleAnimationProfileCatalog.Entry>();
        foreach (string speciesFolder in AssetDatabase.GetSubFolders(SpriteRoot))
        {
            string species = Path.GetFileName(speciesFolder);
            if (IsSkippedSpeciesFolder(species))
                continue;

            foreach (string formFolder in AssetDatabase.GetSubFolders(speciesFolder))
            {
                string form = Path.GetFileName(formFolder);
                BattleAnimationProfile built = BattleAnimationProfileLoader.BuildProfileFromSpriteFolders(species, form);
                if (built == null)
                    continue;

                string displayName = ToDisplayCase(species);
                string assetPath = $"{ProfileAssetFolder}/{displayName}_{form}.asset";
                built.name = $"{displayName}_{form}";

                BattleAnimationProfile saved = AssetDatabase.LoadAssetAtPath<BattleAnimationProfile>(assetPath);
                if (saved != null)
                {
                    string keepName = saved.name;
                    EditorUtility.CopySerialized(built, saved);
                    saved.name = keepName;
                    Object.DestroyImmediate(built);
                    EditorUtility.SetDirty(saved);
                }
                else
                {
                    AssetDatabase.CreateAsset(built, assetPath);
                    saved = built;
                }

                entries.Add(new BattleAnimationProfileCatalog.Entry
                {
                    codeName = displayName,
                    formName = form,
                    profile = saved,
                });
            }
        }

        BattleAnimationProfileCatalog catalog = LoadOrCreate<BattleAnimationProfileCatalog>(ProfileCatalogPath);
        catalog.SetEntriesForEditor(entries.ToArray());
        EditorUtility.SetDirty(catalog);
        return entries.Count;
    }

    private static int RebuildUiAssetCatalog()
    {
        var entries = new List<RuntimeUiAssetCatalog.Entry>();

        foreach (string path in UiSpritePaths)
            AddEntry<Sprite>(entries, path);
        foreach (string path in UiTexturePaths)
            AddEntry<Texture2D>(entries, path);

        // Bitmap feedback fonts: atlas texture plus metrics text per font folder.
        if (AssetDatabase.IsValidFolder(BitmapFontRoot))
        {
            foreach (string fontFolder in AssetDatabase.GetSubFolders(BitmapFontRoot))
            {
                foreach (string file in Directory.GetFiles(fontFolder, "*.*", SearchOption.TopDirectoryOnly))
                {
                    string assetPath = file.Replace('\\', '/');
                    if (assetPath.EndsWith(".png"))
                        AddEntry<Texture2D>(entries, assetPath);
                    else if (assetPath.EndsWith(".txt") || assetPath.EndsWith(".fnt") || assetPath.EndsWith(".lua"))
                        AddEntry<TextAsset>(entries, assetPath);
                }
            }
        }

        // Species roster stills (the top-level <Species>_<Form>.png next to the animation folders).
        foreach (string speciesFolder in AssetDatabase.GetSubFolders(SpriteRoot))
        {
            if (IsSkippedSpeciesFolder(Path.GetFileName(speciesFolder)))
                continue;

            foreach (string file in Directory.GetFiles(speciesFolder, "*.png", SearchOption.TopDirectoryOnly))
                AddEntry<Sprite>(entries, file.Replace('\\', '/'));
        }

        RuntimeUiAssetCatalog catalog = LoadOrCreate<RuntimeUiAssetCatalog>(UiCatalogPath);
        catalog.SetEntriesForEditor(entries.ToArray());
        EditorUtility.SetDirty(catalog);
        return entries.Count;
    }

    private static bool IsSkippedSpeciesFolder(string folderName)
    {
        return string.Equals(folderName, "UI", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(folderName, "Effects", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void AddEntry<T>(List<RuntimeUiAssetCatalog.Entry> entries, string assetPath) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
        {
            Debug.Log($"Runtime asset catalog: skipped missing {assetPath}");
            return;
        }

        entries.Add(new RuntimeUiAssetCatalog.Entry { path = assetPath, asset = asset });
    }

    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        return asset;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string name = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static string ToDisplayCase(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            return folderName;
        if (folderName.Length == 1)
            return folderName.ToUpperInvariant();
        return char.ToUpperInvariant(folderName[0]) + folderName.Substring(1).ToLowerInvariant();
    }
}
