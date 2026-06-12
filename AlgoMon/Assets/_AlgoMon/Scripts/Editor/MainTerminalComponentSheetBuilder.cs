using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainTerminalComponentSheetBuilder
{
    private const string SpriteRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal/Components";
    private const string HudRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD";
    private const string HudDerivedRoot = SpriteRoot + "/CyberpunkHUD";
    private const string PixelRoot = "Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD";
    private const string PrefabRoot = "Assets/_AlgoMon/Prefabs/UI/MainTerminal";
    private const string PseudoSpriteRoot = "Assets/Resources/UI/MainTerminal/PseudoSprite_Tier";
    private const string CroppedPseudoSpriteRoot = SpriteRoot + "/PseudoSpriteCropped_Tier";
    private const string MainMenuOuterShellPath = HudDerivedRoot + "/panel_base_01_outer_shell.png";
    private const string SourceLayoutTrialName = "MainTerminal_SourceLayoutTrialVisual";
    private const string SourceLayoutTrialPrefabPath = PrefabRoot + "/MainTerminal_SourceLayoutTrialVisual.prefab";
    private const float TrialButtonTopY = 150f;
    private const float TrialButtonSpacing = 96f;

    private const string ScanlineTileSpritePath = SpriteRoot + "/MainTerminal_ScanlineTile.png";
    private const string BitmapFontAtlasPath = "Assets/_AlgoMon/Fonts/NicoBitmap/PaintBasic/PaintBasic.png";
    private const string BitmapFontMetricsPath = "Assets/_AlgoMon/Fonts/NicoBitmap/PaintBasic/PaintBasic.txt";
    private const string TerminalFontPath = "Assets/_AlgoMon/Fonts/CyberpunkUISystem/Techno.ttf";

    private static readonly Color Background = CyberUiTheme.Background;
    private static readonly Color Panel = new Color(0.020f, 0.018f, 0.055f, 1f);
    private static readonly Color Primary = CyberUiTheme.Primary;
    private static readonly Color Selected = CyberUiTheme.Selected;
    private static readonly Color Danger = CyberUiTheme.Danger;
    private static readonly Color Reward = CyberUiTheme.Reward;
    private static readonly Color Success = CyberUiTheme.Success;
    private static readonly Color TextPrimary = CyberUiTheme.TextPrimary;
    private static readonly Color TextSecondary = CyberUiTheme.TextSecondary;
    private static readonly string[] SharpHudPrefixes =
    {
        "bar_",
        "btn_",
        "deco_",
        "frame_",
        "health_bar_",
        "hud_",
        "icon_",
        "panel_",
        "progress_",
        "slider_",
        "slot_",
        "toggle_"
    };

    [MenuItem("Tools/AlgoMon/UI/Rebuild MainTerminal Component Sheet")]
    public static void Rebuild()
    {
        EnsureFolder(SpriteRoot);
        EnsureFolder(HudDerivedRoot);
        EnsureFolder(PrefabRoot);
        EnsureBitmapFontImportSettings();
        AssetDatabase.Refresh();
        EnsureHudSourceImportSettings();
        EnsureMainMenuOuterShellImportSettings();
        EnsurePixelSourceImportSettings();
        GenerateSprites();
        AssetDatabase.Refresh();

        GameObject terminalPanel = CreateTerminalPanelPrefab();
        SavePrefab(terminalPanel, PrefabRoot + "/MainTerminal_TerminalPanel.prefab");

        GameObject commandButton = CreateCommandButtonPrefab("MainTerminal_CommandButton", "ENTER GRID", Primary, "icon_skill_06.png", false);
        SavePrefab(commandButton, PrefabRoot + "/MainTerminal_CommandButton.prefab");

        GameObject tierCard = CreateTierCardPrefab("MainTerminal_TierCard", 3, Selected, true);
        SavePrefab(tierCard, PrefabRoot + "/MainTerminal_TierCard.prefab");

        GameObject pseudoWindow = CreatePseudoSpriteWindowPrefab();
        SavePrefab(pseudoWindow, PrefabRoot + "/MainTerminal_PseudoSpriteWindow.prefab");

        GameObject scanlineStrip = CreateScanlineStripPrefab();
        SavePrefab(scanlineStrip, PrefabRoot + "/MainTerminal_ScanlineStrip.prefab");

        GameObject accentRail = CreateAccentRailPrefab();
        SavePrefab(accentRail, PrefabRoot + "/MainTerminal_AccentRail.prefab");

        GameObject statusChip = CreateStatusChipPrefab();
        SavePrefab(statusChip, PrefabRoot + "/MainTerminal_StatusChip.prefab");

        GameObject dagNode = CreateDagNodePrefab();
        SavePrefab(dagNode, PrefabRoot + "/MainTerminal_DagNode.prefab");

        GameObject dagPreview = CreateDagPreviewPrefab();
        SavePrefab(dagPreview, PrefabRoot + "/MainTerminal_DagPreview.prefab");

        GameObject valueBar = CreateValueBarPrefab();
        SavePrefab(valueBar, PrefabRoot + "/MainTerminal_ValueBar.prefab");

        GameObject moduleSlot = CreateModuleSlotPrefab();
        SavePrefab(moduleSlot, PrefabRoot + "/MainTerminal_ModuleSlot.prefab");

        GameObject sourceLayout = CreateSourceLayoutPrefab();
        SavePrefab(sourceLayout, PrefabRoot + "/MainTerminal_SourceLayout.prefab");

        GameObject sourceLayoutTrial = CreateMainMenuSourceLayoutTrialPrefab();
        SavePrefab(sourceLayoutTrial, SourceLayoutTrialPrefabPath);

        GameObject sheet = CreateComponentSheetPrefab();
        SavePrefab(sheet, PrefabRoot + "/MainTerminal_ComponentSheet.prefab");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt MainTerminal component sheet prefabs and 9-slice sprites.");
    }

    [MenuItem("Tools/AlgoMon/UI/Show MainTerminal Component Sheet Preview")]
    public static void ShowPreview()
    {
        ClearPreview();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/MainTerminal_ComponentSheet.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("MainTerminal component sheet prefab is missing. Run rebuild first.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "MainTerminal_ComponentSheet_Preview";
        instance.hideFlags = HideFlags.DontSave;
        Canvas canvas = instance.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = 5000;

        Debug.Log("Showing MainTerminal component sheet preview in the active scene.");
    }

    [MenuItem("Tools/AlgoMon/UI/Clear MainTerminal Component Sheet Preview")]
    public static void ClearPreview()
    {
        GameObject existing = GameObject.Find("MainTerminal_ComponentSheet_Preview");
        if (existing != null)
            Object.DestroyImmediate(existing);
    }

    [MenuItem("Tools/AlgoMon/UI/Export MainTerminal Component Sheet Preview PNG")]
    public static void ExportPreviewPng()
    {
        ExportPrefabPreviewPng(PrefabRoot + "/MainTerminal_ComponentSheet.prefab", "Assets/Screenshots/MainTerminal_component_sheet_assetpack_preview.png");
    }

    [MenuItem("Tools/AlgoMon/UI/Export MainTerminal Source Layout Preview PNG")]
    public static void ExportSourceLayoutPreviewPng()
    {
        ExportPrefabPreviewPng(PrefabRoot + "/MainTerminal_SourceLayout.prefab", "Assets/Screenshots/MainTerminal_source_layout_preview.png");
    }

    [MenuItem("Tools/AlgoMon/UI/Install Source Layout Trial Into MainTerminal Scene")]
    public static void InstallSourceLayoutTrialInMainTerminalScene()
    {
        RectTransform mainTerminalRoot = FindMainTerminalRoot();
        if (mainTerminalRoot == null)
        {
            Debug.LogWarning("MainTerminalRoot not found. Open Assets/_AlgoMon/Scenes/MainTerminal.unity first.");
            return;
        }

        ClearSourceLayoutTrialFromMainTerminalScene();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourceLayoutTrialPrefabPath);
        if (prefab == null)
        {
            Rebuild();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourceLayoutTrialPrefabPath);
        }

        if (prefab == null)
        {
            Debug.LogWarning("Source layout trial prefab is missing: " + SourceLayoutTrialPrefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = SourceLayoutTrialName;
        instance.transform.SetParent(mainTerminalRoot, false);
        Canvas parentCanvas = mainTerminalRoot.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            parentCanvas.pixelPerfect = true;
        const float trialScale = 0.68f;
        Vector2 trialCenter = new Vector2(1060f, 560f);
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = trialCenter;
        rect.sizeDelta = new Vector2(1600f, 900f);
        rect.localScale = Vector3.one * trialScale;
        ConfigureSourceLayoutTrialInteraction(instance);
        AlignMainMenuHitboxes(mainTerminalRoot, trialCenter, trialScale);

        instance.transform.SetAsLastSibling();
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log("Installed MainTerminal source layout trial visual. Existing transparent hitbox buttons remain in place.");
    }

    [MenuItem("Tools/AlgoMon/UI/Clear Source Layout Trial From MainTerminal Scene")]
    public static void ClearSourceLayoutTrialFromMainTerminalScene()
    {
        GameObject existing = GameObject.Find(SourceLayoutTrialName);
        if (existing == null)
            return;

        var scene = existing.scene;
        Object.DestroyImmediate(existing);
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/AlgoMon/UI/Export MainTerminal Source Layout Trial Scene PNG")]
    public static void ExportSourceLayoutTrialScenePng()
    {
        ExportMainTerminalCanvasPng("Assets/Screenshots/MainTerminal_source_layout_trial_scene.png");
    }

    private static void ExportPrefabPreviewPng(string prefabPath, string screenshotPath)
    {
        const int width = 1600;
        const int height = 900;

        EnsureFolder("Assets/Screenshots");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("Preview prefab is missing. Run rebuild first: " + prefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        GameObject cameraObject = new GameObject("MainTerminal_PreviewCamera", typeof(Camera));
        instance.hideFlags = HideFlags.DontSave;
        cameraObject.hideFlags = HideFlags.DontSave;

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.orthographic = true;
        camera.orthographicSize = height * 0.5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        Canvas canvas = instance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 0;
        }

        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        try
        {
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            output.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            output.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), screenshotPath), output.EncodeToPNG());
            AssetDatabase.ImportAsset(screenshotPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Exported MainTerminal preview: " + screenshotPath);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(instance);
        }
    }

    private static void ExportMainTerminalCanvasPng(string screenshotPath)
    {
        const int width = 1672;
        const int height = 941;

        EnsureFolder("Assets/Screenshots");
        GameObject canvasObject = GameObject.Find("Canvas_MainTerminal");
        Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            Debug.LogWarning("Canvas_MainTerminal not found. Open the MainTerminal scene first.");
            return;
        }

        GameObject cameraObject = new GameObject("MainTerminal_ScenePreviewCamera", typeof(Camera));
        cameraObject.hideFlags = HideFlags.DontSave;
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = height * 0.5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        RenderMode previousRenderMode = canvas.renderMode;
        Camera previousWorldCamera = canvas.worldCamera;
        float previousPlaneDistance = canvas.planeDistance;
        int previousSortingOrder = canvas.sortingOrder;
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 0;
            Canvas.ForceUpdateCanvases();

            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            output.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            output.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), screenshotPath), output.EncodeToPNG());
            AssetDatabase.ImportAsset(screenshotPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Exported MainTerminal scene preview: " + screenshotPath);
        }
        finally
        {
            canvas.renderMode = previousRenderMode;
            canvas.worldCamera = previousWorldCamera;
            canvas.planeDistance = previousPlaneDistance;
            canvas.sortingOrder = previousSortingOrder;
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static RectTransform FindMainTerminalRoot()
    {
        GameObject root = GameObject.Find("Canvas_MainTerminal/MainTerminalRoot");
        if (root == null)
            root = GameObject.Find("MainTerminalRoot");
        return root != null ? root.GetComponent<RectTransform>() : null;
    }

    private static void ForceVisualLayerNonInteractive(GameObject root)
    {
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
            selectables[i].interactable = false;
    }

    private static void ConfigureSourceLayoutTrialInteraction(GameObject root)
    {
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
            selectables[i].interactable = false;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!buttons[i].name.StartsWith("DepthButton_") &&
                !buttons[i].name.StartsWith("BossRoute_") &&
                !buttons[i].name.StartsWith("Button_"))
                continue;

            buttons[i].interactable = true;
            if (buttons[i].targetGraphic != null)
                buttons[i].targetGraphic.raycastTarget = true;
        }
    }

    private static void AlignMainMenuHitboxes(RectTransform mainTerminalRoot, Vector2 trialCenter, float trialScale)
    {
        Transform systemLogHitbox = mainTerminalRoot.Find("SystemLogButton");
        if (systemLogHitbox != null)
            systemLogHitbox.gameObject.SetActive(false);

        string[] hitboxNames =
        {
            "EnterGridButton",
            "GeneLabButton",
            "PayloadButton",
            "SettingsButton",
            "ExitButton"
        };

        for (int i = 0; i < hitboxNames.Length; i++)
        {
            Transform hitbox = mainTerminalRoot.Find(hitboxNames[i]);
            if (hitbox == null)
                continue;

            hitbox.gameObject.SetActive(true);

            RectTransform rect = hitbox.GetComponent<RectTransform>();
            if (rect == null)
                continue;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = trialCenter + new Vector2(-455f, TrialButtonTopY - i * TrialButtonSpacing) * trialScale;
            rect.sizeDelta = new Vector2(310f, 92f) * trialScale;
            rect.localScale = Vector3.one;

            Image image = hitbox.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(1f, 1f, 1f, 0.001f);
                image.raycastTarget = true;
            }
        }
    }

    private static void GenerateSprites()
    {
        GenerateHudTintMask("panel_base_01.png", new Vector4(190f, 190f, 120f, 120f), 1.15f);
        GenerateHudTintMask("panel_base_02.png", new Vector4(150f, 150f, 110f, 110f), 1.10f);
        GenerateHudTintMask("panel_base_03.png", new Vector4(160f, 160f, 90f, 90f), 1.12f);
        GenerateHudTintMask("panel_inventory_bg.png", new Vector4(210f, 210f, 180f, 180f), 1.05f);
        GenerateHudTintMask("panel_menu_frame_full.png", new Vector4(210f, 210f, 130f, 130f), 1.06f);
        GenerateHudTintMask("btn_wide_01.png", new Vector4(95f, 95f, 72f, 72f), 1.20f);
        GenerateHudTintMask("btn_trapezoid_base_01.png", new Vector4(110f, 110f, 54f, 54f), 1.16f);
        GenerateHudTintMask("btn_trapezoid_base_02.png", new Vector4(110f, 110f, 54f, 54f), 1.20f);
        GenerateHudTintMask("frame_square_01.png", new Vector4(150f, 150f, 110f, 110f), 1.08f);
        GenerateHudTintMask("frame_square_02.png", new Vector4(150f, 150f, 110f, 110f), 1.08f);
        GenerateHudTintMask("frame_square_03.png", new Vector4(160f, 160f, 42f, 42f), 1.14f);
        GenerateHudTintMask("frame_square_04.png", new Vector4(82f, 82f, 62f, 62f), 1.18f);
        GenerateHudTintMask("frame_square_05.png", new Vector4(160f, 160f, 44f, 44f), 1.16f);
        GenerateHudTintMask("frame_square_05.1.png", new Vector4(20f, 20f, 20f, 20f), 1.18f);
        GenerateHudTintMask("bar_frame_long.png", new Vector4(120f, 120f, 42f, 42f), 1.14f);
        GenerateHudTintMask("hud_bottom_bar.png", new Vector4(160f, 160f, 80f, 80f), 1.08f);
        GenerateHudTintMask("hud_radar_frame.png", new Vector4(120f, 120f, 120f, 120f), 1.10f);
        GenerateHudTintMask("slot_item_bg.png", new Vector4(76f, 76f, 76f, 76f), 1.12f);
        GenerateHudTintMask("progress_bar_striped_frame.png", new Vector4(120f, 120f, 48f, 48f), 1.08f);
        GenerateHudTintMask("progress_fill_striped_texture.png", Vector4.zero, 1.20f);
        GenerateHudTintMask("slider_fill_highlight.png", new Vector4(80f, 80f, 20f, 20f), 1.20f);
        GenerateHudTintMask("slider_track_bg.png", new Vector4(80f, 80f, 20f, 20f), 1.00f);
        GenerateHudTintMask("toggle_on.png", new Vector4(84f, 84f, 40f, 40f), 1.10f);
        GenerateHudTintMask("toggle_off.png", new Vector4(84f, 84f, 40f, 40f), 1.10f);
        GenerateHudTintMask("deco_misc_01.png", Vector4.zero, 1.14f);
        GenerateHudTintMask("deco_misc_02.png", Vector4.zero, 1.14f);
        GenerateHudTintMask("deco_misc_03.png", Vector4.zero, 1.14f);
        GenerateHudTintMask("deco_shape_01.png", Vector4.zero, 1.16f);
        GenerateHudTintMask("icon_datapack.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_credits_small.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_weapon_mod.png,.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_gameplay_01.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_gameplay_02.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_gameplay_03.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_gameplay_05.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_lock.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_nav_01.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_nav_06.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_nav_09.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_skill_06.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_skill_12.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_skill_13.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_skill_21.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_skill_23.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_skill_example.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_tech_01.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_tech_02.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_tech_03.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_tech_04.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_tech_05.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("icon_tech_06.png", Vector4.zero, 1.18f);
        GenerateHudTintMask("img_body_silhouette.png", Vector4.zero, 1.06f);
        GenerateHudTintMask("img_body_silhouette2.png", Vector4.zero, 1.06f);
        WriteScanlineSprite(ScanlineTileSpritePath);
        GenerateCroppedPseudoSprites();
    }

    private static GameObject CreateTerminalPanelPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_TerminalPanel", new Vector2(900f, 420f));
        AddSlicedImage(root, TerminalPanelSprite(), CyberUiTheme.WithAlpha(Primary, 0.64f), Image.Type.Sliced, false);
        RectTransform panelFill = CreateRect("PanelFill", root.transform);
        SetRect(panelFill, new Vector2(0f, -6f), new Vector2(842f, 360f));
        AddSlicedImage(panelFill.gameObject, null, new Color(0.004f, 0.014f, 0.026f, 0.86f), Image.Type.Simple, false);
        AddScanlineLayer(root.transform, 0.040f);
        AddHudSprite(root.transform, "TopCircuitRail", "frame_square_03.png", CyberUiTheme.WithAlpha(Primary, 0.30f), new Vector2(-2f, 196f), new Vector2(770f, 54f), Image.Type.Sliced);
        AddHudSprite(root.transform, "LowerCircuitRail", "bar_frame_long.png", CyberUiTheme.WithAlpha(Danger, 0.36f), new Vector2(70f, -178f), new Vector2(690f, 36f), Image.Type.Sliced);

        RectTransform header = CreateRect("HeaderFrame", root.transform);
        SetRect(header, new Vector2(0f, 154f), new Vector2(800f, 76f));
        AddSlicedImage(header.gameObject, HudTintSprite("frame_square_03.png"), CyberUiTheme.WithAlpha(Primary, 0.48f), Image.Type.Sliced, false);
        AddText(header.transform, "Title", "ALGOMON // SYSTEM_TERMINAL", 27, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-150f, 4f), new Vector2(460f, 44f));
        AddText(header.transform, "Session", "SESSION: ADMIN\nSTATUS ONLINE", 11, FontStyle.Bold, TextAnchor.MiddleLeft, Success, new Vector2(270f, 6f), new Vector2(190f, 46f));
        AddStatusBars(header.transform, new Vector2(460f, 10f));

        RectTransform body = CreateRect("DepthSelectPanel", root.transform);
        SetRect(body, new Vector2(20f, -30f), new Vector2(760f, 250f));
        AddSlicedImage(body.gameObject, HudTintSprite("panel_base_03.png"), CyberUiTheme.WithAlpha(Primary, 0.50f), Image.Type.Sliced, false);
        AddScanlineLayer(body, 0.026f);
        AddText(body, "BodyTitle", "DEPTH_SELECT.EXE", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Primary, new Vector2(-270f, 88f), new Vector2(260f, 34f));
        AddText(body, "BodyMeta", "SELECT THREAT AVATAR / ROUTE DEPTH / REWARD MODEL", 9, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary, new Vector2(-205f, 58f), new Vector2(430f, 22f));

        RectTransform avatar = CreateRect("ThreatAvatarWindow", body);
        SetRect(avatar, new Vector2(-260f, -35f), new Vector2(190f, 150f));
        AddSlicedImage(avatar.gameObject, HudTintSprite("hud_radar_frame.png"), CyberUiTheme.WithAlpha(Selected, 0.38f), Image.Type.Sliced, false);
        AddHudSprite(avatar, "RadarSweep", "deco_misc_02.png", CyberUiTheme.WithAlpha(Primary, 0.34f), Vector2.zero, new Vector2(126f, 112f), Image.Type.Simple, true);
        AddPseudoSprite(avatar, 3, new Vector2(0f, -8f), new Vector2(126f, 96f), Color.white);
        AddCornerTicks(avatar, Selected);

        AddMiniInfoBox(body, "LevelBox", "LV\n30-40", new Vector2(-70f, 20f), Primary);
        AddMiniInfoBox(body, "RiskBox", "RISK\nMID", new Vector2(80f, 20f), Reward);
        AddMiniInfoBox(body, "CoreBox", "CORE\nDAG", new Vector2(230f, 20f), Primary);
        AddEmbeddedTierCards(body);

        RectTransform reward = CreateRect("RewardSummary", body);
        SetRect(reward, new Vector2(205f, -86f), new Vector2(250f, 48f));
        AddSlicedImage(reward.gameObject, ThinFrameSprite(), Color.white, Image.Type.Sliced, false);
        AddText(reward, "RewardText", "REWARDS\nALGOMON EXP / CR / FORM DATA", 8, FontStyle.Bold, TextAnchor.MiddleLeft, Reward, new Vector2(6f, 0f), new Vector2(202f, 28f));

        AddAccentRail(root.transform, "BottomRail", new Vector2(0f, -188f), new Vector2(760f, 4f), Primary);
        return root;
    }

    private static void AddEmbeddedTierCards(Transform parent)
    {
        Color[] accents = { Success, Selected, Primary, Reward, Danger };
        for (int i = 0; i < accents.Length; i++)
        {
            int tier = i + 1;
            RectTransform card = CreateRect("EmbeddedTier_" + tier + "F", parent);
            SetRect(card, new Vector2(-30f + i * 96f, -42f), new Vector2(88f, 52f));
            AddSlicedImage(card.gameObject, TierCardSprite(), CyberUiTheme.WithAlpha(accents[i], tier == 3 ? 0.94f : 0.54f), Image.Type.Sliced, false);
            AddAccentRail(card, "TierAccent", new Vector2(-16f, 20f), new Vector2(42f, 3f), accents[i]);
            AddHudIcon(card, "NodeIcon", tier == 5 ? "icon_lock.png" : "icon_tech_02.png", CyberUiTheme.WithAlpha(accents[i], 0.86f), new Vector2(-24f, -3f), 28f);
            AddText(card, "TierLabel", tier + "F", 7, FontStyle.Bold, TextAnchor.MiddleRight, TextPrimary, new Vector2(20f, 7f), new Vector2(34f, 16f));
            AddText(card, "TierCode", "T0" + tier, 5, FontStyle.Bold, TextAnchor.MiddleRight, accents[i], new Vector2(20f, -13f), new Vector2(34f, 12f));
        }
    }

    private static GameObject CreateCommandButtonPrefab(string objectName, string label, Color accent, string iconFileName, bool disabled)
    {
        GameObject root = CreateUiRoot(objectName, new Vector2(320f, 72f));
        RectTransform fill = CreateRect("ButtonFill", root.transform);
        SetStretch(fill);
        AddSlicedImage(fill.gameObject, null, disabled ? new Color(0.020f, 0.025f, 0.037f, 0.68f) : new Color(0.008f, 0.020f, 0.034f, 0.86f), Image.Type.Simple, false);

        RectTransform frameRect = CreateRect("ButtonFrame", root.transform);
        SetStretch(frameRect);
        Image frame = AddSlicedImage(frameRect.gameObject, CommandButtonSprite(), disabled ? CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.54f) : CyberUiTheme.WithAlpha(accent, 0.64f), Image.Type.Sliced, true);

        Button button = root.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = ButtonColors(accent);
        button.interactable = !disabled;

        RectTransform iconFrame = CreateRect("IconFrame", root.transform);
        SetRect(iconFrame, new Vector2(-112f, 0f), new Vector2(54f, 46f));
        AddSlicedImage(iconFrame.gameObject, HudTintSprite("frame_square_04.png"), disabled ? CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.44f) : CyberUiTheme.WithAlpha(accent, 0.68f), Image.Type.Sliced, false);
        AddHudIcon(iconFrame, "Icon", iconFileName, disabled ? CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.42f) : CyberUiTheme.WithAlpha(accent, 0.96f), Vector2.zero, 28f);

        AddText(root.transform, "Label", label, 19, FontStyle.Bold, TextAnchor.MiddleLeft, disabled ? CyberUiTheme.Disabled : TextPrimary, new Vector2(44f, 2f), new Vector2(190f, 38f));
        AddAccentRail(root.transform, "AccentRail", new Vector2(58f, -26f), new Vector2(180f, 4f), disabled ? CyberUiTheme.Disabled : accent);
        AddMicroBars(root.transform, "SignalBars", new Vector2(118f, 22f), disabled ? CyberUiTheme.Disabled : accent);
        AddHudSprite(root.transform, "ButtonNotch", "frame_square_05.1.png", disabled ? CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.34f) : CyberUiTheme.WithAlpha(accent, 0.62f), new Vector2(134f, 0f), new Vector2(30f, 46f), Image.Type.Sliced);
        return root;
    }

    private static GameObject CreateTierCardPrefab(string objectName, int tier, Color accent, bool selected)
    {
        GameObject root = CreateUiRoot(objectName, new Vector2(168f, 104f));
        RectTransform fill = CreateRect("NodeFill", root.transform);
        SetRect(fill, Vector2.zero, new Vector2(130f, 66f));
        AddSlicedImage(fill.gameObject, null, selected ? CyberUiTheme.WithAlpha(accent, 0.13f) : new Color(0.005f, 0.014f, 0.025f, 0.74f), Image.Type.Simple, false);

        RectTransform frameRect = CreateRect("NodeFrame", root.transform);
        SetStretch(frameRect);
        Image frame = AddSlicedImage(frameRect.gameObject, TierCardSprite(), selected ? CyberUiTheme.WithAlpha(accent, 1f) : CyberUiTheme.WithAlpha(accent, 0.64f), Image.Type.Sliced, true);

        Button button = root.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = ButtonColors(accent);

        AddAccentRail(root.transform, "TopAccent", new Vector2(-20f, 43f), new Vector2(84f, 4f), accent);
        AddHudIcon(root.transform, "NodeIcon", tier == 5 ? "icon_lock.png" : "icon_tech_02.png", CyberUiTheme.WithAlpha(accent, selected ? 0.96f : 0.68f), new Vector2(-38f, -4f), 54f);
        if (selected)
            AddHudSprite(root.transform, "SelectedPulse", "deco_misc_01.png", CyberUiTheme.WithAlpha(accent, 0.28f), new Vector2(-38f, -4f), new Vector2(72f, 72f), Image.Type.Simple, true);
        AddText(root.transform, "TierLabel", $"{tier}F", 17, FontStyle.Bold, TextAnchor.MiddleRight, TextPrimary, new Vector2(48f, 18f), new Vector2(58f, 28f));
        AddText(root.transform, "TierCode", $"T0{tier}", 9, FontStyle.Bold, TextAnchor.MiddleRight, accent, new Vector2(48f, -22f), new Vector2(58f, 22f));
        AddMicroBars(root.transform, "CardBits", new Vector2(50f, -39f), accent);
        return root;
    }

    private static GameObject CreatePseudoSpriteWindowPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_PseudoSpriteWindow", new Vector2(300f, 220f));
        AddSlicedImage(root, TerminalPanelSprite(), Color.white, Image.Type.Sliced, false);
        AddScanlineLayer(root.transform, 0.034f);
        AddText(root.transform, "WindowTitle", "THREAT AVATAR", 12, FontStyle.Bold, TextAnchor.MiddleLeft, Selected, new Vector2(-70f, 82f), new Vector2(160f, 26f));

        RectTransform grid = CreateRect("SpriteGrid", root.transform);
        SetRect(grid, new Vector2(0f, -6f), new Vector2(214f, 148f));
        AddSlicedImage(grid.gameObject, ThinFrameSprite(), Color.white, Image.Type.Sliced, false);
        AddCornerTicks(grid, Selected);
        AddPseudoSprite(grid, 3, new Vector2(0f, -4f), new Vector2(150f, 112f), Color.white);

        AddMicroBars(root.transform, "PaletteBits", new Vector2(-66f, -91f), Success);
        AddMicroBars(root.transform, "ThreatBits", new Vector2(62f, -91f), Danger);
        return root;
    }

    private static GameObject CreateScanlineStripPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_ScanlineStrip", new Vector2(520f, 72f));
        AddSlicedImage(root, ThinFrameSprite(), Color.white, Image.Type.Sliced, false);
        AddScanlineLayer(root.transform, 0.060f);
        AddAccentRail(root.transform, "CyanRail", new Vector2(-126f, 23f), new Vector2(210f, 3f), Primary);
        AddAccentRail(root.transform, "MagentaRail", new Vector2(136f, -24f), new Vector2(170f, 3f), Danger);
        AddText(root.transform, "ConsoleLine", "> ROUTE_KERNEL READY // DEPTH 3F // AVATAR BLUE_THREAT", 11, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary, new Vector2(6f, 0f), new Vector2(446f, 26f));
        return root;
    }

    private static GameObject CreateAccentRailPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_AccentRail", new Vector2(380f, 34f));
        AddSlicedImage(root, null, new Color(0.004f, 0.006f, 0.020f, 0.72f), Image.Type.Simple, false);
        AddAccentRail(root.transform, "BaseCyan", new Vector2(-36f, 3f), new Vector2(260f, 3f), CyberUiTheme.WithAlpha(Primary, 0.86f));
        AddAccentRail(root.transform, "BasePurple", new Vector2(38f, -7f), new Vector2(200f, 2f), new Color(0.50f, 0.18f, 1f, 0.40f));
        AddAccentRail(root.transform, "HotMagenta", new Vector2(-128f, 11f), new Vector2(74f, 4f), Danger);
        AddAccentRail(root.transform, "ColdEndCap", new Vector2(148f, 11f), new Vector2(44f, 4f), Selected);
        AddMicroBars(root.transform, "RailBitsLeft", new Vector2(-170f, -10f), Primary);
        AddMicroBars(root.transform, "RailBitsRight", new Vector2(116f, -10f), Danger);
        return root;
    }

    private static GameObject CreateStatusChipPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_StatusChip", new Vector2(172f, 50f));
        AddSlicedImage(root, ThinFrameSprite(), Color.white, Image.Type.Sliced, false);
        AddText(root.transform, "ChipLabel", "STATUS", 7, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary, new Vector2(-44f, 8f), new Vector2(70f, 14f));
        AddText(root.transform, "ChipValue", "ONLINE", 11, FontStyle.Bold, TextAnchor.MiddleLeft, Success, new Vector2(-32f, -8f), new Vector2(92f, 18f));
        AddAccentRail(root.transform, "StateBar", new Vector2(44f, 12f), new Vector2(58f, 4f), Success);
        AddMicroBars(root.transform, "Signal", new Vector2(44f, -15f), Primary);
        return root;
    }

    private static GameObject CreateDagNodePrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_DagNode", new Vector2(104f, 104f));
        Image hit = AddSlicedImage(root, null, Color.clear, Image.Type.Simple, true);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = ButtonColors(Selected);

        CreateDagNodeVisual(root.transform, "Node_Selected", Vector2.zero, Selected, "icon_tech_02.png", "CORE", true, false);
        AddPixelSprite(root.transform, "HoverReticle", "Selectors/Reticle_Hover.png", CyberUiTheme.WithAlpha(Selected, 0.62f), Vector2.zero, new Vector2(94f, 26f), Image.Type.Simple, true);
        AddText(root.transform, "NodeReadout", "T03", 8, FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary, new Vector2(0f, -43f), new Vector2(58f, 16f));
        return root;
    }

    private static GameObject CreateValueBarPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_ValueBar", new Vector2(252f, 52f));
        AddSlicedImage(root, HudTintSprite("bar_frame_long.png"), CyberUiTheme.WithAlpha(Primary, 0.54f), Image.Type.Sliced, false);
        AddText(root.transform, "Label", "BATTERY", 8, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary, new Vector2(-74f, 13f), new Vector2(86f, 16f));
        AddText(root.transform, "Value", "120 / 120", 8, FontStyle.Bold, TextAnchor.MiddleRight, TextPrimary, new Vector2(86f, 13f), new Vector2(88f, 16f));

        AddPixelSprite(root.transform, "BarBackground", "ValueBars/White/RegularBarABackground.png", new Color(0.03f, 0.05f, 0.08f, 0.84f), new Vector2(4f, -8f), new Vector2(172f, 18f), Image.Type.Sliced);
        Image follow = AddPixelSprite(root.transform, "FollowFill", "ValueBars/White/RegularBarAFollowFill.png", CyberUiTheme.WithAlpha(TextSecondary, 0.35f), new Vector2(-15f, -8f), new Vector2(136f, 18f), Image.Type.Filled);
        follow.fillMethod = Image.FillMethod.Horizontal;
        follow.fillAmount = 0.82f;
        Image fill = AddPixelSprite(root.transform, "Fill", "ValueBars/White/RegularBarAFill.png", CyberUiTheme.WithAlpha(Success, 0.94f), new Vector2(-25f, -8f), new Vector2(116f, 18f), Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0.72f;
        AddPixelSprite(root.transform, "BarForeground", "ValueBars/White/RegularBarAForeground.png", CyberUiTheme.WithAlpha(TextPrimary, 0.72f), new Vector2(4f, -8f), new Vector2(172f, 18f), Image.Type.Sliced);
        return root;
    }

    private static GameObject CreateModuleSlotPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_ModuleSlot", new Vector2(82f, 82f));
        Image frame = AddSlicedImage(root, HudTintSprite("slot_item_bg.png"), CyberUiTheme.WithAlpha(Reward, 0.78f), Image.Type.Sliced, true);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = ButtonColors(Reward);

        AddPixelSprite(root.transform, "PixelSelector", "Grid/White/SelectorEdge_Focus.png", CyberUiTheme.WithAlpha(Reward, 0.68f), Vector2.zero, new Vector2(72f, 28f), Image.Type.Simple, true);
        AddHudIcon(root.transform, "ModuleIcon", "icon_datapack.png", CyberUiTheme.WithAlpha(Reward, 0.94f), new Vector2(0f, 4f), 42f);
        AddMicroBars(root.transform, "SlotBits", new Vector2(-20f, -30f), Reward);
        return root;
    }

    private static GameObject CreateDagPreviewPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_DagPreview", new Vector2(560f, 260f));
        AddSlicedImage(root, HudTintSprite("panel_base_02.png"), CyberUiTheme.WithAlpha(Primary, 0.68f), Image.Type.Sliced, false);
        RectTransform fill = CreateRect("PreviewFill", root.transform);
        SetRect(fill, new Vector2(0f, -4f), new Vector2(502f, 196f));
        AddSlicedImage(fill.gameObject, null, new Color(0.004f, 0.014f, 0.026f, 0.82f), Image.Type.Simple, false);
        AddScanlineLayer(root.transform, 0.030f);
        AddText(root.transform, "PreviewTitle", "RUN_DAG_ROUTE", 13, FontStyle.Bold, TextAnchor.MiddleLeft, Primary, new Vector2(-174f, 96f), new Vector2(190f, 24f));
        AddText(root.transform, "PreviewMeta", "DRAG / SELECT / INSPECT", 8, FontStyle.Bold, TextAnchor.MiddleRight, TextSecondary, new Vector2(158f, 96f), new Vector2(160f, 18f));

        AddDagConnector(root.transform, "RouteA", "SkillTree/White/ConnectorHorizontal.png", new Vector2(-126f, 20f), new Vector2(128f, 16f), 0f, Primary, 0.58f);
        AddDagConnector(root.transform, "RouteB", "SkillTree/White/ConnectorDiagonalRight.png", new Vector2(-18f, 44f), new Vector2(90f, 90f), 0f, Primary, 0.62f);
        AddDagConnector(root.transform, "RouteC", "SkillTree/White/ConnectorHorizontal.png", new Vector2(116f, 54f), new Vector2(126f, 16f), 0f, Primary, 0.52f);
        AddDagConnector(root.transform, "RouteDanger", "SkillTree/White/ConnectorDiagonalLeft.png", new Vector2(150f, -24f), new Vector2(92f, 92f), 0f, Danger, 0.50f);
        AddDagConnector(root.transform, "RouteInactive", "SkillTree/White/ConnectorThinHorizontal.png", new Vector2(-22f, -50f), new Vector2(180f, 10f), 0f, TextSecondary, 0.22f);

        CreateDagNodeVisual(root.transform, "StartNode", new Vector2(-214f, 18f), Selected, "icon_nav_06.png", "START", true, false);
        CreateDagNodeVisual(root.transform, "CoreNode", new Vector2(-86f, 20f), Primary, "icon_tech_02.png", "CORE", true, true);
        CreateDagNodeVisual(root.transform, "RewardNode", new Vector2(38f, 58f), Reward, "icon_datapack.png", "PAY", true, false);
        CreateDagNodeVisual(root.transform, "RiskNode", new Vector2(166f, 52f), Danger, "icon_lock.png", "RISK", true, false);
        CreateDagNodeVisual(root.transform, "UnknownNode", new Vector2(84f, -52f), TextSecondary, "icon_tech_01.png", "???", false, false);
        CreateDagNodeVisual(root.transform, "ExitNode", new Vector2(232f, -32f), Success, "icon_gameplay_02.png", "EXIT", false, false);

        AddPixelSprite(root.transform, "DragReticle", "Selectors/Dashed_Hover.png", CyberUiTheme.WithAlpha(Selected, 0.42f), new Vector2(0f, -100f), new Vector2(240f, 60f), Image.Type.Simple, true);
        return root;
    }

    private static void AddDagConnector(Transform parent, string name, string pixelPath, Vector2 anchoredPosition, Vector2 size, float rotation, Color color, float alpha)
    {
        Image connector = AddPixelSprite(parent, name, pixelPath, CyberUiTheme.WithAlpha(color, alpha), anchoredPosition, size, Image.Type.Simple, true);
        connector.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
    }

    private static RectTransform CreateDagNodeVisual(Transform parent, string name, Vector2 anchoredPosition, Color accent, string hudIcon, string label, bool active, bool selected)
    {
        RectTransform node = CreateRect(name, parent);
        SetRect(node, anchoredPosition, new Vector2(70f, 70f));
        AddPixelSprite(node, "Slot", active ? "SkillTree/White/SkillSlotLarge.png" : "SkillTree/White/SkillSlotLargePlaceholder.png", CyberUiTheme.WithAlpha(accent, active ? 0.88f : 0.34f), Vector2.zero, new Vector2(58f, 58f), Image.Type.Simple, true);
        AddPixelSprite(node, "Focus", "SkillTree/White/SkillSlot_FocusLarge.png", CyberUiTheme.WithAlpha(accent, selected ? 0.72f : 0.24f), Vector2.zero, new Vector2(82f, 28f), Image.Type.Simple, true);
        AddHudIcon(node, "Icon", hudIcon, CyberUiTheme.WithAlpha(accent, active ? 0.94f : 0.42f), new Vector2(0f, 4f), 30f);
        AddText(node, "Label", label, 6, FontStyle.Bold, TextAnchor.MiddleCenter, active ? TextPrimary : TextSecondary, new Vector2(0f, -24f), new Vector2(52f, 12f));
        return node;
    }

    private static GameObject CreateComponentSheetPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_ComponentSheet", new Vector2(1600f, 900f));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        Image background = AddSlicedImage(root, null, Background, Image.Type.Simple, false);
        SetStretch(background.rectTransform);
        AddScanlineLayer(root.transform, 0.010f);

        AddText(root.transform, "SheetTitle", "ALGOMON // MAINTERMINAL COMPONENT SHEET", 31, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-390f, 392f), new Vector2(760f, 50f));
        AddText(root.transform, "SheetMeta", "SPRITE / 9-SLICE / PREFAB LANGUAGE", 12, FontStyle.Bold, TextAnchor.MiddleRight, TextSecondary, new Vector2(484f, 394f), new Vector2(360f, 30f));

        GameObject terminal = CreateTerminalPanelPrefab();
        terminal.name = "Component_TerminalPanel";
        terminal.transform.SetParent(root.transform, false);
        SetRect(terminal.GetComponent<RectTransform>(), new Vector2(290f, 142f), new Vector2(860f, 402f));

        GameObject pseudo = CreatePseudoSpriteWindowPrefab();
        pseudo.name = "Component_PseudoSpriteWindow";
        pseudo.transform.SetParent(root.transform, false);
        SetRect(pseudo.GetComponent<RectTransform>(), new Vector2(-550f, 154f), new Vector2(300f, 220f));

        CreateButtonStack(root.transform);
        CreateUtilityStack(root.transform);

        GameObject dagPreview = CreateDagPreviewPrefab();
        dagPreview.name = "Component_DagPreview";
        dagPreview.transform.SetParent(root.transform, false);
        SetRect(dagPreview.GetComponent<RectTransform>(), new Vector2(370f, -220f), new Vector2(560f, 260f));

        GameObject scanline = CreateScanlineStripPrefab();
        scanline.name = "Component_ScanlineStrip";
        scanline.transform.SetParent(root.transform, false);
        SetRect(scanline.GetComponent<RectTransform>(), new Vector2(-58f, -358f), new Vector2(520f, 68f));

        GameObject rail = CreateAccentRailPrefab();
        rail.name = "Component_AccentRail";
        rail.transform.SetParent(root.transform, false);
        SetRect(rail.GetComponent<RectTransform>(), new Vector2(-518f, -358f), new Vector2(310f, 34f));

        GameObject chip = CreateStatusChipPrefab();
        chip.name = "Component_StatusChip";
        chip.transform.SetParent(root.transform, false);
        SetRect(chip.GetComponent<RectTransform>(), new Vector2(-690f, -358f), new Vector2(172f, 50f));

        AddText(root.transform, "SheetFooter", "MAIN MENU COMPONENTS STAY SEPARATE FROM RUN DAG COMPONENTS. THIRD-PARTY HUD SPRITES ARE TINTED AND COMPOSED IN UNITY.", 10, FontStyle.Bold, TextAnchor.MiddleCenter, TextSecondary, new Vector2(0f, -414f), new Vector2(1120f, 24f));
        return root;
    }

    private static GameObject CreateSourceLayoutPrefab()
    {
        GameObject root = CreateUiRoot("MainTerminal_SourceLayout", new Vector2(1600f, 900f));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        Image background = AddSlicedImage(root, HudRawSprite("bg_cyberpunk_city.png.png"), new Color(0.32f, 0.62f, 0.74f, 0.52f), Image.Type.Simple, false);
        SetStretch(background.rectTransform);
        AddSlicedImage(CreateStretchChild("DeepTerminalWash", root.transform).gameObject, null, new Color(0.002f, 0.006f, 0.014f, 0.76f), Image.Type.Simple, false);
        AddScanlineLayer(root.transform, 0.020f);

        AddText(root.transform, "SourceTitle", "ALGOMON // SOURCE_LAYOUT_PASS", 28, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-445f, 382f), new Vector2(640f, 44f));
        AddText(root.transform, "SourceMeta", "CYBERPUNK HUD DEMO RHYTHM / LIGHT ALGOMON LABELS", 10, FontStyle.Bold, TextAnchor.MiddleRight, TextSecondary, new Vector2(445f, 382f), new Vector2(520f, 22f));

        RectTransform menuPanel = CreateRect("Source_MenuPanel", root.transform);
        SetRect(menuPanel, new Vector2(-455f, 8f), new Vector2(420f, 560f));
        AddSlicedImage(menuPanel.gameObject, HudRawSprite("panel_base_02.png"), CyberUiTheme.WithAlpha(Primary, 0.82f), Image.Type.Simple, false);
        AddScanlineLayer(menuPanel, 0.020f);
        AddText(menuPanel, "MenuHeader", "SYSTEM MENU", 18, FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary, new Vector2(0f, 212f), new Vector2(240f, 32f));
        AddSourceButton(menuPanel, "ENTER GRID", "icon_skill_06.png", Primary, new Vector2(0f, 112f), true);
        AddSourceButton(menuPanel, "PAYLOAD BOX", "icon_datapack.png", Reward, new Vector2(0f, -14f), true);
        AddSourceButton(menuPanel, "GENE LAB", "icon_skill_example.png", Danger, new Vector2(0f, -140f), false);
        AddHudSprite(menuPanel, "MenuFooterBits", "bar_frame_long.png", CyberUiTheme.WithAlpha(Primary, 0.58f), new Vector2(0f, -236f), new Vector2(310f, 38f), Image.Type.Sliced);

        RectTransform rightPanel = CreateRect("Source_RightHudGroup", root.transform);
        SetRect(rightPanel, new Vector2(345f, 20f), new Vector2(760f, 586f));
        AddSlicedImage(rightPanel.gameObject, HudRawSprite("panel_base_03.png"), CyberUiTheme.WithAlpha(Primary, 0.62f), Image.Type.Simple, false);
        AddScanlineLayer(rightPanel, 0.016f);
        AddText(rightPanel, "PanelTitle", "TACTICAL HUD", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Primary, new Vector2(-230f, 230f), new Vector2(280f, 34f));

        AddRawHudSprite(rightPanel, "PrimaryHealthUnder", "health_bar_under.png", CyberUiTheme.WithAlpha(TextSecondary, 0.70f), new Vector2(-212f, 168f), new Vector2(340f, 54f), true);
        AddRawHudSprite(rightPanel, "PrimaryHealthOver", "health_bar_over.png", CyberUiTheme.WithAlpha(Success, 0.92f), new Vector2(-222f, 168f), new Vector2(260f, 40f), true);
        AddText(rightPanel, "HealthLabel", "BATTERY 120/120", 10, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-210f, 200f), new Vector2(190f, 18f));

        AddRawHudSprite(rightPanel, "SecondaryHealthUnder", "health_bar_under.png", CyberUiTheme.WithAlpha(TextSecondary, 0.54f), new Vector2(-212f, 112f), new Vector2(340f, 54f), true);
        AddRawHudSprite(rightPanel, "SecondaryHealthOver", "health_bar_over.png", CyberUiTheme.WithAlpha(Reward, 0.92f), new Vector2(-242f, 112f), new Vector2(210f, 40f), true);
        AddText(rightPanel, "CpLabel", "CP 060/100", 10, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-210f, 144f), new Vector2(190f, 18f));

        AddSourceSlotRow(rightPanel, new Vector2(-90f, 12f));

        RectTransform scanner = CreateRect("ScannerFrame", rightPanel);
        SetRect(scanner, new Vector2(-156f, -154f), new Vector2(330f, 216f));
        AddSlicedImage(scanner.gameObject, HudRawSprite("panel_base_03.png"), CyberUiTheme.WithAlpha(Primary, 0.56f), Image.Type.Simple, false);
        AddGridLines(scanner, new Color(0.10f, 0.86f, 1f, 0.18f), 6, 5);
        AddHudSprite(scanner, "ScannerCore", "deco_misc_03.png", CyberUiTheme.WithAlpha(Primary, 0.34f), new Vector2(8f, -6f), new Vector2(118f, 118f), Image.Type.Simple, true);
        AddHudSprite(scanner, "ScannerGlyph", "icon_skill_example.png", CyberUiTheme.WithAlpha(Selected, 0.44f), new Vector2(8f, -6f), new Vector2(76f, 76f), Image.Type.Simple, true);
        AddText(scanner, "ScannerTitle", "SCAN / INSPECT", 10, FontStyle.Bold, TextAnchor.MiddleLeft, Primary, new Vector2(-88f, 83f), new Vector2(160f, 18f));

        RectTransform radar = CreateRect("RadarPanel", rightPanel);
        SetRect(radar, new Vector2(222f, -126f), new Vector2(270f, 196f));
        AddSlicedImage(radar.gameObject, HudRawSprite("hud_radar_frame.png"), CyberUiTheme.WithAlpha(Primary, 0.74f), Image.Type.Simple, false);
        AddHudSprite(radar, "RadarSweep", "deco_misc_02.png", CyberUiTheme.WithAlpha(Selected, 0.60f), Vector2.zero, new Vector2(138f, 138f), Image.Type.Simple, true);
        AddText(radar, "RadarTitle", "PAYLOAD TRACE", 9, FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary, new Vector2(0f, -80f), new Vector2(160f, 18f));

        AddSourceBottomBar(root.transform);
        return root;
    }

    private static GameObject CreateMainMenuSourceLayoutTrialPrefab()
    {
        GameObject root = CreateUiRoot(SourceLayoutTrialName, new Vector2(1600f, 900f));
        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.96f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        AddTrialSystemTitle(root.transform);
        AddTrialMainMenuOuterShell(root.transform);

        RectTransform menuPanel = CreateRect("Trial_MenuPanel", root.transform);
        SetRect(menuPanel, new Vector2(-455f, -54f), new Vector2(410f, 652f));
        AddSlicedImage(menuPanel.gameObject, HudRawSprite("panel_base_02.png"), CyberUiTheme.WithAlpha(Primary, 0.84f), Image.Type.Simple, false);
        AddScanlineLayer(menuPanel, 0.018f);
        AddText(menuPanel, "MenuHeader", "SYSTEM MENU", 24, FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary, new Vector2(0f, 232f), new Vector2(300f, 42f));

        string[] labels =
        {
            "ENTER GRID",
            "GENE LAB",
            "PAYLOAD",
            "SETTINGS",
            "EXIT"
        };
        string[] icons =
        {
            "icon_skill_06.png",
            "icon_skill_example.png",
            "icon_nav_01.png",
            "icon_gameplay_05.png",
            "icon_nav_09.png"
        };
        Color[] accents =
        {
            Primary,
            Danger,
            Reward,
            Primary,
            Danger
        };

        for (int i = 0; i < labels.Length; i++)
            AddSourceButton(menuPanel, labels[i], icons[i], accents[i], new Vector2(0f, TrialButtonTopY - i * TrialButtonSpacing), true);

        AddTrialDepthSelectPanel(root.transform);
        AddTrialBossRouteSelector(root.transform);

        ConfigureSourceLayoutTrialInteraction(root);
        return root;
    }

    private static void AddTrialSystemTitle(Transform parent)
    {
        RectTransform titleBand = CreateRect("Trial_SystemTitle", parent);
        SetRect(titleBand, new Vector2(-58f, 346f), new Vector2(1160f, 72f));
        AddSlicedImage(titleBand.gameObject, null, new Color(0.001f, 0.004f, 0.010f, 0.34f), Image.Type.Simple, false);
        AddText(titleBand, "Title", "ALGOMON // SYSTEM_TERMINAL", 32, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-262f, 8f), new Vector2(660f, 48f));
        AddText(titleBand, "Status", "STATUS ONLINE", 12, FontStyle.Bold, TextAnchor.MiddleRight, Success, new Vector2(424f, 10f), new Vector2(210f, 22f));
        AddAccentRail(titleBand, "TitleRail", new Vector2(-56f, -25f), new Vector2(960f, 3f), CyberUiTheme.WithAlpha(Primary, 0.72f));
    }

    private static void AddTrialMainMenuOuterShell(Transform parent)
    {
        RectTransform shell = CreateRect("Trial_MainMenuOuterShell", parent);
        SetRect(shell, new Vector2(-20f, -18f), new Vector2(1516f, 846f));
        Image image = AddSlicedImage(shell.gameObject, MainMenuOuterShellSprite(), CyberUiTheme.WithAlpha(Color.white, 0.66f), Image.Type.Simple, false);
        image.preserveAspect = false;
        shell.SetAsFirstSibling();
    }

    private static void AddTrialDepthSelectPanel(Transform parent)
    {
        RectTransform panel = CreateRect("Trial_DepthSelect", parent);
        SetRect(panel, new Vector2(190f, 92f), new Vector2(846f, 254f));
        AddSlicedImage(panel.gameObject, null, new Color(0.001f, 0.004f, 0.010f, 0.56f), Image.Type.Simple, false);
        AddDepthPanelShell(panel);
        AddCornerTicks(panel, Primary);
        AddText(panel, "DepthTitle", "DEPTH_SELECT.EXE", 20, FontStyle.Bold, TextAnchor.MiddleLeft, Primary, new Vector2(-232f, 82f), new Vector2(300f, 32f));
        AddTrialDepthPreview(panel);
        AddTrialDepthPalette(panel, new Vector2(124f, 48f));
    }

    private static void AddDepthPanelShell(RectTransform panel)
    {
        Color line = CyberUiTheme.WithAlpha(Primary, 0.74f);
        AddAccentRail(panel, "ShellOuterTop", new Vector2(0f, 112f), new Vector2(828f, 4f), line);
        AddAccentRail(panel, "ShellOuterBottom", new Vector2(0f, -112f), new Vector2(828f, 4f), line);
        AddAccentRail(panel, "ShellOuterLeft", new Vector2(-414f, 0f), new Vector2(4f, 224f), line);
        AddAccentRail(panel, "ShellOuterRight", new Vector2(414f, 0f), new Vector2(4f, 224f), line);
    }

    private static void AddTrialDepthPreview(Transform parent)
    {
        RectTransform preview = CreateRect("DepthPreviewWindow", parent);
        SetRect(preview, new Vector2(-247.31f, -9.04f), new Vector2(292.62f, 209.93f));
        AddSlicedImage(preview.gameObject, null, new Color(0.001f, 0.004f, 0.010f, 0.82f), Image.Type.Simple, false);
        Image previewSprite = AddCroppedPseudoSprite(preview, 3, new Vector2(0f, -1.5f), new Vector2(146f, 129f), Color.white);
        previewSprite.gameObject.name = "DepthPreviewSprite";
        AddCornerTicks(preview, Selected);
    }

    private static void AddTrialDepthPalette(Transform parent, Vector2 origin)
    {
        const int selectedTier = 3;
        for (int i = 0; i < 5; i++)
        {
            int tier = i + 1;
            bool selected = tier == selectedTier;
            Color accent = TrialDepthAccent(tier);
            Color frameColor = selected
                ? CyberUiTheme.WithAlpha(Color.Lerp(CyberUiTheme.RoomPurple, Primary, 0.68f), 0.95f)
                : CyberUiTheme.WithAlpha(Color.Lerp(CyberUiTheme.RoomPurple, Primary, 0.36f), 0.54f);
            RectTransform button = CreateRect("DepthButton_" + tier + "F", parent);
            SetRect(button, origin + new Vector2(-164f + i * 82f, 0f), new Vector2(74f, 42f));
            Image frame = AddSlicedImage(button.gameObject, PixelSprite(DepthPanelFramePath(tier)), frameColor, Image.Type.Sliced, true);
            Button uiButton = button.gameObject.AddComponent<Button>();
            uiButton.targetGraphic = frame;
            uiButton.transition = Selectable.Transition.ColorTint;
            uiButton.colors = ButtonColors(accent);
            RectTransform buttonFill = CreateRect("ButtonFill", button);
            SetRect(buttonFill, new Vector2(0f, -1f), new Vector2(50f, 17f));
            AddSlicedImage(buttonFill.gameObject, null, selected ? new Color(0.010f, 0.045f, 0.060f, 0.62f) : new Color(0.001f, 0.004f, 0.010f, 0.88f), Image.Type.Simple, false);
            AddAccentRail(button, "TierNotch", new Vector2(-29f, 0f), new Vector2(3f, 20f), CyberUiTheme.WithAlpha(accent, selected ? 0.88f : 0.46f));
            AddText(button, "Text", tier + "F", 15, FontStyle.Bold, TextAnchor.MiddleCenter, selected ? TextPrimary : TextSecondary, new Vector2(-5f, 4f), new Vector2(42f, 20f));
            AddText(button, "TierCode", "T0" + tier, 7, FontStyle.Bold, TextAnchor.MiddleCenter, selected ? Primary : CyberUiTheme.WithAlpha(accent, 0.70f), new Vector2(12f, -11f), new Vector2(42f, 12f));
            RectTransform selectedRail = CreateRect("SelectedRail", button);
            SetRect(selectedRail, new Vector2(0f, -18f), new Vector2(46f, 3f));
            AddSlicedImage(selectedRail.gameObject, null, Primary, Image.Type.Simple, false);
            selectedRail.gameObject.SetActive(selected);
        }
    }

    private static void AddTrialBossRouteSelector(Transform parent)
    {
        RectTransform group = CreateRect("Trial_BossRouteSelector", parent);
        SetRect(group, new Vector2(190f, -230f), new Vector2(846f, 306f));

        AddText(group, "BossRouteTitle", "BOSS_TARGET.EXE", 16, FontStyle.Bold, TextAnchor.MiddleLeft, CyberUiTheme.WithAlpha(Selected, 0.96f), new Vector2(-322f, 128f), new Vector2(230f, 24f));
        AddText(group, "BossRouteMeta", "EVOLVED PRIME ROUTES", 9, FontStyle.Bold, TextAnchor.MiddleRight, CyberUiTheme.WithAlpha(CyberUiTheme.RoomPurple, 0.88f), new Vector2(266f, 128f), new Vector2(220f, 18f));
        AddAccentRail(group, "BossRouteTopRail", new Vector2(0f, 106f), new Vector2(724f, 3f), CyberUiTheme.WithAlpha(Primary, 0.48f));

        string[] names =
        {
            "CACHELON",
            "HEAPION",
            "NULLBYTE",
            "OVERFLUX",
            "RECURSIX",
            "SORTEX"
        };
        string[] elementTags =
        {
            "IC",
            "GR",
            "WA",
            "FI",
            "LE",
            "EL"
        };

        for (int i = 0; i < names.Length; i++)
        {
            bool selected = i == 0;
            Color accent = BossRouteAccent(i);
            Vector2 position = new Vector2(-330f + i * 132f, -14f);
            AddTrialBossRouteStrip(group, i + 1, names[i], elementTags[i], accent, position, selected);
        }
    }

    private static void AddTrialBossRouteStrip(Transform parent, int routeNumber, string bossName, string elementTag, Color accent, Vector2 anchoredPosition, bool selected)
    {
        RectTransform button = CreateRect("BossRoute_" + bossName, parent);
        SetRect(button, anchoredPosition, new Vector2(104f, 214f));

        Color frameColor = CyberUiTheme.WithAlpha(Color.Lerp(CyberUiTheme.RoomPurple, Primary, 0.28f), 0.64f);
        Image frame = AddSlicedImage(button.gameObject, PixelSprite("Panels/Blue/PanelInactive.png"), frameColor, Image.Type.Sliced, true);

        RectTransform glow = CreateRect("HoverGlow", button);
        SetRect(glow, Vector2.zero, new Vector2(126f, 238f));
        AddSlicedImage(glow.gameObject, PixelSprite("Panels/Blue/PanelDigital.png"), CyberUiTheme.WithAlpha(accent, selected ? 0.22f : 0f), Image.Type.Sliced, false);

        RectTransform activeFrame = CreateRect("ActiveDigitalFrame", button);
        SetRect(activeFrame, Vector2.zero, new Vector2(120f, 230f));
        AddSlicedImage(activeFrame.gameObject, PixelSprite("Panels/Blue/PanelDigital.png"), CyberUiTheme.WithAlpha(selected ? Selected : accent, selected ? 0.92f : 0.18f), Image.Type.Sliced, false);
        activeFrame.gameObject.SetActive(selected);

        RectTransform fill = CreateRect("ButtonFill", button);
        SetRect(fill, new Vector2(0f, -2f), new Vector2(80f, 166f));
        AddSlicedImage(fill.gameObject, null, selected ? new Color(0.010f, 0.045f, 0.060f, 0.42f) : new Color(0.006f, 0.010f, 0.022f, 0.62f), Image.Type.Simple, false);

        AddBossRoutePortrait(button, bossName, selected, accent);
        RectTransform shadow = CreateRect("InactiveShadowTone", button);
        SetRect(shadow, new Vector2(0f, 4f), new Vector2(84f, 166f));
        AddSlicedImage(shadow.gameObject, null, new Color(0.006f, 0.004f, 0.016f, 0.58f), Image.Type.Simple, false);
        shadow.gameObject.SetActive(!selected);

        AddAccentRail(button, "SignalNotch", new Vector2(-42f, 22f), new Vector2(4f, 122f), CyberUiTheme.WithAlpha(accent, selected ? 0.95f : 0.54f));
        AddText(button, "RouteCode", "R" + routeNumber.ToString("00"), 8, FontStyle.Bold, TextAnchor.MiddleCenter, CyberUiTheme.WithAlpha(TextSecondary, 0.86f), new Vector2(0f, 86f), new Vector2(44f, 14f));
        AddText(button, "Label", bossName, 8, FontStyle.Bold, TextAnchor.MiddleCenter, selected ? TextPrimary : CyberUiTheme.WithAlpha(TextPrimary, 0.76f), new Vector2(0f, -78f), new Vector2(86f, 14f));
        AddText(button, "ElementTag", elementTag, 8, FontStyle.Bold, TextAnchor.MiddleCenter, CyberUiTheme.WithAlpha(accent, selected ? 0.98f : 0.74f), new Vector2(0f, -94f), new Vector2(38f, 14f));
        AddText(button, "RouteStatus", selected ? "TARGET" : "READY", 7, FontStyle.Bold, TextAnchor.MiddleCenter, selected ? Selected : CyberUiTheme.WithAlpha(TextSecondary, 0.58f), new Vector2(0f, -105f), new Vector2(70f, 12f));

        RectTransform selectedRail = CreateRect("SelectedRail", button);
        SetRect(selectedRail, new Vector2(0f, -109f), new Vector2(68f, 3f));
        AddSlicedImage(selectedRail.gameObject, null, CyberUiTheme.WithAlpha(Primary, 0.92f), Image.Type.Simple, false);
        selectedRail.gameObject.SetActive(selected);

        Button uiButton = button.gameObject.AddComponent<Button>();
        uiButton.targetGraphic = frame;
        uiButton.transition = Selectable.Transition.None;

        CyberImageButtonFeedback feedback = button.gameObject.AddComponent<CyberImageButtonFeedback>();
        feedback.CustomAccentColor = accent;
        feedback.Selected = selected;
    }

    private static void AddBossRoutePortrait(Transform parent, string bossName, bool selected, Color accent)
    {
        RectTransform viewport = CreateRect("BossPortraitMask", parent);
        SetRect(viewport, new Vector2(0f, 7f), new Vector2(82f, 152f));
        viewport.gameObject.AddComponent<RectMask2D>();
        AddSlicedImage(viewport.gameObject, null, selected ? new Color(0.004f, 0.024f, 0.034f, 0.76f) : new Color(0.004f, 0.004f, 0.012f, 0.78f), Image.Type.Simple, false);

        RectTransform spriteRect = CreateRect("BossSprite", viewport);
        SetRect(spriteRect, BossPortraitOffset(bossName), BossPortraitSize(bossName));
        Color spriteColor = selected
            ? Color.white
            : CyberUiTheme.WithAlpha(Color.Lerp(new Color(0.34f, 0.38f, 0.48f, 1f), accent, 0.16f), 0.62f);
        Image image = AddSlicedImage(spriteRect.gameObject, BossRouteSprite(bossName), spriteColor, Image.Type.Simple, false);
        image.preserveAspect = true;

        AddAccentRail(viewport, "PortraitScan", new Vector2(0f, -70f), new Vector2(72f, 2f), CyberUiTheme.WithAlpha(selected ? Primary : CyberUiTheme.RoomPurple, selected ? 0.86f : 0.36f));
    }

    private static Vector2 BossPortraitOffset(string bossName)
    {
        switch (bossName)
        {
            case "OVERFLUX":
                return new Vector2(26f, -8f);
            case "NULLBYTE":
                return new Vector2(28f, -10f);
            case "SORTEX":
                return new Vector2(26f, -4f);
            case "HEAPION":
            case "RECURSIX":
            case "CACHELON":
            default:
                return new Vector2(22f, -8f);
        }
    }

    private static Vector2 BossPortraitSize(string bossName)
    {
        switch (bossName)
        {
            case "OVERFLUX":
                return new Vector2(174f, 140f);
            case "NULLBYTE":
                return new Vector2(170f, 136f);
            case "SORTEX":
                return new Vector2(170f, 138f);
            default:
                return new Vector2(162f, 134f);
        }
    }

    private static Sprite BossRouteSprite(string bossName)
    {
        switch (bossName)
        {
            case "CACHELON":
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/CACHELON/Cachelon_Evolved.png");
            case "HEAPION":
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/HEAPION/Heapion_Evolved.png");
            case "NULLBYTE":
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/NULLBYTE/Nullbyte_Evolved.png");
            case "OVERFLUX":
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/OVERFLUX/Overflux_Evolved.png");
            case "RECURSIX":
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/RECURSIX/Recursix_Evolved.png");
            case "SORTEX":
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_AlgoMon/Sprites/SORTEX/Sortex_Evolved.png");
            default:
                return null;
        }
    }

    private static Color BossRouteAccent(int index)
    {
        Color purple = CyberUiTheme.RoomPurple;
        switch (index)
        {
            case 0:
                return Selected;
            case 1:
                return Color.Lerp(purple, Reward, 0.42f);
            case 2:
                return Color.Lerp(purple, Primary, 0.58f);
            case 3:
                return Color.Lerp(purple, Danger, 0.50f);
            case 4:
                return Color.Lerp(purple, Success, 0.36f);
            case 5:
            default:
                return Color.Lerp(purple, Primary, 0.34f);
        }
    }

    private static string DepthPanelFramePath(int tier)
    {
        switch (tier)
        {
            case 1:
                return "Panels/White/FrameDigitalA.png";
            case 2:
                return "Panels/White/FrameDigitalB.png";
            case 4:
                return "Panels/White/FrameDigitalC.png";
            case 5:
                return "Panels/White/FrameDigitalD.png";
            case 3:
            default:
                return "Panels/White/FrameDigitalLarge.png";
        }
    }

    private static Color TrialDepthAccent(int tier)
    {
        switch (tier)
        {
            case 1:
                return Success;
            case 2:
                return Selected;
            case 4:
                return Reward;
            case 5:
                return Danger;
            case 3:
            default:
                return Primary;
        }
    }

    private static void AddTrialStatusCluster(Transform parent)
    {
        AddRawHudSprite(parent, "BatteryUnder", "health_bar_under.png", CyberUiTheme.WithAlpha(TextSecondary, 0.68f), new Vector2(-174f, -98f), new Vector2(330f, 52f), true);
        AddRawHudSprite(parent, "BatteryOver", "health_bar_over.png", CyberUiTheme.WithAlpha(Success, 0.92f), new Vector2(-198f, -98f), new Vector2(250f, 38f), true);
        AddText(parent, "BatteryLabel", "BATTERY 120/120", 9, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-170f, -69f), new Vector2(190f, 18f));

        AddRawHudSprite(parent, "CpUnder", "health_bar_under.png", CyberUiTheme.WithAlpha(TextSecondary, 0.52f), new Vector2(-174f, -156f), new Vector2(330f, 52f), true);
        AddRawHudSprite(parent, "CpOver", "health_bar_over.png", CyberUiTheme.WithAlpha(Reward, 0.92f), new Vector2(-222f, -156f), new Vector2(194f, 38f), true);
        AddText(parent, "CpLabel", "CP 060/100", 9, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary, new Vector2(-170f, -127f), new Vector2(190f, 18f));

        AddText(parent, "ModuleLabel", "PAYLOAD MODULES", 9, FontStyle.Bold, TextAnchor.MiddleLeft, Primary, new Vector2(-226f, -214f), new Vector2(180f, 18f));
        AddSourceSlotRow(parent, new Vector2(-132f, -260f));
    }

    private static void AddTrialPayloadInspectPanel(Transform parent)
    {
        RectTransform payload = CreateRect("Trial_PayloadInspect", parent);
        SetRect(payload, new Vector2(238f, -152f), new Vector2(238f, 320f));
        AddSlicedImage(payload.gameObject, HudRawSprite("panel_base_03.png"), CyberUiTheme.WithAlpha(Primary, 0.52f), Image.Type.Simple, false);
        AddGridLines(payload, new Color(0.10f, 0.86f, 1f, 0.16f), 4, 5);
        AddText(payload, "PayloadTitle", "PAYLOAD / INSPECT", 9, FontStyle.Bold, TextAnchor.MiddleLeft, Primary, new Vector2(-52f, 130f), new Vector2(150f, 18f));
        AddHudSprite(payload, "PayloadSilhouette", "img_body_silhouette.png", CyberUiTheme.WithAlpha(Selected, 0.28f), new Vector2(0f, 22f), new Vector2(130f, 170f), Image.Type.Simple, true);
        AddHudSprite(payload, "PayloadCore", "deco_misc_03.png", CyberUiTheme.WithAlpha(Primary, 0.34f), new Vector2(0f, 12f), new Vector2(92f, 92f), Image.Type.Simple, true);
        AddCornerTicks(payload, Primary);
        AddText(payload, "PayloadStats", "FRAGMENTS 00\nGENE SLOTS 02\nARCHIVE OK", 8, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary, new Vector2(-32f, -118f), new Vector2(140f, 48f));
    }

    private static void AddTrialBottomConsole(Transform parent)
    {
        RectTransform bottom = CreateRect("Trial_BottomConsole", parent);
        SetRect(bottom, new Vector2(164f, -392f), new Vector2(1230f, 98f));
        AddSlicedImage(bottom.gameObject, HudRawSprite("hud_bottom_bar.png"), CyberUiTheme.WithAlpha(Primary, 0.76f), Image.Type.Simple, false);
        AddText(bottom, "Console", "> MAIN_MENU_VISUAL_TRIAL // SOURCE_ASSET_LAYOUT // DEPTH_SELECT + PSEUDO_COLOR_VARIANTS", 11, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary, new Vector2(-30f, -8f), new Vector2(820f, 24f));
        AddAccentRail(bottom, "CyanRail", new Vector2(-268f, 25f), new Vector2(360f, 4f), Primary);
        AddAccentRail(bottom, "MagentaRail", new Vector2(288f, -26f), new Vector2(250f, 4f), Danger);
    }

    private static RectTransform CreateStretchChild(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        SetStretch(rect);
        return rect;
    }

    private static void AddSourceButton(Transform parent, string label, string iconFileName, Color accent, Vector2 anchoredPosition, bool enabled)
    {
        RectTransform button = CreateRect("Button_" + label.Replace(" ", ""), parent);
        SetRect(button, anchoredPosition, new Vector2(310f, 92f));
        Color baseTint = enabled
            ? CyberUiTheme.WithAlpha(Color.Lerp(new Color(0.72f, 0.92f, 1f, 1f), accent, 0.44f), 0.94f)
            : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.62f);
        Image frame = AddSlicedImage(button.gameObject, HudRawSprite("btn_wide_01.png"), baseTint, Image.Type.Simple, true);

        RectTransform glow = CreateRect("HoverGlow", button);
        SetRect(glow, Vector2.zero, new Vector2(336f, 112f));
        AddSlicedImage(glow.gameObject, HudTintSprite("btn_wide_01.png"), CyberUiTheme.WithAlpha(accent, 0f), Image.Type.Sliced, false);

        AddHudIcon(button, "Icon", iconFileName, enabled ? CyberUiTheme.WithAlpha(accent, 0.96f) : CyberUiTheme.WithAlpha(CyberUiTheme.Disabled, 0.48f), new Vector2(-104f, 0f), 42f);
        AddText(button, "LabelShadow", label, 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0f, 0f, 0f, 0.74f), new Vector2(54f, -2f), new Vector2(190f, 38f));
        AddText(button, "Label", label, 22, FontStyle.Bold, TextAnchor.MiddleLeft, enabled ? TextPrimary : CyberUiTheme.Disabled, new Vector2(52f, 0f), new Vector2(190f, 38f));
        AddMicroBars(button, "Signal", new Vector2(82f, -32f), enabled ? accent : CyberUiTheme.Disabled);

        Button uiButton = button.gameObject.AddComponent<Button>();
        uiButton.targetGraphic = frame;
        uiButton.transition = Selectable.Transition.None;
        uiButton.interactable = enabled;

        CyberImageButtonFeedback feedback = button.gameObject.AddComponent<CyberImageButtonFeedback>();
        feedback.CustomAccentColor = accent;
    }

    private static void AddSourceSlotRow(Transform parent, Vector2 origin)
    {
        string[] icons =
        {
            "icon_datapack.png",
            "icon_tech_02.png",
            "icon_skill_example.png",
            "icon_gameplay_02.png",
            "icon_lock.png"
        };

        for (int i = 0; i < icons.Length; i++)
        {
            Color accent = i == 4 ? Danger : (i == 1 ? Reward : Primary);
            RectTransform slot = CreateRect("SourceSlot_" + i, parent);
            SetRect(slot, origin + new Vector2(i * 94f, 0f), new Vector2(76f, 76f));
            AddSlicedImage(slot.gameObject, HudRawSprite("slot_item_bg.png"), CyberUiTheme.WithAlpha(accent, 0.84f), Image.Type.Simple, false);
            AddHudIcon(slot, "Icon", icons[i], CyberUiTheme.WithAlpha(accent, 0.98f), Vector2.zero, 36f);
        }
    }

    private static void AddSourceBottomBar(Transform parent)
    {
        RectTransform bottom = CreateRect("SourceBottomBar", parent);
        SetRect(bottom, new Vector2(0f, -364f), new Vector2(1100f, 108f));
        AddSlicedImage(bottom.gameObject, HudRawSprite("hud_bottom_bar.png"), CyberUiTheme.WithAlpha(Primary, 0.78f), Image.Type.Simple, false);
        AddText(bottom, "Console", "> SOURCE_LAYOUT_READY // CYBERPUNK_HUD_BASELINE // NEXT_PASS: ALGOMON_MAIN_MENU", 12, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary, new Vector2(-78f, -8f), new Vector2(720f, 26f));
        AddAccentRail(bottom, "CyanRail", new Vector2(-268f, 25f), new Vector2(360f, 4f), Primary);
        AddAccentRail(bottom, "MagentaRail", new Vector2(284f, -26f), new Vector2(260f, 4f), Danger);
    }

    private static void CreateButtonStack(Transform parent)
    {
        Color[] accents = { Primary, Reward, Danger };
        string[] labels = { "ENTER GRID", "PAYLOAD BOX", "GENE LAB" };
        string[] icons = { "icon_skill_06.png", "icon_datapack.png", "icon_skill_example.png" };
        bool[] disabled = { false, false, true };

        for (int i = 0; i < labels.Length; i++)
        {
            GameObject button = CreateCommandButtonPrefab("Component_CommandButton_" + labels[i].Replace(" ", ""), labels[i], accents[i], icons[i], disabled[i]);
            button.transform.SetParent(parent, false);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(-532f, -95f - i * 92f), new Vector2(320f, 72f));
        }
    }

    private static void CreateUtilityStack(Transform parent)
    {
        GameObject barA = CreateValueBarPrefab();
        barA.name = "Component_ValueBar_Battery";
        barA.transform.SetParent(parent, false);
        SetRect(barA.GetComponent<RectTransform>(), new Vector2(-234f, -116f), new Vector2(252f, 52f));

        GameObject barB = CreateValueBarPrefab();
        barB.name = "Component_ValueBar_CP";
        barB.transform.SetParent(parent, false);
        SetRect(barB.GetComponent<RectTransform>(), new Vector2(-234f, -174f), new Vector2(252f, 52f));
        TintFirstNamedGraphic(barB.transform, "Fill", Reward);

        GameObject slotA = CreateModuleSlotPrefab();
        slotA.name = "Component_ModuleSlot_Reward";
        slotA.transform.SetParent(parent, false);
        SetRect(slotA.GetComponent<RectTransform>(), new Vector2(-322f, -260f), new Vector2(82f, 82f));

        GameObject slotB = CreateModuleSlotPrefab();
        slotB.name = "Component_ModuleSlot_Danger";
        slotB.transform.SetParent(parent, false);
        SetRect(slotB.GetComponent<RectTransform>(), new Vector2(-226f, -260f), new Vector2(82f, 82f));
        TintFirstNamedGraphic(slotB.transform, "ModuleIcon", Danger);
        TintFirstNamedGraphic(slotB.transform, "PixelSelector", Danger);

        GameObject node = CreateDagNodePrefab();
        node.name = "Component_DagNode_Selected";
        node.transform.SetParent(parent, false);
        SetRect(node.GetComponent<RectTransform>(), new Vector2(-116f, -260f), new Vector2(104f, 104f));
    }

    private static void TintFirstNamedGraphic(Transform root, string childName, Color color)
    {
        Transform found = root.Find(childName);
        if (found == null)
        {
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].name == childName)
                {
                    graphics[i].color = color;
                    return;
                }
            }

            return;
        }

        Graphic graphic = found.GetComponent<Graphic>();
        if (graphic != null)
            graphic.color = color;
    }

    private static void CreateTierRail(Transform parent)
    {
        Color[] accents = { Success, Selected, Primary, Reward, Danger };
        for (int i = 0; i < accents.Length; i++)
        {
            int tier = i + 1;
            GameObject card = CreateTierCardPrefab("Component_TierCard_" + tier + "F", tier, accents[i], tier == 3);
            card.transform.SetParent(parent, false);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(-184f + i * 178f, -198f), new Vector2(168f, 104f));
        }
    }

    private static void CreateSwatchStrip(Transform parent)
    {
        Color[] colors = { Background, Panel, Primary, Selected, Danger, Reward, Success };
        string[] names = { "BG", "PANEL", "CYAN", "SELECT", "DANGER", "REWARD", "ONLINE" };
        for (int i = 0; i < colors.Length; i++)
        {
            RectTransform swatch = CreateRect("Swatch_" + names[i], parent);
            SetRect(swatch, new Vector2(-555f + i * 94f, -365f), new Vector2(74f, 42f));
            AddSlicedImage(swatch.gameObject, ThinFrameSprite(), Color.white, Image.Type.Sliced, false);
            AddAccentRail(swatch, "Chip", new Vector2(0f, 5f), new Vector2(52f, 8f), colors[i]);
            AddText(swatch, "Label", names[i], 7, FontStyle.Bold, TextAnchor.MiddleCenter, TextSecondary, new Vector2(0f, -10f), new Vector2(60f, 14f));
        }
    }

    private static void AddStatusBars(Transform parent, Vector2 anchoredPosition)
    {
        Color[] colors = { Success, Success, Success, Primary, Primary, Danger, Danger };
        for (int i = 0; i < colors.Length; i++)
        {
            RectTransform bar = CreateRect("StatusBar_" + i, parent);
            SetRect(bar, anchoredPosition + new Vector2(i * 13f, 15f), new Vector2(8f, 8f));
            AddSlicedImage(bar.gameObject, null, colors[i], Image.Type.Simple, false);
        }
    }

    private static void AddMiniInfoBox(Transform parent, string name, string label, Vector2 anchoredPosition, Color accent)
    {
        RectTransform box = CreateRect(name, parent);
        SetRect(box, anchoredPosition, new Vector2(92f, 52f));
        AddSlicedImage(box.gameObject, ThinFrameSprite(), Color.white, Image.Type.Sliced, false);
        AddText(box, "Text", label, 9, FontStyle.Bold, TextAnchor.MiddleLeft, accent, new Vector2(6f, 0f), new Vector2(62f, 34f));
    }

    private static void AddGridLines(RectTransform parent, Color color, int columns, int rows)
    {
        for (int i = 1; i < columns; i++)
        {
            RectTransform line = CreateRect("GridV_" + i, parent);
            float x = Mathf.Lerp(-parent.sizeDelta.x * 0.5f + 16f, parent.sizeDelta.x * 0.5f - 16f, i / (float)columns);
            SetRect(line, new Vector2(x, -4f), new Vector2(1f, parent.sizeDelta.y - 42f));
            AddSlicedImage(line.gameObject, null, color, Image.Type.Simple, false);
        }

        for (int i = 1; i < rows; i++)
        {
            RectTransform line = CreateRect("GridH_" + i, parent);
            float y = Mathf.Lerp(-parent.sizeDelta.y * 0.5f + 20f, parent.sizeDelta.y * 0.5f - 48f, i / (float)rows);
            SetRect(line, new Vector2(0f, y), new Vector2(parent.sizeDelta.x - 40f, 1f));
            AddSlicedImage(line.gameObject, null, color, Image.Type.Simple, false);
        }
    }

    private static void AddCornerTicks(RectTransform parent, Color accent)
    {
        const float length = 24f;
        const float thickness = 3f;
        Vector2 half = parent.sizeDelta * 0.5f;
        AddAccentRail(parent, "CornerTL_H", new Vector2(-half.x + 28f, half.y - 22f), new Vector2(length, thickness), accent);
        AddAccentRail(parent, "CornerTL_V", new Vector2(-half.x + 16f, half.y - 34f), new Vector2(thickness, length), accent);
        AddAccentRail(parent, "CornerTR_H", new Vector2(half.x - 28f, half.y - 22f), new Vector2(length, thickness), accent);
        AddAccentRail(parent, "CornerTR_V", new Vector2(half.x - 16f, half.y - 34f), new Vector2(thickness, length), accent);
        AddAccentRail(parent, "CornerBL_H", new Vector2(-half.x + 28f, -half.y + 22f), new Vector2(length, thickness), accent);
        AddAccentRail(parent, "CornerBL_V", new Vector2(-half.x + 16f, -half.y + 34f), new Vector2(thickness, length), accent);
        AddAccentRail(parent, "CornerBR_H", new Vector2(half.x - 28f, -half.y + 22f), new Vector2(length, thickness), accent);
        AddAccentRail(parent, "CornerBR_V", new Vector2(half.x - 16f, -half.y + 34f), new Vector2(thickness, length), accent);
    }

    private static void AddMicroBars(Transform parent, string prefix, Vector2 anchoredPosition, Color accent)
    {
        for (int i = 0; i < 5; i++)
        {
            RectTransform bar = CreateRect(prefix + "_" + i, parent);
            SetRect(bar, anchoredPosition + new Vector2(i * 10f, 0f), new Vector2(i % 2 == 0 ? 7f : 4f, 3f));
            AddSlicedImage(bar.gameObject, null, Color.Lerp(accent, TextSecondary, 0.35f), Image.Type.Simple, false);
        }
    }

    private static Image AddPseudoSprite(Transform parent, int tier, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform spriteRect = CreateRect("PseudoSprite_Tier" + tier, parent);
        SetRect(spriteRect, anchoredPosition, size);
        Image image = AddSlicedImage(spriteRect.gameObject, PseudoSprite(tier), color, Image.Type.Simple, false);
        image.preserveAspect = true;
        return image;
    }

    private static Image AddCroppedPseudoSprite(Transform parent, int tier, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform spriteRect = CreateRect("PseudoSprite_Tier" + tier, parent);
        SetRect(spriteRect, anchoredPosition, size);
        Image image = AddSlicedImage(spriteRect.gameObject, CroppedPseudoSprite(tier), color, Image.Type.Simple, false);
        image.preserveAspect = true;
        return image;
    }

    private static Image AddHudSprite(Transform parent, string name, string sourceFileName, Color color, Vector2 anchoredPosition, Vector2 size, Image.Type imageType, bool preserveAspect = false)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, anchoredPosition, size);
        Image image = AddSlicedImage(rect.gameObject, HudTintSprite(sourceFileName), color, imageType, false);
        image.preserveAspect = preserveAspect;
        return image;
    }

    private static Image AddRawHudSprite(Transform parent, string name, string sourceFileName, Color color, Vector2 anchoredPosition, Vector2 size, bool preserveAspect = false)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, anchoredPosition, size);
        Image image = AddSlicedImage(rect.gameObject, HudRawSprite(sourceFileName), color, Image.Type.Simple, false);
        image.preserveAspect = preserveAspect;
        return image;
    }

    private static Image AddHudIcon(Transform parent, string name, string sourceFileName, Color color, Vector2 anchoredPosition, float size)
    {
        return AddHudSprite(parent, name, sourceFileName, color, anchoredPosition, new Vector2(size, size), Image.Type.Simple, true);
    }

    private static Image AddPixelSprite(Transform parent, string name, string relativePath, Color color, Vector2 anchoredPosition, Vector2 size, Image.Type imageType, bool preserveAspect = false)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, anchoredPosition, size);
        Image image = AddSlicedImage(rect.gameObject, PixelSprite(relativePath), color, imageType, false);
        image.preserveAspect = preserveAspect;
        return image;
    }

    private static Image AddPixelIcon(Transform parent, string name, string relativePath, Color color, Vector2 anchoredPosition, float size)
    {
        return AddPixelSprite(parent, name, relativePath, color, anchoredPosition, new Vector2(size, size), Image.Type.Simple, true);
    }

    private static void AddScanlineLayer(Transform parent, float alpha)
    {
        RectTransform scan = CreateRect("ScanlineOverlay", parent);
        SetStretch(scan);
        CyberScanlineGraphic graphic = scan.gameObject.AddComponent<CyberScanlineGraphic>();
        graphic.raycastTarget = false;
        graphic.color = new Color(1f, 1f, 1f, alpha);
        graphic.LineSpacing = 12f;
        graphic.LineThickness = 1f;
        graphic.DrawVerticalTicks = false;
        graphic.LineColor = new Color(0.18f, 0.90f, 1f, 0.72f);
        graphic.TickColor = new Color(1f, 0.23f, 0.53f, 0.14f);
    }

    private static void AddAccentRail(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform rail = CreateRect(name, parent);
        SetRect(rail, anchoredPosition, size);
        AddSlicedImage(rail.gameObject, null, color, Image.Type.Simple, false);
    }

    private static void AddText(Transform parent, string name, string value, int size, FontStyle style, TextAnchor alignment, Color color, Vector2 anchoredPosition, Vector2 textSize)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, anchoredPosition, textSize);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = TerminalFont();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;
    }

    private static Image AddSlicedImage(GameObject target, Sprite sprite, Color color, Image.Type imageType, bool raycastTarget)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? imageType : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static GameObject CreateUiRoot(string name, Vector2 size)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return root;
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static ColorBlock ButtonColors(Color accent)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(Color.white, accent, 0.18f);
        colors.pressedColor = Color.Lerp(Color.white, accent, 0.38f);
        colors.selectedColor = Color.Lerp(Color.white, Selected, 0.24f);
        colors.disabledColor = new Color(0.36f, 0.40f, 0.48f, 0.66f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private static Texture2D BitmapFontAtlas()
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(BitmapFontAtlasPath);
    }

    private static TextAsset BitmapFontMetrics()
    {
        return AssetDatabase.LoadAssetAtPath<TextAsset>(BitmapFontMetricsPath);
    }

    private static Font TerminalFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(TerminalFontPath);
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void EnsureBitmapFontImportSettings()
    {
        TextureImporter importer = AssetImporter.GetAtPath(BitmapFontAtlasPath) as TextureImporter;
        if (importer == null)
            return;

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }

        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            dirty = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        dirty |= EnsureUncompressedPlatformSettings(importer);

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();
    }

    private static void EnsureHudSourceImportSettings()
    {
        if (!AssetDatabase.IsValidFolder(HudRoot))
        {
            Debug.LogWarning("Cyberpunk HUD source folder is missing: " + HudRoot);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { HudRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            FilterMode targetFilterMode = UsesSharpHudSampling(path) ? FilterMode.Point : FilterMode.Bilinear;
            if (importer.filterMode != targetFilterMode)
            {
                importer.filterMode = targetFilterMode;
                dirty = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            dirty |= EnsureUncompressedPlatformSettings(importer);

            if (dirty)
                importer.SaveAndReimport();
        }
    }

    private static void EnsureMainMenuOuterShellImportSettings()
    {
        TextureImporter importer = AssetImporter.GetAtPath(MainMenuOuterShellPath) as TextureImporter;
        if (importer == null)
            return;

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        dirty |= EnsureUncompressedPlatformSettings(importer);

        if (dirty)
            importer.SaveAndReimport();
    }

    private static bool UsesSharpHudSampling(string assetPath)
    {
        string fileName = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(fileName))
            return false;

        for (int i = 0; i < SharpHudPrefixes.Length; i++)
        {
            if (fileName.StartsWith(SharpHudPrefixes[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void EnsurePixelSourceImportSettings()
    {
        if (!AssetDatabase.IsValidFolder(PixelRoot))
        {
            Debug.LogWarning("Pixel UI HUD source folder is missing: " + PixelRoot);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PixelRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            dirty |= EnsureUncompressedPlatformSettings(importer);

            if (dirty)
                importer.SaveAndReimport();
        }
    }

    private static bool EnsureUncompressedPlatformSettings(TextureImporter importer)
    {
        bool dirty = false;
        string[] platformNames =
        {
            "Standalone",
            "WebGL",
            "Android",
            "iPhone"
        };

        for (int i = 0; i < platformNames.Length; i++)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformNames[i]);
            if (!settings.overridden || settings.textureCompression == TextureImporterCompression.Uncompressed)
                continue;

            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
            dirty = true;
        }

        return dirty;
    }

    private static void GenerateHudTintMask(string sourceFileName, Vector4 spriteBorder, float alphaMultiplier)
    {
        string sourceAssetPath = HudRoot + "/" + sourceFileName;
        string fullSourcePath = Path.Combine(Directory.GetCurrentDirectory(), sourceAssetPath);
        if (!File.Exists(fullSourcePath))
        {
            Debug.LogWarning("Cyberpunk HUD source sprite is missing: " + sourceAssetPath);
            return;
        }

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(fullSourcePath)))
        {
            Object.DestroyImmediate(source);
            Debug.LogWarning("Failed to read Cyberpunk HUD source sprite: " + sourceAssetPath);
            return;
        }

        Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            float intensity = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) / 255f;
            float alpha = (pixel.a / 255f) * Mathf.Pow(intensity, 0.72f) * alphaMultiplier;
            byte outputAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
            pixels[i] = new Color32(255, 255, 255, outputAlpha);
        }

        Texture2D mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        mask.SetPixels32(pixels);
        string assetPath = HudTintPath(sourceFileName);
        SaveTexture(assetPath, mask, spriteBorder, FilterMode.Point);
        Object.DestroyImmediate(source);
    }

    private static string HudTintPath(string sourceFileName)
    {
        return HudDerivedRoot + "/" + Path.GetFileNameWithoutExtension(sourceFileName) + "_tint.png";
    }

    private static Sprite HudTintSprite(string sourceFileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(HudTintPath(sourceFileName));
    }

    private static Sprite HudRawSprite(string sourceFileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(HudRoot + "/" + sourceFileName);
    }

    private static Sprite MainMenuOuterShellSprite()
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuOuterShellPath);
    }

    private static Sprite PixelSprite(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(PixelRoot + "/" + relativePath);
    }

    private static Sprite TerminalPanelSprite()
    {
        return HudTintSprite("panel_menu_frame_full.png");
    }

    private static Sprite CommandButtonSprite()
    {
        return HudTintSprite("btn_trapezoid_base_01.png");
    }

    private static Sprite TierCardSprite()
    {
        return HudTintSprite("frame_square_05.png");
    }

    private static Sprite ThinFrameSprite()
    {
        return HudTintSprite("frame_square_04.png");
    }

    private static Sprite PseudoSprite(int tier)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(PseudoSpriteRoot + Mathf.Clamp(tier, 1, 5) + ".png");
    }

    private static Sprite CroppedPseudoSprite(int tier)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(CroppedPseudoSpriteRoot + Mathf.Clamp(tier, 1, 5) + ".png");
    }

    private static void GenerateCroppedPseudoSprites()
    {
        for (int tier = 1; tier <= 5; tier++)
            GenerateCroppedPseudoSprite(tier);
    }

    private static void GenerateCroppedPseudoSprite(int tier)
    {
        string sourceAssetPath = PseudoSpriteRoot + tier + ".png";
        string fullSourcePath = Path.Combine(Directory.GetCurrentDirectory(), sourceAssetPath);
        if (!File.Exists(fullSourcePath))
            return;

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(fullSourcePath)))
        {
            Object.DestroyImmediate(source);
            return;
        }

        Color32[] pixels = source.GetPixels32();
        int minX = source.width;
        int minY = source.height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < source.height; y++)
        {
            for (int x = 0; x < source.width; x++)
            {
                if (pixels[y * source.width + x].a <= 8)
                    continue;

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            Object.DestroyImmediate(source);
            return;
        }

        const int padding = 4;
        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(source.width - 1, maxX + padding);
        maxY = Mathf.Min(source.height - 1, maxY + padding);
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] croppedPixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                croppedPixels[y * width + x] = pixels[(minY + y) * source.width + minX + x];
        }

        cropped.SetPixels32(croppedPixels);
        SaveTexture(CroppedPseudoSpriteRoot + tier + ".png", cropped, Vector4.zero, FilterMode.Point);
        Object.DestroyImmediate(source);
    }

    private static void SavePrefab(GameObject root, string path)
    {
        EnsureFolder(Path.GetDirectoryName(path)?.Replace("\\", "/"));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return;
        if (path == "Assets")
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        if (!string.IsNullOrWhiteSpace(parent))
            EnsureFolder(parent);

        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(folderName) && !AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void WriteFrameSprite(string assetPath, int width, int height, int cornerCut, int border, Color fill, Color borderColor, Color quietLine, Color accent, Vector4 spriteBorder)
    {
        Texture2D texture = NewTexture(width, height);
        DrawCutPanel(texture, width, height, cornerCut, fill);
        DrawCutBorder(texture, width, height, cornerCut + 4, 2, new Color(0f, 0f, 0f, 0.62f));
        DrawCutBorder(texture, width, height, cornerCut, border, borderColor);
        DrawInsetFrame(texture, width, height, cornerCut + 9, border, quietLine);
        DrawInsetFrame(texture, width, height, cornerCut + 18, 1, new Color(0.04f, 0.16f, 0.26f, 0.54f));
        DrawCircuitDetails(texture, width, height, cornerCut, borderColor, quietLine, accent);
        SaveTexture(assetPath, texture, spriteBorder);
    }

    private static void DrawCircuitDetails(Texture2D texture, int width, int height, int cornerCut, Color borderColor, Color quietLine, Color accent)
    {
        Color dimCyan = new Color(borderColor.r, borderColor.g, borderColor.b, 0.42f);
        Color dimMagenta = new Color(accent.r, accent.g, accent.b, 0.52f);
        Color purpleShadow = new Color(0.19f, 0.07f, 0.34f, 0.40f);

        FillRect(texture, cornerCut + 18, height - 12, Mathf.Max(16, width / 3), 3, dimMagenta);
        FillRect(texture, cornerCut + 30, height - 20, Mathf.Max(16, width / 5), 2, borderColor);
        FillRect(texture, width - cornerCut - width / 4, height - 12, Mathf.Max(12, width / 6), 3, dimCyan);
        FillRect(texture, width - cornerCut - width / 5, 8, Mathf.Max(14, width / 7), 3, borderColor);
        FillRect(texture, cornerCut + 14, 10, Mathf.Max(16, width / 6), 2, quietLine);

        FillRect(texture, 8, cornerCut + 12, 4, Mathf.Max(12, height - cornerCut * 2 - 24), purpleShadow);
        FillRect(texture, width - 12, cornerCut + 12, 4, Mathf.Max(12, height - cornerCut * 2 - 24), purpleShadow);
        FillRect(texture, 14, height - cornerCut - 26, 6, 18, dimCyan);
        FillRect(texture, width - 20, cornerCut + 8, 6, 18, dimMagenta);

        for (int i = 0; i < 7; i++)
        {
            int x = width - cornerCut - 60 + i * 8;
            FillRect(texture, x, height - 22, 4, 4, i % 3 == 0 ? dimMagenta : dimCyan);
        }

        for (int i = 0; i < 5; i++)
        {
            int y = cornerCut + 20 + i * 9;
            FillRect(texture, width - 23, y, 8, 2, quietLine);
        }

        for (int i = 0; i < 4; i++)
            FillRect(texture, cornerCut + 28 + i * 12, 18, i % 2 == 0 ? 8 : 4, 2, dimCyan);
    }

    private static void WriteScanlineSprite(string assetPath)
    {
        Texture2D texture = NewTexture(16, 16);
        FillRect(texture, 0, 0, 16, 16, new Color(0f, 0f, 0f, 0f));
        FillRect(texture, 0, 0, 16, 1, new Color(0.10f, 0.86f, 1f, 0.28f));
        FillRect(texture, 0, 8, 2, 1, new Color(1f, 0.23f, 0.53f, 0.32f));
        SaveTexture(assetPath, texture, Vector4.zero);
    }

    private static Texture2D NewTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, Color.clear);
        }

        return texture;
    }

    private static void DrawCutPanel(Texture2D texture, int width, int height, int cornerCut, Color color)
    {
        for (int y = 0; y < height; y++)
        {
            int inset = 0;
            if (y < cornerCut)
                inset = cornerCut - y;
            else if (y >= height - cornerCut)
                inset = y - (height - cornerCut - 1);

            FillRect(texture, inset, y, width - inset * 2, 1, color);
        }
    }

    private static void DrawCutBorder(Texture2D texture, int width, int height, int cornerCut, int border, Color color)
    {
        FillRect(texture, cornerCut, height - border, width - cornerCut * 2, border, color);
        FillRect(texture, cornerCut, 0, width - cornerCut * 2, border, color);
        FillRect(texture, 0, cornerCut, border, height - cornerCut * 2, color);
        FillRect(texture, width - border, cornerCut, border, height - cornerCut * 2, color);

        for (int i = 0; i < cornerCut; i++)
        {
            FillRect(texture, i, height - cornerCut + i, border + 1, border + 1, color);
            FillRect(texture, width - i - border - 1, height - cornerCut + i, border + 1, border + 1, color);
            FillRect(texture, i, cornerCut - i - border, border + 1, border + 1, color);
            FillRect(texture, width - i - border - 1, cornerCut - i - border, border + 1, border + 1, color);
        }
    }

    private static void DrawInsetFrame(Texture2D texture, int width, int height, int inset, int thickness, Color color)
    {
        FillRect(texture, inset, height - inset, width - inset * 2, thickness, color);
        FillRect(texture, inset, inset - thickness, width - inset * 2, thickness, color);
        FillRect(texture, inset - thickness, inset, thickness, height - inset * 2, color);
        FillRect(texture, width - inset, inset, thickness, height - inset * 2, color);
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        Color32 pixel = color;
        int xMin = Mathf.Max(0, x);
        int yMin = Mathf.Max(0, y);
        int xMax = Mathf.Min(texture.width, x + width);
        int yMax = Mathf.Min(texture.height, y + height);
        for (int py = yMin; py < yMax; py++)
        {
            for (int px = xMin; px < xMax; px++)
                texture.SetPixel(px, py, pixel);
        }
    }

    private static void SaveTexture(string assetPath, Texture2D texture, Vector4 spriteBorder, FilterMode filterMode = FilterMode.Point)
    {
        texture.Apply();
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = spriteBorder;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = filterMode;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        EnsureUncompressedPlatformSettings(importer);
        importer.SaveAndReimport();
    }
}
