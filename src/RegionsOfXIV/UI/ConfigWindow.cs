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
    Action RestoreNativeLoadingTitle,
    Action ReloadLanguage);

// Split across ConfigWindow.*.cs, one file per tab, sharing the small widget helpers at the
// bottom of this file. Those helpers all take the current value and hand back the new one,
// setting a shared "changed" flag, so a tab can act once at the end rather than after each
// control.
//
// The window never touches the plugin directly: everything it needs to make happen goes through
// the delegates in ConfigActions, which Plugin.cs supplies.
internal sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly ConfigActions actions;
    private readonly FileDialogManager fileDialogs = new();

    private bool editing;

    // The title is not translated: "Regions of XIV" is the plugin's name and the rest is a
    // version number, so no language changes it. It is also set once here, in the constructor,
    // where a translated one would keep whichever language the game started in. The ### part is
    // the identity Dalamud saves this window's position and size against.
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

            // SetTooltip formats its argument. The button has already decided to show this, so
            // the Tooltip helper's hover check does not fit -- but the pair inside it does.
            ShowTooltip = () =>
            {
                using var tooltip = ImRaii.Tooltip();

                ImGui.TextUnformatted(
                    Loc.Format("window.discord", "Join the Discord\n{0}", DiscordInvite));
            },
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

        DrawTranslationNotice();
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

    // Only for a file that says it is a machine draft, and only until it is waved away for that
    // language. Someone reading a rough translation should be told it is rough in the language
    // they are reading, which is why the wording lives in each locale file rather than here.
    private void DrawTranslationNotice()
    {
        if (!Loc.IsMachineDraft || this.config.TranslationNoticeDismissedFor == Loc.Current)
            return;

        Warn(
            CautionColor,
            Loc.Format(
                "notice.translation",
                "This translation was drafted by a machine and has not been checked by anyone "
                + "who speaks the language. If something reads badly, corrections are very "
                + "welcome:\n{0}",
                DiscordInvite));

        if (ImGui.SmallButton(Loc.Label("notice.dismiss", "Hide this")))
        {
            this.config.TranslationNoticeDismissedFor = Loc.Current;
            this.config.Save();
        }

        ImGui.Separator();
    }

    private void DrawEditingToggle()
    {
        var toggled = false;
        var wanted = Checkbox(
            Loc.Label("window.editing", "Editing mode"), this.editing, ref toggled);

        if (toggled)
            SetEditing(wanted);

        UiText.Tooltip(Loc.Get(
            "window.editing.tooltip",
            "Keeps one sample notification on screen while you work, instead of\n" +
            "starting a new one every time you change something.\n\n" +
            "Zone announcements are held back while this is on. It switches itself\n" +
            "off when you close this window."));

        ImGui.Separator();
    }

    private bool unsaved;

    private void MarkUnsaved() => this.unsaved = true;

    // Writing the config on every changed frame meant a full JSON serialise and disk write per
    // frame while a slider was being dragged, which made the pickers feel like they had stuck.
    // The write is deferred until nothing is being interacted with.
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

    // The sample's place names are content rather than the plugin's own words, so they stay as
    // they are. The weather name comes from the game's own sheets; "Fair Skies" is only the
    // fallback for when that lookup finds nothing.
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

        // The same key Link uses: every one of these tooltips is a sentence about the button
        // followed by the address it opens, so a translator sees the pattern once.
        UiText.Tooltip(Loc.Format("common.opens", "{0}\n\nOpens {1} in your browser.", tooltip, DiscordInvite));
    }

    // Trump Gothic, Jupiter and Axis are the names of typefaces and are not translated in any
    // language -- they are what the files are called. Only the two entries carrying words go
    // through Loc.
    private static string Label(FontChoice choice) => choice switch
    {
        FontChoice.NotoSansCjk => Loc.Format(
            "fonts.choice.noto", "{0} (recommended)", "Noto Sans CJK"),
        FontChoice.TrumpGothic => "Trump Gothic",
        FontChoice.Jupiter => "Jupiter",
        FontChoice.Axis => "Axis",
        FontChoice.Custom => Loc.Get("fonts.choice.custom", "Custom file"),
        _ => choice.ToString(),
    };

    // Loc.Get rather than Loc.Label: Choice supplies the identity from the enum value itself, so
    // these are wording only. The two "None"s keep separate keys because a language that inflects
    // for gender will not want one word for both.
    private static string Label(MotionEffect effect) => effect switch
    {
        MotionEffect.None => Loc.Get("effects.motion.none", "None"),
        MotionEffect.Typewriter => Loc.Get("effects.motion.typewriter", "Typewriter"),
        MotionEffect.Rise => Loc.Get("effects.motion.rise", "Rise"),
        MotionEffect.Wave => Loc.Get("effects.motion.wave", "Wave"),
        MotionEffect.Burn => Loc.Get("effects.motion.burn", "Burn"),
        _ => effect.ToString(),
    };

    private static string Label(ParticleEffect effect) => effect switch
    {
        ParticleEffect.None => Loc.Get("effects.particles.none", "None"),
        ParticleEffect.Hearts => Loc.Get("effects.particles.hearts", "Hearts"),
        ParticleEffect.Embers => Loc.Get("effects.particles.embers", "Embers"),
        ParticleEffect.Sparkles => Loc.Get("effects.particles.sparkles", "Sparkles"),
        ParticleEffect.Petals => Loc.Get("effects.particles.petals", "Petals"),
        _ => effect.ToString(),
    };

    // format reaches native sprintf. Handing the whole string to a translator would put the
    // specifier in their keeping, and one that no longer matches the float being passed is
    // undefined behaviour rather than a wrong-looking number -- so callers concatenate a
    // translated unit onto a literal specifier instead of translating the format.
    //
    // The unit half goes through Loc.Unit, which doubles any per-cent sign in it. Concatenating
    // on its own only moves the problem one string along: the unit reaches sprintf as well.
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
        var edited = ImGui.SliderFloat(
            label, ref seconds, min, max, "%.2f " + Loc.Unit("units.seconds", "s"));

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

    // The alpha bar is back, but floored at Configuration.MinAlpha rather than running to zero.
    // Unbounded, it was easy to drag to nothing by accident and impossible to undo, because
    // picking a new colour leaves the alpha where it was -- so the line simply vanished and read
    // as a broken plugin. Dragging below the floor now stops there instead.
    //
    // NoTooltip replaces ImGui's own colour tooltip with the one below, which is the only place
    // the floor is explained.
    private static Vector4 ColorPicker(string label, Vector4 value, ref bool changed)
    {
        var edited = ImGui.ColorEdit4(
            label,
            ref value,
            ImGuiColorEditFlags.NoInputs
            | ImGuiColorEditFlags.AlphaBar
            | ImGuiColorEditFlags.AlphaPreviewHalf
            | ImGuiColorEditFlags.NoTooltip);

        if (edited)
        {
            changed = true;
            value = value with { W = Math.Max(value.W, Configuration.MinAlpha) };
        }

        UiText.Tooltip(Loc.Format(
            "common.colour.tooltip",
            "Currently {0:F0}% solid.\n\nClick for the full picker. The narrow chequered strip "
            + "right of the rainbow is alpha — how solid this colour is — and it stops at "
            + "{1:F0}%, far enough back to sit behind the other lines but not so far that the "
            + "line disappears and looks like a fault.",
            value.W * 100f,
            Configuration.MinAlpha * 100f));

        return value;
    }

    private static T Choice<T>(string label, T value, Func<T, string> name, ref bool changed)
        where T : struct, Enum
    {
        using var combo = ImRaii.Combo(label, name(value));
        if (!combo)
            return value;

        foreach (var option in Enum.GetValues<T>())
        {
            // The enum value is the identity, not the wording. Two options that translate to the
            // same word would otherwise be one entry, and the one you could not pick would look
            // like a dropdown that ignores you.
            if (!ImGui.Selectable($"{name(option)}###{option}", option.Equals(value)))
                continue;

            value = option;
            changed = true;
        }

        return value;
    }

    // What a row of buttons will occupy, including the spacing before each one. Used to reserve
    // room beside a field rather than guessing at it -- button words are translated, and several
    // languages set them a good deal wider than English does.
    //
    // Measured with the identity hidden, because these labels carry "###key" and CalcTextSize
    // counts that as visible text unless told not to.
    private static float ButtonRowWidth(params string[] labels)
    {
        var style = ImGui.GetStyle();
        var width = 0f;

        foreach (var label in labels)
        {
            width += ImGui.CalcTextSize(label, hideTextAfterDoubleHash: true).X
                     + (style.FramePadding.X * 2f)
                     + style.ItemSpacing.X;
        }

        return width;
    }

    private static readonly Vector4 GoodColor = new(0.45f, 0.85f, 0.45f, 1f);

    private static readonly Vector4 CautionColor = new(1f, 0.78f, 0.35f, 1f);

    private static readonly Vector4 FaultColor = new(1f, 0.35f, 0.35f, 1f);

    // This window's own two compositions -- a colour plus a wrap -- over the shared drawing in
    // UiText. Everything else in here calls UiText directly.
    private static void Warn(Vector4 color, string text)
    {
        using var pushed = ImRaii.PushColor(ImGuiCol.Text, color);

        UiText.Wrapped(text);
    }

    private static void DisabledWrapped(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));

        UiText.Wrapped(text);
    }
}
