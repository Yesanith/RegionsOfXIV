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

        var settled = false;

        ImGui.TextWrapped(
            "The line arrives, lands, then decodes. Motion and decode are separate " +
            "stages and each takes its own time.");
        ImGui.Separator();

        this.config.FadeInDuration = DrawSeconds(
            "Fade in", this.config.FadeInDuration, 0.05f, 3f, ref changed);
        settled |= ImGui.IsItemDeactivatedAfterEdit();
        Tooltip("How long the line takes to come up to full strength as it appears.");

        using (ImRaii.Disabled(this.config.Motion == MotionEffect.None))
        {
            this.config.MotionDuration = DrawSeconds(
            "Motion", this.config.MotionDuration, 0.1f, 5f, ref changed);
            settled |= ImGui.IsItemDeactivatedAfterEdit();
        }

        Tooltip(
            "How long the letters take to arrive. Runs alongside the fade in,\n" +
            "and does nothing when the motion on the Effects tab is None.");

        using (ImRaii.Disabled(!this.config.DecodeEffectEnabled))
        {
            this.config.RevealDuration = DrawSeconds(
            "Decode", this.config.RevealDuration, 0.05f, 5f, ref changed);
            settled |= ImGui.IsItemDeactivatedAfterEdit();
        }

        Tooltip(
            "How long the Eorzean takes to resolve, once the letters have landed.\n" +
            "Needs the decode effect on the Effects tab.");

        this.config.ShowDuration = DrawSeconds(
            "Hold", this.config.ShowDuration, 0.5f, 15f, ref changed);
        settled |= ImGui.IsItemDeactivatedAfterEdit();
        Tooltip("How long the finished line stays up before it starts to fade.");

        this.config.FadeOutDuration = DrawSeconds(
            "Fade out", this.config.FadeOutDuration, 0.05f, 5f, ref changed);
        settled |= ImGui.IsItemDeactivatedAfterEdit();
        Tooltip("How long the line takes to disappear once its time is up.");

        if (changed)
            MarkUnsaved();

        if (settled)
            this.actions.Preview(Sample);
    }
}
