using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private void DrawNotificationsTab()
    {
        using var tab = ImRaii.TabItem("Notifications");
        if (!tab) return;

        ImGui.TextWrapped(
            "This plugin replaces the game's own location text rather than drawing alongside " +
            "it. If you turn the suppression below off, the game's version comes back.");
        ImGui.Separator();

        var changed = Checkbox("Zone changes",
            () => this.config.ZoneNotificationEnabled, v => this.config.ZoneNotificationEnabled = v);

        changed |= Checkbox("Area changes",
            () => this.config.AreaNotificationEnabled, v => this.config.AreaNotificationEnabled = v);

        changed |= Checkbox("Sub-area changes",
            () => this.config.SubAreaNotificationEnabled, v => this.config.SubAreaNotificationEnabled = v);

        ImGui.Separator();

        changed |= Checkbox("Weather changes",
            () => this.config.WeatherNotificationEnabled, v => this.config.WeatherNotificationEnabled = v);

        Tooltip(
            "Announces the weather turning over, on its own line just above the "
            + "place name, so it never interrupts a location notice.\n\n"
            + "Weather runs on a fixed cycle of about 23 minutes, and arriving somewhere "
            + "never announces it — only an actual change does.");

        ImGui.Separator();

        changed |= Checkbox("Show the parent tier as a header",
            () => this.config.IncludeParentTierAsHeader, v => this.config.IncludeParentTierAsHeader = v);

        if (Checkbox("Hide the game's own area text",
                () => this.config.HideNativeAreaText, v => this.config.HideNativeAreaText = v))
        {
            changed = true;

            if (!this.config.HideNativeAreaText)
                this.actions.RestoreNativeAreaText();
        }

        Tooltip("Suppresses the native \"_AreaText\" flash, which draws underneath this plugin.");

        if (Checkbox("Hide the loading-screen zone title",
                () => this.config.HideNativeLoadingTitle, v => this.config.HideNativeLoadingTitle = v))
        {
            changed = true;

            if (!this.config.HideNativeLoadingTitle)
                this.actions.RestoreNativeLoadingTitle();
        }

        Tooltip(
            "Suppresses \"_LocationTitle\" and \"_LocationTitleShort\", the gold title\n" +
            "drawn over the loading screen, and shows the same names in this\n" +
            "plugin's style instead.");

        if (!changed)
            return;

        this.config.Save();
        this.actions.LivePreview(Sample);
    }
}
