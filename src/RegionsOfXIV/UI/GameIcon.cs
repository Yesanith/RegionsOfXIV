using System;
using System.Collections.Generic;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace RegionsOfXIV.UI;

// Icon ids arrive from game sheets and from the banner watcher, and not all of them resolve to
// artwork the client actually has. A miss is remembered so a bad id costs one failed lookup
// rather than one per frame for as long as the notification is up.
internal static class GameIcon
{
    private static readonly HashSet<uint> Missing = [];

    public static IDalamudTextureWrap? Get(uint iconId)
    {
        if (iconId == 0 || Missing.Contains(iconId))
            return null;

        try
        {
            return Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId))
                .TryGetWrap(out var wrap, out _)
                ? wrap
                : null;
        }
        catch (Exception)
        {
            Missing.Add(iconId);
            return null;
        }
    }
}
