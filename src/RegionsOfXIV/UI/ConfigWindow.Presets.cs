using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

// Presets, both kinds, on a page of their own.
//
// They sat under the Effects tab while a preset meant only motion, particles and
// a palette. A saved preset now carries every setting on every tab, so belonging
// to one tab misrepresented it.
//
// The two kinds stay visibly apart because their scope differs and the difference
// matters before you click. A built-in look changes how the notification looks and
// nothing else — apply one and your size, position, durations and which tiers
// announce are exactly where you left them. A saved preset restores all of it.
internal sealed partial class ConfigWindow
{
    private string newPresetName = string.Empty;

    private void DrawPresetsTab()
    {
        using var tab = ImRaii.TabItem("Presets");
        if (!tab) return;

        var applied = false;

        ImGui.TextWrapped("Built-in looks");
        ImGui.TextDisabled("Motion, particles and colours only. Your size, position, durations and notification settings are left alone.");
        applied |= DrawBuiltInPresets();

        ImGui.Separator();

        ImGui.TextWrapped("Your presets");
        ImGui.TextDisabled("A complete snapshot: everything on General, Effects, Notifications and Durations.");

        if (this.config.UserPresets.Count == 0)
            ImGui.TextDisabled("You have not saved any yet.");

        applied |= DrawSavedPresets();

        ImGui.Separator();
        DrawSavePresetRow();

        if (!applied)
            return;

        // A saved preset can carry a different font and size, so the atlas has to
        // be rebuilt before anything is drawn with it — same ordering as the
        // General tab, and for the same reason.
        this.actions.RebuildFonts();

        // A saved preset also carries the native-suppression flags, and the
        // suppressor only ever hides — switching one off has to put the game's own
        // text back by hand, which is what the Notifications tab does when the
        // checkbox moves. Without this, applying a preset with suppression off
        // would leave the game's text hidden until a reload.
        if (!this.config.HideNativeAreaText)
            this.actions.RestoreNativeAreaText();

        if (!this.config.HideNativeLoadingTitle)
            this.actions.RestoreNativeLoadingTitle();

        this.config.Save();
        this.actions.Preview(SampleHeader, SampleText);
    }

    // Laid out as a wrapping row of buttons rather than a combo, because a preset
    // is an action and not a stored state — nothing here is "current", and a combo
    // would imply otherwise.
    private bool DrawBuiltInPresets()
    {
        var applied = false;
        var row = new WrappingRow();

        foreach (var preset in Presets.All)
        {
            row.Place(preset.Name);

            if (ImGui.Button(preset.Name))
            {
                preset.Apply(this.config);
                applied = true;
            }

            Tooltip(preset.Description);
        }

        return applied;
    }

    private bool DrawSavedPresets()
    {
        if (this.config.UserPresets.Count == 0)
            return false;

        var applied = false;
        var row = new WrappingRow();

        // Collected rather than acted on inside the loop: mutating a List while
        // enumerating it throws, and one of each per frame is plenty.
        UserPreset? remove = null;
        UserPreset? overwrite = null;

        foreach (var preset in this.config.UserPresets)
        {
            row.Place(preset.Name);

            // The id is kept off the label so two presets named alike cannot
            // collide, which nothing prevents.
            if (ImGui.Button($"{preset.Name}##saved-{preset.Name}"))
            {
                preset.ApplyTo(this.config);
                applied = true;
            }

            Tooltip("Apply this preset.\nRight-click to overwrite or delete it.");

            if (ImGui.BeginPopupContextItem($"##saved-ctx-{preset.Name}"))
            {
                if (ImGui.Selectable("Overwrite with current settings"))
                    overwrite = preset;

                if (ImGui.Selectable("Delete"))
                    remove = preset;

                ImGui.EndPopup();
            }
        }

        if (overwrite is not null)
        {
            // Replaced in place so the button keeps its position in the row.
            var at = this.config.UserPresets.IndexOf(overwrite);
            this.config.UserPresets[at] = UserPreset.Capture(overwrite.Name, this.config);
            this.config.Save();
        }

        if (remove is not null)
        {
            this.config.UserPresets.Remove(remove);
            this.config.Save();
        }

        return applied;
    }

    private void DrawSavePresetRow()
    {
        var name = this.newPresetName.Trim();

        // Matching an existing name replaces it rather than producing a second
        // entry with the same label, which would be indistinguishable on screen.
        var existing = this.config.UserPresets.FirstOrDefault(
            p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        ImGui.SetNextItemWidth(180f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##new-preset-name", "Name a preset to save it", ref this.newPresetName, 48);

        ImGui.SameLine();

        using (ImRaii.Disabled(name.Length == 0))
        {
            if (ImGui.Button(existing is null ? "Save" : "Replace"))
            {
                if (existing is not null)
                    this.config.UserPresets.Remove(existing);

                this.config.UserPresets.Add(UserPreset.Capture(name, this.config));
                this.config.Save();
                this.newPresetName = string.Empty;
            }
        }

        Tooltip("Saves every setting on General, Effects, Notifications and Durations.");
    }

    // Buttons of differing widths wrapped to the window, which ImGui has no layout
    // for. Measuring before placing is the only way to know whether the next one
    // fits on the current line.
    private struct WrappingRow()
    {
        private float spent = 0f;

        public void Place(string label)
        {
            var available = ImGui.GetContentRegionAvail().X;
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var width = ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f);

            if (this.spent > 0f && this.spent + width < available)
                ImGui.SameLine();
            else
                this.spent = 0f;

            this.spent += width + spacing;
        }
    }
}
