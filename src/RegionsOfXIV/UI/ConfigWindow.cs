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
    string? Header, string Text, string Weather, uint WeatherIcon, string Banner);

internal readonly record struct ConfigActions(
    Action<PreviewSample> Preview,
    Action<PreviewSample> LivePreview,
    Action<bool, PreviewSample> HoldPreview,
    Action RebuildFonts,
    Func<FontRole, string?> FontProblem,
    Action ShowChangelog,
    Action RestoreNativeAreaText,
    Action RestoreNativeLoadingTitle,
    Action ReloadLanguage,
    Action AuditionSound,
    Func<string, string?> SoundFileProblem,
    Func<string?> SoundSilenceReason);

// Split across ConfigWindow.*.cs, one file per tab, over the widget vocabulary they all share in
// ConfigWindow.Widgets.cs. This file is the window itself: its lifetime, the font it pushes, the
// tab bar, and the saving.
//
// The window never touches the plugin directly: everything it needs to make happen goes through
// the delegates in ConfigActions, which Plugin.cs supplies.
internal sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly ConfigActions actions;
    private readonly WindowFont font;
    private readonly FileDialogManager fileDialogs = new();

    private IDisposable? pushedFont;

    private bool editing;

    // The title is not translated: "Regions of XIV" is the plugin's name and the rest is a
    // version number, so no language changes it. It is also set once here, in the constructor,
    // where a translated one would keep whichever language the game started in. The ### part is
    // the identity Dalamud saves this window's position and size against.
    public ConfigWindow(Configuration config, ConfigActions actions, WindowFont font)
        : base($"Regions of XIV v{Changelog.Current}###RegionsOfXIVConfig")
    {
        this.config = config;
        this.actions = actions;
        this.font = font;

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

    // Either side of the ImGui window rather than around Draw, so the title bar is drawn with the
    // same font as the body. The font itself belongs to Plugin.cs, which is why this only borrows
    // it. WindowHost calls these two in matching branches, so the push is always popped.
    public override void PreDraw() => this.pushedFont = this.font.Push();

    public override void PostDraw()
    {
        this.pushedFont?.Dispose();
        this.pushedFont = null;
    }

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

        DrawAnnouncementsTab();
        DrawAppearanceTab();
        DrawMotionTab();
        DrawFontsTab();
        DrawSoundTab();
        DrawPresetsTab();
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
    //
    // The banner is Light Party, and not for the sake of an example: it is the one that collides
    // with an arrival in practice, so previewing it is previewing the case the drop exists for.
    // It goes through BannerNotification.Format for the same reason the weather goes through the
    // resolver, so what the preview shows is what the live path would produce.
    private static PreviewSample BuildSample()
    {
        var weather = WeatherNameResolver.Resolve(FairWeather);

        return new PreviewSample(
            "Middle La Noscea",
            "Summerford Farms",
            weather?.Name ?? "Fair Skies",
            weather?.IconId ?? 0u,
            BannerNotification.Format(BannerNameResolver.Resolve(LightParty) ?? "Light Party"));
    }

    private const uint FairWeather = 2;

    private const uint LightParty = 120114;

    private const string DiscordInvite = DiscordLink.Invite;

    private static void DrawDiscordLink(string label, string tooltip)
    {
        DiscordLink.DrawButton(label);

        // The same key Link uses: every one of these tooltips is a sentence about the button
        // followed by the address it opens, so a translator sees the pattern once.
        UiText.Tooltip(Loc.Format("common.opens", "{0}\n\nOpens {1} in your browser.", tooltip, DiscordInvite));
    }

}
