using System;

namespace RegionsOfXIV.UI;

internal readonly record struct GlyphState(float OffsetY, float Alpha, float Heat);

internal static class GlyphAnimator
{
    private const float GlyphWindow = 0.45f;

    public static GlyphState For(MotionEffect effect, int index, int count, float progress, float fontSize)
    {
        var local = LocalProgress(index, count, progress);

        return effect switch
        {
            MotionEffect.Typewriter => new GlyphState(0f, local > 0f ? 1f : 0f, 0f),

            MotionEffect.Rise => new GlyphState((1f - OutBack(local)) * fontSize * 0.5f, local, 0f),

            MotionEffect.Wave => new GlyphState(
                -MathF.Sin(local * MathF.PI) * fontSize * 0.18f,
                Clamp01(local * 3f),
                0f),

            MotionEffect.Burn => new GlyphState(0f, local, 1f - local),

            _ => new GlyphState(0f, 1f, 0f),
        };
    }

    public static float LocalProgress(int index, int count, float progress)
    {
        if (count <= 1)
            return Clamp01(progress);

        var start = index / (float)(count - 1) * (1f - GlyphWindow);

        return Clamp01((progress - start) / GlyphWindow);
    }

    private static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        var u = t - 1f;

        return 1f + (c3 * u * u * u) + (c1 * u * u);
    }

    private static float Clamp01(float value) => float.Clamp(value, 0f, 1f);
}
