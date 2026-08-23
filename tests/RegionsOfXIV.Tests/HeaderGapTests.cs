using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

public class HeaderGapTests
{
    [Fact]
    public void ANewConfigKeepsTheTightGapThePluginHasAlwaysShipped()
    {
        Assert.Equal(Configuration.OverlappedHeaderGap, new Configuration().HeaderGap);
    }

    [Fact]
    public void AnOldConfigThatOverlappedTheHeaderKeepsOverlappingIt()
    {
        var config = new Configuration { Version = 2, OverlapHeader = true, HeaderGap = 99f };

        Assert.True(config.Migrate());
        Assert.Equal(Configuration.OverlappedHeaderGap, config.HeaderGap);
    }

    [Fact]
    public void AnOldConfigThatSpacedTheHeaderKeepsTheWiderGap()
    {
        var config = new Configuration { Version = 2, OverlapHeader = false, HeaderGap = 99f };

        Assert.True(config.Migrate());
        Assert.Equal(Configuration.SpacedHeaderGap, config.HeaderGap);
    }

    [Fact]
    public void AConfigAlreadyOnThisVersionKeepsTheGapItWasGiven()
    {
        var config = new Configuration { HeaderGap = 2.25f };

        Assert.False(config.Migrate());
        Assert.Equal(2.25f, config.HeaderGap);
    }

    [Fact]
    public void TheOldToggleNoLongerTravelsInPresetsSoItCannotOverrideTheGap()
    {
        Assert.Null(ConfigurationCopy.Find("OverlapHeader"));
        Assert.NotNull(ConfigurationCopy.Find("HeaderGap"));
    }

    [Fact]
    public void TheGapTravelsInAShareCode()
    {
        var original = new Configuration { HeaderGap = 2.4f };

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Roomy", original), out var preset, out var error), error);
        Assert.Equal(2.4f, preset!.Settings.HeaderGap);
    }

    [Fact]
    public void TheSliderRangeCoversBothOfTheOldFixedGaps()
    {
        Assert.True(Configuration.OverlappedHeaderGap >= 0.5f);
        Assert.True(Configuration.SpacedHeaderGap <= 2.5f);
    }
}
