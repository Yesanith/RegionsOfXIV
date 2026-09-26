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

// A soft halo behind the letters, under the outline, spread this many pixels out.
internal readonly record struct Glow(uint Color, float Spread)
{
    public static readonly Glow None = default;

    public bool IsVisible => (this.Color >> 24) != 0u && this.Spread > 0f;
}

// FillBottom is a second fill colour for the foot of each glyph, or zero for a flat fill.
internal readonly record struct Ink(
    uint Fill, uint Stroke, float StrokeDistance, Shadow Shadow, Glow Glow = default, uint FillBottom = 0u)
{
    public bool HasStroke => this.StrokeDistance > 0f && (this.Stroke >> 24) != 0u;

    public bool HasFill => (this.Fill >> 24) != 0u;

    public bool HasGradient => (this.FillBottom >> 24) != 0u;
}

// Text is drawn a glyph at a time rather than handed to ImGui as a string, because the motion
// and decode effects move each letter independently. Everything here is built around that.
internal static class GlyphPainter
{
    private static readonly Vector2[] StrokeOffsets =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    ];

    private const int GlowLayers = 3;

    // Shadow first, then the glow, then the outline ring, then the fill on top. Each layer is
    // skipped when it would be invisible: a line costs up to forty-three draw calls per glyph
    // with everything on, so the cheap checks are worth making.
    public static void DrawStroked(
        ImDrawListPtr drawList,
        Vector2 position,
        ReadOnlySpan<char> text,
        in Ink ink,
        float scale = 1f)
    {
        if (text.IsEmpty || (!ink.HasFill && !ink.HasStroke && !ink.Shadow.IsVisible && !ink.Glow.IsVisible))
            return;

        var font = ImGui.GetFont();
        var size = ImGui.GetFontSize() * scale;

        if (ink.Shadow.IsVisible)
            DrawShadow(drawList, font, size, position, text, ink.Shadow);

        if (ink.Glow.IsVisible)
            DrawGlow(drawList, font, size, position, text, ink.Glow);

        if (ink.HasStroke)
        {
            foreach (var offset in StrokeOffsets)
                drawList.AddText(font, size, position + (offset * ink.StrokeDistance), ink.Stroke, text);
        }

        if (!ink.HasFill)
            return;

        var start = drawList.VtxBuffer.Size;
        drawList.AddText(font, size, position, ink.Fill, text);

        // Shaded top to bottom over the line's own height, so every glyph in a run takes the same
        // gradient whatever its own shape.
        if (ink.HasGradient)
        {
            ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(
                drawList, start, drawList.VtxBuffer.Size,
                position, position + new Vector2(0f, size),
                ink.Fill, ink.FillBottom);
        }
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

    // Rings of the glow colour at growing distances and falling alpha. Three rings of eight is
    // enough to read as soft at notification sizes without a blur pass, which the draw list
    // cannot do.
    private static void DrawGlow(
        ImDrawListPtr drawList,
        ImFontPtr font,
        float size,
        Vector2 position,
        ReadOnlySpan<char> text,
        in Glow glow)
    {
        var alpha = (glow.Color >> 24) / 255f;

        for (var layer = GlowLayers; layer >= 1; layer--)
        {
            var distance = glow.Spread * layer / GlowLayers;
            var layerAlpha = alpha * (1f - ((layer - 1f) / GlowLayers)) / 4f;
            var color = (glow.Color & 0x00FFFFFFu) | ((uint)(layerAlpha * 255f) << 24);

            foreach (var offset in StrokeOffsets)
                drawList.AddText(font, size, position + (offset * distance), color, text);
        }
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

    // The game's fonts carry kerning pairs, so a glyph's position depends on the one before it.
    // Measuring the prefix up to and including this glyph, then subtracting the glyph's own width,
    // recovers a position that keeps the pair spacing; measuring glyphs individually loses one
    // kern per letter and leaves the line loose and off centre. This is what made the Q in
    // "Quest Accepted" and "Entrance Square" sit wrong.
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
