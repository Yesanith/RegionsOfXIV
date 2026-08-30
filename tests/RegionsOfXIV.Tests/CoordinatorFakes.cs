using System.Numerics;
using RegionsOfXIV.Models;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

internal sealed record Announcement(string? Header, string Text, bool IsWeather, uint IconId);

internal sealed class FakeSink : INotificationSink
{
    public List<Announcement> Pushed { get; } = [];

    public NotificationTiming Timing { get; set; } =
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4));

    public Announcement? Last => this.Pushed.Count == 0 ? null : this.Pushed[^1];

    public List<Announcement> Places =>
        this.Pushed.Where(p => !p.IsWeather).ToList();

    public List<Announcement> Weather =>
        this.Pushed.Where(p => p.IsWeather).ToList();

    public void Push(string? header, string text) =>
        this.Pushed.Add(new Announcement(header, text, false, 0));

    public void PushWeather(string text, uint iconId) =>
        this.Pushed.Add(new Announcement(null, text, true, iconId));
}

internal sealed class FakeLocations : ILocationSource
{
    public event Action<LocationSnapshot, LocationSnapshot>? OnLocationChanged;

    public event Action<bool>? OnSanctuaryChanged;

    public LocationSnapshot Current { get; set; } = LocationSnapshot.Empty;

    public float Speed { get; set; }

    public int Polls { get; private set; }

    public void Poll() => this.Polls++;

    public void Move(LocationSnapshot to)
    {
        var from = this.Current;
        this.Current = to;
        OnLocationChanged?.Invoke(from, to);
    }

    public void EnterSanctuary(bool inside) => OnSanctuaryChanged?.Invoke(inside);
}

internal sealed class FakeWeather : IWeatherSource
{
    public event Action<byte>? OnWeatherChanged;

    public byte Current { get; set; }

    public uint Primed { get; private set; }

    public int Resets { get; private set; }

    public void Prime(uint weatherId) => this.Primed = weatherId;

    public void Reset() => this.Resets++;

    public void Change(byte weatherId)
    {
        this.Current = weatherId;
        OnWeatherChanged?.Invoke(weatherId);
    }
}

internal sealed class FakeAreaText : IAreaTextSource
{
    public event Action<string?>? OnAreaTextShown;

    public void Show(string? text) => OnAreaTextShown?.Invoke(text);
}

internal sealed class FakeBanners : IBannerSource
{
    public event Action<uint, string, BannerBlock>? OnBannerShown;

    // Defaulted, because the gate refusing is the interesting case and every other test wants a
    // banner that simply arrives. BannerWatcher is what really fills this in.
    public void Show(uint iconId, string text, BannerBlock blocked = BannerBlock.None) =>
        OnBannerShown?.Invoke(iconId, text, blocked);
}

internal sealed class FakeZones : IZoneArrivals
{
    public event Action<ZoneArrival>? Arrived;

    public event Action? LoggedOut;

    public void Arrive(ZoneArrival arrival) => Arrived?.Invoke(arrival);

    public void LogOut() => LoggedOut?.Invoke();
}

internal sealed class FakePlaceNames : IPlaceNames
{
    public Dictionary<uint, string> Names { get; } = [];

    public string? Resolve(uint placeNameRowId) =>
        this.Names.TryGetValue(placeNameRowId, out var name) ? name : null;

    public ResolvedLocation Resolve(in LocationSnapshot snapshot) => new(
        Resolve(snapshot.RegionPlaceNameId),
        Resolve(snapshot.ZonePlaceNameId),
        Resolve(snapshot.PlacePlaceNameId),
        Resolve(snapshot.AreaPlaceNameId),
        Resolve(snapshot.SubAreaPlaceNameId));
}

internal sealed class FakeWeatherNames : IWeatherNames
{
    public Dictionary<uint, ResolvedWeather> Names { get; } = [];

    public ResolvedWeather? Forecasts { get; set; }

    public ResolvedWeather? Resolve(uint weatherRowId) =>
        this.Names.TryGetValue(weatherRowId, out var weather) ? weather : null;

    public ResolvedWeather? Forecast(uint territoryTypeId, DateTimeOffset at) => this.Forecasts;
}
