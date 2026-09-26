using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // Where and how the lines are set on the left; what they are coloured and lit with on the
    // right. The rows that belong to a switch appear under it only while it is on.
    private void DrawAppearancePage()
    {
        PageHeader.Draw(
            Loc.Get("appearance.tab", "Appearance"),
            Loc.Get(
                "appearance.intro",
                "Where the notification sits, how it is lettered, and the colours, outline and "
                + "shadow it is drawn with. The preview above follows every change."));

        var changed = false;

        this.columns.Begin(2);
        this.columns.Next();

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

        using (SettingsGroup.Begin(Loc.Get("appearance.group.backing", "Backing")))
        {
            DrawBacking(ref changed);
        }

        this.columns.Next();

        using (SettingsGroup.Begin(Loc.Get("appearance.group.colours", "Colours")))
        {
            DrawFillColors(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.palette", "Colour play")))
        {
            DrawPalette(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.outline", "Outline")))
        {
            DrawOutline(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.glow", "Glow")))
        {
            DrawGlow(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("appearance.group.shadow", "Drop shadow")))
        {
            DrawShadow(ref changed);
        }

        this.columns.End();

        if (!changed)
        {
            return;
        }

        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    private void DrawPlacement(ref bool changed)
    {
        var scale = ImGuiHelpers.GlobalScale;

        SettingsRow.Block(
            Loc.Get("appearance.position", "Position on screen"),
            Loc.Get(
                "appearance.position.tooltip",
                "Drag the marker to move the notification. The two sliders below set the\n" +
                "same thing exactly. The text is centred on this point, so 50% across is\n" +
                "the middle of the screen, and a long place name set near either end\n" +
                "will reach past it."));

        var inner = SettingsGroup.InnerWidth();
        var padWidth = MathF.Min(Layout.PositionPadWidth * scale, inner);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((inner - padWidth) * 0.5f));

        var horizontal = this.config.HorizontalPosition;
        var vertical = this.config.VerticalPosition;
        if (PositionPad.Draw("##rox-position-pad", ref horizontal, ref vertical, padWidth))
        {
            this.config.HorizontalPosition = horizontal;
            this.config.VerticalPosition = vertical;
            changed = true;
        }

        SettingsRow.EndBlock();

        // "%%" is an escaped per-cent sign rather than a word, so there is nothing in this format
        // for a translator to change and it stays whole.
        this.config.HorizontalPosition = Slider(
            "##rox-horizontal", Loc.Get("appearance.horizontal", "Horizontal position"), null,
            this.config.HorizontalPosition, 0f, 100f, "%.0f%%", ref changed);

        this.config.VerticalPosition = Slider(
            "##rox-vertical", Loc.Get("appearance.vertical", "Vertical position"), null,
            this.config.VerticalPosition, 0f, 100f, "%.0f%%", ref changed);
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

        // Shown whether or not the header is, because both also govern the weather line.
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

    private void DrawBacking(ref bool changed)
    {
        this.config.Backing = Choice(
            "##rox-backing",
            Loc.Get("appearance.backing", "Behind the text"),
            Loc.Get(
                "appearance.backing.tooltip",
                "Something dark behind the lines, for a name that lands on a bright sky.\n\n" +
                "A band is the width of the text and fades at its ends. A strip runs the\n" +
                "whole width of the screen, the way the original does it."),
            this.config.Backing, BackingLabels, ref changed);

        using var rows = Reveal("##rox-backing-rows", this.config.Backing != BackingStyle.None);
        if (rows is null)
        {
            return;
        }

        this.config.BackingColor = Colour(
            "##rox-backingcolour", Loc.Get("appearance.backingcolour", "Backing colour"), null,
            this.config.BackingColor, ref changed);
    }

    // SeparateLineColors gates one picker here and two in DrawOutline below: the switch belongs
    // with the pickers it enables, and those sit either side of the split between fill and outline.
    private void DrawFillColors(ref bool changed)
    {
        this.config.TextColor = Colour(
            "##rox-textcolour", Loc.Get("appearance.textcolour", "Text colour"), null,
            this.config.TextColor, ref changed);

        this.config.TextGradientEnabled = Toggle(
            "##rox-gradient",
            Loc.Get("appearance.gradient", "Fade the name to a second colour"),
            Loc.Get(
                "appearance.gradient.tooltip",
                "The place name shades from its text colour at the top of each letter to\n" +
                "this one at the foot. The header and weather keep a flat colour."),
            this.config.TextGradientEnabled, ref changed);

        using (var rows = Reveal("##rox-gradient-rows", this.config.TextGradientEnabled))
        {
            if (rows is not null)
            {
                this.config.TextGradientColor = Colour(
                    "##rox-gradientcolour", Loc.Get("appearance.gradientcolour", "Lower colour"), null,
                    this.config.TextGradientColor, ref changed);
            }
        }

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

        using var weatherRows = Reveal("##rox-weather-colour-rows", this.config.SeparateLineColors);
        if (weatherRows is null)
        {
            return;
        }

        this.config.WeatherColor = Colour(
            "##rox-weathercolour", Loc.Get("appearance.weathercolour", "Weather colour"), null,
            this.config.WeatherColor, ref changed);
    }

    // A run of colours over the letters of the name, in place of one text colour. The name only,
    // so the header stays readable above whatever the name is doing.
    private void DrawPalette(ref bool changed)
    {
        this.config.Palette = Choice(
            "##rox-palette",
            Loc.Get("appearance.palette", "Palette"),
            Loc.Get(
                "appearance.palette.tooltip",
                "Letters the place name in a run of colours instead of one. The text colour\n" +
                "above still sets the outline's contrast and is what a preset carries."),
            this.config.Palette, PaletteLabels, ref changed);

        using var rows = Reveal("##rox-palette-rows", this.config.Palette != TextPalette.None);
        if (rows is null)
        {
            return;
        }

        this.config.PaletteMotion = Choice(
            "##rox-palette-motion",
            Loc.Get("appearance.palettemotion", "Movement"), null,
            this.config.PaletteMotion, PaletteMotionLabels, ref changed);

        this.config.PaletteSpeed = Slider(
            "##rox-palette-speed",
            Loc.Get("appearance.palettespeed", "Speed"), null,
            this.config.PaletteSpeed, 0.2f, 3f,
            "%.1f" + Loc.Unit("units.times", "x"), ref changed, enabled: this.config.PaletteMotion != PaletteMotion.Static);
    }

    private void DrawOutline(ref bool changed)
    {
        this.config.StrokeColor = Colour(
            "##rox-outlinecolour", Loc.Get("appearance.outlinecolour", "Outline colour"), null,
            this.config.StrokeColor, ref changed);

        using (var rows = Reveal("##rox-outline-colour-rows", this.config.SeparateLineColors))
        {
            if (rows is not null)
            {
                this.config.HeaderStrokeColor = Colour(
                    "##rox-headeroutlinecolour", Loc.Get("appearance.headeroutlinecolour", "Header outline colour"), null,
                    this.config.HeaderStrokeColor, ref changed);

                this.config.WeatherStrokeColor = Colour(
                    "##rox-weatheroutlinecolour", Loc.Get("appearance.weatheroutlinecolour", "Weather outline colour"), null,
                    this.config.WeatherStrokeColor, ref changed);
            }
        }

        this.config.StrokeThickness = Slider(
            "##rox-outlinethickness",
            Loc.Get("appearance.outlinethickness", "Outline thickness"),
            Loc.Get("appearance.outlinethickness.tooltip", "Zero turns the outline off."),
            this.config.StrokeThickness, 0f, 4f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed);
    }

    private void DrawGlow(ref bool changed)
    {
        this.config.GlowEnabled = Toggle(
            "##rox-glow",
            Loc.Get("appearance.glow", "Glow"),
            Loc.Get(
                "appearance.glow.tooltip",
                "A soft halo behind every line, under the outline. Costs more to draw than\n" +
                "the outline does, which matters only at very large sizes."),
            this.config.GlowEnabled, ref changed);

        using var rows = Reveal("##rox-glow-rows", this.config.GlowEnabled);
        if (rows is null)
        {
            return;
        }

        this.config.GlowColor = Colour(
            "##rox-glowcolour", Loc.Get("appearance.glowcolour", "Glow colour"), null,
            this.config.GlowColor, ref changed);

        this.config.GlowSpread = Slider(
            "##rox-glowspread",
            Loc.Get("appearance.glowspread", "Glow spread"), null,
            this.config.GlowSpread, 1f, 14f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed);
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

        using var rows = Reveal("##rox-shadow-rows", this.config.ShadowEnabled);
        if (rows is null)
        {
            return;
        }

        this.config.ShadowColor = Colour(
            "##rox-shadowcolour", Loc.Get("appearance.shadowcolour", "Shadow colour"), null,
            this.config.ShadowColor, ref changed);

        this.config.ShadowOffsetX = Slider(
            "##rox-shadowacross",
            Loc.Get("appearance.shadowacross", "Shadow across"),
            Loc.Get("appearance.shadowacross.tooltip", "Negative moves the shadow to the left."),
            this.config.ShadowOffsetX, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed);

        this.config.ShadowOffsetY = Slider(
            "##rox-shadowdown",
            Loc.Get("appearance.shadowdown", "Shadow down"),
            Loc.Get("appearance.shadowdown.tooltip", "Negative lifts the shadow above the text."),
            this.config.ShadowOffsetY, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed);

        this.config.ShadowSoftness = Slider(
            "##rox-shadowspread",
            Loc.Get("appearance.shadowspread", "Shadow spread"),
            Loc.Get(
                "appearance.shadowspread.tooltip",
                "Fattens the shadow outwards. Zero keeps it the same shape as the\n" +
                "letters; higher values thicken it into a halo behind them."),
            this.config.ShadowSoftness, 0f, 6f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed);
    }
}
