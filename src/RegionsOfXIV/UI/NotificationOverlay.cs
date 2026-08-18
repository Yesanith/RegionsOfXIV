using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Full-screen, input-transparent draw surface for notifications. Registered with
// WindowSystem, which is both a D17 approval criterion and what gets us correct
// behaviour when the user hides the UI.
internal sealed class NotificationOverlay : Window, IDisposable, INotificationSink
{
    private const float StackSpacing = 46f;

    private readonly Configuration config;
    private readonly FontService fonts;

    private readonly List<AreaNotification> active = [];

    // How long the live preview outlives the last settings change before it is let
    // go. Long enough to bridge the gap between two drags of a slider, short
    // enough that it reads as a response to what you just did.
    private static readonly TimeSpan PreviewLinger = TimeSpan.FromMilliseconds(250);

    private AreaNotification? preview;
    private DateTime previewTouchedAt;

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

        // It is an overlay, not a window: no chrome, no pinning, no sounds.
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
    }

    public void Push(string? header, string text)
    {
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(header))
            return;

        // Anything arriving supersedes the preview like any other notification, so
        // stop tracking it — the next settings change starts a fresh one rather
        // than trying to revive one already on its way off screen.
        this.preview = null;

        // A newer notification supersedes older ones: they slide down and fade.
        foreach (var existing in this.active)
        {
            existing.StackOffset += StackSpacing;
            existing.Dismiss();
        }

        // A stage the settings have switched off is passed as no time at all,
        // rather than as time spent showing something that is not happening. The
        // decode also depends on a font that may not have loaded, which is
        // knowable here and not inside the notification.
        var notification = new AreaNotification(
            header,
            text,
            this.config.FadeInDuration,
            this.config.Motion == MotionEffect.None ? TimeSpan.Zero : this.config.MotionDuration,
            IsDecoding ? this.config.RevealDuration : TimeSpan.Zero,
            this.config.ShowDuration,
            this.config.FadeOutDuration);

        this.active.Add(notification);
    }

    // Keeps a sample notification on screen while the user is changing settings.
    //
    // DrawOne reads position, colour and the font handles out of the config on
    // every frame, so a notification already on screen follows the sliders by
    // itself. The only thing missing is one being there — and not timing out
    // halfway through a drag, which is what pinning prevents.
    public void TouchPreview(string? header, string text)
    {
        this.previewTouchedAt = DateTime.UtcNow;

        if (this.preview is { IsDone: false } existing)
        {
            existing.IsPinned = true;
            return;
        }

        Push(header, text);

        // Push clears the field and appends, in that order, so the new
        // notification is both the last entry and safe to claim as the preview.
        this.preview = this.active[^1];
        this.preview.IsPinned = true;
    }

    // Lets the preview go once the settings have been still for a moment. Unpinned
    // rather than dismissed: it has been sitting fully revealed with its Show phase
    // long since elapsed, so releasing it drops it straight into the same fade-out
    // any other notification gets.
    private void ReleasePreviewIfIdle()
    {
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

    // Whether a decode will actually happen: asked for, and with a font able to
    // answer. Both halves matter — a missing bundled font is not an error, it just
    // means there is nothing to decode from.
    private bool IsDecoding => this.config.DecodeEffectEnabled && this.fonts.EorzeanDisplay != null;

    // The fade and the motion share a stage, so that stage lasts as long as the
    // longer of them; the decode follows, and only if there is one.
    public TimeSpan EstimatedDuration =>
        (this.config.Motion == MotionEffect.None
            ? this.config.FadeInDuration
            : Longer(this.config.FadeInDuration, this.config.MotionDuration))
        + TimeSpan.FromMilliseconds(200)
        + (IsDecoding ? this.config.RevealDuration : TimeSpan.Zero)
        + this.config.ShowDuration
        + this.config.FadeOutDuration;

    private static TimeSpan Longer(TimeSpan a, TimeSpan b) => a > b ? a : b;

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

    // --- Notification rendering --------------------------------------------

    private void DrawOne(AreaNotification notification)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        var centerX = viewport.Pos.X + (viewport.Size.X * (this.config.HorizontalPosition / 100f));
        var top = viewport.Pos.Y
                  + (viewport.Size.Y * (this.config.VerticalPosition / 100f))
                  + (notification.StackOffset * ImGuiHelpers.GlobalScale);

        var headerFill = GlyphPainter.Packed(this.config.HeaderColor, notification.Opacity);
        var stroke = GlyphPainter.Packed(this.config.StrokeColor, notification.Opacity);
        var strokeDistance = ImGuiHelpers.GlobalScale * this.config.StrokeThickness;

        // Cased here rather than when the notification was built, so toggling the
        // setting reaches the one already on screen — which is what makes the
        // config window's live preview answer the checkbox immediately. The
        // notification remembers the result; this only tells it what to hold.
        notification.ApplyCasing(this.config.UppercaseText);

        var header = notification.CasedHeader;

        if (!string.IsNullOrWhiteSpace(header))
        {
            using (this.fonts.Header.Push())
            {
                var tracking = Tracking();

                if (this.config.UnderlineHeader)
                {
                    var underlineY = top + ImGui.GetTextLineHeight() + (4f * ImGuiHelpers.GlobalScale);
                    GlyphPainter.DrawUnderline(
                        drawList, centerX, underlineY, GlyphPainter.RunWidth(header, tracking),
                        notification.RevealProgress, headerFill, stroke,
                        2f * ImGuiHelpers.GlobalScale);
                }

                if (tracking > 0f)
                {
                    GlyphPainter.DrawRun(
                        drawList, Positions(notification.HeaderLayout, header, tracking, centerX),
                        header, top, headerFill, stroke, strokeDistance);
                }
                else
                {
                    GlyphPainter.DrawCentered(drawList, centerX, top, header, headerFill, stroke, strokeDistance);
                }

                top += ImGui.GetTextLineHeight() * (this.config.OverlapHeader ? 1.1f : 1.6f);
            }
        }

        var text = notification.CasedText;
        var particles = this.config.Particles;

        // Skipped outright when there is nothing playing and nothing left over
        // from an effect just switched off. It is only a measurement and a font
        // push, but it is one of each on every frame of every notification, spent
        // on a feature that is off by default.
        if (particles != ParticleEffect.None || !notification.Particles.IsEmpty)
        {
            // What the particles play around: the middle of the display line, and
            // how far it reaches either way.
            Vector2 center;
            Vector2 extent;
            using (this.fonts.Display.Push())
            {
                var lineHeight = ImGui.GetTextLineHeight();
                center = new Vector2(centerX, top + (lineHeight / 2f));
                extent = new Vector2(GlyphPainter.RunWidth(text, Tracking()) / 2f, lineHeight / 2f);
            }

            // Before the text, so glyphs stay legible with particles behind them.
            notification.Particles.Update(
                particles,
                this.config.ParticleDensity,
                ImGui.GetIO().DeltaTime,
                center,
                extent,
                spawning: !notification.IsFadingOut);

            notification.Particles.Draw(
                drawList, particles, this.config.ParticleColor, notification.Opacity);
        }

        DrawTextLine(
            notification,
            drawList,
            centerX,
            top,
            text,
            notification.MotionProgress,
            notification.RevealProgress,
            notification.Opacity,
            strokeDistance);
    }

    // The line's three stages, in the order they happen.
    //
    //   arriving   the motion, in Eorzean script if there is a decode to come and
    //              in plain text if there is not
    //   decoding   the Eorzean resolving, with the glyphs already landed
    //   settled    one run, no per-glyph work
    //
    // The handovers line up by construction rather than by tolerance. The motion
    // ends with the glyphs at the Eorzean layout, which is exactly where the
    // decode's interpolation begins; the decode ends at the display layout, which
    // is what the settled run draws. Neither seam moves a glyph.
    private void DrawTextLine(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, string text,
        float motionProgress, float revealProgress, float opacity, float strokeDistance)
    {
        var motion = this.config.Motion;
        var decoding = IsDecoding;

        if (motion != MotionEffect.None && motionProgress < 1f)
        {
            if (decoding)
                DrawArrivingCipher(notification, drawList, centerX, top, motionProgress, opacity, strokeDistance, motion);
            else
                DrawAnimatedLine(notification, drawList, centerX, top, text, motionProgress, opacity, strokeDistance, motion);

            return;
        }

        if (decoding && revealProgress < 1f)
        {
            DrawDecodingLine(
                notification,
                drawList,
                centerX,
                top,
                text,
                revealProgress,
                GlyphPainter.Packed(this.config.TextColor, opacity),
                GlyphPainter.Packed(this.config.StrokeColor, opacity),
                strokeDistance);

            return;
        }

        // Settled. The one path with no per-glyph work at all, and the one a
        // notification spends most of its life on — the hold is longer than the
        // motion and the decode together.
        using (this.fonts.Display.Push())
        {
            var fill = GlyphPainter.Packed(this.config.TextColor, opacity);
            var stroke = GlyphPainter.Packed(this.config.StrokeColor, opacity);
            var tracking = Tracking();

            if (tracking > 0f)
            {
                GlyphPainter.DrawRun(
                    drawList, Positions(notification.DisplayLayout, text, tracking, centerX),
                    text, top, fill, stroke, strokeDistance);
            }
            else
            {
                GlyphPainter.DrawCentered(drawList, centerX, top, text, fill, stroke, strokeDistance);
            }
        }
    }

    // The motion, with a decode still to come: the glyphs that fly in are the
    // Eorzean cipher, laid out on the Eorzean font's own advances.
    //
    // Those advances are what makes this its own method rather than a flag on
    // DrawAnimatedLine. The Eorzean face is far wider than the display one, and
    // landing the cipher on display metrics would leave the decode nothing to
    // interpolate — the line would arrive already at its final width and then
    // resolve in place, which is the flat version of the effect.
    private void DrawArrivingCipher(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, float progress,
        float opacity, float strokeDistance, MotionEffect motion)
    {
        var eorzean = this.fonts.EorzeanDisplay!;
        var cipher = notification.Cipher ??= EorzeanCipher.Build(notification.CasedText);

        float tracking;
        float fontSize;
        using (this.fonts.Display.Push())
        {
            tracking = Tracking();
            fontSize = ImGui.GetTextLineHeight();
        }

        using (eorzean.Push())
        {
            var xs = Positions(notification.CipherLayout, cipher, tracking, centerX);

            for (var i = 0; i < cipher.Length; i++)
            {
                var state = GlyphAnimator.For(motion, i, cipher.Length, progress, fontSize);

                if (state.Alpha <= 0f)
                    continue;

                var color = state.Heat > 0f
                    ? Vector4.Lerp(this.config.TextColor, EmberColor, state.Heat)
                    : this.config.TextColor;

                GlyphPainter.DrawGlyph(
                    drawList,
                    xs[i],
                    top + state.OffsetY,
                    cipher[i],
                    GlyphPainter.Packed(color, opacity * state.Alpha),
                    GlyphPainter.Packed(this.config.StrokeColor, opacity * state.Alpha),
                    strokeDistance);
            }
        }
    }

    // Motion without a decode. GlyphAnimator decides what each glyph is doing;
    // this places it and picks its colour.
    private void DrawAnimatedLine(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, string text, float progress,
        float opacity, float strokeDistance, MotionEffect effect)
    {
        using (this.fonts.Display.Push())
        {
            var tracking = Tracking();
            var fontSize = ImGui.GetTextLineHeight();
            var xs = Positions(notification.DisplayLayout, text, tracking, centerX);

            for (var i = 0; i < text.Length; i++)
            {
                var state = GlyphAnimator.For(effect, i, text.Length, progress, fontSize);

                if (state.Alpha <= 0f)
                    continue;

                // Heat is the burn's only colour work: from the ember towards the
                // configured colour as the glyph cools. Every other effect leaves
                // it at zero and takes the configured colour untouched.
                var color = state.Heat > 0f
                    ? Vector4.Lerp(this.config.TextColor, EmberColor, state.Heat)
                    : this.config.TextColor;

                GlyphPainter.DrawGlyph(
                    drawList,
                    xs[i],
                    top + state.OffsetY,
                    text[i],
                    GlyphPainter.Packed(color, opacity * state.Alpha),
                    GlyphPainter.Packed(this.config.StrokeColor, opacity * state.Alpha),
                    strokeDistance);
            }
        }
    }

    // Glyph positions for a line, from the cache when it still holds and worked
    // out afresh when it does not. The font must already be pushed: what is being
    // remembered is a measurement against it, which is also why the font's
    // generation is part of the key.
    private float[] Positions(LineLayout cache, string text, float tracking, float centerX)
    {
        var generation = this.fonts.Generation;

        return cache.IsCurrent(text, tracking, centerX, generation)
            ? cache.Positions
            : cache.Store(text, tracking, centerX, generation, GlyphPainter.GlyphPositions(text, centerX, tracking));
    }

    // What a glyph is at the moment it catches: hot, and well clear of any colour
    // a player is likely to have chosen for the text itself.
    private static readonly Vector4 EmberColor = new(1f, 0.55f, 0.15f, 1f);

    // Extra advance between glyphs, in pixels, for whichever font is pushed right
    // now. Read from ImGui rather than from the configured size so the header —
    // built at its own size, from a different family — gets its own proportional
    // share of the one setting.
    //
    // GetTextLineHeight is the font's size: ImGui returns the same field from
    // both, and this file already leans on it for the line advance below.
    private float Tracking() => ImGui.GetTextLineHeight() * (this.config.LetterSpacing / 100f);

    // Eorzean -> readable decode, the same trick the GW2 original uses.
    //
    // For Latin the bundled font substitutes codepoints 1:1, so the identical
    // string renders as Eorzean script in one font and plain text in the other.
    // Japanese has no such luxury — the font holds 240 Latin glyphs and nothing
    // else — so the undecoded half is drawn from a stand-in string instead. The
    // effect never depended on the scrambled glyph *being* the same character,
    // only on it being unreadable and resolving into the right one.
    //
    // Glyphs swap individually rather than cross-fading, which avoids two
    // overlapping glyph shapes turning to mush.
    //
    // Runs after any motion has finished, so the glyphs are already at rest and
    // this only has to substitute them.
    private void DrawDecodingLine(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, string text, float progress,
        uint fill, uint stroke, float strokeDistance)
    {
        // Non-null and mid-reveal, both established by the caller.
        var eorzean = this.fonts.EorzeanDisplay!;

        var cipher = notification.Cipher ??= EorzeanCipher.Build(text);

        // Both handles are built at the display size, so one tracking value serves
        // both layouts — which it has to, or the interpolation below would be
        // sliding between two differently-spaced lines.
        float tracking;
        using (this.fonts.Display.Push())
            tracking = Tracking();

        // The two faces have very different advance widths — the display face is
        // condensed, the Eorzean one is not, and a full-width Japanese glyph is
        // wider than either — so laying one out on the other's advances crowds or
        // strands the glyphs. Measure both layouts and interpolate: the line starts
        // on Eorzean metrics and settles onto the real ones as it decodes. Both are
        // centred, so it grows symmetrically rather than drifting.
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
                GlyphPainter.DrawGlyph(drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), top, cipher[i], fill, stroke, strokeDistance);
        }

        using (this.fonts.Display.Push())
        {
            for (var i = 0; i < decoded && i < text.Length; i++)
                GlyphPainter.DrawGlyph(drawList, GlyphPainter.Lerp(runes[i], plain[i], progress), top, text[i], fill, stroke, strokeDistance);
        }
    }

}
