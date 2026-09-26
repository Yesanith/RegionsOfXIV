using RegionsOfXIV;
using RegionsOfXIV.UI;

namespace RegionsOfXIV.Tests;

public class GlyphAnimatorTests
{
    private const float FontSize = 91f;

    private static readonly MotionEffect[] Animated =
    [
        MotionEffect.Typewriter,
        MotionEffect.Rise,
        MotionEffect.Wave,
        MotionEffect.Burn,
        MotionEffect.Drop,
        MotionEffect.Slide,
        MotionEffect.Assemble,
        MotionEffect.Flicker,
        MotionEffect.Zoom,
    ];

    public static TheoryData<MotionEffect> AnimatedEffects()
    {
        var data = new TheoryData<MotionEffect>();
        foreach (var effect in Animated)
            data.Add(effect);

        return data;
    }

    [Theory]
    [MemberData(nameof(AnimatedEffects))]
    public void EveryEffectIsAtRestWhenTheRevealCompletes(MotionEffect effect)
    {
        foreach (var count in new[] { 1, 2, 7, 40 })
        {
            for (var i = 0; i < count; i++)
            {
                var state = GlyphAnimator.For(effect, i, count, 1f, FontSize);

                Assert.Equal(0f, state.OffsetX, 5);
                Assert.Equal(0f, state.OffsetY, 5);
                Assert.Equal(1f, state.Alpha, 5);
                Assert.Equal(0f, state.Heat, 5);
                Assert.Equal(1f, state.Scale, 5);
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

    [Fact]
    public void LaterGlyphsStartLater()
    {
        Assert.True(GlyphAnimator.LocalProgress(11, 12, 0.3f) < GlyphAnimator.LocalProgress(0, 12, 0.3f));
    }

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

    [Fact]
    public void RiseComesUpFromBelowAndOvershoots()
    {
        Assert.True(GlyphAnimator.For(MotionEffect.Rise, 0, 12, 0f, FontSize).OffsetY > 0f);

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

    [Fact]
    public void TypewriterNeverDrawsAPartialGlyph()
    {
        for (var step = 0; step <= 100; step++)
        {
            var alpha = GlyphAnimator.For(MotionEffect.Typewriter, 5, 12, step / 100f, FontSize).Alpha;

            Assert.True(alpha is 0f or 1f, $"alpha was {alpha} at {step}%");
        }
    }

    private static bool IsSettled(MotionEffect effect, float progress, int count = 16)
    {
        for (var i = 0; i < count; i++)
        {
            var now = GlyphAnimator.For(effect, i, count, progress, FontSize);
            var end = GlyphAnimator.For(effect, i, count, 1f, FontSize);

            if (MathF.Abs(now.Alpha - end.Alpha) > 0.001f
                || MathF.Abs(now.OffsetX - end.OffsetX) > 0.01f
                || MathF.Abs(now.OffsetY - end.OffsetY) > 0.01f
                || MathF.Abs(now.Heat - end.Heat) > 0.001f
                || MathF.Abs(now.Scale - end.Scale) > 0.001f)
                return false;
        }

        return true;
    }

    [Theory]
    [MemberData(nameof(AnimatedEffects))]
    public void EveryEffectKeepsMovingUntilTheEndOfItsStage(MotionEffect effect)
    {
        Assert.False(
            IsSettled(effect, 0.85f),
            $"{effect} had already finished at 85% and stalls for the rest of its stage");
    }

    private static (float Lowest, float Highest) TravelWhileLegible(MotionEffect effect)
    {
        float lowest = 0f, highest = 0f;
        var seen = false;

        for (var step = 0; step <= 1000; step++)
        {
            var state = GlyphAnimator.For(effect, 0, 1, step / 1000f, FontSize);
            if (state.Alpha < 0.25f)
                continue;

            if (!seen)
            {
                lowest = highest = state.OffsetY;
                seen = true;
            }

            lowest = MathF.Max(lowest, state.OffsetY);
            highest = MathF.Min(highest, state.OffsetY);
        }

        return (lowest, highest);
    }

    [Fact]
    public void RiseIsStillBelowTheLineWhenItBecomesLegible()
    {
        var start = GlyphAnimator.For(MotionEffect.Rise, 0, 1, 0f, FontSize).OffsetY;
        var (lowest, _) = TravelWhileLegible(MotionEffect.Rise);

        Assert.True(
            lowest > start * 0.5f,
            $"Rise was only {lowest:0.0}px below its target once legible, out of {start:0.0}px of travel");
    }

    [Fact]
    public void RiseAndWaveDoNotLookLikeEachOther()
    {
        var (riseLowest, riseHighest) = TravelWhileLegible(MotionEffect.Rise);
        var (waveLowest, waveHighest) = TravelWhileLegible(MotionEffect.Wave);

        Assert.True(riseLowest > 0f, "Rise never appears below the line while legible");
        Assert.True(waveLowest <= 0.001f, "Wave should never appear below the line");

        Assert.True(
            MathF.Abs(riseLowest - riseHighest) > MathF.Abs(waveLowest - waveHighest) * 2f,
            "Rise should travel visibly further than Wave");
    }

    [Fact]
    public void DropComesDownFromAbove()
    {
        Assert.True(GlyphAnimator.For(MotionEffect.Drop, 0, 12, 0f, FontSize).OffsetY < 0f);
    }

    [Fact]
    public void SlideComesInFromTheRightAndOnlyMovesSideways()
    {
        var start = GlyphAnimator.For(MotionEffect.Slide, 0, 1, 0f, FontSize);

        Assert.True(start.OffsetX > 0f);
        Assert.Equal(0f, start.OffsetY, 5);
    }

    // The scatter is fixed by the glyph's index, so a replay looks like the first showing.
    [Fact]
    public void AssembleScattersEachGlyphTheSameWayEveryTime()
    {
        var first = GlyphAnimator.For(MotionEffect.Assemble, 4, 12, 0.1f, FontSize);
        var again = GlyphAnimator.For(MotionEffect.Assemble, 4, 12, 0.1f, FontSize);

        Assert.Equal(first, again);
        Assert.True(first.OffsetX != 0f || first.OffsetY != 0f, "the glyph started in place");
    }

    [Fact]
    public void AssembleOnlyEverConverges()
    {
        var previous = float.MaxValue;

        for (var step = 0; step <= 100; step++)
        {
            var state = GlyphAnimator.For(MotionEffect.Assemble, 2, 1, step / 100f, FontSize);
            var distance = MathF.Sqrt((state.OffsetX * state.OffsetX) + (state.OffsetY * state.OffsetY));

            Assert.True(distance <= previous + 0.0001f, $"glyph moved away again at {step}%");
            previous = distance;
        }
    }

    // Never quite solid until its window ends, so a line visibly settles rather than happening
    // to be lit when the stage runs out.
    [Fact]
    public void FlickerIsNeverSolidBeforeItsWindowEnds()
    {
        for (var step = 1; step < 100; step++)
        {
            var alpha = GlyphAnimator.For(MotionEffect.Flicker, 0, 1, step / 100f, FontSize).Alpha;

            Assert.True(alpha < 1f, $"flicker was solid at {step}%");
        }
    }

    [Fact]
    public void ZoomGrowsIntoPlaceAndOvershootsOnTheWay()
    {
        Assert.True(GlyphAnimator.For(MotionEffect.Zoom, 0, 1, 0.05f, FontSize).Scale < 0.5f);

        var largest = 0f;
        for (var step = 0; step <= 100; step++)
            largest = MathF.Max(largest, GlyphAnimator.For(MotionEffect.Zoom, 0, 1, step / 100f, FontSize).Scale);

        Assert.True(largest > 1f, "Zoom never overshot its resting size");
    }

    [Fact]
    public void APlainFadeHasNoDepartureMotion()
    {
        Assert.Null(GlyphAnimator.DepartureMotion(DepartureEffect.Fade, MotionEffect.Rise));
    }

    [Fact]
    public void ReversingAnArrivalPlaysThatArrival()
    {
        Assert.Equal(MotionEffect.Wave, GlyphAnimator.DepartureMotion(DepartureEffect.Reverse, MotionEffect.Wave));
    }

    // There is nothing to run backwards, so the line fades as it would have anyway.
    [Fact]
    public void ReversingNoArrivalIsAPlainFade()
    {
        Assert.Null(GlyphAnimator.DepartureMotion(DepartureEffect.Reverse, MotionEffect.None));
    }

    [Fact]
    public void FallingAndDissolvingDoNotDependOnTheArrival()
    {
        Assert.Equal(MotionEffect.Rise, GlyphAnimator.DepartureMotion(DepartureEffect.Fall, MotionEffect.Burn));
        Assert.Equal(MotionEffect.Assemble, GlyphAnimator.DepartureMotion(DepartureEffect.Dissolve, MotionEffect.None));
    }
}
