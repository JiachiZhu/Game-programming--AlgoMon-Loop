using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight runtime radar (spider) chart drawn directly into a UI mesh.
/// Feed it normalized values (0..1) via SetValues; it renders guide rings,
/// spokes, a filled value polygon and its outline. Used by the Payload
/// unit inspector for the 6-axis stat readout.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
// Defense note: RadarChartGraphic draws a custom UI graphic procedurally in Unity.
public class RadarChartGraphic : Graphic
{
    [SerializeField] private int axisCount = 6;
    [SerializeField] private float startAngleDeg = 90f;
    [Range(0.4f, 1f)] [SerializeField] private float fillScale = 0.82f;
    [SerializeField] private int gridRings = 2;
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private Color gridColor = new Color(0.33f, 0.72f, 0.88f, 0.30f);
    [SerializeField] private Color spokeColor = new Color(0.33f, 0.72f, 0.88f, 0.40f);
    [SerializeField] private Color outlineColor = new Color(0.36f, 1f, 1f, 0.95f);
    [SerializeField] private Color areaColor = new Color(0.20f, 0.90f, 1f, 0.32f);

    private float[] values;

    public int AxisCount => axisCount;
    public float FillScale => fillScale;

    // Defense note: Updates the values state or visual value.
    public void SetValues(float[] v)
    {
        values = v;
        SetVerticesDirty();
    }

    // Defense note: Runs the axis direction helper used by this script.
    public Vector2 AxisDirection(int index)
    {
        float ang = (startAngleDeg - index * (360f / axisCount)) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
    }

    // Defense note: Runs the on populate mesh helper used by this script.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (axisCount < 3)
            return;

        Rect r = GetPixelAdjustedRect();
        Vector2 center = r.center;
        float radius = Mathf.Min(r.width, r.height) * 0.5f * fillScale;
        if (radius <= 1f)
            return;

        Vector2[] dirs = new Vector2[axisCount];
        for (int i = 0; i < axisCount; i++)
            dirs[i] = AxisDirection(i);

        for (int ring = 1; ring <= gridRings + 1; ring++)
        {
            float t = (float)ring / (gridRings + 1);
            for (int i = 0; i < axisCount; i++)
                AddLine(vh, center + dirs[i] * radius * t, center + dirs[(i + 1) % axisCount] * radius * t, gridColor);
        }

        for (int i = 0; i < axisCount; i++)
            AddLine(vh, center, center + dirs[i] * radius, spokeColor);

        if (values != null && values.Length >= axisCount)
        {
            int baseIdx = vh.currentVertCount;
            AddVert(vh, center, areaColor);
            for (int i = 0; i < axisCount; i++)
                AddVert(vh, center + dirs[i] * radius * Mathf.Clamp01(values[i]), areaColor);

            for (int i = 0; i < axisCount; i++)
                vh.AddTriangle(baseIdx, baseIdx + 1 + i, baseIdx + 1 + ((i + 1) % axisCount));

            for (int i = 0; i < axisCount; i++)
            {
                Vector2 p0 = center + dirs[i] * radius * Mathf.Clamp01(values[i]);
                Vector2 p1 = center + dirs[(i + 1) % axisCount] * radius * Mathf.Clamp01(values[(i + 1) % axisCount]);
                AddLine(vh, p0, p1, outlineColor);
            }
        }
    }

    // Defense note: Adds the vert entry into the target collection or UI.
    private void AddVert(VertexHelper vh, Vector2 pos, Color c)
    {
        UIVertex v = UIVertex.simpleVert;
        v.color = c;
        v.position = pos;
        vh.AddVert(v);
    }

    // Defense note: Adds the line entry into the target collection or UI.
    private void AddLine(VertexHelper vh, Vector2 a, Vector2 b, Color color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 1e-5f)
            return;
        dir.Normalize();
        Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);
        int idx = vh.currentVertCount;
        AddVert(vh, a - normal, color);
        AddVert(vh, a + normal, color);
        AddVert(vh, b + normal, color);
        AddVert(vh, b - normal, color);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
}
