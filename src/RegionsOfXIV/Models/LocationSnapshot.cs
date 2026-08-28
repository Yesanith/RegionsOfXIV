namespace RegionsOfXIV.Models;

// Where the player is, at every tier the game tracks, as one comparable value. Being a record
// struct is what lets NotificationGate keep a short history of recent places and compare them
// directly to spot walking back and forth over a boundary.
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

    // Returns the coarsest tier that differs, and the order of these checks is the whole point:
    // walking into a new zone also changes every finer id under it, and that should announce as
    // a zone change rather than as a sub-area one.
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

// Ordered coarse to fine, and the gate compares these with < and > to decide which announcements
// suppress which. Do not reorder.
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

public readonly record struct ResolvedLocation(
    string? Region,
    string? Zone,
    string? Place,
    string? Area,
    string? SubArea);
