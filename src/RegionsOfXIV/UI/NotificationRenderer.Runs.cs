using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.UI;

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
        float Top);

    private void DrawAnimatedText(
        AreaNotification notification, ImDrawListPtr drawList, in TextRun run, float scale)
    {
        var opacity = notification.Opacity;
        var decoding = config.DecodeEffectEnabled && run.Fonts.Eorzean != null;

        if (config.Motion != MotionEffect.None && notification.MotionProgress < 1f)
        {
            var glyphs = decoding ? notification.Cipher ??= EorzeanCipher.Build(run.Text) : run.Text;

            DrawMovingRun(
                drawList,
                run,
                decoding ? run.Fonts.Eorzean! : run.Fonts.Plain,
                decoding ? notification.CipherLayout : notification.DisplayLayout,
                glyphs,
                notification.MotionProgress,
                opacity,
                scale);

            return;
        }

        var ink = InkFor(run.Fill, run.Stroke, opacity);

        if (decoding && notification.RevealProgress < 1f)
        {
            DrawDecodingRun(notification, drawList, run, notification.RevealProgress, ink, scale);
            return;
        }

        using (run.Fonts.Plain.Push())
        {
            var tracking = Tracking();

            DrawRun(
                drawList,
                Layout(notification.DisplayLayout, run.Text, tracking, run.CenterX, scale),
                run.Text, run.CenterX, run.Top, tracking, ink, scale);
        }
    }

    private void DrawMovingRun(
        ImDrawListPtr drawList, in TextRun run, IFontHandle font, LineLayout cache,
        string glyphs, float progress, float opacity, float scale)
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
                var state = GlyphAnimator.For(config.Motion, i, glyphs.Length, progress, fontSize);

                if (state.Alpha <= 0f)
                    continue;

                var glyphColor = state.Heat > 0f
                    ? Vector4.Lerp(run.Fill, EmberColor, state.Heat)
                    : run.Fill;

                GlyphPainter.DrawGlyph(
                    drawList,
                    xs[i],
                    run.Top + state.OffsetY,
                    glyphs[i],
                    InkFor(glyphColor, run.Stroke, opacity * state.Alpha),
                    scale);
            }
        }
    }

    private void DrawDecodingRun(
        AreaNotification notification, ImDrawListPtr drawList, in TextRun run,
        float progress, in Ink ink, float scale)
    {
        var text = run.Text;
        var eorzean = run.Fonts.Eorzean!;
        var cipher = notification.Cipher ??= EorzeanCipher.Build(text);
        var decoded = (int)MathF.Round(progress * text.Length);

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
                    ink, scale);
        }

        using (run.Fonts.Plain.Push())
        {
            for (var i = 0; i < decoded && i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), run.Top, text[i],
                    ink, scale);
        }
    }

    private static void DrawRun(
        ImDrawListPtr drawList, LineLayout layout, string text, float centerX, float top,
        float tracking, in Ink ink, float scale = 1f)
    {
        if (tracking > 0f)
        {
            GlyphPainter.DrawRun(drawList, layout.Positions, text, top, ink, scale);
            return;
        }

        GlyphPainter.DrawStroked(
            drawList, new Vector2(centerX - (layout.Width / 2f), top), text, ink, scale);
    }

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

    private float ScaleFor(in FontPair faces, LineLayout cache, string text, float room)
    {
        using (faces.Plain.Push())
            return FitScale(cache, text, Tracking(), room);
    }

    private static float RoomFor(ImGuiViewportPtr viewport, float centerX)
    {
        var toLeft = centerX - viewport.Pos.X;
        var toRight = viewport.Pos.X + viewport.Size.X - centerX;

        return MathF.Max(MathF.Min(toLeft, toRight), 1f) * 2f * WidthBudget;
    }

    private float FitScale(LineLayout cache, string text, float tracking, float room)
    {
        if (string.IsNullOrEmpty(text))
            return 1f;

        var natural = NaturalWidth(cache, text, tracking);

        return natural <= room ? 1f : room / natural;
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
