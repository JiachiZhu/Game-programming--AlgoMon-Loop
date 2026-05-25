using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Thin interaction layer for the MainTerminal cover scene.
/// Visual layout lives in the scene; this script only owns run start routing
/// and small status text updates.
/// </summary>
[DisallowMultipleComponent]
public class MainTerminalController : MonoBehaviour
{
    private const int StarterLevel = 20;

    [Header("Actions")]
    [SerializeField] private Button enterGridButton;
    [SerializeField] private Button geneLabButton;
    [SerializeField] private Button payloadButton;
    [SerializeField] private Button systemLogButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Status")]
    [SerializeField] private Text moduleText;
    [SerializeField] private Text warningText;
    [SerializeField] private Text detailText;
    [SerializeField] private Text footerText;
    [SerializeField] private Text partyPreviewText;
    [SerializeField] private Text statsText;
    [SerializeField] private Image progressFill;
    [SerializeField] private Image progressTrack;
    [SerializeField] private Image progressBase;
    [SerializeField] private Image progressScan;
    [SerializeField] private Image statusPanel;
    [SerializeField] private RectTransform payloadPanel;
    [SerializeField] private Image payloadPortraitImage;
    [SerializeField] private Text payloadPortraitFallbackText;
    [SerializeField] private Text payloadListText;
    [SerializeField] private Text payloadDetailPanelText;

    [Header("Starter Fallback")]
    [SerializeField] private AlgoMonData fallbackStarter;

    private GameManager manager;
    private float bootTime;
    private Font defaultFont;
    private int selectedPayloadIndex = -1;

    private void Awake()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        manager = GameManager.EnsureInstance();
        EnsureStarterParty(manager, fallbackStarter);
        EnsureHudWidgets();
        RefreshRunOverview();
    }

    private void OnEnable()
    {
        WireButton(enterGridButton, StartRun);
        WireButton(geneLabButton, ShowGeneLabPlaceholder);
        WireButton(payloadButton, ShowPayloadBox);
        WireButton(systemLogButton, ShowSystemLogPlaceholder);
        WireButton(settingsButton, ShowSettingsPlaceholder);
        WireButton(exitButton, ShowExitPlaceholder);
    }

    private void OnDisable()
    {
        UnwireButton(enterGridButton, StartRun);
        UnwireButton(geneLabButton, ShowGeneLabPlaceholder);
        UnwireButton(payloadButton, ShowPayloadBox);
        UnwireButton(systemLogButton, ShowSystemLogPlaceholder);
        UnwireButton(settingsButton, ShowSettingsPlaceholder);
        UnwireButton(exitButton, ShowExitPlaceholder);
    }

    private void Start()
    {
        bootTime = Time.unscaledTime;
        SetModule("ENTER_GRID", "WARNING:", "INITIALIZING GRID CONNECTION...", "DATA LOSS IMMINENT | SYNCING NEURAL LINK...");
        RefreshRunOverview();
    }

    private void Update()
    {
        if (footerText != null)
        {
            float elapsed = Time.unscaledTime - bootTime;
            footerText.text = $"> SESSION_USER: ADMIN | LOCAL_TIME: {FormatClock(elapsed)} | SYSTEM_STABILITY: 98.4%";
        }

        if (progressFill != null)
        {
            const float segmentWidth = 0.34f;
            float cycle = Mathf.Repeat(Time.unscaledTime * 0.52f, 1f);
            float start = Mathf.Lerp(-segmentWidth, 1f, cycle);
            float glow = Mathf.PingPong(Time.unscaledTime * 2.8f, 1f);
            progressFill.color = Color.Lerp(
                new Color(1f, 0.34f, 0.67f, 0.86f),
                new Color(1f, 0.62f, 0.86f, 0.98f),
                glow);

            RectTransform fillRect = progressFill.rectTransform;
            fillRect.anchorMin = new Vector2(start, 0f);
            fillRect.anchorMax = new Vector2(start + segmentWidth, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        if (progressScan != null && progressScan.enabled)
        {
            const float segmentWidth = 0.34f;
            const float scanWidth = 0.055f;
            float cycle = Mathf.Repeat(Time.unscaledTime * 0.52f, 1f);
            float start = Mathf.Lerp(-segmentWidth, 1f, cycle);
            float scanStart = start + segmentWidth * 0.68f;
            RectTransform scanRect = progressScan.rectTransform;
            scanRect.anchorMin = new Vector2(scanStart, 0f);
            scanRect.anchorMax = new Vector2(scanStart + scanWidth, 1f);
            scanRect.offsetMin = Vector2.zero;
            scanRect.offsetMax = Vector2.zero;
        }

        RefreshRunOverview();
    }

    private void StartRun()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureStarterParty(manager, fallbackStarter);
        RefreshRunOverview();
        manager.BeginRun();
        GameManager.GoTo(GameScene.TheGrid);
    }

    private void ShowGeneLabPlaceholder()
    {
        SetModule("GENE_LAB", "LOCKED:", "GENE MERGE PROTOCOL OFFLINE.", "Gene Lab routing is scheduled for a later sprint.");
    }

    private void ShowPayloadBox()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        int payloadCount = manager != null && manager.payload != null ? manager.payload.Count : 0;
        if (payloadCount > 0)
            selectedPayloadIndex = Mathf.Clamp(selectedPayloadIndex < 0 ? payloadCount - 1 : selectedPayloadIndex, 0, payloadCount - 1);

        SetModule(
            "PAYLOAD_BOX",
            "PAYLOAD:",
            $"{payloadCount} DATA FRAGMENT(S) STORED.",
            BuildPayloadPreview(manager));
        RenderPayloadPanel(manager);
    }

    private void ShowSystemLogPlaceholder()
    {
        SetModule("SYSTEM_LOG", "LOG:", "GRID MODULE READY.", "Start Run will initialize a fresh route graph.");
    }

    private void ShowSettingsPlaceholder()
    {
        SetModule("SETTINGS", "LOCKED:", "CONFIG PANEL NOT DEPLOYED.", "Settings are outside the Sprint 3 playable loop.");
    }

    private void ShowExitPlaceholder()
    {
        SetModule("EXIT_SYSTEM", "STANDBY:", "TERMINAL SESSION HELD OPEN.", "Exit is disabled in editor builds.");
    }

    private void SetModule(string moduleId, string warning, string headline, string detail)
    {
        if (moduleText != null)
            moduleText.text = $"MODULE_ID: {moduleId}";
        if (warningText != null)
            warningText.text = warning;
        if (detailText != null)
            detailText.text = $"{headline}\n\n{detail}";
        if (moduleId != "PAYLOAD_BOX")
            HidePayloadPanel();
    }

    private void RefreshRunOverview()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureStarterParty(manager, fallbackStarter);

        if (partyPreviewText != null)
            partyPreviewText.text = BuildPartyPreview(manager);

        if (statsText != null)
        {
            int payloadCount = manager.payload != null ? manager.payload.Count : 0;
            string runStatus = manager.IsRunActive ? "ACTV" : "STBY";
            int rewardPercent = manager.IsRunActive
                ? Mathf.RoundToInt(manager.currentRewardMultiplier * 100f)
                : ThreatTierRules.RewardMultiplierPercent(manager.SelectedThreatTier, manager.HighestUnlockedThreatTier);
            statsText.text =
                $"PAYLOAD// {payloadCount:00}\n" +
                $"RUN// {runStatus} T{manager.SelectedThreatTierNumber:00}/{manager.HighestUnlockedThreatTierNumber:00} x{rewardPercent:000}%\n" +
                $"SQUAD// {PartyCount(manager):00}/{GameManager.MaxPartySize:00}";
        }

        if (payloadPanel != null && payloadPanel.gameObject.activeSelf)
            RenderPayloadPanel(manager);
    }

    private string BuildPartyPreview(GameManager targetManager)
    {
        if (targetManager == null || targetManager.party == null || targetManager.party.Count == 0)
            return "SQUAD// EMPTY";

        var builder = new StringBuilder("SQUAD// ACTIVE");
        for (int i = 0; i < targetManager.party.Count && i < GameManager.MaxPartySize; i++)
        {
            AlgoMonInstance mon = targetManager.party[i];

            builder.Append('\n');
            builder.Append(FormatPartyMon(mon, i + 1));
        }

        return builder.ToString();
    }

    private static string FormatPartyMon(AlgoMonInstance mon, int slot)
    {
        if (mon == null)
            return $"SLOT {slot}: EMPTY";

        string name = DisplayNameFor(mon);
        string element = mon.data != null ? mon.data.elementType.ToString().ToUpperInvariant() : "NORMAL";
        return $"{slot:00}// {name.ToUpperInvariant()}\nL{mon.level:00} [{ShortElement(element)}]\nBAT{mon.Battery:00} SPD{mon.ClockSpeed:00}";
    }

    private static string BuildPayloadPreview(GameManager targetManager)
    {
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
            return "No extracted AlgoMon data yet.\nDefeat encounters in TheArena to auto-store copies here.";

        const int maxVisible = 8;
        var builder = new StringBuilder("EXTRACTED DATA CACHE");
        int visible = Mathf.Min(targetManager.payload.Count, maxVisible);
        for (int i = 0; i < visible; i++)
        {
            builder.Append('\n');
            builder.Append(FormatPayloadMon(targetManager.payload[i], i + 1));
        }

        int hidden = targetManager.payload.Count - visible;
        if (hidden > 0)
            builder.Append($"\n... {hidden} more record(s)");

        return builder.ToString();
    }

    private static string FormatPayloadMon(AlgoMonInstance mon, int slot)
    {
        if (mon == null)
            return $"{slot:00}// CORRUPTED RECORD";

        string name = DisplayNameFor(mon);
        string element = mon.data != null ? mon.data.elementType.ToString().ToUpperInvariant() : "NORMAL";
        return $"{slot:00}// {name.ToUpperInvariant()} L{mon.level:00} [{ShortElement(element)}] BAT{mon.Battery:00} CPU{mon.ComputingPower:00} TP{mon.Throughput:00}";
    }

    private void RenderPayloadPanel(GameManager targetManager)
    {
        if (payloadPanel == null)
            return;

        payloadPanel.gameObject.SetActive(true);
        if (targetManager == null || targetManager.payload == null || targetManager.payload.Count == 0)
        {
            selectedPayloadIndex = -1;
            SetPayloadPortrait(null, "NO DATA");
            if (payloadListText != null)
                payloadListText.text = "PAYLOAD INDEX\n-- EMPTY --";
            if (payloadDetailPanelText != null)
                payloadDetailPanelText.text = "Defeat an encounter in TheArena to archive its persistent AlgoMon data here.";
            return;
        }

        selectedPayloadIndex = Mathf.Clamp(selectedPayloadIndex, 0, targetManager.payload.Count - 1);
        AlgoMonInstance selected = targetManager.payload[selectedPayloadIndex];
        SetPayloadPortrait(ResolvePayloadSprite(selected), DisplayNameFor(selected).ToUpperInvariant());

        if (payloadListText != null)
            payloadListText.text = BuildPayloadList(targetManager, selectedPayloadIndex);
        if (payloadDetailPanelText != null)
            payloadDetailPanelText.text = BuildPayloadDetail(selected);
    }

    private void HidePayloadPanel()
    {
        if (payloadPanel != null)
            payloadPanel.gameObject.SetActive(false);
    }

    private void SetPayloadPortrait(Sprite sprite, string fallbackLabel)
    {
        if (payloadPortraitImage != null)
        {
            payloadPortraitImage.sprite = sprite;
            payloadPortraitImage.enabled = sprite != null;
            payloadPortraitImage.preserveAspect = true;
            payloadPortraitImage.color = Color.white;
        }

        if (payloadPortraitFallbackText != null)
        {
            payloadPortraitFallbackText.enabled = sprite == null;
            payloadPortraitFallbackText.text = string.IsNullOrWhiteSpace(fallbackLabel) ? "NO IMAGE" : fallbackLabel;
        }
    }

    private static string BuildPayloadList(GameManager targetManager, int selectedIndex)
    {
        const int maxVisible = 8;
        int count = targetManager.payload.Count;
        int start = Mathf.Clamp(selectedIndex - maxVisible + 1, 0, Mathf.Max(0, count - maxVisible));
        int end = Mathf.Min(count, start + maxVisible);
        var builder = new StringBuilder("PAYLOAD INDEX");

        for (int i = start; i < end; i++)
        {
            AlgoMonInstance mon = targetManager.payload[i];
            string marker = i == selectedIndex ? ">" : " ";
            string label = mon != null
                ? $"{DisplayNameFor(mon).ToUpperInvariant()} L{mon.level:00}"
                : "CORRUPTED RECORD";
            builder.Append('\n');
            builder.Append($"{marker} {i + 1:00}// {label}");
        }

        return builder.ToString();
    }

    private static string BuildPayloadDetail(AlgoMonInstance mon)
    {
        if (mon == null)
            return "CORRUPTED RECORD";

        AlgoMonData data = mon.data;
        string codeName = data != null && !string.IsNullOrWhiteSpace(data.codeName) ? data.codeName.Trim() : DisplayNameFor(mon);
        string element = data != null ? data.elementType.ToString().ToUpperInvariant() : "NORMAL";
        string subroutine = data != null && data.subroutine != null && !string.IsNullOrWhiteSpace(data.subroutine.subroutineName)
            ? data.subroutine.subroutineName.Trim()
            : "NONE";

        var builder = new StringBuilder();
        builder.AppendLine($"{DisplayNameFor(mon).ToUpperInvariant()}");
        builder.AppendLine($"CODE: {codeName.ToUpperInvariant()}  ELEMENT: {element}");
        builder.AppendLine($"LV {mon.level:00}/{AlgoMonInstance.MAX_LEVEL}  EXP {mon.exp}/{mon.expToNextLevel}");
        builder.AppendLine($"SUBROUTINE: {subroutine.ToUpperInvariant()}");
        builder.AppendLine();
        builder.AppendLine("ACTIVE STATS");
        builder.AppendLine($"BAT {mon.Battery:000}  SPD {mon.ClockSpeed:000}");
        builder.AppendLine($"CPU {mon.ComputingPower:000}  TP  {mon.Throughput:000}");
        builder.AppendLine($"FW  {mon.Firewall:000}  ENC {mon.Encryption:000}");
        builder.AppendLine();
        builder.AppendLine("HARDWARE IV");
        builder.AppendLine($"BAT {mon.iv_Battery:000}  SPD {mon.iv_ClockSpeed:000}");
        builder.AppendLine($"CPU {mon.iv_ComputingPower:000}  TP  {mon.iv_Throughput:000}");
        builder.AppendLine($"FW  {mon.iv_Firewall:000}  ENC {mon.iv_Encryption:000}");
        builder.AppendLine();
        builder.AppendLine("SKILLS");
        AppendPayloadSkills(builder, mon);

        if (data != null && !string.IsNullOrWhiteSpace(data.description))
        {
            builder.AppendLine();
            builder.AppendLine("PROFILE");
            builder.Append(data.description.Trim());
        }

        return builder.ToString();
    }

    private static void AppendPayloadSkills(StringBuilder builder, AlgoMonInstance mon)
    {
        if (mon.knownSkills == null || mon.knownSkills.Count == 0)
        {
            builder.Append("- NONE");
            return;
        }

        for (int i = 0; i < mon.knownSkills.Count; i++)
        {
            SkillData skill = mon.knownSkills[i];
            if (skill == null)
            {
                builder.AppendLine("- CORRUPTED SKILL");
                continue;
            }

            string name = !string.IsNullOrWhiteSpace(skill.skillName) ? skill.skillName.Trim() : skill.name;
            string type = skill.instructionType.ToString().ToUpperInvariant();
            string element = skill.elementType.ToString().ToUpperInvariant();
            builder.AppendLine($"- {name.ToUpperInvariant()} [{type}/{element}] PWR {skill.basePower:00} CP {skill.cpCost:00}");
        }
    }

    private static Sprite ResolvePayloadSprite(AlgoMonInstance mon)
    {
        if (mon == null || mon.data == null)
            return null;

        if (mon.data.portrait != null)
            return mon.data.portrait;

#if UNITY_EDITOR
        string codeName = PayloadSpriteName(mon.data.codeName);
        if (string.IsNullOrEmpty(codeName))
            return null;

        string path = $"Assets/_AlgoMon/Sprites/{codeName.ToUpperInvariant()}/{codeName}_Base.png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }

    private static string PayloadSpriteName(string codeName)
    {
        if (string.IsNullOrWhiteSpace(codeName))
            return string.Empty;

        var builder = new StringBuilder(codeName.Length);
        for (int i = 0; i < codeName.Length; i++)
        {
            char c = codeName[i];
            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static string DisplayNameFor(AlgoMonInstance mon)
    {
        if (mon == null)
            return "AlgoMon";

        if (!string.IsNullOrWhiteSpace(mon.nickname))
            return mon.nickname.Trim();

        return mon.data != null && !string.IsNullOrWhiteSpace(mon.data.codeName)
            ? mon.data.codeName.Trim()
            : "AlgoMon";
    }

    private static string ShortElement(string element)
    {
        if (string.IsNullOrEmpty(element))
            return "NORM";

        return element.Length <= 4 ? element : element.Substring(0, 4);
    }

    private static int PartyCount(GameManager targetManager)
    {
        return targetManager != null && targetManager.party != null ? targetManager.party.Count : 0;
    }

    private void EnsureHudWidgets()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        RectTransform root = canvas.GetComponent<RectTransform>();
        RectTransform overlay = CreateRect("TerminalStatusOverlay", root);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        moduleText = moduleText != null
            ? moduleText
            : CreateText("ModuleText", overlay, 15, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 1f, 1f, 1f));
        moduleText.color = new Color(0.78f, 1f, 1f, 1f);
        ApplyCyberText(moduleText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1f, -1f));
        SetAnchors(moduleText.rectTransform, new Vector2(0.055f, 0.825f), new Vector2(0.43f, 0.88f));

        warningText = warningText != null
            ? warningText
            : CreateText("WarningText", overlay, 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.78f, 1f));
        warningText.color = new Color(1f, 0.55f, 0.78f, 1f);
        ApplyCyberText(warningText, new Color(0.2f, 0f, 0.18f, 1f), new Vector2(1.4f, -1.4f));
        SetAnchors(warningText.rectTransform, new Vector2(0.055f, 0.755f), new Vector2(0.43f, 0.82f));

        detailText = detailText != null
            ? detailText
            : CreateText("DetailText", overlay, 18, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.96f, 0.99f, 1f, 1f));
        detailText.color = new Color(0.96f, 0.99f, 1f, 1f);
        ApplyCyberText(detailText, new Color(0.03f, 0.1f, 0.16f, 0.9f), new Vector2(1.35f, -1.35f));
        SetAnchors(detailText.rectTransform, new Vector2(0.055f, 0.595f), new Vector2(0.48f, 0.75f));

        RectTransform statusPanelRect = EnsureStatusPanel(overlay);

        partyPreviewText = partyPreviewText != null
            ? partyPreviewText
            : CreateText("PartyPreviewText", statusPanelRect, 12, FontStyle.Bold, TextAnchor.UpperRight, new Color(0.54f, 1f, 0.72f, 1f));
        partyPreviewText.transform.SetParent(statusPanelRect, false);
        partyPreviewText.fontSize = 12;
        partyPreviewText.color = new Color(0.54f, 1f, 0.72f, 1f);
        partyPreviewText.lineSpacing = 0.92f;
        ApplyCyberText(partyPreviewText, new Color(0f, 0.2f, 0.12f, 1f), new Vector2(1.15f, -1.15f));
        SetAnchors(partyPreviewText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.48f));

        statsText = statsText != null
            ? statsText
            : CreateText("StatsText", statusPanelRect, 12, FontStyle.Bold, TextAnchor.UpperRight, new Color(0.78f, 1f, 1f, 1f));
        statsText.transform.SetParent(statusPanelRect, false);
        statsText.fontSize = 12;
        statsText.color = new Color(0.78f, 1f, 1f, 1f);
        statsText.lineSpacing = 0.95f;
        ApplyCyberText(statsText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1.2f, -1.2f));
        SetAnchors(statsText.rectTransform, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.90f));

        footerText = footerText != null
            ? footerText
            : CreateText("FooterText", overlay, 13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.58f, 0.9f, 1f, 1f));
        footerText.color = new Color(0.58f, 0.9f, 1f, 1f);
        ApplyCyberText(footerText, new Color(0f, 0.12f, 0.18f, 0.95f), new Vector2(1f, -1f));
        SetAnchors(footerText.rectTransform, new Vector2(0.055f, 0.07f), new Vector2(0.945f, 0.115f));

        RectTransform progressTrackRect = EnsureMainScreenProgress(overlay);
        RectTransform progressRailRect = progressBase != null
            ? progressBase.rectTransform
            : progressTrackRect;

        progressFill = progressFill != null
            ? progressFill
            : CreateImage("ProgressFill", progressRailRect, new Color(1f, 0.48f, 0.72f, 0.86f));
        progressFill.transform.SetParent(progressRailRect, false);
        progressFill.type = Image.Type.Simple;
        progressFill.raycastTarget = false;
        SetAnchors(progressFill.rectTransform, new Vector2(-0.34f, 0f), new Vector2(0f, 1f));

        progressScan = progressScan != null
            ? progressScan
            : CreateImage("ProgressScan", progressRailRect, Color.clear);
        progressScan.transform.SetParent(progressRailRect, false);
        progressScan.color = Color.clear;
        progressScan.enabled = false;
        progressScan.raycastTarget = false;

        EnsurePayloadPanel(overlay);
        HidePayloadPanel();
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private Text CreateText(string objectName, Transform parent, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private RectTransform EnsureStatusPanel(Transform parent)
    {
        statusPanel = statusPanel != null
            ? statusPanel
            : CreateImage("StatusPanel", parent, new Color(0.01f, 0.018f, 0.035f, 0.66f));
        statusPanel.raycastTarget = false;
        statusPanel.color = new Color(0.01f, 0.018f, 0.035f, 0.66f);
        RectTransform panelRect = statusPanel.rectTransform;
        SetAnchors(panelRect, new Vector2(0.922f, 0.64f), new Vector2(0.992f, 0.86f));

        Image topLine = CreateImage("StatusPanelTopLine", panelRect, new Color(0.08f, 0.92f, 1f, 0.86f));
        SetAnchors(topLine.rectTransform, new Vector2(0f, 0.96f), new Vector2(1f, 1f));

        Image rightLine = CreateImage("StatusPanelRightLine", panelRect, new Color(1f, 0.25f, 0.86f, 0.62f));
        SetAnchors(rightLine.rectTransform, new Vector2(0.985f, 0f), new Vector2(1f, 1f));

        Image scanLine = CreateImage("StatusPanelScanLine", panelRect, new Color(0.54f, 1f, 0.72f, 0.28f));
        SetAnchors(scanLine.rectTransform, new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.505f));

        return panelRect;
    }

    private RectTransform EnsureMainScreenProgress(Transform parent)
    {
        progressTrack = progressTrack != null
            ? progressTrack
            : CreateImage("MainScreenProgressTrack", parent, new Color(0.006f, 0.008f, 0.014f, 1f));
        progressTrack.raycastTarget = false;
        progressTrack.color = new Color(0.006f, 0.008f, 0.014f, 1f);
        RectTransform trackRect = progressTrack.rectTransform;
        SetAnchors(trackRect, new Vector2(0.580f, 0.386f), new Vector2(0.788f, 0.416f));

        progressBase = progressBase != null
            ? progressBase
            : CreateImage("MainScreenProgressBase", trackRect, new Color(1f, 0.20f, 0.52f, 0.22f));
        progressBase.transform.SetParent(trackRect, false);
        progressBase.raycastTarget = false;
        progressBase.color = new Color(0.018f, 0.024f, 0.038f, 1f);
        SetAnchors(progressBase.rectTransform, new Vector2(0.045f, 0.32f), new Vector2(0.955f, 0.68f));

        RectMask2D railMask = progressBase.GetComponent<RectMask2D>();
        if (railMask == null)
            railMask = progressBase.gameObject.AddComponent<RectMask2D>();
        railMask.padding = Vector4.zero;

        return trackRect;
    }

    private void EnsurePayloadPanel(Transform parent)
    {
        if (payloadPanel == null)
        {
            Image panelImage = CreateImage("PayloadDetailPanel", parent, new Color(0.006f, 0.012f, 0.026f, 0.86f));
            payloadPanel = panelImage.rectTransform;
        }

        Image background = payloadPanel.GetComponent<Image>();
        if (background == null)
            background = payloadPanel.gameObject.AddComponent<Image>();
        background.raycastTarget = false;
        background.color = new Color(0.006f, 0.012f, 0.026f, 0.86f);
        SetAnchors(payloadPanel, new Vector2(0.515f, 0.465f), new Vector2(0.888f, 0.785f));

        Image topLine = CreateImage("PayloadTopLine", payloadPanel, new Color(0.08f, 0.92f, 1f, 0.82f));
        SetAnchors(topLine.rectTransform, new Vector2(0f, 0.985f), new Vector2(1f, 1f));

        Image sideLine = CreateImage("PayloadSideLine", payloadPanel, new Color(1f, 0.25f, 0.86f, 0.58f));
        SetAnchors(sideLine.rectTransform, new Vector2(0f, 0f), new Vector2(0.012f, 1f));

        Image portraitFrame = CreateImage("PayloadPortraitFrame", payloadPanel, new Color(0.02f, 0.036f, 0.064f, 0.92f));
        SetAnchors(portraitFrame.rectTransform, new Vector2(0.045f, 0.23f), new Vector2(0.38f, 0.88f));

        payloadPortraitImage = payloadPortraitImage != null
            ? payloadPortraitImage
            : CreateImage("PayloadPortraitImage", portraitFrame.rectTransform, Color.white);
        payloadPortraitImage.transform.SetParent(portraitFrame.rectTransform, false);
        payloadPortraitImage.preserveAspect = true;
        payloadPortraitImage.raycastTarget = false;
        SetAnchors(payloadPortraitImage.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));

        payloadPortraitFallbackText = payloadPortraitFallbackText != null
            ? payloadPortraitFallbackText
            : CreateText("PayloadPortraitFallbackText", portraitFrame.rectTransform, 16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.78f, 1f, 1f, 1f));
        payloadPortraitFallbackText.transform.SetParent(portraitFrame.rectTransform, false);
        payloadPortraitFallbackText.color = new Color(0.78f, 1f, 1f, 1f);
        payloadPortraitFallbackText.lineSpacing = 0.9f;
        ApplyCyberText(payloadPortraitFallbackText, new Color(0f, 0.16f, 0.24f, 1f), new Vector2(1f, -1f));
        SetAnchors(payloadPortraitFallbackText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));

        payloadListText = payloadListText != null
            ? payloadListText
            : CreateText("PayloadListText", payloadPanel, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.54f, 1f, 0.72f, 1f));
        payloadListText.transform.SetParent(payloadPanel, false);
        payloadListText.fontSize = 10;
        payloadListText.lineSpacing = 0.9f;
        payloadListText.color = new Color(0.54f, 1f, 0.72f, 1f);
        ApplyCyberText(payloadListText, new Color(0f, 0.2f, 0.12f, 1f), new Vector2(1f, -1f));
        SetAnchors(payloadListText.rectTransform, new Vector2(0.045f, 0.035f), new Vector2(0.38f, 0.205f));

        payloadDetailPanelText = payloadDetailPanelText != null
            ? payloadDetailPanelText
            : CreateText("PayloadDetailText", payloadPanel, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.96f, 0.99f, 1f, 1f));
        payloadDetailPanelText.transform.SetParent(payloadPanel, false);
        payloadDetailPanelText.fontSize = 10;
        payloadDetailPanelText.lineSpacing = 0.88f;
        payloadDetailPanelText.color = new Color(0.96f, 0.99f, 1f, 1f);
        ApplyCyberText(payloadDetailPanelText, new Color(0.03f, 0.1f, 0.16f, 0.95f), new Vector2(1f, -1f));
        SetAnchors(payloadDetailPanelText.rectTransform, new Vector2(0.43f, 0.06f), new Vector2(0.965f, 0.92f));
    }

    private static void ApplyCyberText(Text text, Color effectColor, Vector2 distance)
    {
        if (text == null)
            return;

        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = effectColor;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;

        Shadow shadow = FindExactShadow(text);
        if (shadow == null)
            shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;
    }

    private static Shadow FindExactShadow(Text text)
    {
        Shadow[] shadows = text.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                return shadows[i];
        }

        return null;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnwireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private static void EnsureStarterParty(GameManager targetManager, AlgoMonData starterData)
    {
        if (targetManager == null || targetManager.party == null || targetManager.party.Count > 0)
            return;

        if (starterData == null)
            return;

        var starter = new AlgoMonInstance
        {
            data = starterData,
            nickname = starterData.codeName,
            level = StarterLevel,
            iv_Battery = 180,
            iv_ClockSpeed = 165,
            iv_ComputingPower = 150,
            iv_Throughput = 145,
            iv_Firewall = 130,
            iv_Encryption = 135
        };
        starter.EnsureKnownSkillsFromLearnset();
        targetManager.AddToParty(starter);
    }

    private static string FormatClock(float elapsed)
    {
        int totalSeconds = Mathf.FloorToInt(elapsed);
        int hours = 20 + totalSeconds / 3600;
        int minutes = (totalSeconds / 60) % 60;
        int seconds = totalSeconds % 60;
        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }
}
