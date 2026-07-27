using Lumina.Excel.Sheets;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// PlaceName row ids to display strings. Lumina reads the sheet for whatever
// language the client is running, so nothing here needs to know which that is.
internal static class PlaceNameResolver
{
    public static string? Resolve(uint placeNameRowId)
    {
        if (placeNameRowId == 0)
            return null;

        if (!Plugin.DataManager.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var row))
            return null;

        // Name is a ReadOnlySeString. ToString() strips payloads, which is what we
        // want for display; ToMacroString() is the one to reach for when debugging
        // an odd-looking name.
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
