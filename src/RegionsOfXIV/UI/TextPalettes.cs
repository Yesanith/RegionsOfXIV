using System;
using System.Numerics;

namespace RegionsOfXIV.UI;

// The colour runs a place name can be lettered in, and where along the run each glyph sits.
//
// Every palette is a loop: the last stop blends back into the first, so a wave can travel along
// the line for as long as it is up without a seam.
internal static class TextPalettes
{
    private static readonly Vector4[] Sunset =
    [
        new(1.00f, 0.86f, 0.45f, 1f),
        new(1.00f, 0.55f, 0.25f, 1f),
        new(0.86f, 0.25f, 0.36f, 1f),
        new(0.48f, 0.22f, 0.52f, 1f),
    ];

    private static readonly Vector4[] Ocean =
    [
        new(0.55f, 0.92f, 0.95f, 1f),
        new(0.20f, 0.62f, 0.90f, 1f),
        new(0.10f, 0.30f, 0.65f, 1f),
        new(0.25f, 0.75f, 0.70f, 1f),
    ];

    private static readonly Vector4[] Aurora =
    [
        new(0.45f, 0.95f, 0.65f, 1f),
        new(0.25f, 0.80f, 0.85f, 1f),
        new(0.55f, 0.40f, 0.90f, 1f),
        new(0.85f, 0.45f, 0.75f, 1f),
    ];

    private static readonly Vector4[] Blossom =
    [
        new(1.00f, 0.93f, 0.95f, 1f),
        new(1.00f, 0.70f, 0.80f, 1f),
        new(0.92f, 0.45f, 0.62f, 1f),
        new(1.00f, 0.82f, 0.88f, 1f),
    ];

    private static readonly Vector4[] Golden =
    [
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.95f, 0.78f, 0.40f, 1f),
        new(0.75f, 0.52f, 0.18f, 1f),
        new(0.98f, 0.86f, 0.55f, 1f),
    ];

    private const float WaveSpeed = 0.35f;

    private const float PulseSpeed = 0.25f;

    // Hourly wrap keeps the float exact; the one jump per hour lands mid-cycle and is not seen.
    private const long ClockWrapMs = 3_600_000L;

    // Where in the run glyph `index` of `count` sits, as a fraction of the loop. Static lays the
    // run across the line once; Wave slides it along; Pulse moves every glyph together.
    public static float Phase(PaletteMotion motion, int index, int count, float speed)
    {
        var along = count <= 1 ? 0f : index / (float)count;
        var seconds = (Environment.TickCount64 % ClockWrapMs) / 1000f * speed;

        return motion switch
        {
            PaletteMotion.Wave => along - (seconds * WaveSpeed),
            PaletteMotion.Pulse => seconds * PulseSpeed,
            _ => along,
        };
    }

    public static Vector4 Colour(TextPalette palette, float phase)
    {
        phase -= MathF.Floor(phase);

        if (palette == TextPalette.Rainbow)
            return FromHue(phase);

        var stops = StopsFor(palette);
        var scaled = phase * stops.Length;
        var index = (int)scaled;
        var next = (index + 1) % stops.Length;

        return Vector4.Lerp(stops[index], stops[next], scaled - index);
    }

    private static Vector4[] StopsFor(TextPalette palette) => palette switch
    {
        TextPalette.Sunset => Sunset,
        TextPalette.Ocean => Ocean,
        TextPalette.Aurora => Aurora,
        TextPalette.Blossom => Blossom,
        _ => Golden,
    };

    // Slightly under full saturation so the rainbow reads as lettering rather than as a test card.
    private static Vector4 FromHue(float hue)
    {
        const float saturation = 0.72f;
        const float value = 1f;

        var sector = hue * 6f;
        var slice = (int)sector % 6;
        var fraction = sector - MathF.Floor(sector);
        var low = value * (1f - saturation);
        var falling = value * (1f - (saturation * fraction));
        var rising = value * (1f - (saturation * (1f - fraction)));

        return slice switch
        {
            0 => new Vector4(value, rising, low, 1f),
            1 => new Vector4(falling, value, low, 1f),
            2 => new Vector4(low, value, rising, 1f),
            3 => new Vector4(low, falling, value, 1f),
            4 => new Vector4(rising, low, value, 1f),
            _ => new Vector4(value, low, falling, 1f),
        };
    }
}
