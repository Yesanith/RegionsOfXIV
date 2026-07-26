# Dalamud Plugin Development Guide

A practical guide to building FFXIV plugins with Dalamud, written against **this** repository (the
[goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin) template) and the official docs at
[dalamud.dev](https://dalamud.dev).

**Target as checked out here:** Dalamud **API 15**, `Dalamud.NET.Sdk/15.0.0`, `net10.0-windows`, game Patch 7.5.

---

## Table of contents

1. [What Dalamud actually is](#1-what-dalamud-actually-is)
2. [This repository, file by file](#2-this-repository-file-by-file)
3. [Prerequisites and the dev loop](#3-prerequisites-and-the-dev-loop)
4. [Renaming the template for your plugin](#4-renaming-the-template-for-your-plugin)
5. [Project layout and the SDK](#5-project-layout-and-the-sdk)
6. [The plugin manifest](#6-the-plugin-manifest)
7. [The entrypoint and the plugin lifecycle](#7-the-entrypoint-and-the-plugin-lifecycle)
8. [Services: the full catalogue](#8-services-the-full-catalogue)
9. [Configuration](#9-configuration)
10. [UI with ImGui and the Windowing API](#10-ui-with-imgui-and-the-windowing-api)
11. [Textures and icons](#11-textures-and-icons)
12. [Game data with Lumina](#12-game-data-with-lumina)
13. [SeString](#13-sestring)
14. [Commands, chat, and the DTR bar](#14-commands-chat-and-the-dtr-bar)
15. [Interacting with the game: the three tiers](#15-interacting-with-the-game-the-three-tiers)
16. [Hooking game functions](#16-hooking-game-functions)
17. [Calling game functions](#17-calling-game-functions)
18. [Native UI: AddonLifecycle and AddonEventManager](#18-native-ui-addonlifecycle-and-addoneventmanager)
19. [Reverse engineering](#19-reverse-engineering)
20. [Versions, API levels, and release channels](#20-versions-api-levels-and-release-channels)
21. [Technical considerations and performance](#21-technical-considerations-and-performance)
22. [Plugin restrictions — read before you build](#22-plugin-restrictions--read-before-you-build)
23. [The AI usage policy](#23-the-ai-usage-policy)
24. [Publishing to the official repository](#24-publishing-to-the-official-repository)
25. [Custom repositories](#25-custom-repositories)
26. [Debugging toolbox](#26-debugging-toolbox)
27. [Pitfalls checklist](#27-pitfalls-checklist)
28. [Reference links](#28-reference-links)

---

## 1. What Dalamud actually is

Dalamud is a .NET plugin framework injected into the FFXIV client by
[XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher). It hosts a CLR inside the game process,
loads your plugin DLL, and hands you a set of managed services that wrap the game's native state.

Three projects matter, and it is worth keeping them straight:

| Project | Role |
| --- | --- |
| **Dalamud** | The framework. Loads plugins, provides services, renders ImGui, manages hooks. |
| **FFXIVClientStructs** ("ClientStructs" / CS) | Community-maintained C# bindings for the game's own structs and member functions. Shipped *with* Dalamud — you don't add it yourself. |
| **Lumina** | Reader for the game's `.dat`/`.exd` data files — items, territories, actions, quests, etc. Also ships with Dalamud. |

Because Dalamud ships CS and Lumina, your `.csproj` stays nearly empty; the SDK wires the references for you.

An important framing point from the docs: everything you write runs **inside** the game process. There is
no sandbox. An unhandled exception in a hook will take the client down with it.

---

## 2. This repository, file by file

```
RegionsOfXIV/
├── .editorconfig                   C# style rules (goatcorp defaults)
├── .github/workflows/pr-build.yml  CI: builds the plugin on PRs to master
├── Data/goat.png                   Sample image asset, copied to output
├── LICENSE.md                      AGPL-3.0-or-later
├── README.md                       Template readme
├── SamplePlugin.sln
└── SamplePlugin/
    ├── SamplePlugin.csproj         Uses Dalamud.NET.Sdk/15.0.0
    ├── SamplePlugin.json           Plugin manifest (name, author, tags…)
    ├── packages.lock.json          Locked NuGet graph (DalamudPackager, DotNet.ReproducibleBuilds)
    ├── Plugin.cs                   IDalamudPlugin entrypoint
    ├── Configuration.cs            IPluginConfiguration
    └── Windows/
        ├── MainWindow.cs           Window with image, job icon, territory lookup
        └── ConfigWindow.cs         Window with two checkboxes
```

What each sample file demonstrates:

- [Plugin.cs](SamplePlugin/Plugin.cs) — service injection, `WindowSystem` setup, slash-command
  registration, `UiBuilder` event subscription, and a symmetric `Dispose()`.
- [Configuration.cs](SamplePlugin/Configuration.cs) — a serializable config with a `Save()` convenience
  wrapper.
- [MainWindow.cs](SamplePlugin/Windows/MainWindow.cs) — `ImRaii` scoped ImGui calls, loading a texture
  from disk, loading a game icon by ID, reading `IPlayerState`, and a Lumina `TerritoryType` lookup.
- [ConfigWindow.cs](SamplePlugin/Windows/ConfigWindow.cs) — `PreDraw()` flag manipulation and the
  "save immediately on change" pattern.

### Two discrepancies worth fixing in this checkout

1. **README says .NET 8, the project is .NET 10.** [README.md:54](README.md#L54) states "A .NET Core 8
   SDK has been installed". The actual target framework is `net10.0-windows` and
   [pr-build.yml:22](.github/workflows/pr-build.yml#L22) installs `10.0.x`. Install the **.NET 10 SDK**.
2. **Stale GUID in the solution.** [SamplePlugin.sln:17-20](SamplePlugin.sln#L17-L20) has configuration
   entries for project `{4FEC9558-EB25-419F-B86E-51B8CFDA32B7}`, which has no matching `Project(...)`
   block. Harmless, but it's leftover noise you can delete.

---

## 3. Prerequisites and the dev loop

### Prerequisites

- **XIVLauncher + FFXIV + Dalamud**, and the game launched with Dalamud **at least once**. That first
  run is what populates `%AppData%\XIVLauncher\addon\Hooks\dev\`, which is where the SDK resolves the
  Dalamud reference assemblies from.
- **.NET 10 SDK**.
- **Visual Studio 2022** or **JetBrains Rider**. Either works; Rider is common in this community.
- If Dalamud lives somewhere non-default, set the `DALAMUD_HOME` environment variable to that dev
  directory.

### Build

```powershell
dotnet build --configuration Debug
```

Output lands in `SamplePlugin\bin\x64\Debug\SamplePlugin\`. That whole folder is the plugin —
`DalamudPackager` copies the DLL, the generated manifest, and your content files into it, and
deliberately *omits* anything Dalamud already provides. You can zip that directory as-is for
distribution.

### Load it in-game

1. `/xlsettings` → **Experimental** → add the full path to `SamplePlugin.dll` under **Dev Plugin
   Locations**. (One time only; it persists.)
2. `/xlplugins` → **Dev Tools → Installed Dev Plugins** → enable your plugin.
3. Run `/pmycommand`.

### Iterating

The fast loop is: edit → build → in the Dev Plugins list, hit the reload button. You do not need to
restart the game. If the DLL is locked on rebuild, the plugin is still loaded — disable it first.

Every Dalamud console command has a chat form (`/xlplugins`) and a console form (`xlplugins`, in the
Dalamud console window). The console form works when you're not logged in.

---

## 4. Renaming the template for your plugin

Do this **before** you write much code, because one of these values is permanent.

The `AssemblyName` becomes your plugin's **InternalName**. It determines the config directory path, the
DLL filename, and your log prefix. Per the docs, once published it **may not be changed**. Choose
carefully.

Steps:

1. Rename the folder `SamplePlugin/` → `RegionsOfXIV/`.
2. Rename `SamplePlugin.csproj` → `RegionsOfXIV.csproj`, `SamplePlugin.json` → `RegionsOfXIV.json`,
   `SamplePlugin.sln` → `RegionsOfXIV.sln`.
3. Update the `Project(...)` path in the `.sln`, and drop the orphan GUID block noted above.
4. Replace the `namespace SamplePlugin` / `SamplePlugin.Windows` declarations and the matching `using`
   in [Plugin.cs](SamplePlugin/Plugin.cs).
5. Update `Solution_Name`, the artifact `name`, and the artifact `path` in
   [pr-build.yml](.github/workflows/pr-build.yml).
6. Fill in `RegionsOfXIV.json` — see the next section.
7. Delete `SamplePlugin/obj/` and `bin/` so stale build artifacts don't confuse the new build. (Both are
   gitignored but they exist on disk here.)

The default `AssemblyName` is inferred from the project filename, so renaming the `.csproj` is
sufficient — you don't need an explicit `<AssemblyName>` element.

---

## 5. Project layout and the SDK

The canonical layout from the docs:

```
MySolution/
├── MyPlugin/
│   ├── MyPlugin.csproj
│   ├── MyPlugin.json
│   ├── packages.lock.json
│   └── Plugin.cs
└── MySolution.sln
```

You may nest under `src/`, and you may have additional projects in the solution in any .NET language.
Nothing is enforced beyond "one class implementing `IDalamudPlugin`".

### The csproj

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
  <PropertyGroup>
    <Version>0.0.0.1</Version>
    <PackageProjectUrl>https://github.com/you/RegionsOfXIV</PackageProjectUrl>
    <PackageLicenseExpression>AGPL-3.0-or-later</PackageLicenseExpression>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="..\Data\goat.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Visible>false</Visible>
    </Content>
  </ItemGroup>
</Project>
```

Things to understand here:

- **`Sdk="Dalamud.NET.Sdk/15.0.0"`** — this single line sets the target framework, the x64 platform, the
  Dalamud/ImGui/Lumina/ClientStructs references, `DalamudPackager`, nullable and unsafe settings, and
  deterministic build flags. The SDK major version is pinned to the Dalamud API level. To move to a new
  API level, you bump this number.
- **`<Version>`** — this is what becomes `AssemblyVersion` in the generated manifest. The plugin
  installer uses it to detect updates, so bump it on every release.
- **`<Content Include>` with `<Visible>false</Visible>`** — the pattern for shipping data files that live
  outside the project directory, without cluttering the IDE's solution tree.
- **`packages.lock.json`** — keep it committed. The D17 build system builds with locked restore, so a
  missing or stale lockfile breaks your submission. Regenerate with
  `dotnet restore --force-evaluate` after changing package references.

### Adding NuGet packages

You can, but weigh it. Anything you reference and that Dalamud doesn't already provide gets copied into
your output folder and shipped to users, and reviewers will look at it. Prefer what's already in the box.
Never add `Dalamud`, `FFXIVClientStructs`, `Lumina`, or `ImGui.NET` manually — the SDK supplies them, and
adding your own copy causes assembly conflicts at load.

---

## 6. The plugin manifest

`RegionsOfXIV.json` sits next to your DLL and drives your entry in the plugin installer. JSON and YAML
are both supported (YAML uses `snake_case` keys instead of `CamelCase`).

### Required

| Field | Meaning |
| --- | --- |
| `Name` | Display name in `/xlplugins`. |
| `Author` | Your name/handle. |
| `Punchline` | One-line summary. Keep it to a single line — it is rendered as one. |
| `Description` | The long description. List your slash commands here. |

`RepoUrl` is listed as required by the project-layout page and is effectively mandatory for submission —
set it to your GitHub repo.

### Commonly used optional fields

| Field | Meaning |
| --- | --- |
| `ApplicableVersion` | Game version constraint; `"any"` unless you have a reason. |
| `Tags` | Free-form search keywords. |
| `CategoryTags` | Installer category placement. |
| `IconUrl`, `ImageUrls` | Only for custom repos — D17 takes images from the PR instead. |
| `Changelog` | Shown on update. For D17, prefer the `changelog` key in `manifest.toml`. |
| `AcceptsFeedback`, `FeedbackMessage` | Controls the in-installer feedback box. |
| `LoadRequiredState` | When your plugin may load — e.g. only once game data is available. |
| `LoadSync` | Load synchronously rather than in parallel. Slows startup; use only if you must. |
| `CanUnloadAsync` | Whether unloading off the main thread is safe for you. |
| `LoadPriority` | Higher loads earlier. Only relevant if other plugins depend on you. |

### Never write these by hand

`DalamudPackager` injects them at build time and hand-written values will be overwritten or rejected:

- `InternalName`
- `AssemblyVersion`
- `DalamudApiLevel`

### Filled in for this repo

```json
{
  "Author": "Yunus Alperen",
  "Name": "Regions of XIV",
  "Punchline": "A short one-liner that shows up in /xlplugins.",
  "Description": "A longer description. List any major slash-command(s).",
  "RepoUrl": "https://github.com/<you>/RegionsOfXIV",
  "ApplicableVersion": "any",
  "Tags": ["map", "territory", "zones"]
}
```

---

## 7. The entrypoint and the plugin lifecycle

Your DLL must contain **exactly one** class implementing `IDalamudPlugin`. Dalamud constructs it on
load and calls `Dispose()` on unload.

### Service injection

Two styles, both used in the wild:

**Static properties with `[PluginService]`** (what this template uses):

```csharp
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    // ...
}
```

Dalamud populates these via reflection *before* the constructor body runs, so they're safe to use inside
the constructor. The `= null!` silences the nullable analyzer for something the framework guarantees.

**Constructor injection:**

```csharp
public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
{
    // ...
}
```

Cleaner for testing; the static approach is more convenient in a codebase where many types need
services. Pick one and be consistent. A common middle ground is a static `Services` class holding all
the `[PluginService]` properties, initialized with
`pluginInterface.Create<Services>()`.

### Lifecycle rules

Dalamud gives you no `Update()` or `Initialize()` overrides. The constructor *is* initialization, and
`Dispose()` *is* teardown. Everything else is event subscription.

**The single most important rule:** for every `+=`, there is a matching `-=`; for every `AddHandler`, a
`RemoveHandler`; for every `RegisterListener`, an `UnregisterListener`. The template's `Dispose()` mirrors
its constructor line for line — keep that discipline:

```csharp
public void Dispose()
{
    PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
    PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
    PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

    WindowSystem.RemoveAllWindows();

    ConfigWindow.Dispose();
    MainWindow.Dispose();

    CommandManager.RemoveHandler(CommandName);
}
```

If you leak a subscription, the next hot-reload crashes the game when Dalamud invokes a delegate pointing
into an unloaded assembly. This is the number-one cause of "my plugin works until I reload it".

### The three UiBuilder entry points

```csharp
PluginInterface.UiBuilder.Draw         += WindowSystem.Draw;  // called every frame
PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;     // gear icon in the installer
PluginInterface.UiBuilder.OpenMainUi   += ToggleMainUi;       // main-UI button in the installer
```

Wiring `OpenConfigUi` and `OpenMainUi` is cheap and users expect both. Do it.

### Useful IDalamudPluginInterface members

| Member | Use |
| --- | --- |
| `GetPluginConfig()` / `SavePluginConfig()` | Config persistence. |
| `AssemblyLocation` | Path to your DLL — the basis for locating shipped assets. |
| `ConfigDirectory` / `ConfigFile` | Your private storage under `%AppData%\XIVLauncher\pluginConfigs\`. |
| `Manifest` | Your own manifest at runtime (`Manifest.Name`, version, …). |
| `UiBuilder` | Rendering and UI events. |
| `IsDev` | True when loaded as a dev plugin — handy for gating debug UI. |
| `GetIpcSubscriber<T>()` / `GetIpcProvider<T>()` | Cross-plugin IPC. |

---

## 8. Services: the full catalogue

Everything below is in `Dalamud.Plugin.Services` and injectable with `[PluginService]`.

### Core

| Service | Purpose |
| --- | --- |
| `IDalamudPluginInterface` | Your handle on Dalamud itself (in `Dalamud.Plugin`). |
| `IFramework` | The game's main loop. `Update` event, `RunOnFrameworkThread`, `RunOnTick`, delta time. |
| `IPluginLog` | Logging. `Verbose`/`Debug`/`Information`/`Warning`/`Error`/`Fatal`. |
| `ICommandManager` | Register and remove slash commands. |
| `IConsole` | Register Dalamud console commands and variables. |
| `IGameLifecycle` | Cancellation tokens for logout, game shutdown, plugin unload. |
| `IGameInteropProvider` | **Hook creation.** |
| `ISigScanner` | Raw signature scanning in the game module. |
| `IReliableFileStorage` | Atomic, crash-safe file writes. |

### Player and world state

| Service | Purpose |
| --- | --- |
| `IClientState` | Login state, `TerritoryType`, `MapId`, `LocalPlayer`, `Login`/`Logout`/`TerritoryChanged`/`CfPop` events. |
| `IPlayerState` | Typed wrapper over local player info — job, level, GC, etc. Newer and generally nicer than digging through `LocalPlayer`. |
| `ICondition` | Player condition flags (in combat, mounted, in duty, occupied, …). Essential for gating behaviour. |
| `IObjectTable` | Every spawned game object. Enumerate for NPCs, players, gathering nodes. |
| `IPartyList` / `IBuddyList` | Party/alliance members; chocobo and trust companions. |
| `ITargetManager` | Current, focus, soft, and mouseover targets. |
| `IDutyState` | Duty started/completed/wiped events. |
| `IFateTable` | Active FATEs. |
| `IAetheryteList` | Aetherytes in the teleport window. |
| `IJobGauges` | Job gauge data as typed structs. |
| `IUnlockState` | What the character has unlocked. |
| `IGameInventory` | Inventory contents and change events. |
| `IMarketBoard` | Market board events. |

### Data

| Service | Purpose |
| --- | --- |
| `IDataManager` | Lumina access — `GetExcelSheet<T>()`, `GameData`, `GetFile<T>()`. |
| `IGameConfig` | Read/write the game's own settings. |
| `ISeStringEvaluator` | Resolve SeString macros to displayable text. |

### UI

| Service | Purpose |
| --- | --- |
| `IGameGui` | Find addons by name, world↔screen coordinate conversion, hovered item/action. |
| `IChatGui` | Print to chat; `ChatMessage` event to observe it. |
| `IToastGui` | Native toasts (normal, quest, error). |
| `IFlyTextGui` | Native flying combat text. |
| `IDtrBar` | Entries in the server-info bar (top-right). Cheap, unobtrusive status display. |
| `INotificationManager` | Dalamud's own ImGui notification popups. |
| `IPartyFinderGui` | Party Finder listing events. |
| `IContextMenu` | Add entries to the game's right-click menus. |
| `INamePlateGui` | Modify nameplate rendering data. |
| `IAddonLifecycle` | Addon setup/draw/update/refresh/finalize events. |
| `IAddonEventManager` | Attach custom input events to native UI nodes. |
| `IAgentLifecycle` | Agent (UI backend) lifecycle events. |
| `ITitleScreenMenu` | Add entries to the title screen menu. |

### Textures and input

| Service | Purpose |
| --- | --- |
| `ITextureProvider` | Load textures for ImGui — from file, from game path, from game icon ID. |
| `ITextureReadbackProvider` | Read texture pixel data back. |
| `ITextureSubstitutionProvider` | Replace game texture data. |
| `IKeyState` | Keyboard state. |
| `IGamepadState` | Gamepad state. |

`ISelfTestRegistry` lets you register steps into Dalamud's self-test harness; `IDalamudService` is just a
marker interface.

Only request the services you actually use — the template's list is a starting point, not a requirement.

---

## 9. Configuration

Implement `IPluginConfiguration`:

```csharp
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
```

Load with the null-coalescing pattern, which handles both first run and a failed deserialize:

```csharp
Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
```

It serializes to JSON at
`%AppData%\XIVLauncher\pluginConfigs\<InternalName>.json`. Larger data belongs in
`PluginInterface.ConfigDirectory` instead — use `IReliableFileStorage` to write it so a crash mid-write
doesn't corrupt the file.

**On `Version`:** it exists so you can migrate. When you change the shape of your config in a breaking
way, bump it and migrate on load:

```csharp
if (Configuration.Version == 0)
{
    Configuration.NewField = DeriveFrom(Configuration.OldField);
    Configuration.Version = 1;
    Configuration.Save();
}
```

**Don't save every frame.** `Save()` does synchronous file I/O. The template calls it in the checkbox
`if` block, which only fires on change — that's the right shape. Never call it unconditionally in
`Draw()`.

---

## 10. UI with ImGui and the Windowing API

Dalamud renders [Dear ImGui](https://github.com/ocornut/imgui) over the game. In API 15 the bindings are
`Dalamud.Bindings.ImGui` (note: *not* `ImGuiNET` — that's the older namespace you'll see in outdated
tutorials).

ImGui is **immediate mode**: your `Draw()` runs every single frame and rebuilds the UI from scratch.
There is no retained widget tree. Consequences: `Draw()` must be fast, must not block, and must not
allocate heavily. No file I/O, no network calls, no `.Wait()`.

### The Windowing API

The docs explicitly recommend `WindowSystem` over hand-rolled `ImGui.Begin`/`End` — it gives users
consistent behaviour and integrates with Dalamud's window management.

```csharp
public readonly WindowSystem WindowSystem = new("RegionsOfXIV");
// ...
WindowSystem.AddWindow(ConfigWindow);
PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
```

Subclass `Window`:

```csharp
public class MainWindow : Window, IDisposable
{
    public MainWindow(Plugin plugin)
        : base("My Amazing Window##With a hidden ID",
               ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void PreDraw() { /* mutate Flags here, not in Draw */ }
    public override void Draw()    { /* your UI */ }
    public void Dispose()          { }
}
```

Overridable members: `PreDraw`, `Draw`, `PostDraw`, `OnOpen`, `OnClose`, `DrawConditions`. Useful
properties: `IsOpen`, `Toggle()`, `Size`, `SizeCondition`, `SizeConstraints`, `Flags`,
`RespectCloseHotkey`, `TitleBarButtons`.

**Flags must be set in `PreDraw()`**, not `Draw()` — by the time `Draw()` runs, `ImGui.Begin` has already
been called with them. [ConfigWindow.cs:28-39](SamplePlugin/Windows/ConfigWindow.cs#L28-L39) shows this.

### Window IDs: `##` vs `###`

```csharp
"My Amazing Window##With a hidden ID"     // ID = whole string; visible title = "My Amazing Window"
"A Wonderful Window###With a constant ID" // ID = "###With a constant ID" only
```

Use `###` whenever the visible title changes at runtime (`$"{count} regions###RegionsMain"`). With `##`,
a changing title changes the ImGui ID, and the window loses its saved position and size every time.

### ImRaii — use it

ImGui's C API pairs every `Begin*` with an `End*`, and skipping the `End` on an early return corrupts
the ImGui state stack for *every other plugin*. `ImRaii` ties the cleanup to a C# scope:

```csharp
using (var child = ImRaii.Child("Scroller", Vector2.Zero, true))
{
    if (child.Success)   // check before drawing into it
    {
        ImGui.Text("...");
    }
}
```

The same applies to `ImRaii.Table`, `ImRaii.TabBar`, `ImRaii.PushColor`, `ImRaii.PushIndent`,
`ImRaii.Disabled`, and friends. Prefer `ImRaii` universally; it makes early `return`s safe.

### Scaling

Users run at HUD scales from 50% to 200%, and Dalamud has its own global font scale. Multiply every
hardcoded pixel value by `ImGuiHelpers.GlobalScale`:

```csharp
ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
ImGui.Image(icon.Handle, new Vector2(28, 28) * ImGuiHelpers.GlobalScale);
ImGuiHelpers.ScaledDummy(20.0f);   // already scaled
```

This is the single most common polish bug in community plugins. [MainWindow.cs:89-96](SamplePlugin/Windows/MainWindow.cs#L89-L96)
gets it right.

### Fonts and glyphs

Default ImGui fonts don't include Japanese/Chinese/Korean glyphs, and game item names frequently contain
them. `PluginInterface.UiBuilder` exposes font handles including
`IconFontHandle` (FontAwesome) and the default game font — push the right one rather than shipping your
own font file.

---

## 11. Textures and icons

`ITextureProvider` is the only correct way to get textures into ImGui. It handles the DirectX
interop, caching, and lifetime.

```csharp
// From a file on disk, next to your DLL
var path = Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, "goat.png");
var tex = TextureProvider.GetFromFile(path).GetWrapOrDefault();
if (tex != null)
    ImGui.Image(tex.Handle, tex.Size);

// From a game icon ID (job icons start at 62100)
var icon = TextureProvider.GetFromGameIcon(new GameIconLookup(62100 + classJobId)).GetWrapOrEmpty();
ImGui.Image(icon.Handle, new Vector2(28, 28) * ImGuiHelpers.GlobalScale);

// From a game path
var t = TextureProvider.GetFromGame("ui/icon/062000/062101_hr1.tex").GetWrapOrDefault();
```

Key points:

- Loading is **asynchronous**. `GetWrapOrDefault()` returns `null` until the texture is ready;
  `GetWrapOrEmpty()` returns a blank placeholder. Handle both — don't assume the first frame has it.
- **Do not dispose or cache the wrap** across frames. Call the getter each frame; the provider caches
  internally.
- `GameIconLookup` supports HD and language variants — pass those rather than building `_hr1` paths
  yourself.

The docs note you'd normally embed assets as manifest resources rather than loose files; the template
uses a loose file for clarity.

---

## 12. Game data with Lumina

The docs are explicit: **prefer Lumina over XIVAPI**. It reads the local game files, so it's fast, needs
no network, and is always current with the installed patch.

```csharp
using Lumina.Excel.Sheets;

var territoryId = ClientState.TerritoryType;
if (DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
{
    var name = row.PlaceName.Value.Name.ToString();
}
```

Notes:

- `GetExcelSheet<T>()` is cheap to call but **cache the sheet reference**, not per-frame lookups, if
  you're iterating.
- Sheets are typed and generated: `Item`, `Action`, `TerritoryType`, `PlaceName`, `ClassJob`, `Map`,
  `ContentFinderCondition`, `Quest`, `ENpcResident`, and hundreds more.
- Row references (`row.PlaceName`) are lazy links. Check `.IsValid` before `.Value` — see
  [MainWindow.cs:80](SamplePlugin/Windows/MainWindow.cs#L80).
- Use `TryGetRow` over indexing. Row IDs are not dense and missing rows are common.
- Text fields are `ReadOnlySeString`; call `.ToString()` for display text or `.ToMacroString()` to see
  the raw payloads.
- Pass a `ClientLanguage` to `GetExcelSheet<T>(lang)` for a specific language, or omit it to follow the
  client.

For territory/region work specifically, the useful chain is
`TerritoryType` → `PlaceName` / `PlaceNameRegion` / `PlaceNameZone` → `Map`, plus
`ContentFinderCondition` for instanced content.

---

## 13. SeString

FFXIV strings are not plain UTF-8. They're a custom format (`Utf8String` internally, universally called
**SeString** by the community) that interleaves text with binary payloads — colours, item links, player
links, auto-translate entries, and macros for conditional and localized content.

Wire format of a payload:

```
0x02 | macro code (1 byte) | length expression | macro data | 0x03
```

Expression kinds you'll encounter: integer (encoded value-minus-one so no null bytes appear),
placeholder (contextual values like time), binary (comparisons), parameter (local/global variables), and
string (a nested SeString). There are 50+ macro types, from `Bold` and `ColorType` up to localized noun
declension with correct grammar per language.

### Which API to use

**Use Lumina's `ReadOnlySeString` and `SeStringBuilder`.** The docs state plainly that
`Lumina.Text.SeString` is an old implementation and should no longer be used. Dalamud's own older
`SeString` types are likewise legacy.

```csharp
var s = new SeStringBuilder()
    .PushColorType(500)
    .Append("Eorzea")
    .PopColorType()
    .Append(" — you are here")
    .ToReadOnlySeString();

ChatGui.Print(s);
```

Use `ISeStringEvaluator` to resolve macros at runtime into displayable output while preserving
formatting payloads. And when debugging, `.ToMacroString()` shows you what the payloads actually are —
far more useful than `.ToString()`, which silently drops them.

**Never string-match on game text.** It differs per language and often contains payloads that break
naive comparisons. Compare row IDs instead.

---

## 14. Commands, chat, and the DTR bar

### Slash commands

```csharp
CommandManager.AddHandler("/regions", new CommandInfo(OnCommand)
{
    HelpMessage = "Opens the Regions of XIV window.",
    ShowInHelp  = true,
});

private void OnCommand(string command, string args) => MainWindow.Toggle();
```

- Always `RemoveHandler` in `Dispose()`.
- `HelpMessage` shows in `/xlhelp`.
- Argument parsing is entirely yours — `args` is the raw remainder of the line.
- Pick a name unlikely to collide with the game's or another plugin's. The `p` prefix convention
  (`/pmycommand`) exists for this reason.
- Every chat command also works in the Dalamud console without the slash.

### Chat

```csharp
ChatGui.Print("plain message");
ChatGui.PrintError("something went wrong");
ChatGui.Print(new XivChatEntry { Type = XivChatType.Debug, Message = seString });
```

Subscribe to `ChatGui.ChatMessage` to observe messages. Note the restriction rules: **sending** chat or
otherwise talking to the game server automatically, without direct user action, is prohibited.

### The DTR bar

`IDtrBar` puts a small entry in the server-info bar next to the clock. It's the least intrusive way to
show persistent status and is well-suited to a zone/region plugin:

```csharp
var entry = DtrBar.Get("RegionsOfXIV");
entry.Text = currentRegionName;
entry.OnClick = () => MainWindow.Toggle();
// dispose the entry in Dispose()
```

---

## 15. Interacting with the game: the three tiers

The docs prescribe a strict preference order. Follow it — reviewers do check.

**Tier 1 — Dalamud APIs.** Stable, safe, documented, and survive game patches. If a service covers what
you need, use it. There is no reason to hook `TerritoryChanged` yourself when `IClientState` raises it.

**Tier 2 — FFXIVClientStructs.** When Dalamud has no wrapper, CS gives you the game's own structs and
member functions as C# types. Requires `unsafe` and pointer discipline:

```csharp
unsafe
{
    var playerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
    if (playerState is not null && playerState->IsMentor())
    {
        // ...
    }
}
```

Always null-check `Instance()`. It returns null before the relevant subsystem initializes — that means
at the title screen, during zoning, and during logout.

**Tier 3 — raw memory and signature-scanned functions.** For genuinely novel work on undocumented
structures. Least stable; breaks on patches. If you end up here, **contribute your findings back to
FFXIVClientStructs** — the docs ask for this explicitly, and it's how the ecosystem stays maintainable.

Related rule: **don't ship a custom fork of ClientStructs to users.** If CS is missing or wrong about
something, contact the Dalamud maintainers so it can be fixed upstream. For local testing you can
reference your own CS project with `Private="true"` and initialize the resolver manually in your
constructor — but clean the project and revert before release, or you'll ship a stale DLL.

---

## 16. Hooking game functions

A hook redirects a game function to *your* code. Your detour runs instead of the original, and you
choose whether, when, and with what arguments to call the original.

`IGameInteropProvider` is the service. Three ways in:

### From a ClientStructs address (preferred)

```csharp
private readonly Hook<AgentMap.Delegates.OpenMap> openMapHook;

public unsafe MyThing(IGameInteropProvider interop)
{
    openMapHook = interop.HookFromAddress<AgentMap.Delegates.OpenMap>(
        AgentMap.Addresses.OpenMap.Value, OpenMapDetour);
    openMapHook.Enable();
}
```

CS resolves the address for you, so patches that shift offsets don't break you.

### From a signature

```csharp
public class MySiggedHook
{
    [Signature("48 89 5C 24 ?? 57 48 83 EC ??", DetourName = nameof(Detour))]
    private readonly Hook<MyDelegate>? hook = null;

    public MySiggedHook(IGameInteropProvider interop)
    {
        interop.InitializeFromAttributes(this);
        hook?.Enable();
    }

    private nint Detour(nint a1, nint a2)
    {
        // observe / mutate
        return hook!.Original(a1, a2);
    }
}
```

`InitializeFromAttributes` reflects over the object, resolves every `[Signature]` member, and assigns
them. **A signature that fails to resolve yields `null`** — check for it rather than assuming success,
because that's exactly what happens on the first day of a new patch.

You can also use `interop.HookFromSignature<T>(sig, detour)` directly, or `HookFromAddress` with an
address you obtained however you like (e.g. from `ISigScanner`).

### Rules

- **`Enable()` to activate, `Dispose()` to remove.** Dispose all hooks in your `Dispose()`.
- **An exception in a detour crashes the game.** Wrap the body in try/catch, log the exception, and still
  call `Original()`. Never let one escape.
- **Detours are blocking.** The game is stopped until you return. Do the minimum; queue real work
  elsewhere.
- **Detour signature must match exactly** — calling convention, parameter types, return type. A mismatch
  corrupts the stack.
- **You are not the only hooker.** Multiple plugins hook the same functions, and hooks run in *inverse
  load order* (last loaded runs first). Mutating arguments affects every plugin downstream of you — avoid
  it unless it's the whole point of your plugin.
- **Hooking is highly invasive.** Before writing one, check whether a Dalamud service already gives you
  the event. `IAddonLifecycle`, `IGameInventory`, `IDutyState`, and `IClientState` exist precisely so you
  don't have to hook.

---

## 17. Calling game functions

The inverse of hooking: use the client as a library.

**Via ClientStructs** — always try this first:

```csharp
unsafe
{
    var uiState = UIState.Instance();
    var isUnlocked = uiState->IsUnlockLinkUnlocked(linkId);
}
```

**Via a delegate and `[Signature]`:**

```csharp
private delegate byte IsQuestCompleteDelegate(ushort questId);

[Signature("E8 ?? ?? ?? ?? 84 C0 74 ?? 48 8B CB")]
private readonly IsQuestCompleteDelegate? isQuestComplete = null;

public bool IsQuestComplete(ushort questId)
{
    if (isQuestComplete is null)
        throw new InvalidOperationException("IsQuestComplete signature failed to resolve.");
    return isQuestComplete(questId) != 0;
}
```

The pattern the docs recommend: declare the delegate, tag it with `[Signature]`, resolve with
`InitializeFromAttributes()`, then **wrap it in a managed method** that null-checks and converts return
values. Callers get a clean API and one clear failure point.

For projects with many signatures, a dedicated "Resolver" class holding all address resolution (using
`ISigScanner` directly) keeps things tidy and makes patch-day breakage easy to locate.

**Before calling anything:** be certain you're allowed to. Calling game functions that send packets to
the server without direct user input is the fastest route to rejection — see
[restrictions](#22-plugin-restrictions--read-before-you-build).

---

## 18. Native UI: AddonLifecycle and AddonEventManager

An **Addon** is a window in the game's own UI (`ChatLog`, `AreaMap`, `FieldMarker`, …). The name is
inherited from WoW's terminology.

### IAddonLifecycle

The purpose, per the docs, is to make it easy to modify native UI or read addon data *without* hooking
every addon yourself.

```csharp
AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "AreaMap", OnAreaMapSetup);

private void OnAreaMapSetup(AddonEvent type, AddonArgs args)
{
    if (args is AddonSetupArgs setupArgs)
    {
        var values = setupArgs.AtkValueSpan;
        // ...
    }
}
```

- Events come in `Pre`/`Post` pairs: `PreSetup`/`PostSetup`, `PreDraw`/`PostDraw`,
  `PreUpdate`/`PostUpdate`, `PreRefresh`/`PostRefresh`, `PreFinalize`.
- **There is no `PostFinalize`** — the addon's memory is already freed by then.
- Overloads accept a single addon name, a collection of names, or none at all (meaning *all* addons).
  Listening to all addons on `PreDraw` is a per-frame firehose; scope it.
- `UnregisterListener` in `Dispose()`. You can unregister by handler reference alone.

### IAddonEventManager

Attaches custom input events to native UI nodes, with Dalamud tracking them so they're cleaned up
automatically if your plugin unloads or the addon closes.

```csharp
targetNode->NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.RespondToMouse | NodeFlags.HasCollision;
var handle = AddonEventManager.AddEvent(addonPtr, nodePtr, AddonEventType.MouseClick, OnClick);
```

Two things will bite you:

- **Register a `Node`, never a `Component`.** Passing a Component's address **crashes the game**.
- Nodes usually need the flags above set before they'll emit events at all.

`AddEvent` returns an `IAddonEventHandle` — keep it and call `RemoveEvent` for persistent addons.
Non-persistent addons clean up on close. `SetCursor()` / `ResetCursor()` signal interactivity to the
user; use them so your clickable regions don't feel invisible.

The natural pairing is `IAddonLifecycle` `PostSetup` → attach events via `IAddonEventManager`.

---

## 19. Reverse engineering

Only necessary for tier-3 work. Two complementary approaches:

**Static analysis** — disassemble `ffxiv_dx11.exe`:
- **IDA Pro (Hex-Rays)** and **Ghidra** are the community standards; **Binary Ninja** also works.
- Load FFXIVClientStructs' exported data files to import known struct and function names — it saves
  enormous time.

**Dynamic analysis** — inspect the running process:
- **Cheat Engine** for value scanning, **x64dbg** for breakpoints and stepping, **ReClass.NET** for
  mapping struct layouts live.

**Offsets vs signatures.** Every function lives at an offset, but *every game patch changes every
offset*. A **signature** is a byte pattern (with `??` wildcards over relative addresses and volatile
bytes) that identifies the function by its actual code. Signatures survive patches unless the underlying
code itself changes — which is why every example above uses them.

What you do with a resolved address is either **hook** it or **call** it — sections 16 and 17.

Contribute what you find back to [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs). It is
the shared map, and it only improves if people who explore add to it.

---

## 20. Versions, API levels, and release channels

### API levels

The **API level** increments on every breaking change to Dalamud's API. Since version 9 it matches the
major version: Dalamud 15 → API 15. **Plugins compiled against an older API level will not load on a
newer Dalamud.** Every API bump requires a recompile and usually some migration work.

Current at time of writing: **API 15**, on **.NET 10**, supporting **Patch 7.5**. That is what this repo
targets via `Dalamud.NET.Sdk/15.0.0`.

### Channels

Users choose a channel in XIVLauncher's `/xldev` menu:

| Channel | What it is |
| --- | --- |
| **Release** | Default. Stable tagged builds from `master`. |
| **Canary** | Newly tagged releases, rolled out to a small subset first to catch problems. |
| **Staging** (`stg`) | Latest commits to `master`, ahead of any tag. For developers and testers. |

Develop against **staging** when a new API level is approaching, so you're ready on release day; test
against **release** for what your users actually run.

### Surviving a patch

Game patch days break signatures. API bumps break compiles. Practical habits:

- Prefer Dalamud services over CS, and CS over your own signatures. Fewer things to fix.
- Null-check every resolved signature and log clearly when one fails — "signature X failed to resolve"
  in the log beats a silent no-op.
- Use `ApplicableVersion` if your plugin genuinely can't work on a newer game version.
- Watch the `#plugin-dev` channels on the [Discord](https://discord.gg/holdshift) around patches.

---

## 21. Technical considerations and performance

From the technical-considerations page, plus practice:

**Performance.** Minimize your impact on the game. The **Plugin Statistics** window (via `/xldev`) shows
per-plugin draw and framework time — check yours there, not by feel. `Draw()` and `Framework.Update`
both run on the game's critical path.

**Threading.** Game state is not thread-safe. Anything touching game memory, CS, or native UI must run
on the framework thread — use `Framework.RunOnFrameworkThread(...)` or `RunOnTick(...)` to marshal back
from a background task. Conversely, do heavy work (parsing, HTTP, disk) *off* the framework thread.

**Windowing.** Use the Dalamud Windowing API for settings and utility windows so users get consistent
behaviour.

**Data.** Lumina over XIVAPI — local, fast, always in sync with the installed client.

**Backend servers.** If your plugin talks to a server you run, the docs impose real requirements:

- Send **the minimum data necessary** to do the job.
- **Hash player information client-side**, so a breach doesn't expose identities.
- Any telemetry beyond that requires **explicit opt-in**.
- Analytics should use **pseudo-random or no identifiers**.
- **HTTPS/TLS with a trusted certificate** (Let's Encrypt is fine), connecting by **DNS hostname, not IP**.
- **Never expose a list of your plugin's users.**

Recommended on top of that: let users point at a custom backend, open-source the server, support IPv6,
implement retry and version checking, and surface server status in the UI. The Plugin Approval Committee
judges data appropriateness case by case, weighing necessity, intent, and how clearly it's communicated
to users.

---

## 22. Plugin restrictions — read before you build

This section decides whether your plugin can exist. Read it *before* writing code, not after.

Your plugin must **not**:

- **Automate server interaction.** No polling or requests without direct user interaction.
- **Bypass game mechanics.** Stay within what a player can normally do.
- **Augment combat**, except by displaying information already available to you and your own
  party/alliance.
- **Interfere with monetization** — no access to paid Mog Station items.
- **Enable cheating** — no parsing, DPS meters, raid logging, or surfacing non-standard player info.
- **Collect player data** — no character account IDs other than your own.
- **Hard-depend on a rule-breaking plugin.**
- **Facilitate out-of-spec play.**
- **Provide any PvP advantage.**

Explicitly banned plugin categories:

emote/expression looping · cutscene skipping · dialog automation · automated crafting · auto-loot rolling ·
friend login alerts · telegraph-less AOE markers · camera zoom adjustment · damage parsers · Fantasia
bypasses · additional XIVCombo features · AOE recolouring · anything PvP-enhancing

**If you are unsure, ask in the [Dalamud Discord](https://discord.gg/holdshift) before you build.** The
approval committee says outright that they would rather collaborate on a design early than reject a
finished plugin. Fifteen minutes in Discord can save you a month.

---

## 23. The AI usage policy

Both this repo's README and the docs point at [dalamud.dev/plugin-publishing/ai-policy](https://dalamud.dev/plugin-publishing/ai-policy).
It is enforced, so know it.

**The core requirement:** you must **understand, test, and be able to explain your code**. "I'm not sure,
the AI did it" is not an acceptable answer to a reviewer's question.

**Disclosure is mandatory** for anything beyond basic autocomplete, using these levels:

| Level | Meaning |
| --- | --- |
| **Assist** | AI handles specific tasks; you lead. |
| **Pair** | Roughly equal collaboration. |
| **Copilot** | AI writes; you plan and review. |
| **Auto** | AI works autonomously with minimal direction. |

**Consequences:**

- Entirely AI-generated plugins are **rejected automatically**.
- Undisclosed AI use in a demonstrably AI-written submission → **ban**.
- A second offence → **permanent ban**.
- Submissions with AI-flavoured mistakes but clear human intent get constructive feedback, not rejection.

**Assets and translations:** AI-generated icons and images must be disclosed to users — the community
states it prefers a crude MS Paint icon over an AI-generated one. AI translations are acceptable as
placeholders but should be reviewed by native speakers where possible.

---

## 24. Publishing to the official repository

### Before you submit

- Metadata complete and correct (section 6).
- Technical considerations addressed (section 21).
- Restrictions satisfied (section 22).
- AI policy read, and your AI use disclosed **in the PR** (section 23).
- `packages.lock.json` committed and current.
- Repository **public and open-source** — this is non-negotiable.

### How it works: Plogon

Dalamud's CI system is called **Plogon**. It guarantees that the binary on a user's machine was built
from **an exact commit hash in a publicly available Git repository**. The build runs in the cloud
**without internet access** (hence the lockfile) and produces a diff that reviewers read. This is what
makes the ecosystem auditable, and it's why you submit a commit hash rather than a binary.

### Tracks

| Track | For |
| --- | --- |
| **testing** (primary track: `live`) | New plugins and experimental versions. |
| **stable** | Public, bug-free releases you're prepared to support. |

**All new plugins start in testing.**

### The submission

Open a PR against [goatcorp/DalamudPluginsD17](https://github.com/goatcorp/DalamudPluginsD17), **one
plugin per PR**, from a dedicated branch. For a new plugin the path is under `testing/live/`:

```
RegionsOfXIV/
├── manifest.toml
└── images/
    ├── icon.png
    ├── image1.png   [optional]
    └── image2.png   [optional]
```

`manifest.toml`:

```toml
[plugin]
repository = "https://github.com/<you>/RegionsOfXIV.git"
commit = "765d9bb434ac99a27e9a3f2ba0a555b55fe6269d"
owners = ["<your-github-username>"]
project_path = "RegionsOfXIV"
changelog = "Initial release."
```

**Images:** the icon must be **1:1**, between **64×64 and 512×512**. Up to five optional marketing images
(`image1.png`–`image5.png`).

### Review

- **Six volunteer reviewers**, all plugin developers themselves, unpaid.
- New plugins need **4 yes votes**; **any member can veto**.
- Expect **over a week** in the queue. Be patient and be responsive.
- They review code, verify functionality, and check that no personal data is uploaded.
- **Updates need only one approval** — the slow part is the first submission.

### Afterwards

- **To update:** new PR with an updated `commit` hash.
- **To promote testing → stable:** move the manifest to the stable directory; no version bump needed.
- **Ban system:** `bannedPlugin.json` in DalamudAssets can globally disable a plugin version. It is a
  *safety* tool, not a moderation tool. Recover by releasing a higher `AssemblyVersion`. The Dalamud team
  can ban without maintainer consent for game-breaking or critical safety issues, and will normally try
  to notify first.

---

## 25. Custom repositories

You can host your own repo, but the docs are blunt: **the Dalamud project offers minimal support for
custom repositories**, including setting one up. Prefer the official repo. Users are also right to be
wary of third-party repos, since none of the D17 auditing applies.

A custom repo is just a publicly reachable URL serving a JSON array of plugin entries over HTTP GET. No
auth.

Required fields per entry:

`Author` · `Name` · `Description` · `Punchline` · `InternalName` · `AssemblyVersion` · `RepoUrl` ·
`ApplicableVersion` · `DalamudApiLevel` · `DownloadLinkInstall` · `DownloadLinkUpdate` · `LastUpdate`
(Unix timestamp)

Also supported: `IsHide`, `DownloadCount`, `IconUrl`, `ImageUrls`.

For beta builds: `IsTestingExclusive`, `TestingAssemblyVersion`, `TestingDalamudApiLevel`,
`DownloadLinkTesting`, `TestingChangelog`.

The download links point at ZIPs — which is exactly the shape of your `bin\x64\Release\<Plugin>\` output
directory. A GitHub Actions release workflow that zips it and updates the JSON is the usual setup.

---

## 26. Debugging toolbox

| Tool | How | What it's for |
| --- | --- | --- |
| Log window | `/xllog` | Your `IPluginLog` output, plus Dalamud's. First stop for everything. |
| Dev menu | `/xldev` | Gateway to all the tools below. |
| Plugin Statistics | `/xldev` | Per-plugin draw/update timing. Use it before claiming you're fast. |
| Data window | `/xldata` | Live inspection of object table, conditions, addons, gauges, and more. |
| Settings | `/xlsettings` | Dev plugin locations, log level, channel. |
| Plugin installer | `/xlplugins` | Enable/disable/reload your dev plugin. |

Log with intent:

```csharp
Log.Verbose("per-frame noise");
Log.Debug("dev-only detail");
Log.Information($"===Loaded {PluginInterface.Manifest.Name}===");
Log.Warning("something unexpected but survivable");
Log.Error(ex, "hook detour threw");
```

Log entries are prefixed with your InternalName, so `[RegionsOfXIV]` filters cleanly in `/xllog`.

**Attaching a debugger:** you can attach Visual Studio or Rider to `ffxiv_dx11.exe` and hit breakpoints
in managed code. Be aware that pausing the process pauses the game — the connection will time out if you
sit on a breakpoint too long while logged in. For hot paths, logging often beats breakpoints.

---

## 27. Pitfalls checklist

Ordered roughly by how often they bite people.

- [ ] Every `+=` has a `-=`, every `AddHandler` a `RemoveHandler`, every `RegisterListener` an
      `UnregisterListener`, every `Hook` a `Dispose`. Verify by reading `Dispose()` against your
      constructor line by line.
- [ ] Hardcoded pixel values multiplied by `ImGuiHelpers.GlobalScale`.
- [ ] `###` (not `##`) for any window whose visible title changes.
- [ ] `ImRaii` for every ImGui begin/end pair, so early returns are safe.
- [ ] No file I/O, network calls, or blocking waits inside `Draw()`.
- [ ] `Configuration.Save()` only on actual change, never every frame.
- [ ] `TryGetRow` and `.IsValid` checks on all Lumina lookups.
- [ ] Null-check every `Instance()` from ClientStructs — it's null at the title screen and during zoning.
- [ ] Null-check every `[Signature]`-resolved delegate and hook; log loudly on failure.
- [ ] Try/catch inside every hook detour, and still call `Original()`.
- [ ] Game-touching work marshalled onto the framework thread.
- [ ] `AssemblyName` / InternalName chosen deliberately — it is permanent.
- [ ] `<Version>` bumped for every release.
- [ ] `packages.lock.json` committed and regenerated after package changes.
- [ ] Manifest has no hand-written `InternalName`, `AssemblyVersion`, or `DalamudApiLevel`.
- [ ] No `Dalamud`/`Lumina`/`ClientStructs`/`ImGui` NuGet packages added manually.
- [ ] Tested with a hot-reload, not just a fresh game launch — reload is what exposes leaks.
- [ ] Tested logged out and at the title screen, not only in-game.
- [ ] Restrictions reviewed; Discord consulted if there was any doubt.
- [ ] AI use disclosed in the submission PR.

---

## 28. Reference links

**Documentation**
- [dalamud.dev](https://dalamud.dev) — official developer docs
- [API reference](https://dalamud.dev/api/) — generated Dalamud API docs
- [Plugin Development](https://dalamud.dev/category/plugin-development)
- [Getting Started](https://dalamud.dev/plugin-development/getting-started)
- [Project Layout](https://dalamud.dev/plugin-development/project-layout)
- [Plugin Metadata](https://dalamud.dev/plugin-development/plugin-metadata)
- [Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations)
- [SeString](https://dalamud.dev/plugin-development/sestring)
- [Glossary](https://dalamud.dev/plugin-development/glossary) — internal names for player-facing systems
- [Versions & Channels](https://dalamud.dev/versions/)

**Interaction and how-tos**
- [Interacting With The Game](https://dalamud.dev/plugin-development/interaction/)
- [Expanding On Game Events (hooks)](https://dalamud.dev/plugin-development/interaction/expanding-game-events/)
- [Calling The Game's Code](https://dalamud.dev/plugin-development/interaction/calling-game-code/)
- [AddonLifecycle](https://dalamud.dev/plugin-development/how-tos/AddonLifecycle)
- [AddonEventManager](https://dalamud.dev/plugin-development/how-tos/AddonEventManager)
- [Migrating to Dalamud.NET.Sdk](https://dalamud.dev/plugin-development/how-tos/v12-SDK-migration)
- [Reverse Engineering](https://dalamud.dev/plugin-development/reverse-engineering/)
- [Using Custom ClientStructs](https://dalamud.dev/plugin-development/reverse-engineering/using-custom-cs)

**Publishing**
- [Publishing overview](https://dalamud.dev/plugin-publishing/)
- [Plugin Restrictions](https://dalamud.dev/plugin-publishing/restrictions)
- [Approval Process](https://dalamud.dev/plugin-publishing/approval-process)
- [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy)
- [Submission Process](https://dalamud.dev/plugin-publishing/submission)
- [Advanced Publishing](https://dalamud.dev/plugin-publishing/advanced)
- [Custom Repositories](https://dalamud.dev/plugin-publishing/custom-repositories)
- [Code of Conduct](https://dalamud.dev/code-of-conduct)

**Repositories**
- [goatcorp/Dalamud](https://github.com/goatcorp/Dalamud)
- [goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin) — this template
- [goatcorp/DalamudPluginsD17](https://github.com/goatcorp/DalamudPluginsD17) — the plugin repo
- [aers/FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs)
- [NotAdam/Lumina](https://github.com/NotAdam/Lumina)
- [goatcorp/FFXIVQuickLauncher](https://github.com/goatcorp/FFXIVQuickLauncher) — XIVLauncher

**Community**
- [Discord](https://discord.gg/holdshift) — `#plugin-dev` is where the answers are
