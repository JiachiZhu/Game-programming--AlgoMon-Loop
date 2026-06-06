/*
Script Audit:
- Purpose: Loads editor-time battle animation profiles from sprite folders and optional manifest files.
- Attached GameObject: None; this is a static editor helper used by BattlePresentationController.
- Main responsibilities: Find species/form sprite folders, create a runtime BattleAnimationProfile, apply manifest timing, load clip frames, and fix sprite import settings.
- Important variables: SpriteRoot, ManifestName, TryLoadEditorProfile.
- Inputs: AlgoMon codeName, formName, sprite folders, and battle_animation_manifest.json files.
- Outputs or effects: Returns an editor-only BattleAnimationProfile for preview/play mode animation.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Add frames under Assets/_AlgoMon/Sprites/SPECIES/Form and confirm the profile auto-loads in the editor.
*/
using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class BattleAnimationProfileLoader
{
    private const string SpriteRoot = "Assets/_AlgoMon/Sprites";
    private const string ManifestName = "battle_animation_manifest.json";

    public static BattleAnimationProfile TryLoadEditorProfile(string codeName, string formName)
    {
#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(codeName))
            return null;

        string speciesFolder = codeName.Trim().ToUpperInvariant();
        string form = string.IsNullOrWhiteSpace(formName) ? "Base" : formName.Trim();
        string root = $"{SpriteRoot}/{speciesFolder}/{form}";
        if (!AssetDatabase.IsValidFolder(root))
            return null;

        BattleAnimationProfile profile = ScriptableObject.CreateInstance<BattleAnimationProfile>();
        profile.profileId = $"{codeName}_{form}_EditorRuntime";

        ApplyManifest(profile, root);
        LoadClipFrames(profile.entry, root, "Entry");
        LoadClipFrames(profile.idle, root, "Idle");
        LoadClipFrames(profile.attack, root, "Attack");
        LoadClipFrames(profile.defense, root, "Defense");
        LoadClipFrames(profile.status, root, "Status");
        LoadClipFrames(profile.hit, root, "Hit");
        LoadClipFrames(profile.faint, root, "Faint");

        return HasAnyFrames(profile) ? profile : null;
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static bool HasAnyFrames(BattleAnimationProfile profile)
    {
        return profile != null &&
               ((profile.idle != null && profile.idle.HasFrames) ||
                (profile.entry != null && profile.entry.HasFrames) ||
                (profile.attack != null && profile.attack.HasFrames) ||
                (profile.defense != null && profile.defense.HasFrames) ||
                (profile.status != null && profile.status.HasFrames) ||
                (profile.hit != null && profile.hit.HasFrames) ||
                (profile.faint != null && profile.faint.HasFrames));
    }

    private static void ApplyManifest(BattleAnimationProfile profile, string root)
    {
        string manifestAssetPath = $"{root}/{ManifestName}";
        string manifestPath = Path.Combine(Directory.GetCurrentDirectory(), manifestAssetPath);
        if (!File.Exists(manifestPath))
            return;

        string json = File.ReadAllText(manifestPath);
        BattleAnimationProfileManifest manifest = JsonUtility.FromJson<BattleAnimationProfileManifest>(json);
        if (manifest == null)
            return;

        if (!string.IsNullOrWhiteSpace(manifest.profileId))
            profile.profileId = manifest.profileId;
        profile.mirrorX = manifest.mirrorX;
        if (manifest.visualScaleMultiplier > 0f)
            profile.visualScaleMultiplier = manifest.visualScaleMultiplier;

        ApplyClipManifest(profile.entry, manifest.entry);
        ApplyClipManifest(profile.idle, manifest.idle);
        ApplyClipManifest(profile.attack, manifest.attack);
        ApplyClipManifest(profile.defense, manifest.defense);
        ApplyClipManifest(profile.status, manifest.status);
        ApplyClipManifest(profile.hit, manifest.hit);
        ApplyClipManifest(profile.faint, manifest.faint);
    }

    private static void ApplyClipManifest(BattleAnimationClipData clip, BattleAnimationClipManifest manifest)
    {
        if (clip == null || manifest == null)
            return;

        if (manifest.fps > 0f)
            clip.fps = manifest.fps;
        clip.loop = manifest.loop;
        if (manifest.startFrame > 0)
            clip.startFrame = manifest.startFrame;
        clip.actionFrame = manifest.actionFrame;
        clip.contactFrame = manifest.contactFrame;
        clip.returnFrame = manifest.returnFrame;
        clip.smoothContactMovement = manifest.smoothContactMovement;
        clip.smoothReturnMovement = manifest.smoothReturnMovement;
        if (manifest.contactDistanceFromTarget >= 0f)
            clip.contactDistanceFromTarget = manifest.contactDistanceFromTarget;
        clip.contactOffset = manifest.contactOffset;
        clip.holdLastFrame = manifest.holdLastFrame;
    }

    private static void LoadClipFrames(BattleAnimationClipData clip, string root, string actionName)
    {
        if (clip == null)
            return;

        string folder = $"{root}/{actionName}";
        if (!AssetDatabase.IsValidFolder(folder))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        Array.Sort(guids, (a, b) =>
        {
            string pathA = AssetDatabase.GUIDToAssetPath(a);
            string pathB = AssetDatabase.GUIDToAssetPath(b);
            return string.Compare(pathA, pathB, StringComparison.OrdinalIgnoreCase);
        });

        Sprite[] frames = new Sprite[guids.Length];
        int count = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            EnsureSpriteImporter(assetPath);
            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (frame != null)
                frames[count++] = frame;
        }

        if (count != frames.Length)
            Array.Resize(ref frames, count);

        clip.frames = frames;
    }

    private static void EnsureSpriteImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.Sprite)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    [Serializable]
    private class BattleAnimationProfileManifest
    {
        public string profileId;
        public bool mirrorX;
        public float visualScaleMultiplier = 1f;
        public BattleAnimationClipManifest entry;
        public BattleAnimationClipManifest idle;
        public BattleAnimationClipManifest attack;
        public BattleAnimationClipManifest defense;
        public BattleAnimationClipManifest status;
        public BattleAnimationClipManifest hit;
        public BattleAnimationClipManifest faint;
    }

    [Serializable]
    private class BattleAnimationClipManifest
    {
        public float fps = -1f;
        public bool loop;
        public int startFrame = -1;
        public int actionFrame = -1;
        public int contactFrame = -1;
        public int returnFrame = -1;
        public bool smoothContactMovement;
        public bool smoothReturnMovement;
        public float contactDistanceFromTarget = -1f;
        public Vector2 contactOffset;
        public bool holdLastFrame;
    }
#endif
}
