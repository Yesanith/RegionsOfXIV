using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Where the notification sits and what it looks like: position, face, size,
// spacing, casing, colours. The decode switch lives here too, because it is a
// decision about the plugin rather than about a look — see the Effects tab.
internal sealed partial class ConfigWindow
{
    private void DrawGeneralTab()
    {
        using var tab = ImRaii.TabItem("General");
        if (!tab) return;

        var changed = false;

        changed |= Slider("Vertical position",
            () => this.config.VerticalPosition, v => this.config.VerticalPosition = v, 0f, 100f, "%.0f%%");

        changed |= Slider("Horizontal position",
            () => this.config.HorizontalPosition, v => this.config.HorizontalPosition = v, 0f, 100f, "%.0f%%");
        Tooltip(
            "The text is centred on this point, so 50% is the middle of the screen.\n" +
            "A long place name set near either end will reach past it.");

        changed |= Slider("Font size",
            () => this.config.DisplayFontSize, v => this.config.DisplayFontSize = v, 24f, 140f, "%.0f px");

        changed |= Choice("Font",
            () => this.config.DisplayFont, v => this.config.DisplayFont = v, Label);
        Tooltip(
            "Noto Sans CJK — shipped with Dalamud. Vector rather than bitmap, so it is\n" +
            "                crisp at any size, and it carries every language.\n\n" +
            "The game's own faces look more like FFXIV, but are bitmaps baked at fixed\n" +
            "sizes, so each softens past its own ceiling:\n\n" +
            "Trump Gothic — the narrow title face. Latin only. Sharp to ~91 px.\n" +
            "Jupiter — the serif face. Latin only. Sharp to ~61 px.\n" +
            "Axis — the general UI face. The only game font with Japanese glyphs,\n" +
            "       but sharp only to 48 px.");

        DrawFontWarnings();

        changed |= Slider("Letter spacing",
            () => this.config.LetterSpacing, v => this.config.LetterSpacing = v, 0f, 30f, "%.0f%%");
        Tooltip(
            "Extra space between letters, as a percentage of the font size, so it keeps\n" +
            "its proportions when the size changes and the header gets its own share.\n" +
            "Wide spacing is much of what gives the Guild Wars 2 original its look.");

        changed |= Checkbox("Uppercase",
            () => this.config.UppercaseText, v => this.config.UppercaseText = v);

        // Its own flag, not `changed`: turning the decode on or off changes what
        // the reveal does, and a sample already on screen has finished revealing.
        // Only a fresh notification shows the difference.
        var restart = Checkbox("Decode from Eorzean script",
            () => this.config.DecodeEffectEnabled, v => this.config.DecodeEffectEnabled = v);
        Tooltip(
            "Requires a bundled Eorzean font. Latin text only.\n\n" +
            "Runs after the motion on the Effects tab rather than alongside it:\n" +
            "the line arrives in Eorzean, lands, then resolves. Turned off, it\n" +
            "arrives already readable. Presets leave this alone, so switching it\n" +
            "off here switches it off for all of them.");

        changed |= ColorPicker("Text colour",
            () => this.config.TextColor, v => this.config.TextColor = v);

        changed |= ColorPicker("Header colour",
            () => this.config.HeaderColor, v => this.config.HeaderColor = v);

        changed |= ColorPicker("Outline colour",
            () => this.config.StrokeColor, v => this.config.StrokeColor = v);

        changed |= Slider("Outline thickness",
            () => this.config.StrokeThickness, v => this.config.StrokeThickness = v, 0f, 4f, "%.1f px");
        Tooltip("Zero turns the outline off.");

        changed |= Checkbox("Underline header",
            () => this.config.UnderlineHeader, v => this.config.UnderlineHeader = v);

        changed |= Checkbox("Overlap header",
            () => this.config.OverlapHeader, v => this.config.OverlapHeader = v);

        ImGui.Separator();

        changed |= Checkbox("Hide during combat",
            () => this.config.HideInCombat, v => this.config.HideInCombat = v);

        changed |= Checkbox("Hide inside duties",
            () => this.config.HideInDuty, v => this.config.HideInDuty = v);

        changed |= Checkbox("Skip sub-areas while travelling quickly",
            () => this.config.HideWhileTravellingFast, v => this.config.HideWhileTravellingFast = v);
        Tooltip(
            "Only affects sub-areas, and only above a speed no ground travel reaches,\n" +
            "so it comes into play when flying. Zone and area changes are always\n" +
            "announced however fast you are moving.");

        ImGui.Separator();

        if (ImGui.Button("Preview"))
            this.actions.Preview(SampleHeader, SampleText);

        if (!changed && !restart)
            return;

        // Size and family changes need new atlas handles; doing it here keeps the
        // rebuild off the draw path.
        this.actions.RebuildFonts();
        this.config.Save();

        // After the rebuild, so the sample is drawn with the size and family just
        // chosen rather than the previous one. Everything on this tab changes how a
        // notification looks, so every change earns a preview — that is the whole
        // point of the tab.
        if (restart)
            this.actions.Preview(SampleHeader, SampleText);
        else
            this.actions.LivePreview(SampleHeader, SampleText);
    }

    // Two warnings about the chosen face, in order of severity.
    private void DrawFontWarnings()
    {
        // Latin-only faces do not merely look wrong on a Japanese client, they
        // render nothing at all. That is a different severity from the softening
        // warning below, so it gets its own colour and goes first.
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

        // Bitmap faces soften past their ceiling. Noto reports an infinite ceiling
        // rather than being special-cased here, so this simply never fires for it.
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
