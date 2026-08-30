using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private void DrawNotificationsTab()
    {
        using var tab = ImRaii.TabItem(Loc.Label("notifications.tab", "Notifications"));
        if (!tab) return;

        UiText.Wrapped(Loc.Get(
            "notifications.intro",
            "This plugin replaces the game's own location text rather than drawing alongside " +
            "it. If you turn the suppression below off, the game's version comes back."));
        ImGui.Separator();

        var changed = false;
        this.config.ZoneNotificationEnabled = Checkbox(
            Loc.Label("notifications.zone", "Zone changes"),
            this.config.ZoneNotificationEnabled, ref changed);

        this.config.AreaNotificationEnabled = Checkbox(
            Loc.Label("notifications.area", "Area changes"),
            this.config.AreaNotificationEnabled, ref changed);

        this.config.SubAreaNotificationEnabled = Checkbox(
            Loc.Label("notifications.subarea", "Sub-area changes"),
            this.config.SubAreaNotificationEnabled, ref changed);

        ImGui.Separator();

        this.config.WeatherNotificationEnabled = Checkbox(
            Loc.Label("notifications.weather", "Weather changes"),
            this.config.WeatherNotificationEnabled, ref changed);

        UiText.Tooltip(Loc.Get(
            "notifications.weather.tooltip",
            "Announces the weather turning over, on its own line just above the "
            + "place name, so it never interrupts a location notice.\n\n"
            + "Weather runs on a fixed cycle of about 23 minutes, and arriving anywhere "
            + "new announces what it is doing there, so it shows up with the place name "
            + "as you walk in."));

        using (ImRaii.Disabled(!this.config.WeatherNotificationEnabled))
        {
            this.config.ShowWeatherIcon = Checkbox(
                Loc.Label("notifications.weathericon", "Show the weather icon"),
                this.config.ShowWeatherIcon, ref changed);
        }

        UiText.Tooltip(Loc.Get(
            "notifications.weathericon.tooltip",
            "Draws the game's own icon for the weather to the left of its name."));

        ImGui.Separator();

        this.config.BannerNotificationEnabled = Checkbox(
            Loc.Label("notifications.banners", "Banners"),
            this.config.BannerNotificationEnabled, ref changed);

        UiText.Tooltip(Loc.Get(
            "notifications.banners.tooltip",
            "Redraws the game's full-screen banners — \"Quest Accepted\", "
            + "\"Duty Commenced\", \"Level Up!\" — in this plugin's lettering.\n\n"
            + "The wording is painted into the game's artwork rather than stored as "
            + "text, so only banners this plugin has words for are taken over. Any "
            + "it does not recognise keep the game's own."));

        using (ImRaii.Disabled(!this.config.BannerNotificationEnabled))
        {
            this.config.HideNativeBanner = Checkbox(
                Loc.Label("notifications.hidebanner", "Hide the game's own banner"),
                this.config.HideNativeBanner, ref changed);
        }

        UiText.Tooltip(Loc.Get(
            "notifications.hidebanner.tooltip",
            "Fades out the game's artwork so only this plugin's version shows.\n\n"
            + "Turn this off to see both, which is a quick way to check the "
            + "wording matches."));

        ImGui.Separator();

        var toggled = false;
        this.config.HideNativeAreaText = Checkbox(
            Loc.Label("notifications.hideareatext", "Hide the game's own area text"),
            this.config.HideNativeAreaText, ref toggled);

        if (toggled)
        {
            changed = true;

            if (!this.config.HideNativeAreaText)
                this.actions.RestoreNativeAreaText();
        }

        UiText.Tooltip(Loc.Get(
            "notifications.hideareatext.tooltip",
            "Suppresses the native \"_AreaText\" flash, which draws underneath this plugin."));

        var titleToggled = false;
        this.config.HideNativeLoadingTitle = Checkbox(
            Loc.Label("notifications.hideloadingtitle", "Hide the loading-screen zone title"),
            this.config.HideNativeLoadingTitle, ref titleToggled);

        if (titleToggled)
        {
            changed = true;

            if (!this.config.HideNativeLoadingTitle)
                this.actions.RestoreNativeLoadingTitle();
        }

        UiText.Tooltip(Loc.Get(
            "notifications.hideloadingtitle.tooltip",
            "Suppresses \"_LocationTitle\" and \"_LocationTitleShort\", the gold title\n" +
            "drawn over the loading screen, and shows the same names in this\n" +
            "plugin's style instead."));

        if (!changed)
            return;

        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }
}
