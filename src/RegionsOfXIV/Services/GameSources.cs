using System;
using Dalamud.Game.ClientState;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

internal sealed class GameZoneArrivals : IZoneArrivals, IDisposable
{
    public event Action<ZoneArrival>? Arrived;

    public event Action? LoggedOut;

    public GameZoneArrivals()
    {
        Plugin.ClientState.ZoneInit += OnZoneInit;
        Plugin.ClientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        Plugin.ClientState.ZoneInit -= OnZoneInit;
        Plugin.ClientState.Logout -= OnLogout;
    }

    private void OnZoneInit(ZoneInitEventArgs args)
    {
        if (args.TerritoryType.ValueNullable is not { } territory)
            return;

        Arrived?.Invoke(new ZoneArrival(
            territory.RowId,
            territory.PlaceName.RowId,
            territory.PlaceNameZone.RowId,
            territory.PlaceNameRegion.RowId,
            territory.IsPvpZone,
            args.ContentFinderCondition.RowId != 0));
    }

    private void OnLogout(int type, int code) => LoggedOut?.Invoke();
}

internal sealed class GamePlaceNames : IPlaceNames
{
    public string? Resolve(uint placeNameRowId) => PlaceNameResolver.Resolve(placeNameRowId);

    public ResolvedLocation Resolve(in LocationSnapshot snapshot) => PlaceNameResolver.Resolve(snapshot);
}

internal sealed class GameWeatherNames : IWeatherNames
{
    public ResolvedWeather? Resolve(uint weatherRowId) => WeatherNameResolver.Resolve(weatherRowId);

    public ResolvedWeather? Forecast(uint territoryTypeId, DateTimeOffset at) =>
        WeatherNameResolver.Forecast(territoryTypeId, at);
}
