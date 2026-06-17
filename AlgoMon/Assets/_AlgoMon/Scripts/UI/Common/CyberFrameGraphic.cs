using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
// Defense note: CyberFrameGraphic draws a custom UI graphic procedurally in Unity.
public sealed class CyberFrameGraphic : MaskableGraphic
{
    [Header("Frame")]
    [SerializeField] private bool drawFill = true;
    [SerializeField] private bool drawBorder = true;
    [SerializeField] private bool drawCornerAccents = true;
    [SerializeField] private float cornerCut = 18f;
    [SerializeField] private float borderThickness = 2f;
    [SerializeField] private float cornerAccentLength = 28f;
    [SerializeField] private float cornerAccentThickness = 3f;

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(0.027f, 0.067f, 0.122f, 0.88f);
    [SerializeField] private Color borderColor = new Color(0.09f, 0.85f, 1f, 0.64f);
    [SerializeField] private Color accentColor = new Color(1f, 0.23f, 0.53f, 0.72f);

    public Color FillColor
    {
        get => fillColor;
        set
        {
            fillColor = value;
            SetVerticesDirty();
        }
    }

    public Color BorderColor
    {
        get => borderColor;
        set
        {
            borderColor = value;
            SetVerticesDirty();
        }
    }

    public Color AccentColor
    {
        get => accentColor;
        set
        {
            accentColor = value;
            SetVerticesDirty();
        }
    }

    public float CornerCut
    {
        get => cornerCut;
        set
        {
            cornerCut = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public float BorderThickness
    {
        get => borderThickness;
        set
        {
            borderThickness = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

#if UNITY_EDITOR
    // Graphic.OnValidate only exists in the editor assembly; the override
    // must be compiled out of player builds.
    // Defense note: Unity lifecycle hook that runs the on validate step for this component.
    protected override void OnValidate()
    {
        base.OnValidate();
        cornerCut = Mathf.Max(0f, cornerCut);
        borderThickness = Mathf.Max(0f, borderThickness);
        cornerAccentLength = Mathf.Max(0f, cornerAccentLength);
        cornerAccentThickness = Mathf.Max(0f, cornerAccentThickness);
        SetVerticesDirty();
    }
#endif

    // Defense note: Runs the on populate mesh helper used by this script.
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float maxCut = Mathf.Min(rect.width, rect.height) * 0.45f;
        float cut = Mathf.Clamp(cornerCut, 0f, maxCut);
        float thickness = Mathf.Clamp(borderThickness, 0f, Mathf.Min(rect.width, rect.height) * 0.25f);

        Vector2[] outer = BuildOctagon(rect, cut);
        Rect innerRect = Inset(rect, thickness);
        Vector2[] inner = BuildOctagon(innerRect, Mathf.Max(0f, cut - thickness));

        if (drawFill)
            AddPolygon(vertexHelper, thickness > 0f ? inner : outer, Tinted(fillColor));

        if (drawBorder && thickness > 0f)
            AddRing(vertexHelper, outer, inner, Tinted(borderColor));

        if (drawCornerAccents)
            AddCornerAccents(vertexHelper, rect, cut, Tinted(accentColor));
    }

    // Defense note: Builds the octagon data or UI structure.
    private static Vector2[] BuildOctagon(Rect rect, float cut)
    {
        float xMin = rect.xMin;
        float xMax = rect.xMax;
        float yMin = rect.yMin;
        float yMax = rect.yMax;

        return new[]
        {
            new Vector2(xMin + cut, yMin),
            new Vector2(xMax - cut, yMin),
            new Vector2(xMax, yMin + cut),
            new Vector2(xMax, yMax - cut),
            new Vector2(xMax - cut, yMax),
            new Vector2(xMin + cut, yMax),
            new Vector2(xMin, yMax - cut),
            new Vector2(xMin, yMin + cut)
        };
    }

    // Defense note: Runs the inset helper used by this script.
    private static Rect Inset(Rect rect, float amount)
    {
        return new Rect(
            rect.xMin + amount,
            rect.yMin + amount,
            Mathf.Max(0f, rect.width - amount * 2f),
            Mathf.Max(0f, rect.height - amount * 2f));
    }

    // Defense note: Adds the corner accents entry into the target collection or UI.
    private void AddCornerAccents(VertexHelper vertexHelper, Rect rect, float cut, Color32 tint)
    {
        float length = Mathf.Clamp(cornerAccentLength, 0f, Mathf.Min(rect.width, rect.height) * 0.35f);
        float thickness = Mathf.Clamp(cornerAccentThickness, 0f, Mathf.Min(rect.width, rect.height) * 0.08f);
        if (length <= 0f || thickness <= 0f)
            return;

        float inset = Mathf.Max(borderThickness, 1f);
        float left = rect.xMin + inset;
        float right = rect.xMax - inset;
        float bottom = rect.yMin + inset;
        float top = rect.yMax - inset;
        float cutInset = Mathf.Max(0f, cut + inset);

        AddRect(vertexHelper, new Rect(left + cutInset, top - thickness, length, thickness), tint);
        AddRect(vertexHelper, new Rect(left, top - cutInset - length, thickness, length), tint);
        AddRect(vertexHelper, new Rect(right - cutInset - length, top - thickness, length, thickness), tint);
        AddRect(vertexHelper, new Rect(right - thickness, top - cutInset - length, thickness, length), tint);
        AddRect(vertexHelper, new Rect(left + cutInset, bottom, length, thickness), tint);
        AddRect(vertexHelper, new Rect(left, bottom + cutInset, thickness, length), tint);
        AddRect(vertexHelper, new Rect(right - cutInset - length, bottom, length, thickness), tint);
        AddRect(vertexHelper, new Rect(right - thickness, bottom + cutInset, thickness, length), tint);
    }

    // Defense note: Adds the polygon entry into the target collection or UI.
    private static void AddPolygon(VertexHelper vertexHelper, Vector2[] points, Color32 tint)
    {
        int start = vertexHelper.currentVertCount;
        Vector2 center = Vector2.zero;
        for (int i = 0; i < points.Length; i++)
            center += points[i];
        center /= points.Length;

        vertexHelper.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i < points.Length; i++)
            vertexHelper.AddVert(points[i], tint, Vector2.zero);

        for (int i = 0; i < points.Length; i++)
        {
            int next = i == points.Length - 1 ? 0 : i + 1;
            vertexHelper.AddTriangle(start, start + 1 + i, start + 1 + next);
        }
    }

    // Defense note: Adds the ring entry into the target collection or UI.
    private static void AddRing(VertexHelper vertexHelper, Vector2[] outer, Vector2[] inner, Color32 tint)
    {
        int start = vertexHelper.currentVertCount;
        for (int i = 0; i < outer.Length; i++)
            vertexHelper.AddVert(outer[i], tint, Vector2.zero);
        for (int i = 0; i < inner.Length; i++)
            vertexHelper.AddVert(inner[i], tint, Vector2.zero);

        int count = outer.Length;
        for (int i = 0; i < count; i++)
        {
            int next = i == count - 1 ? 0 : i + 1;
            int outerA = start + i;
            int outerB = start + next;
            int innerA = start + count + i;
            int innerB = start + count + next;

            vertexHelper.AddTriangle(outerA, outerB, innerB);
            vertexHelper.AddTriangle(outerA, innerB, innerA);
        }
    }

    // Defense note: Adds the rect entry into the target collection or UI.
    private static void AddRect(VertexHelper vertexHelper, Rect rect, Color32 tint)
    {
        int start = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMin), tint, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMax), tint, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMax), tint, Vector2.zero);
        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start, start + 2, start + 3);
    }

    // Defense note: Runs the tinted helper used by this script.
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
