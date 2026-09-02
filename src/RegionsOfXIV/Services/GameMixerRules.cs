namespace RegionsOfXIV.Services;

// Whether a sound file may be played right now and how loud, and if not, what to tell the player.
//
// It exists because NAudio bypasses the game's mixer entirely. A file goes out through a Windows
// audio device in the FFXIV process's own session, so not one of the player's in-game sound
// settings reaches it: left alone it would play at full volume into a muted client, or into
// headphones somebody walked away from an hour ago. Everything the mixer would have done to a
// game sound has to be done here by hand, and this is the one place that does it.
//
// Deliberately not in NotificationGate. The gate decides whether there is anything to announce at
// all, and this runs downstream of a decision it has already made: no notification, no sound, and
// nothing here can cause one. What it decides is a volume, which is a question only the file path
// has to ask, because a game sound is mixed by the game.
//
// The flag meanings come from watching the numbers move while the settings were changed in a
// running client, not from documentation: Dalamud describes every one of these as "This option is
// a UInt" and nothing else. Verified that way: a chat sound effect is on the System Sounds bus
// rather than Sound Effects, and IsSndSystem reads 1 when System Sounds is muted. IsSndMaster is
// taken to work the same way on the strength of the shared range, the shared default of 0, and the
// game shipping unmuted, which is inference rather than observation.
internal static class GameMixerRules
{
    // Volume is null when nothing should play. Reason is set only when a player pressing the
    // audition button would otherwise get silence and no explanation, which is the one case where
    // staying quiet is indistinguishable from being broken. A notification ignores it.
    internal readonly record struct MixerDecision(float? Volume, string? Reason);

    // One method rather than a volume one and a reason one, so the two can never answer
    // differently about the same settings.
    public static MixerDecision Decide(IGameAudio audio)
    {
        // Nothing could be read, which happens before the game's config is up. Treated as a
        // refusal rather than as full volume: this whole class exists because playing without
        // knowing the player's settings is the failure worth avoiding, and not being able to read
        // them is the purest case of it.
        if (!audio.Readable)
            return new MixerDecision(null, Loc.Get(
                "sound.silent.unreadable",
                "The game's sound settings cannot be read yet, so nothing is played. This clears "
                + "once you are in game."));

        if (audio.SoundDisabled)
            return new MixerDecision(null, Loc.Get(
                "sound.silent.disabled", "The game's sound is turned off, so nothing is played."));

        if (audio.MasterMuted || audio.SystemMuted)
            return new MixerDecision(null, Loc.Get(
                "sound.silent.muted",
                "The game is muted, so nothing is played. This follows the master and System "
                + "Sounds mute settings, because that is the bus a notification sound uses."));

        // Both flags, rather than either. Which of the two the game actually consults is not
        // known: the overall one defaults to 0 and the per-category ones to 1, which reads like two
        // levels, but a client with the feature off was seen holding 0 in all of them, so a
        // per-category row cannot be told apart from a stale one.
        //
        // Requiring both is the conservative reading, and it is chosen because the two ways of
        // being wrong are not equal. Refusing when the player would have allowed it costs a sound
        // they were not looking at anyway; allowing when they would have refused puts a noise into
        // a room they deliberately left quiet.
        if (!audio.WindowFocused && !(audio.UnfocusedSoundAllowed && audio.UnfocusedSystemSoundAllowed))
            return new MixerDecision(null, Loc.Get(
                "sound.silent.unfocused",
                "The game window is not in front and its setting for playing sound while it is "
                + "not active is off, so nothing is played."));

        // Linear in amplitude, and not claimed to match whatever curve the game applies to its own
        // sliders. Matching that would need a measurement nobody has made; being in the right
        // proportion, and quiet when the player asked for quiet, is what this is for.
        var scale = Fraction(audio.MasterVolume) * Fraction(audio.SystemVolume);

        if (scale <= 0f)
            return new MixerDecision(null, Loc.Get(
                "sound.silent.zero",
                "The game's master or System Sounds volume is at zero, so nothing is played."));

        return new MixerDecision(scale, null);
    }

    // Clamped rather than trusted. The range read back from the client is 0 to 100, but this is
    // one unchecked multiply away from playing a file at eight times its own amplitude.
    private static float Fraction(int volume) =>
        volume <= 0 ? 0f : (volume >= 100 ? 1f : volume / 100f);
}
