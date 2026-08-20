using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class NotificationRenderer(Configuration config, FontService fonts)
{
    private const float IconScale = 1.3f;

    private const float IconGap = 0.5f;

    private const float UnderlinedWeatherGap = 1.7f;

    private const float UnderlineDrop = 4f;

    private const float UnderlineThickness = 2f;

    public bool IsDecoding => config.DecodeEffectEnabled && fonts.EorzeanDisplay != null;

    public void Draw(AreaNotification notification)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        var anchor = Anchor(viewport);
        var centerX = anchor.X;
        var top = anchor.Y + (notification.StackOffset * ImGuiHelpers.GlobalScale);

        notification.ApplyCasing(config.UppercaseText);

        var room = RoomFor(viewport, centerX);

        top = DrawHeader(notification, drawList, centerX, top, room);

        var faces = new FontPair(fonts.Display, fonts.EorzeanDisplay);
        var scale = ScaleFor(faces, notification.CasedText, room);

        DrawParticles(notification, drawList, centerX, top, scale);

        DrawAnimatedText(
            notification,
            drawList,
            new TextRun(faces, notification.CasedText, config.TextColor, config.StrokeColor, centerX, top),
            scale);
    }

    public void DrawWeather(AreaNotification notification)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        var anchor = Anchor(viewport);
        var centerX = anchor.X;

        notification.ApplyCasing(config.UppercaseText);

        var text = notification.CasedText;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var fill = GlyphPainter.Packed(WeatherFill, notification.Opacity);
        var stroke = GlyphPainter.Packed(WeatherStroke, notification.Opacity);

        float top;
        float textCenterX;
        float scale;

        using (fonts.Header.Push())
        {
            top = anchor.Y - WeatherGap() - (notification.StackOffset * ImGuiHelpers.GlobalScale);

            var lineHeight = ImGui.GetTextLineHeight();

            var hasIcon = config.ShowWeatherIcon && notification.IconId != 0;
            var iconWidth = hasIcon ? lineHeight * (IconScale + IconGap) : 0f;

            textCenterX = centerX + (iconWidth / 2f);

            var tracking = Tracking();
            scale = FitScale(text, tracking, RoomFor(viewport, centerX) - iconWidth);

            var width = Layout(notification.DisplayLayout, text, tracking, textCenterX, scale).Width;

            if (hasIcon)
                DrawWeatherIcon(notification, drawList, textCenterX - (width / 2f) - iconWidth, top, lineHeight);

            if (config.UnderlineHeader)
                DrawUnderline(drawList, centerX, top, iconWidth + width, notification.RevealProgress, fill, stroke);
        }

        DrawAnimatedText(
            notification,
            drawList,
            new TextRun(
                new FontPair(fonts.Header, fonts.EorzeanHeader),
                text, WeatherFill, WeatherStroke, textCenterX, top),
            scale);
    }

    private Vector2 Anchor(ImGuiViewportPtr viewport) => new(
        viewport.Pos.X + (viewport.Size.X * (config.HorizontalPosition / 100f)),
        viewport.Pos.Y + (viewport.Size.Y * (config.VerticalPosition / 100f)));

    private float StrokeDistance => ImGuiHelpers.GlobalScale * config.StrokeThickness;

    private float DrawHeader(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, float room)
    {
        var header = notification.CasedHeader;
        if (string.IsNullOrWhiteSpace(header))
            return top;

        var fill = GlyphPainter.Packed(config.HeaderColor, notification.Opacity);
        var stroke = GlyphPainter.Packed(HeaderStroke, notification.Opacity);

        using (fonts.Header.Push())
        {
            var tracking = Tracking();
            var scale = FitScale(header, tracking, room);
            var layout = Layout(notification.HeaderLayout, header, tracking, centerX, scale);

            if (config.UnderlineHeader)
                DrawUnderline(drawList, centerX, top, layout.Width, notification.RevealProgress, fill, stroke);

            DrawRun(drawList, layout, header, centerX, top, tracking, fill, stroke, StrokeDistance, scale);

            return top + HeaderGap();
        }
    }

    private static void DrawWeatherIcon(
        AreaNotification notification, ImDrawListPtr drawList, float left, float top, float lineHeight)
    {
        if (GameIcon.Get(notification.IconId) is not { } icon)
            return;

        var size = lineHeight * IconScale;
        var iconTop = top + ((lineHeight - size) / 2f);

        drawList.AddImage(
            icon.Handle,
            new Vector2(left, iconTop),
            new Vector2(left + size, iconTop + size),
            Vector2.Zero,
            Vector2.One,
            GlyphPainter.Packed(Vector4.One, notification.Opacity));
    }

    private static void DrawUnderline(
        ImDrawListPtr drawList, float centerX, float top, float width,
        float progress, uint fill, uint stroke)
    {
        var y = top + ImGui.GetTextLineHeight() + (UnderlineDrop * ImGuiHelpers.GlobalScale);

        GlyphPainter.DrawUnderline(
            drawList, centerX, y, width, progress, fill, stroke,
            UnderlineThickness * ImGuiHelpers.GlobalScale);
    }

    private float HeaderGap() => ImGui.GetTextLineHeight() * (config.OverlapHeader ? 1.1f : 1.6f);

    private float WeatherGap()
    {
        var gap = HeaderGap();

        return config.UnderlineHeader
            ? MathF.Max(gap, ImGui.GetTextLineHeight() * UnderlinedWeatherGap)
            : gap;
    }

    private Vector4 HeaderStroke =>
        config.SeparateLineColors ? config.HeaderStrokeColor : config.StrokeColor;

    private Vector4 WeatherStroke =>
        config.SeparateLineColors ? config.WeatherStrokeColor : config.StrokeColor;

    private Vector4 WeatherFill =>
        config.SeparateLineColors ? config.WeatherColor : config.HeaderColor;

    private void DrawParticles(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, float scale)
    {
        var effect = config.Particles;
        if (effect == ParticleEffect.None && notification.Particles.IsEmpty)
            return;

        Vector2 center;
        Vector2 extent;

        using (fonts.Display.Push())
        {
            var lineHeight = ImGui.GetTextLineHeight() * scale;
            var layout = Layout(notification.DisplayLayout, notification.CasedText, Tracking(), centerX, scale);

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
}
