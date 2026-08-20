using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private void DrawGeneralTab()
    {
        using var tab = ImRaii.TabItem("General");
        if (!tab) return;

        var changed = false;

        this.config.VerticalPosition = Slider(
            "Vertical position", this.config.VerticalPosition, 0f, 100f, "%.0f%%", ref changed);

        this.config.HorizontalPosition = Slider(
            "Horizontal position", this.config.HorizontalPosition, 0f, 100f, "%.0f%%", ref changed);
        Tooltip(
            "The text is centred on this point, so 50% is the middle of the screen.\n" +
            "A long place name set near either end will reach past it.");

        this.config.DisplayFontSize = Slider(
            "Font size", this.config.DisplayFontSize, 24f, 140f, "%.0f px", ref changed);

        this.config.DisplayFont = Choice(
            "Font", this.config.DisplayFont, Label, ref changed);
        Tooltip(
            "Noto Sans CJK is vector — sharp at any size, and it covers every language.\n\n"
            + "The game's own faces suit FFXIV better, but each is a bitmap with a ceiling:\n"
            + "Trump Gothic — Latin only, to 91 px.\n"
            + "Jupiter — Latin only, to 61 px.\n"
            + "Axis — to 48 px, the only one with Japanese glyphs.");

        DrawFontWarnings();

        this.config.LetterSpacing = Slider(
            "Letter spacing", this.config.LetterSpacing, 0f, 30f, "%.0f%%", ref changed);
        Tooltip(
            "Extra space between letters, as a percentage of the font size, so it keeps\n" +
            "its proportions when the size changes and the header gets its own share.\n" +
            "Wide spacing is much of what gives the Guild Wars 2 original its look.");

        this.config.UppercaseText = Checkbox(
            "Uppercase", this.config.UppercaseText, ref changed);

        var restart = false;
        this.config.DecodeEffectEnabled = Checkbox(
            "Decode from Eorzean script", this.config.DecodeEffectEnabled, ref restart);
        Tooltip(
            "Requires a bundled Eorzean font. Latin text only.\n\n" +
            "Runs after the motion on the Effects tab rather than alongside it:\n" +
            "the line arrives in Eorzean, lands, then resolves. Turned off, it\n" +
            "arrives already readable. Presets leave this alone, so switching it\n" +
            "off here switches it off for all of them.");

        this.config.TextColor = ColorPicker(
            "Text colour", this.config.TextColor, ref changed);

        this.config.HeaderColor = ColorPicker(
            "Header colour", this.config.HeaderColor, ref changed);

        this.config.StrokeColor = ColorPicker(
            "Outline colour", this.config.StrokeColor, ref changed);

        this.config.SeparateLineColors = Checkbox(
            "Colour each line separately", this.config.SeparateLineColors, ref changed);

        Tooltip(
            "Off, the weather line follows the header, and one outline colour\n" +
            "covers all three lines.\n\n" +
            "On, the weather line and the outlines take the colours below. Making\n" +
            "a line transparent in both its colour and its outline is how you fade\n" +
            "that one line back without touching the others.");

        using (ImRaii.Disabled(!this.config.SeparateLineColors))
        {
            this.config.WeatherColor = ColorPicker(
            "Weather colour", this.config.WeatherColor, ref changed);

            this.config.HeaderStrokeColor = ColorPicker(
            "Header outline colour", this.config.HeaderStrokeColor, ref changed);

            this.config.WeatherStrokeColor = ColorPicker(
            "Weather outline colour", this.config.WeatherStrokeColor, ref changed);
        }

        this.config.StrokeThickness = Slider(
            "Outline thickness", this.config.StrokeThickness, 0f, 4f, "%.1f px", ref changed);
        Tooltip("Zero turns the outline off.");

        this.config.IncludeParentTierAsHeader = Checkbox(
            "Show header", this.config.IncludeParentTierAsHeader, ref changed);

        Tooltip(
            "The smaller line above the name, giving where the place sits: the region\n" +
            "above a zone, the area above a sub-area.\n\n" +
            "Turned off, only the name itself is shown. The weather line, if you have\n" +
            "it on, is unaffected — as are the two settings below, which still apply\n" +
            "to it.");

        this.config.UnderlineHeader = Checkbox(
            "Underline header", this.config.UnderlineHeader, ref changed);

        this.config.OverlapHeader = Checkbox(
            "Overlap header", this.config.OverlapHeader, ref changed);

        ImGui.Separator();

        this.config.HideInCombat = Checkbox(
            "Hide during combat", this.config.HideInCombat, ref changed);

        this.config.HideInDuty = Checkbox(
            "Hide inside duties", this.config.HideInDuty, ref changed);

        this.config.HideWhileTravellingFast = Checkbox(
            "Skip sub-areas while travelling quickly", this.config.HideWhileTravellingFast, ref changed);
        Tooltip(
            "Only affects sub-areas, and only above a speed no ground travel reaches,\n" +
            "so it comes into play when flying. Zone and area changes are always\n" +
            "announced however fast you are moving.");

        ImGui.Separator();

        if (ImGui.Button("Preview"))
            this.actions.Preview(Sample);

        if (!changed && !restart)
            return;

        this.actions.RebuildFonts();
        MarkUnsaved();

        if (restart)
            this.actions.Preview(Sample);
        else
            this.actions.LivePreview(Sample);
    }

    private void DrawFontWarnings()
    {
        if (FontService.IsLatinOnly(this.config.DisplayFont) &&
            Plugin.ClientState.ClientLanguage == ClientLanguage.Japanese)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.35f, 0.35f, 1f)))
            {
                ImGui.TextWrapped(
                    $"{Label(this.config.DisplayFont)} has no Japanese glyphs. On this client that " +
                    "means place names will render as blank boxes, not just look soft. " +
                    "Choose Axis or Noto Sans CJK instead.");
            }
        }

        var ceiling = FontService.NativeCeilingPx(this.config.DisplayFont);
        if (this.config.DisplayFontSize <= ceiling)
            return;

        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.78f, 0.35f, 1f)))
        {
            ImGui.TextWrapped(
                $"This font has no bitmap above {ceiling:F0} px, so at " +
                $"{this.config.DisplayFontSize:F0} px it is being upscaled and will look soft. " +
                "Lower the size, or switch to Noto Sans CJK, which stays sharp at any size.");
        }
    }
}
