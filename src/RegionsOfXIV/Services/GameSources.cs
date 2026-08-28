using System;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// The real implementations behind AnnouncementSources: thin adapters from Dalamud events and
// sheet lookups onto the plugin's own types. Deliberately free of decisions -- anything worth
// testing belongs in the coordinator, not here.
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

internal sealed class DalamudGameState : IGameState
{
    public bool IsLoggedIn => Plugin.ClientState.IsLoggedIn;

    public bool IsBetweenAreas =>
        Plugin.Condition[ConditionFlag.BetweenAreas] ||
        Plugin.Condition[ConditionFlag.BetweenAreas51];

    public bool IsInCutscene =>
        Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
        Plugin.Condition[ConditionFlag.WatchingCutscene] ||
        Plugin.Condition[ConditionFlag.WatchingCutscene78];

    public bool IsPvP => Plugin.ClientState.IsPvP;

    public bool IsGPosing => Plugin.ClientState.IsGPosing;

    public bool IsInCombat => Plugin.Condition[ConditionFlag.InCombat];

    public bool IsBoundByDuty => Plugin.Condition[ConditionFlag.BoundByDuty];
}
