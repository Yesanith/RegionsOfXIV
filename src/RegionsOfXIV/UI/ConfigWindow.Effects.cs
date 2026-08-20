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

        this.config.Motion = Choice("Motion", this.config.Motion, Label, ref restart);
        Tooltip(
            "None — the letters simply appear where they belong.\n" +
            "Typewriter — one letter at a time, no fade.\n" +
            "Rise — letters lift into place from below.\n" +
            "Wave — letters ride a wave through the line as it appears.\n" +
            "Burn — letters catch alight and cool into their colour.\n\n" +
            "Runs alongside the Eorzean decode rather than instead of it.");

        ImGui.Separator();
        ImGui.TextWrapped("What plays around it, for as long as it is on screen.");

        this.config.Particles = Choice("Particles", this.config.Particles, Label, ref restart);

        if (this.config.Particles != ParticleEffect.None)
        {
            this.config.ParticleDensity = Slider(
                "Density", this.config.ParticleDensity, 0.2f, 3f, "%.1fx", ref changed);

            this.config.ParticleColor = ColorPicker(
                "Particle colour", this.config.ParticleColor, ref changed);
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

        MarkUnsaved();

        if (restart)
            this.actions.Preview(Sample);
        else
            this.actions.LivePreview(Sample);
    }
}
