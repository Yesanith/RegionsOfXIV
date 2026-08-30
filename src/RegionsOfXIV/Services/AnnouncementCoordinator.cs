using System;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Everything that decides what gets announced and when. Start reading here.
//
// It never touches the game directly: arrivals, movement, weather, banners, the game's own area
// text and the two name lookups all arrive through the interfaces in AnnouncementSources.
// Plugin.cs is the only place that knows which real implementation goes with which, which is why
// these rules can be exercised in tests without launching the game.
internal sealed class AnnouncementCoordinator : IDisposable
{
    private readonly Configuration config;
    private readonly INotificationSink sink;
    private readonly AnnouncementSources sources;
    private readonly NotificationGate gate;

    private string? pendingNativeAreaText;

    private string? lastNativeAreaText;

    // The gate arrives rather than being built here because BannerWatcher needs the same one: it
    // has to know whether a banner will really be replaced before it hides the game's own, and a
    // second gate would keep its own cooldown and answer differently.
    public AnnouncementCoordinator(
        Configuration config, NotificationGate gate, INotificationSink sink, AnnouncementSources sources)
    {
        this.config = config;
        this.sink = sink;
        this.sources = sources;
        this.gate = gate;

        sources.Banners.OnBannerShown += HandleBannerShown;
        sources.Weather.OnWeatherChanged += HandleWeatherChanged;
        sources.Locations.OnLocationChanged += HandleLocationChanged;
        sources.Locations.OnSanctuaryChanged += HandleSanctuaryChanged;
        sources.AreaText.OnAreaTextShown += HandleAreaTextShown;
        sources.Zones.Arrived += OnArrived;
        sources.Zones.LoggedOut += OnLoggedOut;
    }

    public void Dispose()
    {
        this.sources.Zones.LoggedOut -= OnLoggedOut;
        this.sources.Zones.Arrived -= OnArrived;
        this.sources.AreaText.OnAreaTextShown -= HandleAreaTextShown;
        this.sources.Locations.OnSanctuaryChanged -= HandleSanctuaryChanged;
        this.sources.Locations.OnLocationChanged -= HandleLocationChanged;
        this.sources.Weather.OnWeatherChanged -= HandleWeatherChanged;
        this.sources.Banners.OnBannerShown -= HandleBannerShown;
    }

    public void PushPreview()
    {
        var names = this.sources.PlaceNames.Resolve(this.sources.Locations.Current);

        this.sink.Push(
            names.Area ?? names.Place ?? "Middle La Noscea",
            names.SubArea ?? names.Area ?? "Summerford Farms");

        if (!this.config.WeatherNotificationEnabled)
            return;

        var current = this.sources.WeatherNames.Resolve(this.sources.Weather.Current)
                      ?? this.sources.WeatherNames.Resolve(1);

        if (current is not { } weather)
            return;

        this.sink.PushWeather(weather.Name, weather.IconId);
    }

    // Forecast rather than observed: on arrival the client has not necessarily settled on the
    // real weather yet, and EorzeaWeather can work it out from the clock immediately. Priming the
    // tracker with the answer stops it announcing the same weather again a moment later.
    private void AnnounceArrivalWeather(uint territoryTypeId)
    {
        if (!this.config.WeatherNotificationEnabled)
            return;

        if (this.sources.WeatherNames.Forecast(territoryTypeId, DateTimeOffset.UtcNow) is not { } weather)
            return;

        this.sources.Weather.Prime(weather.Id);

        this.sink.PushWeather(weather.Name, weather.IconId);
    }

    // The gate has already been asked, by the watcher, which needed the answer before this to know
    // whether to hide the game's own banner. Asking again here would read the clock a second time
    // and could disagree with it, so the reason it reached is what gets acted on.
    private void HandleBannerShown(uint iconId, string text, BannerBlock blocked)
    {
        Log.Debug(blocked == BannerBlock.None
            ? $"Banner [{iconId}]: {text}"
            : $"Banner [{iconId}]: {text} -- refused: {blocked}");

        if (blocked != BannerBlock.None)
            return;

        BannerNotification.PushTo(this.sink, text);
        this.gate.MarkBannerAnnounced(this.sink.Timing);
    }

    private void HandleWeatherChanged(byte weatherId)
    {
        if (!this.gate.ShouldAnnounceWeather())
            return;

        if (this.sources.WeatherNames.Resolve(weatherId) is not { } weather)
            return;

        this.sink.PushWeather(weather.Name, weather.IconId);
    }

    private void OnLoggedOut()
    {
        this.gate.Reset();
        this.sources.Weather.Reset();
    }

    private void OnArrived(ZoneArrival arrival)
    {
        if (!this.gate.ShouldAnnounceZoneEntry(arrival.IsPvp, arrival.IsDuty))
            return;

        var text = this.sources.PlaceNames.Resolve(arrival.PlaceNameId)
                   ?? this.sources.PlaceNames.Resolve(arrival.ZonePlaceNameId);

        if (string.IsNullOrWhiteSpace(text))
            return;

        var header = HeaderOrNull(this.sources.PlaceNames.Resolve(arrival.RegionPlaceNameId), text);

        Log.Debug($"Arrived [{arrival.TerritoryTypeId}]: {header} / {text} (duty={arrival.IsDuty})");

        this.sink.Push(header, text);
        this.gate.MarkZoneAnnounced(this.sink.Timing);

        AnnounceArrivalWeather(arrival.TerritoryTypeId);
    }

    // The game showing its own area text is the only warning we get for some sub-area changes, so
    // it is used as a trigger to re-read our own position. If polling explains the change, the
    // location handlers announce it and clear pendingNativeAreaText on the way through; if it is
    // still set afterwards, the game knows about somewhere we cannot see and its wording is used
    // verbatim rather than dropping the notice entirely.
    private void HandleAreaTextShown(string? nativeText)
    {
        if (string.IsNullOrWhiteSpace(nativeText))
        {
            this.sources.Locations.Poll();
            return;
        }

        this.pendingNativeAreaText = nativeText;
        this.lastNativeAreaText = nativeText;
        try
        {
            this.sources.Locations.Poll();

            if (this.pendingNativeAreaText is not null && this.gate.ShouldAnnounceNativeAreaText())
            {
                Log.Debug($"Native area text only (TerritoryInfo unchanged): {nativeText}");
                this.sink.Push(null, nativeText);
                this.gate.MarkAnnounced(
                    this.sources.Locations.Current, LocationTier.SubArea, this.sink.Timing);
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

        var names = this.sources.PlaceNames.Resolve(this.sources.Locations.Current);

        var text = inSanctuary
            ? names.SubArea ?? names.Area ?? this.lastNativeAreaText
            : names.Area ?? names.Place;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var parent = inSanctuary ? names.Area ?? names.Place : names.Place;
        var header = HeaderOrNull(parent, text);

        Log.Debug($"Sanctuary {(inSanctuary ? "entered" : "left")}: {header} / {text}");

        this.sink.Push(header, text);
        this.gate.MarkAnnounced(this.sources.Locations.Current, LocationTier.SubArea, this.sink.Timing);
    }

    private void HandleLocationChanged(LocationSnapshot previous, LocationSnapshot current)
    {
        var tier = current.DiffTier(previous);
        var names = this.sources.PlaceNames.Resolve(current);

        Log.Debug(
            $"Location changed [{tier}]: {names.Region} / {names.Zone} / {names.Place} " +
            $"/ {names.Area} / {names.SubArea} " +
            $"[ids {current.TerritoryTypeId}/{current.RegionPlaceNameId}/{current.ZonePlaceNameId}" +
            $"/{current.PlacePlaceNameId}/{current.AreaPlaceNameId}/{current.SubAreaPlaceNameId}]");

        if (!this.gate.ShouldAnnounce(previous, current, tier, this.sources.Locations.Speed))
            return;

        var (header, text) = BuildNotificationText(tier, names);
        (header, text) = ReconcileWithNative(header, text, names);

        if (string.IsNullOrWhiteSpace(text))
            return;

        this.sink.Push(header, text);
        this.gate.MarkAnnounced(current, tier, this.sink.Timing);
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

    // Where the game's wording and ours disagree, the game wins for the name itself -- it knows
    // about places TerritoryInfo does not expose -- while the header still comes from our own
    // sheet lookups so it stays consistent with every other announcement.
    private (string? Header, string Text) ReconcileWithNative(
        string? header, string text, in ResolvedLocation names)
    {
        if (this.pendingNativeAreaText is not { } native)
            return (header, text);

        this.pendingNativeAreaText = null;

        if (string.Equals(text, native, StringComparison.OrdinalIgnoreCase))
            return (header, text);

        Log.Debug($"TerritoryInfo says \"{text}\", the game says \"{native}\". Taking the game's.");

        var parent = names.Area is not null
                     && !string.Equals(names.Area, native, StringComparison.OrdinalIgnoreCase)
            ? names.Area
            : null;

        return (HeaderOrNull(parent, native), native);
    }

    private string? HeaderOrNull(string? parent, string? text) =>
        this.config.HeaderFor(parent, text);
}
