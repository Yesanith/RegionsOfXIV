# Translating Regions of XIV

This document is for translators. You do not need C#, .NET, git, or a working build to help.
Everything here can be done in a web browser with a free GitHub account.

The plugin's settings window is translatable. Place names, weather names and the game's own banner
wording are **not**: those come from your own FFXIV client and are already in your language.

## Key files

| Path | Role |
| --- | --- |
| `src/RegionsOfXIV/Localization/en.json` | The English source with notes for translators. **Generated, so never edit it** |
| `src/RegionsOfXIV/Localization/de.json` | German. Edit this to improve German |
| `src/RegionsOfXIV/Localization/fr.json`, `ja.json` | French and Japanese |
| `src/RegionsOfXIV/Services/Localization.cs` | The loader. Developers only |
| `tools/export-en-json.py` | Regenerates `en.json`. Developers only |

A translation pull request should touch exactly one file.

## How this differs from most plugins

Two things are unusual here, and both make your job easier.

**English is not in a file.** It lives in the C# source, at every place a string is drawn:

```csharp
Loc.Get("durations.hold", "Hold")
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
  "durations.hold": {
    "message": "Halten",
    "description": "Slider label. How long the finished notification stays on screen."
  },
```

- **The key** (`durations.hold`) is machine-readable. Never translate, rename or invent one.
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
  Use exactly what your client shows.
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

## The two underscore keys

Keys starting with `_` are notes, not strings. The loader skips them.

```json
"_status": "machine-drafted -- not reviewed by a German speaker. …",
"_untranslated": [
  "general.hideduty: 'duty' is FFXIV's term for instanced content …"
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

## Which languages can ship

**English, German, French, Japanese and Russian only.**

The settings window draws with the game's own AXIS font, and that font's glyph coverage is fixed
when the plugin starts, and nothing can add to it at runtime. AXIS carries Latin-1, kana, about 6,300
kanji, and the complete Russian Cyrillic alphabet. It carries only eight characters of Latin
Extended-A.

So Polish, Czech, Turkish, Romanian and Vietnamese would draw as **blank spaces** where their
diacritics fall. Ukrainian and Serbian hit the same wall for their non-Russian Cyrillic letters.
Hebrew, Arabic, Thai, Korean and Chinese have no coverage at all.

The loader logs a warning naming the offending characters when a file uses them, so this fails
visibly rather than silently. Supporting those languages would mean giving the settings window its
own font, which is a real change rather than a file drop. If you want your language and it is on that list, open
an issue and say so; it is the argument for doing that work.

## Adding a new language

If your language is on the shippable list above and has no file yet:

1. Copy `en.json` to `xx.json`, where `xx` is the two-letter code (`ru.json` for Russian).
2. Replace each `message` with your translation. Delete the keys you are unsure of.
3. Add `_status` and `_untranslated` at the top.
4. Open a pull request.

No code change is needed. The plugin discovers languages from the files themselves.

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