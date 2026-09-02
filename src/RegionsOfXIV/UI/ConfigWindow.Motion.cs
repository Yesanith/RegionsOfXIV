using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // How a notification arrives, resolves and leaves, which was three tabs' worth of controls
    // with two features split across them: the motion type and its duration, and the decode
    // toggle and its duration. Each pair is now adjacent, which is why three tooltips on this tab
    // no longer have to name another tab to explain themselves.
    //
    // Four flags rather than the two either half used, because the two halves preview differently
    // and both behaviours have to survive. See the tail of this method for what each one does.
    private void DrawMotionTab()
    {
        using var tab = ImRaii.TabItem(Loc.Label("motion.tab", "Motion"));
        if (!tab) return;

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

        UiText.Wrapped(Loc.Get(
            "motion.intro",
            "The line arrives, lands, then decodes. Motion and decode are separate " +
            "stages and each takes its own time."));
        ImGui.Separator();

        UiText.Wrapped(Loc.Get("motion.choice.intro", "How the letters move as they arrive."));

        this.config.Motion = Choice(
            Loc.Label("motion.choice", "Motion"), this.config.Motion, Label, ref restart);
        UiText.Tooltip(Loc.Get(
            "motion.choice.tooltip",
            "None: the letters simply appear where they belong.\n" +
            "Typewriter: one letter at a time, no fade.\n" +
            "Rise: letters lift into place from below.\n" +
            "Wave: letters ride a wave through the line as it appears.\n" +
            "Burn: letters catch alight and cool into their colour.\n\n" +
            "Runs alongside the Eorzean decode rather than instead of it."));

        using (ImRaii.Disabled(this.config.Motion == MotionEffect.None))
        {
            this.config.MotionDuration = DrawSeconds(
                Loc.Label("motion.duration", "Motion time"),
                this.config.MotionDuration, 0.1f, 5f, ref timing, ref settled);
        }

        UiText.Tooltip(Loc.Get(
            "motion.duration.tooltip",
            "How long the letters take to arrive. Runs alongside the fade in, and\n" +
            "does nothing when the motion above is None."));

        ImGui.Separator();

        var wasDecoding = this.config.DecodeEffectEnabled;

        this.config.DecodeEffectEnabled = Checkbox(
            Loc.Label("motion.decode", "Decode from Eorzean script"),
            this.config.DecodeEffectEnabled, ref restart);

        // Asked explicitly rather than reading restart, which happens to mean the same thing today
        // only because this is the one control on the tab that sets it without also being a
        // Choice.
        if (this.config.DecodeEffectEnabled != wasDecoding)
            refont = true;

        UiText.Tooltip(Loc.Get(
            "motion.decode.tooltip",
            "Requires a bundled Eorzean font. Latin text only.\n\n" +
            "Runs after the motion above rather than alongside it: the line\n" +
            "arrives in Eorzean, lands, then resolves. Turned off, it arrives\n" +
            "already readable. Presets leave this alone, so switching it off\n" +
            "here switches it off for all of them."));

        using (ImRaii.Disabled(!this.config.DecodeEffectEnabled))
        {
            this.config.RevealDuration = DrawSeconds(
                Loc.Label("motion.decode.duration", "Decode time"),
                this.config.RevealDuration, 0.05f, 5f, ref timing, ref settled);
        }

        UiText.Tooltip(Loc.Get(
            "motion.decode.duration.tooltip",
            "How long the Eorzean takes to resolve, once the letters have landed.\n" +
            "Needs the decode effect above."));

        ImGui.Separator();
        UiText.Wrapped(Loc.Get(
            "motion.particles.intro", "What plays around it, for as long as it is on screen."));

        this.config.Particles = Choice(
            Loc.Label("motion.particles", "Particles"), this.config.Particles, Label, ref restart);

        if (this.config.Particles != ParticleEffect.None)
        {
            this.config.ParticleDensity = Slider(
                Loc.Label("motion.density", "Density"),
                this.config.ParticleDensity, 0.2f, 3f,
                "%.1f" + Loc.Unit("units.times", "x"), ref changed);

            this.config.ParticleColor = ColorPicker(
                Loc.Label("motion.particlecolour", "Particle colour"),
                this.config.ParticleColor, ref changed);
            UiText.Tooltip(Loc.Get(
                "motion.particlecolour.tooltip",
                "The default amber suits embers and sparkles. Hearts and petals\n" +
                "want moving towards pink."));

            if (this.config.Particles == ParticleEffect.Embers && this.config.Motion != MotionEffect.Burn)
                UiText.Wrapped(Loc.Get(
                    "motion.embers.note",
                    "Embers go with the Burn motion, but they do not need it."));
        }

        ImGui.Separator();
        UiText.Wrapped(Loc.Get(
            "motion.timing.intro",
            "How long the whole notification lasts, whichever of the above it is using."));

        this.config.FadeInDuration = DrawSeconds(
            Loc.Label("motion.fadein", "Fade in"),
            this.config.FadeInDuration, 0.05f, 3f, ref timing, ref settled);
        UiText.Tooltip(Loc.Get(
            "motion.fadein.tooltip",
            "How long the line takes to come up to full strength as it appears."));

        this.config.ShowDuration = DrawSeconds(
            Loc.Label("motion.hold", "Hold"),
            this.config.ShowDuration, 0.5f, 15f, ref timing, ref settled);
        UiText.Tooltip(Loc.Get(
            "motion.hold.tooltip",
            "How long the finished line stays up before it starts to fade."));

        this.config.FadeOutDuration = DrawSeconds(
            Loc.Label("motion.fadeout", "Fade out"),
            this.config.FadeOutDuration, 0.05f, 5f, ref timing, ref settled);
        UiText.Tooltip(Loc.Get(
            "motion.fadeout.tooltip",
            "How long the line takes to disappear once its time is up."));

        ImGui.Separator();

        if (ImGui.Button(Loc.Label("motion.preview", "Preview")))
            this.actions.Preview(Sample);

        if (!changed && !restart && !timing && !settled)
            return;

        MarkUnsaved();

        if (refont)
            this.actions.RebuildFonts();

        // restart and settled both mean "play it from the top", for different reasons: a changed
        // effect cannot be seen in a notification that has already arrived, and a duration is only
        // worth showing once the slider has been let go. A drag on its own sets neither, which is
        // what keeps it from replaying sixty times a second.
        if (restart || settled)
            this.actions.Preview(Sample);
        else if (changed)
            this.actions.LivePreview(Sample);
    }
}
