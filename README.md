<div align="center">

<img src="assets/images/icon.png" width="140" alt="Regions of XIV icon">

# Regions of XIV

**Announces the region, zone, area and sub-area you walk into,
with a styled, animated on-screen notification.**

[![release](https://img.shields.io/github/v/release/Yesanith/RegionsOfXIV?color=blue)](https://github.com/Yesanith/RegionsOfXIV/releases/latest)
[![build](https://img.shields.io/github/actions/workflow/status/Yesanith/RegionsOfXIV/pr-build.yml?branch=master)](https://github.com/Yesanith/RegionsOfXIV/actions/workflows/pr-build.yml)
[![license](https://img.shields.io/badge/license-AGPL--3.0--or--later-blue)](LICENSE.md)

</div>

Final Fantasy XIV announces zone changes on screen, but changes your **sub-area**
silently — the only feedback is the small text above the minimap. Regions of XIV
surfaces those transitions, and lets you restyle zone announcements to taste.

Inspired by [Nekres' *Regions of Tyria*](https://blishhud.com/modules/?module=Nekres.Regions_Of_Tyria)
for Guild Wars 2.

## Features

- **Four tiers, not one.** Region, zone, area and sub-area, each announced as it
  changes — including the sub-area changes the game never announces at all.
- **Replaces rather than stacks.** Hides the game's own area flash and
  loading-screen title and draws in their place, so an arrival reads as one
  notice instead of two. Both are a checkbox away from coming back.
- **Weather, if you want it.** The sky above the place name, with the game's own
  icon beside it, as you arrive and again whenever it turns over. Worked out from
  the clock rather than read off the screen, so it is there the moment you land.
- **The game's banners, in your lettering.** Quest Accepted, Duty Commenced,
  Level Up! and the rest, redrawn with the same effects as a place name. Only the
  ones the plugin can name are taken over — the rest keep the game's own.
- **Decodes from the Eorzean alphabet** as it reveals, glyph by glyph — and the
  letters can rise, wave, type or catch alight while they resolve.
- **Hearts, embers, sparkles or petals** drifting around the text, if that is
  your sort of thing. Drawn from primitives, so they cost no download and work
  under every font.
- **Presets to start from.** Inferno, Sweetheart, Starlight, Sakura, Dispatch,
  Tyria — each one a motion, a particle and a palette that suit each other.
  Every setting stays yours to change afterwards.
- **Correct in every language.** Names come from game data rather than from the
  screen, and the default font carries glyphs for every language the client can
  display.
- **Your own fonts.** Name, header and weather each pick their own face and
  size: one of the game's own, Dalamud's Noto, or any `.ttf`, `.otf` or `.ttc`
  sitting on your PC. A font you supply loads exactly as it is and stays yours to
  look after — and a preset carries where the file sits rather than the font
  itself, so a shared one falls back to Noto on someone else's machine.
- **Styled to taste.** Place it anywhere on screen, with your own colours, letter
  spacing, casing, outline weight and a drop shadow you can throw in any
  direction. Name, header and weather can each take their own colour and outline,
  or share one.
- **Live preview.** Drag the position, size and colour sliders and a sample
  notification follows them as you go.
- **Knows when to stay quiet.** Silent through cutscenes, PvP and gpose; through
  combat and duties if you ask; and it skips sub-areas while you are flying, so
  crossing a zone at speed does not announce a string of places you passed over.

## Installing

> Not on Dalamud's official plugin installer yet — a
> [D17](https://github.com/goatcorp/DalamudPluginsD17) submission is open. Until
> it lands, the custom repository below is the way in, and it updates itself
> like any other plugin.

In game: `/xlsettings` → **Experimental** → paste into **Custom Plugin
Repositories**:

```
https://raw.githubusercontent.com/Yesanith/DalamudPlugins/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` →
**All Plugins**, search for **Regions of XIV**, and install.

Then `/regions` opens the settings.

<details>
<summary>Or install it by hand</summary>

1. Download `latest.zip` from the
   [latest release](https://github.com/Yesanith/RegionsOfXIV/releases/latest).
2. Unzip it somewhere permanent.
3. In Dalamud settings, add that folder to **Dev Plugin Locations**, then enable
   the plugin.

This does not auto-update. Building from source works the same way — see
[Building](#building).

</details>

## Usage

| Command | Effect |
| --- | --- |
| `/regions` | open the settings |
| `/regions test` | fire a sample notification, bypassing the suppression rules |
| `/regions changelog` | show what has changed, all versions |

The settings open by themselves the first time, because the defaults change what
the game itself draws.

## Building

Requires the **.NET 10 SDK (10.0.101 or later)** and a XIVLauncher install that has
run Dalamud at least once.

```sh
dotnet build RegionsOfXIV.sln -c Release
```

Two things come out of that, and they are easy to mix up:

| Path | What it is |
| --- | --- |
| `src/RegionsOfXIV/bin/x64/Release/` | the plugin itself — DLL, `Fonts/`, `images/`. Point Dalamud's **Dev Plugin Locations** here |
| `src/RegionsOfXIV/bin/x64/Release/RegionsOfXIV/` | the packaged layout — `latest.zip`, manifest and icon, for a repo listing or a D17 submission. No DLL, so dev-loading from here finds nothing |

The tests need neither the game nor Dalamud, so they run anywhere the SDK does:

```sh
dotnet test RegionsOfXIV.sln -c Release
```

### Developer tools

Three files are compiled only in Debug, which is what Rider and Visual Studio
build by default:

| File | What it does |
| --- | --- |
| `Services/SheetSearch.cs` | searches the game's Excel sheets from chat |
| `UI/IconBrowserWindow.cs` | a scrollable grid of the game's own icons, for finding an icon ID |
| `UI/BannerPreviewWindow.cs` | fires any banner on demand, and is where banner names get transcribed |

They are wired to four `/regions` subcommands that exist only in a Debug build:

| Command | Effect |
| --- | --- |
| `/regions preview` | open the banner preview |
| `/regions icons` | open the icon browser |
| `/regions banners` | list the banner rows found in the sheets |
| `/regions find <term>` | search the sheets for a term |

`RegionsOfXIV.csproj` removes all three files from compilation in every
configuration but Debug, so none of it reaches a release build — the types are
absent from the Release assembly entirely, not merely unreachable.

### How it fits together

`Services/AnnouncementCoordinator.cs` is the brain: everything that decides *what*
gets announced and *when* lives there, and it is where to start reading.

It never touches the game directly. Everything it listens to arrives through a
small interface — arrivals, movement, weather, banners, the game's own area text,
and the two name lookups — bundled as `AnnouncementSources`. `Plugin.cs` is the
only place that knows which real implementation goes with which, and the tests
hand it fakes instead. That is why the announcement rules can be exercised without
launching the game.

| Layer | What lives there |
| --- | --- |
| `Services/` | detection, decisions, game data. Never draws, never references `UI` |
| `UI/` | windows, the overlay, and the glyph painting |
| `Models/` | the few plain records both sides pass around |

The plugin draws its own text glyph by glyph rather than handing ImGui a string,
because the effects animate each letter separately. `UI/NotificationRenderer.cs`
decides where a line goes; `UI/NotificationRenderer.Runs.cs` paints it.

`Services/FontService.cs` keeps one face per `FontRole` — text, header, weather
— and rebuilds only the roles whose face, size or file actually changed. A role
set to a custom file also holds a Noto fallback, so a font that will not load
degrades to something readable rather than to nothing.

## Feedback

Issues and suggestions are welcome on the
[issue tracker](https://github.com/Yesanith/RegionsOfXIV/issues).

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).

The licence covers this project's own source. It does **not** extend to third-party
material the plugin uses or references — see [NOTICE](NOTICE).
