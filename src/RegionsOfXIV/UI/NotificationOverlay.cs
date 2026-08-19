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

    private readonly Lane locations;
    private readonly Lane weather;

    private bool previewHeld;
    private PreviewSample held;

    public NotificationOverlay(Configuration config, FontService fonts)
        : base("##RegionsOfXIVOverlay")
    {
        this.config = config;
        this.renderer = new NotificationRenderer(config, fonts);

        this.locations = new Lane(this.renderer.Draw, animated: true);
        this.weather = new Lane(this.renderer.DrawWeather, animated: false);

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
        this.locations.Clear();
        this.weather.Clear();
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

        Spawn(this.locations, header, text);
    }

    public void PushWeather(string text)
    {
        if (this.previewHeld)
            return;

        Spawn(this.weather, null, text);
    }

    public void TouchPreview(PreviewSample sample)
    {
        Touch(this.locations, sample.Header, sample.Text);
        Touch(this.weather, null, WeatherPreview(sample));
    }

    public void PreviewOnce(PreviewSample sample)
    {
        if (!this.previewHeld)
        {
            Restart(this.locations, sample.Header, sample.Text);
            Restart(this.weather, null, WeatherPreview(sample));
            return;
        }

        Replay(this.locations, this.held.Header, this.held.Text);
        Replay(this.weather, null, WeatherPreview(this.held));
    }

    public void HoldPreview(bool held, PreviewSample sample)
    {
        this.previewHeld = held;
        this.held = sample;

        if (!held)
        {
            Release(this.locations);
            Release(this.weather);
            return;
        }

        HoldEach();
    }

    public override bool DrawConditions() => !this.locations.IsEmpty || !this.weather.IsEmpty;

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
        if (this.previewHeld)
        {
            // Weather can be switched on and off while a held preview is up, so the
            // lanes are reconciled every frame rather than only when a preview starts.
            HoldEach();
        }
        else
        {
            ReleaseIfIdle(this.locations);
            ReleaseIfIdle(this.weather);
        }

        Advance(this.locations);
        Advance(this.weather);
    }

    private string? WeatherPreview(in PreviewSample sample) =>
        this.config.WeatherNotificationEnabled ? sample.Weather : null;

    private void HoldEach()
    {
        Hold(this.locations, this.held.Header, this.held.Text);
        Hold(this.weather, null, WeatherPreview(this.held));
    }

    private static void Advance(Lane lane)
    {
        for (var i = lane.Items.Count - 1; i >= 0; i--)
        {
            var notification = lane.Items[i];
            notification.Update();

            if (notification.IsDone)
            {
                lane.Items.RemoveAt(i);
                continue;
            }

            lane.Draw(notification);
        }
    }

    private AreaNotification? Spawn(Lane lane, string? header, string text)
    {
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(header))
            return null;

        foreach (var existing in lane.Items)
        {
            existing.StackOffset += StackSpacing;
            existing.Dismiss();
        }

        var notification = new AreaNotification(
            header,
            text,
            this.config.FadeInDuration,
            lane.Animated && this.config.Motion != MotionEffect.None
                ? this.config.MotionDuration
                : TimeSpan.Zero,
            lane.Animated && this.renderer.IsDecoding ? this.config.RevealDuration : TimeSpan.Zero,
            this.config.ShowDuration,
            this.config.FadeOutDuration);

        lane.Items.Add(notification);
        lane.Preview = null;

        return notification;
    }

    private void SpawnPreview(Lane lane, string? header, string text)
    {
        if (Spawn(lane, header, text) is not { } notification)
            return;

        notification.IsPinned = true;

        lane.Preview = notification;
        lane.PreviewTouchedAt = DateTime.UtcNow;
    }

    private void Restart(Lane lane, string? header, string? text)
    {
        if (text is null)
        {
            Release(lane);
            return;
        }

        Spawn(lane, header, text);
    }

    private void Replay(Lane lane, string? header, string? text)
    {
        if (text is null)
        {
            Release(lane);
            return;
        }

        if (lane.Preview is { } current)
            lane.Items.Remove(current);

        SpawnPreview(lane, header, text);
    }

    private void Touch(Lane lane, string? header, string? text)
    {
        if (text is null)
        {
            Release(lane);
            return;
        }

        lane.PreviewTouchedAt = DateTime.UtcNow;

        if (lane.Preview is { IsDone: false } existing)
        {
            existing.IsPinned = true;
            return;
        }

        SpawnPreview(lane, header, text);
    }

    private void Hold(Lane lane, string? header, string? text)
    {
        if (text is null)
        {
            Release(lane);
            return;
        }

        if (lane.Preview is not { IsDone: false })
            SpawnPreview(lane, header, text);
    }

    private static void Release(Lane lane)
    {
        if (lane.Preview is { } current)
            current.IsPinned = false;

        lane.Preview = null;
    }

    private static void ReleaseIfIdle(Lane lane)
    {
        if (lane.Preview is not { } current)
            return;

        if (current.IsDone)
        {
            lane.Preview = null;
            return;
        }

        if (DateTime.UtcNow - lane.PreviewTouchedAt < PreviewLinger)
            return;

        current.IsPinned = false;
        lane.Preview = null;
    }

    private static TimeSpan Longer(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private sealed class Lane(Action<AreaNotification> draw, bool animated)
    {
        public readonly List<AreaNotification> Items = [];

        public readonly Action<AreaNotification> Draw = draw;

        public readonly bool Animated = animated;

        public AreaNotification? Preview;

        public DateTime PreviewTouchedAt;

        public bool IsEmpty => this.Items.Count == 0;

        public void Clear()
        {
            this.Items.Clear();
            this.Preview = null;
        }
    }
}
