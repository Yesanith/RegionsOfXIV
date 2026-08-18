using System;
using System.Numerics;
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
    public event Action<LocationSnapshot, LocationSnapshot>? OnLocationChanged;

    // Raised when the player crosses into or out of a sanctuary. Separate from
    // OnLocationChanged because the two do not always coincide: settlements can be
    // entered without any TerritoryInfo place name moving.
    public event Action<bool>? OnSanctuaryChanged;

    public LocationSnapshot Current { get; private set; } = LocationSnapshot.Empty;

    public bool InSanctuary { get; private set; }

    // Yalms per second, averaged across the last sample interval. Zero whenever
    // there is nothing sensible to report — no player, the first sample after a
    // loading screen, or a jump too large to be movement.
    public float Speed { get; private set; }

    private bool wasLoading;

    private Vector3? lastPosition;
    private DateTime lastPositionAt;

    // The same instance the gate judges with, so the two cannot disagree about
    // whether a cutscene is running — one holding this tracker still while the
    // other decides the moment has passed would lose the arrival either way.
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

    // Forget where we were, so the next poll re-announces.
    public void Reset()
    {
        Current = LocationSnapshot.Empty;
        InSanctuary = false;
        this.wasLoading = false;
        ForgetPosition();
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
        if (!this.game.IsLoggedIn)
        {
            ForgetPosition();
            return;
        }

        // TerritoryInfo is not worth reading mid-transition: it describes the zone
        // being left and flips partway through, so sampling here would produce a
        // change event for a place the player never saw.
        if (this.game.IsBetweenAreas)
        {
            this.wasLoading = true;
            ForgetPosition();
            return;
        }

        // Cutscenes and gpose are suppressed periods, and sampling through one
        // loses the arrival rather than merely delaying it: Current would advance
        // to a place nothing was allowed to announce, and once it has advanced the
        // next sample sees no change to report. The transition is then gone for
        // good. A dungeon is where this bites — you load in, a cutscene starts at
        // once, and it moves you.
        //
        // Holding Current still instead means the change is simply noticed late,
        // the moment the cutscene ends.
        //
        // Only the unconditional suppressions belong here. Combat and duty are the
        // user's own choice and are deliberately still sampled, so "hide in
        // combat" stays quiet rather than turning into amnesia about everywhere
        // you went while fighting.
        if (this.game.IsInCutscene || this.game.IsGPosing)
        {
            ForgetPosition();
            return;
        }

        var justLoadedIn = this.wasLoading;
        this.wasLoading = false;

        // Across a loading screen the old position describes another zone
        // entirely, so the first sample on the far side establishes a new baseline
        // rather than measuring against it.
        if (justLoadedIn)
            ForgetPosition();

        SampleSpeed();

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

    // Anything above this in a single interval is not movement. A teleport, a
    // return, a duty finder pull — the position simply appears somewhere else, and
    // dividing by the interval would report thousands of yalms per second. Well
    // clear of the fastest flying mount, so nothing legitimate reaches it.
    private const float ImplausibleSpeed = 200f;

    // Below this the interval is too short for the difference between two
    // positions to mean anything, so the previous reading stands rather than being
    // replaced by noise. Poll() can fire at any time — the game raising its own
    // area text is enough — so intervals are not reliably the poll interval.
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

        // Full 3D distance rather than ground plane: a flying mount climbing is
        // travelling, and that is exactly the case this measurement is for.
        var speed = Vector3.Distance(previous, position) / seconds;

        Speed = speed >= ImplausibleSpeed ? 0f : speed;
    }

    private void ForgetPosition()
    {
        this.lastPosition = null;
        Speed = 0f;
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
