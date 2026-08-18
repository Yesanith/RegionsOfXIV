using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

// Presets, the motion, and the particles. Everything here is judged by watching
// it, which is why every control on this tab fires a sample.
internal sealed partial class ConfigWindow
{
    private void DrawEffectsTab()
    {
        using var tab = ImRaii.TabItem("Effects");
        if (!tab) return;

        var changed = false;

        // Changes that alter the animation itself rather than how it looks. A
        // sample already on screen is past its motion stage, so keeping it alive
        // would show nothing: these have to start a fresh one to be seen at all.
        var restart = false;

        // Presets first: they are the fastest way to a look worth keeping, and
        // everything below them is how you adjust one afterwards.
        ImGui.TextWrapped("Start from a look, then change anything you like.");
        restart |= DrawPresetButtons();
        ImGui.TextDisabled("Presets set motion, particles and colours. They leave the decode, font and position as you have them.");

        ImGui.Separator();
        ImGui.TextWrapped("How the letters move as they arrive.");

        restart |= Choice("Motion", () => this.config.Motion, v => this.config.Motion = v, Label);
        Tooltip(
            "None — the letters simply appear where they belong.\n" +
            "Typewriter — one letter at a time, no fade.\n" +
            "Rise — letters lift into place from below.\n" +
            "Wave — letters ride a wave through the line as it appears.\n" +
            "Burn — letters catch alight and cool into their colour.\n\n" +
            "Runs alongside the Eorzean decode rather than instead of it.");

        ImGui.Separator();
        ImGui.TextWrapped("What plays around it, for as long as it is on screen.");

        restart |= Choice("Particles", () => this.config.Particles, v => this.config.Particles = v, Label);

        // The rest of the section is dead weight with nothing to configure, so it
        // only appears once an effect is chosen.
        if (this.config.Particles != ParticleEffect.None)
        {
            changed |= Slider("Density",
                () => this.config.ParticleDensity, v => this.config.ParticleDensity = v, 0.2f, 3f, "%.1fx");

            changed |= ColorPicker("Particle colour",
                () => this.config.ParticleColor, v => this.config.ParticleColor = v);
            Tooltip(
                "The default amber suits embers and sparkles. Hearts and petals\n" +
                "want moving towards pink.");

            // Embers under a burn is the pairing these were built for, but the two
            // are independent settings and neither implies the other.
            if (this.config.Particles == ParticleEffect.Embers && this.config.Motion != MotionEffect.Burn)
                ImGui.TextWrapped("Embers go with the Burn motion, but they do not need it.");
        }

        ImGui.Separator();

        if (ImGui.Button("Preview"))
            this.actions.Preview(SampleHeader, SampleText);

        if (!changed && !restart)
            return;

        this.config.Save();

        // Every setting on this tab is something you have to watch to judge, so
        // each change earns a sample — same reasoning as the General tab. Which
        // kind of sample is the difference between seeing your choice and
        // wondering whether it did anything.
        if (restart)
            this.actions.Preview(SampleHeader, SampleText);
        else
            this.actions.LivePreview(SampleHeader, SampleText);
    }

    // Laid out as a wrapping row of buttons rather than a combo, because a preset
    // is an action and not a stored state — nothing here is "current", and a combo
    // would imply otherwise.
    private bool DrawPresetButtons()
    {
        var available = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var padding = ImGui.GetStyle().FramePadding.X * 2f;
        var spent = 0f;
        var applied = false;

        foreach (var preset in Presets.All)
        {
            var width = ImGui.CalcTextSize(preset.Name).X + padding;

            if (spent > 0f && spent + width < available)
                ImGui.SameLine();
            else
                spent = 0f;

            spent += width + spacing;

            if (ImGui.Button(preset.Name))
            {
                preset.Apply(this.config);
                applied = true;
            }

            Tooltip(preset.Description);
        }

        return applied;
    }
}
