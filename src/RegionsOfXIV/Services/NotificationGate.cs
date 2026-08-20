using System;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

internal sealed class NotificationGate
{
    private static readonly TimeSpan GlobalCooldown = TimeSpan.FromMilliseconds(2000);

    private static readonly TimeSpan PingPongWindow = TimeSpan.FromSeconds(30);

    private const int RecentPlaces = 6;

    private const float TravellingSpeed = 20f;

    private readonly IGateSettings config;
    private readonly IGameState game;

    private readonly Func<DateTime> now;

    private DateTime nextAllowed = DateTime.MinValue;

    private DateTime suppressFinerUntil = DateTime.MinValue;

    private DateTime suppressCoarseUntil = DateTime.MinValue;

    private readonly (LocationSnapshot Place, DateTime At)[] recent =
        new (LocationSnapshot, DateTime)[RecentPlaces];

    private int nextSlot;

    public NotificationGate(IGateSettings config, IGameState game, Func<DateTime>? clock = null)
    {
        this.config = config;
        this.game = game;
        this.now = clock ?? (() => DateTime.UtcNow);
    }

    public void Reset()
    {
        this.nextAllowed = DateTime.MinValue;
        this.suppressFinerUntil = DateTime.MinValue;
        this.suppressCoarseUntil = DateTime.MinValue;

        Array.Clear(this.recent);
        this.nextSlot = 0;
    }

    public bool ShouldAnnounceZoneEntry(bool destinationIsPvp, bool destinationIsDuty)
    {
        if (!this.config.HideNativeLoadingTitle)
            return false;

        if (destinationIsPvp)
            return false;

        if (this.config.HideInDuty && destinationIsDuty)
            return false;

        return true;
    }

    public bool ShouldAnnounceNativeAreaText()
    {
        if (!this.config.AreaNotificationEnabled && !this.config.SubAreaNotificationEnabled)
            return false;

        if (IsBlockedByGameState())
            return false;

        var now = this.now();

        if (now < this.nextAllowed)
            return false;

        return now >= this.suppressFinerUntil;
    }

    public bool ShouldAnnounceSanctuary()
    {
        if (!this.config.AreaNotificationEnabled && !this.config.SubAreaNotificationEnabled)
            return false;

        if (IsBlockedByGameState())
            return false;

        return this.now() >= this.nextAllowed;
    }

    public void MarkZoneAnnounced(NotificationTiming timing)
    {
        var now = this.now();

        HoldOff(now, timing);

        this.suppressFinerUntil = now + timing.OnScreen;
        this.suppressCoarseUntil = now + timing.OnScreen;
    }

    public bool ShouldAnnounce(
        in LocationSnapshot previous, in LocationSnapshot current, LocationTier tier, float speed)
    {
        if (tier == LocationTier.None)
            return false;

        if (!IsTierEnabled(tier))
            return false;

        if (IsBlockedByGameState())
            return false;

        var now = this.now();

        if (now < this.nextAllowed)
            return false;

        if (tier > LocationTier.Place && now < this.suppressFinerUntil)
            return false;

        if (tier <= LocationTier.Place && now < this.suppressCoarseUntil)
            return false;

        if (AnnouncedRecently(current, now))
            return false;

        if (tier == LocationTier.SubArea &&
            this.config.HideWhileTravellingFast &&
            speed >= TravellingSpeed)
            return false;

        return true;
    }

    public void MarkAnnounced(in LocationSnapshot announced, LocationTier tier, NotificationTiming timing)
    {
        var now = this.now();

        HoldOff(now, timing);

        this.recent[this.nextSlot] = (announced, now);
        this.nextSlot = (this.nextSlot + 1) % RecentPlaces;

        if (tier <= LocationTier.Place)
            this.suppressFinerUntil = now + timing.OnScreen;
    }

    private void HoldOff(DateTime now, NotificationTiming timing) =>
        this.nextAllowed = now + (timing.UntilReadable > GlobalCooldown
            ? timing.UntilReadable
            : GlobalCooldown);

    private bool AnnouncedRecently(in LocationSnapshot current, DateTime now)
    {
        foreach (var (place, at) in this.recent)
        {
            if (at == default || now - at >= PingPongWindow)
                continue;

            if (place == current)
                return true;
        }

        return false;
    }

    private bool IsTierEnabled(LocationTier tier) => tier switch
    {
        LocationTier.Territory or LocationTier.Region or LocationTier.Zone or LocationTier.Place
            => this.config.ZoneNotificationEnabled,
        LocationTier.Area => this.config.AreaNotificationEnabled,
        LocationTier.SubArea => this.config.SubAreaNotificationEnabled,
        _ => false,
    };

    public bool ShouldAnnounceWeather()
    {
        if (!this.config.WeatherNotificationEnabled)
            return false;

        return !IsBlockedByGameState();
    }

    private bool IsBlockedByGameState()
    {
        if (!this.game.IsLoggedIn)
            return true;

        if (this.game.IsBetweenAreas)
            return true;

        if (this.game.IsInCutscene)
            return true;

        if (this.game.IsPvP)
            return true;

        if (this.game.IsGPosing)
            return true;

        if (this.config.HideInCombat && this.game.IsInCombat)
            return true;

        if (this.config.HideInDuty && this.game.IsBoundByDuty)
            return true;

        return false;
    }
}
