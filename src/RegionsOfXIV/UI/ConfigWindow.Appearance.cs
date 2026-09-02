using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // Grouped by what is being coloured rather than by kind of widget. The seven pickers used to
    // run together with the outline thickness after them and "Colour each line separately" three
    // rows above two of the three pickers it enables, so every governing control was somewhere
    // other than beside the thing it governs.
    private void DrawAppearanceTab()
    {
        using var tab = ImRaii.TabItem(Loc.Label("appearance.tab", "Appearance"));
        if (!tab) return;

        var changed = false;

        DrawPlacement(ref changed);

        ImGui.Separator();

        DrawLettering(ref changed);

        ImGui.Separator();

        DrawHeaderShape(ref changed);

        ImGui.Separator();

        DrawFillColors(ref changed);

        ImGui.Separator();

        DrawOutline(ref changed);

        ImGui.Separator();

        DrawShadow(ref changed);

        ImGui.Separator();

        if (ImGui.Button(Loc.Label("appearance.preview", "Preview")))
            this.actions.Preview(Sample);

        if (!changed)
            return;

        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    private void DrawPlacement(ref bool changed)
    {
        // "%%" is an escaped per-cent sign rather than a word, so there is nothing in this format
        // for a translator to change and it stays whole.
        this.config.VerticalPosition = Slider(
            Loc.Label("appearance.vertical", "Vertical position"),
            this.config.VerticalPosition, 0f, 100f, "%.0f%%", ref changed);

        this.config.HorizontalPosition = Slider(
            Loc.Label("appearance.horizontal", "Horizontal position"),
            this.config.HorizontalPosition, 0f, 100f, "%.0f%%", ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.horizontal.tooltip",
            "The text is centred on this point, so 50% is the middle of the screen.\n" +
            "A long place name set near either end will reach past it."));
    }

    private void DrawLettering(ref bool changed)
    {
        this.config.LetterSpacing = Slider(
            Loc.Label("appearance.letterspacing", "Letter spacing"),
            this.config.LetterSpacing, 0f, 30f, "%.0f%%", ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.letterspacing.tooltip",
            "Extra space between letters, as a percentage of the font size, so it keeps\n" +
            "its proportions when the size changes and the header gets its own share.\n" +
            "Wide spacing is much of what gives the Guild Wars 2 original its look."));

        this.config.UppercaseText = Checkbox(
            Loc.Label("appearance.uppercase", "Uppercase"), this.config.UppercaseText, ref changed);
    }

    private void DrawHeaderShape(ref bool changed)
    {
        this.config.IncludeParentTierAsHeader = Checkbox(
            Loc.Label("appearance.showheader", "Show header"),
            this.config.IncludeParentTierAsHeader, ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.showheader.tooltip",
            "The smaller line above the name, giving where the place sits: the region\n" +
            "above a zone, the area above a sub-area.\n\n" +
            "Turned off, only the name itself is shown. The weather line, if you have\n" +
            "it on, is unaffected, as are the two settings below, which still apply\n" +
            "to it."));

        this.config.UnderlineHeader = Checkbox(
            Loc.Label("appearance.underlineheader", "Underline header"),
            this.config.UnderlineHeader, ref changed);

        this.config.HeaderGap = Slider(
            Loc.Label("appearance.headergap", "Header gap"),
            this.config.HeaderGap, 0.5f, 2.5f,
            "%.2f " + Loc.Unit("units.lines", "lines"), ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.headergap.tooltip",
            "How far the name sits below the header, measured in lines of header text.\n\n" +
            "Low values pull the two together until they almost touch, which is the\n" +
            "tighter look the plugin has always shipped with. Higher values open the\n" +
            "gap up and give each line room to breathe."));
    }

    // The colours the letters are drawn in. SeparateLineColors gates one picker here and two in
    // DrawOutline below, which is why its disabled block is opened twice rather than once: the
    // checkbox belongs with the pickers it enables, and those sit either side of the split
    // between fill and outline.
    private void DrawFillColors(ref bool changed)
    {
        this.config.TextColor = ColorPicker(
            Loc.Label("appearance.textcolour", "Text colour"), this.config.TextColor, ref changed);

        this.config.HeaderColor = ColorPicker(
            Loc.Label("appearance.headercolour", "Header colour"), this.config.HeaderColor, ref changed);

        this.config.SeparateLineColors = Checkbox(
            Loc.Label("appearance.separatecolours", "Colour each line separately"),
            this.config.SeparateLineColors, ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.separatecolours.tooltip",
            "Off, the weather line follows the header, and one outline colour\n" +
            "covers all three lines.\n\n" +
            "On, the weather line and the outlines take the colours below, so one\n" +
            "line can be pushed towards the background without touching the others."));

        using (ImRaii.Disabled(!this.config.SeparateLineColors))
        {
            this.config.WeatherColor = ColorPicker(
                Loc.Label("appearance.weathercolour", "Weather colour"),
                this.config.WeatherColor, ref changed);
        }
    }

    private void DrawOutline(ref bool changed)
    {
        this.config.StrokeColor = ColorPicker(
            Loc.Label("appearance.outlinecolour", "Outline colour"), this.config.StrokeColor, ref changed);

        using (ImRaii.Disabled(!this.config.SeparateLineColors))
        {
            this.config.HeaderStrokeColor = ColorPicker(
                Loc.Label("appearance.headeroutlinecolour", "Header outline colour"),
                this.config.HeaderStrokeColor, ref changed);

            this.config.WeatherStrokeColor = ColorPicker(
                Loc.Label("appearance.weatheroutlinecolour", "Weather outline colour"),
                this.config.WeatherStrokeColor, ref changed);
        }

        this.config.StrokeThickness = Slider(
            Loc.Label("appearance.outlinethickness", "Outline thickness"),
            this.config.StrokeThickness, 0f, 4f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get("appearance.outlinethickness.tooltip", "Zero turns the outline off."));
    }

    private void DrawShadow(ref bool changed)
    {
        this.config.ShadowEnabled = Checkbox(
            Loc.Label("appearance.shadow", "Drop shadow"), this.config.ShadowEnabled, ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.shadow.tooltip",
            "A second copy of every line, offset behind it. Sits under the outline, so\n" +
            "the two can be used together: a tight outline for legibility and a soft\n" +
            "shadow for depth.\n\n" +
            "One shadow covers all three lines; it does not follow the separate colours."));

        using var disabled = ImRaii.Disabled(!this.config.ShadowEnabled);

        this.config.ShadowColor = ColorPicker(
            Loc.Label("appearance.shadowcolour", "Shadow colour"), this.config.ShadowColor, ref changed);

        this.config.ShadowOffsetX = Slider(
            Loc.Label("appearance.shadowacross", "Shadow across"),
            this.config.ShadowOffsetX, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.shadowacross.tooltip", "Negative moves the shadow to the left."));

        this.config.ShadowOffsetY = Slider(
            Loc.Label("appearance.shadowdown", "Shadow down"),
            this.config.ShadowOffsetY, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.shadowdown.tooltip", "Negative lifts the shadow above the text."));

        this.config.ShadowSoftness = Slider(
            Loc.Label("appearance.shadowspread", "Shadow spread"),
            this.config.ShadowSoftness, 0f, 6f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get(
            "appearance.shadowspread.tooltip",
            "Fattens the shadow outwards. Zero keeps it the same shape as the\n" +
            "letters; higher values thicken it into a halo behind them."));
    }
}
