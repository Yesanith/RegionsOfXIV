using RegionsOfXIV.Models;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

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

    private bool Announce(LocationSnapshot to, LocationTier tier, float speed = 0f) =>
        Gate().ShouldAnnounce(Here, to, tier, speed);

    private static NotificationTiming Timing(double onScreen = 5, double readable = 1) =>
        new(TimeSpan.FromSeconds(readable), TimeSpan.FromSeconds(onScreen));

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

    [Fact]
    public void AnnouncementsAreSpacedByTheGlobalCooldown()
    {
        var gate = Gate();

        Assert.True(gate.ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
        gate.MarkAnnounced(NextSubArea, LocationTier.SubArea, Timing());

        this.clock.Advance(1);
        Assert.False(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));

        this.clock.Advance(1.5);
        Assert.True(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));
    }

    [Fact]
    public void AFinerChangeWaitsOutTheZoneAnnouncementOnScreen()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(Timing(onScreen: 8));

        this.clock.Advance(3);
        Assert.False(gate.ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));

        this.clock.Advance(6);
        Assert.True(gate.ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void TheTrackerDoesNotRepeatWhatZoneInitAlreadyAnnounced()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(Timing(onScreen: 8));

        this.clock.Advance(3);
        Assert.False(gate.ShouldAnnounce(Here, NextZone, LocationTier.Zone, 0f));

        this.clock.Advance(6);
        Assert.True(gate.ShouldAnnounce(Here, NextZone, LocationTier.Zone, 0f));
    }

    [Fact]
    public void BouncingBackToARecentAreaIsSuppressed()
    {
        var gate = Gate();

        gate.MarkAnnounced(Here, LocationTier.SubArea, Timing());
        this.clock.Advance(3);

        gate.MarkAnnounced(NextSubArea, LocationTier.SubArea, Timing());
        this.clock.Advance(3);

        Assert.False(gate.ShouldAnnounce(NextSubArea, Here, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void ReturningLongAfterwardsIsAnnouncedAgain()
    {
        var gate = Gate();

        gate.MarkAnnounced(Here, LocationTier.SubArea, Timing());
        this.clock.Advance(31);

        Assert.True(gate.ShouldAnnounce(NextSubArea, Here, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var gate = Gate();

        gate.MarkAnnounced(Here, LocationTier.SubArea, Timing());
        gate.Reset();

        Assert.True(gate.ShouldAnnounce(NextSubArea, Here, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void SubAreasAreSkippedWhileTravellingFast()
    {
        Assert.False(Announce(NextSubArea, LocationTier.SubArea, speed: 25f));
    }

    [Fact]
    public void GroundSpeedIsNotFastEnoughToSkipAnything()
    {
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

    [Fact]
    public void ZoneEntryIsAnnouncedForAnOrdinaryZone()
    {
        this.game.IsBetweenAreas = true;
        this.game.IsLoggedIn = false;

        Assert.True(Gate().ShouldAnnounceZoneEntry(destinationIsPvp: false, destinationIsDuty: false));
    }

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

    [Fact]
    public void SanctuaryEntryIgnoresTheZoneAnnouncementStillOnScreen()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(Timing(onScreen: 8));

        this.clock.Advance(3);

        Assert.True(gate.ShouldAnnounceSanctuary());
    }

    [Fact]
    public void SanctuaryEntryStillWaitsOutTheGlobalCooldown()
    {
        var gate = Gate();
        gate.MarkZoneAnnounced(Timing(onScreen: 8));

        this.clock.Advance(1);

        Assert.False(gate.ShouldAnnounceSanctuary());
    }

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
        gate.MarkZoneAnnounced(Timing(onScreen: 8));

        this.clock.Advance(3);
        Assert.False(gate.ShouldAnnounceNativeAreaText());

        this.clock.Advance(6);
        Assert.True(gate.ShouldAnnounceNativeAreaText());
    }

    [Fact]
    public void ALineIsNotReplacedBeforeItHasBeenReadable()
    {
        var gate = Gate();

        // A default setup takes 2.4s to finish decoding: fade in, the beat, then the reveal.
        gate.MarkAnnounced(NextSubArea, LocationTier.SubArea, Timing(readable: 2.4));

        this.clock.Advance(2);
        Assert.False(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));

        this.clock.Advance(0.5);
        Assert.True(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));
    }

    [Fact]
    public void AQuickLineStillWaitsOutTheGlobalCooldown()
    {
        var gate = Gate();

        gate.MarkAnnounced(NextSubArea, LocationTier.SubArea, Timing(readable: 0.4));

        this.clock.Advance(1);
        Assert.False(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));

        this.clock.Advance(1.1);
        Assert.True(gate.ShouldAnnounce(NextSubArea, NextArea, LocationTier.Area, 0f));
    }

    [Fact]
    public void AWholeCityBlockIsRememberedNotJustTheLastOne()
    {
        var gate = Gate();

        // Five sub-areas in a loop, the sort of circuit a city block is.
        for (var i = 0; i < 5; i++)
        {
            gate.MarkAnnounced(SubArea(i), LocationTier.SubArea, Timing());
            this.clock.Advance(3);
        }

        Assert.False(gate.ShouldAnnounce(SubArea(4), SubArea(0), LocationTier.SubArea, 0f));
    }

    [Fact]
    public void TheOldestPlaceEventuallyFallsOutOfMemory()
    {
        var gate = Gate();

        for (var i = 0; i < 7; i++)
        {
            gate.MarkAnnounced(SubArea(i), LocationTier.SubArea, Timing());
            this.clock.Advance(3);
        }

        Assert.True(gate.ShouldAnnounce(SubArea(6), SubArea(0), LocationTier.SubArea, 0f));
    }

    private static LocationSnapshot SubArea(int index) => new(100, 1, 2, 3, 4, (uint)(20 + index));
}
