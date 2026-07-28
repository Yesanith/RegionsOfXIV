# Architecture

How Regions of XIV decides what to put on screen, and where the pieces live.
For installing and using it, see the [README](README.md).

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

## Fonts

Names also *render* in every client language. The display font defaults to Noto Sans
CJK, which Dalamud already ships — vector, so it is crisp at any size, and it
carries glyphs for every language rather than just the client's. The game's own
Trump Gothic, Jupiter and Axis are selectable alongside it for a more FFXIV look;
each is a fixed-size bitmap and warns when the chosen size outgrows it, and the two
Latin-only faces warn in red on a Japanese client, where they would render place
names as blank boxes.

The Eorzean alphabet used by the decode effect is the only font shipped with the
plugin, and it is optional — see [NOTICE](NOTICE). Without it the reveal falls back
to a plain fade.

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
```

## Why it is split this way

`Plugin` builds the services and owns the Dalamud lifecycle; it decides nothing.
`AnnouncementCoordinator` holds the announcement rules and reaches the screen only
through `INotificationSink`, so those rules can be exercised without an ImGui
context.

`NotificationGate` goes further and touches no Dalamud type at all: it reads the
game through `IGameState`, the settings through `IGateSettings`, and the clock
through an injected delegate. That is what lets the suppression matrix be tested
without a game running — cooldowns are stepped over rather than waited out, and a
cutscene is one line rather than a trip to one.

The tests are not shipped: the packaged plugin is the DLL, its manifest, and the
font. If a test ever fails with a Dalamud assembly load error, the seam has leaked
rather than the test being wrong.
