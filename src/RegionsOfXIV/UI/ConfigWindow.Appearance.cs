using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // Grouped by what is being coloured rather than by kind of widget, so every governing control
    // sits beside the thing it governs.
    private void DrawAppearancePage()
    {
        PageHeader.Draw(
            Loc.Get("appearance.tab", "Appearance"),
            Loc.Get(
                "appearance.intro",
                "Where the notification sits, how it is lettered, and the colours, outline and "
                + "shadow it is drawn with. The sample follows every slider as you move it."));

        var changed = false;

        using (SettingsGroup.Begin(Loc.Get("appearance.group.placement", "Placement")))
        {
            DrawPlacement(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.lettering", "Lettering")))
        {
            DrawLettering(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.header", "Header")))
        {
            DrawHeaderShape(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.colours", "Colours")))
        {
            DrawFillColors(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.outline", "Outline")))
        {
            DrawOutline(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.shadow", "Drop shadow")))
        {
            DrawShadow(ref changed);
        }

        if (!changed)
        {
            return;
        }

        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    private void DrawPlacement(ref bool changed)
    {
        // "%%" is an escaped per-cent sign rather than a word, so there is nothing in this format
        // for a translator to change and it stays whole.
        this.config.VerticalPosition = Slider(
            "##rox-vertical", Loc.Get("appearance.vertical", "Vertical position"), null,
            this.config.VerticalPosition, 0f, 100f, "%.0f%%", ref changed);

        this.config.HorizontalPosition = Slider(
            "##rox-horizontal",
            Loc.Get("appearance.horizontal", "Horizontal position"),
            Loc.Get(
                "appearance.horizontal.tooltip",
                "The text is centred on this point, so 50% is the middle of the screen.\n" +
                "A long place name set near either end will reach past it."),
            this.config.HorizontalPosition, 0f, 100f, "%.0f%%", ref changed);
    }

    private void DrawLettering(ref bool changed)
    {
        this.config.LetterSpacing = Slider(
            "##rox-letterspacing",
            Loc.Get("appearance.letterspacing", "Letter spacing"),
            Loc.Get(
                "appearance.letterspacing.tooltip",
                "Extra space between letters, as a percentage of the font size, so it keeps\n" +
                "its proportions when the size changes and the header gets its own share.\n" +
                "Wide spacing is much of what gives the Guild Wars 2 original its look."),
            this.config.LetterSpacing, 0f, 30f, "%.0f%%", ref changed);

        this.config.UppercaseText = Toggle(
            "##rox-uppercase", Loc.Get("appearance.uppercase", "Uppercase"), null,
            this.config.UppercaseText, ref changed);
    }

    private void DrawHeaderShape(ref bool changed)
    {
        this.config.IncludeParentTierAsHeader = Toggle(
            "##rox-showheader",
            Loc.Get("appearance.showheader", "Show header"),
            Loc.Get(
                "appearance.showheader.tooltip",
                "The smaller line above the name, giving where the place sits: the region\n" +
                "above a zone, the area above a sub-area.\n\n" +
                "Turned off, only the name itself is shown. The weather line, if you have\n" +
                "it on, is unaffected, as are the two settings below, which still apply\n" +
                "to it."),
            this.config.IncludeParentTierAsHeader, ref changed);

        this.config.UnderlineHeader = Toggle(
            "##rox-underlineheader", Loc.Get("appearance.underlineheader", "Underline header"), null,
            this.config.UnderlineHeader, ref changed);

        this.config.HeaderGap = Slider(
            "##rox-headergap",
            Loc.Get("appearance.headergap", "Header gap"),
            Loc.Get(
                "appearance.headergap.tooltip",
                "How far the name sits below the header, measured in lines of header text.\n\n" +
                "Low values pull the two together until they almost touch, which is the\n" +
                "tighter look the plugin has always shipped with. Higher values open the\n" +
                "gap up and give each line room to breathe."),
            this.config.HeaderGap, 0.5f, 2.5f,
            "%.2f " + Loc.Unit("units.lines", "lines"), ref changed);
    }

    // SeparateLineColors gates one picker here and two in DrawOutline below: the switch belongs
    // with the pickers it enables, and those sit either side of the split between fill and outline.
    private void DrawFillColors(ref bool changed)
    {
        this.config.TextColor = Colour(
            "##rox-textcolour", Loc.Get("appearance.textcolour", "Text colour"), null,
            this.config.TextColor, ref changed);

        this.config.HeaderColor = Colour(
            "##rox-headercolour", Loc.Get("appearance.headercolour", "Header colour"), null,
            this.config.HeaderColor, ref changed);

        this.config.SeparateLineColors = Toggle(
            "##rox-separatecolours",
            Loc.Get("appearance.separatecolours", "Colour each line separately"),
            Loc.Get(
                "appearance.separatecolours.tooltip",
                "Off, the weather line follows the header, and one outline colour\n" +
                "covers all three lines.\n\n" +
                "On, the weather line and the outlines take the colours below, so one\n" +
                "line can be pushed towards the background without touching the others."),
            this.config.SeparateLineColors, ref changed);

        this.config.WeatherColor = Colour(
            "##rox-weathercolour", Loc.Get("appearance.weathercolour", "Weather colour"), null,
            this.config.WeatherColor, ref changed, this.config.SeparateLineColors);
    }

    private void DrawOutline(ref bool changed)
    {
        this.config.StrokeColor = Colour(
            "##rox-outlinecolour", Loc.Get("appearance.outlinecolour", "Outline colour"), null,
            this.config.StrokeColor, ref changed);

        this.config.HeaderStrokeColor = Colour(
            "##rox-headeroutlinecolour", Loc.Get("appearance.headeroutlinecolour", "Header outline colour"), null,
            this.config.HeaderStrokeColor, ref changed, this.config.SeparateLineColors);

        this.config.WeatherStrokeColor = Colour(
            "##rox-weatheroutlinecolour", Loc.Get("appearance.weatheroutlinecolour", "Weather outline colour"), null,
            this.config.WeatherStrokeColor, ref changed, this.config.SeparateLineColors);

        this.config.StrokeThickness = Slider(
            "##rox-outlinethickness",
            Loc.Get("appearance.outlinethickness", "Outline thickness"),
            Loc.Get("appearance.outlinethickness.tooltip", "Zero turns the outline off."),
            this.config.StrokeThickness, 0f, 4f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed);
    }

    private void DrawShadow(ref bool changed)
    {
        this.config.ShadowEnabled = Toggle(
            "##rox-shadow",
            Loc.Get("appearance.shadow", "Drop shadow"),
            Loc.Get(
                "appearance.shadow.tooltip",
                "A second copy of every line, offset behind it. Sits under the outline, so\n" +
                "the two can be used together: a tight outline for legibility and a soft\n" +
                "shadow for depth.\n\n" +
                "One shadow covers all three lines; it does not follow the separate colours."),
            this.config.ShadowEnabled, ref changed);

        var enabled = this.config.ShadowEnabled;

        this.config.ShadowColor = Colour(
            "##rox-shadowcolour", Loc.Get("appearance.shadowcolour", "Shadow colour"), null,
            this.config.ShadowColor, ref changed, enabled);

        this.config.ShadowOffsetX = Slider(
            "##rox-shadowacross",
            Loc.Get("appearance.shadowacross", "Shadow across"),
            Loc.Get("appearance.shadowacross.tooltip", "Negative moves the shadow to the left."),
            this.config.ShadowOffsetX, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed, enabled: enabled);

        this.config.ShadowOffsetY = Slider(
            "##rox-shadowdown",
            Loc.Get("appearance.shadowdown", "Shadow down"),
            Loc.Get("appearance.shadowdown.tooltip", "Negative lifts the shadow above the text."),
            this.config.ShadowOffsetY, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed, enabled: enabled);

        this.config.ShadowSoftness = Slider(
            "##rox-shadowspread",
            Loc.Get("appearance.shadowspread", "Shadow spread"),
            Loc.Get(
                "appearance.shadowspread.tooltip",
                "Fattens the shadow outwards. Zero keeps it the same shape as the\n" +
                "letters; higher values thicken it into a halo behind them."),
            this.config.ShadowSoftness, 0f, 6f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed, enabled: enabled);
    }
}
