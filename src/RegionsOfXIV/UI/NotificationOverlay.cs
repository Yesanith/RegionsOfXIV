using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed class NotificationOverlay : Window, IDisposable, INotificationSink
{
    private const float StackSpacing = 46f;

    private static readonly TimeSpan PreviewLinger = TimeSpan.FromMilliseconds(250);

    private static readonly Vector4 EmberColor = new(1f, 0.55f, 0.15f, 1f);

    private readonly Configuration config;
    private readonly FontService fonts;

    private readonly List<AreaNotification> active = [];

    private AreaNotification? preview;
    private DateTime previewTouchedAt;

    private bool previewHeld;
    private string? heldHeader;
    private string heldText = string.Empty;

    public NotificationOverlay(Configuration config, FontService fonts)
        : base("##RegionsOfXIVOverlay")
    {
        this.config = config;
        this.fonts = fonts;

        Flags = ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoBringToFrontOnFocus;

        RespectCloseHotkey = false;
        AllowPinning = false;
        AllowClickthrough = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;

        IsOpen = true;
    }

    public void Dispose()
    {
        this.active.Clear();
        this.preview = null;
        this.previewHeld = false;
    }

    public void Push(string? header, string text)
    {
        if (this.previewHeld)
            return;

        Spawn(header, text);
    }

    private AreaNotification? Spawn(string? header, string text)
    {
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(header))
            return null;

        this.preview = null;

        foreach (var existing in this.active)
        {
            existing.StackOffset += StackSpacing;
            existing.Dismiss();
        }

        var notification = new AreaNotification(
            header,
            text,
            this.config.FadeInDuration,
            this.config.Motion == MotionEffect.None ? TimeSpan.Zero : this.config.MotionDuration,
            IsDecoding ? this.config.RevealDuration : TimeSpan.Zero,
            this.config.ShowDuration,
            this.config.FadeOutDuration);

        this.active.Add(notification);
        return notification;
    }

    public TimeSpan EstimatedDuration =>
        (this.config.Motion == MotionEffect.None
            ? this.config.FadeInDuration
            : Longer(this.config.FadeInDuration, this.config.MotionDuration))
        + TimeSpan.FromMilliseconds(200)
        + (IsDecoding ? this.config.RevealDuration : TimeSpan.Zero)
        + this.config.ShowDuration
        + this.config.FadeOutDuration;

    public void TouchPreview(string? header, string text)
    {
        this.previewTouchedAt = DateTime.UtcNow;

        if (this.preview is { IsDone: false } existing)
        {
            existing.IsPinned = true;
            return;
        }

        SpawnPreview(header, text);
    }

    public void PreviewOnce(string? header, string text)
    {
        if (!this.previewHeld)
        {
            Spawn(header, text);
            return;
        }

        if (this.preview is { } current)
            this.active.Remove(current);

        SpawnPreview(this.heldHeader, this.heldText);
    }

    public void HoldPreview(bool held, string? header, string text)
    {
        this.previewHeld = held;

        if (!held)
        {
            if (this.preview is { } current)
                current.IsPinned = false;

            this.preview = null;
            return;
        }

        this.heldHeader = header;
        this.heldText = text;

        if (this.preview is not { IsDone: false })
            SpawnPreview(header, text);
    }

    private void SpawnPreview(string? header, string text)
    {
        if (Spawn(header, text) is not { } notification)
            return;

        this.preview = notification;
        this.preview.IsPinned = true;
        this.previewTouchedAt = DateTime.UtcNow;
    }

    public override bool DrawConditions() => this.active.Count > 0;

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        Position = viewport.Pos;
        Size = viewport.Size;
        PositionCondition = ImGuiCond.Always;
        SizeCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        ReleasePreviewIfIdle();

        for (var i = this.active.Count - 1; i >= 0; i--)
        {
            var notification = this.active[i];
            notification.Update();

            if (notification.IsDone)
            {
                this.active.RemoveAt(i);
                continue;
            }

            DrawOne(notification);
        }
    }

    private void ReleasePreviewIfIdle()
    {
        if (this.previewHeld)
            return;

        if (this.preview is not { } current)
            return;

        if (current.IsDone)
        {
            this.preview = null;
            return;
        }

        if (DateTime.UtcNow - this.previewTouchedAt < PreviewLinger)
            return;

        current.IsPinned = false;
        this.preview = null;
    }

    private bool IsDecoding => this.config.DecodeEffectEnabled && this.fonts.EorzeanDisplay != null;

    private static TimeSpan Longer(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private void DrawOne(AreaNotification notification)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        var centerX = viewport.Pos.X + (viewport.Size.X * (this.config.HorizontalPosition / 100f));
        var top = viewport.Pos.Y
                  + (viewport.Size.Y * (this.config.VerticalPosition / 100f))
                  + (notification.StackOffset * ImGuiHelpers.GlobalScale);

        var stroke = GlyphPainter.Packed(this.config.StrokeColor, notification.Opacity);
        var strokeDistance = ImGuiHelpers.GlobalScale * this.config.StrokeThickness;

        notification.ApplyCasing(this.config.UppercaseText);

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

        var fill = GlyphPainter.Packed(this.config.HeaderColor, notification.Opacity);

        using (this.fonts.Header.Push())
        {
            var tracking = Tracking();

            if (this.config.UnderlineHeader)
            {
                var underlineY = top + ImGui.GetTextLineHeight() + (4f * ImGuiHelpers.GlobalScale);
                GlyphPainter.DrawUnderline(
                    drawList, centerX, underlineY, GlyphPainter.RunWidth(header, tracking),
                    notification.RevealProgress, fill, stroke,
                    2f * ImGuiHelpers.GlobalScale);
            }

            DrawStaticRun(drawList, notification.HeaderLayout, header, centerX, top, tracking, fill, stroke, strokeDistance);

            return top + (ImGui.GetTextLineHeight() * (this.config.OverlapHeader ? 1.1f : 1.6f));
        }
    }

    private void DrawParticles(AreaNotification notification, ImDrawListPtr drawList, float centerX, float top)
    {
        var effect = this.config.Particles;
        if (effect == ParticleEffect.None && notification.Particles.IsEmpty)
            return;

        Vector2 center;
        Vector2 extent;
        using (this.fonts.Display.Push())
        {
            var lineHeight = ImGui.GetTextLineHeight();
            center = new Vector2(centerX, top + (lineHeight / 2f));
            extent = new Vector2(GlyphPainter.RunWidth(notification.CasedText, Tracking()) / 2f, lineHeight / 2f);
        }

        notification.Particles.Update(
            effect,
            this.config.ParticleDensity,
            ImGui.GetIO().DeltaTime,
            center,
            extent,
            spawning: !notification.IsFadingOut);

        notification.Particles.Draw(drawList, effect, this.config.ParticleColor, notification.Opacity);
    }

    private void DrawTextLine(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, float strokeDistance)
    {
        var text = notification.CasedText;
        var opacity = notification.Opacity;
        var fill = GlyphPainter.Packed(this.config.TextColor, opacity);
        var stroke = GlyphPainter.Packed(this.config.StrokeColor, opacity);

        var motion = this.config.Motion;
        var decoding = IsDecoding;

        if (motion != MotionEffect.None && notification.MotionProgress < 1f)
        {
            var glyphs = decoding ? notification.Cipher ??= EorzeanCipher.Build(text) : text;

            DrawMovingRun(
                drawList,
                decoding ? this.fonts.EorzeanDisplay! : this.fonts.Display,
                decoding ? notification.CipherLayout : notification.DisplayLayout,
                glyphs, centerX, top, motion, notification.MotionProgress, opacity, strokeDistance);

            return;
        }

        if (decoding && notification.RevealProgress < 1f)
        {
            DrawDecodingRun(
                notification, drawList, centerX, top, text,
                notification.RevealProgress, fill, stroke, strokeDistance);

            return;
        }

        using (this.fonts.Display.Push())
            DrawStaticRun(drawList, notification.DisplayLayout, text, centerX, top, Tracking(), fill, stroke, strokeDistance);
    }

    private void DrawMovingRun(
        ImDrawListPtr drawList, IFontHandle font, LineLayout cache, string glyphs,
        float centerX, float top, MotionEffect motion, float progress, float opacity, float strokeDistance)
    {
        float tracking;
        float fontSize;
        using (this.fonts.Display.Push())
        {
            tracking = Tracking();
            fontSize = ImGui.GetTextLineHeight();
        }

        using (font.Push())
        {
            var xs = Positions(cache, glyphs, tracking, centerX);

            for (var i = 0; i < glyphs.Length; i++)
            {
                var state = GlyphAnimator.For(motion, i, glyphs.Length, progress, fontSize);

                if (state.Alpha <= 0f)
                    continue;

                var color = state.Heat > 0f
                    ? Vector4.Lerp(this.config.TextColor, EmberColor, state.Heat)
                    : this.config.TextColor;

                GlyphPainter.DrawGlyph(
                    drawList,
                    xs[i],
                    top + state.OffsetY,
                    glyphs[i],
                    GlyphPainter.Packed(color, opacity * state.Alpha),
                    GlyphPainter.Packed(this.config.StrokeColor, opacity * state.Alpha),
                    strokeDistance);
            }
        }
    }

    private void DrawDecodingRun(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, string text,
        float progress, uint fill, uint stroke, float strokeDistance)
    {
        var eorzean = this.fonts.EorzeanDisplay!;
        var cipher = notification.Cipher ??= EorzeanCipher.Build(text);

        float tracking;
        using (this.fonts.Display.Push())
            tracking = Tracking();

        float[] runes;
        float[] plain;
        using (eorzean.Push())
            runes = Positions(notification.CipherLayout, cipher, tracking, centerX);

        using (this.fonts.Display.Push())
            plain = Positions(notification.DisplayLayout, text, tracking, centerX);

        var decoded = (int)MathF.Round(progress * text.Length);

        using (eorzean.Push())
        {
            for (var i = decoded; i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), top, cipher[i], fill, stroke, strokeDistance);
        }

        using (this.fonts.Display.Push())
        {
            for (var i = 0; i < decoded && i < text.Length; i++)
                GlyphPainter.DrawGlyph(
                    drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), top, text[i], fill, stroke, strokeDistance);
        }
    }

    private void DrawStaticRun(
        ImDrawListPtr drawList, LineLayout cache, string text, float centerX, float top,
        float tracking, uint fill, uint stroke, float strokeDistance)
    {
        if (tracking > 0f)
            GlyphPainter.DrawRun(drawList, Positions(cache, text, tracking, centerX), text, top, fill, stroke, strokeDistance);
        else
            GlyphPainter.DrawCentered(drawList, centerX, top, text, fill, stroke, strokeDistance);
    }

    private float[] Positions(LineLayout cache, string text, float tracking, float centerX)
    {
        var generation = this.fonts.Generation;

        return cache.IsCurrent(text, tracking, centerX, generation)
            ? cache.Positions
            : cache.Store(text, tracking, centerX, generation, GlyphPainter.GlyphPositions(text, centerX, tracking));
    }

    private float Tracking() => ImGui.GetTextLineHeight() * (this.config.LetterSpacing / 100f);
}
