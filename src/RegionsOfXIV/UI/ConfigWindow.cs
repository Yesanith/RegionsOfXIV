using System;
using System.Globalization;
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
    Action<SoundCategory> AuditionSound,
    Func<string, string?> SoundFileProblem,
    Func<string?> SoundSilenceReason);

// Split across ConfigWindow.*.cs, one file per page, over the widget vocabulary they all share in
// ConfigWindow.Widgets.cs. This file is the window itself: its lifetime, the chrome and fonts it
// pushes, the header, the preview stage, the tab strip, the page it is on, and the saving.
//
// The window never touches the plugin directly: everything it needs to make happen goes through
// the delegates in ConfigActions, which Plugin.cs supplies. The stage is the one exception, and
// only because it draws with the same renderer the overlay does.
internal sealed partial class ConfigWindow : Window, IDisposable
{
    public enum Page
    {
        Announcements,
        Appearance,
        Fonts,
        Motion,
        Sound,
        Presets,
        Changelog,
        About,
    }

    private const float PageRevealMs = 260f;
    private const float PageSlide = 12f;
    private const float MinimumBodyHeight = 300f;

    private const ImGuiWindowFlags ShellFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;

    private static readonly Vector2 DefaultSize = new(1080, 840);

    private readonly Configuration config;
    private readonly ConfigActions actions;
    private readonly PreviewStage stage;
    private readonly FileDialogManager fileDialogs = new();
    private readonly TabStrip.Item[] tabItems = new TabStrip.Item[Enum.GetValues<Page>().Length];
    private readonly Action dismissTranslationNotice;

    private IDisposable? pushedFont;
    private IDisposable? pushedChrome;

    private Page page = Page.Announcements;
    private long pageShownTick = Environment.TickCount64;
    private bool resetScroll;
    private int tabsGeneration = -1;

    private bool pinned;
    private bool unsaved;

    private string? translationNotice;
    private int translationNoticeGeneration = -1;

    // The title is not translated: "Regions of XIV" is the plugin's name and the rest is a
    // version number. The ### part is the identity Dalamud saves this window's position and size
    // against, and it is kept from before the redesign so nobody's window moves.
    public ConfigWindow(Configuration config, PreviewStage stage, ConfigActions actions)
        : base($"Regions of XIV v{Changelog.Current}###RegionsOfXIVConfig", ShellFlags)
    {
        this.config = config;
        this.actions = actions;
        this.stage = stage;
        this.dismissTranslationNotice = DrawTranslationNoticeDismiss;

        Size = DefaultSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(HeaderBar.MinimumWidth, Layout.HeaderHeight + Layout.StageMinHeight + Layout.TabStripHeight + MinimumBodyHeight),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        AllowPinning = false;
        AllowClickthrough = false;
    }

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

    public override void OnOpen()
    {
        this.pageShownTick = Environment.TickCount64;
        this.stage.Replay();
    }

    public override void OnClose()
    {
        SetPinned(false);
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
        var tabsHeight = Layout.TabStripHeight * scale;
        var stageHeight = StageHeight(windowSize.Y, scale);
        var windowRounding = Styling.WindowRounding * scale;
        var drawList = ImGui.GetWindowDrawList();

        HeaderBar.HandleDrag(windowPos, windowSize.X, headerHeight);
        Ambient.Draw(drawList, windowPos, windowPos + windowSize);
        HeaderBar.Draw(this, windowPos, windowSize.X, headerHeight, windowRounding);

        var stageTop = windowPos.Y + headerHeight;
        DrawStage(new Vector2(windowPos.X, stageTop), windowSize.X, stageHeight);

        var tabsTop = stageTop + stageHeight;
        DrawTabs(new Vector2(windowPos.X, tabsTop), windowSize.X, tabsHeight);

        var bodyTop = tabsTop + tabsHeight;
        DrawBody(new Vector2(windowPos.X, bodyTop), new Vector2(windowSize.X, windowSize.Y - (bodyTop - windowPos.Y)));
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

    // As tall as the lines need, within a floor and a share of the window, so a huge display
    // font does not push the settings off the bottom.
    private float StageHeight(float windowHeight, float scale)
    {
        var wanted = ((Layout.StagePad * 2f) + Layout.StageToolbarHeight + 8f) * scale
                     + ((this.stage.AboveAnchor() + this.stage.BelowAnchor()) * scale);

        return Math.Clamp(wanted, Layout.StageMinHeight * scale, windowHeight * Layout.StageMaxShare);
    }

    private void DrawTabs(Vector2 origin, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Fill(drawList, origin, origin + new Vector2(width, height), Styling.WithAlpha(Styling.Surface1, 0.35f), 0f);
        Paint.Hairline(drawList, new Vector2(origin.X, origin.Y + height - 0.5f), new Vector2(origin.X + width, origin.Y + height - 0.5f));

        var padX = Layout.ContentPadding * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + padX, origin.Y));
        var selected = (int)this.page;
        if (TabStrip.Draw("##rox-tabs", TabItems(), ref selected, width - (padX * 2f), height))
        {
            Show((Page)selected);
        }
    }

    private TabStrip.Item[] TabItems()
    {
        var unseen = this.unseenChanges > 0 && this.page != Page.Changelog;
        if (this.tabsGeneration == Loc.Generation && this.tabItems[(int)Page.Changelog].Badge == unseen)
        {
            return this.tabItems;
        }

        this.tabsGeneration = Loc.Generation;
        this.tabItems[(int)Page.Announcements] = new TabStrip.Item(FontAwesomeIcon.Bullhorn, Loc.Get("announcements.tab", "Announcements"));
        this.tabItems[(int)Page.Appearance] = new TabStrip.Item(FontAwesomeIcon.Palette, Loc.Get("appearance.tab", "Appearance"));
        this.tabItems[(int)Page.Fonts] = new TabStrip.Item(FontAwesomeIcon.Font, Loc.Get("fonts.tab", "Fonts"));
        this.tabItems[(int)Page.Motion] = new TabStrip.Item(FontAwesomeIcon.Wind, Loc.Get("motion.tab", "Motion"));
        this.tabItems[(int)Page.Sound] = new TabStrip.Item(FontAwesomeIcon.VolumeUp, Loc.Get("sound.tab", "Sound"));
        this.tabItems[(int)Page.Presets] = new TabStrip.Item(FontAwesomeIcon.Swatchbook, Loc.Get("presets.tab", "Presets"));
        this.tabItems[(int)Page.Changelog] = new TabStrip.Item(FontAwesomeIcon.Newspaper, Loc.Get("changelog.tab", "What's new"), unseen);
        this.tabItems[(int)Page.About] = new TabStrip.Item(FontAwesomeIcon.InfoCircle, Loc.Get("about.tab", "About"));
        return this.tabItems;
    }

    private void DrawBody(Vector2 origin, Vector2 size)
    {
        if (size.Y < 1f)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;

        // The page column is capped and centred by padding the child, so it scrolls as one and a
        // very wide window does not stretch every row across it.
        var bodyWidth = size.X - (Layout.ContentRightInset * scale);
        var padding = Layout.ContentPadding * scale;
        var padX = MathF.Max(padding, (bodyWidth - (Layout.ContentMaxWidth * scale)) * 0.5f);

        ImGui.SetCursorScreenPos(origin);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(padX, padding * 0.8f)))
        using (var content = ImRaii.Child("##rox-page", new Vector2(bodyWidth, size.Y), false, ImGuiWindowFlags.AlwaysUseWindowPadding))
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
            case Page.Fonts:
                DrawFontsPage();
                break;
            case Page.Motion:
                DrawMotionPage();
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

    // Loc.Shipped never contains "en" -- English is the compiled-in fallback rather than a
    // bundled file -- so listing it here cannot double it up.
    private static readonly string?[] LanguageOptions = [null, "en", .. Loc.Shipped];

    private static readonly string[] LanguageNames = new string[LanguageOptions.Length];

    private static int LanguageGeneration = -1;

    internal void DrawLanguagePicker(float width)
    {
        var selected = Array.IndexOf(LanguageOptions, this.config.Language);
        if (Dropdown.Draw("##rox-language", LanguageLabels(), ref selected, width))
        {
            this.config.Language = LanguageOptions[selected];
            this.actions.ReloadLanguage();
            MarkUnsaved();
        }

        if (ImGui.IsItemHovered())
        {
            Tooltip.Show(Loc.Get(
                "about.language.tooltip",
                "Follow Dalamud takes whichever language Dalamud itself is set to, and\n" +
                "changes with it.\n\n" +
                "Only this window is affected. Place and weather names come from the game\n" +
                "and stay in whatever language your client is running in."));
        }
    }

    private static string[] LanguageLabels()
    {
        if (LanguageGeneration == Loc.Generation)
        {
            return LanguageNames;
        }

        for (var index = 0; index < LanguageOptions.Length; index++)
        {
            LanguageNames[index] = LanguageName(LanguageOptions[index]);
        }

        LanguageGeneration = Loc.Generation;
        return LanguageNames;
    }

    private static string LanguageName(string? code)
        => code is null ? Loc.Get("about.language.follow", "Follow Dalamud") : NativeLanguageName(code);

    // The runtime writes some languages' own names in lower case (français, español, português),
    // which reads as a slip in a list of names, so the first letter is raised by that language's
    // own casing rules.
    internal static string NativeLanguageName(string code)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            var name = culture.NativeName;

            return name.Length == 0 ? code : string.Concat(char.ToUpper(name[0], culture).ToString(), name.AsSpan(1));
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
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

    private void SetPinned(bool on)
    {
        this.pinned = on;
        this.actions.HoldPreview(on, Sample);
    }

    // Built on each use rather than once, because the banner language can change while the window
    // is open and the sample has to change with it. Both resolvers cache, and this is reached from
    // button presses and change handlers rather than from the draw loop; the stage has a cached
    // copy of its own for that.
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
