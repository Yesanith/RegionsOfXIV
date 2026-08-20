using System;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace RegionsOfXIV.Services;

internal sealed class WeatherTracker : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IGameState game;
    private readonly Func<byte> readActive;

    private DateTime nextPoll = DateTime.MinValue;

    private byte current;

    // TEMPORARY probe: works out when the game swaps ActiveWeather relative to the
    // loading screen. Delete this and every Probe call once the timing is known.
    private bool probeBetween;
    private byte probeWeather;
    private DateTime probeStartedAt;

    public event Action<byte>? OnWeatherChanged;

    public WeatherTracker(IGameState game, Func<byte>? readActive = null)
    {
        this.game = game;
        this.readActive = readActive ?? ReadActiveWeather;
    }

    public void Start()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.ClientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.ClientState.Logout -= OnLogout;
    }

    public byte Current => this.current;

    public void Reset() => this.current = 0;

    /// <summary>
    /// Sets the baseline without announcing. The coordinator calls this when it has
    /// already announced a zone's weather from the forecast, so the reading that lands
    /// a moment later is not treated as a change.
    /// </summary>
    public void Prime(uint weatherId)
    {
        if (weatherId <= byte.MaxValue)
            this.current = (byte)weatherId;
    }

    public void Sample()
    {
        // Logged out you were not there to see anything, so the next reading starts fresh.
        if (!this.game.IsLoggedIn)
        {
            Reset();
            return;
        }

        // Mid-load the reading still belongs to where you came from.
        if (this.game.IsBetweenAreas)
            return;

        var active = this.readActive();

        // Zero means the game has not settled on a weather yet.
        if (active == 0 || active == this.current)
            return;

        var hadReading = this.current != 0;
        this.current = active;

        if (hadReading)
        {
            Probe("announcing");
            OnWeatherChanged?.Invoke(active);
        }
    }

    private void OnLogout(int type, int code) => Reset();

    public void Probe(string tag)
    {
        // Plugin.Log is only wired up inside the game; the tests drive Sample directly.
        if (Plugin.Log is null)
            return;

        var elapsed = this.probeStartedAt == default
            ? TimeSpan.Zero
            : DateTime.UtcNow - this.probeStartedAt;

        var active = this.readActive();
        var name = WeatherNameResolver.Resolve(active)?.Name ?? "-";

        var forecast = WeatherNameResolver.Forecast(
            Plugin.ClientState.TerritoryType, DateTimeOffset.UtcNow);

        var agrees = forecast is null ? "?" : forecast.Value.Id == active ? "match" : "MISMATCH";

        Plugin.Log.Information(
            $"[weather-probe] +{elapsed.TotalMilliseconds,6:F0}ms  {tag,-14} " +
            $"betweenAreas={this.game.IsBetweenAreas,-5} weather={active,-3} ({name})  " +
            $"forecast={forecast?.Id.ToString() ?? "-",-3} ({forecast?.Name ?? "-"}) {agrees}");
    }

    private void ProbeChanges()
    {
        var between = this.game.IsBetweenAreas;
        var active = this.readActive();

        if (between && !this.probeBetween)
            this.probeStartedAt = DateTime.UtcNow;

        if (between == this.probeBetween && active == this.probeWeather)
            return;

        this.probeBetween = between;
        this.probeWeather = active;

        Probe(between ? "loading" : "in-world");
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        ProbeChanges();

        var now = DateTime.UtcNow;
        if (now < this.nextPoll)
            return;

        this.nextPoll = now + PollInterval;
        Sample();
    }

    private static unsafe byte ReadActiveWeather()
    {
        var env = EnvManager.Instance();
        return env == null ? (byte)0 : env->ActiveWeather;
    }
}
