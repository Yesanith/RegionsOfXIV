using System;

namespace RegionsOfXIV.Services;

/// <summary>
/// How long a notification takes to become readable, and how long it stays up in
/// total. The gate paces announcements by the first and suppresses finer tiers for
/// the second.
/// </summary>
internal readonly record struct NotificationTiming(TimeSpan UntilReadable, TimeSpan OnScreen);

internal interface INotificationSink
{
    void Push(string? header, string text);

    void PushWeather(string text, uint iconId);

    NotificationTiming Timing { get; }
}
