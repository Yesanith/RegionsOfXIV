using RegionsOfXIV.Models;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// The suppression matrix. Every rule here is one that is tedious to confirm in
// game — you would have to fly somewhere, wait out a cooldown, or find a
// cutscene — and each is a decision the plugin makes many times a session.
public class NotificationGateTests
{
    private readonly FakeSettings settings = new();
    private readonly FakeGameState game = new();
    private readonly TestClock clock = new();

    private NotificationGate Gate() => new(this.settings, this.game, this.clock.Read);

    private static readonly LocationSnapshot Here = new(100, 1, 2, 3, 4, 5);
    private static readonly LocationSnapshot NextSubArea = new(100, 1, 2, 3, 4, 6);
    private static readonly LocationSnapshot NextArea = new(100, 1, 2, 3, 7, 8);
    private static readonly LocationSnapshot NextZone = new(200, 1, 9, 10, 11, 12);

    // Standing still unless a test says otherwise.
    private bool Announce(LocationSnapshot to, LocationTier tier, float speed = 0f) =>
        Gate().ShouldAnnounce(Here, to, tier, speed);

    // --- tiers and toggles --------------------------------------------------

    [Fact]
    public void NoChangeIsNotAnnounced()
    {
        Assert.False(Announce(Here, LocationTier.None));
    }

    [Fact]
    public void ADisabledTierIsNotAnnounced()
    {
        this.settings.SubAreaNotificationEnabled = false;

        Assert.False(Announce(NextSubArea, LocationTier.SubArea));
    }

    [Fact]
    public void DisablingOneTierLeavesTheOthersAlone()
    {
        this.settings.SubAreaNotificationEnabled = false;

        Assert.True(Announce(NextArea, LocationTier.Area));
        Assert.True(Announce(NextZone, LocationTier.Zone));
    }

    // Territory, Region, Zone and Place all answer to the one "zone" toggle,
    // because they are all the same event to a player: the loading screen ended
    // somewhere new.
    [Theory]
    [InlineData(LocationTier.Territory)]
    [InlineData(LocationTier.Region)]
    [InlineData(LocationTier.Zone)]
    [InlineData(LocationTier.Place)]
    public void TheCoarseTiersShareTheZoneToggle(LocationTier tier)
    {
        this.settings.ZoneNotificationEnabled = false;

        Assert.False(Announce(NextZone, tier));
    }

    // --- game state ---------------------------------------------------------

    [Fact]
    public void NothingIsAnnouncedWhileLoggedOut()
    {
        this.game.IsLoggedIn = false;

        Assert.False(Announce(NextSubArea, LocationTier.SubArea));
    }

    [Fact]
    public void NothingIsAnnouncedMidLoadingScreen()
    {
        this.game.IsBetweenAreas = true;

        Assert.False(Announce(NextSubArea, LocationTier.SubArea));
    }

    // Not negotiable, unlike combat and duties below: there is no setting that
    // turns these back on.
    [Fact]
    public void CutscenesPvpAndGposeAreSuppressedUnconditionally()
    {
        foreach (var block in new Action[]
                 {
                     () => this.game.IsInCutscene = true,
                     () => this.game.IsPvP = true,
                     () => this.game.IsGPosing = true,
                 })
        {
            this.game.IsInCutscene = this.game.IsPvP = this.game.IsGPosing = false;
            block();

            Assert.False(Announce(NextSubArea, LocationTier.SubArea));
        }
    }

    [Fact]
    public void CombatOnlySuppressesWhenTheUserAsked()
    {
        this.game.IsInCombat = true;

        Assert.True(Announce(NextSubArea, LocationTier.SubArea));

        this.settings.HideInCombat = true;

        Assert.False(Announce(NextSubArea, LocationTier.SubArea));
    }

    [Fact]
    public void DutiesOnlySuppressWhenTheUserAsked()
    {
        this.game.IsBoundByDuty = true;

        Assert.True(Announce(NextSubArea, LocationTier.SubArea));

        this.settings.HideInDuty = true;

        Assert.False(Announce(NextSubArea, LocationTier.SubArea));
    }

    // --- timing -------------------------------------------------------------

    [Fact]
    public void AnnouncementsAreSpacedByTheGlobalCooldown()
    {
        var gate = Gate();

        Assert.True(gate.ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
        gate.MarkAnnounced(NextSubArea, LocationTier.SubArea, TimeSpan.FromSeconds(5));

        this.clock.Advance(1);
        Assert.False(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));

        this.clock.Advance(1.5);
        Assert.True(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));
    }

    [Fact]
    public void AFinerChangeWaitsOutTheZoneAnnouncementOnScreen()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(TimeSpan.FromSeconds(8));

        this.clock.Advance(3);
        Assert.False(gate.ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));

        this.clock.Advance(6);
        Assert.True(gate.ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
    }

    // ZoneInit announces during the loading screen; LocationTracker sees the same
    // arrival once TerritoryType catches up. Only one of them should reach the
    // screen.
    [Fact]
    public void TheTrackerDoesNotRepeatWhatZoneInitAlreadyAnnounced()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(TimeSpan.FromSeconds(8));

        this.clock.Advance(3);
        Assert.False(gate.ShouldAnnounce(Here, NextZone, LocationTier.Zone, 0f));

        this.clock.Advance(6);
        Assert.True(gate.ShouldAnnounce(Here, NextZone, LocationTier.Zone, 0f));
    }

    // Standing on a boundary line, which walks A -> B -> A.
    [Fact]
    public void BouncingBackToARecentAreaIsSuppressed()
    {
        var gate = Gate();

        gate.MarkAnnounced(Here, LocationTier.SubArea, TimeSpan.FromSeconds(5));
        this.clock.Advance(3);

        gate.MarkAnnounced(NextSubArea, LocationTier.SubArea, TimeSpan.FromSeconds(5));
        this.clock.Advance(3);

        Assert.False(gate.ShouldAnnounce(NextSubArea, Here, LocationTier.SubArea, 0f));
    }

    // Bounded on purpose: coming back an hour later is a real arrival, not a
    // bounce.
    [Fact]
    public void ReturningLongAfterwardsIsAnnouncedAgain()
    {
        var gate = Gate();

        gate.MarkAnnounced(Here, LocationTier.SubArea, TimeSpan.FromSeconds(5));
        this.clock.Advance(31);

        Assert.True(gate.ShouldAnnounce(NextSubArea, Here, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var gate = Gate();

        gate.MarkAnnounced(Here, LocationTier.SubArea, TimeSpan.FromSeconds(5));
        gate.Reset();

        // Both the cooldown and the ping-pong memory are gone, with no time passed.
        Assert.True(gate.ShouldAnnounce(NextSubArea, Here, LocationTier.SubArea, 0f));
    }

    // --- speed --------------------------------------------------------------

    [Fact]
    public void SubAreasAreSkippedWhileTravellingFast()
    {
        Assert.False(Announce(NextSubArea, LocationTier.SubArea, speed: 25f));
    }

    [Fact]
    public void GroundSpeedIsNotFastEnoughToSkipAnything()
    {
        // A ground mount sits in the low teens; crossing sub-areas on one is
        // ordinary play.
        Assert.True(Announce(NextSubArea, LocationTier.SubArea, speed: 13f));
    }

    [Fact]
    public void SpeedNeverSuppressesTheCoarserTiers()
    {
        Assert.True(Announce(NextArea, LocationTier.Area, speed: 40f));
        Assert.True(Announce(NextZone, LocationTier.Zone, speed: 40f));
    }

    [Fact]
    public void TheSpeedGuardCanBeTurnedOff()
    {
        this.settings.HideWhileTravellingFast = false;

        Assert.True(Announce(NextSubArea, LocationTier.SubArea, speed: 25f));
    }

    // --- the zone-entry path ------------------------------------------------

    // ZoneInit arrives mid-loading-screen, when the condition flags still describe
    // the zone being left, so this path reads the destination out of the event and
    // deliberately consults no game state at all.
    [Fact]
    public void ZoneEntryIsAnnouncedForAnOrdinaryZone()
    {
        this.game.IsBetweenAreas = true;
        this.game.IsLoggedIn = false;

        Assert.True(Gate().ShouldAnnounceZoneEntry(destinationIsPvp: false, destinationIsDuty: false));
    }

    // The drawing is tied to the hiding: this replaces the game's own title, so
    // leaving that title alone means staying quiet.
    [Fact]
    public void ZoneEntryIsSilentWhenTheNativeTitleIsLeftAlone()
    {
        this.settings.HideNativeLoadingTitle = false;

        Assert.False(Gate().ShouldAnnounceZoneEntry(destinationIsPvp: false, destinationIsDuty: false));
    }

    [Fact]
    public void ZoneEntryIsSilentIntoPvp()
    {
        Assert.False(Gate().ShouldAnnounceZoneEntry(destinationIsPvp: true, destinationIsDuty: false));
    }

    [Fact]
    public void ZoneEntryIntoADutyFollowsTheDutySetting()
    {
        Assert.True(Gate().ShouldAnnounceZoneEntry(destinationIsPvp: false, destinationIsDuty: true));

        this.settings.HideInDuty = true;

        Assert.False(Gate().ShouldAnnounceZoneEntry(destinationIsPvp: false, destinationIsDuty: true));
    }

    // --- sanctuaries --------------------------------------------------------

    // Loading into a settlement is exactly the case where the finer name is meant
    // to follow the zone name and supersede it, so this path ignores the window
    // that would otherwise hold it back.
    [Fact]
    public void SanctuaryEntryIgnoresTheZoneAnnouncementStillOnScreen()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(TimeSpan.FromSeconds(8));

        this.clock.Advance(3);

        Assert.True(gate.ShouldAnnounceSanctuary());
    }

    [Fact]
    public void SanctuaryEntryStillWaitsOutTheGlobalCooldown()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(TimeSpan.FromSeconds(8));

        this.clock.Advance(1);

        Assert.False(gate.ShouldAnnounceSanctuary());
    }

    // --- the game's own area flash -----------------------------------------

    [Fact]
    public void NativeAreaTextNeedsAtLeastOneOfTheFinerTiersEnabled()
    {
        this.settings.AreaNotificationEnabled = false;
        this.settings.SubAreaNotificationEnabled = false;

        Assert.False(Gate().ShouldAnnounceNativeAreaText());

        this.settings.SubAreaNotificationEnabled = true;

        Assert.True(Gate().ShouldAnnounceNativeAreaText());
    }

    [Fact]
    public void NativeAreaTextWaitsForACoarseAnnouncementToClear()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(TimeSpan.FromSeconds(8));

        this.clock.Advance(3);
        Assert.False(gate.ShouldAnnounceNativeAreaText());

        this.clock.Advance(6);
        Assert.True(gate.ShouldAnnounceNativeAreaText());
    }
}
