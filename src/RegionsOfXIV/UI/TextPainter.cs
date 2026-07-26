using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RegionsOfXIV.UI;

internal static class TextPainter
{
    // ImGui has no text outline, so the stroke is built by stamping the glyph run
    // around the origin before drawing the fill on top. 8-way gives a cleaner edge
    // than 4-way.
    private static readonly Vector2[] StrokeOffsets =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    ];

    public static void DrawStroked(
        ImDrawListPtr drawList,
        Vector2 position,
        string text,
        uint fillColor,
        uint strokeColor,
        float strokeDistance = 1f)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (strokeDistance > 0f)
        {
            foreach (var offset in StrokeOffsets)
                drawList.AddText(position + (offset * strokeDistance), strokeColor, text);
        }

        drawList.AddText(position, fillColor, text);
    }

    public static void DrawStrokedCentered(
        ImDrawListPtr drawList,
        float centerX,
        float top,
        string text,
        uint fillColor,
        uint strokeColor,
        float strokeDistance = 1f)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var size = ImGui.CalcTextSize(text);
        DrawStroked(drawList, new Vector2(centerX - (size.X / 2f), top), text, fillColor, strokeColor, strokeDistance);
    }

    // Drawn as two halves so it can animate outward from the centre, and drawn
    // before the header text so descenders and serifs are not overdrawn.
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
}
