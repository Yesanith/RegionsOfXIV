namespace RegionsOfXIV.Services;

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
