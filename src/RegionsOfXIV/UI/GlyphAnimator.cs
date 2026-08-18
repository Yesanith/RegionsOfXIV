using System;

namespace RegionsOfXIV.UI;

// What one glyph is doing at one moment of the reveal.
//
// Heat is deliberately not a colour: the animator knows how far through its burn
// a glyph is, and the overlay knows what colour the text is meant to end up. One
// of those is animation and the other is configuration, and mixing them here
// would put the config's palette inside a maths helper.
internal readonly record struct GlyphState(float OffsetY, float Alpha, float Heat);

// Per-glyph animation for the motion effects.
//
// Every effect is driven from one number — the notification's RevealProgress,
// 0..1 — turned into a *per-glyph* progress by staggering the start times. That
// stagger is the whole trick: the same easing applied to every glyph at once
// reads as the line moving, while the same easing applied a few milliseconds
// apart reads as the line being written.
//
// Pure arithmetic, no ImGui, no config: it can be reasoned about (and tested)
// without a font atlas or a running game.
internal static class GlyphAnimator
{
    // The share of the reveal one glyph gets to itself. Well under 1, so the
    // glyphs overlap heavily — a window of 1 would mean every glyph animating
    // across the whole reveal and the stagger vanishing; a very small window
    // would make the line snap in glyph by glyph with visible gaps between.
    private const float GlyphWindow = 0.45f;

    public static GlyphState For(MotionEffect effect, int index, int count, float progress, float fontSize)
    {
        var local = LocalProgress(index, count, progress);

        return effect switch
        {
            // A hard cut, no fade: the glyph is either typed or it is not.
            MotionEffect.Typewriter => new GlyphState(0f, local > 0f ? 1f : 0f, 0f),

            // Up from below, overshooting slightly and settling back — see OutBack.
            MotionEffect.Rise => new GlyphState((1f - OutBack(local)) * fontSize * 0.5f, local, 0f),

            // A single bump that returns to the baseline. Travelling, because the
            // stagger puts each glyph at a different point of its own bump.
            //
            // Alpha runs three times faster than the bump so the glyph is visible
            // for nearly all of its ride rather than fading in through it.
            MotionEffect.Wave => new GlyphState(
                -MathF.Sin(local * MathF.PI) * fontSize * 0.18f,
                Clamp01(local * 3f),
                0f),

            // Heat runs the other way from progress: 1 at ignition, 0 once the
            // glyph has cooled into the configured colour.
            MotionEffect.Burn => new GlyphState(0f, local, 1f - local),

            // None: every glyph sits where it belongs, fully opaque, from the
            // first frame. The decode may still be running over the top of that —
            // it is a substitution rather than a displacement, and has its own
            // path in the overlay.
            _ => new GlyphState(0f, 1f, 0f),
        };
    }

    // Where a glyph is in its own animation, given where the line is in the
    // reveal as a whole.
    //
    // Glyph 0 starts at 0 and the last glyph starts at 1 - GlyphWindow, so the
    // final glyph finishes exactly as the reveal does. A one-glyph line has no
    // stagger to spread and simply follows the reveal.
    public static float LocalProgress(int index, int count, float progress)
    {
        if (count <= 1)
            return Clamp01(progress);

        var start = index / (float)(count - 1) * (1f - GlyphWindow);

        return Clamp01((progress - start) / GlyphWindow);
    }

    // The "back" ease: overshoots its target and settles onto it. The constants
    // are the conventional ones — c1 sets how far past 1 it goes, which at this
    // value is about 10%.
    //
    // Written out rather than taken from Dalamud's easing set because those are
    // stateful objects driven by their own clocks, and what is wanted here is a
    // plain function of t that can be called per glyph per frame.
    private static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        var u = t - 1f;

        return 1f + (c3 * u * u * u) + (c1 * u * u);
    }

    private static float Clamp01(float value) => float.Clamp(value, 0f, 1f);
}
