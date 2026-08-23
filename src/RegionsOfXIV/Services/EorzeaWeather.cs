using System;

namespace RegionsOfXIV.Services;

// Weather in FFXIV is not random and not sent to the client -- it is a pure function of the
// clock, so it can be worked out for any zone at any time without waiting to observe it. That is
// what lets an arrival announce the weather in the same breath as the place name instead of a
// beat later.
internal static class EorzeaWeather
{
    private const long BellSeconds = 175;

    public const long WindowSeconds = BellSeconds * 8;

    public static readonly TimeSpan WindowLength = TimeSpan.FromSeconds(WindowSeconds);

    public static DateTimeOffset WindowStart(DateTimeOffset at)
    {
        var seconds = at.ToUnixTimeSeconds();
        return DateTimeOffset.FromUnixTimeSeconds(Floor(seconds, WindowSeconds));
    }

    public static DateTimeOffset NextWindow(DateTimeOffset at) => WindowStart(at) + WindowLength;

    public static int Chance(DateTimeOffset at)
    {
        var seconds = at.ToUnixTimeSeconds();

        var bell = Floor(seconds, BellSeconds) / BellSeconds;
        var increment = (uint)((bell + 8 - (bell % 8)) % 24);

        var totalDays = (uint)(Floor(seconds, BellSeconds * 24) / (BellSeconds * 24));

        var calcBase = (totalDays * 100u) + increment;

        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1 >> 8) ^ step1;

        return (int)(step2 % 100u);
    }

    public static int Pick(int chance, ReadOnlySpan<byte> rates)
    {
        var cumulative = 0;

        for (var i = 0; i < rates.Length; i++)
        {
            cumulative += rates[i];

            if (chance < cumulative)
                return i;
        }

        return -1;
    }

    private static long Floor(long value, long step)
    {
        var whole = value / step;

        if (value < 0 && whole * step != value)
            whole--;

        return whole * step;
    }
}
