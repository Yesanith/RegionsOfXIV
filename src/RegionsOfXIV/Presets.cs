using System;
using System.Numerics;

namespace RegionsOfXIV;

// A named look, as a complete configuration.
//
// Look holds only the settings that make this preset what it is; ApplyTo puts
// everything else back to its default first, so a built-in is a whole
// configuration and not a diff against whatever happened to be there. Two players
// who click "Inferno" see the same notification, whatever either had changed
// beforehand — which is what a named preset has to mean to be worth naming.
//
// Applied and then forgotten. Nothing records which preset a config came from,
// and the settings a preset writes are the same ones the tabs edit, so there is
// no such thing as "leaving" one — you pick a look and then adjust it. The
// alternative, tracking an active preset and flipping it to "Custom" the moment
// anything is touched, buys a label at the cost of bookkeeping in every setter.
internal readonly record struct Preset(string Name, string Description, Action<Configuration> Look)
{
    public void ApplyTo(Configuration config)
    {
        ConfigurationCopy.ResetToDefaults(config);
        this.Look(config);
    }
}

internal static class Presets
{
    // Each Look below writes only what distinguishes it. Size, position, font,
    // durations, which tiers announce, the decode, when to stay quiet — all of it
    // arrives from the defaults by way of ResetToDefaults, so nothing here has to
    // list a setting merely to be complete, and a setting added to Configuration
    // later is covered the moment it has a default.
    //
    // Note this reaches the decode switch too. A preset used to leave
    // DecodeEffectEnabled alone on the grounds that it was a decision about the
    // whole plugin; now that applying one is understood as a full reset, carving
    // out a single setting would be the surprising behaviour rather than the safe
    // one.
    public static readonly Preset[] All =
    [
        // Nothing to write: this *is* the defaults, so the reset has already done
        // the whole job. Kept as a preset because "put it all back" is exactly what
        // someone reaches for after experimenting.
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

        // The plugin's own ancestor. Wide tracking and uppercase is most of what
        // makes the Guild Wars 2 original recognisable, and it wants no motion —
        // the decode alone carries it.
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
