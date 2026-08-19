using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

/// <summary>A weather as it is shown: the name the client uses, and its game icon.</summary>
internal readonly record struct ResolvedWeather(string Name, uint IconId);

internal static class WeatherNameResolver
{
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

        return new ResolvedWeather(name.Trim(), icon > 0 ? (uint)icon : 0u);
    }
}
