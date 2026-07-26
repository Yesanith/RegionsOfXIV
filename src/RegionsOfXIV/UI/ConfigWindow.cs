using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace RegionsOfXIV.UI;

// TODO(Phase 8): live preview while dragging position/size sliders. The GW2
// original pops a sample notification that auto-disposes 250ms after the last
// change — without it those sliders are pure guesswork.
// Side effects the window needs to trigger, kept together so the constructor does
// not grow an Action parameter per setting.
internal readonly record struct ConfigActions(
    Action<string?, string> Preview,
    Action RebuildFonts,
    Action RestoreNativeAreaText,
    Func<float> DisplayFontCeilingPx);

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
        DrawSoundTab();
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

        using (var combo = ImRaii.Combo("Font", this.config.DisplayFont.ToString()))
        {
            if (combo)
            {
                foreach (var choice in Enum.GetValues<DisplayFontChoice>())
                {
                    if (!ImGui.Selectable(choice.ToString(), choice == this.config.DisplayFont))
                        continue;

                    this.config.DisplayFont = choice;
                    changed = true;
                }
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Trump Gothic and Jupiter cover Latin only.\n" +
                "Auto switches to Axis on a Japanese client.\n\n" +
                "The game's fonts are bitmaps baked at fixed sizes, so they soften\n" +
                "above their largest one. Dalamud's face is vector and stays sharp\n" +
                "at any size.");
        }

        // The game ships no large Axis bitmap, so asking for a big size upscales it.
        // Say so rather than leaving the user to wonder why it looks soft.
        var ceiling = this.actions.DisplayFontCeilingPx();
        if (this.config.DisplayFontSize > ceiling)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.78f, 0.35f, 1f)))
            {
                ImGui.TextWrapped(
                    $"This font has no bitmap above {ceiling:F0} px, so at {this.config.DisplayFontSize:F0} px " +
                    "it is being upscaled and will look soft. Lower the size, or pick Dalamud.");
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

        ImGui.Separator();

        if (ImGui.Button("Preview"))
            this.actions.Preview("Middle La Noscea", "Summerford Farms");

        if (changed)
        {
            // Size and family changes need new atlas handles; doing it here keeps
            // the rebuild off the draw path.
            this.actions.RebuildFonts();
            this.config.Save();
        }
    }

    private void DrawNotificationsTab()
    {
        using var tab = ImRaii.TabItem("Notifications");
        if (!tab) return;

        var changed = false;

        ImGui.TextWrapped(
            "FFXIV already announces zone changes on screen. Sub-area changes it keeps to " +
            "itself, which is where this plugin earns its keep.");
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

    private void DrawSoundTab()
    {
        using var tab = ImRaii.TabItem("Sound");
        if (!tab) return;

        var changed = false;

        ImGui.TextWrapped("Uses the game's own UI sound effects. Off by default.");
        ImGui.Separator();

        var reveal = this.config.RevealSoundEnabled;
        if (ImGui.Checkbox("Play a sound on reveal", ref reveal))
        {
            this.config.RevealSoundEnabled = reveal;
            changed = true;
        }

        var revealId = (int)this.config.RevealSoundEffectId;
        if (ImGui.InputInt("Reveal sound ID", ref revealId))
        {
            this.config.RevealSoundEffectId = (uint)Math.Clamp(revealId, 0, 100);
            changed = true;
        }

        ImGui.Separator();

        var vanish = this.config.VanishSoundEnabled;
        if (ImGui.Checkbox("Play a sound on fade-out", ref vanish))
        {
            this.config.VanishSoundEnabled = vanish;
            changed = true;
        }

        var vanishId = (int)this.config.VanishSoundEffectId;
        if (ImGui.InputInt("Fade-out sound ID", ref vanishId))
        {
            this.config.VanishSoundEffectId = (uint)Math.Clamp(vanishId, 0, 100);
            changed = true;
        }

        if (changed)
            this.config.Save();
    }

    private static bool DrawSeconds(string label, Func<TimeSpan> get, Action<TimeSpan> set, float min, float max)
    {
        var seconds = (float)get().TotalSeconds;
        if (!ImGui.SliderFloat(label, ref seconds, min, max, "%.2f s"))
            return false;

        set(TimeSpan.FromSeconds(seconds));
        return true;
    }
}
