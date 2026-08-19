using Lumina.Excel.Sheets;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

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
