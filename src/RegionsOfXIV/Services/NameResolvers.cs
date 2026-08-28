using System;
using System.Collections.Generic;
using Dalamud.Game;
using Lumina.Excel.Sheets;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Turning the game's own ids into words on screen: places, weather and banners.
//
// All three read from Lumina sheets rather than from the screen, so the answer is already in the
// player's language and stays correct even while the game's own text is being suppressed. The
// banner half is the awkward one -- see the note above BannerNameResolver.

// Names come from the game's own sheets rather than off the screen, so they are already in the
// player's language and correct even when the on-screen text is suppressed.
internal static class PlaceNameResolver
{
    public static string? Resolve(uint placeNameRowId)
    {
        if (placeNameRowId == 0)
            return null;

        if (!Plugin.DataManager.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var row))
            return null;

        var name = row.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    public static ResolvedLocation Resolve(in LocationSnapshot snapshot) => new(
        Resolve(snapshot.RegionPlaceNameId),
        Resolve(snapshot.ZonePlaceNameId),
        Resolve(snapshot.PlacePlaceNameId),
        Resolve(snapshot.AreaPlaceNameId),
        Resolve(snapshot.SubAreaPlaceNameId));
}

internal readonly record struct ResolvedWeather(uint Id, string Name, uint IconId);

// Turns a weather id into something showable, and works out what the weather will be without
// waiting to observe it.
//
// Each zone has its own weighted table in WeatherRate; EorzeaWeather turns the clock into a
// number from 0 to 99 and that table turns the number into the weather, which is how the client
// and the server stay in agreement without exchanging anything.
internal static class WeatherNameResolver
{
    public static ResolvedWeather? Forecast(uint territoryTypeId, DateTimeOffset at)
    {
        if (Rates(territoryTypeId) is not { } rate)
            return null;

        var rates = rate.Rate;

        Span<byte> weights = stackalloc byte[rates.Count];
        for (var i = 0; i < rates.Count; i++)
            weights[i] = rates[i];

        var index = EorzeaWeather.Pick(EorzeaWeather.Chance(at), weights);

        return index < 0 ? null : Resolve(rate.Weather[index].RowId);
    }

    private static WeatherRate? Rates(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
            return null;

        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryTypeId, out var territory))
            return null;

        if (territory.WeatherRate.ValueNullable is not { } rate)
            return null;

        return rate.Rate.Count == 0 ? null : rate;
    }

    public static ResolvedWeather? Resolve(uint weatherRowId)
    {
        if (weatherRowId == 0)
            return null;

        if (!Plugin.DataManager.GetExcelSheet<Weather>().TryGetRow(weatherRowId, out var row))
            return null;

        var name = row.Name.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var icon = row.Icon;

        return new ResolvedWeather(weatherRowId, name.Trim(), icon > 0 ? (uint)icon : 0u);
    }
}

// A banner's wording exists only as pixels inside its artwork, so there is no string to read.
// Two sources are crossed to recover it: GroupPoseStamp names a dozen or so of the same icons for
// the gpose stamp picker and is localised by the game, and ScreenImage says which of those icons
// are actually used as banners. Anything left over falls back to a hand-read English table, which
// is why that half only applies on an English client.
//
// ScreenImage is also where the ids themselves come from, which is worth stating plainly because
// it is easy to assume otherwise: the sheet lists far more banners than anything has a name for,
// so the gap is names, not discovery.
internal static class BannerNameResolver
{
    private static (Dictionary<uint, string> Named, HashSet<uint> Ids)? loaded;

    private static (Dictionary<uint, string> Named, HashSet<uint> Ids) Data => loaded ??= Build();

    public static IReadOnlyDictionary<uint, string> All => Data.Named;

    // Every banner id the plugin knows of, named or not. The unnamed ones are the working set for
    // /regions preview; they are useless to Resolve and deliberately invisible to it.
    public static IReadOnlySet<uint> KnownIds => Data.Ids;

    public static string? Resolve(uint iconId) =>
        iconId != 0 && All.TryGetValue(iconId, out var name) ? name : null;

    private static (Dictionary<uint, string> Named, HashSet<uint> Ids) Build()
    {
        var stamps = new Dictionary<uint, string>();

        foreach (var stamp in Plugin.DataManager.GetExcelSheet<GroupPoseStamp>())
        {
            if (stamp.StampIcon <= 0)
                continue;

            var name = stamp.Name.ToString();

            if (!string.IsNullOrWhiteSpace(name))
                stamps.TryAdd((uint)stamp.StampIcon, name.Trim());
        }

        var ids = new HashSet<uint>();
        var found = new Dictionary<uint, string>();

        foreach (var image in Plugin.DataManager.GetExcelSheet<ScreenImage>())
        {
            // Lang marks an image that ships once per client language, which is the same thing as
            // saying its wording is painted in. The rest of the sheet -- a quarter of it -- is
            // 1280x720 cutscene stills with no text in them at all, and listing those as banners
            // waiting to be named would bury the ones that are.
            if (image.Image == 0 || !image.Lang)
                continue;

            if (!ids.Add(image.Image))
                continue;

            if (stamps.TryGetValue(image.Image, out var name))
                found[image.Image] = name;
        }

        var fromScreenImage = ids.Count;
        var fromStamps = found.Count;

        if (Plugin.ClientState.ClientLanguage == ClientLanguage.English)
        {
            foreach (var (icon, name) in BannerNames.English)
            {
                found.TryAdd(icon, name);

                // A handful of banners that have been walked into in-game are not in ScreenImage
                // at all, so the sheet is a floor rather than the whole truth. Folding the named
                // ones back in keeps the invariant the preview window relies on: every id that
                // has a name is an id you can fire.
                ids.Add(icon);
            }
        }

        Log.Debug(
            $"Banners: {ids.Count} ids known ({fromScreenImage} from ScreenImage), {found.Count} of them named "
            + $"-- {fromStamps} from stamps, {found.Count - fromStamps} from the English table.");

        return (found, ids);
    }
}
