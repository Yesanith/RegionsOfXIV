using System;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

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
