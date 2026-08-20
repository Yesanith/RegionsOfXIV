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

    public void Prime(uint weatherId)
    {
        if (weatherId <= byte.MaxValue)
            this.current = (byte)weatherId;
    }

    public void Sample()
    {
        if (!this.game.IsLoggedIn)
        {
            Reset();
            return;
        }

        if (this.game.IsBetweenAreas)
            return;

        var active = this.readActive();

        if (active == 0 || active == this.current)
            return;

        var hadReading = this.current != 0;
        this.current = active;

        if (hadReading)
            OnWeatherChanged?.Invoke(active);
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

    private static unsafe byte ReadActiveWeather()
    {
        var env = EnvManager.Instance();
        return env == null ? (byte)0 : env->ActiveWeather;
    }
}
