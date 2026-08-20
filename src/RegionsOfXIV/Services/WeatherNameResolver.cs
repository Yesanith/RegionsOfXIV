using System;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

/// <summary>A weather as it is shown: the name the client uses, and its game icon.</summary>
internal readonly record struct ResolvedWeather(uint Id, string Name, uint IconId);

internal static class WeatherNameResolver
{
    /// <summary>
    /// What the weather will be in a territory at a given moment, worked out from the
    /// clock and the zone's own table rather than read from the running game. This is
    /// known before the loading screen ends, which is what lets the weather line land
    /// alongside the place name instead of seconds behind it.
    /// </summary>
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
