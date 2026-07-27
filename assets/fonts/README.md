# Bundled fonts

Drop `.ttf` / `.otf` files here. The `.csproj` links everything in this folder to
`Fonts/` next to the built plugin DLL, and `FontService` picks it up at runtime
(preferring a filename containing "eorzea").

## Currently bundled

**`Eorzea.ttf`** — the Eorzean alphabet, v1.00 (2010). Drives the decode reveal
effect, the FFXIV analogue of Regions of Tyria's New Krytan → Latin transition.

Coverage: 240 codepoints. Latin `A–Z`, `a–z`, digits, ASCII punctuation, accented
Latin, and the `fi`/`fl` ligatures. **No CJK** — `NotificationOverlay` detects this
and falls back to a plain fade rather than rendering tofu on a JP/CN/KR client.

The mapping is 1:1 against Latin codepoints, so the effect needs no transliteration
layer: the same string simply renders as Eorzean script in one font and plain text
in the other.

## What belongs here

**Only fonts we ship ourselves** — realistically just the Eorzean alphabet.

## What does NOT belong here

**The game's own UI fonts.** Dalamud builds ImGui fonts directly from the game's
files at runtime, so bundling them would be redundant — and they are commercial
typefaces, so redistributing them would be a licensing problem.

Request them by family instead:

```csharp
atlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.TrumpGothic, sizePx));
```

| `GameFontFamily` | Role in the game | Coverage |
| --- | --- | --- |
| **`Axis`** | **The general UI font** — chat, menus, tooltips, body text | Latin **+ Japanese** |
| `Jupiter` | Serif display face, used for job names | Latin only |
| `TrumpGothic` | Narrow display face for addon/window titles; ships up to 184 px | Latin only |
| `Meidinger` | Wide digits — HP/MP/item level | Digits |
| `MiedingerMid` | Wide sans for gauge names | Latin only |
| `JupiterNumeric` | Digits for flying text | Digits |

> **`Axis` is the only game family with Japanese glyphs.** TrumpGothic and Jupiter
> render Japanese place names as blank boxes — not soft, absent. `ConfigWindow`
> warns about this in red when either is selected on a Japanese client, which is a
> louder treatment than the amber upscaling warning precisely because it is a
> different kind of failure.
>
> `FontService.IsLatinOnly` is the single source of that fact; it lives beside the
> fonts rather than in the UI, because it describes the font and not the window.
>
> The default display font sidesteps the question entirely — see below. The header
> line is always Axis; at that size the narrow display faces buy nothing.

### These are bitmap fonts, and that constrains size

The `.fdt` files are **bitmap atlases baked at fixed sizes**, not vector outlines.
Request a size above a family's largest atlas and Dalamud upscales the bitmap,
which visibly softens the glyphs. Largest native size per family:

| Family | Largest `.fdt` | Ceiling in px |
| --- | --- | --- |
| Axis | `AXIS_36` (36 pt) | **48 px** |
| Jupiter | `Jupiter_46` (46 pt) | ~61 px |
| TrumpGothic | `TrumpGothic_68` (68 pt) | ~91 px |
| MiedingerMid | `MiedingerMid_36` | ~48 px |
| Meidinger | `Meidinger_40` | ~53 px |
| JupiterNumeric | `Jupiter_90` | ~120 px (digits only) |

`GameFontStyle` converts px to pt as `px * 3/4`, so the pixel ceiling is
`nativePt * 4/3`. Note that the largest-numbered file is not always the largest
size — `AXIS_96` is 9.6 pt and `TrumpGothic_184` is 18.4 pt, both high-resolution
atlases for *small* text.

**Consequence for this plugin:** `FontService.NativeCeilingPx` holds one ceiling
per choice, and `ConfigWindow` warns in amber when the chosen size passes it. The
61 px default clears TrumpGothic's, sits exactly on Jupiter's, and is over Axis's.

### The default is Noto Sans CJK, which has no ceiling at all

Dalamud ships `DalamudAsset.NotoSansCjkMedium` (confirmed present in
Dalamud.NET.Sdk 15.0.0 — the name is unchanged from earlier versions). It is
**vector**, so it is crisp at any size, and it covers every language rather than
just the client's. `NativeCeilingPx` reports `float.PositiveInfinity` for it, which
is what makes the amber warning go quiet without the UI special-casing anything.

The three game faces remain selectable for anyone who wants the FFXIV look and is
willing to keep an eye on the size.

#### Bounding the glyph range is not optional

`SafeFontConfig.GlyphRanges` left null means *"all the glyphs from the font that is
in the range of UCS-2"*. Noto Sans CJK holds tens of thousands — rasterised at
display size, and rebuilt **every time the size slider moves**. That is a very
large texture and a visible stutter, landing on new users by default and on the one
interaction where it is most noticeable.

`FontService.JapaneseGlyphRanges()` bounds it using ImGui's own
`GetGlyphRangesJapanese()`: Latin, kana, the ~3000 common-use kanji and the
fullwidth forms, well short of the whole of CJK Unified Ideographs. Reusing that
table beats maintaining a Unicode range list by hand. ImGui returns a `ushort*`
into static memory while `SafeFontConfig` wants a `ushort[]`, so the helper copies
it once and caches the result.

> **Do not "simplify" that to `GlyphRanges = null`.** It will look like it works,
> because the font is correct — the cost is in atlas build time and texture size,
> which is invisible until someone drags the size slider.

Untested at time of writing: whether ImGui's common-use kanji set covers every
kanji appearing in FFXIV place names. If one renders blank on a JP client, the fix
is an additional range, not removing the bound.

Underneath, these are the `common/font/*.fdt` files in the game data
(`AXIS_12/14/18/36/96`, `Jupiter_16/20/23/45/46/90`, `TrumpGothic_23/34/68/184`,
`Meidinger_16/20/40`, `MiedingerMid_10/12/14/18/36`).

## Licensing

Check the license of anything you put here before shipping. Fan-made Eorzean fonts
vary — some are freely redistributable, some are not. The plugin degrades
gracefully to a plain fade when no font is present, so shipping without one is a
valid option.
