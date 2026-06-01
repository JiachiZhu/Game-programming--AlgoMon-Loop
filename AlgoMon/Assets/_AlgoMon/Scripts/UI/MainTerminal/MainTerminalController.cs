/*
Script Audit:
- Purpose: Controls the MainTerminal menu scene and starts the gameplay run.
- Attached GameObject: MainTerminal scene UI/controller object, usually the terminal canvas or screen root.
- Main responsibilities: Wire menu buttons, create starter party if needed, start a run, show payload summary, update status text, and build fallback HUD widgets.
- Important variables: enterGridButton, geneLabButton, payloadButton, moduleText, detailText, partyPreviewText, statsText, payloadPanel, fallbackStarter, manager.
- Inputs: Player button clicks, GameManager payload/party data, fallback starter asset, and Time.unscaledTime.
- Outputs or effects: Updates terminal UI, starts GameManager.BeginRun, and transitions to TheGrid.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Open MainTerminal, start a run, inspect payload, and confirm starter party and TheGrid transition work.
*/
using System;
using System.Collections.Generic;
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
    private const int MinimumPlayablePartySize = 2;
    private const string AlgoMonAssetSearchFolder = "Assets/_AlgoMon/ScriptableObjects/AlgoMons";
    private const string EncounterSpeciesCatalogResourcePath = "EncounterSpeciesCatalog";
    private static readonly string[] PreferredReserveSpecies =
    {
        "Heapion",
        "Cachelon",
        "Recursix",
        "Overflux",
        "Nullbyte",
        "Sortex"
    };

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
    [SerializeField] private RectTransform depthTierPanel;
    [SerializeField] private Text depthTierTitleText;
    [SerializeField] private Text depthTierDetailText;
    [SerializeField] private Button[] depthTierButtons;
    [SerializeField] private bool unlockAllThreatTiersForVerticalSlice = true;

    [Header("Starter Fallback")]
    [SerializeField] private AlgoMonData fallbackStarter;

    private GameManager manager;
    private float bootTime;
    private Font defaultFont;
    private int selectedPayloadIndex = -1;
    private UnityEngine.Events.UnityAction[] depthTierButtonActions;

    private void Awake()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        manager = GameManager.EnsureInstance();
        EnsureThreatTierAccess(manager);
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
        WireDepthTierButtons();
    }

    private void OnDisable()
    {
        UnwireButton(enterGridButton, StartRun);
        UnwireButton(geneLabButton, ShowGeneLabPlaceholder);
        UnwireButton(payloadButton, ShowPayloadBox);
        UnwireButton(systemLogButton, ShowSystemLogPlaceholder);
        UnwireButton(settingsButton, ShowSettingsPlaceholder);
        UnwireButton(exitButton, ShowExitPlaceholder);
        UnwireDepthTierButtons();
    }

    private void Start()
    {
        bootTime = Time.unscaledTime;
        SetModule("ENTER_GRID", "DEPTH TIER:", "GRID ENTRY DEPTH SELECTED", BuildDepthTierDetail(manager));
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

        EnsureThreatTierAccess(manager);
        EnsureStarterParty(manager, fallbackStarter);
        RefreshRunOverview();
        manager.BeginRun();
        GameManager.GoTo(GameScene.TheGrid);
    }

    private void SelectDepthTier(int tier)
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        if (manager.IsRunActive)
        {
            SetModule("ENTER_GRID", "RUN ACTIVE:", "DEPTH TIER LOCKED", BuildDepthTierDetail(manager));
            RefreshRunOverview();
            return;
        }

        if (manager.TrySetSelectedThreatTier(tier))
            SetModule("ENTER_GRID", "DEPTH TIER:", $"DEPTH {tier}F SELECTED", BuildDepthTierDetail(manager));
        else
            SetModule("ENTER_GRID", "LOCKED:", $"DEPTH {tier}F UNAVAILABLE", BuildDepthTierDetail(manager));

        RefreshRunOverview();
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

        EnsureThreatTierAccess(manager);
        EnsureStarterParty(manager, fallbackStarter);

        if (partyPreviewText != null)
            partyPreviewText.text = BuildPartyPreview(manager);

        if (statsText != null)
        {
            int payloadCount = manager.payload != null ? manager.payload.Count : 0;
            int evolutionDataCount = manager.evolutionDataSpeciesCodes != null ? manager.evolutionDataSpeciesCodes.Count : 0;
            string runStatus = manager.IsRunActive ? "ACTV" : "STBY";
            int rewardPercent = manager.IsRunActive
                ? Mathf.RoundToInt(manager.currentRewardMultiplier * 100f)
                : ThreatTierRules.RewardMultiplierPercent(manager.SelectedThreatTier, manager.HighestUnlockedThreatTier);
            statsText.text =
                $"USER// XP {manager.playerExp:0000} CMP {manager.computeBalance:0000}\n" +
                $"PAYLOAD// {payloadCount:00}\n" +
                $"EVO// {evolutionDataCount:00}\n" +
                $"RUN// {runStatus} T{manager.SelectedThreatTierNumber:00}/{manager.HighestUnlockedThreatTierNumber:00} x{rewardPercent:000}%\n" +
                $"SQUAD// {PartyCount(manager):00}/{GameManager.MaxPartySize:00}";
        }

        RefreshDepthTierSelector();

        if (payloadPanel != null && payloadPanel.gameObject.activeSelf)
            RenderPayloadPanel(manager);
    }

    private void EnsureThreatTierAccess(GameManager targetManager)
    {
        if (targetManager == null || !unlockAllThreatTiersForVerticalSlice)
            return;

        if (targetManager.HighestUnlockedThreatTierNumber < ThreatTierRules.MaxTier)
            targetManager.SetHighestUnlockedThreatTier(ThreatTierRules.MaxTier);
    }

    private string BuildDepthTierDetail(GameManager targetManager)
    {
        if (targetManager == null)
            return "Depth tier data unavailable.";

        int selected = targetManager.SelectedThreatTierNumber;
        int highest = targetManager.HighestUnlockedThreatTierNumber;
        ThreatTier tier = targetManager.SelectedThreatTier;
        int rewardPercent = targetManager.IsRunActive
            ? Mathf.RoundToInt(targetManager.currentRewardMultiplier * 100f)
            : ThreatTierRules.RewardMultiplierPercent(tier, targetManager.HighestUnlockedThreatTier);

        return $"DEPTH {selected}F / TIER T{selected:00}/{highest:00}\nENEMY BAND LV {ThreatTierRules.MinLevel(tier):00}-{ThreatTierRules.MaxLevel(tier):00}\nREWARD MULTIPLIER x{rewardPercent:000}%";
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
        builder.AppendLine($"DATA QUALITY: {EncounterReward.FormatQuality(mon.dataQuality)}");
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

        EnsureDepthTierSelector(overlay);

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

    private void EnsureDepthTierSelector(Transform parent)
    {
        if (depthTierPanel == null)
        {
            Image panelImage = CreateImage("DepthTierSelector", parent, new Color(0.006f, 0.012f, 0.026f, 0.92f));
            depthTierPanel = panelImage.rectTransform;
        }

        depthTierPanel.transform.SetParent(parent, false);
        Image background = depthTierPanel.GetComponent<Image>();
        if (background == null)
            background = depthTierPanel.gameObject.AddComponent<Image>();
        background.raycastTarget = false;
        background.color = new Color(0.006f, 0.012f, 0.026f, 0.92f);
        SetAnchors(depthTierPanel, new Vector2(0.735f, 0.792f), new Vector2(0.805f, 0.868f));

        depthTierTitleText = depthTierTitleText != null
            ? depthTierTitleText
            : CreateText("DepthTierTitle", depthTierPanel, 10, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.88f, 0.94f, 1f, 1f));
        depthTierTitleText.transform.SetParent(depthTierPanel, false);
        depthTierTitleText.fontSize = 10;
        depthTierTitleText.color = new Color(0.88f, 0.94f, 1f, 1f);
        depthTierTitleText.text = "DEPTH";
        ApplyCyberText(depthTierTitleText, new Color(0f, 0.14f, 0.22f, 1f), new Vector2(1f, -1f));
        SetAnchors(depthTierTitleText.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 1f));

        depthTierDetailText = depthTierDetailText != null
            ? depthTierDetailText
            : CreateText("DepthTierDetail", depthTierPanel, 8, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.54f, 1f, 0.72f, 1f));
        depthTierDetailText.transform.SetParent(depthTierPanel, false);
        depthTierDetailText.fontSize = 8;
        depthTierDetailText.color = new Color(0.54f, 1f, 0.72f, 1f);
        ApplyCyberText(depthTierDetailText, new Color(0f, 0.2f, 0.12f, 1f), new Vector2(1f, -1f));
        SetAnchors(depthTierDetailText.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.68f));

        EnsureDepthTierButtons();
        RefreshDepthTierSelector();
    }

    private void EnsureDepthTierButtons()
    {
        if (depthTierPanel == null)
            return;

        int tierCount = ThreatTierRules.MaxTier - ThreatTierRules.MinTier + 1;
        if (depthTierButtons == null || depthTierButtons.Length != tierCount)
            depthTierButtons = new Button[tierCount];

        const float spacing = 0.012f;
        float width = (1f - spacing * (tierCount - 1)) / tierCount;
        for (int i = 0; i < tierCount; i++)
        {
            int tier = i + ThreatTierRules.MinTier;
            if (depthTierButtons[i] == null)
                depthTierButtons[i] = CreateDepthTierButton($"DepthTierButton_{tier}F", depthTierPanel);

            RectTransform rect = depthTierButtons[i].GetComponent<RectTransform>();
            float minX = i * (width + spacing);
            SetAnchors(rect, new Vector2(minX, 0.06f), new Vector2(minX + width, 0.40f));
        }
    }

    private Button CreateDepthTierButton(string objectName, RectTransform parent)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.018f, 0.032f, 0.052f, 1f);
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.Lerp(Color.white, new Color(0.54f, 1f, 0.72f, 1f), 0.24f);
        colors.pressedColor = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.78f, 1f), 0.38f);
        colors.selectedColor = Color.Lerp(Color.white, new Color(0.54f, 1f, 0.72f, 1f), 0.28f);
        colors.disabledColor = new Color(0.48f, 0.50f, 0.56f, 1f);
        button.colors = colors;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.92f, 1f, 0.42f);
        outline.effectDistance = new Vector2(1f, -1f);

        Text label = CreateText("Text", rect, 8, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.96f, 0.99f, 1f, 1f));
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 6;
        label.resizeTextMaxSize = 8;
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);

        return button;
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

    private void RefreshDepthTierSelector()
    {
        if (manager == null)
            return;

        EnsureThreatTierAccess(manager);
        int selected = manager.SelectedThreatTierNumber;
        int highest = manager.HighestUnlockedThreatTierNumber;
        ThreatTier tier = manager.SelectedThreatTier;

        if (depthTierTitleText != null)
            depthTierTitleText.text = "DEPTH";
        if (depthTierDetailText != null)
            depthTierDetailText.text = $"T{selected:00} LV{ThreatTierRules.MinLevel(tier):00}-{ThreatTierRules.MaxLevel(tier):00}";

        if (depthTierButtons == null)
            return;

        for (int i = 0; i < depthTierButtons.Length; i++)
        {
            Button button = depthTierButtons[i];
            if (button == null)
                continue;

            int buttonTier = i + ThreatTierRules.MinTier;
            bool unlocked = buttonTier <= highest;
            bool isSelected = buttonTier == selected;
            button.interactable = unlocked && !manager.IsRunActive;

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                if (isSelected)
                    image.color = new Color(0.08f, 0.25f, 0.20f, 0.98f);
                else if (unlocked)
                    image.color = new Color(0.018f, 0.032f, 0.052f, 0.96f);
                else
                    image.color = new Color(0.012f, 0.014f, 0.020f, 0.70f);
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = isSelected
                    ? new Color(0.54f, 1f, 0.72f, 0.92f)
                    : new Color(0.08f, 0.92f, 1f, unlocked ? 0.42f : 0.16f);
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = unlocked ? $"{buttonTier}F" : "--";
                label.color = isSelected
                    ? new Color(0.54f, 1f, 0.72f, 1f)
                    : (unlocked ? new Color(0.96f, 0.99f, 1f, 1f) : new Color(0.48f, 0.54f, 0.60f, 1f));
            }
        }
    }

    private void WireDepthTierButtons()
    {
        if (depthTierButtons == null)
            return;

        if (depthTierButtonActions == null || depthTierButtonActions.Length != depthTierButtons.Length)
        {
            depthTierButtonActions = new UnityEngine.Events.UnityAction[depthTierButtons.Length];
            for (int i = 0; i < depthTierButtonActions.Length; i++)
            {
                int tier = i + ThreatTierRules.MinTier;
                depthTierButtonActions[i] = () => SelectDepthTier(tier);
            }
        }

        for (int i = 0; i < depthTierButtons.Length; i++)
            WireButton(depthTierButtons[i], depthTierButtonActions[i]);
    }

    private void UnwireDepthTierButtons()
    {
        if (depthTierButtons == null || depthTierButtonActions == null)
            return;

        int count = Mathf.Min(depthTierButtons.Length, depthTierButtonActions.Length);
        for (int i = 0; i < count; i++)
            UnwireButton(depthTierButtons[i], depthTierButtonActions[i]);
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
        if (targetManager == null || targetManager.party == null)
            return;

        if (targetManager.party.Count == 0)
        {
            AlgoMonData starterSpecies = starterData != null
                ? starterData
                : FindPartyCandidate(targetManager, "Sortex");
            TryAddPartyMember(targetManager, starterSpecies, 0);
        }

        int targetCount = Mathf.Min(MinimumPlayablePartySize, GameManager.MaxPartySize);
        while (targetManager.party.Count < targetCount)
        {
            AlgoMonData reserveSpecies = FindReserveCandidate(targetManager);
            if (!TryAddPartyMember(targetManager, reserveSpecies, targetManager.party.Count))
                break;
        }
    }

    private static bool TryAddPartyMember(GameManager targetManager, AlgoMonData species, int slotIndex)
    {
        if (targetManager == null || species == null || targetManager.party == null)
            return false;
        if (targetManager.party.Count >= GameManager.MaxPartySize)
            return false;
        if (PartyContainsSpecies(targetManager, species))
            return false;

        var member = new AlgoMonInstance
        {
            data = species,
            nickname = species.codeName,
            level = StarterLevel,
            iv_Battery = Mathf.Clamp(180 - slotIndex * 8, 1, 255),
            iv_ClockSpeed = Mathf.Clamp(165 + slotIndex * 5, 1, 255),
            iv_ComputingPower = Mathf.Clamp(150 + slotIndex * 4, 1, 255),
            iv_Throughput = Mathf.Clamp(145 + slotIndex * 6, 1, 255),
            iv_Firewall = Mathf.Clamp(130 + slotIndex * 7, 1, 255),
            iv_Encryption = Mathf.Clamp(135 + slotIndex * 7, 1, 255)
        };
        member.EnsureKnownSkillsFromLearnset();
        return targetManager.AddToParty(member);
    }

    private static AlgoMonData FindReserveCandidate(GameManager targetManager)
    {
        for (int i = 0; i < PreferredReserveSpecies.Length; i++)
        {
            AlgoMonData preferred = FindPartyCandidate(targetManager, PreferredReserveSpecies[i]);
            if (preferred != null)
                return preferred;
        }

        return FindPartyCandidate(targetManager, null);
    }

    private static AlgoMonData FindPartyCandidate(GameManager targetManager, string preferredCodeName)
    {
        List<AlgoMonData> candidates = LoadPartyCandidateSpecies();
        if (!string.IsNullOrWhiteSpace(preferredCodeName))
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                AlgoMonData candidate = candidates[i];
                if (candidate != null &&
                    !PartyContainsSpecies(targetManager, candidate) &&
                    string.Equals(NormalizedCodeName(candidate), preferredCodeName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            AlgoMonData candidate = candidates[i];
            if (candidate != null && !PartyContainsSpecies(targetManager, candidate))
                return candidate;
        }

        return null;
    }

    private static List<AlgoMonData> LoadPartyCandidateSpecies()
    {
        var candidates = new List<AlgoMonData>();
        EncounterSpeciesCatalog catalog = Resources.Load<EncounterSpeciesCatalog>(EncounterSpeciesCatalogResourcePath);
        if (catalog != null)
        {
            AlgoMonData[] catalogSpecies = catalog.GetSpecies();
            for (int i = 0; i < catalogSpecies.Length; i++)
                AddUniqueCandidate(candidates, catalogSpecies[i]);
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AlgoMonData", new[] { AlgoMonAssetSearchFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AddUniqueCandidate(candidates, AssetDatabase.LoadAssetAtPath<AlgoMonData>(path));
        }
#endif

        candidates.Sort((a, b) => string.Compare(NormalizedCodeName(a), NormalizedCodeName(b), StringComparison.Ordinal));
        return candidates;
    }

    private static void AddUniqueCandidate(List<AlgoMonData> candidates, AlgoMonData candidate)
    {
        if (candidate == null)
            return;

        string codeName = NormalizedCodeName(candidate);
        for (int i = 0; i < candidates.Count; i++)
        {
            AlgoMonData existing = candidates[i];
            if (existing == candidate ||
                string.Equals(NormalizedCodeName(existing), codeName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        candidates.Add(candidate);
    }

    private static bool PartyContainsSpecies(GameManager targetManager, AlgoMonData species)
    {
        if (targetManager == null || targetManager.party == null || species == null)
            return false;

        string codeName = NormalizedCodeName(species);
        for (int i = 0; i < targetManager.party.Count; i++)
        {
            AlgoMonInstance mon = targetManager.party[i];
            if (mon == null || mon.data == null)
                continue;
            if (mon.data == species ||
                string.Equals(NormalizedCodeName(mon.data), codeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizedCodeName(AlgoMonData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.codeName))
            return string.Empty;
        return data.codeName.Trim();
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
