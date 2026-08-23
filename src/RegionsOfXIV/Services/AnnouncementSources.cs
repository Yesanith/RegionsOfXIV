using System;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Everything the coordinator is allowed to know about the outside world. Each of these has one
// real implementation backed by the game and one fake in the tests, which is what keeps the
// announcement rules testable without launching FFXIV.
//
// ZoneArrival exists so Dalamud's own event argument type does not leak past this boundary.
internal readonly record struct AnnouncementSources(
    ILocationSource Locations,
    IWeatherSource Weather,
    IAreaTextSource AreaText,
    IBannerSource Banners,
    IZoneArrivals Zones,
    IPlaceNames PlaceNames,
    IWeatherNames WeatherNames);

internal interface ILocationSource
{
    event Action<LocationSnapshot, LocationSnapshot>? OnLocationChanged;

    event Action<bool>? OnSanctuaryChanged;

    LocationSnapshot Current { get; }

    float Speed { get; }

    void Poll();
}

internal interface IWeatherSource
{
    event Action<byte>? OnWeatherChanged;

    byte Current { get; }

    void Prime(uint weatherId);

    void Reset();
}

internal interface IAreaTextSource
{
    event Action<string?>? OnAreaTextShown;
}

internal interface IBannerSource
{
    event Action<uint, string>? OnBannerShown;
}

internal readonly record struct ZoneArrival(
    uint TerritoryTypeId,
    uint PlaceNameId,
    uint ZonePlaceNameId,
    uint RegionPlaceNameId,
    bool IsPvp,
    bool IsDuty);

internal interface IZoneArrivals
{
    event Action<ZoneArrival>? Arrived;

    event Action? LoggedOut;
}

internal interface IPlaceNames
{
    string? Resolve(uint placeNameRowId);

    ResolvedLocation Resolve(in LocationSnapshot snapshot);
}

internal interface IWeatherNames
{
    ResolvedWeather? Resolve(uint weatherRowId);

    ResolvedWeather? Forecast(uint territoryTypeId, DateTimeOffset at);
}
