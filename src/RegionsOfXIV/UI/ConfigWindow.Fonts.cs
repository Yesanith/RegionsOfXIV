using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // Per-role scratch state for the custom font row. The typed path is buffered rather than
    // written straight to the config, because committing on every keystroke would rebuild the
    // font atlas per character; it lands when the field loses focus.
    //
    // ProblemWith caches its answer for the same reason -- validating a path touches the disk,
    // and this is drawn every frame the page is open.
    private sealed class FontPathEditor
    {
        public string? Buffer;

        public string? Checked;

        public string? Problem;

        private string? label;

        private int labelGeneration = -1;

        public string? ProblemWith(string path)
        {
            if (this.Checked == path)
            {
                return this.Problem;
            }

            this.Checked = path;
            this.Problem = FontLimits.CustomFontProblem(path);
            this.label = null;

            return this.Problem;
        }

        // Rebuilt when the path or the language changes, not on every frame the page is drawn.
        public string LabelFor(string path)
        {
            if (this.label is not null && this.labelGeneration == Loc.Generation)
            {
                return this.label;
            }

            this.labelGeneration = Loc.Generation;
            return this.label = Loc.Format("fonts.drawingwith", "Drawing with {0}.", Path.GetFileName(path));
        }
    }

    private readonly FontPathEditor[] pathEditors = [new(), new(), new()];

    private readonly Segmented.Item[] fontRoleItems = new Segmented.Item[3];

    private int fontRole;

    private int fontRoleGeneration = -1;

    // Pixels at 100% Dalamud scale. The atlas multiplies by the global scale when it builds, so a
    // player at 200% sees twice these and pays four times the texture for them.
    //
    // The ceiling is FontLimits.MaxAffordablePx, read from there rather than repeated here so the
    // slider cannot offer a size the build would then hold down.
    private const float MinTextPx = 24f;

    private const float MinHeaderPx = 10f;

    private const float ButtonGap = 8f;

    private void DrawFontsPage()
    {
        PageHeader.Draw(
            Loc.Get("fonts.tab", "Fonts"),
            Loc.Get(
                "fonts.intro",
                "Every line of a notification has its own face and size. Pick one of the built-in "
                + "faces, or point the plugin at a font file on this PC."));

        Segmented.Draw("##rox-font-roles", FontRoleItems(), ref this.fontRole);
        Styling.VSpace(14f);

        switch ((FontRole)this.fontRole)
        {
            case FontRole.Header:
                DrawFontRole(
                    FontRole.Header,
                    Loc.Get("fonts.role.header", "Header"),
                    Loc.Get("fonts.role.header.hint", "The smaller line above the name, giving the region or area it sits in."),
                    MinHeaderPx,
                    FontLimits.MaxAffordablePx(FontRole.Header));
                break;
            case FontRole.Weather:
                DrawFontRole(
                    FontRole.Weather,
                    Loc.Get("fonts.role.weather", "Weather"),
                    Loc.Get("fonts.role.weather.hint", "The forecast line above the header, shown when weather announcements are on."),
                    MinHeaderPx,
                    FontLimits.MaxAffordablePx(FontRole.Weather));
                break;
            default:
                DrawFontRole(
                    FontRole.Text,
                    Loc.Get("fonts.role.text", "Text"),
                    Loc.Get("fonts.role.text.hint", "The place name itself, the largest line."),
                    MinTextPx,
                    FontLimits.MaxAffordablePx(FontRole.Text));
                break;
        }
    }

    private Segmented.Item[] FontRoleItems()
    {
        if (this.fontRoleGeneration == Loc.Generation)
        {
            return this.fontRoleItems;
        }

        this.fontRoleItems[(int)FontRole.Text] = new Segmented.Item(FontAwesomeIcon.Heading, Loc.Get("fonts.role.text", "Text"));
        this.fontRoleItems[(int)FontRole.Header] = new Segmented.Item(FontAwesomeIcon.MapSigns, Loc.Get("fonts.role.header", "Header"));
        this.fontRoleItems[(int)FontRole.Weather] = new Segmented.Item(FontAwesomeIcon.CloudSun, Loc.Get("fonts.role.weather", "Weather"));
        this.fontRoleGeneration = Loc.Generation;
        return this.fontRoleItems;
    }

    // Every identity here is built from the role rather than from the label. The label is
    // translated, and an identity that moves with the language is a different widget in each one.
    private void DrawFontRole(FontRole role, string label, string describes, float minSize, float maxSize)
    {
        var changed = false;
        var font = this.config.FontFor(role);

        using (Motion.PushSwitch("##rox-font-role-page", (int)role))
        using (SettingsGroup.Begin(label, describes))
        {
            // Logarithmic, because the useful range sits near the bottom: a linear slider from
            // 24 to 280 puts every size anyone actually picks into the first fifth of the track.
            font = font with
            {
                SizePx = Slider(
                    $"##rox-font-size-{(int)role}",
                    Loc.Get("fonts.size", "Size"), null,
                    font.SizePx, minSize, maxSize,
                    "%.0f " + Loc.Unit("units.px", "px"), ref changed,
                    ImGuiSliderFlags.Logarithmic),
            };

            font = font with
            {
                Choice = Choice(
                    $"##rox-font-choice-{(int)role}",
                    Loc.Get("fonts.face", "Font"),
                    Loc.Get(
                        "fonts.face.tooltip",
                        "Noto Sans CJK is vector: sharp at any size, and it covers every language.\n\n"
                        + "The game's own faces suit FFXIV better, but each is a bitmap with a ceiling:\n"
                        + "Trump Gothic: Latin only, to 91 px.\n"
                        + "Jupiter: Latin only, to 61 px.\n"
                        + "Axis: to 48 px, the only one with Japanese glyphs.\n\n"
                        + "Custom file loads a font of your own from this PC."),
                    font.Choice, FontChoiceLabels, ref changed),
            };

            this.config.SetFontFor(role, font);

            if (font.IsCustom)
            {
                changed |= DrawCustomFont(role, font);
            }
        }

        DrawSizeCeilingNote(role, font);

        if (font.IsCustom)
        {
            DrawCustomFontNotice();
        }
        else
        {
            DrawStockFontWarnings(font);
        }

        if (!changed)
        {
            return;
        }

        this.actions.RebuildFonts();
        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    // Only reachable by a config that outlived a change of client language, or one edited by
    // hand. Drawn for every role rather than through ProblemWith, which the stock path never
    // reaches, and Noto is exactly what someone at a large size will be on.
    private static void DrawSizeCeilingNote(FontRole role, FontSetting font)
    {
        var ceiling = FontLimits.MaxAffordablePx(role);

        if (font.SizePx <= ceiling)
        {
            return;
        }

        Warn(Loc.Format(
            "fonts.reduced",
            "Drawing at {0:F0} px rather than {1:F0}. This client shows Japanese place names, "
            + "so every notification font has to carry kanji, and at that size it would be too "
            + "large to build. Your setting is kept as it is and comes back if the game's "
            + "language changes.",
            ceiling,
            font.SizePx));
    }

    private bool DrawCustomFont(FontRole role, FontSetting font)
    {
        var editor = this.pathEditors[(int)role];
        var stored = font.Path;
        var buffer = editor.Buffer ??= stored;
        var changed = false;
        var scale = ImGuiHelpers.GlobalScale;

        SettingsRow.Block(Loc.Get("fonts.path", "Font file"), null);

        // Browse and Clear sit to the right of this field, so the field gets what is left after
        // measuring them.
        var browse = Loc.Get("fonts.browse", "Browse");
        var clear = Loc.Get("fonts.clear", "Clear");
        var buttons = PillButton.Width(browse, FontAwesomeIcon.FolderOpen) + PillButton.Width(clear, FontAwesomeIcon.Times) + (ButtonGap * 2f * scale);
        var fieldWidth = Math.Max(SettingsGroup.InnerWidth() - buttons, 120f * scale);

        var field = TextField.Draw(
            $"##rox-font-path-{(int)role}",
            Loc.Get("fonts.path.hint", "Path to a .ttf, .otf or .ttc file"),
            ref buffer,
            fieldWidth);

        editor.Buffer = buffer;

        if (field.Committed)
        {
            stored = buffer.Trim().Trim('"');
            this.config.SetFontFor(role, font with { Path = stored });
            editor.Buffer = stored;
            changed = true;
        }
        else if (!field.Active && buffer != stored)
        {
            editor.Buffer = stored;
        }

        ImGui.SameLine(0f, ButtonGap * scale);
        if (PillButton.Draw($"##rox-font-browse-{(int)role}", browse, Styling.AccentGold, PillButton.Emphasis.Tinted, FontAwesomeIcon.FolderOpen,
                height: Layout.FieldHeight,
                tooltip: Loc.Get("fonts.browse.tooltip", "Opens your Windows font folder. Any .ttf, .otf or .ttc file will do.")))
        {
            BrowseForFont(role);
        }

        ImGui.SameLine(0f, ButtonGap * scale);
        if (PillButton.Draw($"##rox-font-clear-{(int)role}", clear, Styling.AccentRose, PillButton.Emphasis.Ghost, FontAwesomeIcon.Times,
                enabled: stored.Length > 0, height: Layout.FieldHeight))
        {
            this.config.SetFontFor(role, font with { Path = string.Empty });
            editor.Buffer = string.Empty;
            changed = true;
        }

        DrawCustomFontStatus(role, editor, stored);
        SettingsRow.EndBlock();

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
            SettingsRow.Note(editor.LabelFor(path), Styling.AccentMintSoft);
            return;
        }

        SettingsRow.Note(problem, Styling.AccentRoseSoft);
    }

    private static void DrawCustomFontNotice()
    {
        Warn(Loc.Get(
            "fonts.custom.notice",
            "A font you supply is loaded exactly as it is, and it stays yours to look after. "
            + "Missing glyphs, odd spacing, soft edges, a file the game cannot read, or a licence "
            + "you do not hold are on you rather than on Regions of XIV, and no support is offered "
            + "for anything that comes of one. If a line stops looking right, put it back on one of "
            + "the built-in faces."));
    }

    private static void DrawStockFontWarnings(FontSetting font)
    {
        if (FontLimits.IsLatinOnly(font.Choice) &&
            Plugin.ClientState.ClientLanguage == ClientLanguage.Japanese)
        {
            Fault(Loc.Format(
                "fonts.nojapanese",
                "{0} has no Japanese glyphs. On this client that means place names "
                + "will render as blank boxes, not just look soft. Choose Axis or Noto Sans CJK instead.",
                Label(font.Choice)));
        }

        var ceiling = FontLimits.NativeCeilingPx(font.Choice);
        var size = font.SizePx;

        if (size <= ceiling)
        {
            return;
        }

        Warn(Loc.Format(
            "fonts.upscaled",
            "This font has no bitmap above {0:F0} px, so at {1:F0} px it is being "
            + "upscaled and will look soft. Lower the size, or switch to Noto Sans CJK or a "
            + "font file of your own, either of which stays sharp at any size.",
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
        {
            return;
        }

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
