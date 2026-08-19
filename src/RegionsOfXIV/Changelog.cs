using System;
using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

internal readonly record struct ChangelogEntry(Version Version, string[] Changes);

// What changed, per release, for the window that shows up once after an update.
//
// Held in code rather than read from a file: it is a handful of lines that change
// when the version does, and shipping it as an asset would mean a file that can go
// missing, arrive stale, or need parsing — three failure modes for something with
// no moving parts.
//
// Newest first, which is both the order it reads in and the order Since depends
// on. Write for somebody who has been away for one release: what is different now,
// not what the commits were.
internal static class Changelog
{
    public static readonly ChangelogEntry[] All =
    [
        new(new Version("0.2.2.0"),
        [
            "Save your own presets. A preset now covers every setting on General, Effects, Notifications and Durations, not just the look.",
            "Share presets as codes. Copy one to the clipboard, paste it into chat, and anyone can paste it back in here.",
            "Editing mode keeps a single sample notification on screen while you work, instead of starting a new one every time you change something.",
            "The built-in looks are complete configurations now: applying one returns everything it does not name to its default.",
            "A Discord link, on the title bar and on the Presets page.",
        ]),

        new(new Version("0.2.1.0"),
        [
            "The plugin ships its own icon, so it looks like itself in the installer.",
        ]),

        new(new Version("0.2.0.0"),
        [
            "Motion effects: type the line out, rise it into place, ride it in on a wave, or set it alight.",
            "Ambient particles: hearts, embers, sparkles and petals, drawn around the text while it is up.",
            "Built-in looks that pair a motion with particles and a palette.",
            "Letter spacing, uppercasing, horizontal placement and outline weight.",
            "The motion and the decode run one after the other rather than over each other, so you can see both.",
        ]),

        new(new Version("0.1.1.0"),
        [
            "A notification no longer freezes during a cutscene and resume stale afterwards.",
            "Arriving somewhere during a cutscene is no longer lost entirely.",
        ]),

        new(new Version("0.1.0.0"),
        [
            "First release. Announces the region, zone, area and sub-area you walk into, replacing the game's own location text rather than drawing alongside it.",
        ]),
    ];

    // What this build is, from the assembly rather than from a constant that would
    // need remembering twice. The csproj's <Version> is what sets it.
    public static Version Current =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    // Everything newer than the version the player last had.
    //
    // A null lastSeen means the stored config predates this window existing, so
    // there is no honest answer to "what have you missed" — it could be one release
    // or all of them. The newest entry alone is the useful half of the guess: it
    // describes the update that just happened, which is what they are here for.
    public static ChangelogEntry[] Since(Version? lastSeen)
    {
        if (All.Length == 0)
            return [];

        if (lastSeen == null)
            return [All[0]];

        return All.Where(entry => entry.Version > lastSeen).ToArray();
    }

    // Tolerant on purpose. The value comes out of a config file that a person can
    // edit, and a version that will not parse should show the changelog rather than
    // throw on the way in.
    public static Version? Parse(string? stored) =>
        Version.TryParse(stored, out var version) ? version : null;
}
