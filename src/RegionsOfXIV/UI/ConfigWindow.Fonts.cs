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
        using var tab = ImRaii.TabItem(Loc.Label("fonts.tab", "Fonts"));
        if (!tab) return;

        UiText.Wrapped(Loc.Get(
            "fonts.intro",
            "Every line of a notification has its own face and size. Pick one of the built-in "
            + "faces, or point the plugin at a font file on this PC."));

        ImGui.Spacing();

        using var roles = ImRaii.TabBar("##RegionsOfXIVFontRoles");
        if (!roles) return;

        DrawFontRole(
            FontRole.Text,
            Loc.Get("fonts.role.text", "Text"),
            Loc.Get("fonts.role.text.hint", "The place name itself, the largest line."),
            24f,
            140f);

        DrawFontRole(
            FontRole.Header,
            Loc.Get("fonts.role.header", "Header"),
            Loc.Get(
                "fonts.role.header.hint",
                "The smaller line above the name, giving the region or area it sits in."),
            10f,
            72f);

        DrawFontRole(
            FontRole.Weather,
            Loc.Get("fonts.role.weather", "Weather"),
            Loc.Get(
                "fonts.role.weather.hint",
                "The forecast line above the header, shown when weather announcements are on."),
            10f,
            72f);
    }

    // Every identity here is built from the role rather than from the label. The label is
    // translated, and an identity that moves with the language is a different widget in each one.
    private void DrawFontRole(FontRole role, string label, string describes, float minSize, float maxSize)
    {
        using var tab = ImRaii.TabItem($"{label}###font-role-{role}");
        if (!tab) return;

        UiText.Disabled(describes);
        ImGui.Spacing();

        var changed = false;
        var font = this.config.FontFor(role);

        font = font with
        {
            SizePx = Slider(
                Loc.Get("fonts.size", "Size") + $"###font-size-{role}",
                font.SizePx, minSize, maxSize,
                "%.0f " + Loc.Unit("units.px", "px"), ref changed),
        };

        font = font with
        {
            Choice = Choice(
                Loc.Get("fonts.face", "Font") + $"###font-choice-{role}",
                font.Choice, Label, ref changed),
        };

        this.config.SetFontFor(role, font);

        UiText.Tooltip(Loc.Get(
            "fonts.face.tooltip",
            "Noto Sans CJK is vector: sharp at any size, and it covers every language.\n\n"
            + "The game's own faces suit FFXIV better, but each is a bitmap with a ceiling:\n"
            + "Trump Gothic: Latin only, to 91 px.\n"
            + "Jupiter: Latin only, to 61 px.\n"
            + "Axis: to 48 px, the only one with Japanese glyphs.\n\n"
            + "Custom file loads a font of your own from this PC."));

        if (font.IsCustom)
            changed |= DrawCustomFont(role, font);
        else
            DrawStockFontWarnings(font);

        ImGui.Spacing();

        if (ImGui.Button(Loc.Get("fonts.preview", "Preview") + $"###font-preview-{role}"))
            this.actions.Preview(Sample);

        if (!changed)
            return;

        this.actions.RebuildFonts();
        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    private bool DrawCustomFont(FontRole role, FontSetting font)
    {
        var editor = this.pathEditors[(int)role];
        var stored = font.Path;
        var buffer = editor.Buffer ??= stored;
        var changed = false;

        // Browse and Clear sit to the right of this field, so the field gets what is left after
        // measuring them. The floor scales with the interface, which the fixed one it replaced
        // did not.
        var browse = Loc.Get("fonts.browse", "Browse") + $"###font-browse-{role}";
        var clear = Loc.Get("fonts.clear", "Clear") + $"###font-clear-{role}";

        ImGui.SetNextItemWidth(Math.Max(
            ImGui.GetContentRegionAvail().X - ButtonRowWidth(browse, clear),
            120f * ImGuiHelpers.GlobalScale));

        ImGui.InputTextWithHint(
            $"##font-path-{role}",
            Loc.Get("fonts.path.hint", "Path to a .ttf, .otf or .ttc file"),
            ref buffer,
            512);

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

        if (ImGui.Button(browse))
            BrowseForFont(role);

        UiText.Tooltip(Loc.Get(
            "fonts.browse.tooltip",
            "Opens your Windows font folder. Any .ttf, .otf or .ttc file will do."));

        ImGui.SameLine();

        using (ImRaii.Disabled(stored.Length == 0))
        {
            if (ImGui.Button(clear))
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
            UiText.Disabled(Loc.Format(
                "fonts.drawingwith", "Drawing with {0}.", Path.GetFileName(path)));
            return;
        }

        Warn(FaultColor, problem);
    }

    private static void DrawCustomFontNotice()
    {
        ImGui.Spacing();

        Warn(
            CautionColor,
            Loc.Get(
                "fonts.custom.notice",
                "A font you supply is loaded exactly as it is, and it stays yours to look after. "
                + "Missing glyphs, odd spacing, soft edges, a file the game cannot read, or a licence "
                + "you do not hold are on you rather than on Regions of XIV, and no support is offered "
                + "for anything that comes of one. If a line stops looking right, put it back on one of "
                + "the built-in faces."));
    }

    private static void DrawStockFontWarnings(FontSetting font)
    {
        if (FontService.IsLatinOnly(font.Choice) &&
            Plugin.ClientState.ClientLanguage == ClientLanguage.Japanese)
        {
            Warn(
                FaultColor,
                Loc.Format(
                    "fonts.nojapanese",
                    "{0} has no Japanese glyphs. On this client that means place names "
                    + "will render as blank boxes, not just look soft. Choose Axis or Noto Sans CJK instead.",
                    Label(font.Choice)));
        }

        var ceiling = FontService.NativeCeilingPx(font.Choice);
        var size = font.SizePx;

        if (size <= ceiling)
            return;

        Warn(
            CautionColor,
            Loc.Format(
                "fonts.upscaled",
                "This font has no bitmap above {0:F0} px, so at {1:F0} px it is being "
                + "upscaled and will look soft. Lower the size, or switch to Noto Sans CJK, which "
                + "stays sharp at any size.",
                ceiling,
                size));
    }

    private void BrowseForFont(FontRole role)
    {
        this.fileDialogs.OpenFileDialog(
            Loc.Get("fonts.dialog.title", "Choose a font file"),
            // Not translated: the braces are Dalamud's filter syntax rather than punctuation, and a
            // translator has no way to know that breaking them stops the dialog listing anything.
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
