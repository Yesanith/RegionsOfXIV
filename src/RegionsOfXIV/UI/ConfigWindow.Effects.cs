using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private void DrawEffectsTab()
    {
        using var tab = ImRaii.TabItem("Effects");
        if (!tab) return;

        var changed = false;

        var restart = false;

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

        if (this.config.Particles != ParticleEffect.None)
        {
            changed |= Slider("Density",
                () => this.config.ParticleDensity, v => this.config.ParticleDensity = v, 0.2f, 3f, "%.1fx");

            changed |= ColorPicker("Particle colour",
                () => this.config.ParticleColor, v => this.config.ParticleColor = v);
            Tooltip(
                "The default amber suits embers and sparkles. Hearts and petals\n" +
                "want moving towards pink.");

            if (this.config.Particles == ParticleEffect.Embers && this.config.Motion != MotionEffect.Burn)
                ImGui.TextWrapped("Embers go with the Burn motion, but they do not need it.");
        }

        ImGui.Separator();

        if (ImGui.Button("Preview"))
            this.actions.Preview(Sample);

        if (!changed && !restart)
            return;

        this.config.Save();

        if (restart)
            this.actions.Preview(Sample);
        else
            this.actions.LivePreview(Sample);
    }

}
