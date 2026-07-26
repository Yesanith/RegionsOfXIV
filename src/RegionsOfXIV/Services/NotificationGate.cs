using System;
using Dalamud.Game.ClientState.Conditions;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Decides whether a location change deserves an on-screen notification.
// See ROADMAP Phase 9 for the full suppression matrix.
internal sealed class NotificationGate
{
    private static readonly TimeSpan GlobalCooldown = TimeSpan.FromMilliseconds(2000);

    // How long a bounce back to a recent area stays suppressed. The GW2 original
    // blocks it indefinitely, which also swallows a legitimate return an hour
    // later; bounding it keeps the anti-strobe behaviour without that.
    private static readonly TimeSpan PingPongWindow = TimeSpan.FromSeconds(30);

    private readonly Configuration config;

    private DateTime lastNotification = DateTime.MinValue;

    // Suppresses fine-grained alerts while a coarse one is still on screen.
    private DateTime suppressFinerUntil = DateTime.MinValue;

    // The last two announcements. Two, not one: walking A -> B -> A must not
    // re-announce A, which is what standing on a boundary line produces.
    private LocationSnapshot lastAnnounced = LocationSnapshot.Empty;
    private LocationSnapshot secondLastAnnounced = LocationSnapshot.Empty;

    public NotificationGate(Configuration config) => this.config = config;

    public void Reset()
    {
        this.lastNotification = DateTime.MinValue;
        this.suppressFinerUntil = DateTime.MinValue;
        this.lastAnnounced = LocationSnapshot.Empty;
        this.secondLastAnnounced = LocationSnapshot.Empty;
    }

    // Framework thread only: reads ICondition and IClientState.
    public bool ShouldAnnounce(in LocationSnapshot previous, in LocationSnapshot current, LocationTier tier)
    {
        if (tier == LocationTier.None)
            return false;

        if (!IsTierEnabled(tier))
            return false;

        if (IsBlockedByGameState())
            return false;

        var now = DateTime.UtcNow;

        if (now - this.lastNotification < GlobalCooldown)
            return false;

        // A coarse announcement is still on screen; don't let a finer one fight it.
        if (tier > LocationTier.Place && now < this.suppressFinerUntil)
            return false;

        if (now - this.lastNotification < PingPongWindow &&
            (current == this.lastAnnounced || current == this.secondLastAnnounced))
            return false;

        // TODO(Phase 9): "moving too fast" guard. GW2 reads speed off the Mumble
        // link; here it would come from IObjectTable.LocalPlayer.Position deltas,
        // or simply from mount/sprint state.

        return true;
    }

    public void MarkAnnounced(in LocationSnapshot announced, LocationTier tier, TimeSpan onScreenDuration)
    {
        this.lastNotification = DateTime.UtcNow;
        this.secondLastAnnounced = this.lastAnnounced;
        this.lastAnnounced = announced;

        if (tier <= LocationTier.Place)
            this.suppressFinerUntil = DateTime.UtcNow + onScreenDuration;
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
        if (!Plugin.ClientState.IsLoggedIn)
            return true;

        // Mid-loading-screen. FFXIV-specific; no GW2 equivalent.
        if (Plugin.Condition[ConditionFlag.BetweenAreas] ||
            Plugin.Condition[ConditionFlag.BetweenAreas51])
            return true;

        if (Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
            Plugin.Condition[ConditionFlag.WatchingCutscene] ||
            Plugin.Condition[ConditionFlag.WatchingCutscene78])
            return true;

        // Never in PvP — nothing that could read as an advantage.
        if (Plugin.ClientState.IsPvP)
            return true;

        if (Plugin.ClientState.IsGPosing)
            return true;

        if (this.config.HideInCombat && Plugin.Condition[ConditionFlag.InCombat])
            return true;

        if (this.config.HideInDuty && Plugin.Condition[ConditionFlag.BoundByDuty])
            return true;

        return false;
    }
}
