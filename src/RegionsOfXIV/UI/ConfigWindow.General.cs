using System;
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

        // See the Effects tab: turning the decode on or off changes what the
        // reveal does, and a sample already on screen has finished revealing.
        var restart = false;

        var verticalPosition = this.config.VerticalPosition;
        if (ImGui.SliderFloat("Vertical position", ref verticalPosition, 0f, 100f, "%.0f%%"))
        {
            this.config.VerticalPosition = verticalPosition;
            changed = true;
        }

        var horizontalPosition = this.config.HorizontalPosition;
        if (ImGui.SliderFloat("Horizontal position", ref horizontalPosition, 0f, 100f, "%.0f%%"))
        {
            this.config.HorizontalPosition = horizontalPosition;
            changed = true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "The text is centred on this point, so 50% is the middle of the screen.\n" +
                "A long place name set near either end will reach past it.");
        }

        var fontSize = this.config.DisplayFontSize;
        if (ImGui.SliderFloat("Font size", ref fontSize, 24f, 140f, "%.0f px"))
        {
            this.config.DisplayFontSize = fontSize;
            changed = true;
        }

        using (var combo = ImRaii.Combo("Font", Label(this.config.DisplayFont)))
        {
            if (combo)
            {
                foreach (var choice in Enum.GetValues<DisplayFontChoice>())
                {
                    if (!ImGui.Selectable(Label(choice), choice == this.config.DisplayFont))
                        continue;

                    this.config.DisplayFont = choice;
                    changed = true;
                }
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Noto Sans CJK — shipped with Dalamud. Vector rather than bitmap, so it is\n" +
                "                crisp at any size, and it carries every language.\n\n" +
                "The game's own faces look more like FFXIV, but are bitmaps baked at fixed\n" +
                "sizes, so each softens past its own ceiling:\n\n" +
                "Trump Gothic — the narrow title face. Latin only. Sharp to ~91 px.\n" +
                "Jupiter — the serif face. Latin only. Sharp to ~61 px.\n" +
                "Axis — the general UI face. The only game font with Japanese glyphs,\n" +
                "       but sharp only to 48 px.");
        }

        // Latin-only faces do not merely look wrong on a Japanese client, they render
        // nothing at all. That is a different severity from the softening warning
        // below, so it gets its own colour and goes first.
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
        if (this.config.DisplayFontSize > ceiling)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.78f, 0.35f, 1f)))
            {
                ImGui.TextWrapped(
                    $"This font has no bitmap above {ceiling:F0} px, so at " +
                    $"{this.config.DisplayFontSize:F0} px it is being upscaled and will look soft. " +
                    "Lower the size, or switch to Noto Sans CJK, which stays sharp at any size.");
            }
        }

        var letterSpacing = this.config.LetterSpacing;
        if (ImGui.SliderFloat("Letter spacing", ref letterSpacing, 0f, 30f, "%.0f%%"))
        {
            this.config.LetterSpacing = letterSpacing;
            changed = true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Extra space between letters, as a percentage of the font size, so it keeps\n" +
                "its proportions when the size changes and the header gets its own share.\n" +
                "Wide spacing is much of what gives the Guild Wars 2 original its look.");
        }

        var uppercase = this.config.UppercaseText;
        if (ImGui.Checkbox("Uppercase", ref uppercase))
        {
            this.config.UppercaseText = uppercase;
            changed = true;
        }

        var decode = this.config.DecodeEffectEnabled;
        if (ImGui.Checkbox("Decode from Eorzean script", ref decode))
        {
            this.config.DecodeEffectEnabled = decode;
            restart = true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Requires a bundled Eorzean font. Latin text only.\n\n" +
                "Runs after the motion on the Effects tab rather than alongside it:\n" +
                "the line arrives in Eorzean, lands, then resolves. Turned off, it\n" +
                "arrives already readable. Presets leave this alone, so switching it\n" +
                "off here switches it off for all of them.");
        }

        var textColor = this.config.TextColor;
        if (ImGui.ColorEdit4("Text colour", ref textColor, ImGuiColorEditFlags.NoInputs))
        {
            this.config.TextColor = textColor;
            changed = true;
        }

        var headerColor = this.config.HeaderColor;
        if (ImGui.ColorEdit4("Header colour", ref headerColor, ImGuiColorEditFlags.NoInputs))
        {
            this.config.HeaderColor = headerColor;
            changed = true;
        }

        var strokeColor = this.config.StrokeColor;
        if (ImGui.ColorEdit4("Outline colour", ref strokeColor, ImGuiColorEditFlags.NoInputs))
        {
            this.config.StrokeColor = strokeColor;
            changed = true;
        }

        var strokeThickness = this.config.StrokeThickness;
        if (ImGui.SliderFloat("Outline thickness", ref strokeThickness, 0f, 4f, "%.1f px"))
        {
            this.config.StrokeThickness = strokeThickness;
            changed = true;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Zero turns the outline off.");

        var underline = this.config.UnderlineHeader;
        if (ImGui.Checkbox("Underline header", ref underline))
        {
            this.config.UnderlineHeader = underline;
            changed = true;
        }

        var overlap = this.config.OverlapHeader;
        if (ImGui.Checkbox("Overlap header", ref overlap))
        {
            this.config.OverlapHeader = overlap;
            changed = true;
        }

        ImGui.Separator();

        var hideInCombat = this.config.HideInCombat;
        if (ImGui.Checkbox("Hide during combat", ref hideInCombat))
        {
            this.config.HideInCombat = hideInCombat;
            changed = true;
        }

        var hideInDuty = this.config.HideInDuty;
        if (ImGui.Checkbox("Hide inside duties", ref hideInDuty))
        {
            this.config.HideInDuty = hideInDuty;
            changed = true;
        }

        var hideWhileTravelling = this.config.HideWhileTravellingFast;
        if (ImGui.Checkbox("Skip sub-areas while travelling quickly", ref hideWhileTravelling))
        {
            this.config.HideWhileTravellingFast = hideWhileTravelling;
            changed = true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Only affects sub-areas, and only above a speed no ground travel reaches,\n" +
                "so it comes into play when flying. Zone and area changes are always\n" +
                "announced however fast you are moving.");
        }

        ImGui.Separator();

        if (ImGui.Button("Preview"))
            this.actions.Preview(SampleHeader, SampleText);

        if (changed || restart)
        {
            // Size and family changes need new atlas handles; doing it here keeps
            // the rebuild off the draw path.
            this.actions.RebuildFonts();
            this.config.Save();

            // After the rebuild, so the sample is drawn with the size and family
            // just chosen rather than the previous one. Everything on this tab
            // changes how a notification looks, so every change earns a preview —
            // that is the whole point of the tab.
            if (restart)
                this.actions.Preview(SampleHeader, SampleText);
            else
                this.actions.LivePreview(SampleHeader, SampleText);
        }
    }
}
