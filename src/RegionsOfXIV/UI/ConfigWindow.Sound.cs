using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // A page of its own rather than a group under any one kind of announcement, because a sound
    // belongs to all three at once: the place name, the weather and the banners each ask for the
    // same noise and configure it here.
    //
    // There is no volume slider for a game sound. It is mixed by the game, so the sliders the
    // player already has are the volume control and a second one here would fight them. A file
    // is not mixed by the game, so it is scaled by those same sliders on the way out (see
    // GameMixerRules) and by the one slider here on top.
    private void DrawSoundPage()
    {
        PageHeader.Draw(
            Loc.Get("sound.tab", "Sound"),
            Loc.Get(
                "sound.intro",
                "A short noise when a notification arrives, from the game's own sound effects or " +
                "from a file of your own."));

        var changed = false;
        var enabled = this.config.SoundSource != SoundSource.Off;

        using (SettingsGroup.Begin(Loc.Get("sound.group.source", "Source")))
        {
            this.config.SoundSource = Choice(
                "##rox-sound-source",
                Loc.Get("sound.source", "Sound"),
                Loc.Get(
                    "sound.source.tooltip",
                    "Plays a sound when a notification appears. Off by default.\n\n"
                    + "One of the game's is a chat sound effect, mixed by the game: the master volume and "
                    + "the System Sounds volume control it, rather than Sound Effects, and turning Sound "
                    + "Effects down will not quieten it.\n\n"
                    + "A file of your own is played by the plugin, which the game does not mix at all, so "
                    + "the plugin reads those same settings and follows them by hand. It stays silent "
                    + "while the game is muted, and while the window is not in front unless you have told "
                    + "the game to keep playing then."),
                this.config.SoundSource, SoundSourceLabels, ref changed);

            if (this.config.SoundSource == SoundSource.File)
            {
                DrawSoundFileChoice(ref changed);
            }
            else if (this.config.SoundSource == SoundSource.GameSound)
            {
                DrawGameSoundChoices(ref changed);
            }
        }

        if (this.config.SoundSource == SoundSource.File)
        {
            DrawSoundFileNotice();
        }

        using (SettingsGroup.Begin(Loc.Get("sound.group.on", "Plays on")))
        {
            var help = Loc.Get(
                "sound.on.tooltip",
                "Which kinds of notification make a sound. Two arriving together only ever make "
                + "one sound, whichever of them lands first.");

            this.config.SoundOnLocation = Toggle(
                "##rox-sound-location", Loc.Get("sound.on.location", "Sound on place names"), help,
                this.config.SoundOnLocation, ref changed, enabled);

            this.config.SoundOnWeather = Toggle(
                "##rox-sound-weather", Loc.Get("sound.on.weather", "Sound on weather"), help,
                this.config.SoundOnWeather, ref changed, enabled);

            this.config.SoundOnBanner = Toggle(
                "##rox-sound-banner", Loc.Get("sound.on.banner", "Sound on banners"), help,
                this.config.SoundOnBanner, ref changed, enabled);
        }

        if (!changed)
        {
            return;
        }

        MarkUnsaved();
    }

    // Numbered the way the player already knows them, from typing "<se.1>" in chat, so a sound
    // can be auditioned in game and then chosen here by the same number. Weather and banners can
    // take one of their own, or follow the one chosen for places.
    private void DrawGameSoundChoices(ref bool changed)
    {
        var places = this.config.GameSoundId;
        if (GameSoundRow("##rox-game-sound", Loc.Get("sound.gamesound", "Sound effect"), ref places, SoundCategory.Location, withSame: false))
        {
            this.config.GameSoundId = places;
            changed = true;
        }

        var weather = this.config.GameSoundIdWeather;
        if (GameSoundRow("##rox-game-sound-weather", Loc.Get("sound.gamesound.weather", "Weather"), ref weather, SoundCategory.Weather, withSame: true))
        {
            this.config.GameSoundIdWeather = weather;
            changed = true;
        }

        var banner = this.config.GameSoundIdBanner;
        if (GameSoundRow("##rox-game-sound-banner", Loc.Get("sound.gamesound.banner", "Banners"), ref banner, SoundCategory.Banner, withSame: true))
        {
            this.config.GameSoundIdBanner = banner;
            changed = true;
        }
    }

    // The stored id is 1 to 16, or 0 for "the same as places" where that is offered; the dropdown
    // index is that id shifted down by one, or with the "same" entry in front.
    private bool GameSoundRow(string id, string label, ref int stored, SoundCategory category, bool withSame)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var play = Loc.Get("sound.play", "Play");
        var playWidth = PillButton.Width(play, FontAwesomeIcon.Play);
        var controlWidth = Layout.RowDropdownWidth + ((playWidth + (ButtonGap * scale)) / scale);

        var row = SettingsRow.Begin(label, null, controlWidth);

        var labels = withSame ? GameSoundLabelsWithSame() : GameSoundLabels();
        var selected = withSame ? Math.Clamp(stored, 0, GameSoundCount) : Math.Clamp(stored - 1, 0, GameSoundCount - 1);
        var picked = false;
        if (Dropdown.Draw(id, labels, ref selected, Layout.RowDropdownWidth))
        {
            stored = withSame ? selected : selected + 1;
            picked = true;

            // Auditioned on pick as well as on the button, because choosing from a list of
            // sixteen numbers is otherwise choosing blind.
            this.actions.AuditionSound(category);
        }

        ImGui.SameLine(0f, ButtonGap * scale);
        ImGui.PushID(id);
        if (PillButton.Draw("##rox-game-sound-play", play, Styling.AccentGold, PillButton.Emphasis.Tinted, FontAwesomeIcon.Play,
                tooltip: Loc.Get(
                    "sound.audition.tooltip",
                    "Plays the chosen sound now. It ignores the short gap that stops two notifications "
                    + "sounding at once, so pressing it repeatedly always makes a noise.")))
        {
            this.actions.AuditionSound(category);
        }

        ImGui.PopID();
        row.End();

        return picked;
    }

    // The typed path is buffered rather than written straight to the config, matching the custom
    // font row: committing on every keystroke would hit the disk once per character, because the
    // check below runs on whatever is stored.
    private string? soundPathBuffer;

    private string? soundPathChecked;

    private string? soundPathProblem;

    private string? soundPathLabel;

    private int soundPathLabelGeneration = -1;

    private string? soundNotice;

    private int soundNoticeGeneration = -1;

    private void DrawSoundFileChoice(ref bool changed)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var stored = this.config.SoundFilePath;
        var buffer = this.soundPathBuffer ??= stored;

        SettingsRow.Block(Loc.Get("sound.file", "Sound file"), null);

        var browse = Loc.Get("sound.browse", "Browse");
        var clear = Loc.Get("sound.clear", "Clear");
        var play = Loc.Get("sound.play", "Play");
        var buttons = PillButton.Width(browse, FontAwesomeIcon.FolderOpen)
                      + PillButton.Width(clear, FontAwesomeIcon.Times)
                      + PillButton.Width(play, FontAwesomeIcon.Play)
                      + (ButtonGap * 3f * scale);
        var fieldWidth = Math.Max(SettingsGroup.InnerWidth() - buttons, 120f * scale);

        var field = TextField.Draw(
            "##rox-sound-path",
            Loc.Get("sound.path.hint", "Path to a .wav or .mp3 file"),
            ref buffer,
            fieldWidth);

        this.soundPathBuffer = buffer;

        if (field.Committed)
        {
            stored = buffer.Trim().Trim('"');
            this.config.SoundFilePath = stored;
            this.soundPathBuffer = stored;
            changed = true;
        }
        else if (!field.Active && buffer != stored)
        {
            this.soundPathBuffer = stored;
        }

        ImGui.SameLine(0f, ButtonGap * scale);
        if (PillButton.Draw("##rox-sound-browse", browse, Styling.AccentGold, PillButton.Emphasis.Tinted, FontAwesomeIcon.FolderOpen, height: Layout.FieldHeight))
        {
            BrowseForSound();
        }

        ImGui.SameLine(0f, ButtonGap * scale);
        if (PillButton.Draw("##rox-sound-clear", clear, Styling.AccentRose, PillButton.Emphasis.Ghost, FontAwesomeIcon.Times,
                enabled: stored.Length > 0, height: Layout.FieldHeight))
        {
            this.config.SoundFilePath = string.Empty;
            this.soundPathBuffer = string.Empty;
            changed = true;
        }

        ImGui.SameLine(0f, ButtonGap * scale);
        if (PillButton.Draw("##rox-sound-play", play, Styling.AccentGold, PillButton.Emphasis.Filled, FontAwesomeIcon.Play,
                height: Layout.FieldHeight,
                tooltip: Loc.Get(
                    "sound.play.tooltip",
                    "Plays the chosen file now. It ignores the short gap that stops two notifications "
                    + "sounding at once, so pressing it repeatedly always tries again.")))
        {
            this.actions.AuditionSound(SoundCategory.Location);
        }

        DrawSoundFileStatus(stored);
        SettingsRow.EndBlock();

        var volume = (float)this.config.SoundFileVolume;
        var edited = false;
        volume = Slider(
            "##rox-sound-volume",
            Loc.Get("sound.filevolume", "Volume"),
            Loc.Get(
                "sound.filevolume.tooltip",
                "On top of the game's own master and System Sounds volumes, which a file\n" +
                "follows as well. A game sound has no slider here because the game's own\n" +
                "settings already are its volume."),
            volume, 0f, 100f, "%.0f%%", ref edited);

        if (edited)
        {
            this.config.SoundFileVolume = (int)MathF.Round(volume);
            changed = true;
        }
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
            this.soundPathLabel = null;
        }

        if ((this.soundPathProblem ?? this.actions.SoundFileProblem(path)) is { } fault)
        {
            SettingsRow.Note(fault, Styling.AccentRoseSoft);
            return;
        }

        if (this.soundPathLabel is null || this.soundPathLabelGeneration != Loc.Generation)
        {
            this.soundPathLabelGeneration = Loc.Generation;
            this.soundPathLabel = Loc.Format("sound.playingwith", "Playing {0}.", Path.GetFileName(path));
        }

        SettingsRow.Note(this.soundPathLabel, Styling.AccentMintSoft);

        if (this.actions.SoundSilenceReason() is { } quiet)
        {
            SettingsRow.Note(quiet, Styling.AccentAmberSoft);
        }
    }

    private void DrawSoundFileNotice()
    {
        if (this.soundNotice is null || this.soundNoticeGeneration != Loc.Generation)
        {
            this.soundNoticeGeneration = Loc.Generation;
            this.soundNotice = Loc.Format(
                "sound.custom.notice",
                "A file you supply is played as it is, and it stays yours to look after. Anything "
                + "past {0} seconds is cut off, so a whole track will not play over a notification "
                + "that has already gone. A file the plugin cannot read, or one you do not hold a "
                + "licence for, is on you rather than on Regions of XIV.",
                5);
        }

        Warn(this.soundNotice);
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
        {
            return;
        }

        this.config.SoundFilePath = chosen[0];
        this.soundPathBuffer = chosen[0];
        this.config.Save();

        // Auditioned on pick, for the same reason the game sounds are: a file chosen from a
        // browser is a filename, and hearing it is the only way to know it was the right one.
        this.actions.AuditionSound(SoundCategory.Location);
    }

    private const int GameSoundCount = 16;

    private static readonly string[] GameSoundNames = new string[GameSoundCount];

    private static readonly string[] GameSoundNamesWithSame = new string[GameSoundCount + 1];

    private static int GameSoundGeneration = -1;

    private static string[] GameSoundLabels()
    {
        RefreshGameSoundLabels();
        return GameSoundNames;
    }

    private static string[] GameSoundLabelsWithSame()
    {
        RefreshGameSoundLabels();
        return GameSoundNamesWithSame;
    }

    private static void RefreshGameSoundLabels()
    {
        if (GameSoundGeneration == Loc.Generation)
        {
            return;
        }

        GameSoundNamesWithSame[0] = Loc.Get("sound.gamesound.same", "The same as places");
        for (var index = 0; index < GameSoundCount; index++)
        {
            GameSoundNames[index] = Loc.Format("sound.effect", "Sound effect {0}", index + 1);
            GameSoundNamesWithSame[index + 1] = GameSoundNames[index];
        }

        GameSoundGeneration = Loc.Generation;
    }

    private static readonly ChoiceLabels<SoundSource> SoundSourceLabels = new(SoundSourceName);

    private static string SoundSourceName(SoundSource source) => source switch
    {
        SoundSource.GameSound => Loc.Get("sound.source.game", "One of the game's"),
        SoundSource.File => Loc.Get("sound.source.file", "A file of your own"),
        _ => Loc.Get("sound.source.off", "None"),
    };
}
