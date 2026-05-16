using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public class BattleHudPreviewBuilder : MonoBehaviour
{
    [SerializeField] private bool rebuildOnEnable = false;
    [SerializeField] private string canvasName = "Canvas_Arena";

    private const int MaxCP = 10;
    private bool isBuilding;

    private static readonly Color32 Panel = new Color32(13, 18, 24, 232);
    private static readonly Color32 PanelStrong = new Color32(9, 13, 18, 246);
    private static readonly Color32 PanelSoft = new Color32(24, 30, 37, 226);
    private static readonly Color32 Stroke = new Color32(64, 75, 88, 255);
    private static readonly Color32 MainText = new Color32(238, 242, 246, 255);
    private static readonly Color32 MutedText = new Color32(158, 170, 181, 255);
    private static readonly Color32 BatteryColor = new Color32(53, 204, 127, 255);
    private static readonly Color32 CPColor = new Color32(73, 181, 255, 255);
    private static readonly Color32 AttackColor = new Color32(239, 96, 82, 255);
    private static readonly Color32 DefenseColor = new Color32(90, 202, 166, 255);
    private static readonly Color32 StatusColor = new Color32(185, 127, 255, 255);
    private static readonly Color32 UtilityColor = new Color32(240, 179, 82, 255);

    private void OnEnable()
    {
        if (!Application.isPlaying && rebuildOnEnable)
            RebuildHud();

        EnsurePreviewExtras();
    }

    private void Start()
    {
        EnsurePreviewExtras();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || !isActiveAndEnabled || !rebuildOnEnable)
            return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && !Application.isPlaying && isActiveAndEnabled)
                RebuildHud();
        };
    }

    [UnityEditor.MenuItem("AlgoMon/UI/Rebuild Battle HUD Preview")]
    private static void RebuildFromMenu()
    {
        BattleHudPreviewBuilder builder = Object.FindObjectOfType<BattleHudPreviewBuilder>();
        if (builder == null)
        {
            var builderObject = new GameObject("ArenaHUDPreviewBuilder");
            builder = builderObject.AddComponent<BattleHudPreviewBuilder>();
        }

        builder.RebuildHud();
        UnityEditor.Selection.activeGameObject = builder.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
    }

    /// <summary>
    /// One-shot: saves the current Canvas_Arena in the open scene as a prefab
    /// at the canonical path and reconnects the scene instance to it.
    /// After running this, the prefab is the source of truth for HUD layout —
    /// manual edits in the prefab persist across runs and are not clobbered
    /// by RebuildHud (which now guards against overwriting a prefab instance).
    /// </summary>
    [UnityEditor.MenuItem("AlgoMon/Build/Migrate HUD to Prefab")]
    private static void MigrateHudToPrefab()
    {
        const string prefabPath = "Assets/_AlgoMon/Prefabs/UI/Arena/BattleHud.prefab";

        GameObject canvas = GameObject.Find(DefaultCanvasName);
        if (canvas == null)
        {
            UnityEditor.EditorUtility.DisplayDialog(
                "Migrate HUD to Prefab — failed",
                $"No GameObject named '{DefaultCanvasName}' was found in the active scene.\n\n" +
                "Run 'AlgoMon/UI/Rebuild Battle HUD Preview' first to generate the HUD, then retry.",
                "OK");
            return;
        }

        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(canvas))
        {
            UnityEditor.EditorUtility.DisplayDialog(
                "Migrate HUD to Prefab — already migrated",
                $"'{DefaultCanvasName}' is already a prefab instance. Nothing to do.\n\n" +
                "Edit the prefab directly to change the HUD layout.",
                "OK");
            return;
        }

        if (canvas.GetComponent<BattleHudController>() == null)
            canvas.AddComponent<BattleHudController>();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prefabPath));

        GameObject prefabAsset = UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect(
            canvas, prefabPath, UnityEditor.InteractionMode.AutomatedAction);

        if (prefabAsset == null)
        {
            UnityEditor.EditorUtility.DisplayDialog(
                "Migrate HUD to Prefab — failed",
                $"Could not save prefab at:\n{prefabPath}",
                "OK");
            return;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(canvas.scene);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        Debug.Log($"HUD prefab saved at {prefabPath}; scene instance reconnected.");
        UnityEditor.EditorUtility.DisplayDialog(
            "Migrate HUD to Prefab — complete",
            $"Canvas_Arena is now a prefab instance.\n\n" +
            $"Prefab saved at:\n{prefabPath}\n\n" +
            "Going forward:\n" +
            "• Edit the prefab to change HUD layout — your edits will stick.\n" +
            "• RebuildHud now asks for confirmation before overwriting a prefab instance.\n" +
            "• Once you've verified everything works, BattleHudPreviewBuilder.cs itself can be deleted.",
            "OK");
    }
#endif

    private const string DefaultCanvasName = "Canvas_Arena";

    [ContextMenu("Rebuild Battle HUD Preview")]
    public void RebuildHud()
    {
        if (isBuilding)
            return;

#if UNITY_EDITOR
        // Refuse to silently disconnect a prefab instance — that would destroy
        // any layout edits the designer made in prefab edit mode.
        GameObject existing = GameObject.Find(canvasName);
        if (existing != null && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(existing))
        {
            bool proceed = UnityEditor.EditorUtility.DisplayDialog(
                "Rebuild Battle HUD Preview",
                $"'{canvasName}' is a prefab instance.\n\n" +
                "Rebuilding will DELETE the instance and recreate the canvas from scratch, " +
                "disconnecting it from the prefab and losing any layout edits.\n\n" +
                "Continue anyway?",
                "Rebuild (lose prefab connection)",
                "Cancel");
            if (!proceed)
                return;
        }
#endif

        isBuilding = true;

        try
        {
            DestroyExistingCanvas();
            DestroyRootObject("SkillButton");

            GameObject canvasObject = CreateUIObject(canvasName, transform);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject safeArea = CreatePanel("SafeArea", canvasObject.transform, Stretch(0f, 0f, 1f, 1f), Clear);

            CreateTopBar(safeArea.transform);
            CreateCombatLayer(safeArea.transform);
            CreateCommandPanel(safeArea.transform);
            RebuildGroundDiscs();

            // Ensure the runtime HUD API controller exists on the canvas. Persistent
            // in the scene file so BattleManager (#15) can reference it; only binds
            // child refs at runtime to avoid hooking events while in edit mode.
            BattleHudController hudController = canvasObject.GetComponent<BattleHudController>();
            if (hudController == null)
                hudController = canvasObject.AddComponent<BattleHudController>();
            if (Application.isPlaying)
                hudController.Bind();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
        finally
        {
            isBuilding = false;
        }
    }

    private void DestroyExistingCanvas()
    {
        GameObject existing = GameObject.Find(canvasName);
        if (existing == null)
            return;

        DestroyObject(existing);
    }

    private static void DestroyRootObject(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing == null || existing.transform.parent != null)
            return;

        DestroyObject(existing);
    }

    private void RebuildGroundDiscs()
    {
        EnsureGroundDisc(
            "PlayerSpriteAnchor",
            "PlayerGroundDisc",
            new Vector3(0f, -0.65f, 0f),
            new Vector3(1.20f, 0.45f, 1f),
            new Color32(221, 239, 255, 118),
            14,
            true);

        EnsureGroundDisc(
            "EnemySpriteAnchor",
            "EnemyGroundDisc",
            new Vector3(0f, -0.52f, 0f),
            new Vector3(0.88f, 0.34f, 1f),
            new Color32(221, 239, 255, 92),
            6,
            true);
    }

    private void EnsurePreviewExtras()
    {
        bool changed = false;
        changed |= EnsureGroundDisc(
            "PlayerSpriteAnchor",
            "PlayerGroundDisc",
            new Vector3(0f, -0.65f, 0f),
            new Vector3(1.20f, 0.45f, 1f),
            new Color32(221, 239, 255, 118),
            14,
            false);

        changed |= EnsureGroundDisc(
            "EnemySpriteAnchor",
            "EnemyGroundDisc",
            new Vector3(0f, -0.52f, 0f),
            new Vector3(0.88f, 0.34f, 1f),
            new Color32(221, 239, 255, 92),
            6,
            false);

        changed |= EnsureVoltArrayPowerTag();

#if UNITY_EDITOR
        if (changed && !Application.isPlaying && gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private bool EnsureGroundDisc(
        string anchorName,
        string discName,
        Vector3 localPosition,
        Vector3 localScale,
        Color32 color,
        int sortingOrder,
        bool resetTransform)
    {
        GameObject anchor = GameObject.Find(anchorName);
        if (anchor == null)
            return false;

        Transform existing = anchor.transform.Find(discName);
        bool changed = false;
        GameObject disc;

        if (existing == null)
        {
            disc = new GameObject(discName);
            disc.transform.SetParent(anchor.transform, false);
            changed = true;
        }
        else
        {
            disc = existing.gameObject;
        }

        if (resetTransform || existing == null)
        {
            disc.transform.localPosition = localPosition;
            disc.transform.localScale = localScale;
            changed = true;
        }

        var renderer = disc.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = disc.AddComponent<SpriteRenderer>();
            changed = true;
        }

        if (renderer.sprite != GroundDiscSprite)
        {
            renderer.sprite = GroundDiscSprite;
            changed = true;
        }

        if (renderer.color != (Color)color)
            changed = true;
        renderer.color = color;

        if (renderer.sortingOrder != sortingOrder)
            changed = true;
        renderer.sortingOrder = sortingOrder;
        return changed;
    }

    private bool EnsureVoltArrayPowerTag()
    {
        GameObject buttonObject = GameObject.Find("SkillButton_1");
        if (buttonObject == null)
            return false;

        bool changed = false;
        changed |= EnsureTag(buttonObject.transform, "CPTag", "CP 4", CPColor, Stretch(0.56f, 0.20f, 0.69f, 0.80f));
        changed |= EnsureTag(buttonObject.transform, "PowerTag", "PWR 50", AttackColor, Stretch(0.72f, 0.20f, 0.94f, 0.80f));
        return changed;
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void CreateTopBar(Transform parent)
    {
        GameObject topBar = CreatePanel("TopBar", parent, Stretch(0f, 0.94f, 1f, 1f), PanelStrong);
        AddText(topBar.transform, "RoundText", "Round 1", 28, TextAnchor.MiddleCenter, Stretch(0.39f, 0.08f, 0.61f, 0.92f), UtilityColor, FontStyle.Bold);
        AddText(topBar.transform, "BattleStateText", "Player turn", 22, TextAnchor.MiddleRight, Stretch(0.70f, 0.08f, 0.975f, 0.92f), MutedText);
    }

    private void CreateCombatLayer(Transform parent)
    {
        GameObject combatLayer = CreatePanel("CombatLayer", parent, Stretch(0f, 0f, 1f, 0.94f), Clear);

        CreateCombatantPanel(
            combatLayer.transform,
            "PlayerCombatantPanel",
            "Sortex",
            "Lv. 14",
            132,
            165,
            6,
            Stretch(0.03f, 0.025f, 0.30f, 0.225f),
            true);

        CreateCombatantPanel(
            combatLayer.transform,
            "EnemyCombatantPanel",
            "Cachelon",
            "Lv. 12",
            180,
            180,
            8,
            Stretch(0.725f, 0.785f, 0.98f, 0.965f),
            false);
    }

    private void CreateCombatantPanel(
        Transform parent,
        string name,
        string monName,
        string level,
        int battery,
        int maxBattery,
        int cp,
        RectSpec rect,
        bool isPlayer)
    {
        GameObject panel = CreatePanel(name, parent, rect, Panel);
        AddOutline(panel, Stroke, new Vector2(1f, -1f));

        AddText(panel.transform, "NameText", monName, 27, TextAnchor.MiddleLeft, Stretch(0.055f, 0.72f, 0.62f, 0.94f), MainText, FontStyle.Bold);
        AddText(panel.transform, "LevelText", level, 18, TextAnchor.MiddleRight, Stretch(0.64f, 0.75f, 0.94f, 0.92f), MutedText);

        float batteryRatio = maxBattery <= 0 ? 0f : Mathf.Clamp01((float)battery / maxBattery);
        CreateResourceBar(panel.transform, "BatteryBar", "BATTERY", $"{battery} / {maxBattery}", BatteryColor, Stretch(0.055f, 0.48f, 0.945f, 0.64f), batteryRatio);
        AddText(panel.transform, "CPLabel", "CP", 15, TextAnchor.MiddleLeft, Stretch(0.055f, 0.28f, 0.13f, 0.40f), CPColor, FontStyle.Bold);
        CreateCPDots(panel.transform, "CPDots", cp, Stretch(0.14f, 0.26f, 0.945f, 0.42f));
        CreateStatusRow(panel.transform, isPlayer ? "Status: Ready" : "Status: Freeze x1", Stretch(0.055f, 0.07f, 0.945f, 0.22f));
    }

    private void CreateCommandPanel(Transform parent)
    {
        GameObject commandPanel = CreatePanel("CommandPanel", parent, Stretch(0f, 0f, 1f, 1f), Clear);

        GameObject skillPanel = CreatePanel("SkillPanel", commandPanel.transform, Stretch(0.045f, 0.635f, 0.305f, 0.925f), PanelStrong);
        AddOutline(skillPanel, new Color32(42, 49, 58, 255), new Vector2(1f, -1f));

        GameObject actionPanel = CreatePanel("ActionPanel", commandPanel.transform, Stretch(0.66f, 0.025f, 0.985f, 0.34f), PanelStrong);
        AddOutline(actionPanel, new Color32(42, 49, 58, 255), new Vector2(1f, -1f));

        CreateSkillDetailPanel(
            actionPanel.transform,
            Stretch(0.05f, 0.52f, 0.95f, 0.92f),
            out Text detailTitle,
            out Text detailBody);

        GameObject skillGrid = CreatePanel("SkillGrid", skillPanel.transform, Stretch(0.055f, 0.06f, 0.945f, 0.94f), Clear);
        var grid = skillGrid.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(456f, 58f);
        grid.spacing = new Vector2(0f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.MiddleCenter;

        CreateSkillButton(skillGrid.transform, "SkillButton_1", "Volt Array", "CP 4", "PWR 50", "", AttackColor, true, detailTitle, detailBody, "Volt Array", "CP 4 | PWR 50\nReliable Electric attack.\nNo counter effect.");
        CreateSkillButton(skillGrid.transform, "SkillButton_2", "Faraday Cage", "CP 2", "", "Counter", DefenseColor, true, detailTitle, detailBody, "Faraday Cage", "CP 2 | Counter\nDefense skill. Reduces incoming damage when it wins the matchup.");
        CreateSkillButton(skillGrid.transform, "SkillButton_3", "Auto-Tuning", "CP 2", "", "", StatusColor, true, detailTitle, detailBody, "Auto-Tuning", "CP 2\nStatus skill. Raises Computing Power.");
        CreateSkillButton(skillGrid.transform, "SkillButton_4", "Hyper-Threading", "CP 2", "", "", StatusColor, true, detailTitle, detailBody, "Hyper-Threading", "CP 2\nStatus skill. Next skill fires twice.");

        GameObject actionGrid = CreatePanel("ActionGrid", actionPanel.transform, Stretch(0.05f, 0.08f, 0.95f, 0.46f), Clear);
        var actionLayout = actionGrid.AddComponent<GridLayoutGroup>();
        actionLayout.cellSize = new Vector2(245f, 44f);
        actionLayout.spacing = new Vector2(16f, 12f);
        actionLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        actionLayout.constraintCount = 2;
        actionLayout.childAlignment = TextAnchor.MiddleCenter;

        CreateActionButton(actionGrid.transform, "RechargeButton", "Recharge", "+5 CP", UtilityColor, detailTitle, detailBody, "Recharge", "+5 CP\nSpend the turn to restore CP.");
        CreateActionButton(actionGrid.transform, "BagButton", "Bag", "", new Color32(126, 167, 224, 255), detailTitle, detailBody, "Bag", "Open battle items.");
        CreateActionButton(actionGrid.transform, "SwitchButton", "Switch", "", new Color32(87, 201, 218, 255), detailTitle, detailBody, "Switch", "Change the active AlgoMon.");
        CreateActionButton(actionGrid.transform, "FleeButton", "Flee", "", new Color32(224, 91, 91, 255), detailTitle, detailBody, "Flee", "Attempt to escape from battle.");
    }

    private GameObject CreateSkillButton(
        Transform parent,
        string name,
        string skillName,
        string cp,
        string powerText,
        string counterText,
        Color32 accent,
        bool interactable,
        Text detailTitle,
        Text detailBody,
        string hoverTitle,
        string hoverBody)
    {
        GameObject buttonObject = CreatePanel(name, parent, FixedSize(456f, 58f), SkillButtonTint(accent));
        buttonObject.GetComponent<Image>().raycastTarget = true;
        AddOutline(buttonObject, new Color32(accent.r, accent.g, accent.b, 215), new Vector2(1f, -1f));

        GameObject accentStrip = CreatePanel("TypeStrip", buttonObject.transform, Stretch(0f, 0f, 0.045f, 1f), accent);
        accentStrip.GetComponent<Image>().raycastTarget = false;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.interactable = interactable;
        var colors = button.colors;
        Color32 normalColor = SkillButtonTint(accent);
        colors.normalColor = normalColor;
        colors.highlightedColor = Brighten(normalColor, 18);
        colors.pressedColor = Brighten(normalColor, 32);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color32(28, 28, 28, 180);
        button.colors = colors;

        AddText(buttonObject.transform, "SkillNameText", skillName, 20, TextAnchor.MiddleLeft, Stretch(0.075f, 0.12f, 0.50f, 0.88f), MainText, FontStyle.Bold);

        bool hasPower = !string.IsNullOrEmpty(powerText);
        bool hasCounter = !string.IsNullOrEmpty(counterText);
        if (hasPower && hasCounter)
        {
            CreateTag(buttonObject.transform, "CPTag", cp, CPColor, Stretch(0.52f, 0.20f, 0.63f, 0.80f));
            CreateTag(buttonObject.transform, "PowerTag", powerText, AttackColor, Stretch(0.65f, 0.20f, 0.78f, 0.80f));
            CreateTag(buttonObject.transform, "CounterTag", counterText, DefenseColor, Stretch(0.80f, 0.20f, 0.96f, 0.80f));
        }
        else if (hasPower)
        {
            CreateTag(buttonObject.transform, "CPTag", cp, CPColor, Stretch(0.56f, 0.20f, 0.69f, 0.80f));
            CreateTag(buttonObject.transform, "PowerTag", powerText, AttackColor, Stretch(0.72f, 0.20f, 0.94f, 0.80f));
        }
        else if (hasCounter)
        {
            CreateTag(buttonObject.transform, "CPTag", cp, CPColor, Stretch(0.56f, 0.20f, 0.69f, 0.80f));
            CreateTag(buttonObject.transform, "CounterTag", counterText, DefenseColor, Stretch(0.72f, 0.20f, 0.96f, 0.80f));
        }
        else
        {
            CreateTag(buttonObject.transform, "CPTag", cp, CPColor, Stretch(0.60f, 0.20f, 0.74f, 0.80f));
        }

        AddHoverPreview(buttonObject, detailTitle, detailBody, hoverTitle, hoverBody);
        return buttonObject;
    }

    private void CreateActionButton(
        Transform parent,
        string name,
        string label,
        string meta,
        Color32 accent,
        Text detailTitle,
        Text detailBody,
        string hoverTitle,
        string hoverBody)
    {
        GameObject buttonObject = CreatePanel(name, parent, FixedSize(245f, 44f), SkillButtonTint(accent));
        buttonObject.GetComponent<Image>().raycastTarget = true;
        AddOutline(buttonObject, accent, new Vector2(1f, -1f));

        GameObject accentStrip = CreatePanel("ActionStrip", buttonObject.transform, Stretch(0f, 0f, 0.035f, 1f), accent);
        accentStrip.GetComponent<Image>().raycastTarget = false;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        var colors = button.colors;
        Color32 normalColor = SkillButtonTint(accent);
        colors.normalColor = normalColor;
        colors.highlightedColor = Brighten(normalColor, 18);
        colors.pressedColor = Brighten(normalColor, 32);
        button.colors = colors;

        float labelMax = string.IsNullOrEmpty(meta) ? 0.94f : 0.62f;
        AddText(buttonObject.transform, "LabelText", label, 18, TextAnchor.MiddleLeft, Stretch(0.08f, 0.10f, labelMax, 0.90f), MainText, FontStyle.Bold);

        if (!string.IsNullOrEmpty(meta))
            CreateTag(buttonObject.transform, "MetaTag", meta, accent, Stretch(0.66f, 0.18f, 0.94f, 0.82f));

        AddHoverPreview(buttonObject, detailTitle, detailBody, hoverTitle, hoverBody);
    }

    private void CreateResourceBar(Transform parent, string name, string title, string value, Color32 fillColor, RectSpec rect, float fillAmount)
    {
        GameObject bar = CreatePanel(name, parent, rect, new Color32(33, 40, 48, 255));

        GameObject fill = CreatePanel("Fill", bar.transform, Stretch(0f, 0f, fillAmount, 1f), fillColor);
        var fillImage = fill.GetComponent<Image>();
        fillImage.raycastTarget = false;

        AddText(bar.transform, "TitleText", title, 14, TextAnchor.MiddleLeft, Stretch(0.03f, 0f, 0.35f, 1f), Color.white, FontStyle.Bold);
        AddText(bar.transform, "ValueText", value, 14, TextAnchor.MiddleRight, Stretch(0.38f, 0f, 0.97f, 1f), Color.white);
    }

    private void CreateCPDots(Transform parent, string name, int currentCP, RectSpec rect)
    {
        GameObject row = CreatePanel(name, parent, rect, Clear);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < MaxCP; i++)
        {
            Color32 color = i < currentCP ? CPColor : new Color32(48, 57, 66, 255);
            GameObject dot = CreatePanel($"CP_{i + 1:00}", row.transform, FixedSize(20f, 20f), color);
            AddOutline(dot, new Color32(15, 19, 24, 255), new Vector2(1f, -1f));
        }
    }

    private void CreateStatusRow(Transform parent, string statusText, RectSpec rect)
    {
        GameObject row = CreatePanel("StatusRow", parent, rect, Clear);
        AddText(row.transform, "StatusText", statusText, 17, TextAnchor.MiddleLeft, Stretch(0f, 0f, 1f, 1f), MutedText);
    }

    private void CreateTag(Transform parent, string name, string text, Color32 color, RectSpec rect)
    {
        GameObject tag = CreatePanel(name, parent, rect, new Color32(color.r, color.g, color.b, 45));
        AddOutline(tag, color, new Vector2(1f, -1f));
        AddText(tag.transform, "Text", text, 16, TextAnchor.MiddleCenter, Stretch(0.06f, 0f, 0.94f, 1f), MainText, FontStyle.Bold);
    }

    private bool EnsureTag(Transform parent, string name, string text, Color32 color, RectSpec rect)
    {
        Transform tagTransform = parent.Find(name);
        if (tagTransform == null)
        {
            CreateTag(parent, name, text, color, rect);
            return true;
        }

        bool changed = false;
        var tagRect = tagTransform.GetComponent<RectTransform>();
        if (tagRect != null &&
            (tagRect.anchorMin != rect.AnchorMin ||
             tagRect.anchorMax != rect.AnchorMax ||
             tagRect.offsetMin != rect.OffsetMin ||
             tagRect.offsetMax != rect.OffsetMax ||
             tagRect.pivot != rect.Pivot))
        {
            ApplyRect(tagRect, rect);
            changed = true;
        }

        var image = tagTransform.GetComponent<Image>();
        Color tagColor = new Color32(color.r, color.g, color.b, 45);
        if (image == null)
        {
            image = tagTransform.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            changed = true;
        }

        if (image.color != tagColor)
        {
            image.color = tagColor;
            changed = true;
        }

        var outline = tagTransform.GetComponent<Outline>();
        if (outline == null)
        {
            AddOutline(tagTransform.gameObject, color, new Vector2(1f, -1f));
            changed = true;
        }
        else if (outline.effectColor != (Color)color || outline.effectDistance != new Vector2(1f, -1f))
        {
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
            changed = true;
        }

        Text label = null;
        Transform labelTransform = tagTransform.Find("Text");
        if (labelTransform != null)
            label = labelTransform.GetComponent<Text>();

        if (label == null)
        {
            AddText(tagTransform, "Text", text, 16, TextAnchor.MiddleCenter, Stretch(0.06f, 0f, 0.94f, 1f), MainText, FontStyle.Bold);
            return true;
        }

        if (label.text != text)
        {
            label.text = text;
            changed = true;
        }

        if (label.color != (Color)MainText)
        {
            label.color = MainText;
            changed = true;
        }

        return changed;
    }

    private GameObject CreateSkillDetailPanel(Transform parent, RectSpec rect, out Text titleText, out Text bodyText)
    {
        GameObject panel = CreatePanel("SkillDetailPanel", parent, rect, PanelSoft);
        AddOutline(panel, Stroke, new Vector2(1f, -1f));
        titleText = AddText(panel.transform, "TitleText", "Skill Details", 22, TextAnchor.MiddleLeft, Stretch(0.06f, 0.70f, 0.94f, 0.92f), MainText, FontStyle.Bold);
        bodyText = AddText(panel.transform, "BodyText", "Ready.", 17, TextAnchor.UpperLeft, Stretch(0.06f, 0.12f, 0.94f, 0.66f), MutedText);
        return panel;
    }

    private void AddHoverPreview(GameObject target, Text detailTitle, Text detailBody, string title, string body)
    {
        var trigger = target.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (detailTitle != null)
                detailTitle.text = title;
            if (detailBody != null)
                detailBody.text = body;
        });

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            if (detailTitle != null)
                detailTitle.text = "Skill Details";
            if (detailBody != null)
                detailBody.text = "Ready.";
        });

        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private static Sprite GroundDiscSprite
    {
        get
        {
            if (groundDiscSprite == null)
                groundDiscSprite = CreateGroundDiscSprite();

            return groundDiscSprite;
        }
    }

    private static Sprite groundDiscSprite;

    private static Sprite CreateGroundDiscSprite()
    {
        const int width = 256;
        const int height = 128;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "GeneratedBattleGroundDiscTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[width * height];
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float radiusX = width * 0.46f;
        float radiusY = height * 0.34f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - center.x) / radiusX;
                float dy = (y - center.y) / radiusY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.SmoothStep(0f, 1f, alpha) * 0.8f;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "GeneratedBattleGroundDiscSprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static GameObject CreatePanel(string name, Transform parent, RectSpec rect, Color color)
    {
        GameObject panel = CreateUIObject(name, parent);
        ApplyRect(panel.GetComponent<RectTransform>(), rect);

        if (color.a > 0)
        {
            var image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        return panel;
    }

    private static Color32 SkillButtonTint(Color32 accent)
    {
        return new Color32(
            (byte)Mathf.Clamp(18f + accent.r * 0.12f, 0f, 255f),
            (byte)Mathf.Clamp(23f + accent.g * 0.12f, 0f, 255f),
            (byte)Mathf.Clamp(29f + accent.b * 0.12f, 0f, 255f),
            255);
    }

    private static Color32 Brighten(Color32 color, byte amount)
    {
        return new Color32(
            (byte)Mathf.Clamp(color.r + amount, 0, 255),
            (byte)Mathf.Clamp(color.g + amount, 0, 255),
            (byte)Mathf.Clamp(color.b + amount, 0, 255),
            color.a);
    }

    private static Text AddText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, RectSpec rect, Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject textObject = CreateUIObject(name, parent);
        ApplyRect(textObject.GetComponent<RectTransform>(), rect);

        var text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = false;
        text.resizeTextMinSize = Mathf.Max(11, fontSize - 8);
        text.resizeTextMaxSize = fontSize;
        return text;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            obj.layer = uiLayer;

        if (parent != null)
            obj.transform.SetParent(parent, false);

        return obj;
    }

    private static void ApplyRect(RectTransform rectTransform, RectSpec rect)
    {
        rectTransform.anchorMin = rect.AnchorMin;
        rectTransform.anchorMax = rect.AnchorMax;
        rectTransform.offsetMin = rect.OffsetMin;
        rectTransform.offsetMax = rect.OffsetMax;
        rectTransform.pivot = rect.Pivot;
        rectTransform.localScale = Vector3.one;
    }

    private static RectSpec Stretch(float minX, float minY, float maxX, float maxY)
    {
        return new RectSpec
        {
            AnchorMin = new Vector2(minX, minY),
            AnchorMax = new Vector2(maxX, maxY),
            OffsetMin = Vector2.zero,
            OffsetMax = Vector2.zero,
            Pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private static RectSpec FixedSize(float width, float height)
    {
        return new RectSpec
        {
            AnchorMin = new Vector2(0.5f, 0.5f),
            AnchorMax = new Vector2(0.5f, 0.5f),
            OffsetMin = new Vector2(-width * 0.5f, -height * 0.5f),
            OffsetMax = new Vector2(width * 0.5f, height * 0.5f),
            Pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private struct RectSpec
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 OffsetMin;
        public Vector2 OffsetMax;
        public Vector2 Pivot;
    }

    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
}
