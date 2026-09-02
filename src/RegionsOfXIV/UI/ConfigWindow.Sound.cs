using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    // A tab of its own rather than a block under any one kind of announcement, because a sound
    // belongs to all three at once: the place name, the weather and the banners each ask for the
    // same noise and configure it here.
    //
    // There is no volume slider, for either source. A game sound is mixed by the game, so the
    // sliders the player already has are the volume control and a second one here would fight
    // them. A file is not mixed by the game, so it is scaled by those same sliders on the way out
    // instead: see GameMixerRules. Either way the answer to "make it quieter" is the game's own
    // settings, which is one place rather than two that disagree.
    private void DrawSoundTab()
    {
        using var tab = ImRaii.TabItem(Loc.Label("sound.tab", "Sound"));
        if (!tab) return;

        UiText.Wrapped(Loc.Get(
            "sound.intro",
            "A short noise when a notification arrives, from the game's own sound effects or " +
            "from a file of your own."));
        ImGui.Separator();

        var changed = false;
        var enabled = this.config.SoundSource != SoundSource.Off;

        using (var combo = ImRaii.Combo(
                   Loc.Label("sound.source", "Sound"),
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
            "sound.source.tooltip",
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
                Loc.Label("sound.on.location", "Sound on place names"),
                this.config.SoundOnLocation, ref changed);

            this.config.SoundOnWeather = Checkbox(
                Loc.Label("sound.on.weather", "Sound on weather"),
                this.config.SoundOnWeather, ref changed);

            this.config.SoundOnBanner = Checkbox(
                Loc.Label("sound.on.banner", "Sound on banners"),
                this.config.SoundOnBanner, ref changed);
        }

        UiText.Tooltip(Loc.Get(
            "sound.on.tooltip",
            "Which kinds of notification make a sound. Two arriving together only ever make "
            + "one sound, whichever of them lands first."));

        if (!changed)
            return;

        MarkUnsaved();
    }

    // Numbered the way the player already knows them, from typing "<se.1>" in chat, so a sound
    // can be auditioned in game and then chosen here by the same number.
    private void DrawGameSoundChoice(ref bool changed)
    {
        var audition = Loc.Label("sound.play", "Play");

        ImGui.SetNextItemWidth(
            ImGui.GetContentRegionAvail().X - ButtonRowWidth(audition));

        using (var combo = ImRaii.Combo(
                   "###sound.gamesound", SoundEffectName(this.config.GameSoundId)))
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
            "sound.audition.tooltip",
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
        Loc.Format("sound.effect", "Sound effect {0}", id);

    private static string SoundSourceName(SoundSource source) => source switch
    {
        SoundSource.GameSound => Loc.Get("sound.source.game", "One of the game's"),
        SoundSource.File => Loc.Get("sound.source.file", "A file of your own"),
        _ => Loc.Get("sound.source.off", "None"),
    };
}
