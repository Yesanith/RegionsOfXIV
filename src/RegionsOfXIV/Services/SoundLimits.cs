using System;
using System.IO;
using System.Linq;

namespace RegionsOfXIV.Services;

// What a sound file has to be before anything tries to decode it, asked without decoding it.
//
// The same shape as FontLimits.CustomFontProblem and for the same reason: the failure that
// matters happens off the draw thread, and by the time it does the player has no way to tell a
// file the plugin refused from a plugin that is simply not working. Every answer that can be had
// from the path alone is had here, immediately, and named on screen.
//
// Callers should cache the result. It touches the disk and the settings window draws every frame.
internal static class SoundLimits
{
    // Generous rather than tuned. Nothing sensible here is above a few megabytes, but the cap is
    // guarding against a video file or a disk image typed into the box by mistake, not policing
    // how someone encoded five seconds of audio.
    private const long MaxSoundBytes = 32L * 1024 * 1024;

    public static readonly string[] Extensions = [".wav", ".mp3"];

    public static string? CustomSoundProblem(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Loc.Get("sound.problem.none", "No sound file has been chosen yet.");

        if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            return Loc.Get("sound.problem.format", "Only .wav and .mp3 files can be played.");

        FileInfo file;

        try
        {
            file = new FileInfo(path);
        }
        catch (Exception)
        {
            return Loc.Get("sound.problem.unreadable", "That path cannot be read.");
        }

        // A directory answers Exists with false on FileInfo, so it would otherwise be reported as
        // a missing file, which sends someone looking for a file they can see is there.
        if (Directory.Exists(path))
            return Loc.Get("sound.problem.directory", "That is a folder, not a sound file.");

        if (!file.Exists)
            return Loc.Get("sound.problem.missing", "There is no file at that path any more.");

        if (file.Length == 0)
            return Loc.Get("sound.problem.empty", "That file is empty.");

        if (file.Length > MaxSoundBytes)
            return Loc.Get(
                "sound.problem.huge",
                "That file is far larger than a notification sound should be, so it is being left "
                + "alone.");

        return null;
    }
}
