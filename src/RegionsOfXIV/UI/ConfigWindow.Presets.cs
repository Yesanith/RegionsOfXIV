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
        using var tab = ImRaii.TabItem(Loc.Label("presets.tab", "Presets"));
        if (!tab) return;

        var applied = false;

        UiText.Wrapped(Loc.Get(
            "presets.intro",
            "A preset covers Announcements, Appearance, Motion and Fonts: the face and size of "
            + "each line, position, colours, motion and particles, which tiers announce, the "
            + "timings, and when to stay quiet. Sound is not included."));
        UiText.Disabled(Loc.Get(
            "presets.intro.warning",
            "Applying one replaces all of them, so anything you have changed is overwritten."));

        ImGui.Spacing();
        ImGui.Separator();

        UiText.Wrapped(Loc.Get("presets.builtin", "Built-in looks"));
        UiText.Disabled(Loc.Get(
            "presets.builtin.hint", "The defaults, plus the look each one is named for."));
        applied |= DrawBuiltInPresets();

        ImGui.Separator();

        UiText.Wrapped(Loc.Get("presets.saved", "Your presets"));
        UiText.Disabled(Loc.Get(
            "presets.saved.hint", "Your settings, saved exactly as they were."));

        if (this.config.UserPresets.Count == 0)
            UiText.Disabled(Loc.Get("presets.saved.none", "You have not saved any yet."));

        applied |= DrawSavedPresets();

        ImGui.Separator();
        DrawCustomFontPresetWarning();
        DrawSavePresetRow();

        ImGui.Separator();
        applied |= DrawShareCodeRow();

        ImGui.Spacing();
        DrawDiscordLink(
            Loc.Label("presets.discord", "Join the Discord"),
            Loc.Get("presets.discord.tooltip", "Trade preset codes, report a bug, or suggest a feature."));

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

    // Built-in preset names are identifiers, not prose: they travel inside share codes, so a
    // "Inferno" saved here has to arrive as "Inferno" on someone else's machine. Only the
    // description is translated.
    private static string DescriptionOf(in Preset preset) => preset.Name switch
    {
        "Classic" => Loc.Get("presets.classic.description", preset.Description),
        "Inferno" => Loc.Get("presets.inferno.description", preset.Description),
        "Sweetheart" => Loc.Get("presets.sweetheart.description", preset.Description),
        "Starlight" => Loc.Get("presets.starlight.description", preset.Description),
        "Sakura" => Loc.Get("presets.sakura.description", preset.Description),
        "Dispatch" => Loc.Get("presets.dispatch.description", preset.Description),
        "Tyria" => Loc.Get("presets.tyria.description", preset.Description),
        _ => preset.Description,
    };

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

            UiText.Tooltip(Loc.Format(
                "presets.builtin.tooltip",
                "{0}\nEverything else returns to its default.",
                DescriptionOf(preset)));
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

            UiText.Tooltip(Loc.Get(
                "presets.saved.tooltip",
                "Apply this preset.\nRight-click to share, overwrite or delete it."));

            if (ImGui.BeginPopupContextItem($"##saved-ctx-{preset.Name}"))
            {
                if (ImGui.Selectable(Loc.Label("presets.menu.share", "Copy share code")))
                    share = preset;

                if (ImGui.Selectable(Loc.Label(
                        "presets.menu.overwrite", "Overwrite with current settings")))
                    overwrite = preset;

                if (ImGui.Selectable(Loc.Label("presets.menu.delete", "Delete")))
                    remove = preset;

                ImGui.EndPopup();
            }
        }

        if (share is not null)
        {
            ImGui.SetClipboardText(PresetCode.Encode(share.Name, share.Settings));
            Report(
                Loc.Format("presets.copied", "Copied a code for \"{0}\".", share.Name),
                failed: false);
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

        // One identity for both wordings. The button changed identity the moment the typed
        // name matched a saved preset, which ImGui reads as a different widget entirely.
        var save = (existing is null
            ? Loc.Get("presets.save", "Save")
            : Loc.Get("presets.replace", "Replace")) + "###presets.save";

        var hint = Loc.Get("presets.name.hint", "Name it to save, copy or import");

        // 180 was picked against the English hint, which a longer one would be cut off inside. The
        // field grows to fit whatever the hint measures, but never past what the button beside it
        // needs, so the two always share the row.
        var floor = 180f * ImGuiHelpers.GlobalScale;
        var room = ImGui.GetContentRegionAvail().X - ButtonRowWidth(save);
        var wanted = ImGui.CalcTextSize(hint).X + (ImGui.GetStyle().FramePadding.X * 2f);

        ImGui.SetNextItemWidth(Math.Max(floor, Math.Min(wanted, room)));
        ImGui.InputTextWithHint("##new-preset-name", hint, ref this.newPresetName, 48);

        ImGui.SameLine();

        using (ImRaii.Disabled(name.Length == 0))
        {
            if (ImGui.Button(save))
            {
                if (existing is not null)
                    this.config.UserPresets.Remove(existing);

                this.config.UserPresets.Add(UserPreset.Capture(name, this.config));
                this.config.Save();
                this.newPresetName = string.Empty;
            }
        }

        UiText.Tooltip(Loc.Get(
            "presets.save.tooltip",
            "Saves every setting on Announcements, Appearance, Motion and Fonts. Sound is not "
            + "included."));
    }

    private void DrawCustomFontPresetWarning()
    {
        if (!this.config.UsesCustomFont)
            return;

        Warn(
            CautionColor,
            Loc.Get(
                "presets.customfont.warning",
                "These settings use a font from this PC. A preset stores where that file sits, not the "
                + "font itself, so on anyone else's machine the path will not exist and those lines fall "
                + "back to Noto Sans CJK. Everything else in the preset still applies. Put the line "
                + "back on a built-in face if you want a preset to look the same for whoever you hand it to."));

        ImGui.Spacing();
    }

    // A preset carries the path to a custom font, not the font, so a code that travels between
    // machines usually points at a file the recipient does not have.
    private string? MissingCustomFontNote()
    {
        foreach (var role in Enum.GetValues<FontRole>())
        {
            var font = this.config.FontFor(role);

            if (font.IsCustom && FontLimits.CustomFontProblem(font.Path) != null)
                return Loc.Get(
                    "presets.customfont.missing",
                    "It carries a font file from the sender's PC that is not on yours, so those "
                    + "lines are drawn with Noto Sans CJK. Choose your own on the Fonts tab.");
        }

        return null;
    }

    private struct WrappingRow()
    {
        private float spent = 0f;

        // Measured with the identity hidden, because callers pass the same "text###key" they give
        // the button. CalcTextSize does not skip it by default, so the key would be measured as if
        // it were on screen and the row would wrap after one button instead of four.
        public void Place(string label)
        {
            var available = ImGui.GetContentRegionAvail().X;
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var width = ImGui.CalcTextSize(label, hideTextAfterDoubleHash: true).X
                        + (ImGui.GetStyle().FramePadding.X * 2f);

            if (this.spent > 0f && this.spent + width < available)
                ImGui.SameLine();
            else
                this.spent = 0f;

            this.spent += width + spacing;
        }
    }
}
