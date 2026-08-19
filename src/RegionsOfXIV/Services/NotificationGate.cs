using System;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

internal sealed class NotificationGate
{
    private static readonly TimeSpan GlobalCooldown = TimeSpan.FromMilliseconds(2000);

    private static readonly TimeSpan PingPongWindow = TimeSpan.FromSeconds(30);

    private const float TravellingSpeed = 20f;

    private readonly IGateSettings config;
    private readonly IGameState game;

    private readonly Func<DateTime> now;

    private DateTime lastNotification = DateTime.MinValue;

    private DateTime suppressFinerUntil = DateTime.MinValue;

    private DateTime suppressCoarseUntil = DateTime.MinValue;

    private LocationSnapshot lastAnnounced = LocationSnapshot.Empty;
    private LocationSnapshot secondLastAnnounced = LocationSnapshot.Empty;

    public NotificationGate(IGateSettings config, IGameState game, Func<DateTime>? clock = null)
    {
        this.config = config;
        this.game = game;
        this.now = clock ?? (() => DateTime.UtcNow);
    }

    public void Reset()
    {
        this.lastNotification = DateTime.MinValue;
        this.suppressFinerUntil = DateTime.MinValue;
        this.suppressCoarseUntil = DateTime.MinValue;
        this.lastAnnounced = LocationSnapshot.Empty;
        this.secondLastAnnounced = LocationSnapshot.Empty;
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

        if (now - this.lastNotification < GlobalCooldown)
            return false;

        return now >= this.suppressFinerUntil;
    }

    public bool ShouldAnnounceSanctuary()
    {
        if (!this.config.AreaNotificationEnabled && !this.config.SubAreaNotificationEnabled)
            return false;

        if (IsBlockedByGameState())
            return false;

        return this.now() - this.lastNotification >= GlobalCooldown;
    }

    public void MarkZoneAnnounced(TimeSpan onScreenDuration)
    {
        var now = this.now();

        this.lastNotification = now;
        this.suppressFinerUntil = now + onScreenDuration;
        this.suppressCoarseUntil = now + onScreenDuration;
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

        if (now - this.lastNotification < GlobalCooldown)
            return false;

        if (tier > LocationTier.Place && now < this.suppressFinerUntil)
            return false;

        if (tier <= LocationTier.Place && now < this.suppressCoarseUntil)
            return false;

        if (now - this.lastNotification < PingPongWindow &&
            (current == this.lastAnnounced || current == this.secondLastAnnounced))
            return false;

        if (tier == LocationTier.SubArea &&
            this.config.HideWhileTravellingFast &&
            speed >= TravellingSpeed)
            return false;

        return true;
    }

    public void MarkAnnounced(in LocationSnapshot announced, LocationTier tier, TimeSpan onScreenDuration)
    {
        this.lastNotification = this.now();
        this.secondLastAnnounced = this.lastAnnounced;
        this.lastAnnounced = announced;

        if (tier <= LocationTier.Place)
            this.suppressFinerUntil = this.now() + onScreenDuration;
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
