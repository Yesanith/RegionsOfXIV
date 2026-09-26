using System;
using System.Numerics;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// The live preview inside the settings window: the real renderer, painting real notifications
// onto a canvas the window provides. Three slots, one per lane, pinned so they never fade, and
// rebuilt whenever the wording they show changes.
//
// Separate from the overlay's preview verbs on purpose. The overlay draws on the game screen and
// is what the position sliders and the pin are for; this draws where the player is looking.
//
// A second world can stand in for the live one while a preset card is hovered: its own renderer
// over a scratch configuration and its own slots, so the hover never disturbs what is live. The
// fonts are shared, so a preset's faces are not previewed, only what it does with them.
internal sealed class PreviewStage
{
    private readonly FontService fonts;
    private readonly World live;

    private World? scratch;

    public PreviewStage(Configuration config, FontService fonts)
    {
        this.fonts = fonts;
        this.live = new World(config, fonts);
    }

    private World Active => this.scratch ?? this.live;

    public void Override(Configuration? settings)
    {
        if (settings is null)
        {
            this.scratch = null;
            return;
        }

        if (this.scratch is null || !ReferenceEquals(this.scratch.Config, settings))
            this.scratch = new World(settings, this.fonts);
    }

    public void Replay() => Active.Replay();

    public float AboveAnchor() => Active.AboveAnchor();

    public float BelowAnchor() => Active.BelowAnchor();

    public void Draw(Vector2 origin, Vector2 size, float anchorY, in PreviewSample sample) =>
        Active.Draw(origin, size, anchorY, sample);

    private sealed class Slot
    {
        public AreaNotification? Notification;
        public string? Header;
        public string? Text;
        public uint Icon;
    }

    private sealed class World(Configuration config, FontService fonts)
    {
        // How much taller the weather icon is than its line, and the gap under an underlined
        // header, both as the renderer draws them.
        private const float WeatherIconOverhang = 0.15f;
        private const float UnderlinedWeatherGap = 1.7f;

        private readonly NotificationRenderer renderer = new(config, fonts);

        private readonly Slot location = new();
        private readonly Slot weather = new();
        private readonly Slot banner = new();

        public Configuration Config => config;

        public void Replay()
        {
            this.location.Notification = null;
            this.weather.Notification = null;
            this.banner.Notification = null;
        }

        // Room the lines need above the anchor, in unscaled pixels: the weather line and its gap.
        public float AboveAnchor()
        {
            if (!config.WeatherNotificationEnabled)
                return 0f;

            var gap = config.UnderlineHeader ? MathF.Max(config.HeaderGap, UnderlinedWeatherGap) : config.HeaderGap;
            var overhang = config.ShowWeatherIcon ? WeatherIconOverhang : 0f;

            return config.WeatherFontSize * (gap + overhang);
        }

        // Room the lines need below the anchor: the header, its gap and the name, or the banner
        // if its drop puts it lower.
        public float BelowAnchor()
        {
            var header = config.IncludeParentTierAsHeader ? config.HeaderFontSize * config.HeaderGap : 0f;
            var below = header + config.DisplayFontSize;

            if (config.BannerNotificationEnabled)
                below = MathF.Max(below, config.DisplayFontSize * (config.BannerGap + 1f));

            return below;
        }

        public void Draw(Vector2 origin, Vector2 size, float anchorY, in PreviewSample sample)
        {
            var anchor = new Vector2(origin.X + (size.X * (config.HorizontalPosition / 100f)), anchorY);
            var canvas = new Canvas(origin, size, anchor);

            Ensure(this.location, config.HeaderFor(sample.Header, sample.Text), sample.Text, 0u);
            Ensure(this.weather, null, config.WeatherNotificationEnabled ? sample.Weather : null, sample.WeatherIcon);
            Ensure(this.banner, null, config.BannerNotificationEnabled ? sample.Banner : null, 0u);

            if (Advance(this.location) is { } place)
                this.renderer.Draw(place, canvas);

            if (Advance(this.weather) is { } sky)
                this.renderer.DrawWeather(sky, canvas);

            if (Advance(this.banner) is { } sign)
                this.renderer.DrawBanner(sign, canvas);
        }

        private void Ensure(Slot slot, string? header, string? text, uint icon)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                slot.Notification = null;
                return;
            }

            if (slot.Notification is { IsDone: false } && slot.Header == header && slot.Text == text && slot.Icon == icon)
                return;

            slot.Header = header;
            slot.Text = text;
            slot.Icon = icon;
            slot.Notification = new AreaNotification(
                header,
                text,
                config.FadeInDuration,
                config.Motion != MotionEffect.None ? config.MotionDuration : TimeSpan.Zero,
                this.renderer.IsDecoding ? config.RevealDuration : TimeSpan.Zero,
                config.ShowDuration,
                config.FadeOutDuration)
            {
                IconId = icon,
                IsPinned = true,
            };
        }

        private static AreaNotification? Advance(Slot slot)
        {
            if (slot.Notification is not { } notification)
                return null;

            notification.Update();
            if (!notification.IsDone)
                return notification;

            slot.Notification = null;
            return null;
        }
    }
}
