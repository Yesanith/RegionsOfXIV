# RegionsOfXIV — Roadmap

An FFXIV/Dalamud port of [Nekres' **Regions of Tyria**](https://github.com/agaertner/bhm-zone-display)
(Blish HUD module for Guild Wars 2): *"A beautiful area notification as you walk in
and out of different zones."*

This document analyses the original, maps each concept onto Dalamud/FFXIV, and lays
out a phased build plan.

---

## Current status

| Phase | State |
| --- | --- |
| 1 — Detection core | **Done.** Four sources; see §3.1. |
| 2 — Name resolution | **Done.** All five tiers, client language. Inlined into `Plugin.cs` — it was ~40 lines and had one caller. |
| 3 — Minimal renderer | **Done.** `NotificationOverlay` + `AreaNotification`, easing chain. |
| 4 — Typography | **Partial.** Stroke, underline, overlap, colour pickers, and a four-way display font choice — Noto Sans CJK (Dalamud-shipped, vector, no ceiling, every language) as the default, with the game's Trump Gothic, Jupiter and Axis alongside it. Each game face warns when the size passes its bitmap ceiling; the two Latin-only faces warn in red on a Japanese client, where they render place names as blank boxes rather than merely soft. Per-tier font sizing outstanding. |
| 5 — Reveal effect | **Done.** Per-glyph Eorzean → Latin swap. Polish listed below. |
| 6 — Sound | **Dropped.** Built on `UIGlobals.PlaySoundEffect`, then cut — a zone notification does not need audio, and the settings were dead weight. |
| 7 — Minimap label | **Dropped.** See §3.2. |
| 8 — Config & preview | **Partial.** Four tabs wired; live drag-preview outstanding. |
| 9 — Edge cases | **Partial.** Suppression matrix implemented. Sanctuaries handled; the rest of the list needs play-testing data. |
| 10 — Release | Not started. |

Plus one thing the original had no equivalent for, since GW2 has no native zone
banner to displace: **the native-UI takeover** (§3.3). That is what most of the
recent work went into.

---

## Part 1 — Analysis of Regions of Tyria

### 1.1 What it actually does

Two distinct notifications, plus a persistent label:

| Feature | Trigger | Displays |
| --- | --- | --- |
| **Map notification** | `Gw2Mumble.CurrentMap.MapChanged` event | Region name (small, above) + Map name (large) |
| **Sector notification** | Polled every frame; player position moved into a new sector polygon | Map name (small, above) + Sector name (large) |
| **Compass label** | Whenever either of the above resolves | Current sector name, pinned above the minimap |

Plus a **NotificationIndicator** — a live preview that pops while you drag the font
size / vertical position sliders.

### 1.2 Architecture

```
RegionsOfTyria.cs           module entry: settings, lifecycle, Update() poll loop
├─ AsyncCache<int, Map>          ─┐  GW2 Web API v2
├─ AsyncCache<int, List<Sector>> ─┘  (maps + continent/floor/region/map/sectors)
├─ Geometry/Sector.cs            point-in-polygon (ray casting) against sector bounds
├─ UI/Controls/MapNotification   MonoGame Container, tween chain, dissolve shader
├─ UI/Controls/NotificationIndicator   settings live-preview
├─ Services/CompassService       minimap-anchored label
└─ ref/  dissolve.mgfx, NewKrytan.ttf, StoweTitling.ttf, decode.wav, vanish.wav
```

### 1.3 The three things that make it feel good

**(a) The animation chain.** `MapNotification.Show()` nests Glide tweens:

```
fade in (fadeInDuration)
  → hold 0.2s
    → play decode sound; tween _amount 0→1 (effectDuration)   // the "translation"
      → stop sound; hold at full opacity (showDuration)
        → play vanish sound; tween out (fadeOutDuration)
           dissolve ? {Opacity 0.9, _amount 1→0} : {Opacity 0}
             → Dispose
```

**(b) The dissolve shader.** `dissolve.mgfx` (HLSL) with parameters `Amount`,
`Opacity`, `Slide`, `GlowColor`, `Glow`. Two `SpriteBatchParameters` instances
share it — one draws the New Krytan (fictional alphabet) text dissolving *out* at
`Amount`, the other draws the Latin text dissolving *in* at `1 - Amount`. The
result: the zone name appears in an alien script and "decodes" into English.

**(c) The anti-spam state machine.** This is where the real domain knowledge lives:

| Guard | Purpose |
| --- | --- |
| `NOTIFICATION_COOLDOWN_MS = 2000` (static) | Global floor between any two notifications |
| `playerSpeed > 54` | Skip while sprinting/mounted across boundaries |
| `_currentSector.Id == sector.Id` | Already here |
| `_previousSector.Id == sector.Id` | Ping-ponging across a boundary line |
| `_delaySectorUntil` | Suppress sector alerts while a map alert is still on screen |
| `_hideInCombat` | Don't interrupt combat |
| `!Gw2Mumble.IsAvailable \|\| !IsInGame` | Not actually playing |
| `_lastRun` 10 ms rate limit | Don't run polygon tests every single frame |

Stacking is handled too: a new notification bumps `ZIndex` and calls `SlideDown(150)`
on all live ones, which cancels their tweens and fades them out.

**(d) `FilterDisplayName`** — cleans API junk: drops `((1089116))` placeholders,
strips a prefix before `:` (e.g. `"Weekly Strike Mission: …"`), strips a `(Squad)`
style suffix.

### 1.4 Where the complexity is

Roughly **40% of the codebase exists to answer one question: "which sub-area is the
player standing in?"** GW2's Mumble Link doesn't tell you. So the module must:

1. Hit the Web API for the map, then for every floor of that map, fetch its sectors
2. Cache both async, with retry, and invalidate on locale change
3. Convert `AvatarPosition` from Mumble units → continent coords → swap Y/Z → project to plane
4. Ray-cast the player point against every sector polygon, every tick

**FFXIV does not have this problem.** More on that below.

---

## Part 2 — Mapping GW2 → FFXIV

### 2.1 Concept mapping

| GW2 | FFXIV | Source |
| --- | --- | --- |
| Region (*Kryta*) | `PlaceNameRegion` (*La Noscea*) | `TerritoryType` sheet |
| — | `PlaceNameZone` (*Vylbrand*) | `TerritoryType` sheet |
| Map (*Queensdale*) | `PlaceName` (*Middle La Noscea*) | `TerritoryType` sheet |
| Sector (*Shaemoor Fields*) | **Area** (*Summerford*) | `TerritoryInfo.AreaPlaceNameId` |
| — | **Sub-area** (*Summerford Farms*) | `TerritoryInfo.SubAreaPlaceNameId` |
| Map ID | `TerritoryType` RowId | `IClientState.TerritoryType` |
| Mumble `MapChanged` | `IClientState.TerritoryChanged` / `ZoneInit` | Dalamud |
| `Gw2Mumble.PlayerCharacter.IsInCombat` | `ICondition[ConditionFlag.InCombat]` | Dalamud |
| `IsInGame` | `IClientState.IsLoggedIn` + `ConditionFlag.BetweenAreas` | Dalamud |
| Compass / minimap | `_NaviMap` addon | `AddonNaviMap` (ClientStructs) |
| GW2 Web API v2 | **Lumina** (local game files) | `IDataManager` |

FFXIV gives you **five** naming tiers to GW2's three. A notification can be as
coarse as *La Noscea → Middle La Noscea* or as fine as *Middle La Noscea →
Summerford Farms*.

### 2.2 The big win: sub-area detection is free

```csharp
// FFXIVClientStructs.FFXIV.Client.Game.UI.TerritoryInfo
[FieldOffset(0x24)] public uint AreaPlaceNameId;
[FieldOffset(0x28)] public uint SubAreaPlaceNameId;
```

The game client already computes which named area you're standing in — that's what
drives the text at the top of your minimap. One pointer read replaces the entire
`AsyncCache` + `Sector` + coordinate-conversion + ray-casting stack.

**Delete before you write:** `Sector.cs`, `AsyncCache.cs`, `CoordinatesExtensions.cs`,
`RequestSectors`, `RequestMap`, `GetSector`, `TaskUtil.RetryAsync`, the locale-change
cache invalidation. All of it.

`TerritoryInfo` also hands you `InSanctuary`, `FlyingDisabled`, `MountsAndOrnamentsDisabled`,
`LalafellOnly`, and `MapIdOverride` (used for housing subdivisions) — free context
for future features.

### 2.3 The big loss: no MonoGame

Blish HUD is a MonoGame overlay. Dalamud is ImGui. Everything in the presentation
layer has to be rebuilt:

| Blish HUD | Dalamud equivalent | Difficulty |
| --- | --- | --- |
| `SpriteBatch.DrawStringOnCtrl` | `ImDrawListPtr.AddText` | easy |
| `BitmapFont` from TTF at arbitrary size | `IFontAtlas` / `IFontHandle` | medium |
| `Glide` tweens (`Animation.Tweener.Tween`) | `Dalamud.Interface.Animation.Easing` + `EasingFunctions`, or `Dalamud.Bindings.ImAnim` (API 14+) | easy |
| `SpriteBatchParameters { Effect = … }` HLSL | **no equivalent** | **hard** |
| `SoundEffect` / `SoundEffectInstance` | **no Dalamud audio service** | medium |
| `Container` control on `SpriteScreen` | ImGui window w/ `NoInputs \| NoBackground \| NoDecoration` | easy |
| `ContentService.Textures.Pixel` | `ImDrawList.AddRectFilled` | easy |
| `Control.ZIndex` / stacking | manual ordering in one draw callback | easy |

**The dissolve shader is the one genuinely hard port.** ImGui has no shader hook you
can reach from a plugin's draw callback. Options in §4, Phase 5.

### 2.4 The strategic question: FFXIV already does this

FFXIV natively displays the zone name on entry (a large fading text) and shows the
current sub-area above the minimap. GW2 does *neither* — which is the entire reason
Regions of Tyria exists.

So the value proposition changes. RegionsOfXIV is competing with a built-in feature,
not filling a void. That's fine, but it must be **deliberate**:

- **Complement** — leave the native title alone; add sub-area notifications the game
  never announces on screen (this is the strongest niche: FFXIV silently changes
  sub-area with no on-screen feedback beyond the tiny minimap text).
- **Replace** — hide the native addon and draw your own, styled/configurable.
- **Both, user's choice** — recommended. Default to complement.

Whichever you pick, **the sub-area notification is the killer feature**, not the zone
notification. Prioritise accordingly — this inverts Regions of Tyria's emphasis.

### 2.5 Approval outlook

Purely informational UI, read-only, no server interaction, no automation, no combat
involvement, no networking. This sits comfortably inside the
[plugin restrictions](https://dalamud.dev/plugin-publishing/restrictions). No PAC
pre-clearance needed.

---

## Part 3 — Target architecture

As built:

```
src/RegionsOfXIV/
├─ Plugin.cs                    entrypoint, [PluginService]s, command, lifecycle,
│                               name resolution, and the announcement decisions
├─ Configuration.cs             IPluginConfiguration
├─ Models/
│  └─ LocationSnapshot.cs       the five ids + DiffTier
├─ Services/
│  ├─ LocationTracker.cs        polls TerritoryInfo; LocationChanged + SanctuaryChanged
│  ├─ NotificationGate.cs       debounce/cooldown/suppression state machine
│  ├─ NativeUiSuppressor.cs     hides _AreaText and the loading-screen title
│  ├─ AddonNodeDump.cs          diagnostic; "/regions dump"
│  └─ FontService.cs            game + Eorzean font handles, size ceilings
└─ UI/
   ├─ NotificationOverlay.cs    the full-screen ImGui draw surface
   ├─ AreaNotification.cs       one notification instance + its animation state
   └─ ConfigWindow.cs           settings (Dalamud Windowing API)
```

`assets/fonts/` and `assets/images/` sit outside `src/`; the `.csproj` links the
fonts to `Fonts/` beside the built DLL.

### 3.1 Core data flow

Four sources feed one overlay. Each answers a question the others cannot:

```
IClientState.ZoneInit                          the zone being ENTERED, while the
   └─> TerritoryType sheet                     loading screen is still up —
         └─> Push(region, place)               TerritoryType has not caught up yet

_AreaText addon (PostSetup/PostRefresh)        the game deciding an announcement
   ├─> read its text, hide the addon           is due, in world
   └─> LocationTracker.Poll()
         └─> reconcile: TerritoryInfo if it agrees, the addon's string if not

LocationTracker.SanctuaryChanged               settlements and inns, which the
   └─> TerritoryInfo.InSanctuary edge          place-name ids do not reliably name
         └─> Push(area, subArea ?? last flash)

IFramework.Update @ 200 ms                     backstop for sub-area changes the
   └─> TerritoryInfo place-name ids            game never flashes — the feature
         └─> on change: LocationChanged        FFXIV itself does not offer

                    ↓ all four ↓
              NotificationGate          cooldown, ping-pong, tier enable,
                    ↓                   coarse/fine mutual suppression
           NotificationOverlay.Push(header, text)
```

The gate's dedup is what keeps overlapping sources from double-announcing: whichever
arrives second sees its own snapshot already in `lastAnnounced` and stays quiet.

### 3.2 Why the minimap label was dropped

`CompassLabel` and `NaviMapAnchor` were built, then removed. Anchoring to `_NaviMap`
meant tracking its position, scale, visibility, HUD-layout edits and the user
dragging it — a lot of surface area for a default-off feature that duplicates what
the notification already says. The DTR bar (§7) remains the better idea if a
persistent readout is ever wanted.

### 3.3 The native-UI takeover

FFXIV already announces locations, so this plugin has to displace the game's own UI
rather than fill a void. Two addons, both plain `AtkUnitBase`, both suppressed by
setting `IsVisible = false` from `IAddonLifecycle`:

| Addon | What it is | Replaced from |
| --- | --- | --- |
| `_AreaText` | in-world area flash | `TerritoryInfo`, reconciled against the addon's own text |
| `_LocationTitle`, `_LocationTitleShort` | loading-screen title | `ZoneInit` |

`PostSetup` hides an addon before it first paints; `PreDraw` catches it re-showing
itself partway through its own timeline. Each suppression is paired with its
replacement in config — `ShouldAnnounceZoneEntry` refuses to draw unless
`HideNativeLoadingTitle` is on, so the two can never disagree.

Reading text out of `_AreaText` is deliberately a *tie-breaker*, not the primary
source: structured ids give the parent tier for the header and are correct in every
client language. The addon's string wins only when `TerritoryInfo` disagrees with it
or has nothing — which is what sanctuaries appear to do.

### 3.4 The location snapshot

```csharp
public readonly record struct LocationSnapshot(
    uint TerritoryTypeId,
    uint RegionPlaceNameId,     // TerritoryType.PlaceNameRegion
    uint ZonePlaceNameId,       // TerritoryType.PlaceNameZone
    uint PlacePlaceNameId,      // TerritoryType.PlaceName
    uint AreaPlaceNameId,       // TerritoryInfo.AreaPlaceNameId
    uint SubAreaPlaceNameId);   // TerritoryInfo.SubAreaPlaceNameId
```

Track the previous snapshot; diff to decide what changed and at which tier.

---

## Part 4 — Phased build plan

Each phase is independently shippable/testable. Phases 1–3 give you a working
plugin; 4–7 make it feel good; 8–10 make it releasable.

---

### Phase 0 — Decisions & discovery *(half a day)*

**Deliverable:** a short decisions doc + verified facts. No production code.

- [ ] **Rename the project.** `SamplePlugin` → `RegionsOfXIV` — `.csproj`, `.sln`,
      namespace, `AssemblyName`, manifest. **This sets your permanent `InternalName`.**
      Do it now; it can never change after publication.
- [ ] **Prior-art search.** Check `/xlplugins` and the Dalamud Discord for existing
      zone-notification plugins. If one exists, decide: differentiate or contribute.
- [x] **Identify the native location addons.** There are two, and they are
      different addons for different moments — the original single-addon
      assumption was wrong. **`_AreaText`** is the in-world area flash;
      **`_LocationTitle`** and **`_LocationTitleShort`** carry the loading-screen
      title. None has a dedicated FFXIVClientStructs struct; all are plain
      `AtkUnitBase`, so `addon->IsVisible = false` suppresses them. See §3.3.
- [x] **Minimap sub-area text node.** Moot — the minimap label was dropped (§3.2).
- [ ] **Decide the visual identity.** GW2's gold-on-black Stowe Titling is
      unmistakably GW2. FFXIV's equivalent is its own display typography. Decide
      whether to (a) mimic FFXIV's native zone title, (b) invent something, or
      (c) ship both. This drives Phase 4.
- [ ] **Decide complement vs replace** (§2.4).

---

### Phase 1 — Detection core *(1–2 days)*

**Deliverable:** a plugin that logs every zone/area/sub-area transition accurately.
No UI at all.

- [ ] `LocationTracker` polling `TerritoryInfo` on `IFramework.Update`.
  ```csharp
  public unsafe LocationSnapshot? Read()
  {
      var ti = TerritoryInfo.Instance();
      if (ti == null) return null;
      // TerritoryType lookup gives the region/zone/place tiers
      return new LocationSnapshot(
          ClientState.TerritoryType,
          /* … from the TerritoryType row … */,
          ti->AreaPlaceNameId,
          ti->SubAreaPlaceNameId);
  }
  ```
- [ ] **Framework-thread discipline.** `TerritoryInfo` is game memory; read it only
      on the framework thread. `IClientState` throws off-thread since API 12.
- [ ] **Rate limit.** The original runs its check every 10 ms. You don't need
      per-frame here — a 100–250 ms cadence is plenty for a human-perceptible zone
      change and costs nothing. Gate with a timestamp or `Framework.RunOnTick`.
- [ ] Subscribe `IClientState.TerritoryChanged` (and/or `ZoneInit` for richer args)
      for the coarse zone transition.
- [ ] Handle **`0` sub-area** — leaving a named sub-area sets it to 0. That's a real
      transition, but usually shouldn't fire a notification.
- [ ] Log every transition via `IPluginLog` with all five IDs. **Walk around and
      collect real data** — Limsa (city + sub-areas), an open-world zone with many
      areas (Middle La Noscea), a housing ward (`MapIdOverride` is set here), a
      duty, Island Sanctuary, Occult Crescent, a Bozja/Eureka zone. This dataset
      drives every heuristic in Phase 9.
- [ ] Unsubscribe everything in `Dispose`.

**Exit criteria:** the log shows a correct, complete transition trail with no false
positives while standing still.

---

### Phase 2 — Name resolution *(half a day)*

**Deliverable:** transitions log as human-readable names in the client's language.

- [ ] `PlaceNameResolver` over Lumina:
  ```csharp
  var territory = DataManager.GetExcelSheet<TerritoryType>().GetRow(territoryId);
  var region = territory.PlaceNameRegion.ValueNullable?.Name.ToString();
  var zone   = territory.PlaceNameZone.ValueNullable?.Name.ToString();
  var place  = territory.PlaceName.ValueNullable?.Name.ToString();

  var placeNames = DataManager.GetExcelSheet<PlaceName>();
  var area    = placeNames.GetRowOrDefault(areaId)?.Name.ToString();
  var subArea = placeNames.GetRowOrDefault(subAreaId)?.Name.ToString();
  ```
- [ ] **Use `RowRef` correctly** (Lumina 5): `IsValid`, `Value` (throws), or
      `ValueNullable` (returns null). Rows are structs — copy freely.
- [ ] **SeString handling.** `PlaceName.Name` is a `ReadOnlySeString`. `ToString()`
      strips payloads and is fine here; use `ToMacroString()` when debugging to see
      what's actually in there. Some place names carry formatting payloads.
- [ ] **Language.** Respect `IClientState.ClientLanguage`. Lumina sheets are
      per-language; optionally let users force a language in config (a genuinely
      nice feature — play in JP, read zone names in EN).
- [ ] **`FilterDisplayName` port.** The GW2 rules (strip `((…))`, strip before `:`,
      strip trailing `(…)`) are GW2-API-specific and mostly **don't apply**. Start
      with a no-op and add rules only where real data demands it — e.g. duty names
      with a difficulty suffix, or `<SoftHyphen>` payloads. Don't cargo-cult this.
- [ ] Cache resolved strings per snapshot; don't re-query Lumina every tick (though
      Lumina 5 reads are cheap enough that correctness beats micro-optimisation).

**Exit criteria:** `Middle La Noscea → Summerford Farms` in the log, correct in at
least EN + one other client language.

---

### Phase 3 — Minimal renderer *(2–3 days)*

**Deliverable:** text fades in and out on screen when you change area. Ugly but real.

- [ ] `NotificationOverlay` — a single always-present ImGui window:
  ```csharp
  Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoBackground  | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing;
  ```
  Size/position it to the viewport, draw into `ImGui.GetWindowDrawList()`.
- [ ] **Register it with `WindowSystem`** — the Dalamud Windowing API is an
      *approval criterion*, and it gives you close-order integration and UI-hide
      behaviour for free.
- [ ] Respect UI hiding: the overlay should vanish during cutscenes/gpose/when the
      user hides the UI. `IUiBuilder` exposes `CutsceneActive`, `ShouldModifyUi`,
      and the `Disable*UiHide` toggles — **don't** set those to force visibility.
- [ ] `AreaNotification` — one instance holds `(header, text, startTime, phase)`.
- [ ] **Animation.** Port the tween chain with `Dalamud.Interface.Animation.Easing`
      (`InOutCubic`, `OutQuint`, `OutSine`, …). Use `ValueClamped` / `ValueUnclamped`
      — the bare `Value` property is deprecated. Alternatively try
      `Dalamud.Bindings.ImAnim` (API 14+) for a closer Glide analogue.
  ```
  FadeIn → Hold(0.2s) → Reveal → Show → FadeOut → dispose
  ```
  Drive it off wall-clock deltas, not frame counts.
- [ ] **Scale everything** by `ImGuiHelpers.GlobalScale`. Non-negotiable — users run
      HUD scales from 50% to 200%.
- [ ] Vertical position as a % of viewport height (the original defaults to 25%).
- [ ] Stacking: keep a `List<AreaNotification>`; when a new one arrives, offset the
      existing ones downward and fade them (the `SlideDown(150)` behaviour).
- [ ] A `/regions` command that fires a test notification — you'll use this
      constantly.

**Exit criteria:** walking between sub-areas produces a clean fade-in/hold/fade-out
of the correct text at the right screen position.

---

### Phase 4 — Typography *(2–3 days)*

**Deliverable:** it looks intentional.

- [ ] **Font loading.** `IUiBuilder.FontAtlas` + `IFontHandle`. Options:
  - Bundle a TTF and build a handle at the configured size
  - Use the game's own fonts (Axis/Jupiter) — richer, more native-feeling, and
    available through the font atlas from game files
  - Fall back to `DefaultFontHandle` if a custom font fails
- [ ] **Font size changes must rebuild the handle.** Regions of Tyria disposes and
      recreates its `BitmapFont` on every size change (`UpdateFonts`). Same here —
      and rebuild off the hot path, not inside `Draw()`.
- [ ] **Stroked text.** ImGui has no outline. Draw the glyph run 4–8 times at ±1–2 px
      offsets in the stroke colour, then once in the fill colour on top. The original
      uses `STROKE_DIST = 1` and a black 0.8-alpha stroke. Wrap this in
      `TextPainter.DrawStroked(drawList, font, size, pos, text, fill, stroke)`.
- [ ] **Header underline.** The original draws it as four rects (two border, two
      fill, drawn as left/right halves so it can animate outward from centre) *before*
      the header text, so serifs aren't overdrawn. `ImDrawList.AddRectFilled`, same
      ordering. Animate its width by the reveal progress.
- [ ] **Overlap mode** — the optional style where the large text rides up over the
      header baseline.
- [ ] **Colours.** GW2's palette is `_brightGold (223,194,149)` / `_darkGold
      (178,160,145)`. Pick an FFXIV-appropriate pair and expose both in config.
- [ ] **Multi-line.** The original splits on a literal `<br>` sentinel and measures
      each line separately to work around titling-font line-height overlap. In ImGui,
      handle wrapping explicitly with `CalcTextSize` per line.
- [ ] Centre horizontally on the viewport, not the window.

**Exit criteria:** side-by-side screenshot against the native FFXIV zone title that
you're happy to post.

---

### Phase 5 — The reveal effect *(mostly done — ~1 day remaining)*

**Deliverable:** the "decode" moment. This is what makes the original memorable.

> **Revised after the font landed.** This phase was originally scoped as the highest
> risk in the project, on the assumption that porting the GW2 two-pass HLSL dissolve
> was the only route and that a Dalamud plugin cannot inject a shader into ImGui's
> pipeline. The second half is still true — but it turned out not to matter.
>
> `assets/fonts/Eorzea.ttf` maps **Latin A–Z, a–z, digits and punctuation 1:1**
> (240 codepoints, plus accented Latin and fi/fl ligatures), exactly as GW2's New
> Krytan font does. So the identical string renders as Eorzean script in one font
> and plain text in the other, with no transliteration layer — which is the actual
> mechanism behind the original effect. The shader was only ever the *transition*,
> not the substitution.

**Implemented** in `NotificationOverlay.DrawDecodingLine`:

- Glyphs swap **individually**, not by cross-fading, so two glyph shapes never
  overlap into mush — this is what the GW2 dissolve shader was buying, achieved
  with a hard per-glyph swap instead.
- Positions come from the **final Latin layout**, so the line never shifts as it
  decodes.
- Falls back to a plain fade when no font is bundled, when `DecodeEffectEnabled`
  is off, or when the text falls outside Latin coverage (the font has no CJK — a
  JP/CN/KR client would otherwise render a line of tofu).

**Remaining polish:**

- [ ] Stagger the swap with a soft per-glyph ramp rather than a hard threshold, so
      the boundary reads less mechanically.
- [ ] Consider a brief per-glyph brightness pop at the moment of swap (the GW2
      shader's `GlowColor`).
- [ ] Per-glyph stroke is currently 8 `AddText` stamps per character. Fine at these
      string lengths, but measure in Plugin Stats before raising the font size cap.
- [ ] The Eorzean glyphs sit on Latin advance widths. Verify this reads well at
      display sizes; the font is a substitution face so it should, but confirm.

**Still deliberately out of scope:** noise-mask compositing via an offscreen render
(`ITextureProvider` / `ITextureReadbackProvider`) would get closer to a true
dissolve. Treat as a post-1.0 experiment; the per-glyph swap is good enough that it
should not block a release.

---

### Phase 6 — Sound — **DROPPED**

Built, shipped behind an off-by-default toggle, then removed along with its four
config properties and its settings tab. A zone notification is a glance-and-forget
thing; audio on every sub-area crossing is noise, and "off by default" settings
nobody turns on are just surface area. The notes below stand if it is ever revived.

**Was:** optional reveal/vanish sounds.

Dalamud has no audio service. Two paths:

- **Game sounds (recommended).** `UIGlobals.PlaySoundEffect(uint effectId)` from
  ClientStructs plays the game's own UI sound effects. Zero bundled assets, always
  fits the soundscape, respects nothing (see below). `PlayChatSoundEffect(id)` maps
  to the `<se.N>` chat sounds.
  ```csharp
  FFXIVClientStructs.FFXIV.Client.UI.UIGlobals.PlaySoundEffect(effectId);
  ```
  Let the user pick the effect ID from a list; ship sensible defaults.
- **Bundled audio.** NAudio or similar. Full control over the sound, but adds a
  dependency, needs your own volume/device handling, and won't follow the game's
  audio settings or mute-on-focus-loss.

**Requirements either way:**
- [ ] Separate volume + mute for reveal and vanish (mirrors the original).
- [ ] **Default to muted or very quiet.** Unexpected sounds are the fastest route to
      a one-star review.
- [ ] Respect the game being backgrounded / audio muted.
- [ ] If you bundle audio, note that **AI-generated audio must be toggleable and
      disclosed** under the AI policy.

---

### Phase 7 — Minimap companion label — **DROPPED**

**Was:** persistent sub-area name anchored to the minimap, the `CompassService`
equivalent. Built as `CompassLabel` + `NaviMapAnchor`, then removed (§3.2).

The warning below turned out to be the right instinct, and it applied more broadly
than expected: `_NaviMap` already shows the current area, so the label restyled
existing information rather than adding any — while costing a running subscription
to the minimap's position, scale, visibility and HUD-layout state. The rest of this
section is kept for whoever revisits the idea; §7 argues the DTR bar is the better
shape for it.

⚠️ **Check first whether this is redundant.** `_NaviMap` may already show the
current area. If so, the feature becomes *restyling* or *showing a different tier*
(e.g. minimap shows sub-area, your label shows area+sub-area), not net-new
information. Validate in Phase 0.

- [ ] `NaviMapAnchor` reads `_NaviMap` via `IGameGui.GetAddonByName("_NaviMap")`
      → `AtkUnitBasePtr`, and pulls `X`, `Y`, `Scale`, `IsVisible`, plus the
      root node's width/height.
- [ ] Use `IAddonLifecycle` to track setup/finalize/show/hide/**move** rather than
      polling. API 14 added `Move`, `Show`, `Hide` events precisely for this — `Move`
      fires when a drag *completes*.
- [ ] Handle: minimap hidden, HUD layout editing, map open (`AreaMap`), user moved
      or rescaled it, ultrawide/multi-monitor.
- [ ] Fade the label out on mouse-over of the minimap (the original does this so it
      never blocks the compass).
- [ ] Optional dark gradient backing (the original's `fade-down-46` texture) —
      `ImDrawList.AddRectFilledMultiColor` gets you a gradient without an asset.
- [ ] **Alternative worth considering:** put the sub-area in the **DTR bar**
      (`IDtrBar`) instead. It's one line of code, it's a first-class Dalamud
      surface, users can position it themselves, and it sidesteps every addon-anchoring
      edge case. Offer both.

---

### Phase 8 — Configuration & live preview *(1–2 days)*

**Deliverable:** a settings window matching the original's coverage.

Port the setting groups:

| Group | Settings |
| --- | --- |
| **General** | reveal effect on/off, underline header, overlap header, vertical position, font size, hide in combat |
| ~~**Sound**~~ | ~~reveal volume, vanish volume, mute reveal, mute vanish~~ — dropped |
| **Durations** | show, fade-in, fade-out, effect |
| **Zone notification** | enabled, include region |
| **Area notification** | enabled, include zone |
| **Sub-area notification** | enabled, include area *(new — FFXIV has a tier GW2 doesn't)* |
| **Minimap / DTR** | show area label, background, which surface |

- [ ] `Configuration : IPluginConfiguration` with a `Version` field for migrations.
- [ ] `ConfigWindow : Window`, registered with `WindowSystem`, wired to
      `UiBuilder.OpenConfigUi`.
- [ ] **Live preview.** The original's `NotificationIndicator` shows a sample while
      you drag sliders, auto-disposing 250 ms after the last change. Port this — it
      makes the position/size sliders usable instead of guesswork.
- [ ] Re-fire a sample notification when a *style* setting changes (the original's
      `PopNotification`), so users see the effect immediately.
- [ ] Save on change (debounced), not on every slider frame.

---

### Phase 9 — Edge cases & the suppression matrix *(2–3 days)*

**Deliverable:** it never annoys anyone. This is what separates a toy from a plugin
people keep installed.

Port the original's guards, adapted:

| Guard | FFXIV implementation |
| --- | --- |
| Global cooldown | Static timestamp, ~2 s floor |
| Zone alert suppresses area alert | `_delaySectorUntil` equivalent — compute from show+fadeout duration |
| Same area | Compare snapshot tiers |
| Ping-pong across a boundary | Keep `previous` as well as `current`; ignore an immediate return |
| Moving too fast | No Mumble speed value — derive from `IObjectTable.LocalPlayer.Position` delta per tick, or check mount/sprint state |
| In combat | `ICondition[ConditionFlag.InCombat]` |
| Not in game | `IClientState.IsLoggedIn` |
| **Loading / zoning** | `ConditionFlag.BetweenAreas`, `BetweenAreas51` — **new, essential**; never fire mid-loading-screen |
| **Cutscene** | `ConditionFlag.OccupiedInCutSceneEvent`, `WatchingCutscene`, `WatchingCutscene78` |
| **GPose** | `IClientState.IsGPosing` |
| **In a duty** | `ConditionFlag.BoundByDuty` — probably suppress by default, user-configurable |
| **PvP** | `IClientState.IsPvP` — suppress; nothing that could read as an advantage |

FFXIV-specific cases to test explicitly:
- [ ] **Housing** — wards/subdivisions/apartments; `TerritoryInfo.MapIdOverride` is
      set here and place names behave differently
- [ ] **Instanced zones** — the `(Instance 2)` suffix on zone names
- [ ] **Duties/raids** — sub-areas exist inside some instances
- [ ] **Island Sanctuary** (`MJI`), **Occult Crescent** (`MKD`), **Bozja** (`MYC`),
      **Eureka** — large zones with heavy sub-area usage; the best stress test
- [ ] **Inn rooms**, **Gold Saucer**, **Firmament** (`HwdDev`)
- [ ] Logout → login without a zone change
- [ ] `/xldev` plugin reload while in a sub-area (state must rebuild correctly)
- [ ] Language change mid-session

**Performance check:** `/xldev` → Plugins → Open Plugin Stats. Your per-tick cost
should be indistinguishable from zero.

---

### Phase 10 — Release *(1–2 days)*

- [ ] **Manifest** — `Name`, `Author`, `Punchline`, `Description`, `RepoUrl`,
      `Tags`, `CategoryTags`. Either `RegionsOfXIV.json` or csproj properties
      (API 14+ SDK supports both).
- [ ] **Verify the manifest ships inside the zip and is accurate** — API 15 no longer
      overwrites it at install time.
- [ ] **Icon** — `images/icon.png`, 1:1, 64×64–512×512. **Hand-make it.** The AI
      policy explicitly prefers "a crude MS Paint icon over an AI-generated icon,"
      and the team may ask you to replace one. Ask in the Discord — people volunteer.
- [ ] Up to five `image1..5.png` preview shots. A zone-notification plugin lives or
      dies on its screenshots; make them good.
- [ ] **Semantic version.** Not a timestamp, not a build counter — that's an explicit
      D17 technical criterion.
- [ ] Test a clean install from a local dev-plugin folder, then a clean uninstall.
- [ ] PR to `DalamudPluginsD17` → **`testing/live/RegionsOfXIV/`** (new plugins must
      start in testing). One plugin, one branch.
- [ ] `manifest.toml` with `repository`, `commit`, `owners`, `project_path`,
      `changelog`.
- [ ] **Disclose AI usage** in the PR body if you used it at Assist level or above.
- [ ] Expect a week or more in the queue; 4 approving votes needed for a new plugin.

---

## Part 5 — Effort summary

| Phase | Scope | Est. | Risk |
| --- | --- | --- | --- |
| 0 | Decisions & discovery | 0.5 d | — |
| 1 | Detection core | 1–2 d | low |
| 2 | Name resolution | 0.5 d | low |
| 3 | Minimal renderer | 2–3 d | low |
| 4 | Typography | 2–3 d | medium |
| 5 | Reveal effect | ~1 d remaining | low |
| 6 | ~~Sound~~ | dropped | — |
| 7 | ~~Minimap label~~ | dropped | — |
| 8 | Config & preview | 1–2 d | low |
| 9 | Edge cases | 2–3 d | medium |
| 10 | Release | 1–2 d | low |

**~16–26 days** of focused work to a polished 1.0. **Phases 1–3 (~4–6 days) get you a
functioning plugin** you can already use daily.

### Suggested milestones

- **v0.1 — "it works"**: Phases 1–3. Fade-in/out sub-area text.
- **v0.2 — "it's pretty"**: Phase 4 + the easy parts of 8. Shareable screenshots.
- **v0.5 — "testing track"**: Phases 5–9. Submit to `testing/live`.
- **v1.0 — "stable"**: after real testing-track feedback, move to `stable/`.

---

## Part 6 — Things the original got right, worth copying verbatim

1. **The nested-tween animation chain.** The 0.2 s pause between fade-in and reveal
   is what makes it feel deliberate rather than twitchy. Keep the timing structure
   even though the implementation changes.
2. **Every duration is user-configurable.** Four separate knobs (show, fade-in,
   fade-out, effect). People's tolerance for on-screen text varies enormously.
3. **The ping-pong guard.** Standing on a zone boundary is common and the naive
   implementation strobes. `_previousSector` is a two-line fix for a fatal annoyance.
4. **Suppressing the fine-grained alert while the coarse one is showing.**
   `_delaySectorUntil` prevents "Middle La Noscea" and "Summerford Farms" from
   fighting over the same pixels.
5. **The live settings preview.** Sliders for position and size are unusable without it.
6. **Header becomes the main text when the main text is empty.** A tiny graceful
   degradation that prevents an empty notification.
7. **Optional prefix at every tier.** Users choose how much hierarchy they want.

## Part 7 — Things to deliberately do differently

1. **Invert the emphasis.** Sub-area notifications are the value; zone notifications
   duplicate a native feature. Default the zone notification to *off* or
   *complement*, and make sub-area alerts the headline.
2. **Don't port `FilterDisplayName` blindly.** Its rules encode GW2 Web API quirks.
   Start empty; add rules from observed FFXIV data.
3. **Offer the DTR bar** as an alternative to minimap anchoring. Less code, fewer
   edge cases, more user control.
4. **Suppress in duties by default.** FFXIV instance content has sub-areas and players
   are busy; GW2 doesn't have the same problem shape.
5. **Consider the reveal effect optional and off by default.** The GW2 New Krytan
   decode is a beloved, lore-specific gimmick. An FFXIV equivalent without that
   grounding may just read as noise.

---

## Reference

- Original module: <https://github.com/agaertner/bhm-zone-display>
- Dalamud plugin guide: [`DALAMUD_PLUGIN_GUIDE.md`](DALAMUD_PLUGIN_GUIDE.md)
- `TerritoryInfo`: [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/TerritoryInfo.cs)
- `AddonNaviMap`: [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonNaviMap.cs)
- Sheet schemas: [EXDSchema](https://github.com/xivdev/EXDSchema) — `TerritoryType.yml`, `Map.yml`, `PlaceName.yml`
- Dalamud easing: `Dalamud.Interface.Animation.Easing` + `EasingFunctions`
- Windowing API: <https://dalamud.dev/api/Dalamud.Interface.Windowing/>
