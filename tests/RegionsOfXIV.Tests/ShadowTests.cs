using System.Numerics;
using RegionsOfXIV;
using RegionsOfXIV.UI;

namespace RegionsOfXIV.Tests;

public class ShadowTests
{
    private const uint Opaque = 0xFF000000u;

    private const uint Invisible = 0x00000000u;

    [Fact]
    public void NoShadowDrawsNothing()
    {
        Assert.False(Shadow.None.IsVisible);
    }

    [Fact]
    public void AShadowSittingExactlyBehindTheLetterDrawsNothing()
    {
        Assert.False(new Shadow(Opaque, Vector2.Zero, 0f).IsVisible);
    }

    [Fact]
    public void AShadowWithNoOpacityDrawsNothingHoweverFarItIsOffset()
    {
        Assert.False(new Shadow(Invisible, new Vector2(8f, 8f), 4f).IsVisible);
    }

    [Fact]
    public void AnOffsetShadowDraws()
    {
        Assert.True(new Shadow(Opaque, new Vector2(2f, 2f), 0f).IsVisible);
    }

    [Fact]
    public void ASpreadShadowDrawsEvenWithNoOffset()
    {
        Assert.True(new Shadow(Opaque, Vector2.Zero, 1.5f).IsVisible);
    }

    [Fact]
    public void AShadowOffsetBackwardsStillDraws()
    {
        Assert.True(new Shadow(Opaque, new Vector2(-3f, -3f), 0f).IsVisible);
    }

    [Fact]
    public void TheShadowIsOffByDefaultSoNobodysLookChanges()
    {
        var config = new Configuration();

        Assert.False(config.ShadowEnabled);
    }

    [Fact]
    public void TheDefaultShadowIsReadyToSeeTheMomentItIsSwitchedOn()
    {
        var config = new Configuration();

        Assert.True(config.ShadowColor.W >= Configuration.FaintAlpha);
        Assert.True(config.ShadowOffsetX != 0f || config.ShadowOffsetY != 0f);
    }

    [Fact]
    public void TheShadowTravelsInAPresetCode()
    {
        var original = new Configuration
        {
            ShadowEnabled = true,
            ShadowColor = new Vector4(0.2f, 0f, 0.4f, 0.5f),
            ShadowOffsetX = -4f,
            ShadowOffsetY = 6f,
            ShadowSoftness = 1.5f,
        };

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Shade", original), out var preset, out var error), error);

        Assert.True(preset!.Settings.ShadowEnabled);
        Assert.Equal(new Vector4(0.2f, 0f, 0.4f, 0.5f), preset.Settings.ShadowColor);
        Assert.Equal(-4f, preset.Settings.ShadowOffsetX);
        Assert.Equal(6f, preset.Settings.ShadowOffsetY);
        Assert.Equal(1.5f, preset.Settings.ShadowSoftness);
    }
}
