namespace RegionsOfXIV.Models;

// Location as row IDs only. Resolution to display strings happens in
// PlaceNameResolver, so a language change doesn't require re-reading game memory.
//
// Region/Zone/Place come from the TerritoryType sheet; Area/SubArea from
// TerritoryInfo in game memory. Example: La Noscea / Vylbrand / Middle La Noscea
// / Summerford / Summerford Farms.
public readonly record struct LocationSnapshot(
    uint TerritoryTypeId,
    uint RegionPlaceNameId,
    uint ZonePlaceNameId,
    uint PlacePlaceNameId,
    uint AreaPlaceNameId,
    uint SubAreaPlaceNameId)
{
    public static readonly LocationSnapshot Empty = new(0, 0, 0, 0, 0, 0);

    public bool IsEmpty => TerritoryTypeId == 0;

    // Returns the coarsest tier that differs, because a zone change implies an
    // area change and only the outermost one should be announced.
    public LocationTier DiffTier(in LocationSnapshot other)
    {
        if (TerritoryTypeId != other.TerritoryTypeId) return LocationTier.Territory;
        if (RegionPlaceNameId != other.RegionPlaceNameId) return LocationTier.Region;
        if (ZonePlaceNameId != other.ZonePlaceNameId) return LocationTier.Zone;
        if (PlacePlaceNameId != other.PlacePlaceNameId) return LocationTier.Place;
        if (AreaPlaceNameId != other.AreaPlaceNameId) return LocationTier.Area;
        if (SubAreaPlaceNameId != other.SubAreaPlaceNameId) return LocationTier.SubArea;
        return LocationTier.None;
    }
}

// Coarsest to finest. Order matters: comparisons rely on it.
public enum LocationTier
{
    None = 0,
    Territory,
    Region,
    Zone,
    Place,
    Area,
    SubArea,
}
