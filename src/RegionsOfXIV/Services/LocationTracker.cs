using System;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using RegionsOfXIV.Models;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

internal sealed unsafe class LocationTracker : IDisposable
{
    // A zone transition is a human-scale event; polling per-frame would pay the
    // cost 100+ times a second for no benefit.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private DateTime nextPoll = DateTime.MinValue;

    // Raised on the framework thread with (previous, current).
    public event Action<LocationSnapshot, LocationSnapshot>? LocationChanged;

    // Raised when the player crosses into or out of a sanctuary. Separate from
    // LocationChanged because the two do not always coincide: settlements can be
    // entered without any TerritoryInfo place name moving.
    public event Action<bool>? SanctuaryChanged;

    public LocationSnapshot Current { get; private set; } = LocationSnapshot.Empty;

    public bool InSanctuary { get; private set; }

    private bool wasLoading;

    public LocationTracker()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.ClientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.ClientState.Logout -= OnLogout;
    }

    // Forget where we were, so the next poll re-announces.
    public void Reset()
    {
        Current = LocationSnapshot.Empty;
        InSanctuary = false;
        this.wasLoading = false;
    }

    // Read now rather than waiting out the interval, for when something else has
    // already established that the location changed — the game raising its own
    // area text, for one. Framework thread only, same as the scheduled poll.
    public void Poll()
    {
        this.nextPoll = DateTime.UtcNow + PollInterval;
        Sample();
    }

    private void OnLogout(int type, int code) => Reset();

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now < this.nextPoll)
            return;
        this.nextPoll = now + PollInterval;

        Sample();
    }

    private void Sample()
    {
        if (!Plugin.ClientState.IsLoggedIn)
            return;

        // TerritoryInfo is not worth reading mid-transition: it describes the zone
        // being left and flips partway through, so sampling here would produce a
        // change event for a place the player never saw.
        if (Plugin.Condition[ConditionFlag.BetweenAreas] ||
            Plugin.Condition[ConditionFlag.BetweenAreas51])
        {
            this.wasLoading = true;
            return;
        }

        var justLoadedIn = this.wasLoading;
        this.wasLoading = false;

        // Loading straight into a sanctuary is not a crossing as far as this
        // tracker is concerned — and neither is teleporting from one sanctuary to
        // another, which would otherwise stay true throughout and never re-raise.
        // Clearing the flag across a loading screen turns both into a real edge.
        if (justLoadedIn)
            InSanctuary = false;

        var snapshot = Read();
        if (!snapshot.IsEmpty && snapshot != Current)
        {
            var previous = Current;
            Current = snapshot;
            LocationChanged?.Invoke(previous, snapshot);
        }

        var info = TerritoryInfo.Instance();
        var sanctuary = info != null && info->InSanctuary;

        if (sanctuary != InSanctuary)
        {
            InSanctuary = sanctuary;
            SanctuaryChanged?.Invoke(sanctuary);
        }
    }

    // Framework thread only: TerritoryInfo is raw game memory, and IClientState
    // throws when touched off-thread.
    private LocationSnapshot Read()
    {
        var territoryId = Plugin.ClientState.TerritoryType;
        if (territoryId == 0)
            return LocationSnapshot.Empty;

        uint region = 0, zone = 0, place = 0;
        if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
        {
            region = row.PlaceNameRegion.RowId;
            zone = row.PlaceNameZone.RowId;
            place = row.PlaceName.RowId;
        }

        uint area = 0, subArea = 0;
        var info = TerritoryInfo.Instance();
        if (info != null)
        {
            area = info->AreaPlaceNameId;
            subArea = info->SubAreaPlaceNameId;
        }

        return new LocationSnapshot(territoryId, region, zone, place, area, subArea);
    }
}
