using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RegionsOfXIV.UI;

// The ambient particles drifting around one notification, and the whole of their
// simulation: spawn, move, age out, draw.
//
// One field per notification, so particles fade away with the text that owned
// them rather than outliving it — and so two notifications overlapping during a
// handover keep their own.
//
// Everything is drawn from ImDrawList primitives. That is not a shortcut: a "♥"
// glyph would render as a blank box under the Latin-only game faces, and a sprite
// would be the first art asset the plugin has ever had to ship, install and load.
// Circles and quads cost nothing and look the same under every font.
internal sealed class ParticleField
{
    // A ceiling rather than a budget: at the default density the effects settle
    // around a dozen live particles, and this only comes into play if someone
    // pushes density to maximum and then stands in a doorway triggering
    // announcements. Cheap insurance against an unbounded list.
    private const int MaxParticles = 256;

    // Long frames — a zone load, a stutter — must not teleport a particle across
    // the screen. Anything past this is treated as this much, so a hitch slows
    // the effect rather than breaking it.
    private const float MaxFrameSeconds = 0.1f;

    private readonly List<Particle> particles = [];
    private readonly Random random = new();

    // Fractional particles owed from previous frames. Kept between frames so a
    // spawn rate below one per frame still spawns at the right *average* rate
    // rather than rounding to zero every time.
    private float pending;

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;

        // Per-particle randomness, so a shared sine does not move every particle
        // in lockstep.
        public float Phase;
    }

    public bool IsEmpty => this.particles.Count == 0;

    // center/extent describe the text the particles are playing around: the
    // middle of the line, and half its width and height.
    public void Update(
        ParticleEffect effect, float density, float deltaSeconds, Vector2 center, Vector2 extent, bool spawning)
    {
        var dt = MathF.Min(deltaSeconds, MaxFrameSeconds);

        for (var i = this.particles.Count - 1; i >= 0; i--)
        {
            var particle = this.particles[i];

            particle.Age += dt;
            if (particle.Age >= particle.Life)
            {
                // Swapped with the last entry rather than removed in place, which
                // would shift everything after it. Order carries no meaning here —
                // they are a cloud, not a list — and the walk is backwards, so the
                // entry moved into this slot has already been dealt with.
                this.particles[i] = this.particles[^1];
                this.particles.RemoveAt(this.particles.Count - 1);
                continue;
            }

            particle.Position += particle.Velocity * dt;

            // Sideways drift, applied as a displacement rather than folded into
            // the velocity so it stays a sway and does not accumulate into a
            // sideways drift across the particle's whole life.
            if (effect is ParticleEffect.Hearts or ParticleEffect.Petals)
                particle.Position.X += MathF.Sin((particle.Age * 2.2f) + particle.Phase) * 14f * dt;

            this.particles[i] = particle;
        }

        if (!spawning || effect == ParticleEffect.None)
            return;

        this.pending += dt * RatePerSecond(effect) * density;

        while (this.pending >= 1f && this.particles.Count < MaxParticles)
        {
            this.pending -= 1f;
            this.particles.Add(Spawn(effect, center, extent));
        }

        // At the ceiling the loop above stops consuming, so drop what is owed
        // rather than banking a backlog that would burst the moment a particle
        // ages out.
        if (this.particles.Count >= MaxParticles)
            this.pending = 0f;
    }

    public void Draw(ImDrawListPtr drawList, ParticleEffect effect, Vector4 color, float opacity)
    {
        if (effect == ParticleEffect.None)
            return;

        foreach (var particle in this.particles)
        {
            var life = particle.Age / particle.Life;
            var alpha = Fade(effect, life, particle.Age, particle.Phase) * opacity;

            if (alpha <= 0f)
                continue;

            // Embers cool as they rise: bright yellow-white at the bottom,
            // settling into the configured colour as they climb and go out.
            var tint = effect == ParticleEffect.Embers
                ? Vector4.Lerp(new Vector4(1f, 0.95f, 0.7f, 1f), color, MathF.Min(life * 2f, 1f))
                : color;

            var packed = ImGui.ColorConvertFloat4ToU32(tint with { W = tint.W * alpha });

            switch (effect)
            {
                case ParticleEffect.Hearts:
                    DrawHeart(drawList, particle.Position, particle.Size, packed);
                    break;

                case ParticleEffect.Embers:
                    drawList.AddCircleFilled(particle.Position, particle.Size * 0.35f, packed);
                    break;

                case ParticleEffect.Sparkles:
                    DrawSparkle(drawList, particle.Position, particle.Size, packed);
                    break;

                case ParticleEffect.Petals:
                    DrawPetal(drawList, particle.Position, particle.Size, particle.Age + particle.Phase, packed);
                    break;
            }
        }
    }

    // Per-second spawn rates at density 1. Sparkles are the densest because they
    // are the smallest and the shortest-lived; hearts the sparsest because they
    // are the most conspicuous and a crowd of them reads as spam rather than as
    // charm.
    private static float RatePerSecond(ParticleEffect effect) => effect switch
    {
        ParticleEffect.Hearts => 6f,
        ParticleEffect.Embers => 18f,
        ParticleEffect.Sparkles => 14f,
        ParticleEffect.Petals => 7f,
        _ => 0f,
    };

    // How a particle's alpha behaves over its life. Everything fades out at the
    // end; sparkles additionally twinkle, and embers fade fast so the tail of the
    // stream thins out rather than ending in a line of dots.
    private static float Fade(ParticleEffect effect, float life, float age, float phase) => effect switch
    {
        ParticleEffect.Sparkles => (1f - life) * (0.45f + (0.55f * MathF.Sin((age * 9f) + phase))),
        ParticleEffect.Embers => (1f - life) * (1f - life),
        _ => MathF.Min((1f - life) * 2f, 1f),
    };

    private Particle Spawn(ParticleEffect effect, Vector2 center, Vector2 extent)
    {
        var phase = Range(0f, MathF.Tau);

        return effect switch
        {
            // From along the baseline, rising and spreading.
            ParticleEffect.Hearts => new Particle
            {
                Position = new Vector2(center.X + Range(-extent.X, extent.X), center.Y + Range(0f, extent.Y)),
                Velocity = new Vector2(Range(-8f, 8f), Range(-70f, -40f)),
                Life = Range(1.1f, 1.9f),
                Size = Range(9f, 15f),
                Phase = phase,
            },

            // From the text itself, fast and short-lived.
            ParticleEffect.Embers => new Particle
            {
                Position = new Vector2(center.X + Range(-extent.X, extent.X), center.Y + Range(-extent.Y, extent.Y)),
                Velocity = new Vector2(Range(-14f, 14f), Range(-110f, -55f)),
                Life = Range(0.5f, 1.1f),
                Size = Range(4f, 9f),
                Phase = phase,
            },

            // Hanging in the air around the line rather than traveling through it.
            ParticleEffect.Sparkles => new Particle
            {
                Position = new Vector2(
                    center.X + Range(-extent.X * 1.1f, extent.X * 1.1f),
                    center.Y + Range(-extent.Y * 1.3f, extent.Y * 1.3f)),
                Velocity = new Vector2(Range(-6f, 6f), Range(-14f, -2f)),
                Life = Range(0.6f, 1.3f),
                Size = Range(5f, 11f),
                Phase = phase,
            },

            // From above, falling past the text.
            _ => new Particle
            {
                Position = new Vector2(
                    center.X + Range(-extent.X * 1.2f, extent.X * 1.2f),
                    center.Y - extent.Y - Range(0f, 30f)),
                Velocity = new Vector2(Range(-10f, 10f), Range(25f, 55f)),
                Life = Range(1.4f, 2.4f),
                Size = Range(7f, 12f),
                Phase = phase,
            },
        };
    }

    private float Range(float min, float max) => min + ((float)this.random.NextDouble() * (max - min));

    // Two lobes and a point. Drawn from three primitives rather than a polygon
    // path because that is the whole shape, and a heart described by five numbers
    // is easier to nudge than one described by a vertex list.
    private static void DrawHeart(ImDrawListPtr drawList, Vector2 at, float size, uint color)
    {
        var r = size * 0.5f;

        drawList.AddCircleFilled(new Vector2(at.X - (r * 0.48f), at.Y), r * 0.6f, color);
        drawList.AddCircleFilled(new Vector2(at.X + (r * 0.48f), at.Y), r * 0.6f, color);

        drawList.AddTriangleFilled(
            new Vector2(at.X - r, at.Y + (r * 0.12f)),
            new Vector2(at.X + r, at.Y + (r * 0.12f)),
            new Vector2(at.X, at.Y + (r * 1.45f)),
            color);
    }

    // A four-point star, as two crossed diamonds. Both are wound the same way
    // round, which convex fill requires.
    private static void DrawSparkle(ImDrawListPtr drawList, Vector2 at, float size, uint color)
    {
        var r = size * 0.5f;
        var w = r * 0.28f;

        drawList.AddQuadFilled(
            new Vector2(at.X, at.Y - r),
            new Vector2(at.X + w, at.Y),
            new Vector2(at.X, at.Y + r),
            new Vector2(at.X - w, at.Y),
            color);

        drawList.AddQuadFilled(
            new Vector2(at.X - r, at.Y),
            new Vector2(at.X, at.Y - w),
            new Vector2(at.X + r, at.Y),
            new Vector2(at.X, at.Y + w),
            color);
    }

    // A leaf-ish quad, turning as it falls. The rotation is the particle's age,
    // so it tumbles at a constant rate from wherever its phase started it.
    private static void DrawPetal(ImDrawListPtr drawList, Vector2 at, float size, float angle, uint color)
    {
        var r = size * 0.5f;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);

        Vector2 Rotated(float x, float y) =>
            new(at.X + ((x * cos) - (y * sin)), at.Y + ((x * sin) + (y * cos)));

        drawList.AddQuadFilled(
            Rotated(0f, -r),
            Rotated(r * 0.55f, 0f),
            Rotated(0f, r),
            Rotated(-r * 0.55f, 0f),
            color);
    }
}
