# Translating Regions of XIV

This document is for translators. You do not need C#, .NET, git, or a working build to help.
Everything here can be done in a web browser with a free GitHub account.

There are two separate jobs here, in two different files:

- **The settings window**, in the `Localization/*.json` files. Most of this document is about that.
- **Banner wording**, in `Services/BannerNames.cs`. See [Banner wording](#banner-wording).

Place names and weather names are **not** translatable: those come from your own FFXIV client and
are already in your language.

## Key files

| Path | Role |
| --- | --- |
| `src/RegionsOfXIV/Localization/en.json` | The English source with notes for translators. **Generated, so never edit it** |
| `src/RegionsOfXIV/Localization/de.json` | German. Edit this to improve German |
| `src/RegionsOfXIV/Localization/fr.json`, `ja.json` | French and Japanese |
| `src/RegionsOfXIV/Services/BannerNames.cs` | Banner wording. A different job with different rules |
| `src/RegionsOfXIV/Services/Localization.cs` | The loader. Developers only |
| `tools/export-en-json.py` | Regenerates `en.json`. Developers only |

A translation pull request should touch exactly one file. Settings-window strings and banner
wording are separate pull requests, because they are checked in completely different ways.

## How this differs from most plugins

Two things are unusual here, and both make your job easier.

**English is not in a file.** It lives in the C# source, at every place a string is drawn:

```csharp
Loc.Get("motion.hold", "Hold")
```

`en.json` is generated from those call sites so translators have something to read. It is never
loaded when the plugin runs. Editing it changes nothing, and your edit would be overwritten the
next time it is regenerated. If you spot an English typo, open an issue instead.

**A missing key falls back to English, per key.** You do not need every key. A file with thirty
entries gives you thirty translated strings and English everywhere else. Nothing breaks, nothing
shows a raw key, and a half-finished translation reads as partly English rather than as a broken
window.

That is deliberate: **leaving a key out is how you say "I am not sure about this one."** It is
always the right answer when you do not know a term.

## What a line looks like

```json
  "motion.hold": {
    "message": "Halten",
    "description": "Slider label. How long the finished notification stays on screen."
  },
```

- **The key** (`motion.hold`) is machine-readable. Never translate, rename or invent one.
- **`message`** is what a player sees. This is the only thing you change.
- **`description`** is a note from the developer to you. It is never shown in game. It explains
  what the string is, where it appears, and anything you need to know: line breaks to preserve,
  words that must not be translated, terms that have an established FFXIV rendering.

**Read the descriptions.** They exist because a key alone does not tell you whether a string is a
button, a tooltip, or a heading, and several of them warn about things that will otherwise bite you.

You may keep `description` in your file or drop it. The loader ignores it either way. Keeping it
helps the next person.

## The rules

**1. Translate `message`, never the key.** Everything left of the colon stays byte-for-byte
identical to `en.json`.

**2. Leave out anything you are unsure of.** Omission is not failure; it is the mechanism. A key
you skip shows English, which is better than a confident guess at a term the game already has a
word for.

**3. FFXIV's own wording wins.** Several strings name things the game itself already translates.
Use the game's word, not a fresh translation, or players will not recognise it:

- **Banner names**: Quest Accepted, Duty Commenced, Level Up! FFXIV shows these in every language.
  Use exactly what your client shows. Several tooltips name them as examples; the banners
  themselves are translated in a different file, described in [Banner wording](#banner-wording).
- **"Duty"**: FFXIV's term for instanced content. It has an established word in your language.
- **"Eorzean script"**: the game's invented alphabet has an official name per language.
- **Sanctuary, aetheryte, gpose, instance, sub-area**: same category.

If you cannot check one against the game, leave the key out.

**4. Keep every placeholder.** `{0}` and `{1}` are filled in at runtime with a name, a version, or
a number. Twenty strings carry them. They may move to wherever your language needs them, but every
placeholder in the English must appear in your translation, and you must not invent new ones.

A mistake here does not crash anything (the plugin falls back to the English pattern and logs a
warning), but the string reverts to English, so it is worth getting right.

**5. Keep the line breaks.** Thirty-two strings contain `\n`, mostly tooltips laid out in two or
three short lines. The `description` says so where it matters. Keep them, and keep blank lines
(`\n\n`) where they appear.

**6. Some things are never translated.** Typeface names (Trump Gothic, Jupiter, Axis, Noto Sans
CJK), the share-code prefix `ROX1-`, command names like `/regions test`, and preset names (Inferno,
Sakura, Tyria and the rest: those travel inside share codes, so they must be identical on every
machine). Where one appears, it is passed in as `{0}` and is already out of your reach.

**7. Units are just words.** `units.px`, `units.lines`, `units.seconds`, `units.times` are the short
labels after a number on a slider: "12 px", "0.35 s". Keep them very short; they sit inside a
narrow control. Do not add a leading space to the first three (the code adds one) and do not add one
to `units.times` (it sits against the number). A `%` in a unit is safe: it is escaped for you.

**8. Keep it short.** The settings window has a minimum width and tooltips wrap at a fixed measure.
German runs about 30% longer than English; where a label grows a lot, it can push controls off the
edge. Aim for the English length where you can.

**9. Speak to the player directly**, in plain second person, the way the English does.

**10. Be consistent.** The same term should get the same translation everywhere. The place tiers (region, zone, area, sub-area)
appear in several tabs and must match across all of them.

**11. Keep the file valid JSON.** UTF-8, two-space indent, a comma after every entry except the
last, quotes intact. If a value contains `\"` or `\\`, keep the backslash. A file that will not
parse leaves the whole language in English. It does not break the plugin, but nothing you wrote
takes effect.

**12. Write real characters, not escapes.** Some editors rewrite non-ASCII text into `\u` escapes.
That still parses, but it makes the file unreadable and the diff impossible to review. Save plain
UTF-8.

**13. No em dash.** The long dash (U+2014) is banned everywhere in this repository, in every
language, and the build fails if one appears. Use your own language's punctuation instead: a
comma, a colon, brackets, or two sentences. Do not swap in a hyphen; in most languages that reads
as a typo.

## The three underscore keys

Keys starting with `_` are notes, not strings. The loader skips them.

```json
"_status": "machine-drafted -- not reviewed by a German speaker. …",
"_untranslated": [
  "announcements.banners.tooltip: translated, but the three banner names inside it …"
],
"_uncertain": [
  "duty (announcements.hideduty). Chosen: 'Inhalte', which is what German FFXIV calls …"
]
```

**`_status`** is load-bearing. If it contains the text `machine-drafted`, the plugin shows a notice
at the top of the settings window telling players the translation is rough and pointing them at the
Discord.

**When a native speaker has reviewed a language, remove that phrase from `_status`.** The notice
disappears on its own. That is the signal that a file has stopped being a draft, and it is the main
thing a first real review changes.

**`_untranslated`** is a list of keys deliberately left English and why. Add to it when you skip a
key, remove entries as you resolve them.

**`_uncertain`** is the other half of that: keys you did translate, where the word you picked is a
judgement call somebody else might make differently. A term FFXIV has its own word for, a coinage
where the game offers none, a label that means nothing in your language but still needs one. Record
what you chose and what the alternative was. It costs a line and it saves the next person from
either second-guessing you or quietly changing half the occurrences.

## Banner wording

Banners are the game's full-screen announcements: "Quest Accepted", "Duty Commenced", "Level Up!".
Translating those is a **second, separate job**, and none of it happens in the JSON files.

The wording is painted into the artwork. It exists nowhere in the game's data as text, so the
plugin cannot look it up and carries its own list instead, keyed by the artwork's icon id:

```csharp
// src/RegionsOfXIV/Services/BannerNames.cs
[120021] = "Duty Commenced",
[120114] = "Light Party",
```

Players choose which language their banners are drawn in, under Announcements in the settings
window, and that dropdown offers exactly the languages with a table here.

This is a C# file rather than JSON, which sounds worse than it is: you are editing a list of
quoted strings and touching nothing else. Everything in [Contributing through
GitHub](#contributing-through-github) works the same way on it.

### Transcribe, do not translate

FFXIV already shows these banners in German, French and Japanese. **Read the words off your own
client and type them in exactly.** A fresh translation of "Duty Commenced" will not match what
players have been reading for years, and the point of replacing the artwork is that it says the
same thing in different lettering.

The exception is a language FFXIV does not have. Turkish is one: there is no Turkish client, so
nothing in that table was transcribed and every string in it is somebody's choice. If you are
adding a language the game does not ship in, say so in the pull request, because it changes what
reviewing it means.

### Which id is which

The number is not descriptive, and several banners share wording, so you cannot work it out by
reading. Two ways to find out:

**Work down the English table.** Every entry is a banner somebody has seen. Fill in the ones you
recognise from your own client and leave the rest, exactly as you would leave out a JSON key you
are unsure of. This needs no tools at all.

**Read the id off the log.** Open Dalamud's log window (`/xllog`) and filter on `RegionsOfXIV`.

A banner the plugin has no wording for announces itself there and asks to be reported, so playing
normally is enough to collect the missing ones. Banners it does recognise are logged too, with
their id and the wording used, but at debug level, so you will only see those if your log window
is set to show debug messages.

There is a third way, `/regions preview`, which fires any banner on demand and copies a
ready-made line to paste in. It is **only present in a development build**, so it is a maintainer's
tool rather than yours. Ask on the Discord if you want ids fired at you.

### What the code does for you

- **Capitals are applied automatically**, by the rules of the language the wording is written in
  rather than the player's. Write the names normally, in ordinary sentence case. Turkish is why
  this is worth spelling out: `i` upcases to `İ` and `ı` to `I`, and getting that from the wrong
  language produces exactly the wrong letter.
- **A missing id keeps the game's own banner.** An incomplete table degrades quietly, so add what
  you can confirm and stop there. Half a table is genuinely useful.
- **The decode effect handles accents by itself.** The Eorzean face draws ASCII and almost nothing
  else, so anything beyond it is folded onto the letter it is built from before the scramble is
  drawn: `ğ` becomes `g`, `é` becomes `e`. Only the scramble is affected. The wording that lands
  when the decode finishes is exactly what you wrote.

### What it does not do for you

Banner wording is drawn with the **notification** fonts, which are the game's own faces or Noto
Sans CJK, whichever the player has chosen. Those are not the settings window's font, and the
Latin Extended merge described in the next section does not reach them. A character a face does
not carry draws as nothing at all, with no warning and no log line, and different font choices
will not agree with each other.

So a banner table is worth looking at in game, under more than one font, before you call it done.

## Which languages can ship

**Anything written in Latin script, plus Japanese and Russian.**

This section is about the settings window. Banner wording is drawn with different fonts and has
its own caveat, above.

The settings window draws with the game's own AXIS font, which carries Latin-1, kana, about 6,300
kanji and the complete Russian Cyrillic alphabet, but only eight characters of Latin Extended-A.
On its own that ruled out most of Europe.

The window no longer draws with AXIS alone. It merges the Windows interface font in behind it for
Latin Extended-A, Latin Extended-B and Latin Extended Additional, so **Turkish, Polish, Czech,
Romanian, Vietnamese and their neighbours all draw properly**. Basic Latin still comes from AXIS,
which means a word can mix two typefaces: `Şık` takes its `Ş` and `ı` from the merged font and its
`k` from AXIS. Slightly uneven, and a great deal better than blank boxes.

What still cannot ship:

- **Ukrainian, Serbian, Bulgarian and other non-Russian Cyrillic.** The merge covers Latin only,
  so AXIS's Russian alphabet is still the whole of the Cyrillic coverage.
- **Hebrew, Arabic, Thai, Korean, and Chinese beyond the kanji AXIS happens to share.** No coverage
  at all, and each would need its own merge.

The loader logs a warning naming the offending characters when a file uses them, so this fails
visibly rather than silently. If you want one of the languages above, open an issue and say so; it
is the argument for widening the merge.

## Adding a new language

If your language can ship, per the section above, and has no file yet:

1. Copy `en.json` to `xx.json`, where `xx` is the two-letter code (`ru.json` for Russian).
2. Replace each `message` with your translation. Delete the keys you are unsure of.
3. Add `_status` and `_untranslated` at the top.
4. Open a pull request.

No code change is needed. The plugin discovers languages from the files themselves.

Banner wording is separate and optional. A settings-window translation with no banner table is a
complete contribution; the banners simply keep the game's own artwork. If you do want to add one,
it is a second pull request against `BannerNames.cs`, and a maintainer adds the one line that puts
your language in the dropdown.

## Contributing through GitHub

1. Create a free account on [github.com](https://github.com) and verify your email.
2. Open the file you want to edit in the repository.
3. Click the pencil icon. GitHub offers to fork the repository. Accept. A fork is your own copy;
   changing it does not affect the original.
4. Edit the values. Use Ctrl+F to find a key or a phrase.
5. Click **Commit changes**. Keep "Create a new branch and start a pull request" selected, and name
   the branch something like `de-settings-strings`.
6. Click **Propose changes**, then **Create pull request**. Say which language, roughly what you
   changed, and whether you are a native speaker. That last part decides whether the
   machine-drafted notice can come off.

For a full pass over a whole file, press `.` in the repository to open a browser-based VS Code
editor, or download the raw file, edit it locally in UTF-8, and upload it back over the same name.

Check the diff before you submit. If it shows hundreds of changed lines when you touched twenty,
your editor reformatted the file. It probably changed the indentation, the line endings, or
escaped the non-ASCII characters. Undo and retry with those settings off.

Do not paste the whole file through a machine translator. That is where the current drafts came
from, and replacing them with more of the same helps nobody. Machine output you then read and
rewrite line by line is fine.

## Finding a string you saw in game

1. Open `en.json` and search for the English text.
2. Note the key on that line.
3. Search your language's file for the same key. If it is missing, that is why you saw English;
   add it. If it is present but still English, someone left it deliberately; check `_untranslated`
   for the reason before changing it.

## Before you submit

- The file still parses as JSON. Any online validator will tell you.
- Every `{0}` and `{1}` in the English is still present in your version.
- Every `\n` the description told you to keep is still there.
- The diff contains only lines you meant to change.
- `_untranslated` matches what you actually left out.

## What happens next

The maintainer reviews the diff and asks about anything that looks off. Translation-only changes
touch no code, so review is usually quick. The change ships with the next plugin release.

If GitHub is awkward for you to reach, bring the edited file or a plain list of `key: new value`
lines to the [project Discord](https://discord.gg/) and someone will open the pull request with
credit to you.

By opening a pull request you agree your contribution is licensed under AGPL-3.0-or-later, the same
as the rest of the project.

## Gotchas

- **Editing `en.json` does nothing.** It is generated from the C# source and never read at runtime.
  Report English typos as an issue.
- **A missing key shows English, silently.** Nothing warns players, so untranslated text is easy to
  miss and worth reporting when you spot it.
- **A malformed file leaves the whole language in English.** The plugin keeps working; your file
  just never loads.
- **Removing `machine-drafted` from `_status` is what hides the notice.** Nothing else does.
- **Characters AXIS lacks draw as blanks, not as boxes.** They vanish. The log warns; the window
  does not.
- **Preset names are identifiers, not words.** They travel in share codes and must match across
  machines.
- **Banner wording is not in the JSON at all.** Searching a locale file for "Duty Commenced" finds
  a tooltip that mentions it, not the banner itself.
- **A banner table can be half empty.** Ids you leave out keep the game's own artwork, so there is
  no need to guess at one you have not seen.
- **Banner characters are not covered by the glyph warning.** The log names characters the settings
  window cannot draw. It says nothing about the notification fonts, which are what banners use.

## For developers

Adding a new user-facing string means adding a `Loc.Get(key, "English")` call and regenerating
`en.json`:

```sh
python tools/export-en-json.py            # rewrite en.json from the call sites
python tools/export-en-json.py --check    # report drift, write nothing
```

The generator preserves existing `description` fields, so notes written for translators survive
regeneration. Translations are not touched, and a new key simply falls back to English in every
language until someone adds it.

Widget labels go through `Loc.Label`, which appends `###key` so a control's ImGui identity comes
from the key rather than the translated text. Slider units go through `Loc.Unit`, which escapes
`%`. Neither is visible to translators.

Banner wording is not part of that system. A new language there is a dictionary in
`BannerNames.cs` plus one line in `ByLanguage`, which is what the Notifications dropdown is built
from, and nothing else. The tables are kept separate rather than folded into one id-to-languages
map so that adding a language does not touch the entries the others already have.