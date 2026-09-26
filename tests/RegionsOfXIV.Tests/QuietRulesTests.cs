using RegionsOfXIV.Models;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// The places that never announce: the quiet list, cities and housing when those are switched
// off, and the flight flag for sub-areas. Each is a rule the gate applies on top of the tiers.
public class QuietRulesTests
{
    private readonly FakeSettings settings = new();
    private readonly FakeGameState game = new();
    private readonly TestClock clock = new();

    private NotificationGate Gate() => new(this.settings, this.game, this.clock.Read);

    private static readonly LocationSnapshot Here = new(100, 1, 2, 3, 4, 5);
    private static readonly LocationSnapshot NextSubArea = new(100, 1, 2, 3, 4, 6);
    private static readonly LocationSnapshot NextArea = new(100, 1, 2, 3, 7, 8);

    [Fact]
    public void AZoneOnTheQuietListNeverAnnouncesAnArea()
    {
        this.settings.Quiet.Add(100);

        Assert.False(Gate().ShouldAnnounce(Here, NextArea, LocationTier.Area, 0f));
    }

    [Fact]
    public void AZoneOnTheQuietListIsNotAnnouncedOnArrivalEither()
    {
        this.settings.Quiet.Add(200);

        Assert.False(Gate().ShouldAnnounceZoneEntry(false, false, 200));
        Assert.True(Gate().ShouldAnnounceZoneEntry(false, false, 300));
    }

    [Fact]
    public void TheQuietListIsCheckedByTheTerritoryTheGateIsAskedAbout()
    {
        this.settings.Quiet.Add(100);

        var elsewhere = new LocationSnapshot(300, 1, 2, 3, 4, 6);

        Assert.True(Gate().ShouldAnnounce(elsewhere with { SubAreaPlaceNameId = 5 }, elsewhere, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void ACityIsQuietOnlyWhenAskedToBe()
    {
        this.game.IsInCity = true;

        Assert.True(Gate().ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));

        this.settings.HideInCities = true;

        Assert.False(Gate().ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void HousingIsQuietOnlyWhenAskedToBe()
    {
        this.game.IsInHousing = true;

        Assert.True(Gate().ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));

        this.settings.HideInHousing = true;

        Assert.False(Gate().ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void HidingCitiesLeavesTheOpenWorldAlone()
    {
        this.settings.HideInCities = true;

        Assert.True(Gate().ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
    }

    // The flag is the exact answer where the speed estimate is only a good one.
    [Fact]
    public void FlyingSkipsSubAreasHoweverSlowly()
    {
        this.game.IsFlying = true;

        Assert.False(Gate().ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void FlyingStillAnnouncesAnArea()
    {
        this.game.IsFlying = true;

        Assert.True(Gate().ShouldAnnounce(Here, NextArea, LocationTier.Area, 0f));
    }

    [Fact]
    public void FlyingIsIgnoredWhenTheSkipIsOff()
    {
        this.game.IsFlying = true;
        this.settings.HideWhileTravellingFast = false;

        Assert.True(Gate().ShouldAnnounce(Here, NextSubArea, LocationTier.SubArea, 0f));
    }

    [Fact]
    public void WeatherStaysQuietInAQuietZone()
    {
        this.settings.WeatherNotificationEnabled = true;
        this.settings.Quiet.Add(this.game.TerritoryTypeId);

        Assert.False(Gate().ShouldAnnounceWeather());
    }
}
