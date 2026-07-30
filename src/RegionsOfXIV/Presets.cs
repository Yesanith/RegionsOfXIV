using System;
using System.Numerics;

namespace RegionsOfXIV;

// A named starting point: motion, particles and a palette that were chosen to go
// together.
//
// Applied and then forgotten. Nothing records which preset a config came from,
// and the settings a preset writes are the same ones the tabs edit, so there is
// no such thing as "leaving" one — you pick a look and then adjust it. The
// alternative, tracking an active preset and flipping it to "Custom" the moment
// anything is touched, buys a label at the cost of bookkeeping in every setter.
internal readonly record struct Preset(string Name, string Description, Action<Configuration> Apply);

internal static class Presets
{
    // What every preset deliberately does not touch:
    //
    //   DecodeEffectEnabled  — one decision, made once, in General. A preset that
    //                          silently switched the decode back on would undo a
    //                          choice the user made about the whole plugin.
    //   font, size, position — where the notification sits and how big it is are
    //                          about the player's screen, not about a look.
    //   durations            — pacing is a preference; several of these motions
    //                          read fine fast or slow.
    //
    // So a preset is exactly: motion, particles, palette, spacing, casing.
    public static readonly Preset[] All =
    [
        new(
            "Classic",
            "The plugin as it ships: a decode, no movement, no particles.",
            config =>
            {
                config.Motion = MotionEffect.None;
                config.Particles = ParticleEffect.None;
                config.LetterSpacing = 0f;
                config.UppercaseText = false;
                config.TextColor = new Vector4(0.875f, 0.761f, 0.584f, 1f);
                config.HeaderColor = new Vector4(0.698f, 0.627f, 0.569f, 1f);
                config.StrokeColor = new Vector4(0f, 0f, 0f, 0.8f);
                config.StrokeThickness = 1f;
                config.ParticleDensity = 1f;
                config.ParticleColor = new Vector4(1f, 0.72f, 0.35f, 1f);
            }),

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
