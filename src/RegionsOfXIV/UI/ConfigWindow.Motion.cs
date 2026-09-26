using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // How a notification arrives and resolves on the left, what plays around it and how long it
    // all lasts on the right. Each pair of a feature and its duration is adjacent, and the
    // duration appears only while the feature is on.
    //
    // Four flags rather than one, because the groups preview differently and both behaviours have
    // to survive. See the tail of this method for what each one does.
    private void DrawMotionPage()
    {
        PageHeader.Draw(
            Loc.Get("motion.tab", "Motion"),
            Loc.Get(
                "motion.intro",
                "The line arrives, lands, then decodes. Motion and decode are separate " +
                "stages and each takes its own time."));

        // A live redraw is enough: the notification on screen can take the new value as it is.
        var changed = false;

        // Has to be played from the top. A different motion or a different arrival cannot be seen
        // in a notification that has already arrived.
        var restart = false;

        // Saved, but deliberately not previewed. A duration slider changes on every frame of a
        // drag and replaying per frame restarts the animation continuously, showing nothing.
        var timing = false;

        // The same sliders, once they are let go. This is when a timing change is worth seeing.
        var settled = false;

        // The Eorzean face only exists while the decode effect is on, so the atlas has to be
        // rebuilt before the replay rather than after it.
        var refont = false;

        this.columns.Begin(2);
        this.columns.Next();

        using (SettingsGroup.Begin(
                   Loc.Get("motion.group.arrival", "Arrival"),
                   Loc.Get("motion.choice.intro", "How the letters move as they arrive.")))
        {
            this.config.Motion = Choice(
                "##rox-motion",
                Loc.Get("motion.choice", "Motion"),
                Loc.Get(
                    "motion.choice.tooltip",
                    "None: the letters simply appear where they belong.\n" +
                    "Typewriter: one letter at a time, no fade.\n" +
                    "Rise: letters lift into place from below.\n" +
                    "Wave: letters ride a wave through the line as it appears.\n" +
                    "Burn: letters catch alight and cool into their colour.\n" +
                    "Drop: letters fall into place from above.\n" +
                    "Slide: letters slide in from the right.\n" +
                    "Assemble: letters converge from every direction.\n" +
                    "Flicker: letters stutter on like a neon sign.\n" +
                    "Zoom: letters grow into place.\n\n" +
                    "Runs alongside the Eorzean decode rather than instead of it."),
                this.config.Motion, MotionLabels, ref restart);

            using (var rows = Reveal("##rox-motion-rows", this.config.Motion != MotionEffect.None))
            {
                if (rows is not null)
                {
                    this.config.MotionDuration = Seconds(
                        "##rox-motion-duration",
                        Loc.Get("motion.duration", "Motion time"),
                        Loc.Get(
                            "motion.duration.tooltip",
                            "How long the letters take to arrive. Runs alongside the fade in, and\n" +
                            "does nothing when the motion above is None."),
                        this.config.MotionDuration, 0.1f, 5f, ref timing, ref settled);
                }
            }

            this.config.Departure = Choice(
                "##rox-departure",
                Loc.Get("motion.departure", "Departure"),
                Loc.Get(
                    "motion.departure.tooltip",
                    "How the line leaves once its time is up, played over the fade out.\n\n" +
                    "Fade: the plain fade.\n" +
                    "The arrival, backwards: whatever motion brought it in takes it out.\n" +
                    "Dissolve: the letters scatter.\n" +
                    "Fall: the letters sink away."),
                this.config.Departure, DepartureLabels, ref restart);
        }

        using (SettingsGroup.Begin(Loc.Get("motion.group.decode", "Decode")))
        {
            var wasDecoding = this.config.DecodeEffectEnabled;

            this.config.DecodeEffectEnabled = Toggle(
                "##rox-decode",
                Loc.Get("motion.decode", "Decode from Eorzean script"),
                Loc.Get(
                    "motion.decode.tooltip",
                    "Requires a bundled Eorzean font. Latin text only.\n\n" +
                    "Runs after the motion above rather than alongside it: the line\n" +
                    "arrives in Eorzean, lands, then resolves. Turned off, it arrives\n" +
                    "already readable. Presets leave this alone, so switching it off\n" +
                    "here switches it off for all of them."),
                this.config.DecodeEffectEnabled, ref restart);

            if (this.config.DecodeEffectEnabled != wasDecoding)
            {
                refont = true;
            }

            using (var rows = Reveal("##rox-decode-rows", this.config.DecodeEffectEnabled))
            {
                if (rows is not null)
                {
                    this.config.RevealDuration = Seconds(
                        "##rox-decode-duration",
                        Loc.Get("motion.decode.duration", "Decode time"),
                        Loc.Get(
                            "motion.decode.duration.tooltip",
                            "How long the Eorzean takes to resolve, once the letters have landed.\n" +
                            "Needs the decode effect above."),
                        this.config.RevealDuration, 0.05f, 5f, ref timing, ref settled);
                }
            }
        }

        this.columns.Next();

        using (SettingsGroup.Begin(
                   Loc.Get("motion.group.particles", "Particles"),
                   Loc.Get("motion.particles.intro", "What plays around it, for as long as it is on screen.")))
        {
            DrawParticles(ref changed, ref restart);
        }

        using (SettingsGroup.Begin(
                   Loc.Get("motion.group.timing", "Timing"),
                   Loc.Get("motion.timing.intro", "How long the whole notification lasts, whichever of the above it is using.")))
        {
            this.config.FadeInDuration = Seconds(
                "##rox-fadein",
                Loc.Get("motion.fadein", "Fade in"),
                Loc.Get("motion.fadein.tooltip", "How long the line takes to come up to full strength as it appears."),
                this.config.FadeInDuration, 0.05f, 3f, ref timing, ref settled);

            this.config.ShowDuration = Seconds(
                "##rox-hold",
                Loc.Get("motion.hold", "Hold"),
                Loc.Get("motion.hold.tooltip", "How long the finished line stays up before it starts to fade."),
                this.config.ShowDuration, 0.5f, 15f, ref timing, ref settled);

            this.config.FadeOutDuration = Seconds(
                "##rox-fadeout",
                Loc.Get("motion.fadeout", "Fade out"),
                Loc.Get("motion.fadeout.tooltip", "How long the line takes to disappear once its time is up."),
                this.config.FadeOutDuration, 0.05f, 5f, ref timing, ref settled);
        }

        this.columns.End();

        if (!changed && !restart && !timing && !settled)
        {
            return;
        }

        MarkUnsaved();

        if (refont)
        {
            this.actions.RebuildFonts();
        }

        // restart and settled both mean "play it from the top", for different reasons: a changed
        // effect cannot be seen in a notification that has already arrived, and a duration is only
        // worth showing once the slider has been let go. A drag on its own sets neither, which is
        // what keeps it from replaying sixty times a second.
        if (restart || settled)
        {
            this.stage.Replay();
            this.actions.Preview(Sample);
        }
        else if (changed)
        {
            this.actions.LivePreview(Sample);
        }
    }

    private void DrawParticles(ref bool changed, ref bool restart)
    {
        this.config.Particles = Choice(
            "##rox-particles", Loc.Get("motion.particles", "Particles"), null,
            this.config.Particles, ParticleLabels, ref restart);

        using var rows = Reveal("##rox-particle-rows", this.config.Particles != ParticleEffect.None);
        if (rows is null)
        {
            return;
        }

        this.config.ParticleDensity = Slider(
            "##rox-density", Loc.Get("motion.density", "Density"), null,
            this.config.ParticleDensity, 0.2f, 3f,
            "%.1f" + Loc.Unit("units.times", "x"), ref changed);

        this.config.ParticleColor = Colour(
            "##rox-particlecolour",
            Loc.Get("motion.particlecolour", "Particle colour"),
            Loc.Get(
                "motion.particlecolour.tooltip",
                "The default amber suits embers and sparkles. Hearts and petals\n" +
                "want moving towards pink; snow and rain want something pale."),
            this.config.ParticleColor, ref changed);

        this.config.ParticleSpread = Slider(
            "##rox-spread",
            Loc.Get("motion.spread", "Spread"),
            Loc.Get(
                "motion.spread.tooltip",
                "How far around the name the particles play. 1 hugs the text; higher\n" +
                "values fill the screen around it."),
            this.config.ParticleSpread, 0.5f, 3f,
            "%.1f" + Loc.Unit("units.times", "x"), ref changed);

        if (this.config.Particles == ParticleEffect.Embers && this.config.Motion != MotionEffect.Burn)
        {
            SettingsRow.Note(Loc.Get("motion.embers.note", "Embers go with the Burn motion, but they do not need it."));
        }
    }
}
