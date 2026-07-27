using System;

namespace RegionsOfXIV.Services;

// Where a decided announcement goes.
//
// AnnouncementCoordinator works out what to say; this is all it needs to know
// about who shows it. Narrow on purpose: it keeps the dependency pointing from
// UI to Services rather than the other way round, and it means the coordinator's
// rules can be exercised without an ImGui context.
internal interface INotificationSink
{
    void Push(string? header, string text);

    // How long a pushed notification stays on screen. The gate needs this to
    // suppress finer announcements while a coarser one is still visible.
    TimeSpan EstimatedDuration { get; }
}
