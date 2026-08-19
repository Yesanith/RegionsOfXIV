using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RegionsOfXIV.UI;

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
        uint fillColor,
        uint strokeColor,
        float strokeDistance)
    {
        if (text.IsEmpty)
            return;

        if (strokeDistance > 0f)
        {
            foreach (var offset in StrokeOffsets)
                drawList.AddText(position + (offset * strokeDistance), strokeColor, text);
        }

        drawList.AddText(position, fillColor, text);
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
        uint fillColor,
        uint strokeColor,
        float strokeDistance)
    {
        for (var i = 0; i < text.Length; i++)
            DrawGlyph(drawList, positions[i], top, text[i], fillColor, strokeColor, strokeDistance);
    }

    public static void DrawUnderline(
        ImDrawListPtr drawList,
        float centerX,
        float y,
        float fullWidth,
        float progress,
        uint fillColor,
        uint borderColor,
        float thickness = 2f)
    {
        var width = fullWidth * float.Clamp(progress, 0f, 1f);
        if (width <= 0f)
            return;

        var half = width / 2f;

        drawList.AddRectFilled(
            new Vector2(centerX - half - 1, y - 1),
            new Vector2(centerX + half + 1, y + thickness + 1),
            borderColor);

        drawList.AddRectFilled(
            new Vector2(centerX - half, y),
            new Vector2(centerX + half, y + thickness),
            fillColor);
    }

    public static float[] GlyphPositions(string text, float centerX, float tracking, out float width)
    {
        var xs = new float[text.Length];
        for (var i = 0; i < text.Length; i++)
            xs[i] = ImGui.CalcTextSize(text[..i]).X + (tracking * i);

        width = RunWidth(text, tracking);

        var left = centerX - (width / 2f);
        for (var i = 0; i < xs.Length; i++)
            xs[i] += left;

        return xs;
    }

    public static float Lerp(float from, float to, float t) => from + ((to - from) * t);

    public static uint Packed(Vector4 color, float alpha) =>
        ImGui.ColorConvertFloat4ToU32(color with { W = color.W * alpha });

    public static void DrawGlyph(
        ImDrawListPtr drawList, float x, float y, char glyph, uint fill, uint stroke, float strokeDistance)
    {
        if (glyph == ' ')
            return;

        Span<char> one = stackalloc char[1];
        one[0] = glyph;

        DrawStroked(drawList, new Vector2(x, y), one, fill, stroke, strokeDistance);
    }
}
