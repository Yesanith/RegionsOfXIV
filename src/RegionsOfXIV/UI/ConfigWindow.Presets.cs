using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private string newPresetName = string.Empty;

    // Applying a preset rewrites the whole config, so afterwards the fonts have to be rebuilt and
    // the native UI suppression re-checked -- the preset may have turned either of those off.
    private void DrawPresetsTab()
    {
        using var tab = ImRaii.TabItem("Presets");
        if (!tab) return;

        var applied = false;

        ImGui.TextWrapped(
            "A preset covers every setting on General, Fonts, Effects, Notifications and Durations "
            + "— the face and size of each line, position, colours, motion and particles, which "
            + "tiers announce, the timings, and when to stay quiet.");
        ImGui.TextDisabled("Applying one replaces all of them, so anything you have changed is overwritten.");

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.TextWrapped("Built-in looks");
        ImGui.TextDisabled("The defaults, plus the look each one is named for.");
        applied |= DrawBuiltInPresets();

        ImGui.Separator();

        ImGui.TextWrapped("Your presets");
        ImGui.TextDisabled("Your settings, saved exactly as they were.");

        if (this.config.UserPresets.Count == 0)
            ImGui.TextDisabled("You have not saved any yet.");

        applied |= DrawSavedPresets();

        ImGui.Separator();
        DrawCustomFontPresetWarning();
        DrawSavePresetRow();

        ImGui.Separator();
        applied |= DrawShareCodeRow();

        ImGui.Spacing();
        DrawDiscordLink(
            "Join the Discord",
            "Trade preset codes, report a bug, or suggest a feature.");

        if (!applied)
            return;

        this.actions.RebuildFonts();

        if (!this.config.HideNativeAreaText)
            this.actions.RestoreNativeAreaText();

        if (!this.config.HideNativeLoadingTitle)
            this.actions.RestoreNativeLoadingTitle();

        this.config.Save();
        this.actions.Preview(Sample);
    }

    private bool DrawBuiltInPresets()
    {
        var applied = false;
        var row = new WrappingRow();

        foreach (var preset in Presets.All)
        {
            row.Place(preset.Name);

            if (ImGui.Button(preset.Name))
            {
                preset.ApplyTo(this.config);
                applied = true;
            }

            Tooltip($"{preset.Description}\nEverything else returns to its default.");
        }

        return applied;
    }

    private bool DrawSavedPresets()
    {
        if (this.config.UserPresets.Count == 0)
            return false;

        var applied = false;
        var row = new WrappingRow();

        UserPreset? remove = null;
        UserPreset? overwrite = null;
        UserPreset? share = null;

        foreach (var preset in this.config.UserPresets)
        {
            row.Place(preset.Name);

            if (ImGui.Button($"{preset.Name}##saved-{preset.Name}"))
            {
                preset.ApplyTo(this.config);
                applied = true;
            }

            Tooltip("Apply this preset.\nRight-click to share, overwrite or delete it.");

            if (ImGui.BeginPopupContextItem($"##saved-ctx-{preset.Name}"))
            {
                if (ImGui.Selectable("Copy share code"))
                    share = preset;

                if (ImGui.Selectable("Overwrite with current settings"))
                    overwrite = preset;

                if (ImGui.Selectable("Delete"))
                    remove = preset;

                ImGui.EndPopup();
            }
        }

        if (share is not null)
        {
            ImGui.SetClipboardText(PresetCode.Encode(share.Name, share.Settings));
            Report($"Copied a code for \"{share.Name}\".", failed: false);
        }

        if (overwrite is not null)
        {
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

        var existing = this.config.UserPresets.FirstOrDefault(
            p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        ImGui.SetNextItemWidth(180f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##new-preset-name", "Name it — to save, copy or import", ref this.newPresetName, 48);

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

        Tooltip("Saves every setting on General, Fonts, Effects, Notifications and Durations.");
    }

    private void DrawCustomFontPresetWarning()
    {
        if (!this.config.UsesCustomFont)
            return;

        Warn(
            CautionColor,
            "These settings use a font from this PC. A preset stores where that file sits, not the "
            + "font itself, so on anyone else's machine the path will not exist and those lines fall "
            + "back to Noto Sans CJK — everything else in the preset still applies. Put the line "
            + "back on a built-in face if you want a preset to look the same for whoever you hand it to.");

        ImGui.Spacing();
    }

    // A preset carries the path to a custom font, not the font, so a code that travels between
    // machines usually points at a file the recipient does not have.
    private string? MissingCustomFontNote()
    {
        foreach (var role in Enum.GetValues<FontRole>())
        {
            var font = this.config.FontFor(role);

            if (font.IsCustom && FontService.CustomFontProblem(font.Path) != null)
                return "It carries a font file from the sender's PC that is not on yours, so those "
                       + "lines are drawn with Noto Sans CJK. Choose your own on the Fonts tab.";
        }

        return null;
    }

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
