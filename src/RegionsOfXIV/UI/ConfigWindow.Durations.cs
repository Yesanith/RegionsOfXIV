using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private void DrawDurationsTab()
    {
        using var tab = ImRaii.TabItem("Durations");
        if (!tab) return;

        var changed = false;

        ImGui.TextWrapped(
            "The line arrives, lands, then decodes. Motion and decode are separate " +
            "stages and each takes its own time.");
        ImGui.Separator();

        changed |= DrawSeconds("Fade in", () => this.config.FadeInDuration, v => this.config.FadeInDuration = v, 0.05f, 3f);
        changed |= DrawSeconds("Motion", () => this.config.MotionDuration, v => this.config.MotionDuration = v, 0.1f, 5f);
        Tooltip(
            "How long the letters take to arrive. Runs alongside the fade in,\n" +
            "and does nothing when the motion on the Effects tab is None.");

        changed |= DrawSeconds("Decode", () => this.config.RevealDuration, v => this.config.RevealDuration = v, 0.05f, 5f);
        Tooltip("How long the Eorzean takes to resolve, once the letters have landed.");

        changed |= DrawSeconds("Hold", () => this.config.ShowDuration, v => this.config.ShowDuration = v, 0.5f, 15f);
        changed |= DrawSeconds("Fade out", () => this.config.FadeOutDuration, v => this.config.FadeOutDuration = v, 0.05f, 5f);

        if (changed)
            this.config.Save();
    }
}
