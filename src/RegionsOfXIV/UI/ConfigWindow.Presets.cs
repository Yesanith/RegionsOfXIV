using System;
using System.Linq;
using System.Numerics;
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
// Both kinds write every setting, so the page says so once at the top rather than
// twice underneath. What still separates them is where the values come from and
// what can be done to them: a built-in is the defaults plus a look, is fixed, and
// cannot be removed; a saved preset is whatever the player had at the moment they
// saved, and can be overwritten or deleted.
internal sealed partial class ConfigWindow
{
    private string newPresetName = string.Empty;

    // The outcome of the last copy or paste, shown under the buttons. Held rather
    // than raised as a toast because it belongs to the thing that was clicked, and
    // a failed import in particular needs to be read next to the button that
    // caused it.
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

        // Collected rather than acted on inside the loop: mutating a List while
        // enumerating it throws, and one of each per frame is plenty.
        UserPreset? remove = null;
        UserPreset? overwrite = null;
        UserPreset? share = null;

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
            // The preset's own stored snapshot, not the live config — this shares
            // what the button would apply, which is not necessarily what is on
            // screen right now.
            ImGui.SetClipboardText(PresetCode.Encode(share.Name, share.Settings));
            Report($"Copied a code for \"{share.Name}\".", failed: false);
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

        // One box, three uses: it names a preset being saved, a code being copied,
        // and a code being imported. All three are "what do you want to call this",
        // asked at the moment you would answer it, so a second field would only
        // raise the question of which one applied.
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

    // Codes travel through the clipboard rather than a text box. A code runs to a
    // few hundred characters, an ImGui input field would show about a fifth of one,
    // and nobody types these — they are copied out of a chat window.
    private bool DrawShareCodeRow()
    {
        ImGui.TextWrapped("Share codes");
        ImGui.TextDisabled("A code is the whole preset as one line of text. Paste one into chat to hand it to someone.");

        var named = this.newPresetName.Trim().Length > 0;

        // Disabled without a name rather than falling back to one.
        //
        // The fallback used to be "Shared preset", which is how everybody who
        // copied without typing anything ended up handing out codes by that name —
        // and how anyone importing several ended up with "Shared preset",
        // "Shared preset (2)", "Shared preset (3)" and no way to tell them apart.
        // The name is the only thing a recipient has to go on, so it is fixed here,
        // at the one point where somebody knows what the preset is.
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
            // The clipboard is a shared OS resource and another process can hold it
            // open, which surfaces here rather than anywhere we control.
            Plugin.Log.Debug(ex, "Could not read the clipboard.");
            Report("Could not read the clipboard. Try again in a moment.", failed: true);
            return false;
        }

        if (!PresetCode.TryDecode(clipboard, out var preset, out var error) || preset is null)
        {
            Report(error, failed: true);
            return false;
        }

        // A name typed here wins over the one in the code. Codes made by this build
        // always carry a deliberate name, but ones made before that was enforced do
        // not, and neither does a code whose author called it something you already
        // use — so the person importing gets the final say over their own list.
        var typed = this.newPresetName.Trim();
        if (typed.Length > 0)
        {
            preset.Name = typed;
            this.newPresetName = string.Empty;
        }

        // Saved before it is applied, so a code someone liked enough to paste is
        // still there after they go on to change things.
        preset.Name = UniqueName(preset.Name);
        this.config.UserPresets.Add(preset);
        preset.ApplyTo(this.config);

        Report($"Imported \"{preset.Name}\" and applied it.", failed: false);

        // The caller saves and rebuilds, same as applying any other preset.
        return true;
    }

    // Two presets with the same name are indistinguishable in a row of buttons, and
    // an import is the one path that can produce a collision without the user
    // having typed anything. Terminates: the list is finite.
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
