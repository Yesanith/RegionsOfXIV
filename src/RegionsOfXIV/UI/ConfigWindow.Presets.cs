using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private string newPresetName = string.Empty;

    private string shareStatus = string.Empty;
    private bool shareFailed;
    private DateTime shareStatusAt;

    private static readonly TimeSpan ShareStatusLinger = TimeSpan.FromSeconds(8);
    private static readonly Vector4 ShareGood = new(0.45f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 ShareBad = new(1f, 0.42f, 0.42f, 1f);

    private void DrawPresetsTab()
    {
        using var tab = ImRaii.TabItem("Presets");
        if (!tab) return;

        var applied = false;

        ImGui.TextWrapped(
            "A preset covers every setting on General, Effects, Notifications and Durations — "
            + "font and size, position, colours, motion and particles, which tiers announce, "
            + "the timings, and when to stay quiet.");
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
        this.actions.Preview(SampleHeader, SampleText);
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

        Tooltip("Saves every setting on General, Effects, Notifications and Durations.");
    }

    private bool DrawShareCodeRow()
    {
        ImGui.TextWrapped("Share codes");
        ImGui.TextDisabled("A code is the whole preset as one line of text. Paste one into chat to hand it to someone.");

        var named = this.newPresetName.Trim().Length > 0;

        using (ImRaii.Disabled(!named))
        {
            if (ImGui.Button("Copy current settings"))
            {
                ImGui.SetClipboardText(PresetCode.Encode(this.newPresetName.Trim(), this.config));
                Report("Copied. Paste it wherever you like.", failed: false);
            }
        }

        Tooltip(named
            ? "Puts a code for everything as it stands right now on the clipboard,\n" +
              $"under the name \"{this.newPresetName.Trim()}\"."
            : "Type a name in the box above first — it travels with the code, and it\n" +
              "is all the person you send it to will have to go on.\n\n" +
              "To share a preset you have already saved, right-click it instead.");

        ImGui.SameLine();

        var imported = false;
        if (ImGui.Button("Paste a code"))
            imported = ImportFromClipboard();

        Tooltip(named
            ? "Reads a code from the clipboard, saves it as one of your presets, and\n" +
              $"applies it. It will be filed under \"{this.newPresetName.Trim()}\" rather than\n" +
              "the name the code arrived with."
            : "Reads a code from the clipboard, saves it as one of your presets, and\n" +
              "applies it under the name it arrived with.\n\n" +
              "Type a name above first to file it under that instead.");

        DrawShareStatus();
        return imported;
    }

    private void DrawShareStatus()
    {
        if (this.shareStatus.Length == 0 || DateTime.UtcNow - this.shareStatusAt > ShareStatusLinger)
            return;

        using var color = ImRaii.PushColor(ImGuiCol.Text, this.shareFailed ? ShareBad : ShareGood);
        ImGui.TextWrapped(this.shareStatus);
    }

    private void Report(string message, bool failed)
    {
        this.shareStatus = message;
        this.shareFailed = failed;
        this.shareStatusAt = DateTime.UtcNow;
    }

    private bool ImportFromClipboard()
    {
        string clipboard;

        try
        {
            clipboard = ImGui.GetClipboardText();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Could not read the clipboard.");
            Report("Could not read the clipboard. Try again in a moment.", failed: true);
            return false;
        }

        if (!PresetCode.TryDecode(clipboard, out var preset, out var error) || preset is null)
        {
            Report(error, failed: true);
            return false;
        }

        var typed = this.newPresetName.Trim();
        if (typed.Length > 0)
        {
            preset.Name = typed;
            this.newPresetName = string.Empty;
        }

        preset.Name = UniqueName(preset.Name);
        this.config.UserPresets.Add(preset);
        preset.ApplyTo(this.config);

        Report($"Imported \"{preset.Name}\" and applied it.", failed: false);

        return true;
    }

    private string UniqueName(string wanted)
    {
        if (!NameTaken(wanted))
            return wanted;

        for (var n = 2; ; n++)
        {
            var candidate = $"{wanted} ({n})";
            if (!NameTaken(candidate))
                return candidate;
        }
    }

    private bool NameTaken(string name) => this.config.UserPresets.Any(
        p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

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
