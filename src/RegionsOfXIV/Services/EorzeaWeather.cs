using System;

namespace RegionsOfXIV.Services;

/// <summary>
/// Eorzean weather is not random: the game derives it from the clock, so the same
/// calculation run anywhere gives the same answer. This is that calculation, kept
/// free of any game data so it can be tested on its own.
/// </summary>
internal static class EorzeaWeather
{
    /// <summary>An Eorzean hour, in real seconds.</summary>
    private const long BellSeconds = 175;

    /// <summary>Weather holds for eight Eorzean hours, which is 23 minutes and 20 seconds.</summary>
    public const long WindowSeconds = BellSeconds * 8;

    public static readonly TimeSpan WindowLength = TimeSpan.FromSeconds(WindowSeconds);

    /// <summary>The instant the weather window containing <paramref name="at"/> began.</summary>
    public static DateTimeOffset WindowStart(DateTimeOffset at)
    {
        var seconds = at.ToUnixTimeSeconds();
        return DateTimeOffset.FromUnixTimeSeconds(Floor(seconds, WindowSeconds));
    }

    /// <summary>The instant the next weather window begins.</summary>
    public static DateTimeOffset NextWindow(DateTimeOffset at) => WindowStart(at) + WindowLength;

    /// <summary>
    /// The zero-to-ninety-nine roll for a moment in time. Every zone shares the roll;
    /// what differs is the table it is looked up in.
    /// </summary>
    public static int Chance(DateTimeOffset at)
    {
        var seconds = at.ToUnixTimeSeconds();

        // The eight-hour window, expressed as the hour it starts at, biased by eight.
        var bell = Floor(seconds, BellSeconds) / BellSeconds;
        var increment = (uint)((bell + 8 - (bell % 8)) % 24);

        var totalDays = (uint)(Floor(seconds, BellSeconds * 24) / (BellSeconds * 24));

        var calcBase = (totalDays * 100u) + increment;

        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1 >> 8) ^ step1;

        return (int)(step2 % 100u);
    }

    /// <summary>
    /// Picks the entry a roll lands on. Rates are weights that add up to a hundred,
    /// so the roll walks them until it runs out.
    /// </summary>
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

    /// <summary>Floor division that keeps working for instants before 1970.</summary>
    private static long Floor(long value, long step)
    {
        var whole = value / step;

        if (value < 0 && whole * step != value)
            whole--;

        return whole * step;
    }
}
