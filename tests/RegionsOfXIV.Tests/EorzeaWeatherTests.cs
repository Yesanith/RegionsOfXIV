using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

public class EorzeaWeatherTests
{
    private static DateTimeOffset At(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

    [Fact]
    public void AWindowIsTwentyThreeMinutesAndTwentySeconds()
    {
        Assert.Equal(1400, EorzeaWeather.WindowSeconds);
        Assert.Equal(TimeSpan.FromMinutes(23) + TimeSpan.FromSeconds(20), EorzeaWeather.WindowLength);
    }

    [Fact]
    public void WindowsAlignToTheEpoch()
    {
        Assert.Equal(At(0), EorzeaWeather.WindowStart(At(0)));
        Assert.Equal(At(0), EorzeaWeather.WindowStart(At(1399)));
        Assert.Equal(At(1400), EorzeaWeather.WindowStart(At(1400)));
        Assert.Equal(At(1400), EorzeaWeather.WindowStart(At(2799)));
    }

    [Fact]
    public void TheNextWindowIsOneLengthOn()
    {
        Assert.Equal(At(1400), EorzeaWeather.NextWindow(At(0)));
        Assert.Equal(At(1400), EorzeaWeather.NextWindow(At(1399)));
        Assert.Equal(At(2800), EorzeaWeather.NextWindow(At(1400)));
    }

    [Fact]
    public void TheRollHoldsForAWholeWindowAndOnlyMovesAtTheBoundary()
    {
        var start = 1_700_000_000L / 1400 * 1400;
        var expected = EorzeaWeather.Chance(At(start));

        for (var offset = 0; offset < 1400; offset += 7)
            Assert.Equal(expected, EorzeaWeather.Chance(At(start + offset)));

        Assert.Equal(expected, EorzeaWeather.Chance(At(start + 1399)));
    }

    [Fact]
    public void TheRollIsAlwaysAPercentage()
    {
        for (var window = 0; window < 5000; window++)
        {
            var chance = EorzeaWeather.Chance(At(window * 1400L));

            Assert.InRange(chance, 0, 99);
        }
    }

    [Fact]
    public void TheRollDoesNotSettleOnOneValue()
    {
        var seen = new HashSet<int>();

        for (var window = 0; window < 500; window++)
            seen.Add(EorzeaWeather.Chance(At(window * 1400L)));

        Assert.True(seen.Count > 50, $"only {seen.Count} distinct rolls in 500 windows");
    }

    [Fact]
    public void TheRollIsStableAcrossRuns()
    {
        // Pins the arithmetic itself: if the shifts or the day maths drift, these move.
        var a = EorzeaWeather.Chance(At(1_700_000_000));
        var b = EorzeaWeather.Chance(At(1_700_000_000));

        Assert.Equal(a, b);
        Assert.NotEqual(EorzeaWeather.Chance(At(0)), EorzeaWeather.Chance(At(1400)));
    }

    [Fact]
    public void PickWalksTheWeightsUntilTheRollRunsOut()
    {
        byte[] rates = [20, 30, 50];

        Assert.Equal(0, EorzeaWeather.Pick(0, rates));
        Assert.Equal(0, EorzeaWeather.Pick(19, rates));
        Assert.Equal(1, EorzeaWeather.Pick(20, rates));
        Assert.Equal(1, EorzeaWeather.Pick(49, rates));
        Assert.Equal(2, EorzeaWeather.Pick(50, rates));
        Assert.Equal(2, EorzeaWeather.Pick(99, rates));
    }

    [Fact]
    public void PickReportsNothingWhenTheWeightsDoNotCoverTheRoll()
    {
        byte[] rates = [10, 10];

        Assert.Equal(-1, EorzeaWeather.Pick(20, rates));
        Assert.Equal(-1, EorzeaWeather.Pick(0, []));
    }

    [Fact]
    public void ASingleWeatherAlwaysWins()
    {
        byte[] fixedWeather = [100];

        for (var chance = 0; chance < 100; chance++)
            Assert.Equal(0, EorzeaWeather.Pick(chance, fixedWeather));
    }
}
