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

    // ImGui has no text outline, so the stroke is built by stamping the glyph run
    // around the origin before drawing the fill on top. 8-way gives a cleaner edge
    // than 4-way.
    private static readonly Vector2[] StrokeOffsets =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    ];

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

        var notification = new AreaNotification(
            header,
            text,
            this.config.FadeInDuration,
            this.config.RevealDuration,
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

    public TimeSpan EstimatedDuration =>
        this.config.FadeInDuration
        + TimeSpan.FromMilliseconds(200)
        + this.config.RevealDuration
        + this.config.ShowDuration
        + this.config.FadeOutDuration;

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

    // --- Text rendering (inlined from TextPainter) -------------------------

    private static void DrawStroked(
        ImDrawListPtr drawList,
        Vector2 position,
        string text,
        uint fillColor,
        uint strokeColor,
        float strokeDistance = 1f)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (strokeDistance > 0f)
        {
            foreach (var offset in StrokeOffsets)
                drawList.AddText(position + (offset * strokeDistance), strokeColor, text);
        }

        drawList.AddText(position, fillColor, text);
    }

    private static void DrawStrokedCentered(
        ImDrawListPtr drawList,
        float centerX,
        float top,
        string text,
        uint fillColor,
        uint strokeColor,
        float strokeDistance = 1f)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var size = ImGui.CalcTextSize(text);
        DrawStroked(drawList, new Vector2(centerX - (size.X / 2f), top), text, fillColor, strokeColor, strokeDistance);
    }

    // Drawn as two halves so it can animate outward from the centre, and drawn
    // before the header text so descenders and serifs are not overdrawn.
    private static void DrawUnderline(
        ImDrawListPtr drawList,
        float centerX,
        float y,
        float fullWidth,
        float progress,
        uint fillColor,
        uint borderColor,
        float thickness = 2f)
    {
        var width = fullWidth * float.Clamp(progress, 0f, 1f);
        if (width <= 0f)
            return;

        var half = width / 2f;

        drawList.AddRectFilled(
            new Vector2(centerX - half - 1, y - 1),
            new Vector2(centerX + half + 1, y + thickness + 1),
            borderColor);

        drawList.AddRectFilled(
            new Vector2(centerX - half, y),
            new Vector2(centerX + half, y + thickness),
            fillColor);
    }

    // --- Notification rendering --------------------------------------------

    private void DrawOne(AreaNotification notification)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        var centerX = viewport.Pos.X + (viewport.Size.X / 2f);
        var top = viewport.Pos.Y
                  + (viewport.Size.Y * (this.config.VerticalPosition / 100f))
                  + (notification.StackOffset * ImGuiHelpers.GlobalScale);

        var fill = Packed(this.config.TextColor, notification.Opacity);
        var headerFill = Packed(this.config.HeaderColor, notification.Opacity);
        var stroke = Packed(this.config.StrokeColor, notification.Opacity);

        if (!string.IsNullOrWhiteSpace(notification.Header))
        {
            using (this.fonts.Header.Push())
            {
                var headerWidth = ImGui.CalcTextSize(notification.Header).X;

                if (this.config.UnderlineHeader)
                {
                    var underlineY = top + ImGui.GetTextLineHeight() + (4f * ImGuiHelpers.GlobalScale);
                    DrawUnderline(
                        drawList, centerX, underlineY, headerWidth,
                        notification.RevealProgress, headerFill, stroke,
                        2f * ImGuiHelpers.GlobalScale);
                }

                DrawStrokedCentered(
                    drawList, centerX, top, notification.Header!, headerFill, stroke,
                    ImGuiHelpers.GlobalScale);

                top += ImGui.GetTextLineHeight() * (this.config.OverlapHeader ? 1.1f : 1.6f);
            }
        }

        DrawDecodingLine(drawList, centerX, top, notification.Text, notification.RevealProgress, fill, stroke);
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
    private void DrawDecodingLine(
        ImDrawListPtr drawList, float centerX, float top, string text, float progress, uint fill, uint stroke)
    {
        var eorzean = this.fonts.EorzeanDisplay;
        var useDecode = this.config.DecodeEffectEnabled && eorzean != null && progress < 1f;

        if (!useDecode)
        {
            using (this.fonts.Display.Push())
            {
                DrawStrokedCentered(
                    drawList, centerX, top, text, fill, stroke, ImGuiHelpers.GlobalScale);
            }

            return;
        }

        var cipher = BuildCipher(text);

        // The two faces have very different advance widths — the display face is
        // condensed, the Eorzean one is not, and a full-width Japanese glyph is
        // wider than either — so laying one out on the other's advances crowds or
        // strands the glyphs. Measure both layouts and interpolate: the line starts
        // on Eorzean metrics and settles onto the real ones as it decodes. Both are
        // centred, so it grows symmetrically rather than drifting.
        var runes = MeasureGlyphs(eorzean!, cipher, centerX);
        var plain = MeasureGlyphs(this.fonts.Display, text, centerX);

        var decoded = (int)MathF.Round(progress * text.Length);

        using (eorzean!.Push())
        {
            for (var i = decoded; i < text.Length; i++)
                DrawGlyph(drawList, Lerp(runes[i], plain[i], progress), top, cipher[i], fill, stroke);
        }

        using (this.fonts.Display.Push())
        {
            for (var i = 0; i < decoded && i < text.Length; i++)
                DrawGlyph(drawList, Lerp(runes[i], plain[i], progress), top, text[i], fill, stroke);
        }
    }

    // What the Eorzean half draws: the text itself wherever the font covers it, and
    // a stand-in letter wherever it does not. Same length as the input, so the two
    // layouts stay index-aligned and a glyph's scrambled and resolved forms occupy
    // the same slot.
    //
    // The substitution is derived from the character and its position rather than
    // from a random source, so it is stable frame to frame. A fresh draw would make
    // the undecoded half flicker, which reads as a rendering fault rather than as
    // an effect.
    private static string BuildCipher(string text)
    {
        Span<char> cipher = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            cipher[i] = IsCoveredByEorzeanFont(c)
                ? c
                : CipherAlphabet[((c * 31) + i) % CipherAlphabet.Length];
        }

        return new string(cipher);
    }

    // Absolute X of every glyph, laid out in the given font and centred on centerX.
    private static float[] MeasureGlyphs(IFontHandle font, string text, float centerX)
    {
        using (font.Push())
        {
            var xs = new float[text.Length];
            for (var i = 0; i < text.Length; i++)
                xs[i] = ImGui.CalcTextSize(text[..i]).X;

            var left = centerX - (ImGui.CalcTextSize(text).X / 2f);
            for (var i = 0; i < xs.Length; i++)
                xs[i] += left;

            return xs;
        }
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * t);

    private static uint Packed(Vector4 color, float alpha) =>
        ImGui.ColorConvertFloat4ToU32(color with { W = color.W * alpha });

    private static void DrawGlyph(
        ImDrawListPtr drawList, float x, float y, char glyph, uint fill, uint stroke)
    {
        if (glyph == ' ')
            return;

        DrawStroked(
            drawList, new Vector2(x, y), glyph.ToString(), fill, stroke, ImGuiHelpers.GlobalScale);
    }

    // U+024F, the last codepoint of Latin Extended-B, and the end of the bundled
    // Eorzean font's coverage. Its 240 glyphs run from ASCII through the accented
    // Latin forms and stop there — no CJK, no Cyrillic, no Greek. Anything past
    // this point has no Eorzean form and needs a stand-in during the reveal.
    private const char EorzeanCoverageEnd = 'ɏ';

    // Letters the stand-in is drawn from. Uppercase only: the Eorzean uppercase
    // forms are the more ornate ones, and they are closer in width to a full-width
    // Japanese glyph than the lowercase, which keeps the line from having to grow
    // too far as it decodes.
    private const string CipherAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static bool IsCoveredByEorzeanFont(char c) => c <= EorzeanCoverageEnd;
}
