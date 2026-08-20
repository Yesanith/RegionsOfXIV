using System.Collections.Generic;
using Dalamud.Game;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

internal static class BannerNameResolver
{
    private static Dictionary<uint, string>? banners;

    public static IReadOnlyDictionary<uint, string> All => banners ??= Build();

    public static string? Resolve(uint iconId) =>
        iconId != 0 && All.TryGetValue(iconId, out var name) ? name : null;

    private static Dictionary<uint, string> Build()
    {
        var named = new Dictionary<uint, string>();

        foreach (var stamp in Plugin.DataManager.GetExcelSheet<GroupPoseStamp>())
        {
            if (stamp.StampIcon <= 0)
                continue;

            var name = stamp.Name.ToString();

            if (!string.IsNullOrWhiteSpace(name))
                named.TryAdd((uint)stamp.StampIcon, name.Trim());
        }

        var found = new Dictionary<uint, string>();

        foreach (var image in Plugin.DataManager.GetExcelSheet<ScreenImage>())
        {
            if (image.Image == 0)
                continue;

            if (named.TryGetValue(image.Image, out var name))
                found[image.Image] = name;
        }

        var fromStamps = found.Count;

        if (Plugin.ClientState.ClientLanguage == ClientLanguage.English)
        {
            foreach (var (icon, name) in BannerNames.English)
                found.TryAdd(icon, name);
        }

        Plugin.Log.Debug(
            $"Banner names: {found.Count} known, {fromStamps} of them from stamps.");

        return found;
    }
}
