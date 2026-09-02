#if DEBUG
using System;

namespace RegionsOfXIV.Services;

// Auditioning the game's chat sound effects, for a Debug build only.
//
// It exists to answer two questions that cannot be answered without a running client: whether
// UIGlobals.PlayChatSoundEffect counts the sixteen effects from zero or from one, and whether the
// sound it makes goes through the game's own mixer. The second is the load-bearing one, because
// inheriting the player's volume and mute settings for free is the whole argument for using a
// game sound rather than playing a file.
//
// Deliberately not routed through INotificationSink. Going that way would put the notification
// path, the category toggles, SoundSource and the 250 ms interval between the command and the
// sound, so sweeping sixteen indices back to back would play one and drop fifteen, and anything
// misbehaving could be either half. This calls the playback path and nothing else.
internal static class SoundSweep
{
    // One either side of what SoundEffectFor assumes, which is 1 to 16. That is the whole point:
    // if the client counts from zero then "sound 0" is what plays <se.1>, and comparing the two
    // against /echo <se.1> settles it. Wider than this is not probing a boundary, it is guessing
    // at an audio function, so it is refused.
    private const int Lowest = 0;
    private const int Highest = 16;

    // Long enough to hear one effect end before the next begins. The chat effects are short, but
    // a couple of them ring.
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(1.5);

    private static int next;
    private static DateTime dueAt;
    private static bool sweeping;

    // Parses rather than taking an int, because the router hands over whatever followed the verb.
    public static void Play(string argument)
    {
        if (!int.TryParse(argument, out var index))
        {
            Log.Information($"/regions sound wants a number from {Lowest} to {Highest}, or no number to sweep.");
            return;
        }

        if (index < Lowest || index > Highest)
        {
            Log.Information($"Sound {index} is outside {Lowest} to {Highest} and was not played.");
            return;
        }

        Stop();

        Log.Information($"Playing sound effect {index}.");
        NotificationSounds.PlayDirect((uint)index);
    }

    // Every effect in turn, spaced by Gap. Driven off the framework tick and a due time rather
    // than a sleep or a thread: the playback call belongs on the framework thread, and blocking it
    // would freeze the client for the length of the sweep.
    public static void Sweep()
    {
        Stop();

        // From Lowest, not from one. Starting at one would assume the answer to the question the
        // sweep is being run to settle: if the client counts from zero then effect 0 is <se.1>,
        // and a sweep that skipped it would never play the first sound.
        next = Lowest;
        dueAt = DateTime.UtcNow;
        sweeping = true;

        Plugin.Framework.Update += OnFrameworkUpdate;
        Log.Information($"Sweeping sound effects {Lowest} to {Highest}, one every {Gap.TotalSeconds:F1}s.");
    }

    public static void Stop()
    {
        if (!sweeping)
            return;

        sweeping = false;
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private static void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        if (DateTime.UtcNow < dueAt)
            return;

        if (next > Highest)
        {
            Log.Information("Sweep finished.");
            Stop();
            return;
        }

        // Logged before it plays, so a sweep read back afterwards lines up with what was heard.
        Log.Information($"Playing sound effect {next}.");
        NotificationSounds.PlayDirect((uint)next);

        next++;
        dueAt = DateTime.UtcNow + Gap;
    }
}
#endif
