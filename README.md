# Regions of XIV

A Dalamud plugin that announces the region, zone, area and sub-area you walk into
with a styled, animated on-screen notification.

Final Fantasy XIV announces zone changes on screen, but changes your **sub-area**
silently — the only feedback is the small text above the minimap. Regions of XIV
surfaces those transitions, and lets you restyle zone announcements to taste.

Inspired by [Nekres' *Regions of Tyria*](https://github.com/agaertner/bhm-zone-display)
for Guild Wars 2.

> **Status: in development.** Detection, name resolution and the notification
> pipeline work. The reveal effect and the minimap label are still stubs.
> See [ROADMAP.md](ROADMAP.md).

## Layout

```
src/RegionsOfXIV/
├─ Plugin.cs                entrypoint, service wiring, lifecycle
├─ Configuration.cs         IPluginConfiguration
├─ Models/
│  └─ LocationSnapshot.cs   the five naming tiers, as row IDs
├─ Services/
│  ├─ LocationTracker.cs    polls TerritoryInfo, raises LocationChanged
│  ├─ PlaceNameResolver.cs  row IDs -> display strings via Lumina
│  ├─ NotificationGate.cs   cooldown / ping-pong / game-state suppression
│  ├─ FontService.cs        game + bundled font handles
│  ├─ SoundService.cs       game UI sound effects
│  └─ NaviMapAnchor.cs      tracks the minimap's screen rect
└─ UI/
   ├─ NotificationOverlay.cs  full-screen draw surface
   ├─ AreaNotification.cs     one notification + its animation state
   ├─ TextPainter.cs          stroked / centred text, underline
   ├─ CompassLabel.cs         minimap companion label (stub)
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

`/regions` opens the settings.

## Documentation

- [ROADMAP.md](ROADMAP.md) — analysis of the GW2 original and the phased build plan
- [DALAMUD_PLUGIN_GUIDE.md](DALAMUD_PLUGIN_GUIDE.md) — full Dalamud API 15 reference

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).
