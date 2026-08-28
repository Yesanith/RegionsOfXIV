using System.Numerics;
using System;

namespace RegionsOfXIV;

internal readonly record struct Preset(string Name, string Description, Action<Configuration> Look)
{
    public void ApplyTo(Configuration config)
    {
        ConfigurationCopy.ResetToDefaults(config);
        this.Look(config);
    }
}

// Each preset resets everything to defaults first, then applies only what it is named for, so
// applying one is not affected by whatever was set before it.
internal static class Presets
{
    public static readonly Preset[] All =
    [
        new(
            "Classic",
            "The plugin as it ships: a decode, no movement, no particles.",
            _ => { }),

        new(
            "Inferno",
            "Letters catch alight and cool into place, embers rising off them.",
            config =>
            {
                config.Motion = MotionEffect.Burn;
                config.Particles = ParticleEffect.Embers;
                config.ParticleDensity = 1.3f;
                config.ParticleColor = new Vector4(1f, 0.55f, 0.18f, 1f);
                config.TextColor = new Vector4(1f, 0.86f, 0.62f, 1f);
                config.HeaderColor = new Vector4(0.96f, 0.66f, 0.34f, 1f);
                config.StrokeColor = new Vector4(0.16f, 0.04f, 0f, 0.85f);
                config.StrokeThickness = 1.4f;
                config.LetterSpacing = 4f;
                config.UppercaseText = false;
            }),

        new(
            "Sweetheart",
            "Letters lift into place with hearts drifting up around them.",
            config =>
            {
                config.Motion = MotionEffect.Rise;
                config.Particles = ParticleEffect.Hearts;
                config.ParticleDensity = 1f;
                config.ParticleColor = new Vector4(1f, 0.45f, 0.62f, 1f);
                config.TextColor = new Vector4(1f, 0.9f, 0.94f, 1f);
                config.HeaderColor = new Vector4(0.98f, 0.68f, 0.78f, 1f);
                config.StrokeColor = new Vector4(0.24f, 0.05f, 0.12f, 0.8f);
                config.StrokeThickness = 1f;
                config.LetterSpacing = 2f;
                config.UppercaseText = false;
            }),

        new(
            "Starlight",
            "The line rides a wave in, sparkles hanging in the air around it.",
            config =>
            {
                config.Motion = MotionEffect.Wave;
                config.Particles = ParticleEffect.Sparkles;
                config.ParticleDensity = 1.1f;
                config.ParticleColor = new Vector4(0.85f, 0.93f, 1f, 1f);
                config.TextColor = new Vector4(0.93f, 0.96f, 1f, 1f);
                config.HeaderColor = new Vector4(0.68f, 0.79f, 0.95f, 1f);
                config.StrokeColor = new Vector4(0.02f, 0.05f, 0.15f, 0.85f);
                config.StrokeThickness = 1.2f;
                config.LetterSpacing = 8f;
                config.UppercaseText = false;
            }),

        new(
            "Sakura",
            "A gentle rise under falling petals.",
            config =>
            {
                config.Motion = MotionEffect.Rise;
                config.Particles = ParticleEffect.Petals;
                config.ParticleDensity = 0.9f;
                config.ParticleColor = new Vector4(1f, 0.72f, 0.82f, 1f);
                config.TextColor = new Vector4(1f, 0.95f, 0.96f, 1f);
                config.HeaderColor = new Vector4(0.93f, 0.74f, 0.8f, 1f);
                config.StrokeColor = new Vector4(0.18f, 0.08f, 0.12f, 0.75f);
                config.StrokeThickness = 1f;
                config.LetterSpacing = 6f;
                config.UppercaseText = false;
            }),

        new(
            "Dispatch",
            "Typed out one letter at a time, in tight uppercase. No particles.",
            config =>
            {
                config.Motion = MotionEffect.Typewriter;
                config.Particles = ParticleEffect.None;
                config.TextColor = new Vector4(0.93f, 0.93f, 0.9f, 1f);
                config.HeaderColor = new Vector4(0.72f, 0.72f, 0.68f, 1f);
                config.StrokeColor = new Vector4(0f, 0f, 0f, 0.9f);
                config.StrokeThickness = 1f;
                config.LetterSpacing = 0f;
                config.UppercaseText = true;
            }),

        new(
            "Tyria",
            "Wide uppercase in gold, still and unhurried — a nod to the original.",
            config =>
            {
                config.Motion = MotionEffect.None;
                config.Particles = ParticleEffect.None;
                config.TextColor = new Vector4(0.93f, 0.85f, 0.66f, 1f);
                config.HeaderColor = new Vector4(0.76f, 0.68f, 0.5f, 1f);
                config.StrokeColor = new Vector4(0f, 0f, 0f, 0.85f);
                config.StrokeThickness = 1.2f;
                config.LetterSpacing = 18f;
                config.UppercaseText = true;
            }),
    ];
}

[Serializable]
// A saved preset is a whole Configuration, copied by value both going in and coming out, so
// editing your live settings never reaches back into a preset you saved earlier.
public class UserPreset
{
    public string Name { get; set; } = string.Empty;

    public Configuration Settings { get; set; } = new();

    public static UserPreset Capture(string name, Configuration config)
    {
        var preset = new UserPreset { Name = name };
        ConfigurationCopy.Apply(config, preset.Settings);
        return preset;
    }

    public void ApplyTo(Configuration config) => ConfigurationCopy.Apply(this.Settings, config);
}
