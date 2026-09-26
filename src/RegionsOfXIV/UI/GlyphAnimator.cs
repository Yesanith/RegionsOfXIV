using System;

namespace RegionsOfXIV.UI;

// Where one glyph is partway through a motion: pushed sideways or up, how solid, how hot, and
// how large against its resting size.
internal readonly record struct GlyphState(float OffsetX, float OffsetY, float Alpha, float Heat, float Scale)
{
    public static readonly GlyphState Rest = new(0f, 0f, 1f, 0f, 1f);
}

// Where one glyph sits partway through a motion effect, as a pure function of its index and the
// overall progress. Each letter runs the same curve offset in time, so the effect sweeps along
// the line rather than moving every letter together.
internal static class GlyphAnimator
{
    private const float GlyphWindow = 0.45f;

    private const float TypedWindow = 0.08f;

    // A flickering glyph changes state this many times over its window, and is never quite solid
    // until the window ends, so the line visibly settles rather than happening to be lit.
    private const int FlickerSteps = 14;

    private const float FlickerDim = 0.15f;

    private const float FlickerLit = 0.8f;

    public static GlyphState For(MotionEffect effect, int index, int count, float progress, float fontSize)
    {
        var local = LocalProgress(index, count, progress, WindowFor(effect));

        return effect switch
        {
            MotionEffect.Typewriter => new GlyphState(0f, 0f, local > 0f ? 1f : 0f, 0f, 1f),

            MotionEffect.Rise => new GlyphState(
                0f,
                (1f - OutBack(local)) * fontSize * 0.5f,
                Clamp01(local * 3f),
                0f,
                1f),

            MotionEffect.Wave => new GlyphState(
                0f,
                -MathF.Sin(local * MathF.PI) * fontSize * 0.18f,
                Clamp01(local * 3f),
                0f,
                1f),

            MotionEffect.Burn => new GlyphState(0f, 0f, local, 1f - local, 1f),

            MotionEffect.Drop => new GlyphState(
                0f,
                -(1f - OutBack(local)) * fontSize * 0.5f,
                Clamp01(local * 3f),
                0f,
                1f),

            MotionEffect.Slide => new GlyphState(
                (1f - OutCubic(local)) * fontSize * 1.2f,
                0f,
                Clamp01(local * 3f),
                0f,
                1f),

            MotionEffect.Assemble => Scattered(index, local, fontSize),

            MotionEffect.Flicker => new GlyphState(0f, 0f, FlickerAlpha(index, local), 0f, 1f),

            MotionEffect.Zoom => new GlyphState(
                0f,
                0f,
                Clamp01(local * 3f),
                0f,
                MathF.Max(0.05f, OutBack(local))),

            _ => GlyphState.Rest,
        };
    }

    // Which motion a departure plays, backwards over the fade, or null for the plain fade.
    // Falling is Rise in reverse, which sinks; dissolving is Assemble in reverse, which scatters.
    public static MotionEffect? DepartureMotion(DepartureEffect departure, MotionEffect arrival) => departure switch
    {
        DepartureEffect.Reverse => arrival == MotionEffect.None ? null : arrival,
        DepartureEffect.Dissolve => MotionEffect.Assemble,
        DepartureEffect.Fall => MotionEffect.Rise,
        _ => null,
    };

    public static float WindowFor(MotionEffect effect) =>
        effect == MotionEffect.Typewriter ? TypedWindow : GlyphWindow;

    public static float LocalProgress(int index, int count, float progress) =>
        LocalProgress(index, count, progress, GlyphWindow);

    public static float LocalProgress(int index, int count, float progress, float window)
    {
        if (count <= 1)
            return Clamp01(progress);

        var start = index / (float)(count - 1) * (1f - window);

        return Clamp01((progress - start) / window);
    }

    // Each glyph converges from its own direction, fixed by its index so the scatter is the same
    // on every frame and every replay.
    private static GlyphState Scattered(int index, float local, float fontSize)
    {
        var angle = Hash(index) * MathF.Tau;
        var distance = (1f - OutCubic(local)) * fontSize * 0.9f;

        return new GlyphState(
            MathF.Cos(angle) * distance,
            MathF.Sin(angle) * distance,
            Clamp01(local * 2f),
            0f,
            1f);
    }

    private static float FlickerAlpha(int index, float local)
    {
        if (local <= 0f)
            return 0f;

        if (local >= 1f)
            return 1f;

        var step = (int)(local * FlickerSteps);
        var lit = Hash((index * 31) + (step * 7) + 1) < 0.45f + (0.5f * local);

        return lit ? FlickerLit + (0.15f * local) : FlickerDim * local;
    }

    // A cheap integer hash spread over [0, 1). Deterministic, so a scatter or a flicker does not
    // change from frame to frame.
    private static float Hash(int seed)
    {
        unchecked
        {
            var x = (uint)seed * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;

            return (x & 0xFFFFFF) / (float)0x1000000;
        }
    }

    private static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        var u = t - 1f;

        return 1f + (c3 * u * u * u) + (c1 * u * u);
    }

    private static float OutCubic(float t)
    {
        var u = 1f - t;

        return 1f - (u * u * u);
    }

    private static float Clamp01(float value) => float.Clamp(value, 0f, 1f);
}
