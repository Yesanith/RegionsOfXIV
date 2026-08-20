using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private const string RepositoryUrl = "https://github.com/Yesanith/RegionsOfXIV";

    private const string IssuesUrl = RepositoryUrl + "/issues";

    private const string InspirationUrl =
        "https://blishhud.com/modules/?module=Nekres.Regions_Of_Tyria";

    private static readonly Vector4 HeadingColor = new(0.875f, 0.761f, 0.584f, 1f);

    private const string WhatsNew = "What's new";
    private const string JoinDiscord = "Join the Discord";
    private const string GitHub = "GitHub";
    private const string ReportIssue = "Report an issue";

    private void DrawAboutTab()
    {
        using var tab = ImRaii.TabItem("About");
        if (!tab) return;

        ImGui.TextColored(HeadingColor, $"Regions of XIV {Changelog.Current}");
        ImGui.TextDisabled("by Yesanith");

        ImGui.Spacing();

        ImGui.TextWrapped(
            "Announces the region, zone, area and sub-area you walk into, and the weather "
            + "while you are there, replacing the game's own location text rather than "
            + "drawing alongside it.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Four buttons do not fit side by side at the window's smallest width.
        var row = new WrappingRow();

        row.Place(WhatsNew);
        if (ImGui.Button(WhatsNew))
            this.actions.ShowChangelog();

        Tooltip("Every release, newest first. Also at \"/regions changelog\".");

        row.Place(JoinDiscord);
        DrawDiscordLink(JoinDiscord, "Ideas, bug reports and preset codes.");

        row.Place(GitHub);
        Link(GitHub, RepositoryUrl, "The source, the releases, and the licence.");

        row.Place(ReportIssue);
        Link(ReportIssue, IssuesUrl, "Something wrong, or something missing.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(HeadingColor, "Commands");

        Command("/regions", "Opens these settings.");
        Command("/regions test", "Shows a notification for where you are standing.");
        Command("/regions changelog", "Opens the What's New window.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped("Inspired by Nekres' Regions of Tyria for Guild Wars 2.");

        if (ImGui.IsItemClicked())
            Util.OpenLink(InspirationUrl);

        Tooltip($"Click to open {InspirationUrl}");

        DisabledWrapped("Licensed under AGPL-3.0-or-later.");

        DisabledWrapped(
            "Place names, weather and fonts come from the game's own data. "
            + "Not affiliated with Square Enix.");
    }

    private static void Link(string label, string url, string tooltip)
    {
        if (ImGui.Button(label))
            Util.OpenLink(url);

        Tooltip($"{tooltip}\n\nOpens {url} in your browser.");
    }

    private static void Command(string command, string what)
    {
        ImGui.Bullet();
        ImGui.TextColored(HeadingColor, command);
        ImGui.SameLine();

        // Wraps at the window edge rather than running off it on a narrow window.
        ImGui.PushTextWrapPos(0f);
        DisabledWrapped(what);
        ImGui.PopTextWrapPos();
    }
}
