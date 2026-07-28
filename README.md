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

> **0.1.0.0** — confirmed working in game, but not yet on Dalamud's plugin
> installer. A [D17](https://github.com/goatcorp/DalamudPluginsD17) submission is
> the next step; until it lands, install it yourself:

1. Download `latest.zip` from the
   [latest release](https://github.com/Yesanith/RegionsOfXIV/releases/latest).
2. Unzip it somewhere permanent.
3. In Dalamud settings, add that folder to **Dev Plugin Locations**, then enable
   the plugin.

Or build it from source — see [Building](#building).

## Usage

| Command | Effect |
| --- | --- |
| `/regions` | open the settings |
| `/regions test` | fire a sample notification, bypassing the suppression rules |

The settings open by themselves the first time, because the defaults change what
the game itself draws.

## How it decides what to show

FFXIV already puts location names on screen, so this plugin hides the game's own
and draws in their place. Four sources feed one overlay, each covering a moment the
others cannot:

| Source | Covers |
| --- | --- |
| `IClientState.ZoneInit` | the zone being **entered**, while the loading screen is still up |
| `_AreaText` addon | the game deciding an area is worth announcing, in world |
| `TerritoryInfo.InSanctuary` | settlements and inns, which the place-name IDs do not reliably name |
| 200 ms poll | sub-area changes the game never announces — the reason this plugin exists |

Names come from game data (the `TerritoryType` sheet and `TerritoryInfo`), so they
are correct in every client language. Text is read out of `_AreaText` only as a
tie-breaker, for the places where that data comes up empty.

They also *render* in every client language. The display font defaults to Noto Sans
CJK, which Dalamud already ships — vector, so it is crisp at any size, and it
carries glyphs for every language rather than just the client's. The game's own
Trump Gothic, Jupiter and Axis are selectable alongside it for a more FFXIV look;
each is a fixed-size bitmap and warns when the chosen size outgrows it, and the two
Latin-only faces warn in red on a Japanese client, where they would render place
names as blank boxes.

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

## Layout

```
src/RegionsOfXIV/
├─ Plugin.cs                       composition root: services, lifecycle, command
├─ Configuration.cs                IPluginConfiguration
├─ Models/
│  ├─ LocationSnapshot.cs          the five naming tiers, as row IDs
│  └─ ResolvedLocation.cs          the same five, as display strings
├─ Services/
│  ├─ AnnouncementCoordinator.cs   decides what is announced, and when
│  ├─ LocationTracker.cs           polls TerritoryInfo; location, sanctuary, speed
│  ├─ NotificationGate.cs          cooldown / ping-pong / game-state suppression
│  ├─ IGateSettings.cs             the settings the gate reads, and only those
│  ├─ IGameState.cs                the game-state questions the gate asks
│  ├─ NativeUiSuppressor.cs        hides _AreaText and the loading-screen title
│  ├─ PlaceNameResolver.cs         row IDs -> display strings, via Lumina
│  ├─ INotificationSink.cs         where a decided announcement goes
│  └─ FontService.cs               font handles and their size ceilings
└─ UI/
   ├─ NotificationOverlay.cs       full-screen draw surface, stroked text, decode
   ├─ AreaNotification.cs          one notification + its animation state
   └─ ConfigWindow.cs              settings

tests/RegionsOfXIV.Tests/
├─ NotificationGateTests.cs        the suppression matrix
├─ LocationSnapshotTests.cs        which tier a change is reported at
└─ Fakes.cs                        settings, game state and a clock

assets/
├─ fonts/    Eorzea.ttf, for the decode effect — see NOTICE
└─ images/   icon.png, copied to images/ beside the DLL for Dalamud's installer
```

`Plugin` builds the services and owns the Dalamud lifecycle; it decides nothing.
`AnnouncementCoordinator` holds the announcement rules and reaches the screen only
through `INotificationSink`, so those rules can be exercised without an ImGui
context.

`NotificationGate` goes further and touches no Dalamud type at all: it reads the
game through `IGameState`, the settings through `IGateSettings`, and the clock
through an injected delegate. That is what lets the suppression matrix be tested
without a game running — cooldowns are stepped over rather than waited out, and a
cutscene is one line rather than a trip to one.

## Feedback

Issues and suggestions are welcome on the
[issue tracker](https://github.com/Yesanith/RegionsOfXIV/issues).

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).

The licence covers this project's own source. It does **not** extend to third-party
material the plugin uses or references — see [NOTICE](NOTICE).
