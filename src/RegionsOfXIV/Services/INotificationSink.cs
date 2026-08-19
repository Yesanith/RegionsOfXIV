using System;

namespace RegionsOfXIV.Services;

internal interface INotificationSink
{
    void Push(string? header, string text);

    void PushWeather(string text, uint iconId);

    TimeSpan EstimatedDuration { get; }
}
