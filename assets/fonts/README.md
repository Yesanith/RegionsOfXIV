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

> **`Axis` is the only family with Japanese glyphs.** Anything rendering
> user-visible game text must either use it or fall back to it — a Latin-only face
> renders Japanese place names as tofu. `FontService.ResolveDisplayFamily` handles
> this: the `Auto` setting picks TrumpGothic for Latin clients and Axis for
> Japanese ones, and the header line uses Axis unconditionally.

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

**Consequence for this plugin:** the default 76 px display size is a downscale for
TrumpGothic (sharp) but a 1.58× upscale for Axis (soft). `FontService.NativeCeilingPx`
exposes these limits and the config window warns when the chosen size exceeds them.
The `Dalamud` font choice sidesteps the issue entirely — it is a vector face and
stays sharp at any size.

Underneath, these are the `common/font/*.fdt` files in the game data
(`AXIS_12/14/18/36/96`, `Jupiter_16/20/23/45/46/90`, `TrumpGothic_23/34/68/184`,
`Meidinger_16/20/40`, `MiedingerMid_10/12/14/18/36`).

## Licensing

Check the license of anything you put here before shipping. Fan-made Eorzean fonts
vary — some are freely redistributable, some are not. The plugin degrades
gracefully to a plain fade when no font is present, so shipping without one is a
valid option.
