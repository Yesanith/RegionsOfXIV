# Regions of XIV

A Dalamud plugin that announces the region, zone, area and sub-area you walk into
with a styled, animated on-screen notification.

Final Fantasy XIV announces zone changes on screen, but changes your **sub-area**
silently — the only feedback is the small text above the minimap. Regions of XIV
surfaces those transitions, and lets you restyle zone announcements to taste.

Inspired by [Nekres' *Regions of Tyria*](https://github.com/agaertner/bhm-zone-display)
for Guild Wars 2.

> **Status: in development.** Detection, naming, the notification pipeline and the
> Eorzean reveal effect all work in game. Remaining: release packaging, and the
> long tail of edge cases in [ROADMAP.md](ROADMAP.md) Phase 9.

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

## Layout

```
src/RegionsOfXIV/
├─ Plugin.cs                  entrypoint, wiring, name resolution, announcements
├─ Configuration.cs           IPluginConfiguration
├─ Models/
│  └─ LocationSnapshot.cs     the five naming tiers, as row IDs
├─ Services/
│  ├─ LocationTracker.cs      polls TerritoryInfo; location + sanctuary changes
│  ├─ NotificationGate.cs     cooldown / ping-pong / game-state suppression
│  ├─ NativeUiSuppressor.cs   hides _AreaText and the loading-screen title
│  ├─ AddonNodeDump.cs        diagnostic node-tree logger ("/regions dump")
│  └─ FontService.cs          game + bundled font handles, size ceilings
└─ UI/
   ├─ NotificationOverlay.cs  full-screen draw surface, stroked text, decode effect
   ├─ AreaNotification.cs     one notification + its animation state
   └─ ConfigWindow.cs         settings

assets/
├─ fonts/    bundled fonts (Eorzean only — see the README there)
└─ images/   installer icon + preview shots (not compiled)
```

## Building

Requires the **.NET 10 SDK (10.0.101 or later)** and a XIVLauncher install that has
run Dalamud at least once.

```sh
dotnet build RegionsOfXIV.sln -c Release
```

Output lands in `src/RegionsOfXIV/bin/x64/Release/RegionsOfXIV/`, packaged and ready
to point Dalamud's **Dev Plugin Locations** at.

## Usage

| Command | Effect |
| --- | --- |
| `/regions` | open the settings |
| `/regions test` | fire a sample notification, bypassing the suppression rules |

## Documentation

- [ROADMAP.md](ROADMAP.md) — analysis of the GW2 original and the phased build plan
- [DALAMUD_PLUGIN_GUIDE.md](DALAMUD_PLUGIN_GUIDE.md) — full Dalamud API 15 reference

## License

GPL-3.0-or-later. See [LICENSE.md](LICENSE.md).

The licence covers this project's own source. It does **not** extend to third-party
material the plugin uses or references — see [NOTICE](NOTICE).
