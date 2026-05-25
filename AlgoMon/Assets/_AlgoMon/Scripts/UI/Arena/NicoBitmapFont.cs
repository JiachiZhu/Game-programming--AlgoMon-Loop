using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public sealed class NicoBitmapFontReference
{
    [SerializeField] private string fontName;
    [SerializeField] private Texture2D atlas;
    [SerializeField] private TextAsset metrics;
    [SerializeField, Min(0.001f)] private float worldScale = 0.018f;
    [SerializeField] private Color tint = Color.white;

    private Texture2D cachedAtlas;
    private TextAsset cachedMetrics;
    private NicoBitmapFont cachedFont;

    public NicoBitmapFontReference()
    {
    }

    public NicoBitmapFontReference(string fontName, Color tint)
    {
        this.fontName = fontName;
        this.tint = tint;
    }

    public string FontName => fontName;
    public bool HasFontName => !string.IsNullOrWhiteSpace(fontName);
    public bool HasAssignedAssets => atlas != null && metrics != null;

    public bool TryGetAssignedFont(out NicoBitmapFont font)
    {
        return TryGetFont(atlas, metrics, out font);
    }

#if UNITY_EDITOR
    public bool TryGetEditorAutoFont(string rootAssetPath, out NicoBitmapFont font)
    {
        font = null;
        if (string.IsNullOrWhiteSpace(fontName) || string.IsNullOrWhiteSpace(rootAssetPath))
            return false;

        string folder = $"{rootAssetPath.TrimEnd('/')}/{fontName}";
        string atlasPath = $"{folder}/{fontName}.png";
        EnsureEditorAtlasImportSettings(atlasPath);

        Texture2D loadedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        TextAsset loadedMetrics =
            AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/{fontName}.txt") ??
            AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/{fontName}.fnt") ??
            AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/{fontName}.lua");

        return TryGetFont(loadedAtlas, loadedMetrics, out font);
    }

    private static void EnsureEditorAtlasImportSettings(string atlasPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (importer == null)
            return;

        bool dirty = false;
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            dirty = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();
    }
#endif

    private bool TryGetFont(Texture2D sourceAtlas, TextAsset sourceMetrics, out NicoBitmapFont font)
    {
        font = null;
        if (sourceAtlas == null || sourceMetrics == null)
            return false;

        if (cachedFont != null && cachedAtlas == sourceAtlas && cachedMetrics == sourceMetrics)
        {
            font = cachedFont;
            return true;
        }

        if (!NicoBitmapFont.TryCreate(sourceAtlas, sourceMetrics.text, worldScale, tint, out NicoBitmapFont parsedFont))
            return false;

        cachedAtlas = sourceAtlas;
        cachedMetrics = sourceMetrics;
        cachedFont = parsedFont;
        font = cachedFont;
        return true;
    }
}

public sealed class NicoBitmapFont
{
    private static readonly Regex LuaCommonRegex =
        new Regex(@"(lineHeight|scaleW|scaleH)\s*=\s*(-?\d+)", RegexOptions.Compiled);

    private static readonly Regex LuaCharRegex =
        new Regex(
            @"id\s*=\s*(?<id>-?\d+)\s*,\s*x\s*=\s*(?<x>-?\d+)\s*,\s*y\s*=\s*(?<y>-?\d+)\s*,\s*width\s*=\s*(?<width>-?\d+)\s*,\s*height\s*=\s*(?<height>-?\d+)\s*,\s*xoffset\s*=\s*(?<xoffset>-?\d+)\s*,\s*yoffset\s*=\s*(?<yoffset>-?\d+)\s*,\s*xadvance\s*=\s*(?<xadvance>-?\d+)",
            RegexOptions.Compiled);

    private readonly Texture2D atlas;
    private readonly Dictionary<int, Glyph> glyphs;
    private readonly Dictionary<int, Sprite> spriteCache = new Dictionary<int, Sprite>();
    private readonly int lineHeight;
    private readonly float worldScale;
    private readonly Color tint;

    private NicoBitmapFont(Texture2D atlas, Dictionary<int, Glyph> glyphs, int lineHeight, float worldScale, Color tint)
    {
        this.atlas = atlas;
        this.glyphs = glyphs;
        this.lineHeight = Mathf.Max(1, lineHeight);
        this.worldScale = Mathf.Max(0.001f, worldScale);
        this.tint = tint;
    }

    public static bool TryCreate(Texture2D atlas, string metricsText, float worldScale, Color tint, out NicoBitmapFont font)
    {
        font = null;
        if (atlas == null || string.IsNullOrWhiteSpace(metricsText))
            return false;

        if (!TryParseMetrics(metricsText, out Dictionary<int, Glyph> parsedGlyphs, out int parsedLineHeight))
            return false;

        font = new NicoBitmapFont(atlas, parsedGlyphs, parsedLineHeight, worldScale, tint);
        return true;
    }

    public List<SpriteRenderer> CreateRenderers(Transform root, string label, int sortingOrder)
    {
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        if (root == null || string.IsNullOrEmpty(label))
            return renderers;

        List<string> lines = SplitLines(label);
        List<float> lineWidths = new List<float>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
            lineWidths.Add(MeasureLine(lines[i]));

        float totalHeight = Mathf.Max(1, lines.Count) * lineHeight;
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string line = lines[lineIndex];
            float penX = -lineWidths[lineIndex] * 0.5f;
            float lineTopY = totalHeight * 0.5f - lineIndex * lineHeight;

            for (int i = 0; i < line.Length; i++)
            {
                int code = line[i];
                Glyph glyph = GlyphFor(code);
                if (glyph.Width > 0 && glyph.Height > 0)
                {
                    Sprite sprite = SpriteFor(code, glyph);
                    if (sprite != null)
                    {
                        GameObject child = new GameObject($"Glyph_{code}");
                        child.transform.SetParent(root, false);
                        child.transform.localPosition = new Vector3(
                            (penX + glyph.XOffset) * worldScale,
                            (lineTopY - glyph.YOffset) * worldScale,
                            0f);
                        child.transform.localScale = Vector3.one * worldScale;

                        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
                        renderer.sprite = sprite;
                        renderer.color = tint;
                        renderer.sortingOrder = sortingOrder;
                        renderers.Add(renderer);
                    }
                }

                penX += glyph.XAdvance;
            }
        }

        return renderers;
    }

    private static bool TryParseMetrics(
        string metricsText,
        out Dictionary<int, Glyph> parsedGlyphs,
        out int parsedLineHeight)
    {
        if (metricsText.TrimStart().StartsWith("<", System.StringComparison.Ordinal))
            return TryParseXmlMetrics(metricsText, out parsedGlyphs, out parsedLineHeight);

        return TryParseLuaMetrics(metricsText, out parsedGlyphs, out parsedLineHeight);
    }

    private static bool TryParseLuaMetrics(
        string metricsText,
        out Dictionary<int, Glyph> parsedGlyphs,
        out int parsedLineHeight)
    {
        parsedGlyphs = new Dictionary<int, Glyph>();
        parsedLineHeight = 16;

        foreach (Match match in LuaCommonRegex.Matches(metricsText))
        {
            if (match.Groups[1].Value == "lineHeight")
                parsedLineHeight = ParseInt(match.Groups[2].Value);
        }

        foreach (Match match in LuaCharRegex.Matches(metricsText))
        {
            Glyph glyph = new Glyph(
                ParseInt(match.Groups["x"].Value),
                ParseInt(match.Groups["y"].Value),
                ParseInt(match.Groups["width"].Value),
                ParseInt(match.Groups["height"].Value),
                ParseInt(match.Groups["xoffset"].Value),
                ParseInt(match.Groups["yoffset"].Value),
                ParseInt(match.Groups["xadvance"].Value));
            parsedGlyphs[ParseInt(match.Groups["id"].Value)] = glyph;
        }

        return parsedGlyphs.Count > 0;
    }

    private static bool TryParseXmlMetrics(
        string metricsText,
        out Dictionary<int, Glyph> parsedGlyphs,
        out int parsedLineHeight)
    {
        parsedGlyphs = new Dictionary<int, Glyph>();
        parsedLineHeight = 16;

        XmlDocument document = new XmlDocument();
        try
        {
            document.LoadXml(metricsText);
        }
        catch (XmlException)
        {
            return false;
        }

        XmlNode common = document.SelectSingleNode("/font/common");
        if (common != null)
            parsedLineHeight = ParseAttribute(common, "lineHeight", parsedLineHeight);

        XmlNodeList chars = document.SelectNodes("/font/chars/char");
        if (chars == null)
            return false;

        foreach (XmlNode node in chars)
        {
            int id = ParseAttribute(node, "id", -1);
            if (id < 0)
                continue;

            parsedGlyphs[id] = new Glyph(
                ParseAttribute(node, "x", 0),
                ParseAttribute(node, "y", 0),
                ParseAttribute(node, "width", 0),
                ParseAttribute(node, "height", 0),
                ParseAttribute(node, "xoffset", 0),
                ParseAttribute(node, "yoffset", 0),
                ParseAttribute(node, "xadvance", 0));
        }

        return parsedGlyphs.Count > 0;
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

    private static List<string> SplitLines(string label)
    {
        return new List<string>(label.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
    }

    private Glyph GlyphFor(int code)
    {
        if (glyphs.TryGetValue(code, out Glyph glyph))
            return glyph;
        if (glyphs.TryGetValue(32, out Glyph space))
            return space;
        return new Glyph(0, 0, 0, 0, 0, 0, lineHeight / 2);
    }

    private float MeasureLine(string line)
    {
        float width = 0f;
        for (int i = 0; i < line.Length; i++)
            width += GlyphFor(line[i]).XAdvance;
        return width;
    }

    private Sprite SpriteFor(int code, Glyph glyph)
    {
        if (spriteCache.TryGetValue(code, out Sprite sprite))
            return sprite;

        Rect rect = new Rect(
            glyph.X,
            atlas.height - glyph.Y - glyph.Height,
            glyph.Width,
            glyph.Height);
        sprite = Sprite.Create(atlas, rect, new Vector2(0f, 1f), 1f);
        spriteCache[code] = sprite;
        return sprite;
    }

    private struct Glyph
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
