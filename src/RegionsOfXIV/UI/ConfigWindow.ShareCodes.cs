using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private string shareStatus = string.Empty;
    private bool shareFailed;
    private DateTime shareStatusAt;
    private static readonly TimeSpan ShareStatusLinger = TimeSpan.FromSeconds(8);
    private static readonly Vector4 ShareGood = new(0.45f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 ShareBad = new(1f, 0.42f, 0.42f, 1f);

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

}
