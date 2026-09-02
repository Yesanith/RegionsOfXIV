using System;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace RegionsOfXIV.Services;

// Which announcement asked for the sound. The three match the three real entry points on
// INotificationSink, so a push already says which category it is and nothing had to be added to
// the interface to carry it.
internal enum SoundCategory
{
    Location,
    Weather,
    Banner,
}

// The sound a notification makes, and the one rule about how often. Nothing else.
//
// It decides nothing about whether to announce. NotificationGate owns that, and this is called
// from the sink, which is downstream of it: no notification, no sound. A second set of quiet rules
// here would be a second thing to keep in step with the gate, and they would drift.
internal sealed class NotificationSounds
{
    // Long enough to swallow a pile-up, short enough to leave separate events alone.
    //
    // The gate already keeps two location notices 2000ms apart, so the only sounds that can land
    // close together are cross-lane: an arrival announces its weather in the same call, and a
    // party banner arrives about two milliseconds after the zone arrival that admits you to a
    // duty. At sixty frames a second this is about fifteen frames, which covers those with room
    // to spare while still letting two genuinely separate events a second apart both sound.
    //
    // The later sound is dropped rather than queued. A dropped sound is unnoticeable; two
    // overlapping ones are the thing being avoided, and a queued one would arrive detached from
    // whatever caused it.
    private static readonly TimeSpan MinimumGap = TimeSpan.FromMilliseconds(250);

    private readonly ISoundSettings config;
    private readonly Func<DateTime> now;
    private readonly Action<uint> play;
    private readonly Func<string, bool>? playFile;

    private DateTime nextAllowed = DateTime.MinValue;

    // playFile has no default the way play does, because a file needs a device and the game's own
    // audio settings and neither can be reached from a static. Plugin.cs always supplies one; a
    // test that leaves it out is saying it is not exercising the file path, and File then behaves
    // as it did before there was one, which is silence.
    public NotificationSounds(
        ISoundSettings config,
        Func<DateTime>? clock = null,
        Action<uint>? play = null,
        Func<string, bool>? playFile = null)
    {
        this.config = config;
        this.now = clock ?? (() => DateTime.UtcNow);
        this.play = play ?? PlayGameSound;
        this.playFile = playFile;
    }

    // True when a sound was actually played, which is what the tests assert on. Callers ignore it:
    // there is nothing sensible for a notification to do about a sound it did not make.
    public bool Play(SoundCategory category)
    {
        if (!IsEnabledFor(category))
            return false;

        if (this.config.SoundSource == SoundSource.Off)
            return false;

        var at = this.now();

        if (at < this.nextAllowed)
            return false;

        this.nextAllowed = at + MinimumGap;

        return Emit();
    }

    // Bypasses the gap deliberately: this is the audition button and the point of it is to hear
    // the sound now. It cannot spam anything, because pressing it is the spam.
    public void PlayNow()
    {
        if (this.config.SoundSource == SoundSource.Off)
            return;

        this.nextAllowed = this.now() + MinimumGap;

        Emit();
    }

    // The interval is spent before this is reached, in both callers, so a file that turns out to
    // be unplayable still costs the same quarter second as one that plays. That is deliberate: the
    // alternative is retrying a broken file on every notification, and the interval is the only
    // thing standing between a broken file and a decode attempt per announcement.
    //
    // False from the file path means the sound did not happen, which is what the caller's own
    // return value says. It is not an error here: the game being muted is a perfectly ordinary
    // reason, and the ones that are faults are reported by the player itself.
    private bool Emit()
    {
        if (this.config.SoundSource == SoundSource.File)
            return this.playFile is { } file && file(this.config.SoundFilePath);

        this.play(SoundEffectFor(this.config.GameSoundId));

        return true;
    }

    private bool IsEnabledFor(SoundCategory category) => category switch
    {
        SoundCategory.Location => this.config.SoundOnLocation,
        SoundCategory.Weather => this.config.SoundOnWeather,
        SoundCategory.Banner => this.config.SoundOnBanner,
        _ => false,
    };

    // The stored number is the one the player reads, matching "<se.1>" and the game's own sound
    // settings, so it is 1 to 16 rather than 0 to 15. Clamped rather than validated: a number from
    // a hand-edited config should make the nearest sound rather than nothing at all.
    //
    // Whether the client's own function counts from the same place is NOT confirmed. If it turns
    // out to be zero-based this is the one line that changes, which is why the conversion is here
    // rather than at the call sites or in the config.
    private static uint SoundEffectFor(int stored) =>
        (uint)Math.Clamp(stored, 1, 16);

#if DEBUG
    // For SoundSweep, which auditions effects without the config, the category toggles or the
    // interval in the way. Nothing in a Release build can reach the playback path except through
    // Play and PlayNow above.
    internal static void PlayDirect(uint effectId) => PlayGameSound(effectId);
#endif

    private static bool warned;

    // UIGlobals.PlayChatSoundEffect over UIGlobals.PlaySoundEffect, which wants two SoundData**
    // out-parameters and a byte nobody has named, and over SoundManager.PlaySound, which takes
    // seventeen arguments of which four are unnamed. This one is a plain static taking a number.
    //
    // Verified against a running client, not inferred: the game mixes this itself, so the player's
    // own audio settings apply without anything here reading them, and the master volume silences
    // it. The bus is System Sounds rather than Sound Effects, which is the game's own routing for
    // a chat sound effect and nothing this gets to choose. The settings screen names that slider
    // for exactly that reason: reaching for Sound Effects to quieten this would not work, and it
    // is a confusing thing to discover on your own.
    //
    // A failure is logged once and then swallowed quietly, but never latched off. Latching cost
    // nothing at a notification and everything at a sweep: one bad effect id silenced the rest of
    // the session, including the debug command whose whole job is to find out which ids are bad.
    // Retrying is cheap here because the interval already bounds this to four calls a second.
    private static void PlayGameSound(uint effectId)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect(effectId);
        }
        catch (Exception ex)
        {
            if (warned)
                return;

            warned = true;
            Log.Error(ex, $"Could not play sound effect {effectId}. Further failures are not logged.");
        }
    }
}
