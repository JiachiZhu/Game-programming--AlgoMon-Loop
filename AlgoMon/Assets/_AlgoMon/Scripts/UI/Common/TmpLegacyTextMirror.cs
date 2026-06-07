using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mirrors a legacy UnityEngine.UI.Text onto a TextMeshPro (SDF) graphic so the
// rendered text stays crisp at any resolution/scale. The legacy Text remains the
// data source (its content/color), but it is hidden and the TMP copy is drawn.
[DisallowMultipleComponent]
public sealed class TmpLegacyTextMirror : MonoBehaviour
{
    [SerializeField] private Text sourceText;
    [SerializeField] private TextMeshProUGUI target;
    [SerializeField] private bool hideSourceText = true;

    public Text SourceText
    {
        get => sourceText;
        set
        {
            sourceText = value;
            Sync();
        }
    }

    public bool HideSourceText
    {
        get => hideSourceText;
        set
        {
            hideSourceText = value;
            Sync();
        }
    }

    private void Reset()
    {
        target = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        Sync();
    }

    private void LateUpdate()
    {
        Sync();
    }

    public void Sync()
    {
        if (target == null)
            target = GetComponent<TextMeshProUGUI>();
        if (sourceText == null || target == null)
            return;

        string value = sourceText.text ?? string.Empty;
        if (target.text != value)
            target.text = value;
        if (target.color != sourceText.color)
            target.color = sourceText.color;
        target.raycastTarget = false;
        if (hideSourceText && sourceText.enabled)
            sourceText.enabled = false;
    }
}
