using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CyberScanlineGraphic : MaskableGraphic
{
    [SerializeField, Min(1f)] private float lineSpacing = 6f;
    [SerializeField, Min(0.25f)] private float lineThickness = 1f;
    [SerializeField] private float lineOffset;
    [SerializeField] private Color lineColor = new Color(0.10f, 0.86f, 1f, 0.10f);
    [SerializeField] private bool drawVerticalTicks = true;
    [SerializeField, Min(8f)] private float tickSpacing = 48f;
    [SerializeField, Min(1f)] private float tickWidth = 2f;
    [SerializeField] private Color tickColor = new Color(1f, 0.23f, 0.53f, 0.12f);

    public float LineOffset
    {
        get => lineOffset;
        set
        {
            lineOffset = value;
            SetVerticesDirty();
        }
    }

    public Color LineColor
    {
        get => lineColor;
        set
        {
            lineColor = value;
            SetVerticesDirty();
        }
    }

    public Color TickColor
    {
        get => tickColor;
        set
        {
            tickColor = value;
            SetVerticesDirty();
        }
    }

    public float LineSpacing
    {
        get => lineSpacing;
        set
        {
            lineSpacing = Mathf.Max(1f, value);
            SetVerticesDirty();
        }
    }

    public float LineThickness
    {
        get => lineThickness;
        set
        {
            lineThickness = Mathf.Max(0.25f, value);
            SetVerticesDirty();
        }
    }

    public bool DrawVerticalTicks
    {
        get => drawVerticalTicks;
        set
        {
            drawVerticalTicks = value;
            SetVerticesDirty();
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        lineSpacing = Mathf.Max(1f, lineSpacing);
        lineThickness = Mathf.Max(0.25f, lineThickness);
        tickSpacing = Mathf.Max(8f, tickSpacing);
        tickWidth = Mathf.Max(1f, tickWidth);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        Color32 lineTint = Tinted(lineColor);
        float wrappedOffset = Mathf.Repeat(lineOffset, lineSpacing);
        for (float y = rect.yMin + wrappedOffset; y <= rect.yMax; y += lineSpacing)
        {
            AddRect(
                vertexHelper,
                new Rect(rect.xMin, y, rect.width, Mathf.Min(lineThickness, rect.yMax - y)),
                lineTint);
        }

        if (!drawVerticalTicks)
            return;

        Color32 tickTint = Tinted(tickColor);
        for (float x = rect.xMin + tickSpacing * 0.5f; x <= rect.xMax; x += tickSpacing)
        {
            AddRect(
                vertexHelper,
                new Rect(x, rect.yMin, Mathf.Min(tickWidth, rect.xMax - x), rect.height),
                tickTint);
        }
    }

    private static void AddRect(VertexHelper vertexHelper, Rect rect, Color32 tint)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        int start = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMin), tint, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMax), tint, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMax), tint, Vector2.zero);
        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start, start + 2, start + 3);
    }

    private Color32 Tinted(Color source)
    {
        Color graphicColor = color;
        return new Color(
            source.r * graphicColor.r,
            source.g * graphicColor.g,
            source.b * graphicColor.b,
            source.a * graphicColor.a);
    }
}
