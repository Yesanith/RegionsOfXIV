using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace RegionsOfXIV.UI;

/// <summary>The invite, in one place, for the windows that offer it.</summary>
internal static class DiscordLink
{
    public const string Invite = "https://discord.com/invite/ax2gsRqvpa";

    public static void DrawButton(string label)
    {
        if (ImGui.Button(label))
            Util.OpenLink(Invite);
    }
}
