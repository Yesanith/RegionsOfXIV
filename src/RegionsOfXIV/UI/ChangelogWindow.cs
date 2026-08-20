using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace RegionsOfXIV.UI;

internal sealed class ChangelogWindow : Window, IDisposable
{
    private static readonly Vector4 VersionColor = new(0.875f, 0.761f, 0.584f, 1f);

    private ChangelogEntry[] entries = [];

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

    public void ShowSince(Version? lastSeen)
    {
        this.entries = Changelog.Since(lastSeen);
        this.afterUpdate = true;
        IsOpen = this.entries.Length > 0;
    }

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

        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetTextLineHeightWithSpacing();

        using (var body = ImRaii.Child("##changelog-body", new Vector2(0, -footer), false))
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
        DiscordLink.DrawButton("Join the Discord");

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Ideas, bug reports and preset codes.\n{DiscordLink.Invite}");

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
            ImGui.Bullet();
            ImGui.TextWrapped(change);
        }

        ImGui.Spacing();
    }
}
