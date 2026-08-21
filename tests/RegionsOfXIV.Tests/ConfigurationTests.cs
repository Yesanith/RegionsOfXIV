using System.Numerics;
using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

public class ConfigurationTests
{
    [Fact]
    public void AColourStoredTooFaintToSeeGetsItsOpacityBack()
    {
        var config = new Configuration
        {
            StrokeColor = new Vector4(0.37f, 0.24f, 0.13f, 1f / 255f),
        };

        Assert.True(config.RepairFaintColors());

        Assert.Equal(new Configuration().StrokeColor.W, config.StrokeColor.W);
        Assert.Equal(0.37f, config.StrokeColor.X);
        Assert.Equal(0.24f, config.StrokeColor.Y);
        Assert.Equal(0.13f, config.StrokeColor.Z);
    }

    [Fact]
    public void AFullyTransparentColourIsRepairedToo()
    {
        var config = new Configuration
        {
            TextColor = new Vector4(1f, 1f, 1f, 0f),
        };

        Assert.True(config.RepairFaintColors());
        Assert.Equal(1f, config.TextColor.W);
    }

    [Fact]
    public void ColoursThatCanBeSeenAreLeftAlone()
    {
        var config = new Configuration();

        Assert.False(config.RepairFaintColors());
        Assert.Equal(0.8f, config.StrokeColor.W);
    }

    [Fact]
    public void TheDefaultsAreNeverConsideredFaint()
    {
        var config = new Configuration();

        foreach (var property in ConfigurationCopy.Settings)
        {
            if (property.PropertyType != typeof(Vector4))
                continue;

            var colour = (Vector4)property.GetValue(config)!;
            Assert.True(colour.W >= Configuration.FaintAlpha, property.Name);
        }
    }

    [Fact]
    public void ApplyingAPresetRepairsAFaintColourItCarries()
    {
        var source = new Configuration
        {
            HeaderColor = new Vector4(0.5f, 0.5f, 0.5f, 0f),
        };

        var target = new Configuration();
        ConfigurationCopy.Apply(source, target);

        Assert.Equal(new Configuration().HeaderColor.W, target.HeaderColor.W);
        Assert.Equal(0.5f, target.HeaderColor.X);
    }

    [Fact]
    public void AConfigWrittenBeforeWeatherHadItsOwnSizeKeepsTheSizeItWasShowing()
    {
        var config = new Configuration
        {
            Version = 1,
            HeaderFontSize = 40f,
        };

        Assert.True(config.Migrate());

        Assert.Equal(40f, config.WeatherFontSize);
        Assert.Equal(Configuration.CurrentVersion, config.Version);
    }

    [Fact]
    public void AConfigAlreadyOnThisVersionIsLeftAlone()
    {
        var config = new Configuration
        {
            HeaderFontSize = 40f,
            WeatherFontSize = 12f,
        };

        Assert.False(config.Migrate());
        Assert.Equal(12f, config.WeatherFontSize);
    }

    [Fact]
    public void EveryBuiltInPresetLeavesEveryColourVisible()
    {
        foreach (var preset in Presets.All)
        {
            var config = new Configuration();
            preset.ApplyTo(config);

            Assert.False(config.RepairFaintColors(), preset.Name);
        }
    }
}
