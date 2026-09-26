using Dalamud.Utility;

namespace RegionsOfXIV.UI;

internal static class DiscordLink
{
    public const string Invite = "https://discord.com/invite/ax2gsRqvpa";

    public static void Open() => Util.OpenLink(Invite);
}
