using System;
using Dalamud.Game.ClientState;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

internal sealed class AnnouncementCoordinator : IDisposable
{
    private readonly Configuration config;
    private readonly NativeUiSuppressor suppressor;
    private readonly INotificationSink sink;
    private readonly NotificationGate gate;
    private readonly LocationTracker tracker;
    private readonly WeatherTracker weatherTracker;

    private string? pendingNativeAreaText;

    private string? lastNativeAreaText;

    public AnnouncementCoordinator(
        Configuration config, NativeUiSuppressor suppressor, INotificationSink sink)
    {
        this.config = config;
        this.suppressor = suppressor;
        this.sink = sink;

        var game = new DalamudGameState();

        this.gate = new NotificationGate(config, game);
        this.tracker = new LocationTracker(game);
        this.weatherTracker = new WeatherTracker(game);
        this.weatherTracker.Start();

        this.weatherTracker.OnWeatherChanged += HandleWeatherChanged;
        this.tracker.OnLocationChanged += HandleLocationChanged;
        this.tracker.OnSanctuaryChanged += HandleSanctuaryChanged;
        this.suppressor.OnAreaTextShown += HandleAreaTextShown;

        Plugin.ClientState.Logout += OnLogout;
        Plugin.ClientState.ZoneInit += OnZoneInit;
    }

    public void Dispose()
    {
        Plugin.ClientState.Logout -= OnLogout;
        Plugin.ClientState.ZoneInit -= OnZoneInit;

        this.suppressor.OnAreaTextShown -= HandleAreaTextShown;
        this.tracker.OnSanctuaryChanged -= HandleSanctuaryChanged;
        this.tracker.OnLocationChanged -= HandleLocationChanged;
        this.weatherTracker.OnWeatherChanged -= HandleWeatherChanged;

        this.weatherTracker.Dispose();
        this.tracker.Dispose();
    }

    public void PushPreview()
    {
        var names = PlaceNameResolver.Resolve(this.tracker.Current);

        this.sink.Push(
            names.Area ?? names.Place ?? "Middle La Noscea",
            names.SubArea ?? names.Area ?? "Summerford Farms");

        if (!this.config.WeatherNotificationEnabled)
            return;

        // Falls back to the first row so the command still shows something when the
        // tracker has not taken a reading yet.
        var current = WeatherNameResolver.Resolve(this.weatherTracker.Current)
                      ?? WeatherNameResolver.Resolve(1);

        if (current is not { } weather)
            return;

        this.sink.PushWeather(weather.Name, weather.IconId);
    }

    /// <summary>
    /// Says what the weather is doing where you have just landed. The forecast is worked
    /// out from the clock rather than read from the game, so it is known while the
    /// loading screen is still up and lands with the place name rather than behind it.
    /// </summary>
    private void AnnounceArrivalWeather(uint territoryTypeId)
    {
        if (!this.config.WeatherNotificationEnabled)
            return;

        if (WeatherNameResolver.Forecast(territoryTypeId, DateTimeOffset.UtcNow) is not { } weather)
            return;

        // Baselined so the reading that settles a moment later is not read as a change.
        this.weatherTracker.Prime(weather.Id);

        this.sink.PushWeather(weather.Name, weather.IconId);
    }

    private void HandleWeatherChanged(byte weatherId)
    {
        if (!this.gate.ShouldAnnounceWeather())
            return;

        if (WeatherNameResolver.Resolve(weatherId) is not { } weather)
            return;

        this.sink.PushWeather(weather.Name, weather.IconId);
    }

    private void OnLogout(int type, int code)
    {
        this.gate.Reset();
        this.weatherTracker.Reset();
    }

    private void OnZoneInit(ZoneInitEventArgs args)
    {
        if (args.TerritoryType.ValueNullable is not { } territory)
            return;

        var isDuty = args.ContentFinderCondition.RowId != 0;
        if (!this.gate.ShouldAnnounceZoneEntry(territory.IsPvpZone, isDuty))
            return;

        var text = PlaceNameResolver.Resolve(territory.PlaceName.RowId)
                   ?? PlaceNameResolver.Resolve(territory.PlaceNameZone.RowId);

        if (string.IsNullOrWhiteSpace(text))
            return;

        var header = HeaderOrNull(PlaceNameResolver.Resolve(territory.PlaceNameRegion.RowId), text);

        Plugin.Log.Debug($"ZoneInit [{territory.RowId}]: {header} / {text} (duty={isDuty})");
        this.weatherTracker.Probe("ZoneInit");

        this.sink.Push(header, text);
        this.gate.MarkZoneAnnounced(this.sink.EstimatedDuration);

        AnnounceArrivalWeather(territory.RowId);
    }

    private void HandleAreaTextShown(string? nativeText)
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

    private void HandleSanctuaryChanged(bool inSanctuary)
    {
        if (!this.gate.ShouldAnnounceSanctuary())
            return;

        var names = PlaceNameResolver.Resolve(this.tracker.Current);

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

    private void HandleLocationChanged(LocationSnapshot previous, LocationSnapshot current)
    {
        var tier = current.DiffTier(previous);
        var names = PlaceNameResolver.Resolve(current);

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

            LocationTier.Area => names.SubArea is not null
                ? (names.Area ?? names.Place, names.SubArea)
                : (names.Place, names.Area),

            _ => (names.Region, names.Place ?? names.Zone),
        };

        return (HeaderOrNull(parent, text), text ?? string.Empty);
    }

    private (string? Header, string Text) ReconcileWithNative(
        string? header, string text, in ResolvedLocation names)
    {
        if (this.pendingNativeAreaText is not { } native)
            return (header, text);

        this.pendingNativeAreaText = null;

        if (string.Equals(text, native, StringComparison.OrdinalIgnoreCase))
            return (header, text);

        Plugin.Log.Debug($"TerritoryInfo says \"{text}\", the game says \"{native}\" — taking the game's.");

        var parent = names.Area is not null
                     && !string.Equals(names.Area, native, StringComparison.OrdinalIgnoreCase)
            ? names.Area
            : null;

        return (HeaderOrNull(parent, native), native);
    }

    private string? HeaderOrNull(string? parent, string? text)
    {
        if (!this.config.IncludeParentTierAsHeader || parent is null)
            return null;

        return string.Equals(parent, text, StringComparison.OrdinalIgnoreCase) ? null : parent;
    }
}
