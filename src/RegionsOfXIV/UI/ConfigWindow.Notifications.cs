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

        var changed = false;
        this.config.ZoneNotificationEnabled = Checkbox(
            "Zone changes", this.config.ZoneNotificationEnabled, ref changed);

        this.config.AreaNotificationEnabled = Checkbox(
            "Area changes", this.config.AreaNotificationEnabled, ref changed);

        this.config.SubAreaNotificationEnabled = Checkbox(
            "Sub-area changes", this.config.SubAreaNotificationEnabled, ref changed);

        ImGui.Separator();

        this.config.WeatherNotificationEnabled = Checkbox(
            "Weather changes", this.config.WeatherNotificationEnabled, ref changed);

        Tooltip(
            "Announces the weather turning over, on its own line just above the "
            + "place name, so it never interrupts a location notice.\n\n"
            + "Weather runs on a fixed cycle of about 23 minutes, and arriving anywhere "
            + "new announces what it is doing there, so it shows up with the place name "
            + "as you walk in.");

        using (ImRaii.Disabled(!this.config.WeatherNotificationEnabled))
        {
            this.config.ShowWeatherIcon = Checkbox(
                "Show the weather icon", this.config.ShowWeatherIcon, ref changed);
        }

        Tooltip("Draws the game's own icon for the weather to the left of its name.");

        ImGui.Separator();

        this.config.BannerNotificationEnabled = Checkbox(
            "Banners", this.config.BannerNotificationEnabled, ref changed);

        Tooltip(
            "Redraws the game's full-screen banners — \"Quest Accepted\", "
            + "\"Duty Commenced\", \"Level Up!\" — in this plugin's lettering.\n\n"
            + "The wording is painted into the game's artwork rather than stored as "
            + "text, so only banners this plugin has words for are taken over. Any "
            + "it does not recognise keep the game's own.");

        using (ImRaii.Disabled(!this.config.BannerNotificationEnabled))
        {
            this.config.HideNativeBanner = Checkbox(
            "Hide the game's own banner", this.config.HideNativeBanner, ref changed);
        }

        Tooltip(
            "Fades out the game's artwork so only this plugin's version shows.\n\n"
            + "Turn this off to see both, which is a quick way to check the "
            + "wording matches.");

        ImGui.Separator();

        var toggled = false;
        this.config.HideNativeAreaText = Checkbox("Hide the game's own area text", this.config.HideNativeAreaText, ref toggled);

        if (toggled)
        {
            changed = true;

            if (!this.config.HideNativeAreaText)
                this.actions.RestoreNativeAreaText();
        }

        Tooltip("Suppresses the native \"_AreaText\" flash, which draws underneath this plugin.");

        var titleToggled = false;
        this.config.HideNativeLoadingTitle = Checkbox(
            "Hide the loading-screen zone title", this.config.HideNativeLoadingTitle, ref titleToggled);

        if (titleToggled)
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

        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }
}
