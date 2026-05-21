using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minimal run result screen for the Sprint 3 loop.
/// GameManager owns the outcome state; this scene only presents it and returns
/// to MainTerminal when the player confirms.
/// </summary>
[DisallowMultipleComponent]
public class RunResultController : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text titleText;
    [SerializeField] private Text resultText;
    [SerializeField] private Text detailText;
    [SerializeField] private Text footerText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Image accentFill;

    private GameManager manager;
    private float startTime;
    private Font defaultFont;

    private void Awake()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        manager = GameManager.EnsureInstance();
        EnsureSceneObjects();
    }

    private void OnEnable()
    {
        if (continueButton == null)
            return;

        continueButton.onClick.RemoveListener(ReturnToTerminal);
        continueButton.onClick.AddListener(ReturnToTerminal);
    }

    private void OnDisable()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(ReturnToTerminal);
    }

    private void Start()
    {
        startTime = Time.unscaledTime;
        Refresh();
    }

    private void Update()
    {
        if (accentFill != null)
            accentFill.fillAmount = 0.55f + Mathf.PingPong(Time.unscaledTime * 0.1f, 0.35f);

        if (footerText != null)
            footerText.text = $"> RESULT_BUFFER: {FormatOutcomeId()} | PAYLOAD: {PayloadCount()} | T+{Mathf.FloorToInt(Time.unscaledTime - startTime):000}";
    }

    private void Refresh()
    {
        RunOutcome outcome = manager != null ? manager.pendingRunOutcome : RunOutcome.None;
        bool victory = outcome == RunOutcome.Victory;

        if (titleText != null)
            titleText.text = outcome == RunOutcome.None ? "RUN RESULT" : "RUN COMPLETE";

        if (resultText != null)
            resultText.text = victory ? "VICTORY" : outcome == RunOutcome.Defeat ? "DEFEAT" : "NO RESULT";

        if (detailText != null)
        {
            if (outcome == RunOutcome.None)
            {
                detailText.text = "No active result packet was found.";
            }
            else
            {
                string nodeId = string.IsNullOrWhiteSpace(manager.completedRunNodeId)
                    ? "UNKNOWN"
                    : manager.completedRunNodeId;
                detailText.text =
                    $"RUN SEED: {manager.completedRunSeed}\n" +
                    $"FINAL NODE: {nodeId} [{manager.completedRunNodeType}]\n" +
                    $"NODES VISITED: {manager.completedRunVisitedCount}\n" +
                    $"PAYLOAD SIZE: {PayloadCount()}";
            }
        }
    }

    private void ReturnToTerminal()
    {
        manager = manager != null ? manager : GameManager.EnsureInstance();
        if (manager != null)
            manager.ClearRunResult();

        GameManager.GoTo(GameScene.MainTerminal);
    }

    private string FormatOutcomeId()
    {
        if (manager == null)
            return "NONE";

        return manager.pendingRunOutcome.ToString().ToUpperInvariant();
    }

    private int PayloadCount()
    {
        return manager != null && manager.payload != null ? manager.payload.Count : 0;
    }

    private void EnsureSceneObjects()
    {
        EnsureEventSystem();

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            canvas = CreateCanvas();

        RectTransform root = canvas.GetComponent<RectTransform>();

        Image background = CreateImage("Background", root, new Color(0.006f, 0.009f, 0.02f, 1f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        Image topLine = CreateImage("TopLine", root, new Color(0.11f, 0.75f, 0.88f, 0.75f));
        topLine.rectTransform.anchorMin = new Vector2(0.08f, 0.82f);
        topLine.rectTransform.anchorMax = new Vector2(0.92f, 0.82f);
        topLine.rectTransform.sizeDelta = new Vector2(0f, 3f);
        topLine.rectTransform.anchoredPosition = Vector2.zero;

        accentFill = accentFill != null
            ? accentFill
            : CreateImage("SignalFill", root, new Color(0.96f, 0.32f, 0.52f, 0.45f));
        accentFill.type = Image.Type.Filled;
        accentFill.fillMethod = Image.FillMethod.Horizontal;
        accentFill.rectTransform.anchorMin = new Vector2(0.08f, 0.165f);
        accentFill.rectTransform.anchorMax = new Vector2(0.92f, 0.165f);
        accentFill.rectTransform.sizeDelta = new Vector2(0f, 5f);
        accentFill.rectTransform.anchoredPosition = Vector2.zero;

        titleText = titleText != null ? titleText : CreateText("Title", root, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        titleText.rectTransform.anchorMin = new Vector2(0.08f, 0.72f);
        titleText.rectTransform.anchorMax = new Vector2(0.92f, 0.82f);
        titleText.rectTransform.offsetMin = Vector2.zero;
        titleText.rectTransform.offsetMax = Vector2.zero;
        titleText.color = new Color(0.62f, 0.92f, 1f, 1f);

        resultText = resultText != null ? resultText : CreateText("Outcome", root, 72, FontStyle.Bold, TextAnchor.MiddleLeft);
        resultText.rectTransform.anchorMin = new Vector2(0.08f, 0.49f);
        resultText.rectTransform.anchorMax = new Vector2(0.92f, 0.70f);
        resultText.rectTransform.offsetMin = Vector2.zero;
        resultText.rectTransform.offsetMax = Vector2.zero;
        resultText.color = new Color(1f, 0.88f, 0.62f, 1f);

        detailText = detailText != null ? detailText : CreateText("Details", root, 18, FontStyle.Normal, TextAnchor.UpperLeft);
        detailText.rectTransform.anchorMin = new Vector2(0.08f, 0.30f);
        detailText.rectTransform.anchorMax = new Vector2(0.70f, 0.50f);
        detailText.rectTransform.offsetMin = Vector2.zero;
        detailText.rectTransform.offsetMax = Vector2.zero;
        detailText.color = new Color(0.82f, 0.90f, 0.95f, 1f);

        continueButton = continueButton != null ? continueButton : CreateButton("ContinueButton", root, "RETURN");
        RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.08f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.28f, 0.27f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        footerText = footerText != null ? footerText : CreateText("Footer", root, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        footerText.rectTransform.anchorMin = new Vector2(0.08f, 0.08f);
        footerText.rectTransform.anchorMax = new Vector2(0.92f, 0.14f);
        footerText.rectTransform.offsetMin = Vector2.zero;
        footerText.rectTransform.offsetMax = Vector2.zero;
        footerText.color = new Color(0.46f, 0.78f, 0.86f, 1f);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas_RunResult", typeof(RectTransform));
        Canvas newCanvas = canvasObject.AddComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        return newCanvas;
    }

    private Text CreateText(string objectName, Transform parent, int size, FontStyle style, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = defaultFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Button CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.32f, 0.38f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 1f, 0.96f, 1f);
        colors.pressedColor = new Color(1f, 0.62f, 0.72f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text labelText = CreateText("Text", buttonObject.transform, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        labelText.text = label;
        labelText.color = new Color(0.92f, 1f, 0.98f, 1f);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = Vector2.zero;
        labelText.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

}
