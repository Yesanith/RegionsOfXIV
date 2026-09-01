using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Config;
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

// The game's own sound settings, and whether its window is in front, read for the file playback
// path. Nothing else in the plugin asks: a game sound is mixed by the game and obeys all of this
// without being told.
//
// Free of decisions, like the rest of this file. What the numbers mean is in GameMixerRules,
// including which meanings were verified against a running client and which are inferred.
internal sealed class GameAudio : IGameAudio
{
    // Read once and asked about twice, because "could not be read" and "is set to zero" are
    // different answers and the second is a real setting a player can choose.
    public bool Readable => Level(SystemConfigOption.SoundMaster).HasValue;

    public bool SoundDisabled => Flag(SystemConfigOption.IsSoundDisable);

    public bool MasterMuted => Flag(SystemConfigOption.IsSndMaster);

    public bool SystemMuted => Flag(SystemConfigOption.IsSndSystem);

    public int MasterVolume => Level(SystemConfigOption.SoundMaster) ?? 0;

    public int SystemVolume => Level(SystemConfigOption.SoundSystem) ?? 0;

    public bool UnfocusedSoundAllowed => Flag(SystemConfigOption.IsSoundAlways);

    public bool UnfocusedSystemSoundAllowed => Flag(SystemConfigOption.IsSoundSystemAlways);

    // Asked of Windows rather than of the game. The client exposes nothing that says "I am the
    // active window" without reading a struct that Square Enix reshuffles between patches, and the
    // foreground process is the same answer from a source that does not move. It is also the right
    // answer for a windowed client that has been clicked away from.
    public bool WindowFocused
    {
        get
        {
            var window = GetForegroundWindow();

            if (window == IntPtr.Zero)
                return false;

            return GetWindowThreadProcessId(window, out var owner) != 0
                   && owner == GetCurrentProcessId();
        }
    }

    // Unreadable is answered as "not set". On its own that changes nothing: the volumes above
    // report zero in the same situation, and GameMixerRules refuses on that before it ever asks
    // whether something is muted.
    private static bool Flag(SystemConfigOption option) =>
        Plugin.GameConfig.TryGet(option, out uint value) && value != 0;

    private static int? Level(SystemConfigOption option) =>
        Plugin.GameConfig.TryGet(option, out uint value) ? (int)value : null;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();
}
