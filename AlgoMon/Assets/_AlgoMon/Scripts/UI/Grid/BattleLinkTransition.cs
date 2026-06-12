/*
Script Audit:
- Purpose: Plays the Grid-to-Arena encounter lock transition before battle scene loading.
- Attached GameObject: Runtime-created full-screen overlay object named BattleLinkTransition.
- Main responsibilities: Build a temporary cyber target-lock HUD, block input during handoff, prepare battle data, and load TheArena.
- Important variables: canvasGroup, targetRoot, scanBand, encounterLabel, riskLabel, lockFrames, dataBars.
- Inputs: Encounter/risk labels plus prepare/load callbacks from GameManager.OnNodeSelected.
- Outputs or effects: Shows a short transition overlay and invokes the provided Arena handoff callback.
- AI/tutorial/template assistance: AI was used to help author this transition script; final behavior was checked against the existing GridLinkTransition pattern.
- Testing notes: Click WILD/HACKER/ELITE/BOSS nodes from TheGrid and confirm the lock animation plays before TheArena appears.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleLinkTransition : MonoBehaviour
{
    private const float WarmupSeconds = 0.22f;
    private const float LockSeconds = 0.78f;
    private const float HandoffSeconds = 0.20f;
    private const float ExitSeconds = 0.42f;
    private const int SortingOrder = 32010;
    private const string FontResourcePath = "Fonts/NicoBold-Regular";

    private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

    public static bool IsActive { get; private set; }

    private readonly List<Image> scanlines = new List<Image>();
    private readonly List<Image> dataBars = new List<Image>();
    private readonly List<RectTransform> dataBarRects = new List<RectTransform>();
    private readonly List<CyberFrameGraphic> lockFrames = new List<CyberFrameGraphic>();
    private readonly List<Image> lockSegments = new List<Image>();

    private CanvasGroup canvasGroup;
    private RectTransform root;
    private RectTransform targetRoot;
    private RectTransform scanBand;
    private RectTransform progressFillRect;
    private Image veilImage;
    private Image targetCoreImage;
    private Image warningBandImage;
    private Image progressTrackImage;
    private Image progressFillImage;
    private Text titleText;
    private Text statusText;
    private Text nodeText;
    private Text riskText;
    private Text progressText;
    private Font transitionFont;
    private string encounterLabel;
    private string riskLabel;

    public static void Play(string encounter, string risk, Action prepareBattle, Action loadArena)
    {
        if (IsActive)
            return;

        GameObject transitionObject = new GameObject(
            "BattleLinkTransition",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(BattleLinkTransition));
        DontDestroyOnLoad(transitionObject);

        BattleLinkTransition transition = transitionObject.GetComponent<BattleLinkTransition>();
        transition.encounterLabel = string.IsNullOrWhiteSpace(encounter) ? "ENCOUNTER" : encounter.ToUpperInvariant();
        transition.riskLabel = string.IsNullOrWhiteSpace(risk) ? "RISK UNKNOWN" : risk.ToUpperInvariant();
        transition.BuildVisualTree();
        transition.Begin(prepareBattle, loadArena);
    }

    private void Begin(Action prepareBattle, Action loadArena)
    {
        IsActive = true;
        StartCoroutine(TransitionRoutine(prepareBattle, loadArena));
    }

    private void OnDestroy()
    {
        IsActive = false;
    }

    private IEnumerator TransitionRoutine(Action prepareBattle, Action loadArena)
    {
        bool prepared = false;
        float startTime = Time.unscaledTime;
        float preLoadTotal = WarmupSeconds + LockSeconds + HandoffSeconds;

        while (Time.unscaledTime - startTime < preLoadTotal)
        {
            float elapsed = Time.unscaledTime - startTime;
            float cover = Smooth01(Mathf.Clamp01(elapsed / WarmupSeconds));
            float lockProgress = Mathf.Clamp01((elapsed - WarmupSeconds * 0.40f) / LockSeconds);
            float displayedProgress = Mathf.Clamp01(elapsed / preLoadTotal);

            canvasGroup.alpha = cover;
            UpdateVisuals(elapsed, lockProgress, displayedProgress, false);

            if (!prepared && elapsed >= WarmupSeconds)
            {
                prepared = true;
                SetStatus("ENCOUNTER PACKAGE BUILDING");
                InvokeSafely(prepareBattle, "Battle encounter preparation failed.");
            }

            yield return null;
        }

        // Let the bar visibly reach 100% before the synchronous scene load, so the
        // load hitch hides behind a completed frame instead of freezing the bar
        // mid-fill.
        canvasGroup.alpha = 1f;
        UpdateVisuals(preLoadTotal, 1f, 1f, false);
        SetStatus("ARENA HANDOFF ACCEPTED");
        yield return null;
        yield return null;

        InvokeSafely(loadArena, "Arena scene handoff failed.");
        yield return null;

        float exitStartTime = Time.unscaledTime;
        while (Time.unscaledTime - exitStartTime < ExitSeconds)
        {
            float exitElapsed = Time.unscaledTime - exitStartTime;
            float exitProgress = Mathf.Clamp01(exitElapsed / ExitSeconds);
            float fade = 1f - Smooth01(exitProgress);

            canvasGroup.alpha = fade;
            UpdateVisuals(preLoadTotal + exitElapsed, 1f, Mathf.Lerp(0.94f, 1f, exitProgress), true);
            if (targetRoot != null)
                targetRoot.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.22f, Smooth01(exitProgress));

            yield return null;
        }

        IsActive = false;
        Destroy(gameObject);
    }

    private void InvokeSafely(Action action, string errorMessage)
    {
        if (action == null)
            return;

        try
        {
            action.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError(errorMessage);
            Debug.LogException(exception);
            SetStatus("BATTLE LINK ERROR - CHECK CONSOLE");
        }
    }

    private void BuildVisualTree()
    {
        transitionFont = ResolveFont();

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        veilImage = CreateImage("BattleVeil", root, new Color(0.018f, 0.004f, 0.010f, 0.98f), true);
        Stretch(veilImage.rectTransform);

        BuildScanlines();
        BuildDataBars();
        BuildTargetLock();
        BuildTextAndProgress();
    }

    private void BuildScanlines()
    {
        for (int i = 0; i < 20; i++)
        {
            float alpha = i % 4 == 0 ? 0.060f : 0.026f;
            Image line = CreateImage("BattleScanline_" + i, root, new Color(1f, 0.22f, 0.48f, alpha), false);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 22f + i * 36f);
            rect.sizeDelta = new Vector2(0f, i % 4 == 0 ? 2f : 1f);
            scanlines.Add(line);
        }

        scanBand = CreateRect("TargetingScanSweep", root);
        scanBand.anchorMin = new Vector2(0f, 0.5f);
        scanBand.anchorMax = new Vector2(1f, 0.5f);
        scanBand.pivot = new Vector2(0.5f, 0.5f);
        scanBand.sizeDelta = new Vector2(0f, 62f);
        scanBand.anchoredPosition = new Vector2(0f, -420f);

        CreateSweepLine("SweepLead", scanBand, 0f, 3f, 0.28f);
        CreateSweepLine("SweepEchoUpper", scanBand, 16f, 1f, 0.08f);
        CreateSweepLine("SweepEchoLower", scanBand, -15f, 1f, 0.07f);
        CreateSweepDash("SweepDashLeft", scanBand, new Vector2(-420f, 9f), new Vector2(132f, 1f), 0.18f);
        CreateSweepDash("SweepDashMid", scanBand, new Vector2(-36f, -7f), new Vector2(96f, 1f), 0.14f);
        CreateSweepDash("SweepDashRight", scanBand, new Vector2(374f, 12f), new Vector2(156f, 1f), 0.16f);
    }

    private Image CreateSweepLine(string objectName, RectTransform parent, float y, float height, float alpha)
    {
        Image line = CreateImage(objectName, parent, new Color(1f, 0.30f, 0.58f, alpha), false);
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
        return line;
    }

    private Image CreateSweepDash(string objectName, RectTransform parent, Vector2 position, Vector2 size, float alpha)
    {
        Image dash = CreateImage(objectName, parent, new Color(1f, 0.70f, 0.36f, alpha), false);
        RectTransform rect = dash.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return dash;
    }

    private void BuildDataBars()
    {
        for (int i = 0; i < 18; i++)
        {
            Image bar = CreateImage("ThreatDataStream_" + i, root, Color.white, false);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Lerp(76f, 188f, (i % 6) / 5f), i % 3 == 0 ? 3f : 2f);
            rect.anchoredPosition = new Vector2(-720f + i * 84f, -240f + (i % 7) * 80f);
            bar.color = StreamColor(i, 0.12f);
            dataBars.Add(bar);
            dataBarRects.Add(rect);
        }
    }

    private void BuildTargetLock()
    {
        targetRoot = CreateRect("EncounterTargetLock", root);
        targetRoot.anchorMin = new Vector2(0.5f, 0.5f);
        targetRoot.anchorMax = new Vector2(0.5f, 0.5f);
        targetRoot.pivot = new Vector2(0.5f, 0.5f);
        targetRoot.sizeDelta = new Vector2(880f, 420f);
        targetRoot.anchoredPosition = new Vector2(0f, -10f);
        targetRoot.localScale = Vector3.one * 0.92f;

        Vector2[] frameSizes =
        {
            new Vector2(360f, 214f),
            new Vector2(540f, 310f),
            new Vector2(740f, 420f)
        };

        for (int i = 0; i < frameSizes.Length; i++)
        {
            RectTransform rect = CreateRect("LockFrame_" + i, targetRoot);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = frameSizes[i];
            rect.anchoredPosition = Vector2.zero;

            CyberFrameGraphic frame = rect.gameObject.AddComponent<CyberFrameGraphic>();
            frame.raycastTarget = false;
            frame.FillColor = new Color(0.080f, 0.006f, 0.016f, 0.05f);
            frame.BorderColor = new Color(1f, 0.23f, 0.46f, 0.28f);
            frame.AccentColor = new Color(1f, 0.68f, 0.28f, 0.24f);
            frame.CornerCut = 32f;
            frame.BorderThickness = 2f;
            lockFrames.Add(frame);
        }

        targetCoreImage = CreateImage("TargetCore", targetRoot, new Color(0.32f, 0.018f, 0.038f, 0.28f), false);
        RectTransform coreRect = targetCoreImage.rectTransform;
        coreRect.anchorMin = new Vector2(0.5f, 0.5f);
        coreRect.anchorMax = new Vector2(0.5f, 0.5f);
        coreRect.pivot = new Vector2(0.5f, 0.5f);
        coreRect.anchoredPosition = Vector2.zero;
        coreRect.sizeDelta = new Vector2(250f, 112f);
        Outline coreOutline = targetCoreImage.gameObject.AddComponent<Outline>();
        coreOutline.effectColor = new Color(1f, 0.28f, 0.50f, 0.70f);
        coreOutline.effectDistance = new Vector2(2f, -2f);

        warningBandImage = CreateImage("WarningBand", targetRoot, new Color(1f, 0.18f, 0.35f, 0.18f), false);
        RectTransform warningRect = warningBandImage.rectTransform;
        warningRect.anchorMin = new Vector2(0.5f, 0.5f);
        warningRect.anchorMax = new Vector2(0.5f, 0.5f);
        warningRect.pivot = new Vector2(0.5f, 0.5f);
        warningRect.anchoredPosition = new Vector2(0f, -118f);
        warningRect.sizeDelta = new Vector2(520f, 3f);

        CreateLockSegment("LockTop", new Vector2(0f, 150f), new Vector2(280f, 3f));
        CreateLockSegment("LockBottom", new Vector2(0f, -150f), new Vector2(280f, 3f));
        CreateLockSegment("LockLeft", new Vector2(-220f, 0f), new Vector2(3f, 188f));
        CreateLockSegment("LockRight", new Vector2(220f, 0f), new Vector2(3f, 188f));
        CreateLockSegment("CrossHorizontal", Vector2.zero, new Vector2(640f, 1f));
        CreateLockSegment("CrossVertical", Vector2.zero, new Vector2(1f, 300f));

        nodeText = CreateText("EncounterNode", targetRoot, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        nodeText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        nodeText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        nodeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        nodeText.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        nodeText.rectTransform.sizeDelta = new Vector2(380f, 42f);
        nodeText.text = encounterLabel;
        nodeText.color = new Color(1f, 0.92f, 0.86f, 0f);

        riskText = CreateText("RiskLabel", targetRoot, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        riskText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        riskText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        riskText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        riskText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
        riskText.rectTransform.sizeDelta = new Vector2(420f, 26f);
        riskText.text = riskLabel;
        riskText.color = new Color(1f, 0.58f, 0.32f, 0f);
    }

    private void CreateLockSegment(string objectName, Vector2 position, Vector2 size)
    {
        Image segment = CreateImage(objectName, targetRoot, new Color(1f, 0.26f, 0.46f, 0.28f), false);
        RectTransform rect = segment.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        lockSegments.Add(segment);
    }

    private void BuildTextAndProgress()
    {
        titleText = CreateText("Title", root, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        titleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        titleText.rectTransform.sizeDelta = new Vector2(760f, 42f);
        titleText.text = "BATTLE LINK // ENCOUNTER LOCK";
        titleText.color = new Color(1f, 0.92f, 0.86f, 0f);

        statusText = CreateText("Status", root, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        statusText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, 118f);
        statusText.rectTransform.sizeDelta = new Vector2(740f, 28f);
        statusText.text = "TARGETING ROUTE NODE";
        statusText.color = new Color(1f, 0.64f, 0.42f, 0f);

        RectTransform progressTrack = CreateRect("ProgressTrack", root);
        progressTrack.anchorMin = new Vector2(0.5f, 0f);
        progressTrack.anchorMax = new Vector2(0.5f, 0f);
        progressTrack.pivot = new Vector2(0.5f, 0.5f);
        progressTrack.anchoredPosition = new Vector2(0f, 90f);
        progressTrack.sizeDelta = new Vector2(560f, 18f);
        progressTrackImage = progressTrack.gameObject.AddComponent<Image>();
        progressTrackImage.color = new Color(0.70f, 0.10f, 0.18f, 0.64f);
        progressTrackImage.raycastTarget = false;

        progressFillImage = CreateImage("ProgressFill", progressTrack, new Color(1f, 0.34f, 0.50f, 0.92f), false);
        progressFillRect = progressFillImage.rectTransform;
        progressFillRect.anchorMin = new Vector2(0.025f, 0.30f);
        progressFillRect.anchorMax = new Vector2(0.025f, 0.70f);
        progressFillRect.pivot = new Vector2(0f, 0.5f);
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = Vector2.zero;

        progressText = CreateText("ProgressText", root, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        progressText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        progressText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        progressText.rectTransform.pivot = new Vector2(0.5f, 0f);
        progressText.rectTransform.anchoredPosition = new Vector2(0f, 62f);
        progressText.rectTransform.sizeDelta = new Vector2(520f, 24f);
        progressText.text = "000%";
        progressText.color = new Color(1f, 0.75f, 0.48f, 0f);
    }

    private void UpdateVisuals(float elapsed, float lockProgress, float displayedProgress, bool exiting)
    {
        float pulse = 0.5f + Mathf.Sin(elapsed * 11.5f) * 0.5f;
        float slowPulse = 0.5f + Mathf.Sin(elapsed * 4.2f) * 0.5f;
        float textAlpha = Smooth01(Mathf.Clamp01(elapsed / 0.28f));
        float lockReveal = Smooth01(lockProgress);
        float exitBoost = exiting ? 1f : 0f;

        if (veilImage != null)
            veilImage.color = new Color(0.018f, 0.004f, 0.010f, Mathf.Lerp(0.86f, 0.98f, textAlpha));

        if (titleText != null)
            titleText.color = new Color(1f, 0.84f + pulse * 0.14f, 0.74f, textAlpha);
        if (statusText != null)
            statusText.color = new Color(1f, 0.52f + pulse * 0.22f, 0.32f, textAlpha);
        if (nodeText != null)
            nodeText.color = new Color(1f, 0.92f, 0.86f, lockReveal);
        if (riskText != null)
            riskText.color = new Color(1f, 0.48f + pulse * 0.20f, 0.30f, lockReveal);
        if (progressText != null)
        {
            progressText.color = new Color(1f, 0.76f, 0.48f, textAlpha);
            progressText.text = Mathf.RoundToInt(Mathf.Clamp01(displayedProgress) * 100f).ToString("000") + "%";
        }

        if (progressFillRect != null)
            progressFillRect.anchorMax = new Vector2(Mathf.Lerp(0.025f, 0.975f, Mathf.Clamp01(displayedProgress)), 0.70f);
        if (progressTrackImage != null)
            progressTrackImage.color = Color.Lerp(new Color(0.42f, 0.08f, 0.13f, textAlpha * 0.52f), new Color(1f, 0.30f, 0.46f, textAlpha * 0.78f), pulse);
        if (progressFillImage != null)
            progressFillImage.color = Color.Lerp(new Color(1f, 0.26f, 0.46f, 0.86f), new Color(1f, 0.72f, 0.34f, 1f), pulse);

        if (scanBand != null)
        {
            float scan = Mathf.Repeat(elapsed * 0.72f, 1f);
            scanBand.anchoredPosition = new Vector2(0f, Mathf.Lerp(-430f, 430f, scan));
        }

        UpdateScanlines(elapsed, textAlpha);
        UpdateDataBars(elapsed, lockReveal);
        UpdateTargetLock(elapsed, lockReveal, pulse, slowPulse, exitBoost);
    }

    private void UpdateScanlines(float elapsed, float alpha)
    {
        for (int i = 0; i < scanlines.Count; i++)
        {
            Image line = scanlines[i];
            if (line == null)
                continue;

            float flicker = 0.5f + Mathf.Sin(elapsed * (5.6f + i * 0.11f) + i) * 0.5f;
            line.color = new Color(1f, 0.22f, 0.48f, alpha * Mathf.Lerp(0.012f, 0.066f, flicker));
        }
    }

    private void UpdateDataBars(float elapsed, float lockReveal)
    {
        for (int i = 0; i < dataBarRects.Count; i++)
        {
            RectTransform rect = dataBarRects[i];
            Image image = i < dataBars.Count ? dataBars[i] : null;
            if (rect == null || image == null)
                continue;

            float phase = Mathf.Repeat(elapsed * (0.35f + i * 0.013f) + i * 0.083f, 1f);
            rect.anchoredPosition = new Vector2(Mathf.Lerp(-760f, 760f, phase), rect.anchoredPosition.y);
            float alpha = Mathf.Lerp(0.04f, 0.24f, lockReveal) * (0.62f + Mathf.Sin((elapsed + i) * 5.8f) * 0.24f);
            image.color = StreamColor(i, alpha);
        }
    }

    private void UpdateTargetLock(float elapsed, float lockReveal, float pulse, float slowPulse, float exitBoost)
    {
        if (targetRoot != null && exitBoost <= 0f)
            targetRoot.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.05f + slowPulse * 0.018f, lockReveal);

        for (int i = 0; i < lockFrames.Count; i++)
        {
            CyberFrameGraphic frame = lockFrames[i];
            if (frame == null)
                continue;

            float cycle = Mathf.Repeat(elapsed * (0.22f + i * 0.03f) + i * 0.20f, 1f);
            frame.color = new Color(1f, 1f, 1f, lockReveal * (0.42f - cycle * 0.20f));
            frame.BorderColor = new Color(1f, 0.18f + pulse * 0.20f, 0.40f, lockReveal * (0.34f + pulse * 0.28f));
            frame.AccentColor = new Color(1f, 0.66f, 0.24f, lockReveal * (0.20f + pulse * 0.20f));
            frame.transform.localScale = Vector3.one * Mathf.Lerp(0.84f, 1.08f + i * 0.04f, cycle);
            frame.transform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * elapsed * (5f + i * 2f));
        }

        for (int i = 0; i < lockSegments.Count; i++)
        {
            Image segment = lockSegments[i];
            if (segment == null)
                continue;

            float segmentPulse = 0.5f + Mathf.Sin(elapsed * 10f + i * 0.8f) * 0.5f;
            segment.color = new Color(1f, 0.22f + segmentPulse * 0.24f, 0.42f, lockReveal * (0.18f + segmentPulse * 0.42f));
        }

        if (targetCoreImage != null)
            targetCoreImage.color = new Color(0.36f + pulse * 0.10f, 0.018f, 0.045f, lockReveal * (0.20f + pulse * 0.24f));
        if (warningBandImage != null)
        {
            warningBandImage.color = new Color(1f, 0.18f + pulse * 0.28f, 0.35f, lockReveal * (0.12f + pulse * 0.32f));
            warningBandImage.rectTransform.localScale = new Vector3(Mathf.Lerp(0.3f, 1.0f, lockReveal), 1f + pulse * 0.7f, 1f);
        }
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private static Color StreamColor(int index, float alpha)
    {
        return index % 4 == 0
            ? new Color(1f, 0.70f, 0.26f, alpha)
            : new Color(1f, 0.22f, 0.48f, alpha);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private Font ResolveFont()
    {
        Font font = Resources.Load<Font>(FontResourcePath);
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private Image CreateImage(string objectName, Transform parent, Color color, bool raycastTarget)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = transitionFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
