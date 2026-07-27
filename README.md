# Regions of XIV

A Dalamud plugin that announces the region, zone, area and sub-area you walk into
with a styled, animated on-screen notification.

Final Fantasy XIV announces zone changes on screen, but changes your **sub-area**
silently — the only feedback is the small text above the minimap. Regions of XIV
surfaces those transitions, and lets you restyle zone announcements to taste.

Inspired by [Nekres' *Regions of Tyria*](https://blishhud.com/modules/?module=Nekres.Regions_Of_Tyria)
for Guild Wars 2.

> **Status: in development.** Detection, naming, the notification pipeline and the
> Eorzean reveal effect are all confirmed working in game. Remaining: polish and
> release packaging.

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

## Building

Requires the **.NET 10 SDK (10.0.101 or later)** and a XIVLauncher install that has
run Dalamud at least once.

```sh
dotnet build RegionsOfXIV.sln -c Release
```

Output lands in `src/RegionsOfXIV/bin/x64/Release/RegionsOfXIV/`, packaged and ready
to point Dalamud's **Dev Plugin Locations** at.

The tests need neither the game nor Dalamud, so they run anywhere the SDK does:

```sh
dotnet test RegionsOfXIV.sln -c Release
```

## Usage

| Command | Effect |
| --- | --- |
| `/regions` | open the settings |
| `/regions test` | fire a sample notification, bypassing the suppression rules |

## Feedback

Issues and suggestions are welcome at
[github.com/Yesanith/RegionsOfXIV](https://github.com/Yesanith/RegionsOfXIV).

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).

The licence covers this project's own source. It does **not** extend to third-party
material the plugin uses or references — see [NOTICE](NOTICE).
