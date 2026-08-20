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

        // Timing can only be judged by watching it, so the sample is replayed when a
        // slider is let go rather than restarted on every frame of the drag.
        var settled = false;

        ImGui.TextWrapped(
            "The line arrives, lands, then decodes. Motion and decode are separate " +
            "stages and each takes its own time.");
        ImGui.Separator();

        changed |= DrawSeconds("Fade in", () => this.config.FadeInDuration, v => this.config.FadeInDuration = v, 0.05f, 3f);
        settled |= ImGui.IsItemDeactivatedAfterEdit();
        Tooltip("How long the line takes to come up to full strength as it appears.");

        using (ImRaii.Disabled(this.config.Motion == MotionEffect.None))
        {
            changed |= DrawSeconds("Motion", () => this.config.MotionDuration, v => this.config.MotionDuration = v, 0.1f, 5f);
            settled |= ImGui.IsItemDeactivatedAfterEdit();
        }

        Tooltip(
            "How long the letters take to arrive. Runs alongside the fade in,\n" +
            "and does nothing when the motion on the Effects tab is None.");

        using (ImRaii.Disabled(!this.config.DecodeEffectEnabled))
        {
            changed |= DrawSeconds("Decode", () => this.config.RevealDuration, v => this.config.RevealDuration = v, 0.05f, 5f);
            settled |= ImGui.IsItemDeactivatedAfterEdit();
        }

        Tooltip(
            "How long the Eorzean takes to resolve, once the letters have landed.\n" +
            "Needs the decode effect on the Effects tab.");

        changed |= DrawSeconds("Hold", () => this.config.ShowDuration, v => this.config.ShowDuration = v, 0.5f, 15f);
        settled |= ImGui.IsItemDeactivatedAfterEdit();
        Tooltip("How long the finished line stays up before it starts to fade.");

        changed |= DrawSeconds("Fade out", () => this.config.FadeOutDuration, v => this.config.FadeOutDuration = v, 0.05f, 5f);
        settled |= ImGui.IsItemDeactivatedAfterEdit();
        Tooltip("How long the line takes to disappear once its time is up.");

        if (changed)
            this.config.Save();

        if (settled)
            this.actions.Preview(Sample);
    }
}
