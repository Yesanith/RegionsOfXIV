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
        var scale = ScaleFor(faces, notification.DisplayLayout, notification.CasedText, room);

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

        var ink = InkFor(WeatherFill, WeatherStroke, notification.Opacity);

        float top;
        float textCenterX;
        float scale;

        using (fonts.Weather.Push())
        {
            top = anchor.Y - WeatherGap() - (notification.StackOffset * ImGuiHelpers.GlobalScale);

            var lineHeight = ImGui.GetTextLineHeight();

            var hasIcon = config.ShowWeatherIcon && notification.IconId != 0;
            var iconWidth = hasIcon ? lineHeight * (IconScale + IconGap) : 0f;

            textCenterX = centerX + (iconWidth / 2f);

            var tracking = Tracking();
            scale = FitScale(
                notification.DisplayLayout, text, tracking, RoomFor(viewport, centerX) - iconWidth);

            var width = Layout(notification.DisplayLayout, text, tracking, textCenterX, scale).Width;

            if (hasIcon)
                DrawWeatherIcon(
                    notification, drawList, textCenterX - (width / 2f) - iconWidth, top, lineHeight, ink.Shadow);

            if (config.UnderlineHeader)
                DrawUnderline(drawList, centerX, top, iconWidth + width, notification.RevealProgress, ink);
        }

        DrawAnimatedText(
            notification,
            drawList,
            new TextRun(
                new FontPair(fonts.Weather, fonts.EorzeanWeather),
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

        var ink = InkFor(config.HeaderColor, HeaderStroke, notification.Opacity);

        using (fonts.Header.Push())
        {
            var tracking = Tracking();
            var scale = FitScale(notification.HeaderLayout, header, tracking, room);
            var layout = Layout(notification.HeaderLayout, header, tracking, centerX, scale);

            if (config.UnderlineHeader)
                DrawUnderline(drawList, centerX, top, layout.Width, notification.RevealProgress, ink);

            DrawRun(drawList, layout, header, centerX, top, tracking, ink, scale);

            return top + HeaderGap();
        }
    }

    private static void DrawWeatherIcon(
        AreaNotification notification, ImDrawListPtr drawList,
        float left, float top, float lineHeight, in Shadow shadow)
    {
        if (GameIcon.Get(notification.IconId) is not { } icon)
            return;

        var size = lineHeight * IconScale;
        var iconTop = top + ((lineHeight - size) / 2f);

        var topLeft = new Vector2(left, iconTop);
        var bottomRight = new Vector2(left + size, iconTop + size);

        if (shadow.IsVisible)
            drawList.AddImage(
                icon.Handle,
                topLeft + shadow.Offset,
                bottomRight + shadow.Offset,
                Vector2.Zero,
                Vector2.One,
                shadow.Color);

        drawList.AddImage(
            icon.Handle,
            topLeft,
            bottomRight,
            Vector2.Zero,
            Vector2.One,
            GlyphPainter.Packed(Vector4.One, notification.Opacity));
    }

    private static void DrawUnderline(
        ImDrawListPtr drawList, float centerX, float top, float width, float progress, in Ink ink)
    {
        var y = top + ImGui.GetTextLineHeight() + (UnderlineDrop * ImGuiHelpers.GlobalScale);

        GlyphPainter.DrawUnderline(
            drawList, centerX, y, width, progress, ink,
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

    private Ink InkFor(Vector4 fill, Vector4 stroke, float opacity) => new(
        GlyphPainter.Packed(fill, opacity),
        GlyphPainter.Packed(stroke, opacity),
        StrokeDistance,
        ShadowFor(opacity));

    private Shadow ShadowFor(float opacity)
    {
        if (!config.ShadowEnabled)
            return Shadow.None;

        var scale = ImGuiHelpers.GlobalScale;

        return new Shadow(
            GlyphPainter.Packed(config.ShadowColor, opacity),
            new Vector2(config.ShadowOffsetX, config.ShadowOffsetY) * scale,
            config.ShadowSoftness * scale);
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
