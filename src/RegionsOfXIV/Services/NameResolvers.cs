using System;
using Lumina.Excel.Sheets;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Turning the game's own ids into words on screen: places and weather.
//
// Both read from Lumina sheets rather than from the screen, so the answer is already in the
// player's language and stays correct even while the game's own text is being suppressed.
//
// Banners are the awkward third case and live in BannerNameResolver.cs: their wording is painted
// into the artwork, so there is no sheet to read it from.

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
