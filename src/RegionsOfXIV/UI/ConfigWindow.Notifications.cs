using System;
using System.Globalization;
using System.Linq;
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
            "Redraws the game's full-screen banners (\"Quest Accepted\", "
            + "\"Duty Commenced\", \"Level Up!\") in this plugin's lettering.\n\n"
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

        using (ImRaii.Disabled(!this.config.BannerNotificationEnabled))
        {
            this.config.BannerGap = Slider(
                Loc.Label("notifications.bannergap", "Banner drop"),
                this.config.BannerGap, 0.5f, 5f,
                "%.2f " + Loc.Unit("units.lines", "lines"), ref changed);
        }

        UiText.Tooltip(Loc.Get(
            "notifications.bannergap.tooltip",
            "How far below a place name a banner sits, measured in lines of the\n" +
            "display text.\n\n" +
            "The two can be on screen together: entering a duty announces where you\n" +
            "are and then the party size a moment later. This is the room between\n" +
            "them. The drop is the same whether or not a name is up, so a banner on\n" +
            "its own also lands here."));

        using (ImRaii.Disabled(!this.config.BannerNotificationEnabled))
        {
            DrawBannerLanguage(ref changed);
        }

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

    // Offered from BannerNames.ByLanguage rather than from a list written here, so a language
    // cannot be picked that has no wording behind it. One that did would name nothing, and a
    // banner with no name keeps the game's own, so the setting would read as having switched
    // banners off.
    private void DrawBannerLanguage(ref bool changed)
    {
        using (var combo = ImRaii.Combo(
                   Loc.Label("notifications.bannerlanguage", "Banner language"),
                   BannerLanguageName(this.config.BannerNameLanguage)))
        {
            if (combo)
            {
                foreach (var option in BannerLanguages)
                {
                    // The code is the identity, not the name, for the same reason Choice uses the
                    // enum value: two languages whose names rendered alike would become one entry.
                    if (!ImGui.Selectable(
                            $"{BannerLanguageName(option)}###banner-language-{option ?? "client"}",
                            option == this.config.BannerNameLanguage))
                        continue;

                    this.config.BannerNameLanguage = option;
                    BannerNameResolver.Language = option;
                    changed = true;
                }
            }
        }

        UiText.Tooltip(Loc.Get(
            "notifications.bannerlanguage.tooltip",
            "Which language the banner wording is drawn in.\n\n"
            + "Following the client uses the language the game is in. Choosing another "
            + "replaces the game's own banner with this plugin's version in that language, "
            + "so on a German client set to English the German artwork is faded out and "
            + "English words are drawn in its place.\n\n"
            + "Only languages this plugin has wording for are listed. Banners it has no "
            + "wording for keep the game's own, whichever language is chosen."));
    }

    private static string?[] BannerLanguages =>
        [null, .. BannerNames.ByLanguage.Keys.OrderBy(code => code, StringComparer.Ordinal)];

    private static string BannerLanguageName(string? code)
    {
        if (code is null)
            return Loc.Get("notifications.bannerlanguage.follow", "Follow the client");

        try
        {
            return CultureInfo.GetCultureInfo(code).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

}
