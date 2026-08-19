using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace RegionsOfXIV.UI;

// What changed, shown once after an update and then not again.
//
// Its own window rather than a tab on the config window: it has to be able to
// open itself, and opening the settings unasked to deliver a paragraph would put
// the player in front of five tabs they did not ask to see. Closing this one is
// the whole of the interaction.
//
// Nothing opens it by hand. "Once, after an update" is the entire contract — see
// Plugin, which decides whether to call ShowSince and records the version at the
// moment it does.
internal sealed class ChangelogWindow : Window, IDisposable
{
    private static readonly Vector4 VersionColor = new(0.875f, 0.761f, 0.584f, 1f);

    private ChangelogEntry[] entries = [];

    // Whether this opening was an update announcing itself or somebody asking. Only
    // the wording differs, but the wrong one is actively misleading: telling a
    // player who typed the command that the plugin has just updated, and that they
    // will not see this again, is two false statements in one window.
    private bool afterUpdate;

    public ChangelogWindow()
        : base("Regions of XIV — What's New###RegionsOfXIVChangelog")
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

    // Opens only if there is something to say. A build that adds no entry — a
    // hotfix, a rebuild — passes silently rather than showing an empty window.
    public void ShowSince(Version? lastSeen)
    {
        this.entries = Changelog.Since(lastSeen);
        this.afterUpdate = true;
        IsOpen = this.entries.Length > 0;
    }

    // Every release, for "/regions changelog".
    //
    // Not Since(null), which answers "what did you miss" with the newest entry
    // alone. Somebody typing the command is not asking what changed since anything
    // in particular — they want the page.
    public void ShowAll()
    {
        this.entries = Changelog.All;
        this.afterUpdate = false;
        IsOpen = this.entries.Length > 0;
    }

    public override void Draw()
    {
        if (this.entries.Length == 0)
        {
            IsOpen = false;
            return;
        }

        ImGui.TextWrapped(!this.afterUpdate
            ? "Every release, newest first:"
            : this.entries.Length == 1
                ? "Regions of XIV has updated. Here is what changed:"
                : "Regions of XIV has updated. Here is what changed while you were away:");

        ImGui.Spacing();

        // Scrolls on its own so the close button below stays put, however many
        // releases the reader has to catch up on.
        using (var body = ImRaii.Child("##changelog-body", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), false))
        {
            if (body)
            {
                foreach (var entry in this.entries)
                    DrawEntry(entry);
            }
        }

        if (ImGui.Button("Close"))
            IsOpen = false;

        ImGui.SameLine();
        ImGui.TextDisabled(this.afterUpdate
            ? "You will not see this again until the next update."
            : "Shown because you asked — \"/regions changelog\".");
    }

    private static void DrawEntry(ChangelogEntry entry)
    {
        ImGui.TextColored(VersionColor, entry.Version.ToString());
        ImGui.Separator();

        foreach (var change in entry.Changes)
        {
            // Bullet leaves the cursor on the same line, so the wrapped text lines
            // up beside the marker rather than under it — which BulletText, which
            // does not wrap at all, would not do either way.
            ImGui.Bullet();
            ImGui.TextWrapped(change);
        }

        ImGui.Spacing();
    }
}
