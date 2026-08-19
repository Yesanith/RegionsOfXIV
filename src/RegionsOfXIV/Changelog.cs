using System;
using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

internal readonly record struct ChangelogEntry(Version Version, string[] Changes);

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

    public static Version Current =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    public static ChangelogEntry[] Since(Version? lastSeen)
    {
        if (All.Length == 0)
            return [];

        if (lastSeen == null)
            return [All[0]];

        return All.Where(entry => entry.Version > lastSeen).ToArray();
    }

    public static Version? Parse(string? stored) =>
        Version.TryParse(stored, out var version) ? version : null;
}
