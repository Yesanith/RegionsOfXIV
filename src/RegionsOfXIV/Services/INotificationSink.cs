using System;

namespace RegionsOfXIV.Services;

internal readonly record struct NotificationTiming(TimeSpan UntilReadable, TimeSpan OnScreen);

// Where an announcement goes once it has been decided on. The overlay implements it; the tests
// substitute a fake, which is what keeps AnnouncementCoordinator runnable without a screen.
//
// Timing comes back the other way because the gate needs to know how long the thing it just
// pushed will be readable before it will allow another one.
internal interface INotificationSink
{
    void Push(string? header, string text);

    void PushWeather(string text, uint iconId);

    NotificationTiming Timing { get; }
}
