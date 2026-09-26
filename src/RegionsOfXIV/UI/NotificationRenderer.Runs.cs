using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.UI;

// The painting half of the renderer: given a line and where it goes, put the glyphs on screen.
//
// Four routes through here, in priority order. Leaving with a departure motion: every glyph is
// placed by GlyphAnimator running backwards over the fade. Motion still running: every glyph is
// placed by GlyphAnimator. Motion done but decode still running: glyphs cross-fade from their
// Eorzean stand-ins to the real letters. Neither running: the line is drawn in one piece where
// letter spacing and the palette allow it, which is far cheaper than a call per glyph.
internal sealed partial class NotificationRenderer
{
    private static readonly Vector4 EmberColor = new(1f, 0.55f, 0.15f, 1f);

    private const float WidthBudget = 0.94f;

    private readonly record struct FontPair(IFontHandle Plain, IFontHandle? Eorzean);

    private readonly record struct TextRun(
        FontPair Fonts,
        string Text,
        Vector4 Fill,
        Vector4 Stroke,
        float CenterX,
        float Top,
        bool Name = false);

    // A colour per glyph for the name line while a palette is on, and nothing for any other line.
    private readonly record struct FillPlay(TextPalette Palette, PaletteMotion Motion, float Speed)
    {
        public static readonly FillPlay None = default;

        public bool Active => this.Palette != TextPalette.None;

        public Vector4 Colour(int index, int count) =>
            TextPalettes.Colour(this.Palette, TextPalettes.Phase(this.Motion, index, count, this.Speed));
    }

    private FillPlay PlayFor(in TextRun run) =>
        run.Name ? new FillPlay(config.Palette, config.PaletteMotion, config.PaletteSpeed) : FillPlay.None;

    private void DrawAnimatedText(
        AreaNotification notification, ImDrawListPtr drawList, in TextRun run, float scale)
    {
        var opacity = notification.Opacity;
        var decoding = config.DecodeEffectEnabled && run.Fonts.Eorzean != null;
        var play = PlayFor(run);

        if (notification.IsFadingOut
            && GlyphAnimator.DepartureMotion(config.Departure, config.Motion) is { } leaving)
        {
            DrawMovingRun(
                drawList,
                run,
                run.Fonts.Plain,
                notification.DisplayLayout,
                run.Text,
                leaving,
                1f - notification.DepartureProgress,
                opacity,
                scale,
                play);

            return;
        }

        if (config.Motion != MotionEffect.None && notification.MotionProgress < 1f)
        {
            var glyphs = decoding ? notification.Cipher ??= EorzeanCipher.Build(run.Text) : run.Text;

            DrawMovingRun(
                drawList,
                run,
                decoding ? run.Fonts.Eorzean! : run.Fonts.Plain,
                decoding ? notification.CipherLayout : notification.DisplayLayout,
                glyphs,
                config.Motion,
                notification.MotionProgress,
                opacity,
                scale,
                play);

            return;
        }

        var ink = InkFor(run.Fill, run.Stroke, opacity, run.Name);

        if (decoding && notification.RevealProgress < 1f)
        {
            DrawDecodingRun(notification, drawList, run, notification.RevealProgress, ink, scale, play);
            return;
        }

        using (run.Fonts.Plain.Push())
        {
            var tracking = Tracking();

            DrawRun(
                drawList,
                Layout(notification.DisplayLayout, run.Text, tracking, run.CenterX, scale),
                run.Text, run.CenterX, run.Top, tracking, ink, scale, play, opacity);
        }
    }

    private void DrawMovingRun(
        ImDrawListPtr drawList, in TextRun run, IFontHandle font, LineLayout cache,
        string glyphs, MotionEffect effect, float progress, float opacity, float scale, in FillPlay play)
    {
        float tracking;
        float fontSize;

        using (run.Fonts.Plain.Push())
        {
            tracking = Tracking();

            fontSize = ImGui.GetTextLineHeight() * scale;
        }

        using (font.Push())
        {
            var xs = Layout(cache, glyphs, tracking, run.CenterX, scale).Positions;

            for (var i = 0; i < glyphs.Length; i++)
            {
                var state = GlyphAnimator.For(effect, i, glyphs.Length, progress, fontSize);

                if (state.Alpha <= 0f)
                    continue;

                var fill = play.Active ? play.Colour(i, glyphs.Length) : run.Fill;

                var glyphColor = state.Heat > 0f
                    ? Vector4.Lerp(fill, EmberColor, state.Heat)
                    : fill;

                var x = xs[i] + state.OffsetX;
                var y = run.Top + state.OffsetY;
                var glyphScale = scale;

                // A glyph growing into place grows about its own centre, not its top left corner.
                if (state.Scale != 1f)
                {
                    var width = ImGui.CalcTextSize(glyphs.AsSpan(i, 1)).X * scale;

                    x += width * (1f - state.Scale) * 0.5f;
                    y += fontSize * (1f - state.Scale) * 0.5f;
                    glyphScale = scale * state.Scale;
                }

                GlyphPainter.DrawGlyph(
                    drawList,
                    x,
                    y,
                    glyphs[i],
                    InkFor(glyphColor, run.Stroke, opacity * state.Alpha, run.Name),
                    glyphScale);
            }
        }
    }

    private void DrawDecodingRun(
        AreaNotification notification, ImDrawListPtr drawList, in TextRun run,
        float progress, in Ink ink, float scale, in FillPlay play)
    {
        var text = run.Text;
        var eorzean = run.Fonts.Eorzean!;
        var cipher = notification.Cipher ??= EorzeanCipher.Build(text);
        var decoded = (int)MathF.Round(progress * text.Length);
        var opacity = notification.Opacity;

        float tracking;
        float[] plain;

        using (run.Fonts.Plain.Push())
        {
            tracking = Tracking();
            plain = Layout(notification.DisplayLayout, text, tracking, run.CenterX, scale).Positions;
        }

        float[] runes;

        using (eorzean.Push())
        {
            runes = Layout(notification.CipherLayout, cipher, tracking, run.CenterX, scale).Positions;

            for (var i = decoded; i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), run.Top, cipher[i],
                    Coloured(ink, play, i, text.Length, opacity), scale);
        }

        using (run.Fonts.Plain.Push())
        {
            for (var i = 0; i < decoded && i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), run.Top, text[i],
                    Coloured(ink, play, i, text.Length, opacity), scale);
        }
    }

    // Letter spacing and a palette both force the per-glyph path, since ImGui can neither space a
    // string nor colour it letter by letter for us. Without either the whole line goes out as a
    // single call, which is around ten draw calls instead of ten per letter.
    private static void DrawRun(
        ImDrawListPtr drawList, LineLayout layout, string text, float centerX, float top,
        float tracking, in Ink ink, float scale, in FillPlay play, float opacity)
    {
        if (play.Active)
        {
            for (var i = 0; i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, layout.Positions[i], top, text[i], Coloured(ink, play, i, text.Length, opacity), scale);

            return;
        }

        if (tracking > 0f)
        {
            GlyphPainter.DrawRun(drawList, layout.Positions, text, top, ink, scale);
            return;
        }

        GlyphPainter.DrawStroked(
            drawList, new Vector2(centerX - (layout.Width / 2f), top), text, ink, scale);
    }

    private static Ink Coloured(in Ink ink, in FillPlay play, int index, int count, float opacity) =>
        play.Active ? ink with { Fill = GlyphPainter.Packed(play.Colour(index, count), opacity) } : ink;

    private LineLayout Layout(LineLayout cache, string text, float tracking, float centerX, float scale = 1f)
    {
        var generation = fonts.Generation;

        if (!cache.IsCurrent(text, tracking, centerX, scale, generation))
        {
            var positions = GlyphPainter.GlyphPositions(text, centerX, tracking, scale, out var width);
            cache.Store(text, tracking, centerX, scale, generation, positions, width);
        }

        return cache;
    }

    private float ScaleFor(in FontPair faces, LineLayout cache, string text, float room, float presence)
    {
        using (faces.Plain.Push())
            return FitScale(cache, text, Tracking(), room, presence);
    }

    private static float RoomFor(in Canvas canvas, float centerX)
    {
        var toLeft = centerX - canvas.Pos.X;
        var toRight = canvas.Pos.X + canvas.Size.X - centerX;

        return MathF.Max(MathF.Min(toLeft, toRight), 1f) * 2f * WidthBudget;
    }

    // The presence is the size the line wants to be; the room is what it may have. A minor line
    // that would still not fit shrinks further, the same as a full one.
    private float FitScale(LineLayout cache, string text, float tracking, float room, float presence = 1f)
    {
        if (string.IsNullOrEmpty(text))
            return presence;

        var natural = NaturalWidth(cache, text, tracking);

        return natural * presence <= room ? presence : room / natural;
    }

    private float NaturalWidth(LineLayout cache, string text, float tracking)
    {
        var generation = fonts.Generation;

        if (!cache.HasNaturalWidth(text, tracking, generation))
            cache.StoreNaturalWidth(text, tracking, generation, GlyphPainter.RunWidth(text, tracking));

        return cache.NaturalWidth;
    }

    private float Tracking() => ImGui.GetTextLineHeight() * (config.LetterSpacing / 100f);
}
