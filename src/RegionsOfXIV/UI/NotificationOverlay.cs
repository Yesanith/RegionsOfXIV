using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
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

    // How long the live preview outlives the last settings change before it is let
    // go. Long enough to bridge the gap between two drags of a slider, short
    // enough that it reads as a response to what you just did.
    private static readonly TimeSpan PreviewLinger = TimeSpan.FromMilliseconds(250);

    // What a glyph is at the moment it catches: hot, and well clear of any colour
    // a player is likely to have chosen for the text itself.
    private static readonly Vector4 EmberColor = new(1f, 0.55f, 0.15f, 1f);

    private readonly Configuration config;
    private readonly FontService fonts;

    private readonly List<AreaNotification> active = [];

    private AreaNotification? preview;
    private DateTime previewTouchedAt;

    // Editing mode. While set, exactly one notification is on screen at any time —
    // the preview, pinned open indefinitely — and nothing is allowed to stack on
    // it. The sample it shows is remembered so the mode can put it back after a
    // restart, which is the one thing that legitimately ends it.
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
        this.previewHeld = false;
    }

    public void Push(string? header, string text)
    {
        // Editing mode owns the screen. Stacking an announcement on the held
        // preview is exactly the pile-up the mode exists to prevent, and letting it
        // through would dismiss the preview and leave the mode showing nothing.
        //
        // Dropped rather than queued: the mode is only on while someone is sitting
        // in the config window with the checkbox ticked, and a zone announcement
        // replayed on the way out would arrive with no idea what it referred to.
        if (this.previewHeld)
            return;

        Spawn(header, text);
    }

    // Appends a notification and sweeps whatever was on screen out from under it.
    // The common ground between a real announcement and a preview, which differ
    // only in what happens after.
    private AreaNotification? Spawn(string? header, string text)
    {
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(header))
            return null;

        // Anything arriving supersedes the preview like any other notification, so
        // stop tracking it — the next settings change starts a fresh one rather
        // than trying to revive one already on its way off screen.
        this.preview = null;

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
        return notification;
    }

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

        SpawnPreview(header, text);
    }

    // One sample, start to finish. What the Preview buttons and an applied preset
    // ask for: not "keep something on screen" but "play it again".
    //
    // In editing mode the replay happens in place. The preview already on screen is
    // removed outright rather than dismissed, so the restart costs nothing on
    // screen — no outgoing copy sliding down behind the new one, which is what
    // makes clicking through seven presets in a row read as spam.
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

    // Enters or leaves editing mode.
    //
    // The one preview it holds is an ordinary notification, pinned: it fades in,
    // runs its motion and decode, and then sits in its Show phase indefinitely
    // because pinning is exactly what stops that phase timing out. Everything it
    // draws with is read from the config per frame, so it tracks every setting
    // as it moves without being restarted.
    public void HoldPreview(bool held, string? header, string text)
    {
        this.previewHeld = held;

        if (!held)
        {
            // Unpinned rather than removed: it has long since finished revealing,
            // so letting go drops it into the same fade-out every notification
            // gets, and leaving the mode looks like a notification ending.
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

    // Lets the preview go once the settings have been still for a moment. Unpinned
    // rather than dismissed: it has been sitting fully revealed with its Show phase
    // long since elapsed, so releasing it drops it straight into the same fade-out
    // any other notification gets.
    private void ReleasePreviewIfIdle()
    {
        // Editing mode is the user saying when it ends, so the idle timer does not
        // get a vote.
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

    // Whether a decode will actually happen: asked for, and with a font able to
    // answer. Both halves matter — a missing bundled font is not an error, it just
    // means there is nothing to decode from.
    private bool IsDecoding => this.config.DecodeEffectEnabled && this.fonts.EorzeanDisplay != null;

    private static TimeSpan Longer(TimeSpan a, TimeSpan b) => a > b ? a : b;

    // --- Notification rendering --------------------------------------------

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

        // Cased here rather than when the notification was built, so toggling the
        // setting reaches the one already on screen — which is what makes the
        // config window's live preview answer the checkbox immediately. The
        // notification remembers the result; this only tells it what to hold.
        notification.ApplyCasing(this.config.UppercaseText);

        top = DrawHeader(notification, drawList, centerX, top, stroke, strokeDistance);

        DrawParticles(notification, drawList, centerX, top);

        DrawTextLine(notification, drawList, centerX, top, strokeDistance);
    }

    // Returns where the display line starts, which is below the header when there
    // is one and unchanged when there is not.
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

    // Skipped outright when there is nothing playing and nothing left over from an
    // effect just switched off. It is only a measurement and a font push, but it is
    // one of each on every frame of every notification, spent on a feature that is
    // off by default.
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

        // Drawn before the text, so glyphs stay legible with particles behind them.
        notification.Particles.Update(
            effect,
            this.config.ParticleDensity,
            ImGui.GetIO().DeltaTime,
            center,
            extent,
            spawning: !notification.IsFadingOut);

        notification.Particles.Draw(drawList, effect, this.config.ParticleColor, notification.Opacity);
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
            // With a decode still to come the glyphs that fly in are the cipher, on
            // the Eorzean font's own advances. Those advances are the reason the
            // font is a parameter: the Eorzean face is far wider than the display
            // one, and landing the cipher on display metrics would leave the decode
            // nothing to interpolate — the line would arrive already at its final
            // width and then resolve in place, which is the flat version of the
            // effect.
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

        // Settled: the one path with no per-glyph work at all, and the one a
        // notification spends most of its life on — the hold is longer than the
        // motion and the decode together.
        using (this.fonts.Display.Push())
            DrawStaticRun(drawList, notification.DisplayLayout, text, centerX, top, Tracking(), fill, stroke, strokeDistance);
    }

    // One run of glyphs mid-motion. GlyphAnimator decides what each glyph is doing;
    // this places it and picks its colour.
    //
    // Tracking and the font size are measured against the display face even when
    // the glyphs are drawn in the Eorzean one, so the two stages agree about how
    // far apart the glyphs sit and how far they travel.
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
                    glyphs[i],
                    GlyphPainter.Packed(color, opacity * state.Alpha),
                    GlyphPainter.Packed(this.config.StrokeColor, opacity * state.Alpha),
                    strokeDistance);
            }
        }
    }

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
    private void DrawDecodingRun(
        AreaNotification notification, ImDrawListPtr drawList, float centerX, float top, string text,
        float progress, uint fill, uint stroke, float strokeDistance)
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

    // A line at rest. Letter spacing is what decides how: without it ImGui can draw
    // the whole string in one call, and with it every glyph has to be placed.
    // The font must already be pushed.
    private void DrawStaticRun(
        ImDrawListPtr drawList, LineLayout cache, string text, float centerX, float top,
        float tracking, uint fill, uint stroke, float strokeDistance)
    {
        if (tracking > 0f)
            GlyphPainter.DrawRun(drawList, Positions(cache, text, tracking, centerX), text, top, fill, stroke, strokeDistance);
        else
            GlyphPainter.DrawCentered(drawList, centerX, top, text, fill, stroke, strokeDistance);
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

    // Extra advance between glyphs, in pixels, for whichever font is pushed right
    // now. Read from ImGui rather than from the configured size so the header —
    // built at its own size, from a different family — gets its own proportional
    // share of the one setting.
    //
    // GetTextLineHeight is the font's size: ImGui returns the same field from
    // both, and this file already leans on it for the line advance.
    private float Tracking() => ImGui.GetTextLineHeight() * (this.config.LetterSpacing / 100f);
}
