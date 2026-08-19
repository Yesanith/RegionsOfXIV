using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal readonly record struct PreviewSample(
    string? Header, string Text, string Weather, uint WeatherIcon);

internal readonly record struct ConfigActions(
    Action<PreviewSample> Preview,
    Action<PreviewSample> LivePreview,
    Action<bool, PreviewSample> HoldPreview,
    Action RebuildFonts,
    Action RestoreNativeAreaText,
    Action RestoreNativeLoadingTitle);

internal sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly ConfigActions actions;

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

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Comments,
            IconOffset = new Vector2(1.5f, 1f),
            Click = _ => Util.OpenLink(DiscordInvite),
            ShowTooltip = () => ImGui.SetTooltip($"Join the Discord\n{DiscordInvite}"),
        });
    }

    public void Dispose() { }

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

    private void DrawEditingToggle()
    {
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
        this.actions.HoldPreview(on, Sample);
    }

    private static readonly PreviewSample Sample = BuildSample();

    /// <summary>The sample shown while configuring, using the client's own words for the weather.</summary>
    private static PreviewSample BuildSample()
    {
        var weather = WeatherNameResolver.Resolve(FairWeather);

        return new PreviewSample(
            "Middle La Noscea",
            "Summerford Farms",
            weather?.Name ?? "Fair Skies",
            weather?.IconId ?? 0u);
    }

    private const uint FairWeather = 2;

    private const string DiscordInvite = "https://discord.com/invite/ax2gsRqvpa";

    private static void DrawDiscordLink(string label, string tooltip)
    {
        if (ImGui.Button(label))
            Util.OpenLink(DiscordInvite);

        Tooltip($"{tooltip}\n\nOpens {DiscordInvite} in your browser.");
    }

    private static string Label(DisplayFontChoice choice) => choice switch
    {
        DisplayFontChoice.NotoSansCjk => "Noto Sans CJK (recommended)",
        DisplayFontChoice.TrumpGothic => "Trump Gothic",
        DisplayFontChoice.Jupiter => "Jupiter",
        DisplayFontChoice.Axis => "Axis",
        _ => choice.ToString(),
    };

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

    private const float TooltipWidthInEm = 35f;

    private static void Tooltip(string text)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return;

        using var tooltip = ImRaii.Tooltip();

        ImGui.PushTextWrapPos(ImGui.GetFontSize() * TooltipWidthInEm);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }
}
