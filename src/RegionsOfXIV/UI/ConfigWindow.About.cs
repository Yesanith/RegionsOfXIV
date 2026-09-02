using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private const string RepositoryUrl = "https://github.com/Yesanith/RegionsOfXIV";

    private const string IssuesUrl = RepositoryUrl + "/issues";

    private const string InspirationUrl =
        "https://blishhud.com/modules/?module=Nekres.Regions_Of_Tyria";

    private static readonly Vector4 HeadingColor = new(0.875f, 0.761f, 0.584f, 1f);

    // Each is placed in the row and then drawn, so both need the same string -- including the
    // "###key" that keeps the button's identity steady when its wording changes.
    private static string WhatsNew => Loc.Label("about.whatsnew", "What's new");

    private static string JoinDiscord => Loc.Label("about.discord", "Join the Discord");

    private static string GitHub => Loc.Label("about.github", "GitHub");

    private static string ReportIssue => Loc.Label("about.issue", "Report an issue");

    private void DrawAboutTab()
    {
        using var tab = ImRaii.TabItem(Loc.Label("about.tab", "About"));
        if (!tab) return;

        // First on the tab, above the title, and not because a setting belongs at the top of an
        // otherwise informational page. Each language is named in its own language so somebody who
        // has landed in one they cannot read can find their way out, and that only works if the
        // control is findable by position rather than by reading the prose below it first.
        DrawLanguage();

        ImGui.Separator();

        UiText.Colored(
            HeadingColor,
            Loc.Format("about.title", "Regions of XIV {0}", Changelog.Current));
        UiText.Disabled(Loc.Format("about.author", "by {0}", "Yesanith"));

        ImGui.Spacing();

        UiText.Wrapped(Loc.Get(
            "about.summary",
            "Announces the region, zone, area and sub-area you walk into, and the weather "
            + "while you are there, replacing the game's own location text rather than "
            + "drawing alongside it."));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var row = new WrappingRow();

        var whatsNew = WhatsNew;
        row.Place(whatsNew);
        if (ImGui.Button(whatsNew))
            this.actions.ShowChangelog();

        UiText.Tooltip(Loc.Get(
            "about.whatsnew.tooltip", "Every release, newest first. Also at \"/regions changelog\"."));

        var discord = JoinDiscord;
        row.Place(discord);
        DrawDiscordLink(discord, Loc.Get(
            "about.discord.tooltip", "Ideas, bug reports and preset codes."));

        var github = GitHub;
        row.Place(github);
        Link(github, RepositoryUrl, Loc.Get(
            "about.github.tooltip", "The source, the releases, and the licence."));

        var issue = ReportIssue;
        row.Place(issue);
        Link(issue, IssuesUrl, Loc.Get(
            "about.issue.tooltip", "Something wrong, or something missing."));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        UiText.Colored(HeadingColor, Loc.Get("about.commands", "Commands"));

        Command("/regions", Loc.Get("about.command.settings", "Opens these settings."));
        Command("/regions test", Loc.Get(
            "about.command.test", "Shows a notification for where you are standing."));
        Command("/regions changelog", Loc.Get(
            "about.command.changelog", "Opens the What's New window."));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        UiText.Wrapped(Loc.Get(
            "about.inspiration", "Inspired by Nekres' Regions of Tyria for Guild Wars 2."));

        if (ImGui.IsItemClicked())
            Util.OpenLink(InspirationUrl);

        UiText.Tooltip(Loc.Format("about.inspiration.tooltip", "Click to open {0}", InspirationUrl));

        DisabledWrapped(Loc.Get("about.licence", "Licensed under AGPL-3.0-or-later."));

        DisabledWrapped(Loc.Get(
            "about.notaffiliated",
            "Place names, weather and fonts come from the game's own data. "
            + "Not affiliated with Square Enix."));
    }

    private void DrawLanguage()
    {
        using (var combo = ImRaii.Combo(
                   Loc.Label("about.language", "Language"), LanguageName(this.config.Language)))
        {
            if (combo)
            {
                foreach (var option in LanguageOptions)
                {
                    // The code is the identity, not the name -- two languages whose native names
                    // happened to render alike would otherwise be one entry.
                    if (!ImGui.Selectable(
                            $"{LanguageName(option)}###language-{option ?? "dalamud"}",
                            option == this.config.Language))
                        continue;

                    this.config.Language = option;
                    this.actions.ReloadLanguage();
                    MarkUnsaved();
                }
            }
        }

        UiText.Tooltip(Loc.Get(
            "about.language.tooltip",
            "Follow Dalamud takes whichever language Dalamud itself is set to, and\n" +
            "changes with it.\n\n" +
            "Only this window is affected. Place and weather names come from the game\n" +
            "and stay in whatever language your client is running in."));
    }

    // Loc.Shipped never contains "en" -- English is the compiled-in fallback rather than a
    // bundled file -- so listing it here cannot double it up.
    private static string?[] LanguageOptions => [null, "en", .. Loc.Shipped];

    private static string LanguageName(string? code)
    {
        if (code is null)
            return Loc.Get("about.language.follow", "Follow Dalamud");

        try
        {
            return CultureInfo.GetCultureInfo(code).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    private static void Link(string label, string url, string tooltip)
    {
        if (ImGui.Button(label))
            Util.OpenLink(url);

        UiText.Tooltip(Loc.Format("common.opens", "{0}\n\nOpens {1} in your browser.", tooltip, url));
    }

    private static void Command(string command, string what)
    {
        ImGui.Bullet();
        UiText.Colored(HeadingColor, command);
        ImGui.SameLine();

        ImGui.PushTextWrapPos(0f);
        DisabledWrapped(what);
        ImGui.PopTextWrapPos();
    }
}
