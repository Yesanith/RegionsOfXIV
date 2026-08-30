using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private string shareStatus = string.Empty;
    private bool shareFailed;
    private DateTime shareStatusAt;
    private static readonly TimeSpan ShareStatusLinger = TimeSpan.FromSeconds(8);

    private bool DrawShareCodeRow()
    {
        UiText.Wrapped(Loc.Get("sharecodes.heading", "Share codes"));
        UiText.Disabled(Loc.Get(
            "sharecodes.intro",
            "A code is the whole preset as one line of text. Paste one into chat to hand it to someone."));

        var named = this.newPresetName.Trim().Length > 0;

        // Two long labels on one row. Placed rather than SameLine'd so that a language which sets
        // them wider than the window drops the second onto its own line instead of off the edge.
        var copy = Loc.Label("sharecodes.copy", "Copy current settings");
        var paste = Loc.Label("sharecodes.paste", "Paste a code");
        var row = new WrappingRow();

        row.Place(copy);

        using (ImRaii.Disabled(!named))
        {
            if (ImGui.Button(copy))
            {
                ImGui.SetClipboardText(PresetCode.Encode(this.newPresetName.Trim(), this.config));
                Report(Loc.Get("sharecodes.copied", "Copied. Paste it wherever you like."), failed: false);
            }
        }

        UiText.Tooltip(named
            ? Loc.Format(
                  "sharecodes.copy.tooltip.named",
                  "Puts a code for everything as it stands right now on the clipboard,\n" +
                  "under the name \"{0}\".",
                  this.newPresetName.Trim()) + CustomFontCodeNote()
            : Loc.Get(
                  "sharecodes.copy.tooltip.unnamed",
                  "Type a name in the box above first — it travels with the code, and it\n" +
                  "is all the person you send it to will have to go on.\n\n" +
                  "To share a preset you have already saved, right-click it instead."));

        row.Place(paste);

        var imported = false;
        if (ImGui.Button(paste))
            imported = ImportFromClipboard();

        UiText.Tooltip(named
            ? Loc.Format(
                  "sharecodes.paste.tooltip.named",
                  "Reads a code from the clipboard, saves it as one of your presets, and\n" +
                  "applies it. It will be filed under \"{0}\" rather than\n" +
                  "the name the code arrived with.",
                  this.newPresetName.Trim())
            : Loc.Get(
                  "sharecodes.paste.tooltip.unnamed",
                  "Reads a code from the clipboard, saves it as one of your presets, and\n" +
                  "applies it under the name it arrived with.\n\n" +
                  "Type a name above first to file it under that instead."));

        DrawShareStatus();
        return imported;
    }

    private void DrawShareStatus()
    {
        if (this.shareStatus.Length == 0 || DateTime.UtcNow - this.shareStatusAt > ShareStatusLinger)
            return;

        Warn(this.shareFailed ? FaultColor : GoodColor, this.shareStatus);
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
            Log.Debug(ex, "Could not read the clipboard.");
            Report(
                Loc.Get(
                    "sharecodes.clipboard.unreadable",
                    "Could not read the clipboard. Try again in a moment."),
                failed: true);
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

        // The note is a sentence of its own, so it is appended rather than folded into the message
        // as a second placeholder -- a translator only has to deal with one of them at a time.
        var message = Loc.Format(
            "sharecodes.imported", "Imported \"{0}\" and applied it.", preset.Name);

        if (MissingCustomFontNote() is { } note)
            Report($"{message} {note}", failed: true);
        else
            Report(message, failed: false);

        return true;
    }

    // The number is a disambiguator on a name the player chose, not prose, so it stays as it is in
    // every language.
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

    private string CustomFontCodeNote() => this.config.UsesCustomFont
        ? Loc.Get(
            "sharecodes.customfont.note",
            "\n\nThe code carries a font file from this PC. Whoever you send it to will\n" +
            "not have that file, so those lines fall back to Noto Sans CJK for them.")
        : string.Empty;

    private bool NameTaken(string name) => this.config.UserPresets.Any(
        p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
