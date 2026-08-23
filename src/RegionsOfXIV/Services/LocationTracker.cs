using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using RegionsOfXIV.Models;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

// The game raises no event for walking between areas inside a zone, so position has to be
// sampled. Polled rather than read every frame -- a fifth of a second is far finer than anyone
// can cross a boundary, and reading TerritoryInfo is not free.
//
// Poll() exists so the coordinator can force a sample the moment the game shows its own area
// text, instead of waiting up to a full interval to find out whether it agrees with us.
internal sealed unsafe class LocationTracker : ILocationSource, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private DateTime nextPoll = DateTime.MinValue;

    public event Action<LocationSnapshot, LocationSnapshot>? OnLocationChanged;

    public event Action<bool>? OnSanctuaryChanged;

    public LocationSnapshot Current { get; private set; } = LocationSnapshot.Empty;

    public bool InSanctuary { get; private set; }

    public float Speed { get; private set; }

    private bool wasLoading;

    private Vector3? lastPosition;
    private DateTime lastPositionAt;

    private readonly IGameState game;

    public LocationTracker(IGameState game)
    {
        this.game = game;

        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.ClientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.ClientState.Logout -= OnLogout;
    }

    public void Reset()
    {
        Current = LocationSnapshot.Empty;
        InSanctuary = false;
        this.wasLoading = false;
        ForgetPosition();
    }

    // Pushes the next scheduled sample out as well, so an on-demand poll does not cause a second
    // one a few milliseconds later.
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
        if (!this.game.IsLoggedIn)
        {
            ForgetPosition();
            return;
        }

        if (this.game.IsBetweenAreas)
        {
            this.wasLoading = true;
            ForgetPosition();
            return;
        }

        if (this.game.IsInCutscene || this.game.IsGPosing)
        {
            ForgetPosition();
            return;
        }

        var justLoadedIn = this.wasLoading;
        this.wasLoading = false;

        if (justLoadedIn)
            ForgetPosition();

        SampleSpeed();

        if (justLoadedIn)
            InSanctuary = false;

        var snapshot = Read();
        if (!snapshot.IsEmpty && snapshot != Current)
        {
            var previous = Current;
            Current = snapshot;
            OnLocationChanged?.Invoke(previous, snapshot);
        }

        var info = TerritoryInfo.Instance();
        var sanctuary = info != null && info->InSanctuary;

        if (sanctuary != InSanctuary)
        {
            InSanctuary = sanctuary;
            OnSanctuaryChanged?.Invoke(sanctuary);
        }
    }

    private const float ImplausibleSpeed = 200f;

    private const float MinimumInterval = 0.05f;

    private void SampleSpeed()
    {
        var now = DateTime.UtcNow;
        var player = Plugin.ObjectTable.LocalPlayer;

        if (player is null)
        {
            ForgetPosition();
            return;
        }

        var position = player.Position;

        if (this.lastPosition is not { } previous)
        {
            this.lastPosition = position;
            this.lastPositionAt = now;
            Speed = 0f;
            return;
        }

        var seconds = (float)(now - this.lastPositionAt).TotalSeconds;
        if (seconds < MinimumInterval)
            return;

        this.lastPosition = position;
        this.lastPositionAt = now;

        var speed = Vector3.Distance(previous, position) / seconds;

        Speed = speed >= ImplausibleSpeed ? 0f : speed;
    }

    private void ForgetPosition()
    {
        this.lastPosition = null;
        Speed = 0f;
    }

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
