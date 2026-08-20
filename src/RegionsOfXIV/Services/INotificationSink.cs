using System;

namespace RegionsOfXIV.Services;

internal readonly record struct NotificationTiming(TimeSpan UntilReadable, TimeSpan OnScreen);

internal interface INotificationSink
{
    void Push(string? header, string text);

    void PushWeather(string text, uint iconId);

    NotificationTiming Timing { get; }
}
