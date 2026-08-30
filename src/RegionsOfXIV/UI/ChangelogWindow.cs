using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed class ChangelogWindow : Window, IDisposable
{
    private static readonly Vector4 VersionColor = new(0.875f, 0.761f, 0.584f, 1f);

    private ChangelogEntry[] entries = [];

    private bool afterUpdate;

    public ChangelogWindow()
        : base(Title)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 180),
            MaximumSize = new Vector2(640, 720),
        };

        Size = new Vector2(460, 340);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    // Rebuilt whenever the window opens rather than only in the constructor: the title is
    // translated, and one set once would keep whichever language the game started in. Everything
    // after "###" is the identity Dalamud saves this window's position and size against, so it
    // stays out of the translation.
    private static string Title =>
        Loc.Get("changelog.title", "Regions of XIV: What's New") + "###RegionsOfXIVChangelog";

    // Shown once after an update, listing only what is new to this player. Opens itself only if
    // there is something to say, so a reinstall at the same version stays quiet.
    public void ShowSince(Version? lastSeen)
    {
        this.entries = Changelog.Since(lastSeen);
        this.afterUpdate = true;
        WindowName = Title;
        IsOpen = this.entries.Length > 0;
    }

    public void ShowAll()
    {
        this.entries = Changelog.All;
        this.afterUpdate = false;
        WindowName = Title;
        IsOpen = this.entries.Length > 0;
    }

    public override void Draw()
    {
        if (this.entries.Length == 0)
        {
            IsOpen = false;
            return;
        }

        UiText.Wrapped(!this.afterUpdate
            ? Loc.Get("changelog.all", "Every release, newest first:")
            : this.entries.Length == 1
                ? Loc.Get(
                    "changelog.updated", "Regions of XIV has updated. Here is what changed:")
                : Loc.Get(
                    "changelog.updated.away",
                    "Regions of XIV has updated. Here is what changed while you were away:"));

        ImGui.Spacing();

        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetTextLineHeightWithSpacing();

        using (var body = ImRaii.Child("##changelog-body", new Vector2(0, -footer), false))
        {
            if (body)
            {
                foreach (var entry in this.entries)
                    DrawEntry(entry);
            }
        }

        if (ImGui.Button(Loc.Label("changelog.close", "Close")))
            IsOpen = false;

        ImGui.SameLine();
        DiscordLink.DrawButton(Loc.Label("changelog.discord", "Join the Discord"));

        UiText.Tooltip(Loc.Format(
            "changelog.discord.tooltip",
            "Ideas, bug reports and preset codes.\n{0}",
            DiscordLink.Invite));

        UiText.Disabled(this.afterUpdate
            ? Loc.Get(
                "changelog.onceonly", "You will not see this again until the next update.")
            : Loc.Format(
                "changelog.onrequest",
                "Shown because you asked: \"{0}\".",
                "/regions changelog"));
    }

    private static void DrawEntry(ChangelogEntry entry)
    {
        UiText.Colored(VersionColor, entry.Version.ToString());
        ImGui.Separator();

        foreach (var change in entry.Changes)
        {
            ImGui.Bullet();

            // Release notes stay English, but they go through UiText all the same: a per-cent sign
            // in one would otherwise be read as a format specifier.
            UiText.Wrapped(change);
        }

        ImGui.Spacing();
    }
}
