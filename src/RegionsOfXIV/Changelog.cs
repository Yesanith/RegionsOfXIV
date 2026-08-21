using System;
using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

internal readonly record struct ChangelogEntry(Version Version, string[] Changes);

internal static class Changelog
{
    public static readonly ChangelogEntry[] All =
    [
        new(new Version("0.4.0.0"),
        [
            "Use your own fonts. The name, the header and the weather line each pick their own face and size now, and any of them can point at a .ttf, .otf or .ttc sitting on your PC.",
            "A font you supply stays yours to look after. The plugin loads it exactly as it is, says plainly when it cannot, and falls back to Noto Sans CJK rather than to nothing.",
            "Presets carry where a custom font file sits rather than the font itself. Sharing one warns you first, and importing one that names a font you do not have tells you which lines fell back.",
            "A drop shadow, thrown in any direction and spread as far as you like. It sits under the outline so the two can be used together, and the underline and weather icon cast it too.",
            "The weather line has its own font and size instead of borrowing the header's. Settings you already had keep the size they were showing.",
            "The header's size can be adjusted at last. It had a setting but never a slider.",
            "Font and size have moved off General onto their own Fonts tab, a page for each of the three lines.",
            "Less work every frame: banners are no longer watched for while the feature is off, a line is measured once instead of on every frame it is up, and an outline you cannot see is no longer drawn eight times over.",
        ]),

        new(new Version("0.3.0.0"),
        [
            "The game's own banners are yours now. Quest Accepted, Duty Commenced, Level Up! and the rest are redrawn in this plugin's lettering, with the same effects as a place name. Off by default; switch it on under Notifications.",
            "Only banners the plugin has words for are taken over. The wording is painted into the game's artwork rather than stored as text, so anything it cannot name keeps the game's own banner instead of losing it.",
            "Letters sit properly after a Q. The game's fonts carry per-pair spacing and the plugin was dropping one pair per letter, which left every line slightly loose and slightly off centre. Plain in Jupiter, subtle everywhere else.",
            "Colour each line separately. The weather line, the header's outline and the weather's outline can each take their own colour, so one line can be faded back without touching the others.",
            "The header switch has moved to General, next to the rest of the header settings, where it can actually be found.",
            "Settings are written when you let go of a slider rather than on every frame you drag it, so the colour pickers no longer feel like they have stuck.",
            "A colour turned transparent now says so, instead of looking like a setting that has stopped working.",
            "Preset codes survive being pasted around. Line wrapping, chat formatting and the invisible characters a web page leaves behind are stripped before the code is read.",
        ]),

        new(new Version("0.2.3.0"),
        [
            "Weather announcements. When the weather turns over it is announced on its own line above the place name, with the game's own icon beside it. Off by default; switch it on under Notifications.",
            "The weather line is styled like everything else, sharing the underline, the motion and decode effects, and your colours and timings.",
            "Arriving somewhere new announces its weather alongside the place name, and the weather turning over while you stand there announces on its own.",
            "The preview shows everything that is switched on, weather included, so what you are adjusting is what you can see.",
        ]),

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
