namespace RegionsOfXIV.Models;

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
