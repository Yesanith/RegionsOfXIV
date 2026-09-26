using System;
using System.Linq;
using System.Reflection;
using RegionsOfXIV.Services;

namespace RegionsOfXIV;

internal readonly record struct ChangelogEntry(Version Version, string[] Changes);

// Kept newest first, and the top entry's version must match the assembly version in the csproj
// -- Since compares against it to decide what a returning player has not seen yet, so an entry
// added without bumping the build shows up to people who are not running it.
//
// Every line goes through Loc so the What's new page reads in the window's language. Loc resolves
// at call time, so the array is built again whenever the language changes rather than once at
// start-up, and each key carries its version so a line can never be matched to another release.
internal static class Changelog
{
    private static ChangelogEntry[]? Entries;
    private static int EntriesGeneration = -1;

    public static ChangelogEntry[] All
    {
        get
        {
            // Read before the build rather than after it. Loc.Get resolves at call time, so a
            // language change part way through Build leaves an array holding some lines in each
            // language, and stamping that with the generation read afterwards would mark the
            // mixture as current and leave it on screen until the next language change.
            //
            // Apply runs on the framework thread as well as the draw thread, so that interleaving
            // is reachable even though everything that reads this is on the draw thread. Stamping
            // with the generation the build started from means a swap during it simply loses, and
            // the next read rebuilds.
            var generation = Loc.Generation;

            if (Entries is null || EntriesGeneration != generation)
            {
                Entries = Build();
                EntriesGeneration = generation;
            }

            return Entries;
        }
    }

    private static ChangelogEntry[] Build() =>
    [
        new(new Version("1.0.0.0"),
        [
            Loc.Get("changelog.1.0.0.0.01", "The settings window is built around a live preview. The notification you are shaping is drawn at the top of the window at its real size, by the same code that draws it in game, and follows every change as you make it. Replay it, send it to the game screen, or pin it there while you work."),
            Loc.Get("changelog.1.0.0.0.02", "Eight tabs across the top: Announcements, Appearance, Fonts, Motion, Sound, Presets, What's new and About. Each page is two columns, and a setting that only matters while another is switched on appears when that one is switched on."),
            Loc.Get("changelog.1.0.0.0.03", "Drag a marker to place the notification on the screen. The two position sliders are still there and set exactly the same thing."),
            Loc.Get("changelog.1.0.0.0.04", "The language this window is in sits in the header, where it is found without a trip to About."),
            Loc.Get("changelog.1.0.0.0.05", "Areas and sub-areas can arrive smaller and leave sooner than a zone, so the frequent notices whisper while a new zone still announces itself. Full size is the old look."),
            Loc.Get("changelog.1.0.0.0.06", "Something dark behind the text, for a name landing on a bright sky: a band the width of the text, or a strip across the whole screen as the original does."),
            Loc.Get("changelog.1.0.0.0.07", "The place name can fade to a second colour from top to bottom, glow behind its outline, or be written in a run of colours: Rainbow, Sunset, Ocean, Aurora, Blossom and Golden, laid along the line, breathing all at once or running along it, at a speed of your choosing."),
            Loc.Get("changelog.1.0.0.0.08", "Five more ways to arrive: Drop, Slide, Assemble, Flicker and Zoom. And a choice of how a line leaves: the plain fade, the arrival run backwards, dissolving, or falling."),
            Loc.Get("changelog.1.0.0.0.09", "Five more kinds of particle: snow, fireflies, leaves, rain and stars, with a slider for how far around the name they play."),
            Loc.Get("changelog.1.0.0.0.10", "Quiet rules. Notices can be hidden in cities, in housing districts and inside houses, and in any zone you put on a quiet list by standing in it. Skipping sub-areas while travelling fast now also counts flying and diving."),
            Loc.Get("changelog.1.0.0.0.11", "Weather and banners can each have a game sound of their own rather than sharing the one for places, and a sound file of your own has a volume slider."),
            Loc.Get("changelog.1.0.0.0.12", "Hovering a preset shows it in the preview before you apply it."),
            Loc.Get("changelog.1.0.0.0.13", "The window is drawn in Noto Sans, with Dalamud's Noto Sans CJK merged in for everything Latin does not cover, so every shipped language reads in the same face at the same weight."),
            Loc.Get("changelog.1.0.0.0.14", "Spanish, Portuguese, Russian and Chinese join German, French, Japanese and Turkish, all machine-drafted and marked so at the top until a speaker has been through them. Every string in the window is translated in all eight, and the language pickers write each language's name with a capital letter."),
            Loc.Get("changelog.1.0.0.0.15", "Nothing you have saved is affected. Presets, share codes and your current settings carry over as they were, every new setting starts at the old look, and a preset from an older version still applies."),
            Loc.Get("changelog.1.0.0.0.16", "The About tab credits the plugin's author and the interface's author."),
        ]),

        new(new Version("0.6.1.0"),
        [
            Loc.Get("changelog.0.6.1.0.01", "Turkish banner wording draws properly. The dotted I, the S with a cedilla and the G with a breve were coming out as question marks, so \"ETKINLIK BASLADI\" was missing three of its letters."),
            Loc.Get("changelog.0.6.1.0.02", "The font the plugin draws with had no Turkish letters in it at all. It ships with a small addition that does, and the letters it fills in are from the same family as the ones around them, so a word does not change typeface halfway through. Polish, Czech, Romanian and Hungarian gain the same."),
            Loc.Get("changelog.0.6.1.0.03", "The sample notification in the settings window follows the banner language while you are looking at it. Picking a language used to leave the preview showing whichever one the plugin started in."),
        ]),

        new(new Version("0.6.0.0"),
        [
            Loc.Get("changelog.0.6.0.0.01", "Notifications can make a sound. Off until you turn it on, under a Sound tab of its own: one of the game's sixteen chat sound effects, or a .wav or .mp3 of your own."),
            Loc.Get("changelog.0.6.0.0.02", "It follows the game's own audio settings, including the master and System Sounds volumes and the mute checkboxes. System Sounds rather than Sound Effects is the one to reach for if you want it quieter, because that is the bus the game puts a chat sound effect on."),
            Loc.Get("changelog.0.6.0.0.03", "A file of your own is played by the plugin rather than by the game, so the plugin reads those same settings and follows them by hand. It stays quiet while the game is muted, and while the window is not in front unless you have told the game to keep playing then. Anything past five seconds is cut off."),
            Loc.Get("changelog.0.6.0.0.04", "Which kinds of notification make a sound is yours to pick: place names, weather and banners each have their own switch, and two arriving together still only make one sound."),
            Loc.Get("changelog.0.6.0.0.05", "Sound settings stay on your machine. They do not travel in a preset or a share code, because a noise arriving from a stranger is not something to find out about by accident."),
            Loc.Get("changelog.0.6.0.0.06", "The settings window is grouped by what you are doing rather than by when things were added. Seven tabs: Announcements, Appearance, Motion, Fonts, Sound, Presets and About."),
            Loc.Get("changelog.0.6.0.0.07", "Settings that belonged together are together. Motion and its duration sit on one page, as do the Eorzean decode and its duration, where each pair used to be split across two tabs. Colours are grouped by what they colour."),
            Loc.Get("changelog.0.6.0.0.08", "Nothing you have saved is affected. Presets, share codes and your current settings all carry over exactly as they were; only where a control is drawn has changed."),
            Loc.Get("changelog.0.6.0.0.09", "The language this window is in has moved to the About tab."),
            Loc.Get("changelog.0.6.0.0.10", "German, French, Japanese and Turkish are complete. Every string in the window is translated in all four, where before each was missing a handful. They are still machine-drafted and still say so at the top until a speaker has been through them."),
        ]),

        new(new Version("0.5.0.0"),
        [
            Loc.Get("changelog.0.5.0.0.01", "The settings window reads in your language. German, French, Japanese and Turkish ship with the plugin, and it follows the language Dalamud is set to unless you pick one yourself in the settings."),
            Loc.Get("changelog.0.5.0.0.02", "Those four were drafted by a machine. The window says so at the top until someone who speaks the language has been through it, and correcting one needs nothing but a text editor and a GitHub account. TRANSLATING.md explains how."),
            Loc.Get("changelog.0.5.0.0.03", "Banners can be drawn in a language you choose rather than the one your client runs in. Turkish wording ships. Any banner the plugin has no words for keeps the game's own artwork, whichever language you pick."),
            Loc.Get("changelog.0.5.0.0.04", "Light Party and Full Party are announced and hidden like every other banner. They arrive on a part of the interface the plugin was not watching, which is why those two were always missed."),
            Loc.Get("changelog.0.5.0.0.05", "A banner arriving right behind a place name is no longer dropped. Holding one back never bought quiet, it only left the game's own version on screen instead, so a banner now takes its own line below the place name. How far below is a slider."),
            Loc.Get("changelog.0.5.0.0.06", "The font sliders reach much further up, for anyone running at a high resolution who found the old top of the range still small. On a Japanese client they stop where the game can still build the letters, and your setting is kept as you left it."),
            Loc.Get("changelog.0.5.0.0.07", "Accented letters no longer vanish while a line decodes. The Eorzean alphabet has no glyph for them, so anything with an accent was drawn as blank space until the name resolved. They now stand in as the letter underneath: é as e, ğ as g."),
            Loc.Get("changelog.0.5.0.0.08", "The settings window can draw Turkish, Polish, Czech, Romanian and their neighbours, where before it had eight letters of Latin Extended-A and blanks for the rest."),
        ]),

        new(new Version("0.4.2.0"),
        [
            Loc.Get("changelog.0.4.2.0.01", "Presets and share codes made by older versions apply the way they were saved. One from before the header gap became a slider was landing on the default spacing rather than the spacing it was saved with, and one from before the weather line had its own size was leaving that size behind as well."),
            Loc.Get("changelog.0.4.2.0.02", "This covers both the presets you saved yourself and codes someone sends you. Saved ones are brought up to date the next time the plugin loads, so there is nothing to redo."),
        ]),

        new(new Version("0.4.1.0"),
        [
            Loc.Get("changelog.0.4.1.0.01", "Arriving somewhere new while the last notice is still up no longer leaves the two written over each other. The one on its way out now leaves quickly and moves clear of the one arriving, instead of fading at reading pace underneath it."),
            Loc.Get("changelog.0.4.1.0.02", "The new place still appears the moment you reach it. Nothing waits its turn."),
            Loc.Get("changelog.0.4.1.0.03", "Colours have their alpha back, in the picker where it used to be. It stops at 15% rather than running down to nothing, so a line can be faded behind the others without being faded until it looks like the plugin has stopped working."),
            Loc.Get("changelog.0.4.1.0.04", "A colour already stored fainter than that is brought back up to it when the plugin loads, which covers anything left invisible by the version that had no alpha at all."),
            Loc.Get("changelog.0.4.1.0.05", "The gap between the header and the place name is a slider, where it was a switch with two positions."),
            Loc.Get("changelog.0.4.1.0.06", "The sample in the settings window follows the header switch again. Turning \"Show header\" off left it showing one."),
        ]),

        new(new Version("0.4.0.0"),
        [
            Loc.Get("changelog.0.4.0.0.01", "Use your own fonts. The name, the header and the weather line each pick their own face and size now, and any of them can point at a .ttf, .otf or .ttc sitting on your PC."),
            Loc.Get("changelog.0.4.0.0.02", "A font you supply stays yours to look after. The plugin loads it exactly as it is, says plainly when it cannot, and falls back to Noto Sans CJK rather than to nothing."),
            Loc.Get("changelog.0.4.0.0.03", "Presets carry where a custom font file sits rather than the font itself. Sharing one warns you first, and importing one that names a font you do not have tells you which lines fell back."),
            Loc.Get("changelog.0.4.0.0.04", "A drop shadow, thrown in any direction and spread as far as you like. It sits under the outline so the two can be used together, and the underline and weather icon cast it too."),
            Loc.Get("changelog.0.4.0.0.05", "The weather line has its own font and size instead of borrowing the header's. Settings you already had keep the size they were showing."),
            Loc.Get("changelog.0.4.0.0.06", "The header's size can be adjusted at last. It had a setting but never a slider."),
            Loc.Get("changelog.0.4.0.0.07", "Font and size have moved off General onto their own Fonts tab, a page for each of the three lines."),
            Loc.Get("changelog.0.4.0.0.08", "Less work every frame: banners are no longer watched for while the feature is off, a line is measured once instead of on every frame it is up, and an outline you cannot see is no longer drawn eight times over."),
        ]),

        new(new Version("0.3.0.0"),
        [
            Loc.Get("changelog.0.3.0.0.01", "The game's own banners are yours now. Quest Accepted, Duty Commenced, Level Up! and the rest are redrawn in this plugin's lettering, with the same effects as a place name. Off by default; switch it on in the settings."),
            Loc.Get("changelog.0.3.0.0.02", "Only banners the plugin has words for are taken over. The wording is painted into the game's artwork rather than stored as text, so anything it cannot name keeps the game's own banner instead of losing it."),
            Loc.Get("changelog.0.3.0.0.03", "Letters sit properly after a Q. The game's fonts carry per-pair spacing and the plugin was dropping one pair per letter, which left every line slightly loose and slightly off centre. Plain in Jupiter, subtle everywhere else."),
            Loc.Get("changelog.0.3.0.0.04", "Colour each line separately. The weather line, the header's outline and the weather's outline can each take their own colour, so one line can be faded back without touching the others."),
            Loc.Get("changelog.0.3.0.0.05", "The header switch has moved to General, next to the rest of the header settings, where it can actually be found."),
            Loc.Get("changelog.0.3.0.0.06", "Settings are written when you let go of a slider rather than on every frame you drag it, so the colour pickers no longer feel like they have stuck."),
            Loc.Get("changelog.0.3.0.0.07", "A colour turned transparent now says so, instead of looking like a setting that has stopped working."),
            Loc.Get("changelog.0.3.0.0.08", "Preset codes survive being pasted around. Line wrapping, chat formatting and the invisible characters a web page leaves behind are stripped before the code is read."),
        ]),

        new(new Version("0.2.3.0"),
        [
            Loc.Get("changelog.0.2.3.0.01", "Weather announcements. When the weather turns over it is announced on its own line above the place name, with the game's own icon beside it. Off by default; switch it on in the settings."),
            Loc.Get("changelog.0.2.3.0.02", "The weather line is styled like everything else, sharing the underline, the motion and decode effects, and your colours and timings."),
            Loc.Get("changelog.0.2.3.0.03", "Arriving somewhere new announces its weather alongside the place name, and the weather turning over while you stand there announces on its own."),
            Loc.Get("changelog.0.2.3.0.04", "The preview shows everything that is switched on, weather included, so what you are adjusting is what you can see."),
        ]),

        new(new Version("0.2.2.0"),
        [
            Loc.Get("changelog.0.2.2.0.01", "Save your own presets. A preset now covers every setting on General, Effects, Notifications and Durations, not just the look."),
            Loc.Get("changelog.0.2.2.0.02", "Share presets as codes. Copy one to the clipboard, paste it into chat, and anyone can paste it back in here."),
            Loc.Get("changelog.0.2.2.0.03", "Editing mode keeps a single sample notification on screen while you work, instead of starting a new one every time you change something."),
            Loc.Get("changelog.0.2.2.0.04", "The built-in looks are complete configurations now: applying one returns everything it does not name to its default."),
            Loc.Get("changelog.0.2.2.0.05", "A Discord link, on the title bar and on the Presets page."),
        ]),

        new(new Version("0.2.1.0"),
        [
            Loc.Get("changelog.0.2.1.0.01", "The plugin ships its own icon, so it looks like itself in the installer."),
        ]),

        new(new Version("0.2.0.0"),
        [
            Loc.Get("changelog.0.2.0.0.01", "Motion effects: type the line out, rise it into place, ride it in on a wave, or set it alight."),
            Loc.Get("changelog.0.2.0.0.02", "Ambient particles: hearts, embers, sparkles and petals, drawn around the text while it is up."),
            Loc.Get("changelog.0.2.0.0.03", "Built-in looks that pair a motion with particles and a palette."),
            Loc.Get("changelog.0.2.0.0.04", "Letter spacing, uppercasing, horizontal placement and outline weight."),
            Loc.Get("changelog.0.2.0.0.05", "The motion and the decode run one after the other rather than over each other, so you can see both."),
        ]),

        new(new Version("0.1.1.0"),
        [
            Loc.Get("changelog.0.1.1.0.01", "A notification no longer freezes during a cutscene and resumes stale afterwards."),
            Loc.Get("changelog.0.1.1.0.02", "Arriving somewhere during a cutscene is no longer lost entirely."),
        ]),

        new(new Version("0.1.0.0"),
        [
            Loc.Get("changelog.0.1.0.0.01", "First release. Announces the region, zone, area and sub-area you walk into, replacing the game's own location text rather than drawing alongside it."),
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
