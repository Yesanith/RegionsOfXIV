using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;
using RegionsOfXIV.UI.Shell;

namespace RegionsOfXIV.UI;

internal readonly record struct PreviewSample(
    string? Header, string Text, string Weather, uint WeatherIcon, string Banner);

internal readonly record struct ConfigActions(
    Action<PreviewSample> Preview,
    Action<PreviewSample> LivePreview,
    Action<bool, PreviewSample> HoldPreview,
    Action RebuildFonts,
    Func<FontRole, string?> FontProblem,
    Action RestoreNativeAreaText,
    Action RestoreNativeLoadingTitle,
    Action ReloadLanguage,
    Action AuditionSound,
    Func<string, string?> SoundFileProblem,
    Func<string?> SoundSilenceReason);

// Split across ConfigWindow.*.cs, one file per page, over the widget vocabulary they all share in
// ConfigWindow.Widgets.cs. This file is the window itself: its lifetime, the chrome and fonts it
// pushes, the header, the navigation rail, the page it is on, and the saving.
//
// The window never touches the plugin directly: everything it needs to make happen goes through
// the delegates in ConfigActions, which Plugin.cs supplies.
internal sealed partial class ConfigWindow : Window, IDisposable
{
    public enum Page
    {
        Announcements,
        Appearance,
        Motion,
        Fonts,
        Sound,
        Presets,
        Changelog,
        About,
    }

    private const float PageRevealMs = 260f;
    private const float PageSlide = 12f;
    private const float MinimumBodyHeight = 320f;

    private const ImGuiWindowFlags ShellFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;

    private static readonly Vector2 DefaultSize = new(980, 740);

    private readonly Configuration config;
    private readonly ConfigActions actions;
    private readonly FileDialogManager fileDialogs = new();

    private IDisposable? pushedFont;
    private IDisposable? pushedChrome;

    private Page page = Page.Announcements;
    private long pageShownTick = Environment.TickCount64;
    private bool resetScroll;

    private bool editing;
    private bool unsaved;

    private readonly Action dismissTranslationNotice;

    private string? translationNotice;
    private int translationNoticeGeneration = -1;

    // The title is not translated: "Regions of XIV" is the plugin's name and the rest is a
    // version number. The ### part is the identity Dalamud saves this window's position and size
    // against, and it is kept from before the redesign so nobody's window moves.
    public ConfigWindow(Configuration config, ConfigActions actions)
        : base($"Regions of XIV v{Changelog.Current}###RegionsOfXIVConfig", ShellFlags)
    {
        this.config = config;
        this.actions = actions;
        this.dismissTranslationNotice = DrawTranslationNoticeDismiss;

        Size = DefaultSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(HeaderBar.MinimumWidth, Layout.HeaderHeight + MinimumBodyHeight),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        AllowPinning = false;
        AllowClickthrough = false;
    }

    public bool Editing => this.editing;

    public void Dispose() => this.fileDialogs.Reset();

    public void Show(Page target)
    {
        if (this.page != target)
        {
            this.page = target;
            this.pageShownTick = Environment.TickCount64;
            this.resetScroll = true;
        }

        IsOpen = true;
    }

    public void FirePreview() => this.actions.Preview(Sample);

    // Either side of the ImGui window rather than around Draw, so popups and tooltips opened from
    // the body carry the same font and chrome.
    public override void PreDraw()
    {
        this.pushedFont = Fonts.PushBody();
        this.pushedChrome = Styling.PushChrome(Vector2.Zero);
    }

    public override void PostDraw()
    {
        this.pushedChrome?.Dispose();
        this.pushedChrome = null;
        this.pushedFont?.Dispose();
        this.pushedFont = null;
    }

    public override void OnOpen() => this.pageShownTick = Environment.TickCount64;

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
        DrawFileDialogs();

        var scale = ImGuiHelpers.GlobalScale;
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var headerHeight = Layout.HeaderHeight * scale;
        var windowRounding = Styling.WindowRounding * scale;
        var drawList = ImGui.GetWindowDrawList();

        HeaderBar.HandleDrag(windowPos, windowSize.X, headerHeight);
        Ambient.Draw(drawList, windowPos, windowPos + windowSize);
        HeaderBar.Draw(this, windowPos, windowSize.X, headerHeight, windowRounding);
        DrawBody(windowPos, windowSize, headerHeight);
        DrawResizeGrip(drawList, windowPos + windowSize);
    }

    // The dialog is a window of its own and would otherwise inherit the shell's edge-to-edge
    // padding.
    private void DrawFileDialogs()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(8f, 8f) * scale)
            .Push(ImGuiStyleVar.WindowRounding, Styling.CardRounding * scale);

        this.fileDialogs.Draw();
    }

    private void DrawBody(Vector2 windowPos, Vector2 windowSize, float headerHeight)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var railWidth = Layout.RailWidth * scale;
        var bodyTop = windowPos.Y + headerHeight;
        var bodyHeight = windowSize.Y - headerHeight;
        if (bodyHeight < 1f)
        {
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(windowPos.X, bodyTop));
        using (var rail = ImRaii.Child("##rox-rail", new Vector2(railWidth, bodyHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (rail.Success && NavRail.Draw(this.page, this.unseenChanges > 0) is { } target)
            {
                Show(target);
            }
        }

        Paint.Hairline(drawList, new Vector2(windowPos.X + railWidth, bodyTop + (10f * scale)), new Vector2(windowPos.X + railWidth, bodyTop + bodyHeight - (10f * scale)));

        // The page column is capped and centred by padding the child, so it scrolls as one and a
        // very wide window does not stretch every row across it.
        var bodyWidth = windowSize.X - railWidth - (Layout.ContentRightInset * scale);
        var padding = Layout.ContentPadding * scale;
        var padX = MathF.Max(padding, (bodyWidth - (Layout.ContentMaxWidth * scale)) * 0.5f);

        ImGui.SetCursorScreenPos(new Vector2(windowPos.X + railWidth, bodyTop));
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(padX, padding * 0.8f)))
        using (var content = ImRaii.Child("##rox-page", new Vector2(bodyWidth, bodyHeight), false, ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (content)
            {
                DrawPage();
            }
        }
    }

    private void DrawPage()
    {
        if (this.resetScroll)
        {
            ImGui.SetScrollY(0f);
            this.resetScroll = false;
        }

        using var reveal = Motion.PushReveal(Motion.Reveal(this.pageShownTick, PageRevealMs), PageSlide);

        DrawTranslationNotice();

        switch (this.page)
        {
            case Page.Announcements:
                DrawAnnouncementsPage();
                break;
            case Page.Appearance:
                DrawAppearancePage();
                break;
            case Page.Motion:
                DrawMotionPage();
                break;
            case Page.Fonts:
                DrawFontsPage();
                break;
            case Page.Sound:
                DrawSoundPage();
                break;
            case Page.Presets:
                DrawPresetsPage();
                break;
            case Page.Changelog:
                DrawChangelogPage();
                break;
            case Page.About:
                DrawAboutPage();
                break;
        }

        Styling.VSpace(12f);
    }

    private static void DrawResizeGrip(ImDrawListPtr drawList, Vector2 corner)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var grip = 12f * scale;
        var inset = 4f * scale;
        var anchor = corner - new Vector2(inset, inset);
        var hovered = ImGui.IsMouseHoveringRect(corner - new Vector2(grip + inset, grip + inset), corner);
        var color = Paint.Col(Styling.WithAlpha(Styling.TextStrong, hovered ? 0.38f : 0.14f));
        for (var lineIndex = 1; lineIndex <= 3; lineIndex++)
        {
            var offset = lineIndex * grip / 3f;
            drawList.AddLine(anchor - new Vector2(offset, 0f), anchor - new Vector2(0f, offset), color, 1.4f * scale);
        }
    }

    // Only for a file that says it is a machine draft, and only until it is waved away for that
    // language. Someone reading a rough translation should be told it is rough in the language
    // they are reading, which is why the wording lives in each locale file rather than here.
    private void DrawTranslationNotice()
    {
        if (!Loc.IsMachineDraft || this.config.TranslationNoticeDismissedFor == Loc.Current)
        {
            return;
        }

        if (this.translationNotice is null || this.translationNoticeGeneration != Loc.Generation)
        {
            this.translationNoticeGeneration = Loc.Generation;
            this.translationNotice = Loc.Format(
                "notice.translation",
                "This translation was drafted by a machine and has not been checked by anyone "
                + "who speaks the language. If something reads badly, corrections are very "
                + "welcome:\n{0}",
                DiscordInvite);
        }

        NoticeCard.Draw(Styling.AccentAmber, FontAwesomeIcon.Language, this.translationNotice, trailing: this.dismissTranslationNotice);
    }

    private void DrawTranslationNoticeDismiss()
    {
        Styling.VSpace(6f);
        var label = Loc.Get("notice.dismiss", "Hide this");
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - PillButton.Width(label, FontAwesomeIcon.Check));
        if (!PillButton.Draw("##rox-notice-dismiss", label, Styling.AccentAmber, PillButton.Emphasis.Ghost, FontAwesomeIcon.Check))
        {
            return;
        }

        this.config.TranslationNoticeDismissedFor = Loc.Current;
        this.config.Save();
    }

    private void MarkUnsaved() => this.unsaved = true;

    // Writing the config on every changed frame meant a full JSON serialise and disk write per
    // frame while a slider was being dragged, which made the pickers feel like they had stuck.
    // The write is deferred until nothing is being interacted with.
    private void SaveIfSettled()
    {
        if (!this.unsaved || ImGui.IsAnyItemActive())
        {
            return;
        }

        this.unsaved = false;
        this.config.Save();
    }

    public void SetEditing(bool on)
    {
        this.editing = on;
        this.actions.HoldPreview(on, Sample);
    }

    // Built on each use rather than once, because the banner language can change while the window
    // is open and the sample has to change with it. Both resolvers cache, and this is reached from
    // button presses and change handlers rather than from the draw loop.
    private static PreviewSample Sample => BuildSample();

    // The sample's place names are content rather than the plugin's own words, so they stay as
    // they are. The weather name comes from the game's own sheets; "Fair Skies" is only the
    // fallback for when that lookup finds nothing.
    //
    // The banner is Light Party, and not for the sake of an example: it is the one that collides
    // with an arrival in practice, so previewing it is previewing the case the drop exists for.
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
}
