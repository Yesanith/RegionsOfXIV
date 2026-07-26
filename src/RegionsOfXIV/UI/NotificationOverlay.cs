using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Full-screen, input-transparent draw surface for notifications. Registered with
// WindowSystem, which is both a D17 approval criterion and what gets us correct
// behaviour when the user hides the UI.
internal sealed class NotificationOverlay : Window, IDisposable
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

    public void Dispose() => this.active.Clear();

    public void Push(string? header, string text)
    {
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(header))
            return;

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

        notification.VanishStarted += PlayVanish;

        this.active.Add(notification);
        PlayReveal();
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

    // --- Sound (inlined from SoundService) ---------------------------------
    // Dalamud has no audio service. Rather than bundling audio and taking on NAudio
    // plus our own volume/device handling, we borrow the game's own UI sounds.
    // Off by default — unexpected audio is the fastest route to a one-star review.

    private unsafe void PlayReveal()
    {
        if (!this.config.RevealSoundEnabled || this.config.RevealSoundEffectId == 0)
            return;
        UIGlobals.PlaySoundEffect(this.config.RevealSoundEffectId);
    }

    private unsafe void PlayVanish()
    {
        if (!this.config.VanishSoundEnabled || this.config.VanishSoundEffectId == 0)
            return;
        UIGlobals.PlaySoundEffect(this.config.VanishSoundEffectId);
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

    // Eorzean -> Latin decode, the same trick the GW2 original uses: the bundled
    // font substitutes Latin codepoints 1:1, so the identical string renders as
    // Eorzean script in one font and plain text in the other.
    //
    // Glyphs swap individually rather than cross-fading, which avoids two
    // overlapping glyph shapes turning to mush; positions come from the Latin
    // layout so the line never shifts as it decodes.
    private void DrawDecodingLine(
        ImDrawListPtr drawList, float centerX, float top, string text, float progress, uint fill, uint stroke)
    {
        var eorzean = this.fonts.EorzeanDisplay;
        var useDecode = this.config.DecodeEffectEnabled
                        && eorzean != null
                        && progress < 1f
                        && IsCoveredByEorzeanFont(text);

        if (!useDecode)
        {
            using (this.fonts.Display.Push())
            {
                DrawStrokedCentered(
                    drawList, centerX, top, text, fill, stroke, ImGuiHelpers.GlobalScale);
            }

            return;
        }

        // The two faces have very different advance widths — the display face is
        // condensed, the Eorzean one is not — so laying Eorzean glyphs out on Latin
        // advances alone crowds them into each other. Measure both layouts and
        // interpolate: the line starts on Eorzean metrics and settles onto Latin
        // ones as it decodes. Both are centred, so it never drifts off-centre.
        var runes = MeasureGlyphs(eorzean!, text, centerX);
        var latin = MeasureGlyphs(this.fonts.Display, text, centerX);

        var decoded = (int)MathF.Round(progress * text.Length);

        using (eorzean!.Push())
        {
            for (var i = decoded; i < text.Length; i++)
                DrawGlyph(drawList, Lerp(runes[i], latin[i], progress), top, text[i], fill, stroke);
        }

        using (this.fonts.Display.Push())
        {
            for (var i = 0; i < decoded && i < text.Length; i++)
                DrawGlyph(drawList, Lerp(runes[i], latin[i], progress), top, text[i], fill, stroke);
        }
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

    // The font covers Latin and its accented forms only — no CJK. Skip the effect
    // rather than render a line of missing-glyph boxes on a JP/CN/KR client.
    private static bool IsCoveredByEorzeanFont(string text)
    {
        foreach (var c in text)
        {
            if (c > 'ɏ')
                return false;
        }

        return true;
    }
}
