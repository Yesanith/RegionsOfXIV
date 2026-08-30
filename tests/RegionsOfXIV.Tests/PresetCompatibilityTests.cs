using System.Numerics;
using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

// Codes made by v0.3.0.0, which wrote config version 1 and stored OverlapHeader where this build
// reads HeaderGap. Kept as literals rather than generated, because the point is to exercise what
// an old build actually produced -- a code built by today's encoder cannot reproduce it.
public class PresetCompatibilityTests
{
    private const string SpacedHeaderCode =
        "ROX1-VY_LCsIwFER_RWYdpCo-uFtB3EgXUXzsQnOFYmhCEotY8u_Wmi5cnc2ZGaZDA0Jp9EQ6VbGGQAuaCQRQh7Jlb5Tbs9LsQXdlAgvI6O2Dt9ZY_5UuoGJaCFwzb5nngUngF__319lfZX85-ps-cHKOfaUCH_kVQdE_eWzZ2SbK-s2gxTBysLG2_YV5Sh8";

    // OverlapHeader defaulted to true in 0.3.0.0, and only settings differing from the defaults
    // were stored, so a code from someone using the overlapped header never mentioned it at all.
    private const string OverlappedHeaderCode =
        "ROX1-q1bKU7JS8s9JUfAvSy3KSSwoSE1R0lEqU7Iy1FEqVrKqVgoFChUlJxanhqRWlChZlRSVptbWAgA";

    private static UserPreset Decode(string code)
    {
        Assert.True(PresetCode.TryDecode(code, out var preset, out var error), error);

        return preset!;
    }

    [Fact]
    public void ACodeFromBeforeTheGapSliderStillReads()
    {
        var preset = Decode(SpacedHeaderCode);

        Assert.Equal("Old Spaced", preset.Name);
    }

    // The setting was replaced, not removed, so the spacing someone chose has to survive the
    // change of representation rather than falling back to the default.
    [Fact]
    public void ASpacedHeaderSurvivesTheMoveToTheGapSlider()
    {
        var preset = Decode(SpacedHeaderCode);

        Assert.Equal(Configuration.SpacedHeaderGap, preset.Settings.HeaderGap);
    }

    [Fact]
    public void AnOverlappedHeaderSurvivesItToo()
    {
        var preset = Decode(OverlappedHeaderCode);

        Assert.Equal(Configuration.OverlappedHeaderGap, preset.Settings.HeaderGap);
    }

    // Weather got its own size in 0.4.0.0, seeded from the header's. A code written before that
    // has no weather size in it, and must land on the same answer the config migration gives.
    [Fact]
    public void ACodeFromBeforeWeatherHadItsOwnSizeInheritsTheHeaderSize()
    {
        var preset = Decode(SpacedHeaderCode);

        Assert.Equal(30f, preset.Settings.HeaderFontSize);
        Assert.Equal(30f, preset.Settings.WeatherFontSize);
    }

    [Fact]
    public void EverythingElseInAnOldCodeStillArrives()
    {
        var settings = Decode(SpacedHeaderCode).Settings;

        Assert.True(settings.UppercaseText);
        Assert.Equal(MotionEffect.Rise, settings.Motion);
    }

    // 0.3.0.0 could store a colour dragged to invisible. Applying such a code must not hand
    // someone a line they cannot see and cannot recover from.
    [Fact]
    public void AnInvisibleColourInAnOldCodeIsBroughtBackWhenItIsApplied()
    {
        var live = new Configuration();

        Decode(SpacedHeaderCode).ApplyTo(live);

        Assert.Equal(Configuration.MinAlpha, live.StrokeColor.W);
        Assert.Equal(Configuration.MinAlpha, live.HeaderColor.W);
    }

    [Fact]
    public void AnOldCodeAppliesItsGapToTheLiveConfig()
    {
        var live = new Configuration();

        Decode(SpacedHeaderCode).ApplyTo(live);

        Assert.Equal(Configuration.SpacedHeaderGap, live.HeaderGap);
    }

    // The migration must only fire for codes that need it: a code written by this build already
    // holds HeaderGap directly, and must not have it recomputed from the superseded flag.
    [Fact]
    public void ACodeFromThisBuildIsNotMigratedOutOfShape()
    {
        var source = new Configuration { HeaderGap = 2.4f, WeatherFontSize = 41f };

        var preset = Decode(PresetCode.Encode("Current", source));

        Assert.Equal(2.4f, preset.Settings.HeaderGap);
        Assert.Equal(41f, preset.Settings.WeatherFontSize);
    }

    [Fact]
    public void ASupersededSettingIsStillNotWrittenIntoNewCodes()
    {
        var source = new Configuration { OverlapHeader = false, HeaderGap = 2.4f };

        var preset = Decode(PresetCode.Encode("Current", source));

        Assert.True(preset.Settings.OverlapHeader);
        Assert.Equal(2.4f, preset.Settings.HeaderGap);
    }

    [Fact]
    public void APresetSavedNowNeedsNoMigrationLater()
    {
        var preset = UserPreset.Capture("Mine", new Configuration());

        Assert.Equal(Configuration.CurrentVersion, preset.Settings.Version);
        Assert.False(preset.Settings.Migrate());
    }

    // What a preset saved by an older build looks like once it has been read back out of the
    // config file: its own Version, and the superseded flag still set.
    [Fact]
    public void APresetSavedByAnOlderBuildMigratesLikeTheConfigDoes()
    {
        var stored = new UserPreset
        {
            Name = "Saved in 0.3.0.0",
            Settings = new Configuration
            {
                Version = 1,
                OverlapHeader = false,
                HeaderFontSize = 30f,
                StrokeColor = new Vector4(0f, 0f, 0f, 0f),
            },
        };

        Assert.True(stored.Settings.Migrate());

        Assert.Equal(Configuration.SpacedHeaderGap, stored.Settings.HeaderGap);
        Assert.Equal(30f, stored.Settings.WeatherFontSize);

        var live = new Configuration();
        stored.ApplyTo(live);

        Assert.Equal(Configuration.SpacedHeaderGap, live.HeaderGap);
        Assert.Equal(Configuration.MinAlpha, live.StrokeColor.W);
    }
}
