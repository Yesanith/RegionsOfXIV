using RegionsOfXIV;
using RegionsOfXIV.Models;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

public class AnnouncementCoordinatorTests
{
    private const uint Region = 10;
    private const uint Zone = 20;
    private const uint Place = 30;
    private const uint Area = 40;
    private const uint SubArea = 50;

    private readonly Configuration config = new();
    private readonly FakeGameState game = new();
    private readonly FakeSink sink = new();
    private readonly FakeLocations locations = new();
    private readonly FakeWeather weather = new();
    private readonly FakeAreaText areaText = new();
    private readonly FakeBanners banners = new();
    private readonly FakeZones zones = new();
    private readonly FakePlaceNames places = new();
    private readonly FakeWeatherNames weatherNames = new();

    private AnnouncementCoordinator Build()
    {
        this.places.Names[Region] = "La Noscea";
        this.places.Names[Zone] = "Middle La Noscea";
        this.places.Names[Place] = "Middle La Noscea";
        this.places.Names[Area] = "Summerford";
        this.places.Names[SubArea] = "Summerford Farms";

        return new AnnouncementCoordinator(
            this.config,
            new NotificationGate(this.config, this.game),
            this.sink,
            new AnnouncementSources(
                this.locations, this.weather, this.areaText,
                this.banners, this.zones, this.places, this.weatherNames));
    }

    private static ZoneArrival Arrival(bool duty = false, bool pvp = false) =>
        new(100, Place, Zone, Region, pvp, duty);

    [Fact]
    public void ArrivingSomewhereAnnouncesItWithItsRegionAbove()
    {
        using var _ = Build();

        this.zones.Arrive(Arrival());

        var announced = Assert.Single(this.sink.Places);
        Assert.Equal("Middle La Noscea", announced.Text);
        Assert.Equal("La Noscea", announced.Header);
    }

    [Fact]
    public void ArrivingFallsBackToTheZoneNameWhenThePlaceHasNone()
    {
        using var _ = Build();
        this.places.Names.Remove(Place);

        this.zones.Arrive(Arrival());

        Assert.Equal("Middle La Noscea", Assert.Single(this.sink.Places).Text);
    }

    [Fact]
    public void ArrivingSaysNothingWhenNothingCanBeNamed()
    {
        using var _ = Build();
        this.places.Names.Clear();

        this.zones.Arrive(Arrival());

        Assert.Empty(this.sink.Pushed);
    }

    [Fact]
    public void TheHeaderIsDroppedWhenItRepeatsTheName()
    {
        using var _ = Build();
        this.places.Names[Region] = "Middle La Noscea";

        this.zones.Arrive(Arrival());

        Assert.Null(Assert.Single(this.sink.Places).Header);
    }

    [Fact]
    public void TheHeaderIsDroppedWhenTheSettingIsOff()
    {
        this.config.IncludeParentTierAsHeader = false;
        using var _ = Build();

        this.zones.Arrive(Arrival());

        Assert.Null(Assert.Single(this.sink.Places).Header);
    }

    [Fact]
    public void ArrivingAnnouncesTheWeatherAlongsideThePlace()
    {
        this.config.WeatherNotificationEnabled = true;
        this.weatherNames.Forecasts = new ResolvedWeather(7, "Fair Skies", 60277);
        using var _ = Build();

        this.zones.Arrive(Arrival());

        Assert.Equal("Middle La Noscea", Assert.Single(this.sink.Places).Text);

        var sky = Assert.Single(this.sink.Weather);
        Assert.Equal("Fair Skies", sky.Text);
        Assert.Equal(60277u, sky.IconId);
    }

    [Fact]
    public void TheForecastPrimesTheTrackerSoTheRealReadingIsNotAnnouncedAgain()
    {
        this.config.WeatherNotificationEnabled = true;
        this.weatherNames.Forecasts = new ResolvedWeather(7, "Fair Skies", 60277);
        using var _ = Build();

        this.zones.Arrive(Arrival());

        Assert.Equal(7u, this.weather.Primed);
    }

    [Fact]
    public void ArrivingSaysNothingAboutTheWeatherWhenItIsSwitchedOff()
    {
        this.config.WeatherNotificationEnabled = false;
        this.weatherNames.Forecasts = new ResolvedWeather(7, "Fair Skies", 60277);
        using var _ = Build();

        this.zones.Arrive(Arrival());

        Assert.Empty(this.sink.Weather);
    }

    [Fact]
    public void WeatherTurningOverIsAnnouncedOnItsOwn()
    {
        this.config.WeatherNotificationEnabled = true;
        this.weatherNames.Names[3] = new ResolvedWeather(3, "Rain", 60278);
        using var _ = Build();

        this.weather.Change(3);

        Assert.Equal("Rain", Assert.Single(this.sink.Weather).Text);
        Assert.Empty(this.sink.Places);
    }

    [Fact]
    public void AnUnknownWeatherIsNotAnnounced()
    {
        this.config.WeatherNotificationEnabled = true;
        using var _ = Build();

        this.weather.Change(200);

        Assert.Empty(this.sink.Pushed);
    }

    [Fact]
    public void BannersAreAnnouncedInCapitals()
    {
        this.config.BannerNotificationEnabled = true;

        using var _ = Build();

        this.banners.Show(120001, "Quest Accepted");

        var announced = Assert.Single(this.sink.Places);
        Assert.Equal("QUEST ACCEPTED", announced.Text);
        Assert.Null(announced.Header);
    }

    // Was BannersAreNotHeldBackByThePacingThatGovernsPlaces, which asserted the opposite: that a
    // banner ignored the global cooldown entirely. Banners now share it, so an arrival holds the
    // next banner off and two banners in a row cannot stack.
    //
    // The coordinator builds its own gate on the real clock, so this can only show the holding
    // off. NotificationGateTests covers the release, where the clock can be advanced.
    [Fact]
    public void BannersWaitOutThePacingFromAPlace()
    {
        this.config.BannerNotificationEnabled = true;

        using var _ = Build();

        this.zones.Arrive(Arrival());
        this.banners.Show(120001, "Quest Accepted");
        this.banners.Show(120002, "Quest Complete");

        Assert.Single(this.sink.Pushed);
    }

    [Fact]
    public void LoggingOutForgetsTheWeatherAndTheRecentPlaces()
    {
        using var _ = Build();

        this.zones.Arrive(Arrival());
        this.zones.LogOut();
        this.zones.Arrive(Arrival());

        Assert.Equal(1, this.weather.Resets);
        Assert.Equal(2, this.sink.Places.Count);
    }

    [Fact]
    public void ArrivingInPvPIsSilent()
    {
        using var _ = Build();

        this.zones.Arrive(Arrival(pvp: true));

        Assert.Empty(this.sink.Pushed);
    }

    [Fact]
    public void ArrivingInADutyIsSilentWhenDutiesAreMuted()
    {
        this.config.HideInDuty = true;
        using var _ = Build();

        this.zones.Arrive(Arrival(duty: true));

        Assert.Empty(this.sink.Pushed);
    }

    [Fact]
    public void TheGamesOwnAreaTextIsAnnouncedWhenTheTiersDidNotMove()
    {
        using var _ = Build();

        this.areaText.Show("The Rising Stones");

        Assert.Equal("The Rising Stones", Assert.Single(this.sink.Places).Text);
    }

    [Fact]
    public void AreaTextAlwaysPollsTheTrackerSoThePositionStaysCurrent()
    {
        using var _ = Build();

        this.areaText.Show(null);
        this.areaText.Show("The Rising Stones");

        Assert.Equal(2, this.locations.Polls);
    }

    [Fact]
    public void MovingToASubAreaAnnouncesItUnderItsArea()
    {
        using var _ = Build();
        this.locations.Current = new LocationSnapshot(100, Region, Zone, Place, Area, 0);

        this.locations.Move(new LocationSnapshot(100, Region, Zone, Place, Area, SubArea));

        var announced = Assert.Single(this.sink.Places);
        Assert.Equal("Summerford Farms", announced.Text);
        Assert.Equal("Summerford", announced.Header);
    }

    [Fact]
    public void MovingIsSilentWhenThatTierIsSwitchedOff()
    {
        this.config.SubAreaNotificationEnabled = false;
        using var _ = Build();
        this.locations.Current = new LocationSnapshot(100, Region, Zone, Place, Area, 0);

        this.locations.Move(new LocationSnapshot(100, Region, Zone, Place, Area, SubArea));

        Assert.Empty(this.sink.Pushed);
    }

    [Fact]
    public void EnteringASanctuaryAnnouncesWhereYouAre()
    {
        using var _ = Build();
        this.locations.Current = new LocationSnapshot(100, Region, Zone, Place, Area, SubArea);

        this.locations.EnterSanctuary(true);

        Assert.Equal("Summerford Farms", Assert.Single(this.sink.Places).Text);
    }

    [Fact]
    public void MovingWithinAZoneIsSilentDuringACutscene()
    {
        this.game.IsInCutscene = true;
        using var _ = Build();
        this.locations.Current = new LocationSnapshot(100, Region, Zone, Place, Area, 0);

        this.locations.Move(new LocationSnapshot(100, Region, Zone, Place, Area, SubArea));

        Assert.Empty(this.sink.Pushed);
    }

    [Fact]
    public void ArrivingIsStillAnnouncedWhileTheLoadingScreenFlagsAreSet()
    {
        this.game.IsBetweenAreas = true;
        this.game.IsInCutscene = true;
        using var _ = Build();

        this.zones.Arrive(Arrival());

        Assert.Equal("Middle La Noscea", Assert.Single(this.sink.Places).Text);
    }

    [Fact]
    public void DisposingStopsItListening()
    {
        var coordinator = Build();
        coordinator.Dispose();

        this.zones.Arrive(Arrival());
        this.banners.Show(120001, "Quest Accepted");
        this.weather.Change(3);

        Assert.Empty(this.sink.Pushed);
    }

    [Fact]
    public void ThePreviewUsesWhereYouActuallyAre()
    {
        using var coordinator = Build();
        this.locations.Current = new LocationSnapshot(100, Region, Zone, Place, Area, SubArea);

        coordinator.PushPreview();

        var announced = Assert.Single(this.sink.Places);
        Assert.Equal("Summerford Farms", announced.Text);
        Assert.Equal("Summerford", announced.Header);
    }
}
