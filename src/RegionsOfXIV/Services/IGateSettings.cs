namespace RegionsOfXIV.Services;

// The settings NotificationGate reads, and only those.
//
// Configuration satisfies this without declaring anything extra — its properties
// already match by name and type — so this costs one item on its base list and
// nothing else. What it buys is a gate that can be built without a Configuration,
// and therefore without Dalamud's IPluginConfiguration behind it, which is the
// difference between the suppression rules being testable and not.
//
// Getters only, deliberately: the gate decides, it does not configure.
internal interface IGateSettings
{
    bool ZoneNotificationEnabled { get; }

    bool AreaNotificationEnabled { get; }

    bool SubAreaNotificationEnabled { get; }

    bool HideNativeLoadingTitle { get; }

    bool HideInCombat { get; }

    bool HideInDuty { get; }

    bool HideWhileTravellingFast { get; }
}
