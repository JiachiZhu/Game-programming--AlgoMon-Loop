using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public sealed class CyberTextStyle : MonoBehaviour
{
    [SerializeField] private CyberUiColorRole textRole = CyberUiColorRole.TextPrimary;
    [SerializeField] private bool uppercase = true;
    [SerializeField] private bool useOutline = true;
    [SerializeField] private bool useShadow = true;
    [SerializeField] private Vector2 outlineDistance = new Vector2(1f, -1f);
    [SerializeField] private Vector2 shadowDistance = new Vector2(2f, -2f);

    private Text text;
    private string lastSourceText;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Update()
    {
        if (text != null && uppercase && text.text != lastSourceText)
            ApplyUppercase();
    }

    public void Apply()
    {
        if (text == null)
            text = GetComponent<Text>();
        if (text == null)
            return;

        text.color = CyberUiTheme.ColorFor(textRole);
        ApplyUppercase();
        ConfigureOutline();
        ConfigureShadow();
    }

    private void ApplyUppercase()
    {
        if (text == null)
            return;

        if (uppercase && !string.IsNullOrEmpty(text.text))
            text.text = text.text.ToUpperInvariant();
        lastSourceText = text.text;
    }

    private void ConfigureOutline()
    {
        Outline outline = GetComponent<Outline>();
        if (!useOutline)
        {
            if (outline != null)
                outline.enabled = false;
            return;
        }

        if (outline == null)
            outline = gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = CyberUiTheme.WithAlpha(CyberUiTheme.Background, 0.9f);
        outline.effectDistance = outlineDistance;
        outline.useGraphicAlpha = true;
    }

    private void ConfigureShadow()
    {
        Shadow shadow = FindShadow();
        if (!useShadow)
        {
            if (shadow != null)
                shadow.enabled = false;
            return;
        }

        if (shadow == null)
            shadow = gameObject.AddComponent<Shadow>();
        shadow.enabled = true;
        shadow.effectColor = CyberUiTheme.WithAlpha(Color.black, 0.82f);
        shadow.effectDistance = shadowDistance;
        shadow.useGraphicAlpha = true;
    }

    private Shadow FindShadow()
    {
        Shadow[] shadows = GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            Shadow candidate = shadows[i];
            if (candidate != null && candidate.GetType() == typeof(Shadow))
                return candidate;
        }

        return null;
    }
}
