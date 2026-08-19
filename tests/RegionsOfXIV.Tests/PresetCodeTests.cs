using System.Numerics;
using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

// The codec is the one piece here whose failures are silent: a truncated code
// still looks like a code, and a value that fails to round-trip comes back as a
// plausible default rather than as an error. So it gets exercised end to end.
public class PresetCodeTests
{
    private static Configuration Sample() => new()
    {
        Motion = MotionEffect.Burn,
        Particles = ParticleEffect.Embers,
        DisplayFont = DisplayFontChoice.Jupiter,
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

    // The saved-preset list is excluded from a copy, so it must be absent from a
    // code too — otherwise sharing one preset would drag every other one along.
    [Fact]
    public void DoesNotCarryTheSavedPresetList()
    {
        var config = Sample();
        config.UserPresets.Add(UserPreset.Capture("Someone else's", Sample()));

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Solo", config), out var preset, out _));
        Assert.Empty(preset!.Settings.UserPresets);
    }

    // A code omits anything at its default, so an unmentioned setting has to arrive
    // at that default rather than at zero.
    [Fact]
    public void LeavesUnmentionedSettingsAtTheirDefaults()
    {
        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Bare", new Configuration()), out var preset, out _));

        var defaults = new Configuration();
        foreach (var property in ConfigurationCopy.Settings)
            Assert.Equal(property.GetValue(defaults), property.GetValue(preset!.Settings));
    }

    // The forward-compatibility case, and the reason this plugin can keep adding
    // motions and particles: a code from a newer build names an effect this one
    // does not have. It has to land on the default rather than be stored as a
    // number that no switch arm matches.
    [Theory]
    [InlineData(99)]
    [InlineData(5)]
    [InlineData(-1)]
    public void IgnoresAnEffectThisBuildDoesNotHave(int unknown)
    {
        var future = Sample();
        future.Motion = (MotionEffect)unknown;
        future.Particles = (ParticleEffect)unknown;
        future.DisplayFont = (DisplayFontChoice)unknown;

        Assert.True(PresetCode.TryDecode(PresetCode.Encode("Future", future), out var preset, out var error), error);

        var defaults = new Configuration();
        Assert.Equal(defaults.Motion, preset!.Settings.Motion);
        Assert.Equal(defaults.Particles, preset.Settings.Particles);
        Assert.Equal(defaults.DisplayFont, preset.Settings.DisplayFont);
    }

    // ...while everything the older build *does* understand still arrives. A single
    // unknown effect must not cost the rest of the preset.
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

    // Appending a member must not disturb the ones already out in the wild: a code
    // is numbers, so an existing effect has to keep the number it was written with.
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

        Assert.Equal(0, (int)DisplayFontChoice.TrumpGothic);
        Assert.Equal(1, (int)DisplayFontChoice.Jupiter);
        Assert.Equal(2, (int)DisplayFontChoice.Axis);
        Assert.Equal(3, (int)DisplayFontChoice.NotoSansCjk);
    }

    // One unbroken token: no base64 characters that a chat client would break a
    // line on or that a reader would mistake for punctuation.
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

    // Only the differences go in, so a preset that changes a handful of settings
    // has to stay short enough to paste into a chat message.
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

    // Truncation is the realistic corruption: someone selects most of a long code
    // out of a chat window and misses the tail.
    [Fact]
    public void RefusesATruncatedCode()
    {
        var code = PresetCode.Encode("Cut", Sample());
        Assert.False(PresetCode.TryDecode(code[..(code.Length / 2)], out var preset, out var error));
        Assert.Null(preset);
        Assert.NotEmpty(error);
    }
}
