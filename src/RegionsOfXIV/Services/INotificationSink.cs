using System;

namespace RegionsOfXIV.Services;

internal interface INotificationSink
{
    void Push(string? header, string text);

    TimeSpan EstimatedDuration { get; }
}
