using System;
using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

internal readonly record struct ChangelogEntry(Version Version, string[] Changes);

// Kept newest first, and the top entry's version must match the assembly version in the csproj
// -- Since compares against it to decide what a returning player has not seen yet, so an entry
// added without bumping the build shows up to people who are not running it.
internal static class Changelog
{
    public static readonly ChangelogEntry[] All =
    [
        new(new Version("0.6.0.0"),
        [
            "Notifications can make a sound. Off until you turn it on, under a Sound tab of its own: one of the game's sixteen chat sound effects, or a .wav or .mp3 of your own.",
            "It follows the game's own audio settings, including the master and System Sounds volumes and the mute checkboxes. System Sounds rather than Sound Effects is the one to reach for if you want it quieter, because that is the bus the game puts a chat sound effect on.",
            "A file of your own is played by the plugin rather than by the game, so the plugin reads those same settings and follows them by hand. It stays quiet while the game is muted, and while the window is not in front unless you have told the game to keep playing then. Anything past five seconds is cut off.",
            "Which kinds of notification make a sound is yours to pick: place names, weather and banners each have their own switch, and two arriving together still only make one sound.",
            "Sound settings stay on your machine. They do not travel in a preset or a share code, because a noise arriving from a stranger is not something to find out about by accident.",
            "The settings window is grouped by what you are doing rather than by when things were added. Seven tabs: Announcements, Appearance, Motion, Fonts, Sound, Presets and About.",
            "Settings that belonged together are together. Motion and its duration sit on one page, as do the Eorzean decode and its duration, where each pair used to be split across two tabs. Colours are grouped by what they colour.",
            "Nothing you have saved is affected. Presets, share codes and your current settings all carry over exactly as they were; only where a control is drawn has changed.",
            "The language this window is in has moved to the About tab.",
            "German, French, Japanese and Turkish are complete. Every string in the window is translated in all four, where before each was missing a handful. They are still machine-drafted and still say so at the top until a speaker has been through them.",
        ]),

        new(new Version("0.5.0.0"),
        [
            "The settings window reads in your language. German, French, Japanese and Turkish ship with the plugin, and it follows the language Dalamud is set to unless you pick one yourself under General.",
            "Those four were drafted by a machine. The window says so at the top until someone who speaks the language has been through it, and correcting one needs nothing but a text editor and a GitHub account. TRANSLATING.md explains how.",
            "Banners can be drawn in a language you choose rather than the one your client runs in. Turkish wording ships. Any banner the plugin has no words for keeps the game's own artwork, whichever language you pick.",
            "Light Party and Full Party are announced and hidden like every other banner. They arrive on a part of the interface the plugin was not watching, which is why those two were always missed.",
            "A banner arriving right behind a place name is no longer dropped. Holding one back never bought quiet, it only left the game's own version on screen instead, so a banner now takes its own line below the place name. How far below is a slider.",
            "The font sliders reach much further up, for anyone running at a high resolution who found the old top of the range still small. On a Japanese client they stop where the game can still build the letters, and your setting is kept as you left it.",
            "Accented letters no longer vanish while a line decodes. The Eorzean alphabet has no glyph for them, so anything with an accent was drawn as blank space until the name resolved. They now stand in as the letter underneath: é as e, ğ as g.",
            "The settings window can draw Turkish, Polish, Czech, Romanian and their neighbours, where before it had eight letters of Latin Extended-A and blanks for the rest.",
        ]),

        new(new Version("0.4.2.0"),
        [
            "Presets and share codes made by older versions apply the way they were saved. One from before the header gap became a slider was landing on the default spacing rather than the spacing it was saved with, and one from before the weather line had its own size was leaving that size behind as well.",
            "This covers both the presets you saved yourself and codes someone sends you. Saved ones are brought up to date the next time the plugin loads, so there is nothing to redo.",
        ]),

        new(new Version("0.4.1.0"),
        [
            "Arriving somewhere new while the last notice is still up no longer leaves the two written over each other. The one on its way out now leaves quickly and moves clear of the one arriving, instead of fading at reading pace underneath it.",
            "The new place still appears the moment you reach it. Nothing waits its turn.",
            "Colours have their alpha back, in the picker where it used to be. It stops at 15% rather than running down to nothing, so a line can be faded behind the others without being faded until it looks like the plugin has stopped working.",
            "A colour already stored fainter than that is brought back up to it when the plugin loads, which covers anything left invisible by the version that had no alpha at all.",
            "The gap between the header and the place name is a slider, where it was a switch with two positions.",
            "The sample in the settings window follows the header switch again. Turning \"Show header\" off left it showing one.",
        ]),

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
