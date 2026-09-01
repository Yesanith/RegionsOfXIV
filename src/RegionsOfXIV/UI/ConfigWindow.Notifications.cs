using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
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

        ImGui.Separator();

        DrawSound(ref changed);

        if (!changed)
            return;

        MarkUnsaved();
        this.actions.LivePreview(Sample);
    }

    // Last on this tab rather than beside one feature, because a sound belongs to all three kinds
    // of announcement at once: the sections above each configure one thing that gets announced,
    // and this configures what any of them sounds like.
    //
    // There is no volume slider, for either source. A game sound is mixed by the game, so the
    // sliders the player already has are the volume control and a second one here would fight
    // them. A file is not mixed by the game, so it is scaled by those same sliders on the way out
    // instead: see GameMixerRules. Either way the answer to "make it quieter" is the game's own
    // settings, which is one place rather than two that disagree.
    private void DrawSound(ref bool changed)
    {
        var enabled = this.config.SoundSource != SoundSource.Off;

        using (var combo = ImRaii.Combo(
                   Loc.Label("notifications.sound", "Sound"),
                   SoundSourceName(this.config.SoundSource)))
        {
            if (combo)
            {
                foreach (var option in Enum.GetValues<SoundSource>())
                {
                    if (!ImGui.Selectable(
                            $"{SoundSourceName(option)}###sound-source-{option}",
                            option == this.config.SoundSource))
                        continue;

                    this.config.SoundSource = option;
                    changed = true;
                }
            }
        }

        UiText.Tooltip(Loc.Get(
            "notifications.sound.tooltip",
            "Plays a sound when a notification appears. Off by default.\n\n"
            + "One of the game's is a chat sound effect, mixed by the game: the master volume and "
            + "the System Sounds volume control it, rather than Sound Effects, and turning Sound "
            + "Effects down will not quieten it.\n\n"
            + "A file of your own is played by the plugin, which the game does not mix at all, so "
            + "the plugin reads those same settings and follows them by hand. It stays silent "
            + "while the game is muted, and while the window is not in front unless you have told "
            + "the game to keep playing then."));

        using (ImRaii.Disabled(!enabled))
        {
            if (this.config.SoundSource == SoundSource.File)
                DrawSoundFileChoice(ref changed);
            else
                DrawGameSoundChoice(ref changed);

            this.config.SoundOnLocation = Checkbox(
                Loc.Label("notifications.soundon.location", "Sound on place names"),
                this.config.SoundOnLocation, ref changed);

            this.config.SoundOnWeather = Checkbox(
                Loc.Label("notifications.soundon.weather", "Sound on weather"),
                this.config.SoundOnWeather, ref changed);

            this.config.SoundOnBanner = Checkbox(
                Loc.Label("notifications.soundon.banner", "Sound on banners"),
                this.config.SoundOnBanner, ref changed);
        }

        UiText.Tooltip(Loc.Get(
            "notifications.soundon.tooltip",
            "Which kinds of notification make a sound. Two arriving together only ever make "
            + "one sound, whichever of them lands first."));
    }

    // Numbered the way the player already knows them, from typing "<se.1>" in chat, so a sound
    // can be auditioned in game and then chosen here by the same number.
    private void DrawGameSoundChoice(ref bool changed)
    {
        var audition = Loc.Label("notifications.sound.audition", "Play");

        ImGui.SetNextItemWidth(
            ImGui.GetContentRegionAvail().X - ButtonRowWidth(audition));

        using (var combo = ImRaii.Combo(
                   "###notifications.gamesound", SoundEffectName(this.config.GameSoundId)))
        {
            if (combo)
            {
                for (var id = 1; id <= GameSoundCount; id++)
                {
                    if (!ImGui.Selectable(
                            $"{SoundEffectName(id)}###game-sound-{id}", id == this.config.GameSoundId))
                        continue;

                    this.config.GameSoundId = id;
                    changed = true;

                    // Auditioned on pick as well as on the button, because choosing from a list of
                    // sixteen numbers is otherwise choosing blind.
                    this.actions.AuditionSound();
                }
            }
        }

        ImGui.SameLine();

        if (ImGui.Button(audition))
            this.actions.AuditionSound();

        UiText.Tooltip(Loc.Get(
            "notifications.sound.audition.tooltip",
            "Plays the chosen sound now. It ignores the short gap that stops two notifications "
            + "sounding at once, so pressing it repeatedly always makes a noise."));
    }

    // The typed path is buffered rather than written straight to the config, matching the custom
    // font row: committing on every keystroke would hit the disk once per character, because the
    // check below runs on whatever is stored.
    private string? soundPathBuffer;

    private string? soundPathChecked;

    private string? soundPathProblem;

    private void DrawSoundFileChoice(ref bool changed)
    {
        var stored = this.config.SoundFilePath;
        var buffer = this.soundPathBuffer ??= stored;

        var browse = Loc.Label("sound.browse", "Browse");
        var clear = Loc.Label("sound.clear", "Clear");
        var play = Loc.Label("sound.play", "Play");

        ImGui.SetNextItemWidth(Math.Max(
            ImGui.GetContentRegionAvail().X - ButtonRowWidth(browse, clear, play),
            120f * ImGuiHelpers.GlobalScale));

        ImGui.InputTextWithHint(
            "##sound-path",
            Loc.Get("sound.path.hint", "Path to a .wav or .mp3 file"),
            ref buffer,
            512);

        this.soundPathBuffer = buffer;

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            stored = buffer.Trim().Trim('"');
            this.config.SoundFilePath = stored;
            this.soundPathBuffer = stored;
            changed = true;
        }
        else if (!ImGui.IsItemActive() && buffer != stored)
        {
            this.soundPathBuffer = stored;
        }

        ImGui.SameLine();

        if (ImGui.Button(browse))
            BrowseForSound();

        ImGui.SameLine();

        using (ImRaii.Disabled(stored.Length == 0))
        {
            if (ImGui.Button(clear))
            {
                this.config.SoundFilePath = string.Empty;
                this.soundPathBuffer = string.Empty;
                changed = true;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button(play))
            this.actions.AuditionSound();

        UiText.Tooltip(Loc.Get(
            "sound.play.tooltip",
            "Plays the chosen file now. It ignores the short gap that stops two notifications "
            + "sounding at once, so pressing it repeatedly always tries again."));

        DrawSoundFileStatus(stored);
        DrawSoundFileNotice();
    }

    // Three separate things can be wrong and they are found at three different moments: the path
    // itself, checked here and immediately; the decode, which happens on another thread and only
    // reports afterwards; and the game's own settings, which are neither a fault nor the plugin's
    // to fix but do explain a Play button that makes no noise.
    private void DrawSoundFileStatus(string path)
    {
        if (this.soundPathChecked != path)
        {
            this.soundPathChecked = path;
            this.soundPathProblem = SoundLimits.CustomSoundProblem(path);
        }

        if ((this.soundPathProblem ?? this.actions.SoundFileProblem()) is { } fault)
        {
            Warn(FaultColor, fault);
            return;
        }

        UiText.Disabled(Loc.Format(
            "sound.playingwith", "Playing {0}.", Path.GetFileName(path)));

        if (this.actions.SoundSilenceReason() is { } quiet)
            Warn(CautionColor, quiet);
    }

    private static void DrawSoundFileNotice()
    {
        ImGui.Spacing();

        Warn(
            CautionColor,
            Loc.Format(
                "sound.custom.notice",
                "A file you supply is played as it is, and it stays yours to look after. Anything "
                + "past {0} seconds is cut off, so a whole track will not play over a notification "
                + "that has already gone. A file the plugin cannot read, or one you do not hold a "
                + "licence for, is on you rather than on Regions of XIV.",
                5));
    }

    private void BrowseForSound()
    {
        this.fileDialogs.OpenFileDialog(
            Loc.Get("sound.dialog.title", "Choose a sound file"),
            // Not translated: the braces are Dalamud's filter syntax rather than punctuation, and a
            // translator has no way to know that breaking them stops the dialog listing anything.
            "Sounds{.wav,.mp3}",
            AdoptSound,
            1,
            string.Empty,
            false);
    }

    private void AdoptSound(bool picked, List<string> chosen)
    {
        if (!picked || chosen.Count == 0 || string.IsNullOrWhiteSpace(chosen[0]))
            return;

        this.config.SoundFilePath = chosen[0];
        this.soundPathBuffer = chosen[0];
        this.config.Save();

        // Auditioned on pick, for the same reason the game sounds are: a file chosen from a
        // browser is a filename, and hearing it is the only way to know it was the right one.
        this.actions.AuditionSound();
    }

    private const int GameSoundCount = 16;

    private static string SoundEffectName(int id) =>
        Loc.Format("notifications.sound.effect", "Sound effect {0}", id);

    private static string SoundSourceName(SoundSource source) => source switch
    {
        SoundSource.GameSound => Loc.Get("notifications.sound.game", "One of the game's"),
        SoundSource.File => Loc.Get("notifications.sound.file", "A file of your own"),
        _ => Loc.Get("notifications.sound.off", "None"),
    };

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
