using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RegionsOfXIV.UI;

// Ambient particles around a notification, drawn from ImGui primitives rather than textures so
// they cost nothing to ship and work whatever font is in use. One field per notification, so
// particles fade out with the line that spawned them.
internal sealed class ParticleField
{
    private const int MaxParticles = 256;

    private const float MaxFrameSeconds = 0.1f;

    private readonly List<Particle> particles = [];
    private readonly Random random = new();

    private float pending;

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;

        public float Phase;
    }

    public bool IsEmpty => this.particles.Count == 0;

    // Spawning is budgeted in fractional particles carried between frames, so the rate stays the
    // same whatever the frame rate. The delta is capped as well: after a stutter or a loading
    // screen an uncapped step would teleport every particle off screen at once.
    public void Update(
        ParticleEffect effect, float density, float deltaSeconds, Vector2 center, Vector2 extent, bool spawning)
    {
        var dt = MathF.Min(deltaSeconds, MaxFrameSeconds);

        for (var i = this.particles.Count - 1; i >= 0; i--)
        {
            var particle = this.particles[i];

            // Swap-with-last removal: order does not matter here and it avoids shuffling the
            // tail of the list for every particle that expires.
            particle.Age += dt;
            if (particle.Age >= particle.Life)
            {
                this.particles[i] = this.particles[^1];
                this.particles.RemoveAt(this.particles.Count - 1);
                continue;
            }

            particle.Position += particle.Velocity * dt;

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

    private static float RatePerSecond(ParticleEffect effect) => effect switch
    {
        ParticleEffect.Hearts => 6f,
        ParticleEffect.Embers => 18f,
        ParticleEffect.Sparkles => 14f,
        ParticleEffect.Petals => 7f,
        _ => 0f,
    };

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
            ParticleEffect.Hearts => new Particle
            {
                Position = new Vector2(center.X + Range(-extent.X, extent.X), center.Y + Range(0f, extent.Y)),
                Velocity = new Vector2(Range(-8f, 8f), Range(-70f, -40f)),
                Life = Range(1.1f, 1.9f),
                Size = Range(9f, 15f),
                Phase = phase,
            },

            ParticleEffect.Embers => new Particle
            {
                Position = new Vector2(center.X + Range(-extent.X, extent.X), center.Y + Range(-extent.Y, extent.Y)),
                Velocity = new Vector2(Range(-14f, 14f), Range(-110f, -55f)),
                Life = Range(0.5f, 1.1f),
                Size = Range(4f, 9f),
                Phase = phase,
            },

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
