using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
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
    Func<FontRole, string?> FontProblem,
    Action ShowChangelog,
    Action RestoreNativeAreaText,
    Action RestoreNativeLoadingTitle);

internal sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly ConfigActions actions;
    private readonly FileDialogManager fileDialogs = new();

    private bool editing;

    public ConfigWindow(Configuration config, ConfigActions actions)
        : base($"Regions of XIV v{Changelog.Current}###RegionsOfXIVConfig")
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

    public void Dispose() => this.fileDialogs.Reset();

    public override void OnClose()
    {
        SetEditing(false);
        this.fileDialogs.Reset();

        if (this.unsaved)
        {
            this.unsaved = false;
            this.config.Save();
        }
    }

    public override void Draw()
    {
        SaveIfSettled();

        this.fileDialogs.Draw();

        DrawEditingToggle();

        using var tabs = ImRaii.TabBar("##RegionsOfXIVTabs");
        if (!tabs) return;

        DrawGeneralTab();
        DrawFontsTab();
        DrawEffectsTab();
        DrawPresetsTab();
        DrawNotificationsTab();
        DrawDurationsTab();
        DrawAboutTab();
    }

    private void DrawEditingToggle()
    {
        var toggled = false;
        var wanted = Checkbox("Editing mode", this.editing, ref toggled);

        if (toggled)
            SetEditing(wanted);

        Tooltip(
            "Keeps one sample notification on screen while you work, instead of\n" +
            "starting a new one every time you change something.\n\n" +
            "Zone announcements are held back while this is on. It switches itself\n" +
            "off when you close this window.");

        ImGui.Separator();
    }

    private bool unsaved;

    private void MarkUnsaved() => this.unsaved = true;

    private void SaveIfSettled()
    {
        if (!this.unsaved || ImGui.IsAnyItemActive())
            return;

        this.unsaved = false;
        this.config.Save();
    }

    private void SetEditing(bool on)
    {
        this.editing = on;
        this.actions.HoldPreview(on, Sample);
    }

    private static readonly PreviewSample Sample = BuildSample();

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

    private const string DiscordInvite = DiscordLink.Invite;

    private static void DrawDiscordLink(string label, string tooltip)
    {
        DiscordLink.DrawButton(label);

        Tooltip($"{tooltip}\n\nOpens {DiscordInvite} in your browser.");
    }

    private static string Label(FontChoice choice) => choice switch
    {
        FontChoice.NotoSansCjk => "Noto Sans CJK (recommended)",
        FontChoice.TrumpGothic => "Trump Gothic",
        FontChoice.Jupiter => "Jupiter",
        FontChoice.Axis => "Axis",
        FontChoice.Custom => "Custom file",
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

    private static float Slider(
        string label, float value, float min, float max, string format, ref bool changed)
    {
        if (ImGui.SliderFloat(label, ref value, min, max, format))
            changed = true;

        return value;
    }

    private static TimeSpan DrawSeconds(
        string label, TimeSpan value, float min, float max, ref bool changed, ref bool settled)
    {
        var seconds = (float)value.TotalSeconds;
        var edited = ImGui.SliderFloat(label, ref seconds, min, max, "%.2f s");

        settled |= ImGui.IsItemDeactivatedAfterEdit();

        if (!edited)
            return value;

        changed = true;
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool Checkbox(string label, bool value, ref bool changed)
    {
        if (ImGui.Checkbox(label, ref value))
            changed = true;

        return value;
    }

    private static Vector4 ColorPicker(string label, Vector4 value, ref bool changed)
    {
        var rgb = new Vector3(value.X, value.Y, value.Z);

        if (!ImGui.ColorEdit3(label, ref rgb, ImGuiColorEditFlags.NoInputs))
            return value;

        changed = true;
        return new Vector4(rgb, value.W);
    }

    private static T Choice<T>(string label, T value, Func<T, string> name, ref bool changed)
        where T : struct, Enum
    {
        using var combo = ImRaii.Combo(label, name(value));
        if (!combo)
            return value;

        foreach (var option in Enum.GetValues<T>())
        {
            if (!ImGui.Selectable(name(option), option.Equals(value)))
                continue;

            value = option;
            changed = true;
        }

        return value;
    }

    private static readonly Vector4 GoodColor = new(0.45f, 0.85f, 0.45f, 1f);

    private static readonly Vector4 CautionColor = new(1f, 0.78f, 0.35f, 1f);

    private static readonly Vector4 FaultColor = new(1f, 0.35f, 0.35f, 1f);

    private static void Warn(Vector4 color, string text)
    {
        using var pushed = ImRaii.PushColor(ImGuiCol.Text, color);

        ImGui.TextWrapped(text);
    }

    private const float TooltipWidthInEm = 35f;

    private static void DisabledWrapped(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));

        ImGui.TextWrapped(text);
    }

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
