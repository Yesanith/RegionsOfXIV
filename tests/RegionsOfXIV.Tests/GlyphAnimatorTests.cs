using RegionsOfXIV;
using RegionsOfXIV.UI;

namespace RegionsOfXIV.Tests;

// The motion effects, which are otherwise judged by watching them. What is
// checked here is not how they look — that is a matter of taste and a running
// game — but the handful of properties that make them safe to hand back to the
// plain renderer, and that would show up in game as a flicker or a jump rather
// than as an obvious fault.
//
// GlyphAnimator is pure arithmetic over System types, so this needs no font
// atlas, no ImGui and no Dalamud. Same seam as NotificationGate.
public class GlyphAnimatorTests
{
    private const float FontSize = 91f;

    private static readonly MotionEffect[] Animated =
    [
        MotionEffect.Typewriter,
        MotionEffect.Rise,
        MotionEffect.Wave,
        MotionEffect.Burn,
    ];

    public static TheoryData<MotionEffect> AnimatedEffects()
    {
        var data = new TheoryData<MotionEffect>();
        foreach (var effect in Animated)
            data.Add(effect);

        return data;
    }

    // The one that matters most. NotificationOverlay stops animating at full
    // progress and draws the line in a single run instead, so every effect has to
    // already be exactly at rest by then — otherwise the last frame of the reveal
    // jumps.
    [Theory]
    [MemberData(nameof(AnimatedEffects))]
    public void EveryEffectIsAtRestWhenTheRevealCompletes(MotionEffect effect)
    {
        foreach (var count in new[] { 1, 2, 7, 40 })
        {
            for (var i = 0; i < count; i++)
            {
                var state = GlyphAnimator.For(effect, i, count, 1f, FontSize);

                Assert.Equal(0f, state.OffsetY, 5);
                Assert.Equal(1f, state.Alpha, 5);
                Assert.Equal(0f, state.Heat, 5);
            }
        }
    }

    [Theory]
    [MemberData(nameof(AnimatedEffects))]
    public void NothingIsDrawnBeforeTheRevealStarts(MotionEffect effect)
    {
        Assert.Equal(0f, GlyphAnimator.For(effect, 0, 12, 0f, FontSize).Alpha);
    }

    [Theory]
    [MemberData(nameof(AnimatedEffects))]
    public void AlphaStaysWithinRange(MotionEffect effect)
    {
        for (var i = 0; i < 20; i++)
        {
            for (var step = 0; step <= 50; step++)
            {
                var alpha = GlyphAnimator.For(effect, i, 20, step / 50f, FontSize).Alpha;

                Assert.InRange(alpha, 0f, 1f);
            }
        }
    }

    // --- the stagger --------------------------------------------------------

    [Fact]
    public void LaterGlyphsStartLater()
    {
        Assert.True(GlyphAnimator.LocalProgress(11, 12, 0.3f) < GlyphAnimator.LocalProgress(0, 12, 0.3f));
    }

    // The last glyph must finish exactly as the reveal does, or the line is still
    // arriving when the overlay switches to the plain run.
    [Fact]
    public void TheLastGlyphCompletesExactlyAtTheEnd()
    {
        Assert.Equal(1f, GlyphAnimator.LocalProgress(11, 12, 1f), 5);
    }

    [Fact]
    public void ASingleGlyphFollowsTheRevealDirectly()
    {
        Assert.Equal(0.42f, GlyphAnimator.LocalProgress(0, 1, 0.42f), 5);
    }

    [Fact]
    public void NoGlyphEverRunsBackwards()
    {
        for (var i = 0; i < 12; i++)
        {
            var previous = -1f;
            for (var step = 0; step <= 100; step++)
            {
                var current = GlyphAnimator.LocalProgress(i, 12, step / 100f);

                Assert.True(current >= previous, $"glyph {i} went backwards at {step}%");
                previous = current;
            }
        }
    }

    // --- what the decode rides on -------------------------------------------
    //
    // With a motion running, NotificationOverlay stops flipping glyphs out of
    // Eorzean on a count of its own and asks the stagger instead: a glyph has
    // resolved once it is past the midpoint of its own animation. These pin the
    // two properties that rule has to have.

    [Fact]
    public void EveryGlyphIsPastTheMidpointByTheEnd()
    {
        // One still short of it would be swapped from scrambled to readable in a
        // single frame when the overlay hands over to the plain run.
        foreach (var count in new[] { 1, 2, 7, 40 })
        {
            for (var i = 0; i < count; i++)
                Assert.True(GlyphAnimator.LocalProgress(i, count, 1f) > 0.5f, $"glyph {i} of {count}");
        }
    }

    [Fact]
    public void NoGlyphIsPastTheMidpointAtTheStart()
    {
        for (var i = 0; i < 20; i++)
            Assert.False(GlyphAnimator.LocalProgress(i, 20, 0f) > 0.5f, $"glyph {i}");
    }

    // --- individual effects -------------------------------------------------

    [Fact]
    public void RiseComesUpFromBelowAndOvershoots()
    {
        Assert.True(GlyphAnimator.For(MotionEffect.Rise, 0, 12, 0f, FontSize).OffsetY > 0f);

        // Somewhere in the middle it must pass its target and come back, which is
        // what separates this from a plain ease-out.
        var highest = 0f;
        for (var step = 0; step <= 100; step++)
            highest = MathF.Min(highest, GlyphAnimator.For(MotionEffect.Rise, 0, 1, step / 100f, FontSize).OffsetY);

        Assert.True(highest < 0f, "Rise never overshot its target");
    }

    [Fact]
    public void WaveRestsAtBothEndsOfItsBump()
    {
        Assert.Equal(0f, GlyphAnimator.For(MotionEffect.Wave, 0, 12, 0f, FontSize).OffsetY, 5);
        Assert.Equal(0f, GlyphAnimator.For(MotionEffect.Wave, 0, 12, 1f, FontSize).OffsetY, 5);
    }

    [Fact]
    public void BurnStartsHotAndOnlyCools()
    {
        var previous = float.MaxValue;

        for (var step = 0; step <= 100; step++)
        {
            var heat = GlyphAnimator.For(MotionEffect.Burn, 3, 12, step / 100f, FontSize).Heat;

            Assert.True(heat <= previous, $"heat rose again at {step}%");
            previous = heat;
        }

        Assert.Equal(1f, GlyphAnimator.For(MotionEffect.Burn, 3, 12, 0f, FontSize).Heat, 5);
    }

    // Typewriter is the one effect with no in-between: a glyph is typed or it is
    // not, and half-drawn letters would give away that it is a fade underneath.
    [Fact]
    public void TypewriterNeverDrawsAPartialGlyph()
    {
        for (var step = 0; step <= 100; step++)
        {
            var alpha = GlyphAnimator.For(MotionEffect.Typewriter, 5, 12, step / 100f, FontSize).Alpha;

            Assert.True(alpha is 0f or 1f, $"alpha was {alpha} at {step}%");
        }
    }
}
