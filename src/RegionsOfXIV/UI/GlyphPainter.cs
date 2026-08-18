using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RegionsOfXIV.UI;

// Drawing text the way this plugin needs it drawn: outlined, centred on a point,
// and placeable glyph by glyph so a line can be animated or letter-spaced.
//
// ImGui offers none of that. It draws a string at a position in one colour, on
// the font's own advances, with no outline and no letter spacing — so everything
// here is built out of repeated AddText calls and arithmetic.
//
// Lives apart from NotificationOverlay because none of it knows what a
// notification is. It was inlined there once, when it was three methods; it is
// no longer three methods.
internal static class GlyphPainter
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
        float strokeDistance)
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

    // Width of a glyph run under the font currently pushed, tracking included.
    // The gap belongs *between* glyphs, so N of them carry N-1 gaps and the run
    // ends at the last glyph's own edge rather than a space past it — otherwise a
    // tracked line would sit visibly left of centre.
    public static float RunWidth(string text, float tracking)
    {
        var width = ImGui.CalcTextSize(text).X;

        return tracking > 0f && text.Length > 1
            ? width + (tracking * (text.Length - 1))
            : width;
    }

    // The whole line in one run, centred. Untracked only: ImGui lays a string out
    // on the font's own advances and has no letter spacing to ask for, so this is
    // the path for when none was asked for. Also the cheapest by a wide margin —
    // one AddText per stroke stamp rather than one per glyph.
    public static void DrawCentered(
        ImDrawListPtr drawList,
        float centerX,
        float top,
        string text,
        uint fillColor,
        uint strokeColor,
        float strokeDistance)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var left = centerX - (ImGui.CalcTextSize(text).X / 2f);

        DrawStroked(drawList, new Vector2(left, top), text, fillColor, strokeColor, strokeDistance);
    }

    // A line placed glyph by glyph, at positions worked out once and remembered.
    //
    // Splitting the run does not change how it renders: ImGui applies no kerning
    // pairs, so a glyph draws the same alone as it does in company.
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

    // Absolute X of every glyph under whatever font is pushed, with the given
    // tracking, centred on centerX.
    //
    // Walking prefixes rather than accumulating single glyphs so this agrees to
    // the pixel with DrawCentered, which hands ImGui the whole string: both come
    // out of CalcTextSize on the same characters. That
    // agreement is what lets an animated reveal hand over to the plain run at
    // full progress without the line shifting.
    public static float[] GlyphPositions(string text, float centerX, float tracking)
    {
        var xs = new float[text.Length];
        for (var i = 0; i < text.Length; i++)
            xs[i] = ImGui.CalcTextSize(text[..i]).X + (tracking * i);

        var left = centerX - (RunWidth(text, tracking) / 2f);
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

        DrawStroked(drawList, new Vector2(x, y), Single(glyph), fill, stroke, strokeDistance);
    }

    // ImDrawList.AddText wants a string, and a char is not one. Building that
    // string per glyph per frame is an allocation for every letter of every line
    // sixty times a second, so the ones that recur are made once.
    //
    // ASCII only: it covers every English, French and German place name in the
    // game and the whole of the Eorzean cipher, and a table over every char would
    // be 64k entries to serve the handful of Japanese lines that miss.
    private static readonly string[] AsciiGlyphs = BuildAsciiGlyphs();

    private static string[] BuildAsciiGlyphs()
    {
        var glyphs = new string[128];
        for (var i = 0; i < glyphs.Length; i++)
            glyphs[i] = ((char)i).ToString();

        return glyphs;
    }

    private static string Single(char glyph) =>
        glyph < AsciiGlyphs.Length ? AsciiGlyphs[glyph] : glyph.ToString();
}
