using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

internal sealed class FakeSettings : IGateSettings
{
    public bool ZoneNotificationEnabled { get; set; } = true;

    public bool AreaNotificationEnabled { get; set; } = true;

    public bool SubAreaNotificationEnabled { get; set; } = true;

    public bool HideNativeLoadingTitle { get; set; } = true;

    public bool HideInCombat { get; set; }

    public bool HideInDuty { get; set; }

    public bool HideWhileTravellingFast { get; set; } = true;

    public bool WeatherNotificationEnabled { get; set; }

    public bool BannerNotificationEnabled { get; set; } = true;
}

internal sealed class FakeGameState : IGameState
{
    public bool IsLoggedIn { get; set; } = true;

    public bool IsBetweenAreas { get; set; }

    public bool IsInCutscene { get; set; }

    public bool IsPvP { get; set; }

    public bool IsGPosing { get; set; }

    public bool IsInCombat { get; set; }

    public bool IsBoundByDuty { get; set; }
}

internal sealed class TestClock
{
    public DateTime Now { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public DateTime Read() => Now;

    public void Advance(double seconds) => Now += TimeSpan.FromSeconds(seconds);
}
