using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;

namespace RegionsOfXIV.UI;

// Side effects the window needs to trigger, kept together so the constructor does
// not grow an Action parameter per setting.
//
// Preview fires a fresh notification start to finish, which is what the button
// wants. LivePreview keeps one on screen for as long as the settings are moving,
// which is what the sliders want — dragging with the former would restart the
// animation every frame. HoldPreview turns editing mode on and off, under which
// both of the above collapse onto a single notification that never leaves.
internal readonly record struct ConfigActions(
    Action<string?, string> Preview,
    Action<string?, string> LivePreview,
    Action<bool, string?, string> HoldPreview,
    Action RebuildFonts,
    Action RestoreNativeAreaText,
    Action RestoreNativeLoadingTitle);

internal sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly ConfigActions actions;

    // Editing mode, held here rather than in Configuration: it describes what the
    // player is doing this minute, not what they prefer, and a saved one would
    // come back after a restart as a notification stuck on screen with nothing
    // obvious switching it off.
    private bool editing;

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

        // Comments, not Discord. The enum has a Discord member, but its glyph lives
        // in FontAwesome's Brands set and Dalamud only loads Free Solid — asking for
        // it draws a blank box. A speech bubble is the honest Solid stand-in.
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Comments,
            IconOffset = new Vector2(1.5f, 1f),
            Click = _ => Util.OpenLink(DiscordInvite),
            ShowTooltip = () => ImGui.SetTooltip($"Join the Discord\n{DiscordInvite}"),
        });
    }

    public void Dispose() { }

    // Closing the window is the other way out of editing mode, and the one people
    // will reach for without thinking about it. Without this the sample would stay
    // on screen with the only switch for it now hidden.
    public override void OnClose() => SetEditing(false);

    public override void Draw()
    {
        DrawEditingToggle();

        using var tabs = ImRaii.TabBar("##RegionsOfXIVTabs");
        if (!tabs) return;

        DrawGeneralTab();
        DrawEffectsTab();
        DrawPresetsTab();
        DrawNotificationsTab();
        DrawDurationsTab();
    }

    // Above the tab bar rather than on a tab: it changes what every tab's controls
    // do, so it has to be visible and reachable from all of them.
    private void DrawEditingToggle()
    {
        // The return value is the only one on this window that goes unused: the
        // setter is where the work happens, because leaving the mode has to reach
        // the overlay from OnClose too, where no widget was clicked.
        Checkbox("Editing mode", () => this.editing, SetEditing);

        Tooltip(
            "Keeps one sample notification on screen while you work, instead of\n" +
            "starting a new one every time you change something.\n\n" +
            "Zone announcements are held back while this is on. It switches itself\n" +
            "off when you close this window.");

        ImGui.Separator();
    }

    private void SetEditing(bool on)
    {
        this.editing = on;
        this.actions.HoldPreview(on, SampleHeader, SampleText);
    }

    // A parent/child pair from the starting zones, so the sample exercises both
    // lines and the decode effect has real Latin text to work on.
    private const string SampleHeader = "Middle La Noscea";
    private const string SampleText = "Summerford Farms";

    // Reachable two ways, deliberately, because they answer different questions.
    // The title-bar button is the conventional Dalamud spot for "where do I go with
    // this plugin", available whichever tab you are on. The one on the Presets tab
    // is about the presets themselves — a saved preset is a thing you would want to
    // hand someone, and the tab is where that occurs to you.
    private const string DiscordInvite = "https://discord.com/invite/ax2gsRqvpa";

    private static void DrawDiscordLink(string label, string tooltip)
    {
        if (ImGui.Button(label))
            Util.OpenLink(DiscordInvite);

        Tooltip($"{tooltip}\n\nOpens {DiscordInvite} in your browser.");
    }

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

    // The enum names read well enough for these two, so the labels exist to name
    // the default and to keep an unknown value — a config from a newer build —
    // from showing as nothing at all.
    private static string Label(MotionEffect effect) => effect switch
    {
        MotionEffect.None => "None",
        MotionEffect.Typewriter => "Typewriter",
        MotionEffect.Rise => "Rise",
        MotionEffect.Wave => "Wave",
        MotionEffect.Burn => "Burn",
        _ => effect.ToString(),
    };

    private static string Label(ParticleEffect effect) => effect switch
    {
        ParticleEffect.None => "None",
        ParticleEffect.Hearts => "Hearts",
        ParticleEffect.Embers => "Embers",
        ParticleEffect.Sparkles => "Sparkles",
        ParticleEffect.Petals => "Petals",
        _ => effect.ToString(),
    };

    // --- controls -----------------------------------------------------------
    //
    // ImGui's widgets take a ref and report whether they moved, which on its own
    // means three lines of local variable per setting before anything is decided.
    // These wrap that into one expression per setting, each returning whether it
    // changed so a tab can accumulate with |=.
    //
    // Getters and setters rather than a ref to the property: an auto-property has
    // no address to take.

    private static bool Slider(
        string label, Func<float> get, Action<float> set, float min, float max, string format)
    {
        var value = get();
        if (!ImGui.SliderFloat(label, ref value, min, max, format))
            return false;

        set(value);
        return true;
    }

    private static bool DrawSeconds(string label, Func<TimeSpan> get, Action<TimeSpan> set, float min, float max)
    {
        var seconds = (float)get().TotalSeconds;
        if (!ImGui.SliderFloat(label, ref seconds, min, max, "%.2f s"))
            return false;

        set(TimeSpan.FromSeconds(seconds));
        return true;
    }

    private static bool Checkbox(string label, Func<bool> get, Action<bool> set)
    {
        var value = get();
        if (!ImGui.Checkbox(label, ref value))
            return false;

        set(value);
        return true;
    }

    private static bool ColorPicker(string label, Func<Vector4> get, Action<Vector4> set)
    {
        var value = get();
        if (!ImGui.ColorEdit4(label, ref value, ImGuiColorEditFlags.NoInputs))
            return false;

        set(value);
        return true;
    }

    // The combo is closed before this returns, so a Tooltip call after it still
    // attaches to the combo itself rather than to its last item.
    private static bool Choice<T>(string label, Func<T> get, Action<T> set, Func<T, string> name)
        where T : struct, Enum
    {
        var current = get();
        var changed = false;

        using var combo = ImRaii.Combo(label, name(current));
        if (!combo)
            return false;

        foreach (var option in Enum.GetValues<T>())
        {
            if (!ImGui.Selectable(name(option), option.Equals(current)))
                continue;

            set(option);
            changed = true;
        }

        return changed;
    }

    // AllowWhenDisabled, because a greyed-out control is exactly the one whose
    // tooltip is load-bearing: it is the only thing that says what would re-enable
    // it. ImGui suppresses hover on disabled items by default, so without this flag
    // the explanation is unreachable precisely when it is needed.
    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(text);
    }
}
