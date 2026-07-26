using RegionsOfXIV.Models;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

internal sealed class PlaceNameResolver
{
    public string? Resolve(uint placeNameRowId)
    {
        if (placeNameRowId == 0)
            return null;

        if (!Plugin.DataManager.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var row))
            return null;

        // Name is a ReadOnlySeString. ToString() strips payloads, which is what we
        // want for display; ToMacroString() is the one to reach for when debugging
        // an odd-looking name.
        var name = row.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? null : Sanitize(name);
    }

    public ResolvedLocation Resolve(in LocationSnapshot snapshot) => new(
        Resolve(snapshot.RegionPlaceNameId),
        Resolve(snapshot.ZonePlaceNameId),
        Resolve(snapshot.PlacePlaceNameId),
        Resolve(snapshot.AreaPlaceNameId),
        Resolve(snapshot.SubAreaPlaceNameId));

    // Deliberately almost a no-op. The GW2 original strips "((123456))"
    // placeholders, text before ':' and trailing "(...)" — all quirks of the GW2
    // web API that FFXIV does not share. Add rules here only when real observed
    // data demands it (ROADMAP Phase 2).
    private static string Sanitize(string text) => text.Trim();
}

internal readonly record struct ResolvedLocation(
    string? Region,
    string? Zone,
    string? Place,
    string? Area,
    string? SubArea)
{
    public string? Finest => SubArea ?? Area ?? Place ?? Zone ?? Region;
}
