using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Where a notification is painted: the rectangle that bounds it and the point it grows out from.
// The overlay hands the renderer the whole screen with the anchor the position sliders choose;
// the settings window hands it the preview stage with an anchor of its own.
internal readonly record struct Canvas(Vector2 Pos, Vector2 Size, Vector2 Anchor);

// Decides where each line goes and hands the glyph painting to the Runs half of the class.
//
// The weather line is drawn above the anchor and the header and name below it, so a notification
// grows outwards from the point the position sliders choose rather than downwards from the top.
internal sealed partial class NotificationRenderer(Configuration config, FontService fonts)
{
    private const float IconScale = 1.3f;

    private const float IconGap = 0.5f;

    private const float UnderlinedWeatherGap = 1.7f;

    private const float UnderlineDrop = 4f;

    private const float UnderlineThickness = 2f;

    // The backing's margins around the text, in lines of the display face, and how much of a
    // vignette's height is spent fading in at the top and out at the bottom.
    private const float BackingPadX = 0.6f;

    private const float BackingPadY = 0.3f;

    private const float VignetteFeather = 0.35f;

    public bool IsDecoding => config.DecodeEffectEnabled && fonts.EorzeanDisplay != null;

    public void Draw(AreaNotification notification) => Draw(notification, ScreenCanvas(), 0f);

    // The same paint, dropped clear of the place name. A banner and an arrival can be on screen
    // together, so they need somewhere separate to be rather than one dismissing the other.
    public void DrawBanner(AreaNotification notification) => Draw(notification, ScreenCanvas(), BannerGap());

    public void DrawWeather(AreaNotification notification) => DrawWeather(notification, ScreenCanvas());

    // The settings window's preview stage: the same paint on a canvas of the caller's choosing,
    // drawn into whatever window is current.
    public void Draw(AreaNotification notification, in Canvas canvas) => Draw(notification, canvas, 0f);

    public void DrawBanner(AreaNotification notification, in Canvas canvas) => Draw(notification, canvas, BannerGap());

    private Canvas ScreenCanvas()
    {
        var viewport = ImGui.GetMainViewport();

        return new Canvas(viewport.Pos, viewport.Size, Anchor(viewport));
    }

    private void Draw(AreaNotification notification, in Canvas canvas, float drop)
    {
        var drawList = ImGui.GetWindowDrawList();

        var centerX = canvas.Anchor.X;
        var top = canvas.Anchor.Y + drop + (notification.StackOffset * ImGuiHelpers.GlobalScale);
        var presence = notification.Presence;

        notification.ApplyCasing(config.UppercaseText);

        var room = RoomFor(canvas, centerX);

        var faces = new FontPair(fonts.Display, fonts.EorzeanDisplay);
        var scale = ScaleFor(faces, notification.DisplayLayout, notification.CasedText, room, presence);

        DrawBacking(notification, drawList, canvas, centerX, top, room, scale, presence, withWeather: drop == 0f);

        top = DrawHeader(notification, drawList, centerX, top, room, presence);

        DrawParticles(notification, drawList, centerX, top, scale);

        DrawAnimatedText(
            notification,
            drawList,
            new TextRun(faces, notification.CasedText, config.TextColor, config.StrokeColor, centerX, top, Name: true),
            scale);
    }

    public void DrawWeather(AreaNotification notification, in Canvas canvas)
    {
        var drawList = ImGui.GetWindowDrawList();

        var anchor = canvas.Anchor;
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
                notification.DisplayLayout, text, tracking, RoomFor(canvas, centerX) - iconWidth, notification.Presence);

            var width = Layout(notification.DisplayLayout, text, tracking, textCenterX, scale).Width;

            if (hasIcon)
                DrawWeatherIcon(
                    notification, drawList, textCenterX - (width / 2f) - iconWidth, top, lineHeight, ink.Shadow);

            if (config.UnderlineHeader)
                DrawUnderline(drawList, centerX, top, iconWidth + width, notification.RevealProgress, ink, scale);
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
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, float room, float presence)
    {
        var header = notification.CasedHeader;
        if (string.IsNullOrWhiteSpace(header))
            return top;

        var ink = InkFor(config.HeaderColor, HeaderStroke, notification.Opacity);

        using (fonts.Header.Push())
        {
            var tracking = Tracking();
            var scale = FitScale(notification.HeaderLayout, header, tracking, room, presence);
            var layout = Layout(notification.HeaderLayout, header, tracking, centerX, scale);

            if (config.UnderlineHeader)
                DrawUnderline(drawList, centerX, top, layout.Width, notification.RevealProgress, ink, scale);

            DrawRun(drawList, layout, header, centerX, top, tracking, ink, scale, FillPlay.None, notification.Opacity);

            return top + (HeaderGap() * presence);
        }
    }

    // A band the width of the widest line, or a strip across the whole canvas, painted before the
    // lines and faded with them. Both take in the weather line's room above the anchor when that
    // line is on, so the three lines sit on one backing rather than two.
    private void DrawBacking(
        AreaNotification notification, ImDrawListPtr drawList, in Canvas canvas,
        float centerX, float top, float room, float scale, float presence, bool withWeather)
    {
        if (config.Backing == BackingStyle.None || notification.Opacity <= 0f)
            return;

        float width;
        float nameHeight;
        float unit;

        using (fonts.Display.Push())
        {
            width = Layout(notification.DisplayLayout, notification.CasedText, Tracking(), centerX, scale).Width;
            nameHeight = ImGui.GetTextLineHeight() * scale;
            unit = ImGui.GetTextLineHeight() * presence;
        }

        var headerBlock = 0f;
        var header = notification.CasedHeader;

        if (!string.IsNullOrWhiteSpace(header))
        {
            using (fonts.Header.Push())
            {
                var tracking = Tracking();
                var headerScale = FitScale(notification.HeaderLayout, header, tracking, room, presence);

                width = MathF.Max(width, Layout(notification.HeaderLayout, header, tracking, centerX, headerScale).Width);
                headerBlock = HeaderGap() * presence;
            }
        }

        var above = 0f;

        if (withWeather && config.WeatherNotificationEnabled)
        {
            using (fonts.Weather.Push())
            {
                var overhang = config.ShowWeatherIcon ? ImGui.GetTextLineHeight() * (IconScale - 1f) * 0.5f : 0f;
                above = WeatherGap() + overhang;
            }
        }

        var padX = unit * BackingPadX;
        var padY = unit * BackingPadY;
        var min = new Vector2(centerX - (width / 2f) - padX, top - above - padY);
        var max = new Vector2(centerX + (width / 2f) + padX, top + headerBlock + nameHeight + padY);
        var solid = GlyphPainter.Packed(config.BackingColor, notification.Opacity);
        var clear = GlyphPainter.Packed(config.BackingColor, 0f);

        if (config.Backing == BackingStyle.Vignette)
        {
            min.X = canvas.Pos.X;
            max.X = canvas.Pos.X + canvas.Size.X;

            var feather = (max.Y - min.Y) * VignetteFeather;

            drawList.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + feather), clear, clear, solid, solid);
            drawList.AddRectFilled(new Vector2(min.X, min.Y + feather), new Vector2(max.X, max.Y - feather), solid);
            drawList.AddRectFilledMultiColor(new Vector2(min.X, max.Y - feather), max, solid, solid, clear, clear);
            return;
        }

        var edge = MathF.Min(padX * 2.5f, (max.X - min.X) * 0.5f);

        drawList.AddRectFilledMultiColor(min, new Vector2(min.X + edge, max.Y), clear, solid, solid, clear);
        drawList.AddRectFilled(new Vector2(min.X + edge, min.Y), new Vector2(max.X - edge, max.Y), solid);
        drawList.AddRectFilledMultiColor(new Vector2(max.X - edge, min.Y), max, solid, clear, clear, solid);
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

    // The scale is the line's own: an auto-shrunk or minor line is shorter, and the underline has
    // to sit under its letters rather than under where full-size letters would have been.
    private static void DrawUnderline(
        ImDrawListPtr drawList, float centerX, float top, float width, float progress, in Ink ink, float scale)
    {
        var y = top + (ImGui.GetTextLineHeight() * scale) + (UnderlineDrop * ImGuiHelpers.GlobalScale);

        GlyphPainter.DrawUnderline(
            drawList, centerX, y, width, progress, ink,
            UnderlineThickness * ImGuiHelpers.GlobalScale);
    }

    private float HeaderGap() => ImGui.GetTextLineHeight() * config.HeaderGap;

    // Off the configured display size rather than ImGui.GetTextLineHeight, because no font is
    // pushed where this is read and the ambient line height would be the interface font's. That
    // is the same unit the lane spacing in NotificationOverlay is expressed in.
    private float BannerGap() =>
        config.DisplayFontSize * config.BannerGap * ImGuiHelpers.GlobalScale;

    // An underlined header needs the weather pushed further up, or the underline collides with
    // the descenders of the line above it.
    private float WeatherGap()
    {
        var gap = HeaderGap();

        return config.UnderlineHeader
            ? MathF.Max(gap, ImGui.GetTextLineHeight() * UnderlinedWeatherGap)
            : gap;
    }

    // The name line alone takes the gradient; the glow, like the shadow, covers every line.
    private Ink InkFor(Vector4 fill, Vector4 stroke, float opacity, bool name = false) => new(
        GlyphPainter.Packed(fill, opacity),
        GlyphPainter.Packed(stroke, opacity),
        StrokeDistance,
        ShadowFor(opacity),
        GlowFor(opacity),
        name && config.TextGradientEnabled ? GlyphPainter.Packed(config.TextGradientColor, opacity) : 0u);

    // Offsets are in real pixels scaled by the UI scale, deliberately not by the auto-shrink
    // scale, so the shadow keeps the same weight as the outline when a long name is squeezed.
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

    private Glow GlowFor(float opacity) => config.GlowEnabled
        ? new Glow(GlyphPainter.Packed(config.GlowColor, opacity), config.GlowSpread * ImGuiHelpers.GlobalScale)
        : Glow.None;

    private Vector4 HeaderStroke =>
        config.SeparateLineColors ? config.HeaderStrokeColor : config.StrokeColor;

    private Vector4 WeatherStroke =>
        config.SeparateLineColors ? config.WeatherStrokeColor : config.StrokeColor;

    // With separate colours off, the weather line follows the header rather than the name, since
    // the two of them are the small text above the place name and read as a pair.
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
            extent = new Vector2(layout.Width / 2f, lineHeight / 2f) * config.ParticleSpread;
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
