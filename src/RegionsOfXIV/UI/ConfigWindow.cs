using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Side effects the window needs to trigger, kept together so the constructor does
// not grow an Action parameter per setting.
//
// Preview fires a fresh notification start to finish, which is what the button
// wants. LivePreview keeps one on screen for as long as the settings are moving,
// which is what the sliders want — dragging with the former would restart the
// animation every frame.
internal readonly record struct ConfigActions(
    Action<string?, string> Preview,
    Action<string?, string> LivePreview,
    Action RebuildFonts,
    Action RestoreNativeAreaText,
    Action RestoreNativeLoadingTitle);

internal sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly ConfigActions actions;

    public ConfigWindow(Configuration config, ConfigActions actions)
        : base("Regions of XIV###RegionsOfXIVConfig")
    {
        this.config = config;
        this.actions = actions;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("##RegionsOfXIVTabs");
        if (!tabs) return;

        DrawGeneralTab();
        DrawNotificationsTab();
        DrawDurationsTab();
    }

    private void DrawGeneralTab()
    {
        using var tab = ImRaii.TabItem("General");
        if (!tab) return;

        var changed = false;

        var verticalPosition = this.config.VerticalPosition;
        if (ImGui.SliderFloat("Vertical position", ref verticalPosition, 0f, 100f, "%.0f%%"))
        {
            this.config.VerticalPosition = verticalPosition;
            changed = true;
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

        var decode = this.config.DecodeEffectEnabled;
        if (ImGui.Checkbox("Decode from Eorzean script", ref decode))
        {
            this.config.DecodeEffectEnabled = decode;
            changed = true;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Requires a bundled Eorzean font. Latin text only.");

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

        if (changed)
        {
            // Size and family changes need new atlas handles; doing it here keeps
            // the rebuild off the draw path.
            this.actions.RebuildFonts();
            this.config.Save();

            // After the rebuild, so the sample is drawn with the size and family
            // just chosen rather than the previous one. Everything on this tab
            // changes how a notification looks, so every change earns a preview —
            // that is the whole point of the tab.
            this.actions.LivePreview(SampleHeader, SampleText);
        }
    }

    private void DrawNotificationsTab()
    {
        using var tab = ImRaii.TabItem("Notifications");
        if (!tab) return;

        var changed = false;

        ImGui.TextWrapped(
            "This plugin replaces the game's own location text rather than drawing alongside " +
            "it. If you turn the suppression below off, the game's version comes back.");
        ImGui.Separator();

        var zone = this.config.ZoneNotificationEnabled;
        if (ImGui.Checkbox("Zone changes", ref zone))
        {
            this.config.ZoneNotificationEnabled = zone;
            changed = true;
        }

        var area = this.config.AreaNotificationEnabled;
        if (ImGui.Checkbox("Area changes", ref area))
        {
            this.config.AreaNotificationEnabled = area;
            changed = true;
        }

        var subArea = this.config.SubAreaNotificationEnabled;
        if (ImGui.Checkbox("Sub-area changes", ref subArea))
        {
            this.config.SubAreaNotificationEnabled = subArea;
            changed = true;
        }

        ImGui.Separator();

        var includeParent = this.config.IncludeParentTierAsHeader;
        if (ImGui.Checkbox("Show the parent tier as a header", ref includeParent))
        {
            this.config.IncludeParentTierAsHeader = includeParent;
            changed = true;
        }

        var hideNative = this.config.HideNativeAreaText;
        if (ImGui.Checkbox("Hide the game's own area text", ref hideNative))
        {
            this.config.HideNativeAreaText = hideNative;
            changed = true;

            if (!hideNative)
                this.actions.RestoreNativeAreaText();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Suppresses the native \"_AreaText\" flash, which draws underneath this plugin.");

        var hideLoadingTitle = this.config.HideNativeLoadingTitle;
        if (ImGui.Checkbox("Hide the loading-screen zone title", ref hideLoadingTitle))
        {
            this.config.HideNativeLoadingTitle = hideLoadingTitle;
            changed = true;

            if (!hideLoadingTitle)
                this.actions.RestoreNativeLoadingTitle();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Suppresses \"_LocationTitle\" and \"_LocationTitleShort\", the gold title\n" +
                "drawn over the loading screen, and shows the same names in this\n" +
                "plugin's style instead.");
        }

        if (changed)
            this.config.Save();
    }

    private void DrawDurationsTab()
    {
        using var tab = ImRaii.TabItem("Durations");
        if (!tab) return;

        var changed = false;

        changed |= DrawSeconds("Fade in", () => this.config.FadeInDuration, v => this.config.FadeInDuration = v, 0.05f, 3f);
        changed |= DrawSeconds("Reveal", () => this.config.RevealDuration, v => this.config.RevealDuration = v, 0.05f, 3f);
        changed |= DrawSeconds("Hold", () => this.config.ShowDuration, v => this.config.ShowDuration = v, 0.5f, 15f);
        changed |= DrawSeconds("Fade out", () => this.config.FadeOutDuration, v => this.config.FadeOutDuration = v, 0.05f, 5f);

        if (changed)
            this.config.Save();
    }

    // A parent/child pair from the starting zones, so the sample exercises both
    // lines and the decode effect has real Latin text to work on.
    private const string SampleHeader = "Middle La Noscea";
    private const string SampleText = "Summerford Farms";

    // The enum names are identifiers, not labels. "Noto Sans CJK" carries the
    // recommendation inline so the default explains itself without a tooltip.
    private static string Label(DisplayFontChoice choice) => choice switch
    {
        DisplayFontChoice.NotoSansCjk => "Noto Sans CJK (recommended)",
        DisplayFontChoice.TrumpGothic => "Trump Gothic",
        DisplayFontChoice.Jupiter => "Jupiter",
        DisplayFontChoice.Axis => "Axis",
        _ => choice.ToString(),
    };

    private static bool DrawSeconds(string label, Func<TimeSpan> get, Action<TimeSpan> set, float min, float max)
    {
        var seconds = (float)get().TotalSeconds;
        if (!ImGui.SliderFloat(label, ref seconds, min, max, "%.2f s"))
            return false;

        set(TimeSpan.FromSeconds(seconds));
        return true;
    }
}
