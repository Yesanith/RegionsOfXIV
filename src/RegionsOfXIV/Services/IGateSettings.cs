namespace RegionsOfXIV.Services;

// The slice of Configuration that NotificationGate is allowed to see. Configuration implements
// it directly; the point is that the gate cannot reach a colour or a font and quietly grow a
// dependency on how things look.
internal interface IGateSettings
{
    bool ZoneNotificationEnabled { get; }

    bool AreaNotificationEnabled { get; }

    bool SubAreaNotificationEnabled { get; }

    bool HideNativeLoadingTitle { get; }

    bool HideInCombat { get; }

    bool HideInDuty { get; }

    bool HideWhileTravellingFast { get; }

    bool WeatherNotificationEnabled { get; }
}
