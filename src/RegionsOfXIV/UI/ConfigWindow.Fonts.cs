using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // Per-role scratch state for the custom font row. The typed path is buffered rather than
    // written straight to the config, because committing on every keystroke would rebuild the
    // font atlas per character; it lands when the field loses focus.
    //
    // ProblemWith caches its answer for the same reason -- validating a path touches the disk,
    // and this is drawn every frame the tab is open.
    private sealed class FontPathEditor
    {
        public string? Buffer;

        public string? Checked;

        public string? Problem;

        public string? ProblemWith(string path)
        {
            if (this.Checked == path)
                return this.Problem;

            this.Checked = path;
            this.Problem = FontService.CustomFontProblem(path);

            return this.Problem;
        }
    }

    private readonly FontPathEditor[] pathEditors = [new(), new(), new()];

    private void DrawFontsTab()
    {
        using var tab = ImRaii.TabItem("Fonts");
        if (!tab) return;

        ImGui.TextWrapped(
            "Every line of a notification has its own face and size. Pick one of the built-in "
            + "faces, or point the plugin at a font file on this PC.");

        ImGui.Spacing();

        using var roles = ImRaii.TabBar("##RegionsOfXIVFontRoles");
        if (!roles) return;

        DrawFontRole(
            FontRole.Text,
            "Text",
            "The place name itself, the largest line.",
            24f,
            140f);

        DrawFontRole(
            FontRole.Header,
            "Header",
            "The smaller line above the name, giving the region or area it sits in.",
            10f,
            72f);

        DrawFontRole(
            FontRole.Weather,
            "Weather",
            "The forecast line above the header, shown when weather announcements are on.",
            10f,
            72f);
    }

    private void DrawFontRole(FontRole role, string label, string describes, float minSize, float maxSize)
    {
        using var tab = ImRaii.TabItem(label);
        if (!tab) return;

        ImGui.TextDisabled(describes);
        ImGui.Spacing();

        var changed = false;
        var font = this.config.FontFor(role);

        font = font with
        {
            SizePx = Slider(
                $"Size##font-size-{label}", font.SizePx, minSize, maxSize, "%.0f px", ref changed),
        };

        font = font with
        {
            Choice = Choice($"Font##font-choice-{label}", font.Choice, Label, ref changed),
        };

        this.config.SetFontFor(role, font);

        Tooltip(
            "Noto Sans CJK is vector — sharp at any size, and it covers every language.\n\n"
            + "The game's own faces suit FFXIV better, but each is a bitmap with a ceiling:\n"
            + "Trump Gothic — Latin only, to 91 px.\n"
            + "Jupiter — Latin only, to 61 px.\n"
            + "Axis — to 48 px, the only one with Japanese glyphs.\n\n"
            + "Custom file loads a font of your own from this PC.");

        if (font.IsCustom)
            changed |= DrawCustomFont(role, label, font);
        else
            DrawStockFontWarnings(font);

        ImGui.Spacing();

        if (ImGui.Button($"Preview##font-preview-{label}"))
            this.actions.Preview(Sample);

        if (!changed)
            return;

        this.actions.RebuildFonts();
        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    private bool DrawCustomFont(FontRole role, string label, FontSetting font)
    {
        var editor = this.pathEditors[(int)role];
        var stored = font.Path;
        var buffer = editor.Buffer ??= stored;
        var changed = false;

        ImGui.SetNextItemWidth(
            Math.Max(ImGui.GetContentRegionAvail().X - (150f * ImGuiHelpers.GlobalScale), 120f));

        ImGui.InputTextWithHint(
            $"##font-path-{label}", "Path to a .ttf, .otf or .ttc file", ref buffer, 512);

        editor.Buffer = buffer;

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            stored = buffer.Trim().Trim('"');
            this.config.SetFontFor(role, font with { Path = stored });
            editor.Buffer = stored;
            changed = true;
        }
        else if (!ImGui.IsItemActive() && buffer != stored)
        {
            editor.Buffer = stored;
        }

        ImGui.SameLine();

        if (ImGui.Button($"Browse##font-browse-{label}"))
            BrowseForFont(role);

        Tooltip("Opens your Windows font folder. Any .ttf, .otf or .ttc file will do.");

        ImGui.SameLine();

        using (ImRaii.Disabled(stored.Length == 0))
        {
            if (ImGui.Button($"Clear##font-clear-{label}"))
            {
                this.config.SetFontFor(role, font with { Path = string.Empty });
                editor.Buffer = string.Empty;
                changed = true;
            }
        }

        DrawCustomFontStatus(role, editor, stored);
        DrawCustomFontNotice();

        return changed;
    }

    // Two sources of trouble: the path itself, checked here and immediately; and the atlas
    // failing to parse a file that does exist, which happens asynchronously and only shows up in
    // the font handle afterwards.
    private void DrawCustomFontStatus(FontRole role, FontPathEditor editor, string path)
    {
        var problem = editor.ProblemWith(path) ?? this.actions.FontProblem(role);

        if (problem == null)
        {
            ImGui.TextDisabled($"Drawing with {Path.GetFileName(path)}.");
            return;
        }

        Warn(FaultColor, problem);
    }

    private static void DrawCustomFontNotice()
    {
        ImGui.Spacing();

        Warn(
            CautionColor,
            "A font you supply is loaded exactly as it is, and it stays yours to look after. "
            + "Missing glyphs, odd spacing, soft edges, a file the game cannot read, or a licence "
            + "you do not hold are on you rather than on Regions of XIV, and no support is offered "
            + "for anything that comes of one. If a line stops looking right, put it back on one of "
            + "the built-in faces.");
    }

    private static void DrawStockFontWarnings(FontSetting font)
    {
        if (FontService.IsLatinOnly(font.Choice) &&
            Plugin.ClientState.ClientLanguage == ClientLanguage.Japanese)
        {
            Warn(
                FaultColor,
                $"{Label(font.Choice)} has no Japanese glyphs. On this client that means place names "
                + "will render as blank boxes, not just look soft. Choose Axis or Noto Sans CJK instead.");
        }

        var ceiling = FontService.NativeCeilingPx(font.Choice);
        var size = font.SizePx;

        if (size <= ceiling)
            return;

        Warn(
            CautionColor,
            $"This font has no bitmap above {ceiling:F0} px, so at {size:F0} px it is being "
            + "upscaled and will look soft. Lower the size, or switch to Noto Sans CJK, which "
            + "stays sharp at any size.");
    }

    private void BrowseForFont(FontRole role)
    {
        this.fileDialogs.OpenFileDialog(
            "Choose a font file",
            "Fonts{.ttf,.otf,.ttc}",
            (picked, chosen) => AdoptFont(role, picked, chosen),
            1,
            WindowsFontFolder(),
            false);
    }

    private void AdoptFont(FontRole role, bool picked, List<string> chosen)
    {
        if (!picked || chosen.Count == 0 || string.IsNullOrWhiteSpace(chosen[0]))
            return;

        this.config.SetFontFor(
            role,
            this.config.FontFor(role) with { Choice = FontChoice.Custom, Path = chosen[0] });

        this.pathEditors[(int)role].Buffer = chosen[0];

        this.actions.RebuildFonts();
        this.config.Save();
        this.actions.LivePreview(Sample);
    }

    private static string WindowsFontFolder()
    {
        try
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
