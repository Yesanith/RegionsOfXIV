using System;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Why a banner is being held back, for the debug preview window to show. The gate is otherwise
// only observable by walking around and noticing what fails to appear.
internal enum BannerBlock
{
    None,
    Disabled,
    LoggedOut,
    Cutscene,
    Pvp,
    GPose,
}

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

    // Banners answer to a narrower set of rules than anything else here, and each omission is
    // deliberate.
    //
    // Combat and duty are not consulted. Level Up! and Duty Commenced are *about* those states,
    // so honouring HideInCombat or HideInDuty would suppress precisely the banners worth having.
    //
    // BetweenAreas is not consulted either: Duty Commenced fires around a transition, which is
    // exactly when that flag is set.
    //
    // Nor is the global cooldown, and that one is worth spelling out. Refusing a banner does not
    // buy quiet, because the game draws its own regardless and the watcher only fades that out
    // when we are replacing it. A refusal therefore trades our version for theirs rather than for
    // silence, which is the wrong way round. Light Party arrives about two milliseconds after the
    // zone arrival that lets you into the duty, so under a cooldown it lost that trade every time.
    //
    // What is left is the unconditional set: a banner has no business drawing over a cutscene, in
    // PvP or in gpose.
    public bool ShouldAnnounceBanner() => BannerBlockReason() == BannerBlock.None;

    // The conditions live here rather than in ShouldAnnounceBanner so the debug preview can show
    // why a banner would be refused without restating the rules. A second copy in the UI would
    // drift, and a diagnostic that lies about the thing it diagnoses is worse than none.
    public BannerBlock BannerBlockReason()
    {
        if (!this.config.BannerNotificationEnabled)
            return BannerBlock.Disabled;

        if (!this.game.IsLoggedIn)
            return BannerBlock.LoggedOut;

        if (this.game.IsInCutscene)
            return BannerBlock.Cutscene;

        if (this.game.IsPvP)
            return BannerBlock.Pvp;

        if (this.game.IsGPosing)
            return BannerBlock.GPose;

        return BannerBlock.None;
    }

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

    // The global cooldown only. The finer and coarser windows and the recent-places memory are
    // all about location tiers, and a banner is not a location: muting a sub-area because a
    // Level Up! went past would be the wrong trade.
    //
    // So a banner opens this window without answering to it, and that asymmetry is the point. A
    // location notice held back by it has somewhere quiet to go. A banner does not.
    public void MarkBannerAnnounced(NotificationTiming timing) => HoldOff(this.now(), timing);

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
