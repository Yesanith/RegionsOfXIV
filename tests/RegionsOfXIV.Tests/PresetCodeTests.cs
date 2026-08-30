using System.Numerics;
using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

// Shares a collection with LocalizationTests so the two cannot run at once. Loc keeps one table for
// the whole plugin and LocalizationTests swaps it about, while PresetCode.TryDecode now reads it for
// its error text. Nothing here asserts on that text today -- only that it is non-empty -- but an
// exact-text assertion added later would flake at random without this.
[Collection("Localization")]
public class PresetCodeTests
{
    private static Configuration Sample() => new()
    {
        Motion = MotionEffect.Burn,
        Particles = ParticleEffect.Embers,
        DisplayFont = FontChoice.Jupiter,
        DisplayFontSize = 61f,
        LetterSpacing = 12.5f,
        UppercaseText = true,
        HideInCombat = true,
        TextColor = new Vector4(0.1f, 0.2f, 0.3f, 0.4f),
        ShowDuration = TimeSpan.FromSeconds(7.25),
    };

    [Fact]
    public void RoundTripsEverySettingThatDiffersFromTheDefault()
    {
        var original = Sample();

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Test", original), out var preset, out var error), error);
        Assert.NotNull(preset);
        Assert.Equal("Test", preset!.Name);

        foreach (var property in ConfigurationCopy.Settings)
            Assert.Equal(property.GetValue(original), property.GetValue(preset.Settings));
    }

    [Fact]
    public void CarriesTheColourStructIntact()
    {
        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Colour", Sample()), out var preset, out _));
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), preset!.Settings.TextColor);
    }

    [Fact]
    public void CarriesTimeSpansIntact()
    {
        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Timing", Sample()), out var preset, out _));
        Assert.Equal(TimeSpan.FromSeconds(7.25), preset!.Settings.ShowDuration);
    }

    [Fact]
    public void DoesNotCarryTheSavedPresetList()
    {
        var config = Sample();
        config.UserPresets.Add(UserPreset.Capture("Someone else's", Sample()));

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Solo", config), out var preset, out _));
        Assert.Empty(preset!.Settings.UserPresets);
    }

    [Fact]
    public void LeavesUnmentionedSettingsAtTheirDefaults()
    {
        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Bare", new Configuration()), out var preset, out _));

        var defaults = new Configuration();
        foreach (var property in ConfigurationCopy.Settings)
            Assert.Equal(property.GetValue(defaults), property.GetValue(preset!.Settings));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(5)]
    [InlineData(-1)]
    public void IgnoresAnEffectThisBuildDoesNotHave(int unknown)
    {
        var future = Sample();
        future.Motion = (MotionEffect)unknown;
        future.Particles = (ParticleEffect)unknown;
        future.DisplayFont = (FontChoice)unknown;

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Future", future), out var preset, out var error), error);

        var defaults = new Configuration();
        Assert.Equal(defaults.Motion, preset!.Settings.Motion);
        Assert.Equal(defaults.Particles, preset.Settings.Particles);
        Assert.Equal(defaults.DisplayFont, preset.Settings.DisplayFont);
    }

    [Fact]
    public void KeepsTheRestOfACodeThatNamesAnUnknownEffect()
    {
        var future = Sample();
        future.Motion = (MotionEffect)99;

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Future", future), out var preset, out _));

        Assert.Equal(ParticleEffect.Embers, preset!.Settings.Particles);
        Assert.Equal(12.5f, preset.Settings.LetterSpacing);
        Assert.True(preset.Settings.UppercaseText);
        Assert.Equal(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), preset.Settings.TextColor);
    }

    [Fact]
    public void PinsTheEffectOrdinalsThatCodesAlreadyCarry()
    {
        Assert.Equal(0, (int)MotionEffect.None);
        Assert.Equal(1, (int)MotionEffect.Typewriter);
        Assert.Equal(2, (int)MotionEffect.Rise);
        Assert.Equal(3, (int)MotionEffect.Wave);
        Assert.Equal(4, (int)MotionEffect.Burn);

        Assert.Equal(0, (int)ParticleEffect.None);
        Assert.Equal(1, (int)ParticleEffect.Hearts);
        Assert.Equal(2, (int)ParticleEffect.Embers);
        Assert.Equal(3, (int)ParticleEffect.Sparkles);
        Assert.Equal(4, (int)ParticleEffect.Petals);

        Assert.Equal(0, (int)FontChoice.TrumpGothic);
        Assert.Equal(1, (int)FontChoice.Jupiter);
        Assert.Equal(2, (int)FontChoice.Axis);
        Assert.Equal(3, (int)FontChoice.NotoSansCjk);
        Assert.Equal(4, (int)FontChoice.Custom);

        Assert.Equal(0, (int)FontRole.Text);
        Assert.Equal(1, (int)FontRole.Header);
        Assert.Equal(2, (int)FontRole.Weather);
    }

    [Fact]
    public void ProducesOnePasteableToken()
    {
        var code = PresetCode.Encode("Token", Sample());

        Assert.StartsWith("ROX1-", code);
        Assert.DoesNotContain('+', code);
        Assert.DoesNotContain('/', code);
        Assert.DoesNotContain('=', code);
        Assert.DoesNotContain(' ', code);
    }

    [Fact]
    public void StaysShortEnoughForAChatMessage()
    {
        var code = PresetCode.Encode("Length", Sample());
        Assert.True(code.Length < 400, $"Code was {code.Length} characters.");
    }

    [Fact]
    public void SurvivesSurroundingWhitespace()
    {
        var code = PresetCode.Encode("Padded", Sample());
        Assert.True(PresetCode.TryDecode($"  \r\n{code}\n  ", out var preset, out var error), error);
        Assert.Equal("Padded", preset!.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("ROX1-")]
    [InlineData("ROX1-not-actually-base64-@@@")]
    [InlineData("ROX1-YWJjZGVmZ2hpamts")]
    public void RefusesRubbishWithoutThrowing(string input)
    {
        Assert.False(PresetCode.TryDecode(input, out var preset, out var error));
        Assert.Null(preset);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void RefusesATruncatedCode()
    {
        var code = PresetCode.Encode("Cut", Sample());
        Assert.False(PresetCode.TryDecode(code[..(code.Length / 2)], out var preset, out var error));
        Assert.Null(preset);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void CarriesEverySettingThePluginHas()
    {
        var original = new Configuration();

        foreach (var property in ConfigurationCopy.Settings)
            property.SetValue(original, Changed(property.PropertyType, property.GetValue(original)));

        Assert.True(
            PresetCode.TryDecode(PresetCode.Encode("Every", original), out var preset, out var error), error);

        foreach (var property in ConfigurationCopy.Settings)
            Assert.Equal(property.GetValue(original), property.GetValue(preset!.Settings));
    }

    private static object Changed(Type type, object? value) => value switch
    {
        bool flag => !flag,
        float number => number + 1f,
        int number => number + 1,
        TimeSpan span => span + TimeSpan.FromSeconds(1),
        Vector4 colour => new Vector4(colour.X + 0.125f, 0.25f, 0.375f, 0.5f),
        Enum current => Enum.GetValues(type).Cast<Enum>().First(other => !other.Equals(current)),
        string text => text + "x",
        null when type == typeof(string) => "changed",
        _ => throw new InvalidOperationException(
            $"{type.Name} has no rule here, so this test cannot prove {type.Name} settings travel."),
    };
}
