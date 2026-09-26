using System;
using System.Globalization;
using System.Linq;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // What gets announced, when to stay quiet, and what of the game's own to suppress, in that
    // order. The page reads outward: the first three groups each add something to the screen, the
    // quiet rules take it away again under conditions, and the suppression is about the game's UI
    // rather than about this plugin's, so it ends the page as the least often touched of the four.
    private void DrawAnnouncementsPage()
    {
        PageHeader.Draw(
            Loc.Get("announcements.tab", "Announcements"),
            Loc.Get(
                "announcements.intro",
                "This plugin replaces the game's own location text rather than drawing alongside " +
                "it. If you turn the suppression below off, the game's version comes back."));

        var changed = false;

        using (SettingsGroup.Begin(Loc.Get("announcements.group.places", "Places")))
        {
            this.config.ZoneNotificationEnabled = Toggle(
                "##rox-zone", Loc.Get("announcements.zone", "Zone changes"), null,
                this.config.ZoneNotificationEnabled, ref changed);

            this.config.AreaNotificationEnabled = Toggle(
                "##rox-area", Loc.Get("announcements.area", "Area changes"), null,
                this.config.AreaNotificationEnabled, ref changed);

            this.config.SubAreaNotificationEnabled = Toggle(
                "##rox-subarea", Loc.Get("announcements.subarea", "Sub-area changes"), null,
                this.config.SubAreaNotificationEnabled, ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("announcements.group.weather", "Weather")))
        {
            this.config.WeatherNotificationEnabled = Toggle(
                "##rox-weather",
                Loc.Get("announcements.weather", "Weather changes"),
                Loc.Get(
                    "announcements.weather.tooltip",
                    "Announces the weather turning over, on its own line just above the "
                    + "place name, so it never interrupts a location notice.\n\n"
                    + "Weather runs on a fixed cycle of about 23 minutes, and arriving anywhere "
                    + "new announces what it is doing there, so it shows up with the place name "
                    + "as you walk in."),
                this.config.WeatherNotificationEnabled, ref changed);

            this.config.ShowWeatherIcon = Toggle(
                "##rox-weather-icon",
                Loc.Get("announcements.weathericon", "Show the weather icon"),
                Loc.Get(
                    "announcements.weathericon.tooltip",
                    "Draws the game's own icon for the weather to the left of its name."),
                this.config.ShowWeatherIcon, ref changed, this.config.WeatherNotificationEnabled);
        }

        using (SettingsGroup.Begin(Loc.Get("announcements.group.banners", "Banners")))
        {
            DrawBannerSettings(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("announcements.group.quiet", "When to stay quiet")))
        {
            DrawQuietRules(ref changed);
        }

        using (SettingsGroup.Begin(Loc.Get("announcements.group.native", "The game's own text")))
        {
            DrawNativeSuppression(ref changed);
        }

        if (!changed)
        {
            return;
        }

        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    private void DrawBannerSettings(ref bool changed)
    {
        this.config.BannerNotificationEnabled = Toggle(
            "##rox-banners",
            Loc.Get("announcements.banners", "Banners"),
            Loc.Get(
                "announcements.banners.tooltip",
                "Redraws the game's full-screen banners (\"Quest Accepted\", "
                + "\"Duty Commenced\", \"Level Up!\") in this plugin's lettering.\n\n"
                + "The wording is painted into the game's artwork rather than stored as "
                + "text, so only banners this plugin has words for are taken over. Any "
                + "it does not recognise keep the game's own."),
            this.config.BannerNotificationEnabled, ref changed);

        var enabled = this.config.BannerNotificationEnabled;

        this.config.HideNativeBanner = Toggle(
            "##rox-hide-banner",
            Loc.Get("announcements.hidebanner", "Hide the game's own banner"),
            Loc.Get(
                "announcements.hidebanner.tooltip",
                "Fades out the game's artwork so only this plugin's version shows.\n\n"
                + "Turn this off to see both, which is a quick way to check the "
                + "wording matches."),
            this.config.HideNativeBanner, ref changed, enabled);

        this.config.BannerGap = Slider(
            "##rox-banner-gap",
            Loc.Get("announcements.bannergap", "Banner drop"),
            Loc.Get(
                "announcements.bannergap.tooltip",
                "How far below a place name a banner sits, measured in lines of the\n" +
                "display text.\n\n" +
                "The two can be on screen together: entering a duty announces where you\n" +
                "are and then the party size a moment later. This is the room between\n" +
                "them. The drop is the same whether or not a name is up, so a banner on\n" +
                "its own also lands here."),
            this.config.BannerGap, 0.5f, 5f,
            "%.2f " + Loc.Unit("units.lines", "lines"), ref changed, enabled: enabled);

        DrawBannerLanguage(ref changed, enabled);
    }

    private void DrawQuietRules(ref bool changed)
    {
        this.config.HideInCombat = Toggle(
            "##rox-hide-combat", Loc.Get("announcements.hidecombat", "Hide during combat"), null,
            this.config.HideInCombat, ref changed);

        this.config.HideInDuty = Toggle(
            "##rox-hide-duty", Loc.Get("announcements.hideduty", "Hide inside duties"), null,
            this.config.HideInDuty, ref changed);

        this.config.HideWhileTravellingFast = Toggle(
            "##rox-skip-fast",
            Loc.Get("announcements.skipfast", "Skip sub-areas while travelling quickly"),
            Loc.Get(
                "announcements.skipfast.tooltip",
                "Only affects sub-areas, and only above a speed no ground travel reaches,\n" +
                "so it comes into play when flying. Zone and area changes are always\n" +
                "announced however fast you are moving."),
            this.config.HideWhileTravellingFast, ref changed);
    }

    // Both toggles have to tell the plugin to put the game's own back when they are switched off,
    // which is why neither can go through the shared changed flag alone: the restore happens once,
    // on the frame the switch is cleared, rather than on every frame the setting is false.
    private void DrawNativeSuppression(ref bool changed)
    {
        var toggled = false;
        this.config.HideNativeAreaText = Toggle(
            "##rox-hide-areatext",
            Loc.Get("announcements.hideareatext", "Hide the game's own area text"),
            Loc.Get(
                "announcements.hideareatext.tooltip",
                "Suppresses the native \"_AreaText\" flash, which draws underneath this plugin."),
            this.config.HideNativeAreaText, ref toggled);

        if (toggled)
        {
            changed = true;

            if (!this.config.HideNativeAreaText)
            {
                this.actions.RestoreNativeAreaText();
            }
        }

        var titleToggled = false;
        this.config.HideNativeLoadingTitle = Toggle(
            "##rox-hide-loadingtitle",
            Loc.Get("announcements.hideloadingtitle", "Hide the loading-screen zone title"),
            Loc.Get(
                "announcements.hideloadingtitle.tooltip",
                "Suppresses \"_LocationTitle\" and \"_LocationTitleShort\", the gold title\n" +
                "drawn over the loading screen, and shows the same names in this\n" +
                "plugin's style instead."),
            this.config.HideNativeLoadingTitle, ref titleToggled);

        if (titleToggled)
        {
            changed = true;

            if (!this.config.HideNativeLoadingTitle)
            {
                this.actions.RestoreNativeLoadingTitle();
            }
        }
    }

    // Offered from BannerNames.ByLanguage rather than from a list written here, so a language
    // cannot be picked that has no wording behind it. One that did would name nothing, and a
    // banner with no name keeps the game's own, so the setting would read as having switched
    // banners off.
    private void DrawBannerLanguage(ref bool changed, bool enabled)
    {
        var row = SettingsRow.Begin(
            Loc.Get("announcements.bannerlanguage", "Banner language"),
            Loc.Get(
                "announcements.bannerlanguage.tooltip",
                "Which language the banner wording is drawn in.\n\n"
                + "Following the client uses the language the game is in. Choosing another "
                + "replaces the game's own banner with this plugin's version in that language, "
                + "so on a German client set to English the German artwork is faded out and "
                + "English words are drawn in its place.\n\n"
                + "Only languages this plugin has wording for are listed. Banners it has no "
                + "wording for keep the game's own, whichever language is chosen."),
            Layout.RowDropdownWidth, 0f, enabled);

        var labels = BannerLanguageLabels();
        var selected = Array.IndexOf(BannerLanguages, this.config.BannerNameLanguage);
        if (Dropdown.Draw("##rox-banner-language", labels, ref selected, Layout.RowDropdownWidth, enabled))
        {
            this.config.BannerNameLanguage = BannerLanguages[selected];
            BannerNameResolver.Language = this.config.BannerNameLanguage;
            changed = true;
        }

        row.End();
    }

    private static readonly string?[] BannerLanguages =
        [null, .. BannerNames.ByLanguage.Keys.OrderBy(code => code, StringComparer.Ordinal)];

    private static readonly string[] BannerLanguageNames = new string[BannerLanguages.Length];

    private static int BannerLanguageGeneration = -1;

    private static string[] BannerLanguageLabels()
    {
        if (BannerLanguageGeneration == Loc.Generation)
        {
            return BannerLanguageNames;
        }

        for (var index = 0; index < BannerLanguages.Length; index++)
        {
            BannerLanguageNames[index] = BannerLanguageName(BannerLanguages[index]);
        }

        BannerLanguageGeneration = Loc.Generation;
        return BannerLanguageNames;
    }

    private static string BannerLanguageName(string? code)
    {
        if (code is null)
        {
            return Loc.Get("announcements.bannerlanguage.follow", "Follow the client");
        }

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
