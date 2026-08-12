<div align="center">

<img src="assets/images/icon.png" width="140" alt="Regions of XIV icon">

# Regions of XIV

**Announces the region, zone, area and sub-area you walk into,
with a styled, animated on-screen notification.**

[![release](https://img.shields.io/github/v/release/Yesanith/RegionsOfXIV?color=blue)](https://github.com/Yesanith/RegionsOfXIV/releases/latest)
[![build](https://img.shields.io/github/actions/workflow/status/Yesanith/RegionsOfXIV/pr-build.yml?branch=master)](https://github.com/Yesanith/RegionsOfXIV/actions/workflows/pr-build.yml)
[![license](https://img.shields.io/badge/license-AGPL--3.0--or--later-blue)](LICENSE.md)

<!--
  Live install count from Dalamud's own backend. Uncomment once the plugin is
  listed — until then the badge honestly reports "no result", because the name
  is not in that endpoint yet.

[![downloads](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fkamori.goats.dev%2FPlugin%2FDownloadCounts&query=%24.RegionsOfXIV&label=downloads&color=blue)](https://github.com/Yesanith/RegionsOfXIV)
-->

</div>

<!--
  A screenshot or a short gif of a notification belongs here — it is the one
  thing this page cannot say in words. Drop the file in assets/images/ and
  uncomment:

<div align="center">
  <img src="assets/images/preview.png" width="720" alt="A notification in game">
</div>
-->

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
- **Decodes from the Eorzean alphabet** as it reveals, glyph by glyph.
- **Correct in every language.** Names come from game data rather than from the
  screen, and the default font carries glyphs for every language the client can
  display.
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

The services are commented with the reasoning behind them — how the four
detection sources fit together, and why the notification gate is split the way
it is.

## Feedback

Issues and suggestions are welcome on the
[issue tracker](https://github.com/Yesanith/RegionsOfXIV/issues).

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).

The licence covers this project's own source. It does **not** extend to third-party
material the plugin uses or references — see [NOTICE](NOTICE).
