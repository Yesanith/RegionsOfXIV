using System.IO;
using RegionsOfXIV;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

public class FontTests
{
    [Theory]
    [InlineData(FontRole.Text)]
    [InlineData(FontRole.Header)]
    [InlineData(FontRole.Weather)]
    public void EveryRoleReadsBackTheFaceItWasGiven(FontRole role)
    {
        var config = new Configuration();
        var wanted = new FontSetting(FontChoice.Jupiter, @"C:\fonts\mine.ttf", 55f);

        config.SetFontFor(role, wanted);

        Assert.Equal(wanted, config.FontFor(role));
    }

    [Fact]
    public void APathIsNeverNullHoweverTheSettingWasBuilt()
    {
        Assert.Equal(string.Empty, new FontSetting(FontChoice.Custom, null!, 24f).Path);

        var cleared = new FontSetting(FontChoice.Custom, "mine.ttf", 24f) with { Path = null! };

        Assert.Equal(string.Empty, cleared.Path);
    }

    [Theory]
    [InlineData(FontRole.Text)]
    [InlineData(FontRole.Header)]
    [InlineData(FontRole.Weather)]
    public void SettingOneRoleLeavesTheOthersAlone(FontRole role)
    {
        var config = new Configuration();
        var defaults = new Configuration();

        config.SetFontFor(role, new FontSetting(FontChoice.Custom, "anything.ttf", 99f));

        foreach (var other in Enum.GetValues<FontRole>())
        {
            if (other == role)
                continue;

            Assert.Equal(defaults.FontFor(other), config.FontFor(other));
        }
    }

    [Fact]
    public void TheDefaultsUseNoCustomFont()
    {
        var config = new Configuration();

        Assert.False(config.UsesCustomFont);
        Assert.Equal(string.Empty, config.DisplayFontPath);
        Assert.Equal(string.Empty, config.HeaderFontPath);
        Assert.Equal(string.Empty, config.WeatherFontPath);
    }

    [Theory]
    [InlineData(FontRole.Text)]
    [InlineData(FontRole.Header)]
    [InlineData(FontRole.Weather)]
    public void OneCustomFaceIsEnoughToWarnAPresetIsLocal(FontRole role)
    {
        var config = new Configuration();
        config.SetFontFor(role, config.FontFor(role) with { Choice = FontChoice.Custom });

        Assert.True(config.UsesCustomFont);
        Assert.True(config.FontFor(role).IsCustom);
    }

    [Fact]
    public void TheHeaderAndWeatherStartOnTheGameFaceTheyAlwaysUsed()
    {
        var config = new Configuration();

        Assert.Equal(FontChoice.Axis, config.HeaderFont);
        Assert.Equal(FontChoice.Axis, config.WeatherFont);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void APathThatIsNotThereIsRefusedBeforeAnythingLoads(string? path)
    {
        Assert.NotNull(FontLimits.CustomFontProblem(path));
    }

    [Theory]
    [InlineData("mine.exe")]
    [InlineData("mine.png")]
    [InlineData("mine")]
    public void OnlyFontFileTypesAreAccepted(string name)
    {
        Assert.NotNull(FontLimits.CustomFontProblem(Path.Combine(Path.GetTempPath(), name)));
    }

    [Fact]
    public void AFontFileThatIsNoLongerThereIsRefused()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"regions-{Guid.NewGuid():N}.ttf");

        Assert.NotNull(FontLimits.CustomFontProblem(missing));
    }

    [Fact]
    public void AnEmptyFileIsRefused()
    {
        var path = Path.Combine(Path.GetTempPath(), $"regions-{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(path, []);

        try
        {
            Assert.NotNull(FontLimits.CustomFontProblem(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".ttf")]
    [InlineData(".otf")]
    [InlineData(".TTC")]
    public void AFontFileThatIsThereIsAccepted(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"regions-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, [0x00, 0x01, 0x00, 0x00]);

        try
        {
            Assert.Null(FontLimits.CustomFontProblem(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EveryBuiltInFaceReportsWhetherItCoversJapanese()
    {
        Assert.True(FontLimits.IsLatinOnly(FontChoice.TrumpGothic));
        Assert.True(FontLimits.IsLatinOnly(FontChoice.Jupiter));
        Assert.False(FontLimits.IsLatinOnly(FontChoice.Axis));
        Assert.False(FontLimits.IsLatinOnly(FontChoice.NotoSansCjk));
    }

    [Fact]
    public void ACustomFaceIsNeverWarnedAboutAsLatinOnlyOrUpscaled()
    {
        Assert.False(FontLimits.IsLatinOnly(FontChoice.Custom));
        Assert.Equal(float.PositiveInfinity, FontLimits.NativeCeilingPx(FontChoice.Custom));
    }

    [Fact]
    public void TheGameFacesKeepTheirBitmapCeilings()
    {
        Assert.True(FontLimits.NativeCeilingPx(FontChoice.Jupiter) < 62f);
        Assert.True(FontLimits.NativeCeilingPx(FontChoice.Axis) < 49f);
        Assert.True(FontLimits.NativeCeilingPx(FontChoice.TrumpGothic) < 92f);
        Assert.Equal(float.PositiveInfinity, FontLimits.NativeCeilingPx(FontChoice.NotoSansCjk));
    }
}
