using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed class NotificationRenderer(Configuration config, FontService fonts)
{
    private static readonly Vector4 EmberColor = new(1f, 0.55f, 0.15f, 1f);

    /// <summary>Icon height as a multiple of the header line, and the gap it keeps from the text.</summary>
    private const float IconScale = 1.3f;

    private const float IconGap = 0.25f;

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

    public void DrawWeather(AreaNotification notification)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        var centerX = viewport.Pos.X + (viewport.Size.X * (config.HorizontalPosition / 100f));
        var anchor = viewport.Pos.Y + (viewport.Size.Y * (config.VerticalPosition / 100f));

        notification.ApplyCasing(config.UppercaseText);

        var text = notification.CasedText;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var fill = GlyphPainter.Packed(config.HeaderColor, notification.Opacity);
        var stroke = GlyphPainter.Packed(config.StrokeColor, notification.Opacity);
        var strokeDistance = ImGuiHelpers.GlobalScale * config.StrokeThickness;

        using (fonts.Header.Push())
        {
            // One header line above the block, matching the gap the header keeps
            // from the name below it. Outgoing lines drift up, away from the block.
            var top = anchor - HeaderGap() - (notification.StackOffset * ImGuiHelpers.GlobalScale);

            var lineHeight = ImGui.GetTextLineHeight();

            // Room is kept for the icon whether or not the texture has finished
            // loading, so the text does not jump sideways when it arrives.
            var hasIcon = config.ShowWeatherIcon && notification.IconId != 0;
            var iconWidth = hasIcon ? lineHeight * (IconScale + IconGap) : 0f;

            // The icon and the text are centred as one group, so the text shifts right
            // by half the room the icon takes up.
            var textCenterX = centerX + (iconWidth / 2f);

            var tracking = Tracking();
            var layout = Layout(notification.DisplayLayout, text, tracking, textCenterX);

            if (hasIcon && WeatherIcon(notification.IconId) is { } icon)
            {
                var size = lineHeight * IconScale;
                var left = textCenterX - (layout.Width / 2f) - iconWidth;
                var iconTop = top + ((lineHeight - size) / 2f);

                drawList.AddImage(
                    icon.Handle,
                    new Vector2(left, iconTop),
                    new Vector2(left + size, iconTop + size),
                    Vector2.Zero,
                    Vector2.One,
                    GlyphPainter.Packed(Vector4.One, notification.Opacity));
            }

            DrawRun(drawList, layout, text, textCenterX, top, tracking, fill, stroke, strokeDistance);
        }
    }

    /// <summary>The game's own icon for a weather, or null while it is still loading.</summary>
    private static IDalamudTextureWrap? WeatherIcon(uint iconId)
    {
        if (iconId == 0)
            return null;

        return Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId))
            .TryGetWrap(out var wrap, out _)
            ? wrap
            : null;
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

            return top + HeaderGap();
        }
    }

    /// <summary>Vertical step between the header line and the name below it, in the header font.</summary>
    private float HeaderGap() => ImGui.GetTextLineHeight() * (config.OverlapHeader ? 1.1f : 1.6f);

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
