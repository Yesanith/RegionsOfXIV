using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

public class WeatherTrackerTests
{
    private static (WeatherTracker Tracker, FakeGameState Game, List<byte> Seen, Func<byte, byte> Set) Build()
    {
        var game = new FakeGameState();
        byte active = 1;
        var tracker = new WeatherTracker(game, () => active);

        var seen = new List<byte>();
        tracker.OnWeatherChanged += w => seen.Add(w);

        return (tracker, game, seen, v => active = v);
    }

    [Fact]
    public void TheFirstSampleOnlyTakesABaseline()
    {
        var (tracker, _, seen, _) = Build();

        tracker.Sample();

        Assert.Empty(seen);
        Assert.Equal(1, tracker.Current);
    }

    [Fact]
    public void AChangeIsAnnouncedOnce()
    {
        var (tracker, _, seen, set) = Build();

        tracker.Sample();
        set(7);
        tracker.Sample();
        tracker.Sample();
        tracker.Sample();

        Assert.Equal([(byte)7], seen);
    }

    [Fact]
    public void SteadyWeatherIsSilent()
    {
        var (tracker, _, seen, _) = Build();

        for (var i = 0; i < 50; i++)
            tracker.Sample();

        Assert.Empty(seen);
    }

    [Fact]
    public void ArrivingSomewhereWithDifferentWeatherAnnouncesIt()
    {
        var (tracker, game, seen, set) = Build();

        tracker.Sample();

        game.IsBetweenAreas = true;
        tracker.Sample();

        set(9);
        game.IsBetweenAreas = false;
        tracker.Sample();

        Assert.Equal([(byte)9], seen);
        Assert.Equal(9, tracker.Current);
    }

    [Fact]
    public void ArrivingWithTheSameWeatherSaysNothing()
    {
        var (tracker, game, seen, _) = Build();

        tracker.Sample();

        game.IsBetweenAreas = true;
        tracker.Sample();

        game.IsBetweenAreas = false;
        tracker.Sample();

        Assert.Empty(seen);
    }

    [Fact]
    public void PrimingSetsTheBaselineWithoutAnnouncing()
    {
        var (tracker, _, seen, _) = Build();

        tracker.Prime(9);

        Assert.Empty(seen);
        Assert.Equal(9, tracker.Current);
    }

    [Fact]
    public void AReadingThatAgreesWithWhatWasPrimedIsNotRepeated()
    {
        var (tracker, _, seen, set) = Build();

        tracker.Prime(9);

        set(9);
        tracker.Sample();
        tracker.Sample();

        Assert.Empty(seen);
    }

    [Fact]
    public void AReadingThatContradictsWhatWasPrimedCorrectsItself()
    {
        var (tracker, _, seen, set) = Build();

        tracker.Prime(9);

        set(4);
        tracker.Sample();

        Assert.Equal([(byte)4], seen);
    }

    [Fact]
    public void PrimingIgnoresAnythingTooLargeToBeAWeather()
    {
        var (tracker, _, _, _) = Build();

        tracker.Prime(9);
        tracker.Prime(4000);

        Assert.Equal(9, tracker.Current);
    }

    [Fact]
    public void LoggingBackInSaysNothingUntilSomethingChanges()
    {
        var (tracker, game, seen, _) = Build();

        game.IsLoggedIn = false;
        tracker.Sample();

        game.IsBetweenAreas = true;
        game.IsLoggedIn = true;
        tracker.Sample();

        game.IsBetweenAreas = false;
        tracker.Sample();

        Assert.Empty(seen);
        Assert.Equal(1, tracker.Current);
    }

    [Fact]
    public void AChangeAfterArrivingIsStillAnnounced()
    {
        var (tracker, game, seen, set) = Build();

        tracker.Sample();

        game.IsBetweenAreas = true;
        tracker.Sample();

        set(9);
        game.IsBetweenAreas = false;
        tracker.Sample();

        set(4);
        tracker.Sample();

        Assert.Equal([(byte)9, (byte)4], seen);
    }

    [Fact]
    public void AnUnsettledReadingIsIgnored()
    {
        var (tracker, _, seen, set) = Build();

        tracker.Sample();

        set(0);
        tracker.Sample();

        Assert.Empty(seen);
        Assert.Equal(1, tracker.Current);
    }

    [Fact]
    public void NothingIsAnnouncedWhileLoggedOut()
    {
        var (tracker, game, seen, set) = Build();

        tracker.Sample();

        game.IsLoggedIn = false;
        set(7);
        tracker.Sample();

        Assert.Empty(seen);
    }

    [Fact]
    public void LoggingBackInTakesAFreshBaseline()
    {
        var (tracker, game, seen, set) = Build();

        tracker.Sample();

        game.IsLoggedIn = false;
        tracker.Sample();

        set(7);
        game.IsLoggedIn = true;
        tracker.Sample();

        Assert.Empty(seen);

        set(3);
        tracker.Sample();

        Assert.Equal([(byte)3], seen);
    }

    [Fact]
    public void ResetForgetsWhatItSaw()
    {
        var (tracker, _, seen, set) = Build();

        tracker.Sample();
        tracker.Reset();

        set(7);
        tracker.Sample();

        Assert.Empty(seen);
    }
}
