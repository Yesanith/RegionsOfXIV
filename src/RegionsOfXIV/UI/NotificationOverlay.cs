using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Two independent stacks of notifications share one full-screen, click-through window: place
// names in one lane and weather in the other, so a weather change never displaces an arrival.
//
// On top of that sits the config-window preview, which is the same machinery driven by hand.
// Four entry points feed it and the difference between them matters -- see the note above Touch.
internal sealed class NotificationOverlay : Window, IDisposable, INotificationSink
{
    // Proportional to the line's own size rather than the flat 46px it used to be: at the default
    // 91px display font that pushed the outgoing line only halfway clear of the incoming one, so
    // the two overlapped exactly where they were both still legible. One line's worth of the font
    // being drawn puts them clear of each other whatever size someone is running.
    private const float StackSpacingRatio = 1f;

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

        this.locations = new Lane(
            this.renderer.Draw, () => config.DisplayFontSize * StackSpacingRatio);

        this.weather = new Lane(
            this.renderer.DrawWeather, () => config.WeatherFontSize * StackSpacingRatio);

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

    // Called on every frame a setting is being changed. Keeps one preview alive and lets the
    // renderer pick up the new colours, rather than restarting the animation on each keystroke.
    public void TouchPreview(PreviewSample sample)
    {
        Touch(this.locations, LocationLine(sample));
        Touch(this.weather, WeatherLine(sample));
    }

    // The "Preview" button, and anything that wants the animation played from the top. While
    // editing mode holds a preview on screen the pinned one is swapped out instead, so the
    // button still does something visible rather than being swallowed by the held preview.
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

    // Editing mode. While held, Draw keeps re-asserting the preview every frame so it never
    // ages out, and real announcements are dropped in Push so nothing fights over the screen.
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

    // Goes through the same Configuration.HeaderFor as a real arrival. Building the preview
    // line by hand here is what let "Show header" stop working in the config window while it
    // still worked in game.
    private Line LocationLine(in PreviewSample sample) =>
        new(this.config.HeaderFor(sample.Header, sample.Text), sample.Text);

    private Line? WeatherLine(in PreviewSample sample) =>
        this.config.WeatherNotificationEnabled
            ? new Line(null, sample.Weather, sample.WeatherIcon)
            : null;

    private void HoldEach()
    {
        Hold(this.locations, LocationLine(this.held));
        Hold(this.weather, WeatherLine(this.held));
    }

    // Backwards so a notification finishing its fade can be removed without disturbing the
    // indices of the ones still to be drawn this frame.
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

    // Anything already on screen is pushed down and told to fade, so an arrival that lands on
    // top of the previous one reads as a replacement rather than a pile-up.
    private AreaNotification? Spawn(Lane lane, Line line)
    {
        if (string.IsNullOrWhiteSpace(line.Text) && string.IsNullOrWhiteSpace(line.Header))
            return null;

        var spacing = lane.Spacing();

        foreach (var existing in lane.Items)
        {
            existing.PushDown(spacing);
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
        lane.PreviewLine = line;
        lane.PreviewTouchedAt = DateTime.UtcNow;
    }

    // Removes the old preview outright rather than letting Spawn fade it out, so changing a
    // setting does not leave a ghost of the previous wording sliding down the screen.
    private void Recreate(Lane lane, Line line)
    {
        if (lane.Preview is { } current)
            lane.Items.Remove(current);

        SpawnPreview(lane, line);
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

        Recreate(lane, content);
    }

    // The four preview verbs, easy to confuse:
    //
    //   Touch    keep the current preview, restart only if the wording changed (live editing)
    //   Hold     make sure a preview exists, every frame, for as long as editing mode is on
    //   Restart  play it from the top, no pinning (the Preview button, editing mode off)
    //   Replay   play it from the top and re-pin it (the Preview button, editing mode on)
    //
    // Touch and Hold both compare against Lane.PreviewLine before reusing what is on screen.
    // Without that check they re-pinned the existing AreaNotification and returned, so toggling
    // a setting that changes the wording -- "Show header" being the obvious one -- did nothing
    // visible until editing mode was switched off and on again.
    private void Touch(Lane lane, Line? line)
    {
        if (line is not { } content)
        {
            Release(lane);
            return;
        }

        lane.PreviewTouchedAt = DateTime.UtcNow;

        if (lane.Preview is { IsDone: false } existing && lane.PreviewLine == content)
        {
            existing.IsPinned = true;
            return;
        }

        Recreate(lane, content);
    }

    private void Hold(Lane lane, Line? line)
    {
        if (line is not { } content)
        {
            Release(lane);
            return;
        }

        if (lane.Preview is { IsDone: false } && lane.PreviewLine == content)
            return;

        Recreate(lane, content);
    }

    private static void Release(Lane lane)
    {
        if (lane.Preview is { } current)
            current.IsPinned = false;

        lane.Preview = null;
        lane.PreviewLine = null;
    }

    // A preview left alone for PreviewLinger stops being pinned and is allowed to fade like any
    // other notification. That is what makes the preview disappear on its own once the sliders
    // stop moving, without editing mode having to be involved.
    private static void ReleaseIfIdle(Lane lane)
    {
        if (lane.Preview is not { } current)
            return;

        if (current.IsDone)
        {
            lane.Preview = null;
            lane.PreviewLine = null;
            return;
        }

        if (DateTime.UtcNow - lane.PreviewTouchedAt < PreviewLinger)
            return;

        current.IsPinned = false;
        lane.Preview = null;
    }

    private static TimeSpan Longer(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private readonly record struct Line(string? Header, string Text, uint Icon = 0);

    // One stack of notifications plus whichever of them is currently standing in as the preview.
    // PreviewLine is the wording that produced Preview, kept so Touch and Hold can tell a
    // cosmetic change (recolour, keep it) from a content change (rebuild it).
    private sealed class Lane(Action<AreaNotification> draw, Func<float> spacing)
    {
        public readonly List<AreaNotification> Items = [];

        public readonly Action<AreaNotification> Draw = draw;

        // How far the lane pushes what is already on screen when something new lands. A function
        // rather than a value because it follows the font size, which changes under the sliders.
        public readonly Func<float> Spacing = spacing;

        public AreaNotification? Preview;

        public Line? PreviewLine;

        public DateTime PreviewTouchedAt;

        public bool IsEmpty => this.Items.Count == 0;

        public void Clear()
        {
            this.Items.Clear();
            this.Preview = null;
            this.PreviewLine = null;
        }
    }
}
