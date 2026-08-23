using System;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Every rule about staying quiet, in one place. Three separate suppression windows overlap here:
//
//   nextAllowed         nothing at all until the current notice has been readable for a moment
//   suppressFinerUntil  a zone announcement mutes the smaller areas inside it
//   suppressCoarseUntil the reverse, so a sub-area does not get stomped by its own parent
//
// plus a short memory of recent places, which is what stops a notification flickering when you
// walk back and forth across a boundary.
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

    // Deliberately does not consult IsBlockedByGameState. A zone entry is decided during the
    // loading screen, when BetweenAreas and friends are all set, so those checks would refuse
    // every arrival there is.
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
        if (!CanAnnounceAnyArea())
            return false;

        var now = this.now();

        if (now < this.nextAllowed)
            return false;

        return now >= this.suppressFinerUntil;
    }

    public bool ShouldAnnounceSanctuary() =>
        CanAnnounceAnyArea() && this.now() >= this.nextAllowed;

    public bool ShouldAnnounceWeather() =>
        this.config.WeatherNotificationEnabled && !IsBlockedByGameState();

    private bool CanAnnounceAnyArea() =>
        (this.config.AreaNotificationEnabled || this.config.SubAreaNotificationEnabled)
        && !IsBlockedByGameState();

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

    // A small ring buffer rather than a set: it needs to forget, and the window is short enough
    // that a linear scan of six entries costs nothing.
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
