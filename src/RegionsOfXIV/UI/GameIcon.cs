using System;
using System.Collections.Generic;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace RegionsOfXIV.UI;

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
