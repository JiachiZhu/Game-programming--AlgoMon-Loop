using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CyberBitmapTextGraphic : MaskableGraphic
{
    private static readonly Regex LuaCommonRegex =
        new Regex(@"(lineHeight|scaleW|scaleH)\s*=\s*(-?\d+)", RegexOptions.Compiled);

    private static readonly Regex LuaCharRegex =
        new Regex(
            @"id\s*=\s*(?<id>-?\d+)\s*,\s*x\s*=\s*(?<x>-?\d+)\s*,\s*y\s*=\s*(?<y>-?\d+)\s*,\s*width\s*=\s*(?<width>-?\d+)\s*,\s*height\s*=\s*(?<height>-?\d+)\s*,\s*xoffset\s*=\s*(?<xoffset>-?\d+)\s*,\s*yoffset\s*=\s*(?<yoffset>-?\d+)\s*,\s*xadvance\s*=\s*(?<xadvance>-?\d+)",
            RegexOptions.Compiled);

    [SerializeField] private Texture2D atlas;
    [SerializeField] private TextAsset metrics;
    [SerializeField, TextArea(1, 4)] private string text = "TEXT";
    [SerializeField, Min(0.05f)] private float fontScale = 1f;
    [SerializeField, Min(0.2f)] private float lineSpacing = 1f;
    [SerializeField] private float letterSpacing;
    [SerializeField] private TextAnchor alignment = TextAnchor.MiddleCenter;
    [SerializeField] private bool uppercase = true;
    [SerializeField] private Text sourceText;
    [SerializeField] private bool hideSourceText;

    private readonly Dictionary<int, Glyph> glyphs = new Dictionary<int, Glyph>();
    private Texture2D parsedAtlas;
    private TextAsset parsedMetrics;
    private int lineHeight = 16;

    public override Texture mainTexture => atlas != null ? atlas : s_WhiteTexture;

    public Texture2D Atlas
    {
        get => atlas;
        set
        {
            atlas = value;
            SetMaterialDirty();
            SetVerticesDirty();
        }
    }

    public TextAsset Metrics
    {
        get => metrics;
        set
        {
            metrics = value;
            parsedMetrics = null;
            SetVerticesDirty();
        }
    }

    public string Text
    {
        get => text;
        set
        {
            text = value;
            SetVerticesDirty();
        }
    }

    public float FontScale
    {
        get => fontScale;
        set
        {
            fontScale = Mathf.Max(0.05f, value);
            SetVerticesDirty();
        }
    }

    public TextAnchor Alignment
    {
        get => alignment;
        set
        {
            alignment = value;
            SetVerticesDirty();
        }
    }

    public float LineSpacing
    {
        get => lineSpacing;
        set
        {
            lineSpacing = Mathf.Max(0.2f, value);
            SetVerticesDirty();
        }
    }

    public float LetterSpacing
    {
        get => letterSpacing;
        set
        {
            letterSpacing = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public Text SourceText
    {
        get => sourceText;
        set
        {
            sourceText = value;
            SyncFromSourceText();
        }
    }

    public bool HideSourceText
    {
        get => hideSourceText;
        set
        {
            hideSourceText = value;
            SyncFromSourceText();
        }
    }

    public void SyncFromSourceText()
    {
        if (sourceText == null)
            return;

        string sourceValue = sourceText.text ?? string.Empty;
        if (text != sourceValue)
            Text = sourceValue;

        if (color != sourceText.color)
            color = sourceText.color;
        raycastTarget = false;
        if (hideSourceText && sourceText.enabled)
            sourceText.enabled = false;
    }

    private void LateUpdate()
    {
        SyncFromSourceText();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        fontScale = Mathf.Max(0.05f, fontScale);
        lineSpacing = Mathf.Max(0.2f, lineSpacing);
        letterSpacing = Mathf.Max(0f, letterSpacing);
        SetMaterialDirty();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (atlas == null || metrics == null || string.IsNullOrEmpty(text))
            return;

        EnsureParsed();
        if (glyphs.Count == 0)
            return;

        string renderText = uppercase ? text.ToUpperInvariant() : text;
        string[] lines = renderText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Max(0.05f, fontScale);
        float lineStep = lineHeight * scale * lineSpacing;
        float blockHeight = lineHeight * scale + Mathf.Max(0, lines.Length - 1) * lineStep;
        float topY = TopFor(rect, blockHeight);
        Color32 tint = color;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            float lineWidth = MeasureLine(line, scale);
            float penX = StartXFor(rect, lineWidth);
            float lineTopY = topY - lineIndex * lineStep;

            for (int i = 0; i < line.Length; i++)
            {
                Glyph glyph = GlyphFor(line[i]);
                if (glyph.Width > 0 && glyph.Height > 0)
                    AddGlyph(vertexHelper, glyph, penX, lineTopY, scale, tint);

                penX += glyph.XAdvance * scale + letterSpacing;
            }
        }
    }

    private void EnsureParsed()
    {
        if (parsedAtlas == atlas && parsedMetrics == metrics)
            return;

        glyphs.Clear();
        lineHeight = 16;
        parsedAtlas = atlas;
        parsedMetrics = metrics;

        if (metrics == null || string.IsNullOrWhiteSpace(metrics.text))
            return;

        string source = metrics.text;
        if (source.TrimStart().StartsWith("<", System.StringComparison.Ordinal))
            ParseXml(source);
        else
            ParseLua(source);
    }

    private void ParseLua(string source)
    {
        foreach (Match match in LuaCommonRegex.Matches(source))
        {
            if (match.Groups[1].Value == "lineHeight")
                lineHeight = Mathf.Max(1, ParseInt(match.Groups[2].Value));
        }

        foreach (Match match in LuaCharRegex.Matches(source))
        {
            int id = ParseInt(match.Groups["id"].Value);
            glyphs[id] = new Glyph(
                ParseInt(match.Groups["x"].Value),
                ParseInt(match.Groups["y"].Value),
                ParseInt(match.Groups["width"].Value),
                ParseInt(match.Groups["height"].Value),
                ParseInt(match.Groups["xoffset"].Value),
                ParseInt(match.Groups["yoffset"].Value),
                ParseInt(match.Groups["xadvance"].Value));
        }
    }

    private void ParseXml(string source)
    {
        XmlDocument document = new XmlDocument();
        try
        {
            document.LoadXml(source);
        }
        catch (XmlException)
        {
            return;
        }

        XmlNode common = document.SelectSingleNode("/font/common");
        if (common != null)
            lineHeight = Mathf.Max(1, ParseAttribute(common, "lineHeight", lineHeight));

        XmlNodeList chars = document.SelectNodes("/font/chars/char");
        if (chars == null)
            return;

        foreach (XmlNode node in chars)
        {
            int id = ParseAttribute(node, "id", -1);
            if (id < 0)
                continue;

            glyphs[id] = new Glyph(
                ParseAttribute(node, "x", 0),
                ParseAttribute(node, "y", 0),
                ParseAttribute(node, "width", 0),
                ParseAttribute(node, "height", 0),
                ParseAttribute(node, "xoffset", 0),
                ParseAttribute(node, "yoffset", 0),
                ParseAttribute(node, "xadvance", lineHeight / 2));
        }
    }

    private void AddGlyph(VertexHelper vertexHelper, Glyph glyph, float penX, float lineTopY, float scale, Color32 tint)
    {
        float xMin = penX + glyph.XOffset * scale;
        float xMax = xMin + glyph.Width * scale;
        float yMax = lineTopY - glyph.YOffset * scale;
        float yMin = yMax - glyph.Height * scale;

        float uMin = glyph.X / (float)atlas.width;
        float uMax = (glyph.X + glyph.Width) / (float)atlas.width;
        float vMax = 1f - glyph.Y / (float)atlas.height;
        float vMin = 1f - (glyph.Y + glyph.Height) / (float)atlas.height;

        int start = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector2(xMin, yMin), tint, new Vector2(uMin, vMin));
        vertexHelper.AddVert(new Vector2(xMax, yMin), tint, new Vector2(uMax, vMin));
        vertexHelper.AddVert(new Vector2(xMax, yMax), tint, new Vector2(uMax, vMax));
        vertexHelper.AddVert(new Vector2(xMin, yMax), tint, new Vector2(uMin, vMax));
        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start, start + 2, start + 3);
    }

    private float MeasureLine(string line, float scale)
    {
        float width = 0f;
        for (int i = 0; i < line.Length; i++)
            width += GlyphFor(line[i]).XAdvance * scale + letterSpacing;
        if (line.Length > 0)
            width -= letterSpacing;
        return Mathf.Max(0f, width);
    }

    private Glyph GlyphFor(int code)
    {
        if (glyphs.TryGetValue(code, out Glyph glyph))
            return glyph;
        if (glyphs.TryGetValue(32, out Glyph space))
            return space;
        return new Glyph(0, 0, 0, 0, 0, 0, lineHeight / 2);
    }

    private float StartXFor(Rect rect, float lineWidth)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft:
            case TextAnchor.MiddleLeft:
            case TextAnchor.LowerLeft:
                return rect.xMin;
            case TextAnchor.UpperRight:
            case TextAnchor.MiddleRight:
            case TextAnchor.LowerRight:
                return rect.xMax - lineWidth;
            case TextAnchor.UpperCenter:
            case TextAnchor.MiddleCenter:
            case TextAnchor.LowerCenter:
            default:
                return rect.center.x - lineWidth * 0.5f;
        }
    }

    private float TopFor(Rect rect, float blockHeight)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft:
            case TextAnchor.UpperCenter:
            case TextAnchor.UpperRight:
                return rect.yMax;
            case TextAnchor.LowerLeft:
            case TextAnchor.LowerCenter:
            case TextAnchor.LowerRight:
                return rect.yMin + blockHeight;
            case TextAnchor.MiddleLeft:
            case TextAnchor.MiddleCenter:
            case TextAnchor.MiddleRight:
            default:
                return rect.center.y + blockHeight * 0.5f;
        }
    }

    private static int ParseAttribute(XmlNode node, string attributeName, int fallback)
    {
        XmlAttribute attribute = node.Attributes?[attributeName];
        if (attribute == null)
            return fallback;

        return int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    private static int ParseInt(string rawValue)
    {
        return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
    }

    private readonly struct Glyph
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;
        public readonly int XOffset;
        public readonly int YOffset;
        public readonly int XAdvance;

        public Glyph(int x, int y, int width, int height, int xOffset, int yOffset, int xAdvance)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            XOffset = xOffset;
            YOffset = yOffset;
            XAdvance = xAdvance;
        }
    }
}
