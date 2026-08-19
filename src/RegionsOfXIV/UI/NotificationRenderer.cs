using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed class NotificationRenderer(Configuration config, FontService fonts)
{
    private static readonly Vector4 EmberColor = new(1f, 0.55f, 0.15f, 1f);

    public bool IsDecoding => config.DecodeEffectEnabled && fonts.EorzeanDisplay != null;

    public void Draw(AreaNotification notification)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        var centerX = viewport.Pos.X + (viewport.Size.X * (config.HorizontalPosition / 100f));
        var top = viewport.Pos.Y
                  + (viewport.Size.Y * (config.VerticalPosition / 100f))
                  + (notification.StackOffset * ImGuiHelpers.GlobalScale);

        var stroke = GlyphPainter.Packed(config.StrokeColor, notification.Opacity);
        var strokeDistance = ImGuiHelpers.GlobalScale * config.StrokeThickness;

        notification.ApplyCasing(config.UppercaseText);

        top = DrawHeader(notification, drawList, centerX, top, stroke, strokeDistance);

        DrawParticles(notification, drawList, centerX, top);

        DrawTextLine(notification, drawList, centerX, top, strokeDistance);
    }

    private float DrawHeader(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top,
        uint stroke, float strokeDistance)
    {
        var header = notification.CasedHeader;
        if (string.IsNullOrWhiteSpace(header))
            return top;

        var fill = GlyphPainter.Packed(config.HeaderColor, notification.Opacity);

        using (fonts.Header.Push())
        {
            var tracking = Tracking();
            var layout = Layout(notification.HeaderLayout, header, tracking, centerX);

            if (config.UnderlineHeader)
            {
                var underlineY = top + ImGui.GetTextLineHeight() + (4f * ImGuiHelpers.GlobalScale);
                GlyphPainter.DrawUnderline(
                    drawList, centerX, underlineY, layout.Width,
                    notification.RevealProgress, fill, stroke,
                    2f * ImGuiHelpers.GlobalScale);
            }

            DrawRun(drawList, layout, header, centerX, top, tracking, fill, stroke, strokeDistance);

            return top + (ImGui.GetTextLineHeight() * (config.OverlapHeader ? 1.1f : 1.6f));
        }
    }

    private void DrawParticles(AreaNotification notification, ImDrawListPtr drawList, float centerX, float top)
    {
        var effect = config.Particles;
        if (effect == ParticleEffect.None && notification.Particles.IsEmpty)
            return;

        Vector2 center;
        Vector2 extent;
        using (fonts.Display.Push())
        {
            var lineHeight = ImGui.GetTextLineHeight();
            var layout = Layout(notification.DisplayLayout, notification.CasedText, Tracking(), centerX);

            center = new Vector2(centerX, top + (lineHeight / 2f));
            extent = new Vector2(layout.Width / 2f, lineHeight / 2f);
        }

        notification.Particles.Update(
            effect,
            config.ParticleDensity,
            ImGui.GetIO().DeltaTime,
            center,
            extent,
            spawning: !notification.IsFadingOut);

        notification.Particles.Draw(drawList, effect, config.ParticleColor, notification.Opacity);
    }

    private void DrawTextLine(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, float strokeDistance)
    {
        var text = notification.CasedText;
        var opacity = notification.Opacity;
        var decoding = IsDecoding;

        if (config.Motion != MotionEffect.None && notification.MotionProgress < 1f)
        {
            var glyphs = decoding ? notification.Cipher ??= EorzeanCipher.Build(text) : text;

            DrawMovingRun(
                drawList,
                decoding ? fonts.EorzeanDisplay! : fonts.Display,
                decoding ? notification.CipherLayout : notification.DisplayLayout,
                glyphs, centerX, top, config.Motion, notification.MotionProgress, opacity, strokeDistance);

            return;
        }

        var fill = GlyphPainter.Packed(config.TextColor, opacity);
        var stroke = GlyphPainter.Packed(config.StrokeColor, opacity);

        if (decoding && notification.RevealProgress < 1f)
        {
            DrawDecodingRun(
                notification, drawList, centerX, top, text,
                notification.RevealProgress, fill, stroke, strokeDistance);

            return;
        }

        using (fonts.Display.Push())
        {
            var tracking = Tracking();
            DrawRun(drawList, Layout(notification.DisplayLayout, text, tracking, centerX),
                text, centerX, top, tracking, fill, stroke, strokeDistance);
        }
    }

    private void DrawMovingRun(
        ImDrawListPtr drawList, IFontHandle font, LineLayout cache, string glyphs,
        float centerX, float top, MotionEffect motion, float progress, float opacity, float strokeDistance)
    {
        float tracking;
        float fontSize;
        using (fonts.Display.Push())
        {
            tracking = Tracking();
            fontSize = ImGui.GetTextLineHeight();
        }

        using (font.Push())
        {
            var xs = Layout(cache, glyphs, tracking, centerX).Positions;

            for (var i = 0; i < glyphs.Length; i++)
            {
                var state = GlyphAnimator.For(motion, i, glyphs.Length, progress, fontSize);

                if (state.Alpha <= 0f)
                    continue;

                var color = state.Heat > 0f
                    ? Vector4.Lerp(config.TextColor, EmberColor, state.Heat)
                    : config.TextColor;

                GlyphPainter.DrawGlyph(
                    drawList,
                    xs[i],
                    top + state.OffsetY,
                    glyphs[i],
                    GlyphPainter.Packed(color, opacity * state.Alpha),
                    GlyphPainter.Packed(config.StrokeColor, opacity * state.Alpha),
                    strokeDistance);
            }
        }
    }

    private void DrawDecodingRun(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, string text,
        float progress, uint fill, uint stroke, float strokeDistance)
    {
        var eorzean = fonts.EorzeanDisplay!;
        var cipher = notification.Cipher ??= EorzeanCipher.Build(text);
        var decoded = (int)MathF.Round(progress * text.Length);

        float tracking;
        float[] plain;
        using (fonts.Display.Push())
        {
            tracking = Tracking();
            plain = Layout(notification.DisplayLayout, text, tracking, centerX).Positions;
        }

        float[] runes;
        using (eorzean.Push())
        {
            runes = Layout(notification.CipherLayout, cipher, tracking, centerX).Positions;

            for (var i = decoded; i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), top, cipher[i], fill, stroke, strokeDistance);
        }

        using (fonts.Display.Push())
        {
            for (var i = 0; i < decoded && i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), top, text[i], fill, stroke, strokeDistance);
        }
    }

    private static void DrawRun(
        ImDrawListPtr drawList, LineLayout layout, string text, float centerX, float top,
        float tracking, uint fill, uint stroke, float strokeDistance)
    {
        if (tracking > 0f)
        {
            GlyphPainter.DrawRun(drawList, layout.Positions, text, top, fill, stroke, strokeDistance);
            return;
        }

        GlyphPainter.DrawStroked(
            drawList, new Vector2(centerX - (layout.Width / 2f), top), text, fill, stroke, strokeDistance);
    }

    private LineLayout Layout(LineLayout cache, string text, float tracking, float centerX)
    {
        var generation = fonts.Generation;

        if (!cache.IsCurrent(text, tracking, centerX, generation))
        {
            var positions = GlyphPainter.GlyphPositions(text, centerX, tracking, out var width);
            cache.Store(text, tracking, centerX, generation, positions, width);
        }

        return cache;
    }

    private float Tracking() => ImGui.GetTextLineHeight() * (config.LetterSpacing / 100f);
}
