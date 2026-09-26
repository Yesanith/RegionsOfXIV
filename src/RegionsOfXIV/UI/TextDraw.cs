using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Text through the draw list rather than through ImGui items, so a label can be placed exactly
// and never reaches a format function. Everything here takes the string as it is.
internal static class TextDraw
{
    private const string Ellipsis = "…";
    private const int TruncateCacheSize = 16;

    private static readonly TruncatedText[] TruncateCache = new TruncatedText[TruncateCacheSize];

    private static int TruncateCacheNext;

    private readonly record struct TruncatedText(string Source, float MaxWidth, float FontSize, string Result);

    public static string Upper(string text) => text.ToUpper(Loc.Culture);

    public static Vector2 Measure(string text) => ImGui.CalcTextSize(text);

    public static Vector2 MeasureWrapped(string text, float wrapWidth) => ImGui.CalcTextSize(text, false, wrapWidth);

    public static void At(string text, Vector2 position, Vector4 color)
        => ImGui.GetWindowDrawList().AddText(position, Paint.Col(color), text);

    public static void Wrapped(string text, Vector2 position, float wrapWidth, Vector4 color)
        => ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), position, Paint.Col(color), text, wrapWidth);

    // A paragraph that takes part in the layout, so the cursor moves past it.
    public static void Paragraph(string text, Vector4 color, float width = 0f)
    {
        var wrapWidth = width > 0f ? width : ImGui.GetContentRegionAvail().X;
        var position = ImGui.GetCursorScreenPos();
        var size = MeasureWrapped(text, wrapWidth);
        Wrapped(text, position, wrapWidth, color);
        ImGui.Dummy(new Vector2(wrapWidth, size.Y));
    }

    public static Vector2 IconSize(FontAwesomeIcon icon)
    {
        using (Fonts.PushIcon())
        {
            return Measure(icon.ToIconString());
        }
    }

    public static void Icon(FontAwesomeIcon icon, Vector2 position, Vector4 color)
    {
        using (Fonts.PushIcon())
        {
            At(icon.ToIconString(), position, color);
        }
    }

    public static void IconCentered(FontAwesomeIcon icon, Vector2 center, Vector4 color)
    {
        using (Fonts.PushIcon())
        {
            var glyph = icon.ToIconString();
            var size = Measure(glyph);
            At(glyph, center - (size * 0.5f), color);
        }
    }

    // An animated glyph grows through the draw list at an explicit size, so the window font scale
    // is never touched.
    public static void IconCentered(FontAwesomeIcon icon, Vector2 center, Vector4 color, float sizeScale)
    {
        using (Fonts.PushIcon())
        {
            var glyph = icon.ToIconString();
            var size = Measure(glyph) * sizeScale;
            ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize() * sizeScale, center - (size * 0.5f), Paint.Col(color), glyph, 0f);
        }
    }

    // A line that overflows its slot overflows on every frame it is drawn, so its cut is cached,
    // and prefixes are measured in place, so finding the cut allocates only the result.
    public static string Truncate(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
        {
            return string.Empty;
        }

        if (Measure(text).X <= maxWidth)
        {
            return text;
        }

        var fontSize = ImGui.GetFontSize();
        for (var index = 0; index < TruncateCache.Length; index++)
        {
            var cached = TruncateCache[index];
            if (ReferenceEquals(cached.Source, text) && cached.MaxWidth == maxWidth && cached.FontSize == fontSize)
            {
                return cached.Result;
            }
        }

        var result = Cut(text, maxWidth);
        TruncateCache[TruncateCacheNext] = new TruncatedText(text, maxWidth, fontSize, result);
        TruncateCacheNext = (TruncateCacheNext + 1) % TruncateCacheSize;
        return result;
    }

    private static string Cut(string text, float maxWidth)
    {
        var budget = maxWidth - Measure(Ellipsis).X;
        if (budget <= 0f)
        {
            return Ellipsis;
        }

        var low = 1;
        var high = text.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text.AsSpan(0, middle)).X <= budget)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return string.Concat(text.AsSpan(0, low), Ellipsis);
    }

    public static void SmallCaps(string label, Vector2 position, Vector4 color)
    {
        using (Fonts.PushCaption())
        {
            At(Upper(label), position, color);
        }
    }

    public static Vector2 SmallCapsSize(string label)
    {
        using (Fonts.PushCaption())
        {
            return Measure(Upper(label));
        }
    }

    public static float LineHeight() => ImGui.GetTextLineHeight();
}
