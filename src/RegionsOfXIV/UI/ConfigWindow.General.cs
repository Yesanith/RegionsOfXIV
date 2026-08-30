using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private void DrawGeneralTab()
    {
        using var tab = ImRaii.TabItem(Loc.Label("general.tab", "General"));
        if (!tab) return;

        // Two flags, because they lead to different previews. Most settings only need the
        // notification redrawn with the new value, which LivePreview does without restarting it.
        // The decode toggle changes how the line arrives, so it has to be played from the top to
        // be seen at all.
        var changed = false;
        var restart = false;
        var refont = false;

        DrawLanguage(ref changed);

        ImGui.Separator();

        DrawPlacement(ref changed);
        DrawLettering(ref changed, ref restart, ref refont);

        ImGui.Separator();

        DrawColors(ref changed);
        DrawShadow(ref changed);

        ImGui.Separator();

        DrawHeaderShape(ref changed);

        ImGui.Separator();

        DrawQuietRules(ref changed);

        ImGui.Separator();

        if (ImGui.Button(Loc.Label("general.preview", "Preview")))
            this.actions.Preview(Sample);

        if (!changed && !restart)
            return;

        MarkUnsaved();

        // Before the replay, not after: the Eorzean face only exists while the decode effect is on,
        // so previewing against the old atlas would show the state before the toggle.
        if (refont)
            this.actions.RebuildFonts();

        if (restart)
            this.actions.Preview(Sample);
        else
            this.actions.LivePreview(Sample);
    }

    // Each language is named in its own language rather than in the one currently showing, so
    // somebody who has landed in a language they cannot read can still find their way out.
    private void DrawLanguage(ref bool changed)
    {
        using (var combo = ImRaii.Combo(
                   Loc.Label("general.language", "Language"), LanguageName(this.config.Language)))
        {
            if (combo)
            {
                foreach (var option in LanguageOptions)
                {
                    // The code is the identity, not the name -- two languages whose native names
                    // happened to render alike would otherwise be one entry.
                    if (!ImGui.Selectable(
                            $"{LanguageName(option)}###language-{option ?? "dalamud"}",
                            option == this.config.Language))
                        continue;

                    this.config.Language = option;
                    changed = true;
                    this.actions.ReloadLanguage();
                }
            }
        }

        UiText.Tooltip(Loc.Get(
            "general.language.tooltip",
            "Follow Dalamud takes whichever language Dalamud itself is set to, and\n" +
            "changes with it.\n\n" +
            "Only this window is affected. Place and weather names come from the game\n" +
            "and stay in whatever language your client is running in."));
    }

    // Loc.Shipped never contains "en" -- English is the compiled-in fallback rather than a
    // bundled file -- so listing it here cannot double it up.
    private static string?[] LanguageOptions => [null, "en", .. Loc.Shipped];

    private static string LanguageName(string? code)
    {
        if (code is null)
            return Loc.Get("general.language.follow", "Follow Dalamud");

        try
        {
            return CultureInfo.GetCultureInfo(code).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    private void DrawPlacement(ref bool changed)
    {
        // "%%" is an escaped per-cent sign rather than a word, so there is nothing in this format
        // for a translator to change and it stays whole.
        this.config.VerticalPosition = Slider(
            Loc.Label("general.vertical", "Vertical position"),
            this.config.VerticalPosition, 0f, 100f, "%.0f%%", ref changed);

        this.config.HorizontalPosition = Slider(
            Loc.Label("general.horizontal", "Horizontal position"),
            this.config.HorizontalPosition, 0f, 100f, "%.0f%%", ref changed);
        UiText.Tooltip(Loc.Get(
            "general.horizontal.tooltip",
            "The text is centred on this point, so 50% is the middle of the screen.\n" +
            "A long place name set near either end will reach past it."));
    }

    private void DrawLettering(ref bool changed, ref bool restart, ref bool refont)
    {
        this.config.LetterSpacing = Slider(
            Loc.Label("general.letterspacing", "Letter spacing"),
            this.config.LetterSpacing, 0f, 30f, "%.0f%%", ref changed);
        UiText.Tooltip(Loc.Get(
            "general.letterspacing.tooltip",
            "Extra space between letters, as a percentage of the font size, so it keeps\n" +
            "its proportions when the size changes and the header gets its own share.\n" +
            "Wide spacing is much of what gives the Guild Wars 2 original its look."));

        this.config.UppercaseText = Checkbox(
            Loc.Label("general.uppercase", "Uppercase"), this.config.UppercaseText, ref changed);

        var wasDecoding = this.config.DecodeEffectEnabled;

        this.config.DecodeEffectEnabled = Checkbox(
            Loc.Label("general.decode", "Decode from Eorzean script"),
            this.config.DecodeEffectEnabled, ref restart);

        // Asked explicitly rather than reading restart, which happens to mean the same thing today
        // only because this is the one control on the tab that sets it.
        if (this.config.DecodeEffectEnabled != wasDecoding)
            refont = true;

        UiText.Tooltip(Loc.Get(
            "general.decode.tooltip",
            "Requires a bundled Eorzean font. Latin text only.\n\n" +
            "Runs after the motion on the Effects tab rather than alongside it:\n" +
            "the line arrives in Eorzean, lands, then resolves. Turned off, it\n" +
            "arrives already readable. Presets leave this alone, so switching it\n" +
            "off here switches it off for all of them."));
    }

    private void DrawColors(ref bool changed)
    {
        this.config.TextColor = ColorPicker(
            Loc.Label("general.textcolour", "Text colour"), this.config.TextColor, ref changed);

        this.config.HeaderColor = ColorPicker(
            Loc.Label("general.headercolour", "Header colour"), this.config.HeaderColor, ref changed);

        this.config.StrokeColor = ColorPicker(
            Loc.Label("general.outlinecolour", "Outline colour"), this.config.StrokeColor, ref changed);

        this.config.SeparateLineColors = Checkbox(
            Loc.Label("general.separatecolours", "Colour each line separately"),
            this.config.SeparateLineColors, ref changed);
        UiText.Tooltip(Loc.Get(
            "general.separatecolours.tooltip",
            "Off, the weather line follows the header, and one outline colour\n" +
            "covers all three lines.\n\n" +
            "On, the weather line and the outlines take the colours below, so one\n" +
            "line can be pushed towards the background without touching the others."));

        using (ImRaii.Disabled(!this.config.SeparateLineColors))
        {
            this.config.WeatherColor = ColorPicker(
                Loc.Label("general.weathercolour", "Weather colour"),
                this.config.WeatherColor, ref changed);

            this.config.HeaderStrokeColor = ColorPicker(
                Loc.Label("general.headeroutlinecolour", "Header outline colour"),
                this.config.HeaderStrokeColor, ref changed);

            this.config.WeatherStrokeColor = ColorPicker(
                Loc.Label("general.weatheroutlinecolour", "Weather outline colour"),
                this.config.WeatherStrokeColor, ref changed);
        }

        this.config.StrokeThickness = Slider(
            Loc.Label("general.outlinethickness", "Outline thickness"),
            this.config.StrokeThickness, 0f, 4f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get("general.outlinethickness.tooltip", "Zero turns the outline off."));
    }

    private void DrawShadow(ref bool changed)
    {
        this.config.ShadowEnabled = Checkbox(
            Loc.Label("general.shadow", "Drop shadow"), this.config.ShadowEnabled, ref changed);
        UiText.Tooltip(Loc.Get(
            "general.shadow.tooltip",
            "A second copy of every line, offset behind it. Sits under the outline, so\n" +
            "the two can be used together: a tight outline for legibility and a soft\n" +
            "shadow for depth.\n\n" +
            "One shadow covers all three lines; it does not follow the separate colours."));

        using var disabled = ImRaii.Disabled(!this.config.ShadowEnabled);

        this.config.ShadowColor = ColorPicker(
            Loc.Label("general.shadowcolour", "Shadow colour"), this.config.ShadowColor, ref changed);

        this.config.ShadowOffsetX = Slider(
            Loc.Label("general.shadowacross", "Shadow across"),
            this.config.ShadowOffsetX, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get(
            "general.shadowacross.tooltip", "Negative moves the shadow to the left."));

        this.config.ShadowOffsetY = Slider(
            Loc.Label("general.shadowdown", "Shadow down"),
            this.config.ShadowOffsetY, -20f, 20f,
            "%.0f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get(
            "general.shadowdown.tooltip", "Negative lifts the shadow above the text."));

        this.config.ShadowSoftness = Slider(
            Loc.Label("general.shadowspread", "Shadow spread"),
            this.config.ShadowSoftness, 0f, 6f,
            "%.1f " + Loc.Unit("units.px", "px"), ref changed);
        UiText.Tooltip(Loc.Get(
            "general.shadowspread.tooltip",
            "Fattens the shadow outwards. Zero keeps it the same shape as the\n" +
            "letters; higher values thicken it into a halo behind them."));
    }

    private void DrawHeaderShape(ref bool changed)
    {
        this.config.IncludeParentTierAsHeader = Checkbox(
            Loc.Label("general.showheader", "Show header"),
            this.config.IncludeParentTierAsHeader, ref changed);
        UiText.Tooltip(Loc.Get(
            "general.showheader.tooltip",
            "The smaller line above the name, giving where the place sits: the region\n" +
            "above a zone, the area above a sub-area.\n\n" +
            "Turned off, only the name itself is shown. The weather line, if you have\n" +
            "it on, is unaffected, as are the two settings below, which still apply\n" +
            "to it."));

        this.config.UnderlineHeader = Checkbox(
            Loc.Label("general.underlineheader", "Underline header"),
            this.config.UnderlineHeader, ref changed);

        this.config.HeaderGap = Slider(
            Loc.Label("general.headergap", "Header gap"),
            this.config.HeaderGap, 0.5f, 2.5f,
            "%.2f " + Loc.Unit("units.lines", "lines"), ref changed);
        UiText.Tooltip(Loc.Get(
            "general.headergap.tooltip",
            "How far the name sits below the header, measured in lines of header text.\n\n" +
            "Low values pull the two together until they almost touch, which is the\n" +
            "tighter look the plugin has always shipped with. Higher values open the\n" +
            "gap up and give each line room to breathe."));
    }

    private void DrawQuietRules(ref bool changed)
    {
        this.config.HideInCombat = Checkbox(
            Loc.Label("general.hidecombat", "Hide during combat"),
            this.config.HideInCombat, ref changed);

        this.config.HideInDuty = Checkbox(
            Loc.Label("general.hideduty", "Hide inside duties"),
            this.config.HideInDuty, ref changed);

        this.config.HideWhileTravellingFast = Checkbox(
            Loc.Label("general.skipfast", "Skip sub-areas while travelling quickly"),
            this.config.HideWhileTravellingFast, ref changed);
        UiText.Tooltip(Loc.Get(
            "general.skipfast.tooltip",
            "Only affects sub-areas, and only above a speed no ground travel reaches,\n" +
            "so it comes into play when flying. Zone and area changes are always\n" +
            "announced however fast you are moving."));
    }
}
