using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

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

internal sealed partial class ConfigWindow : Window, IDisposable
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
        DrawEffectsTab();
        DrawNotificationsTab();
        DrawDurationsTab();
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

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }
}
