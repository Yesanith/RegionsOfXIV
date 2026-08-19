using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed class NotificationOverlay : Window, IDisposable, INotificationSink
{
    private const float StackSpacing = 46f;

    private static readonly TimeSpan PreviewLinger = TimeSpan.FromMilliseconds(250);

    private readonly Configuration config;
    private readonly NotificationRenderer renderer;

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
        this.renderer = new NotificationRenderer(config, fonts);

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

    public TimeSpan EstimatedDuration =>
        (this.config.Motion == MotionEffect.None
            ? this.config.FadeInDuration
            : Longer(this.config.FadeInDuration, this.config.MotionDuration))
        + TimeSpan.FromMilliseconds(200)
        + (this.renderer.IsDecoding ? this.config.RevealDuration : TimeSpan.Zero)
        + this.config.ShowDuration
        + this.config.FadeOutDuration;

    public void Push(string? header, string text)
    {
        if (this.previewHeld)
            return;

        Spawn(header, text);
    }

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

            this.renderer.Draw(notification);
        }
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
            this.renderer.IsDecoding ? this.config.RevealDuration : TimeSpan.Zero,
            this.config.ShowDuration,
            this.config.FadeOutDuration);

        this.active.Add(notification);
        return notification;
    }

    private void SpawnPreview(string? header, string text)
    {
        if (Spawn(header, text) is not { } notification)
            return;

        this.preview = notification;
        this.preview.IsPinned = true;
        this.previewTouchedAt = DateTime.UtcNow;
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

    private static TimeSpan Longer(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
