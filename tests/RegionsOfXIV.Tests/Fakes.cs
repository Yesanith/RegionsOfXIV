using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// Stand-ins for the two seams NotificationGate reads through, plus a clock.
//
// Plain mutable fields rather than a mocking library: every one of them is a
// bool, and a test that says `game.IsInCutscene = true` reads better than any
// framework's way of saying the same thing.

// Defaults match Configuration's, so a test only has to state the setting it is
// actually about.
internal sealed class FakeSettings : IGateSettings
{
    public bool ZoneNotificationEnabled { get; set; } = true;

    public bool AreaNotificationEnabled { get; set; } = true;

    public bool SubAreaNotificationEnabled { get; set; } = true;

    public bool HideNativeLoadingTitle { get; set; } = true;

    public bool HideInCombat { get; set; }

    public bool HideInDuty { get; set; }

    public bool HideWhileTravellingFast { get; set; } = true;
}

// Defaults to the ordinary case: logged in, standing in the world, nothing in
// the way.
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

// Starts at a fixed, arbitrary instant rather than DateTime.MinValue: the gate
// initialises its timestamps to MinValue to mean "never", and a clock starting
// there would make "never" and "now" the same moment.
internal sealed class TestClock
{
    public DateTime Now { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public DateTime Read() => Now;

    public void Advance(double seconds) => Now += TimeSpan.FromSeconds(seconds);
}
