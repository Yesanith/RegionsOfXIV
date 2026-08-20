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

        this.locations = new Lane(this.renderer.Draw);
        this.weather = new Lane(this.renderer.DrawWeather);

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

    public NotificationTiming Timing
    {
        get
        {
            // The motion runs alongside the fade in, so arriving takes the longer of the two.
            var arriving = this.config.Motion == MotionEffect.None
                ? this.config.FadeInDuration
                : Longer(this.config.FadeInDuration, this.config.MotionDuration);

            var readable = arriving
                           + AreaNotification.HoldDuration
                           + (this.renderer.IsDecoding ? this.config.RevealDuration : TimeSpan.Zero);

            return new NotificationTiming(
                readable,
                readable + this.config.ShowDuration + this.config.FadeOutDuration);
        }
    }

    public void Push(string? header, string text)
    {
        if (this.previewHeld)
            return;

        Spawn(this.locations, new Line(header, text));
    }

    public void PushWeather(string text, uint iconId)
    {
        if (this.previewHeld)
            return;

        Spawn(this.weather, new Line(null, text, iconId));
    }

    public void TouchPreview(PreviewSample sample)
    {
        Touch(this.locations, LocationLine(sample));
        Touch(this.weather, WeatherLine(sample));
    }

    public void PreviewOnce(PreviewSample sample)
    {
        if (!this.previewHeld)
        {
            Restart(this.locations, LocationLine(sample));
            Restart(this.weather, WeatherLine(sample));
            return;
        }

        Replay(this.locations, LocationLine(this.held));
        Replay(this.weather, WeatherLine(this.held));
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

    private static Line LocationLine(in PreviewSample sample) =>
        new(sample.Header, sample.Text);

    private Line? WeatherLine(in PreviewSample sample) =>
        this.config.WeatherNotificationEnabled
            ? new Line(null, sample.Weather, sample.WeatherIcon)
            : null;

    private void HoldEach()
    {
        Hold(this.locations, LocationLine(this.held));
        Hold(this.weather, WeatherLine(this.held));
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

    private AreaNotification? Spawn(Lane lane, Line line)
    {
        if (string.IsNullOrWhiteSpace(line.Text) && string.IsNullOrWhiteSpace(line.Header))
            return null;

        foreach (var existing in lane.Items)
        {
            existing.StackOffset += StackSpacing;
            existing.Dismiss();
        }

        var notification = new AreaNotification(
            line.Header,
            line.Text,
            this.config.FadeInDuration,
            this.config.Motion != MotionEffect.None ? this.config.MotionDuration : TimeSpan.Zero,
            this.renderer.IsDecoding ? this.config.RevealDuration : TimeSpan.Zero,
            this.config.ShowDuration,
            this.config.FadeOutDuration)
        {
            IconId = line.Icon,
        };

        lane.Items.Add(notification);
        lane.Preview = null;

        return notification;
    }

    private void SpawnPreview(Lane lane, Line line)
    {
        if (Spawn(lane, line) is not { } notification)
            return;

        notification.IsPinned = true;

        lane.Preview = notification;
        lane.PreviewTouchedAt = DateTime.UtcNow;
    }

    private void Restart(Lane lane, Line? line)
    {
        if (line is not { } content)
        {
            Release(lane);
            return;
        }

        Spawn(lane, content);
    }

    private void Replay(Lane lane, Line? line)
    {
        if (line is not { } content)
        {
            Release(lane);
            return;
        }

        if (lane.Preview is { } current)
            lane.Items.Remove(current);

        SpawnPreview(lane, content);
    }

    private void Touch(Lane lane, Line? line)
    {
        if (line is not { } content)
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

        SpawnPreview(lane, content);
    }

    private void Hold(Lane lane, Line? line)
    {
        if (line is not { } content)
        {
            Release(lane);
            return;
        }

        if (lane.Preview is not { IsDone: false })
            SpawnPreview(lane, content);
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

    /// <summary>What one notification says. A null Line means the lane has nothing to show.</summary>
    private readonly record struct Line(string? Header, string Text, uint Icon = 0);

    private sealed class Lane(Action<AreaNotification> draw)
    {
        public readonly List<AreaNotification> Items = [];

        public readonly Action<AreaNotification> Draw = draw;

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
