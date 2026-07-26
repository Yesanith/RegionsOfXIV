using System;
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

    public LocationSnapshot Current { get; private set; } = LocationSnapshot.Empty;

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
    public void Reset() => Current = LocationSnapshot.Empty;

    private void OnLogout(int type, int code) => Reset();

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now < this.nextPoll)
            return;
        this.nextPoll = now + PollInterval;

        if (!Plugin.ClientState.IsLoggedIn)
            return;

        var snapshot = Read();
        if (snapshot.IsEmpty || snapshot == Current)
            return;

        var previous = Current;
        Current = snapshot;
        LocationChanged?.Invoke(previous, snapshot);
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
