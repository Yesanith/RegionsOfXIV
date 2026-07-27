using System;
using Dalamud.Game.ClientState;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Decides what gets announced, and when.
//
// Four sources feed this, because no single one covers every moment:
//
//   ZoneInit                 the zone being entered, while the loading screen is
//                            still up and TerritoryType still names the old one
//   _AreaText                the game itself deciding an announcement is due
//   TerritoryInfo.InSanctuary settlements and inns, which the place-name ids do
//                            not reliably name
//   the 200 ms poll          sub-area changes the game never announces at all
//
// Each arrives on the framework thread, so nothing here needs locking. Everything
// funnels through NotificationGate, whose dedup is what stops two sources
// announcing the same arrival twice.
internal sealed class AnnouncementCoordinator : IDisposable
{
    private readonly Configuration config;
    private readonly NativeUiSuppressor suppressor;
    private readonly INotificationSink sink;
    private readonly NotificationGate gate;
    private readonly LocationTracker tracker;

    // Set for the duration of one _AreaText event, so the announcement being built
    // in response can compare itself against what the game is showing.
    private string? pendingNativeAreaText;

    // The last name the game flashed, kept beyond that event. Sanctuaries are the
    // reason: TerritoryInfo does not reliably name them, and by the time we notice
    // we are inside one the addon is long gone.
    private string? lastNativeAreaText;

    public AnnouncementCoordinator(
        Configuration config, NativeUiSuppressor suppressor, INotificationSink sink)
    {
        this.config = config;
        this.suppressor = suppressor;
        this.sink = sink;

        this.gate = new NotificationGate(config, new DalamudGameState());
        this.tracker = new LocationTracker();

        this.tracker.LocationChanged += OnLocationChanged;
        this.tracker.SanctuaryChanged += OnSanctuaryChanged;
        this.suppressor.AreaTextShown += OnAreaTextShown;

        Plugin.ClientState.Logout += OnLogout;
        Plugin.ClientState.ZoneInit += OnZoneInit;
    }

    public void Dispose()
    {
        Plugin.ClientState.Logout -= OnLogout;
        Plugin.ClientState.ZoneInit -= OnZoneInit;

        this.suppressor.AreaTextShown -= OnAreaTextShown;
        this.tracker.SanctuaryChanged -= OnSanctuaryChanged;
        this.tracker.LocationChanged -= OnLocationChanged;

        this.tracker.Dispose();
    }

    // Bypasses the gate deliberately: this is for checking the visuals, not the
    // suppression rules. Falls back to sample names outside a zone.
    public void PushPreview()
    {
        var names = PlaceNameResolver.Resolve(this.tracker.Current);

        this.sink.Push(
            names.Area ?? names.Place ?? "Middle La Noscea",
            names.SubArea ?? names.Area ?? "Summerford Farms");
    }

    private void OnLogout(int type, int code) => this.gate.Reset();

    // The zone being entered, handed to us as data while the loading screen is
    // still up — well before ClientState.TerritoryType catches up, which is why
    // LocationTracker cannot serve this. Stands in for the suppressed
    // "_LocationTitle".
    private void OnZoneInit(ZoneInitEventArgs args)
    {
        if (args.TerritoryType.ValueNullable is not { } territory)
            return;

        // Both read from the event rather than from current game state, which at
        // this point still describes the zone being left.
        var isDuty = args.ContentFinderCondition.RowId != 0;
        if (!this.gate.ShouldAnnounceZoneEntry(territory.IsPvpZone, isDuty))
            return;

        var text = PlaceNameResolver.Resolve(territory.PlaceName.RowId)
                   ?? PlaceNameResolver.Resolve(territory.PlaceNameZone.RowId);

        if (string.IsNullOrWhiteSpace(text))
            return;

        var header = HeaderOrNull(PlaceNameResolver.Resolve(territory.PlaceNameRegion.RowId), text);

        Plugin.Log.Debug($"ZoneInit [{territory.RowId}]: {header} / {text} (duty={isDuty})");

        this.sink.Push(header, text);
        this.gate.MarkZoneAnnounced(this.sink.EstimatedDuration);
    }

    // In-world, the game itself decides when an area announcement is warranted, so
    // take that as the cue and read TerritoryInfo immediately rather than waiting
    // out the poll interval.
    //
    // Where the two agree, TerritoryInfo wins: same name, but with the parent tier
    // available for the header. Where they disagree — or where TerritoryInfo does
    // not move at all, which is what settlements and sanctuaries appear to do —
    // the game's own string is the one on screen, so it takes precedence.
    //
    // The poll stays running underneath as a backstop for sub-area changes the
    // game never flashes; the gate's dedup keeps whichever arrives second quiet.
    private void OnAreaTextShown(string? nativeText)
    {
        if (string.IsNullOrWhiteSpace(nativeText))
        {
            this.tracker.Poll();
            return;
        }

        this.pendingNativeAreaText = nativeText;
        this.lastNativeAreaText = nativeText;
        try
        {
            // Raises LocationChanged inline when TerritoryInfo moved, which clears
            // the pending text by way of ReconcileWithNative.
            this.tracker.Poll();

            if (this.pendingNativeAreaText is not null && this.gate.ShouldAnnounceNativeAreaText())
            {
                Plugin.Log.Debug($"Native area text only (TerritoryInfo unchanged): {nativeText}");
                this.sink.Push(null, nativeText);
                this.gate.MarkAnnounced(
                    this.tracker.Current, LocationTier.SubArea, this.sink.EstimatedDuration);
            }
        }
        finally
        {
            this.pendingNativeAreaText = null;
        }
    }

    // ZoneInit names the territory — "Western La Noscea" — but says nothing about
    // landing inside a settlement within it. The sanctuary flag is what closes
    // that gap: once the loading screen is down and the tracker sees we are inside
    // one, the finer name follows the zone name and supersedes it on screen.
    //
    // On the way out there is no flash from the game at all, so TerritoryInfo is
    // the only source, and the area we have stepped into is what to announce.
    private void OnSanctuaryChanged(bool inSanctuary)
    {
        if (!this.gate.ShouldAnnounceSanctuary())
            return;

        var names = PlaceNameResolver.Resolve(this.tracker.Current);

        // Entering, the sanctuary's own name is wanted, and TerritoryInfo may not
        // carry it — the game's last flash is the fallback. Leaving, TerritoryInfo
        // is authoritative again and the stale flash would name where we just were.
        var text = inSanctuary
            ? names.SubArea ?? names.Area ?? this.lastNativeAreaText
            : names.Area ?? names.Place;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var parent = inSanctuary ? names.Area ?? names.Place : names.Place;
        var header = HeaderOrNull(parent, text);

        Plugin.Log.Debug($"Sanctuary {(inSanctuary ? "entered" : "left")}: {header} / {text}");

        this.sink.Push(header, text);
        this.gate.MarkAnnounced(this.tracker.Current, LocationTier.SubArea, this.sink.EstimatedDuration);
    }

    // Runs on the framework thread — LocationTracker polls from IFramework.Update,
    // so reading game state here is safe.
    private void OnLocationChanged(LocationSnapshot previous, LocationSnapshot current)
    {
        var tier = current.DiffTier(previous);
        var names = PlaceNameResolver.Resolve(current);

        // Raw ids alongside the names: a blank tier is ambiguous otherwise, since
        // "the game does not track this place" and "the row resolved to nothing"
        // read identically once the ids are gone.
        Plugin.Log.Debug(
            $"Location changed [{tier}]: {names.Region} / {names.Zone} / {names.Place} " +
            $"/ {names.Area} / {names.SubArea} " +
            $"[ids {current.TerritoryTypeId}/{current.RegionPlaceNameId}/{current.ZonePlaceNameId}" +
            $"/{current.PlacePlaceNameId}/{current.AreaPlaceNameId}/{current.SubAreaPlaceNameId}]");

        if (!this.gate.ShouldAnnounce(previous, current, tier, this.tracker.Speed))
            return;

        var (header, text) = BuildNotificationText(tier, names);
        (header, text) = ReconcileWithNative(header, text, names);

        if (string.IsNullOrWhiteSpace(text))
            return;

        this.sink.Push(header, text);
        this.gate.MarkAnnounced(current, tier, this.sink.EstimatedDuration);
    }

    private (string? Header, string Text) BuildNotificationText(
        LocationTier tier, in ResolvedLocation names)
    {
        var (parent, text) = tier switch
        {
            LocationTier.SubArea => (names.Area ?? names.Place, names.SubArea),

            // Arriving at a settlement moves the area and the sub-area at once —
            // Skull Valley and Aleport, say. DiffTier reports the coarser of the
            // two, but the sub-area is the name that identifies the place, and the
            // one the game itself puts on screen. Announce that, with the area as
            // the header, and fall back to the area only when there is no
            // sub-area to show.
            LocationTier.Area => names.SubArea is not null
                ? (names.Area ?? names.Place, names.SubArea)
                : (names.Place, names.Area),

            _ => (names.Region, names.Place ?? names.Zone),
        };

        return (HeaderOrNull(parent, text), text ?? string.Empty);
    }

    // Applied only while the game's area flash is going up in the same breath.
    private (string? Header, string Text) ReconcileWithNative(
        string? header, string text, in ResolvedLocation names)
    {
        if (this.pendingNativeAreaText is not { } native)
            return (header, text);

        // Consumed either way: this decides the one announcement being built.
        this.pendingNativeAreaText = null;

        if (string.Equals(text, native, StringComparison.OrdinalIgnoreCase))
            return (header, text);

        Plugin.Log.Debug($"TerritoryInfo says \"{text}\", the game says \"{native}\" — taking the game's.");

        // The area still makes a usable header, as long as it is not the thing
        // being announced. Anything finer cannot be trusted here: it just
        // disagreed with the game.
        var parent = names.Area is not null
                     && !string.Equals(names.Area, native, StringComparison.OrdinalIgnoreCase)
            ? names.Area
            : null;

        return (HeaderOrNull(parent, native), native);
    }

    // Every announcement applies the same two rules to its header: honour the
    // setting, and never repeat the line below it.
    private string? HeaderOrNull(string? parent, string? text)
    {
        if (!this.config.IncludeParentTierAsHeader || parent is null)
            return null;

        return string.Equals(parent, text, StringComparison.OrdinalIgnoreCase) ? null : parent;
    }
}
