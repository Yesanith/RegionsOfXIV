using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RegionsOfXIV.UI;

internal readonly record struct Shadow(uint Color, Vector2 Offset, float Spread)
{
    public static readonly Shadow None = default;

    public bool IsVisible =>
        (this.Color >> 24) != 0u && (this.Offset != Vector2.Zero || this.Spread > 0f);
}

internal readonly record struct Ink(uint Fill, uint Stroke, float StrokeDistance, Shadow Shadow);

internal static class GlyphPainter
{
    private static readonly Vector2[] StrokeOffsets =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    ];

    public static void DrawStroked(
        ImDrawListPtr drawList,
        Vector2 position,
        ReadOnlySpan<char> text,
        in Ink ink,
        float scale = 1f)
    {
        if (text.IsEmpty)
            return;

        var font = ImGui.GetFont();
        var size = ImGui.GetFontSize() * scale;

        if (ink.Shadow.IsVisible)
            DrawShadow(drawList, font, size, position, text, ink.Shadow);

        if (ink.StrokeDistance > 0f)
        {
            foreach (var offset in StrokeOffsets)
                drawList.AddText(font, size, position + (offset * ink.StrokeDistance), ink.Stroke, text);
        }

        drawList.AddText(font, size, position, ink.Fill, text);
    }

    private static void DrawShadow(
        ImDrawListPtr drawList,
        ImFontPtr font,
        float size,
        Vector2 position,
        ReadOnlySpan<char> text,
        in Shadow shadow)
    {
        var at = position + shadow.Offset;

        if (shadow.Spread > 0f)
        {
            foreach (var offset in StrokeOffsets)
                drawList.AddText(font, size, at + (offset * shadow.Spread), shadow.Color, text);
        }

        drawList.AddText(font, size, at, shadow.Color, text);
    }

    public static float RunWidth(string text, float tracking)
    {
        var width = ImGui.CalcTextSize(text).X;

        return tracking > 0f && text.Length > 1
            ? width + (tracking * (text.Length - 1))
            : width;
    }

    public static void DrawRun(
        ImDrawListPtr drawList,
        float[] positions,
        string text,
        float top,
        in Ink ink,
        float scale = 1f)
    {
        for (var i = 0; i < text.Length; i++)
            DrawGlyph(drawList, positions[i], top, text[i], ink, scale);
    }

    public static void DrawUnderline(
        ImDrawListPtr drawList,
        float centerX,
        float y,
        float fullWidth,
        float progress,
        in Ink ink,
        float thickness = 2f)
    {
        var width = fullWidth * float.Clamp(progress, 0f, 1f);
        if (width <= 0f)
            return;

        var half = width / 2f;

        var topLeft = new Vector2(centerX - half, y);
        var bottomRight = new Vector2(centerX + half, y + thickness);

        if (ink.Shadow.IsVisible)
        {
            var drop = ink.Shadow.Offset + new Vector2(ink.Shadow.Spread);

            drawList.AddRectFilled(topLeft + drop, bottomRight + drop, ink.Shadow.Color);
        }

        drawList.AddRectFilled(
            topLeft - Vector2.One,
            bottomRight + Vector2.One,
            ink.Stroke);

        drawList.AddRectFilled(topLeft, bottomRight, ink.Fill);
    }

    public static float[] GlyphPositions(
        string text, float centerX, float tracking, float scale, out float width)
    {
        var xs = new float[text.Length];
        for (var i = 0; i < text.Length; i++)
            xs[i] = (Offset(text, i) + (tracking * i)) * scale;

        width = RunWidth(text, tracking) * scale;

        var left = centerX - (width / 2f);
        for (var i = 0; i < xs.Length; i++)
            xs[i] += left;

        return xs;
    }

    private static float Offset(string text, int index)
    {
        if (index == 0)
            return 0f;

        var through = ImGui.CalcTextSize(text[..(index + 1)]).X;

        return through - ImGui.CalcTextSize(text.AsSpan(index, 1)).X;
    }

    public static float Lerp(float from, float to, float t) => from + ((to - from) * t);

    public static uint Packed(Vector4 color, float alpha) =>
        ImGui.ColorConvertFloat4ToU32(color with { W = color.W * alpha });

    public static void DrawGlyph(
        ImDrawListPtr drawList, float x, float y, char glyph, in Ink ink, float scale = 1f)
    {
        if (glyph == ' ')
            return;

        Span<char> one = stackalloc char[1];
        one[0] = glyph;

        DrawStroked(drawList, new Vector2(x, y), one, ink, scale);
    }
}
