using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

internal enum TerritoryKind
{
    Other,
    City,
    Housing,
}

// What sort of place a territory is, and what it is called, off the TerritoryType sheet. Cached
// per id because the gate asks on every location change and the quiet-zone list asks every frame
// it is drawn; a sheet lookup is cheap but not that cheap.
internal static class TerritoryKinds
{
    // TerritoryIntendedUse rows: 0 is a town, 13 a housing ward, 14 a housing interior.
    private const uint TownUse = 0;

    private const uint HousingWardUse = 13;

    private const uint HousingInteriorUse = 14;

    private static readonly Dictionary<uint, TerritoryKind> Kinds = new();

    private static readonly Dictionary<uint, string?> Names = new();

    private static readonly object Gate = new();

    public static TerritoryKind Of(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
            return TerritoryKind.Other;

        lock (Gate)
        {
            if (Kinds.TryGetValue(territoryTypeId, out var known))
                return known;

            var kind = TerritoryKind.Other;

            if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryTypeId, out var row))
            {
                kind = row.TerritoryIntendedUse.RowId switch
                {
                    TownUse => TerritoryKind.City,
                    HousingWardUse or HousingInteriorUse => TerritoryKind.Housing,
                    _ => TerritoryKind.Other,
                };
            }

            Kinds[territoryTypeId] = kind;

            return kind;
        }
    }

    // The place name the territory carries, for listing a quiet zone by something a player
    // recognises rather than by its number.
    public static string? NameOf(uint territoryTypeId)
    {
        lock (Gate)
        {
            if (Names.TryGetValue(territoryTypeId, out var known))
                return known;

            string? name = null;

            if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryTypeId, out var row))
                name = PlaceNameResolver.Resolve(row.PlaceName.RowId);

            Names[territoryTypeId] = name;

            return name;
        }
    }
}
