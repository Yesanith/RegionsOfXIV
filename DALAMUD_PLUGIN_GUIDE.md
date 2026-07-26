# Dalamud Plugin Development — Complete Guide

> Source of truth: everything in this document is distilled from <https://dalamud.dev>
> (the full docs tree at `goatcorp/dalamud-docs`), cross-checked against the actual
> interface sources in `goatcorp/Dalamud@master` and `goatcorp/SamplePlugin@master`.
> Current as of **Dalamud API 15 / .NET 10 / Patch 7.5**.
>
> This is written as a working reference for building FFXIV plugins from zero to
> published, covering every documented path of interacting with the game.

---

## Table of Contents

1. [The stack, in one page](#1-the-stack-in-one-page)
2. [Environment setup](#2-environment-setup)
3. [Project layout & the SDK](#3-project-layout--the-sdk)
4. [The plugin entrypoint & lifecycle](#4-the-plugin-entrypoint--lifecycle)
5. [Services: the injection system](#5-services-the-injection-system)
6. [Complete service catalogue (API 15)](#6-complete-service-catalogue-api-15)
7. [The three tiers of game interaction](#7-the-three-tiers-of-game-interaction)
8. [Tier 1 — Dalamud APIs](#8-tier-1--dalamud-apis)
9. [Tier 2 — FFXIVClientStructs](#9-tier-2--ffxivclientstructs)
10. [Tier 3 — Signatures, delegates, hooks](#10-tier-3--signatures-delegates-hooks)
11. [Native UI: AddonLifecycle & AddonEventManager](#11-native-ui-addonlifecycle--addoneventmanager)
12. [Your own UI: ImGui, WindowSystem, ImRaii](#12-your-own-ui-imgui-windowsystem-imraii)
13. [Game data: Lumina & Excel sheets](#13-game-data-lumina--excel-sheets)
14. [SeString: the game's string format](#14-sestring-the-games-string-format)
15. [Configuration & file storage](#15-configuration--file-storage)
16. [Plugin-to-plugin IPC](#16-plugin-to-plugin-ipc)
17. [Threading rules](#17-threading-rules)
18. [Reverse engineering workflow](#18-reverse-engineering-workflow)
19. [Debugging, logging, hot reload](#19-debugging-logging-hot-reload)
20. [Plugin manifest & metadata](#20-plugin-manifest--metadata)
21. [Publishing to the official repo (D17)](#21-publishing-to-the-official-repo-d17)
22. [Custom repositories](#22-custom-repositories)
23. [Rules, restrictions & the AI policy](#23-rules-restrictions--the-ai-policy)
24. [Versions, API levels & migration](#24-versions-api-levels--migration)
25. [Recipes: common plugin patterns](#25-recipes-common-plugin-patterns)
26. [Glossary & internal names](#26-glossary--internal-names)
27. [Reference links](#27-reference-links)

---

## 1. The stack, in one page

| Piece | What it is |
| --- | --- |
| **XIVLauncher** (`goatcorp/FFXIVQuickLauncher`) | Custom launcher. Bootstraps and injects Dalamud into `ffxiv_dx11.exe`. Also disables the game's ACL protections so injection is possible. |
| **Dalamud** (`goatcorp/Dalamud`) | The plugin framework loaded into the game process. Provides services, hooking, ImGui rendering, plugin loading. |
| **FFXIVClientStructs** (`aers/FFXIVClientStructs`) | Community-maintained C# bindings of the game's memory layout and functions. Shipped with Dalamud. Lets you use the game as a library. |
| **Lumina** (`NotAdam/Lumina`) | Reads FFXIV's proprietary game files (Excel sheets, textures, etc.) from your local install. |
| **Lumina.Excel** + **EXDSchema** (`xivdev/EXDSchema`) | Generated C# structs for the game's Excel sheets. |
| **Dalamud.NET.Sdk** | MSBuild SDK. Pulls in all references + DalamudPackager. Replaces the old `.targets` file approach. |
| **DalamudPackager** | Builds the distributable zip and generates the manifest. Included in the SDK. |
| **Plogon** (`goatcorp/Plogon`) | The CI that builds submitted plugins from a commit hash in an isolated, network-less environment. |
| **DalamudPluginsD17** | The official plugin repository — you submit a `manifest.toml` PR here. |

**The essential mental model:** your plugin is a .NET DLL loaded into the game
process by Dalamud. It runs *in* the game, with full access to game memory. There
is no sandbox. Dalamud gives you safe, wrapped APIs for the common cases; below
those you have raw pointers and it's on you.

---

## 2. Environment setup

### Requirements (API 15)

- **Windows 10 / Server 2016+** (Dalamud plugin dev is Windows-only in practice)
- **.NET 10 SDK** — specifically **10.0.101 or later**. The docs explicitly warn
  that **10.0.100 has package-restore problems**; don't use it.
- **Visual Studio 2026** or **JetBrains Rider 2025.3+**
- **XIVLauncher** installed with Dalamud enabled at least once (this populates
  `%AppData%\XIVLauncher\addon\Hooks\dev\` with the Dalamud assemblies)

### Per-API-level requirements

| API | Dalamud | Game patch | .NET | Notes |
| --- | --- | --- | --- | --- |
| 15 | 15.0.0.0 | 7.5 | .NET 10.0 | current |
| 14 | 14.0.0.0 | 7.4 | .NET 10.0 | canary |
| 13 | 13.0.0.0 | 7.3 | .NET 9.0 | |
| 12 | 12.0.0.0 | 7.2 | .NET 9.0 | |
| 11 | 11.0.0.0 | 7.1 | .NET 8.0 | Lumina 5 |
| 10 | 10.0.0.0 | 7.0 | .NET 8.0 | interfaces everywhere |
| 9 | 9.0.0.0 | 6.5 | .NET 7.0 | API == major version from here |

### Starting a project

**Recommended:** click "Use this template" on
[goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin) and rename.

Alternative templates (less actively maintained):
- `karashiiro/DalamudPluginProjectTemplate`
- `lmcintyre/PluginTemplate`

### Enabling dev mode in-game

- `/xldev` — the developer menu (plugin stats, branch switcher, anti-debug toggle,
  Addon Lifecycle / Agent Lifecycle toggles)
- `/xllog` — the Dalamud log + console window
- `/xldata` — data widgets (Addon Inspector, Inventory, SeString Creator, UiDebug, …)
- `/xlplugins` — the plugin installer
- `/xlhelp` — list of registered commands

---

## 3. Project layout & the SDK

### Directory layout

```
MySolution
|- MyPlugin
|  |- MyPlugin.csproj
|  |- MyPlugin.json         <- manifest template (or .yml, or csproj props)
|  |- packages.lock.json
|  |- Plugin.cs
|- MySolution.sln
```

Projects may be nested (`src/…`), and other projects in the solution may be in any
language. Only the plugin project itself must be C#-compatible with the loader.

### The InternalName — choose carefully

Your `AssemblyName` (defaults to the `.csproj` filename) becomes the plugin's
**`InternalName`**. Once published, **it may never be changed.** It is used for:

- the config directory name
- log entry tags
- the DLL filename
- the D17 submission directory

Use the manifest's `Name` field for a different user-facing display name.

### The .csproj

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
  <PropertyGroup>
    <Version>0.0.0.1</Version>
    <PackageProjectUrl>https://github.com/you/MyPlugin</PackageProjectUrl>
    <PackageLicenseExpression>AGPL-3.0-or-later</PackageLicenseExpression>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="..\Data\icon.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Visible>false</Visible>
    </Content>
  </ItemGroup>
</Project>
```

That's the whole file. The SDK supplies `Dalamud.dll`, `ImGui`/`ImPlot`/`ImGuizmo`/
`ImAnim` bindings, `Lumina`, `Lumina.Excel`, `FFXIVClientStructs`,
`InteropGenerator.Runtime`, `Newtonsoft.Json`, the target framework, and
DalamudPackager. `<Version>` is the only required property.

### Migrating off the legacy setups

**From a `DalamudPackager` PackageReference:**
1. Remove the `DalamudPackager` reference.
2. Change `<Project Sdk="Microsoft.NET.Sdk">` → `<Project Sdk="Dalamud.NET.Sdk/15.0.0">`.
3. Delete all `<Reference>` blocks pointing at `$(DalamudLibPath)` (Dalamud,
   ImGui.NET, Lumina, Lumina.Excel, FFXIVClientStructs, Newtonsoft.Json).
4. Delete the `<DalamudLibPath>$(appdata)\XIVLauncher\addon\Hooks\dev\</DalamudLibPath>`
   property.
5. Optionally strip the rest of the `<PropertyGroup>` — the SDK sets it all except
   `<Version>`. Anything you leave overrides the SDK.

**From a `.targets` file:**
1. Delete `Dalamud.Plugin.Bootstrap.targets`.
2. Delete `<Import Project="Dalamud.Plugin.Bootstrap.targets"/>`.
3. Switch the SDK header as above.

The `targets/` folder was removed entirely in API 14. Some IDEs need a restart
after this migration.

---

## 4. The plugin entrypoint & lifecycle

Dalamud scans your DLL for **exactly one** class implementing `IDalamudPlugin`
(or, since API 15, `IAsyncDalamudPlugin`). It constructs that class, injecting
services declared in the constructor or via `[PluginService]` properties.

### Synchronous plugin (`IDalamudPlugin`)

```csharp
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MyPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/mycommand";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("MyPlugin");
    private MainWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow   = new MainWindow(this);
        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Shown in /xlhelp",
        });

        PluginInterface.UiBuilder.Draw         += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi   += ToggleMainUi;

        Log.Information($"{PluginInterface.Manifest.Name} loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw         -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi   -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();
    public void ToggleMainUi()   => MainWindow.Toggle();
    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
```

### Asynchronous plugin (`IAsyncDalamudPlugin`, new in API 15)

Experimental but with a stable interface. The plugin fully initializes **and**
uninitializes off the main thread — no `Task.Run().GetAwaiter().GetResult()`
patterns, no load-time hitches.

```csharp
public sealed class Plugin : IAsyncDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        // Runs off-thread. Cancelled after a timeout (currently 60s).
        // The plugin is not "loaded" until this completes successfully.
        var data = await FetchSomethingExpensiveAsync(cancellationToken);

        // Anything that must touch the main thread goes through IFramework:
        await Framework.Run(() => HookIntoTheGame(data), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // NOT cancellable. Resolve promptly and release everything.
        await Framework.Run(UnhookFromTheGame);
    }
}
```

`IAsyncDalamudPlugin` inherits `IAsyncDisposable` rather than `IDisposable`.

### The dispose contract — non-negotiable

Plugin developers are **required** to implement a fully functional dispose cycle
and ideally leak nothing. Every one of these needs a matching teardown:

| Registered | Must be undone with |
| --- | --- |
| `event += handler` | `event -= handler` |
| `CommandManager.AddHandler` | `RemoveHandler` |
| `AddonLifecycle.RegisterListener` | `UnregisterListener` |
| `AddonEventManager.AddEvent` (persistent addons) | `RemoveEvent` |
| `GameInteropProvider.HookFrom*` | `hook.Dispose()` |
| `DtrBar.Get(...)` entry | dispose the entry |
| `IPC provider` | `UnregisterAction()` / `UnregisterFunc()` |
| `WindowSystem.AddWindow` | `RemoveAllWindows()` + dispose windows |
| `IConsole.AddCommand/AddVariable` | `RemoveEntry` |

A leaked hook keeps executing your detour after unload. This is the single most
common cause of "my plugin is doing weird things after I disabled it" support
threads.

### Load reasons

`PluginInterface.Reason` returns `PluginLoadReason` — a `[Flags]` enum since
API 12, so use `.HasFlag()`. Values cover Installer / Reload / Boot / Update.

---

## 5. Services: the injection system

Dalamud services are marked with `PluginInterfaceAttribute` internally. There are
two ways to obtain them.

### A. Constructor injection

```csharp
public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
{
    // ...
}
```

> The `[RequiredVersion("1.0")]` attribute you'll see in old code/FAQ was
> **removed in API 10**. Don't add it; nothing replaces it.

### B. `[PluginService]` properties (preferred in modern code)

```csharp
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
}
```

Static `[PluginService]` properties on the entrypoint class are filled by Dalamud
automatically. For a **separate** service-holder class, inject into it explicitly:

```csharp
public class Services
{
    public static void Init(IDalamudPluginInterface pi) => pi.Create<Services>();

    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; } = null!;
}
```

`IDalamudPluginInterface` also has `Task InjectAsync(object instance, params object[] scopedObjects)`
for async injection into an arbitrary instance, and since API 14 it implements
`System.IServiceProvider`.

### Notes

- All services inherit the marker interface `IDalamudService` (API 14+).
- All service interfaces live in `Dalamud.Plugin.Services` as of API 14 —
  `ISigScanner` and `ITargetManager` moved there from `Dalamud.Game.*`.
- Experimental services emit warning `Dalamud001`. Suppress with
  `#pragma warning disable Dalamud001` or `<NoWarn>Dalamud001</NoWarn>`.

---

## 6. Complete service catalogue (API 15)

Every interface in `Dalamud/Plugin/Services/` at API 15:

### Core / lifecycle

| Service | Purpose |
| --- | --- |
| `IDalamudPluginInterface` | The root object. Config paths, manifest, UiBuilder, IPC factories, installed-plugin list, Dalamud version. |
| `IFramework` | The game's per-frame tick. `Update` event, `Run`/`RunOnTick`/`RunOnFrameworkThread`, `DelayTicks`, `CreateDebouncer`, `IsInFrameworkUpdateThread`. |
| `IGameLifecycle` | Game/Dalamud shutdown signalling. |
| `IPluginLog` | Structured logging (`Verbose`/`Debug`/`Information`/`Warning`/`Error`/`Fatal`), Serilog-style templates. |
| `IConsole` | Typed console commands & variables for `/xllog`. No string parsing. |
| `ISelfTestRegistry` | Register self-tests for Dalamud's test agent. |
| `IReliableFileStorage` | Crash-safe file read/write backed by a secondary virtual FS. Size-limited. (API 14+) |

### Client state

| Service | Purpose |
| --- | --- |
| `IClientState` | `TerritoryType`, `MapId`, `Instance`, `IsLoggedIn`, `IsPvP`, `IsGPosing`, `ClientLanguage`, `IsClientIdle()`. Events: `Login`, `Logout`, `TerritoryChanged`, `MapIdChanged`, `InstanceChanged`, `ZoneInit`, `ClassJobChanged`, `LevelChanged`, `EnterPvP`/`LeavePvP`, `CfPop`. |
| `IPlayerState` | Player identity & static data valid from login to logout, independent of the GameObject: `CharacterName`, `ContentId`, `EntityId`, `CurrentWorld`/`HomeWorld`, `Race`/`Tribe`/`Sex`, `ClassJob`, `Level`, `EffectiveLevel`, `IsLevelSynced`, `GuardianDeity`, `BirthMonth/Day`, base attributes, `GrandCompany`, `HomeAetheryte`, `FavoriteAetherytes`, `PlayerCommendations`, `IsMentor`/`IsNovice`/`IsReturner`. |
| `IObjectTable` | The game's object table. Indexer, `SearchById`, `SearchByEntityId`, `CreateObjectReference`, `LocalPlayer`, plus filtered enumerables `PlayerObjects`, `CharacterManagerObjects`, `ClientObjects`, `EventObjects`, `StandObjects`, `ReactionEventObjects`. |
| `ITargetManager` | Current target, focus target, soft target, mouseover. |
| `IPartyList` | Party members. |
| `IBuddyList` | Chocobo companion, pets, squadron members. |
| `IAetheryteList` | Attuned aetherytes. |
| `IFateTable` | Active FATEs. |
| `ICondition` | The `ConditionFlag` array — in combat, in duty, occupied, crafting, mounted, etc. |
| `IJobGauges` | Per-job gauge structs. |
| `IKeyState` | Raw key state. |
| `IGamepadState` | Gamepad axes/buttons. |
| `IUnlockState` | Unlock status for mounts, minions, emotes, recipes, aether currents, etc. + `Unlock` event. Experimental (`Dalamud001`). (API 14+) |
| `IDutyState` | Duty started/completed/wiped events; `IDutyStateEventArgs` carries a `RowRef`. |

### GUI

| Service | Purpose |
| --- | --- |
| `IChatGui` | Print to chat; intercept messages. Events: `ChatMessage`, `CheckMessageHandled`, `ChatMessageHandled`, `ChatMessageUnhandled`, `LogMessage`. Chat link handlers (`AddChatLinkHandler`, moved here in API 13). |
| `IGameGui` | Addon/agent lookup (`GetAddonByName` → `AtkUnitBasePtr`, `GetAgentById` → `AgentInterfacePtr`, `GetUIModule` → `UIModulePtr`), world↔screen projection, map links (`OpenMapWithMapLink`), `AgentUpdate` event. |
| `IDtrBar` | Server-info bar entries. `Get(title, text)` → `IDtrBarEntry` with `OnClick(AddonMouseEventData)`. |
| `IToastGui` | Normal / quest / error toasts. |
| `IFlyTextGui` | Fly text (damage numbers etc.). |
| `IContextMenu` | Add entries to game context menus via `OnMenuOpened`. |
| `INamePlateGui` | Modify nameplates: `OnNamePlateUpdate`, `OnPostNamePlateUpdate`, `OnDataUpdate`, `OnPostDataUpdate`. |
| `IPartyFinderGui` | Party Finder listing events. |
| `INotificationManager` | Dalamud's own toast notifications — `AddNotification(Notification)`. |
| `ITitleScreenMenu` | Add entries to the title screen menu. |
| `IAddonLifecycle` | Listen to any addon's lifecycle events by name. See §11. |
| `IAddonEventManager` | Attach mouse/click events to native UI nodes; cursor control. See §11. |
| `IAgentLifecycle` | Same concept as AddonLifecycle, for Agents. `PreventOriginal()` (API 15). |

### Data & interop

| Service | Purpose |
| --- | --- |
| `IDataManager` | Lumina access — `GetExcelSheet<T>()`, `GameData`, file access, `GetFile<T>()`. |
| `ISeStringEvaluator` | Evaluate encoded SeStrings the way the game would. No longer experimental as of API 13. |
| `ISigScanner` | `ScanText`, `TryScanText`, `ScanModule`, `ScanData`, `GetStaticAddressFromSig`, `SearchBase`. 32-bit members removed in API 15. |
| `IGameInteropProvider` | Hook creation + `[Signature]` attribute resolution. |
| `IGameConfig` | Game config options (system/UI config), with change events. |
| `IGameInventory` | Inventory contents + change events; `GetInventoryItems(GameInventoryType)`. |
| `IMarketBoard` | Market board data events (the ones Dalamud already collects for Universalis). |
| `ITextureProvider` | Load textures from game paths, icon IDs, files, memory, or SeStrings. Returns `ISharedImmediateTexture`. |
| `ITextureReadbackProvider` | Get raw RGBA back out of a texture / save to file. |
| `ITextureSubstitutionProvider` | Intercept and replace texture loads (how Penumbra-likes work). |
| `ICommandManager` | Register `/slash` commands. |

---

## 7. The three tiers of game interaction

The docs prescribe a strict priority order:

1. **Dalamud APIs first.** Safest, stable across game patches, only break on API
   bumps, well-documented, often wrap ugly concepts and validate data.
2. **FFXIVClientStructs second.** Ships with Dalamud, effectively lets you use the
   game as a library. Pointers and `unsafe` code — your safety is your problem.
3. **Raw memory / signatures last.** Escape hatch for things nobody has reversed
   yet. Read undocumented structures, call/hook by signature.

Most plugins live in tiers 1–2. If you do tier-3 work, **contribute findings back
to FFXIVClientStructs** so others benefit.

---

## 8. Tier 1 — Dalamud APIs

### Slash commands

```csharp
CommandManager.AddHandler("/mycmd", new CommandInfo(OnCommand)
{
    HelpMessage = "Does the thing. Usage: /mycmd [on|off]",
    ShowInHelp  = true,
});

private void OnCommand(string command, string args) { /* ... */ }

// Dispose:
CommandManager.RemoveHandler("/mycmd");
```

### Console commands & variables (typed, no parsing)

```csharp
Console.AddCommand("mycmd.reload", "Reload config", () => { Reload(); return true; });
Console.AddCommand<int>("mycmd.set", "Set the count", (int n) => { Count = n; return true; });
var verbose = Console.AddVariable("mycmd.verbose", "Verbose logging", false);
```

Prefixed with your plugin's `Console.Prefix`. Remove with `Console.RemoveEntry(entry)`.

### Chat

```csharp
ChatGui.Print("Hello!");
ChatGui.Print(new SeString(...), messageTag: "MyPlugin", tagColor: 45);
ChatGui.PrintError("Something went wrong.");
ChatGui.Print(new XivChatEntry { Type = XivChatType.Echo, Message = seString });
```

Intercepting (API 15 signature — parameters were consolidated into `IChatMessage`):

```csharp
ChatGui.ChatMessage += OnChat;

private void OnChat(IHandleableChatMessage message)
{
    // message.Sender / message.Message are mutable via IMutableChatMessage
    // message.PreventOriginal() suppresses the message
    if (message.Message.TextValue.Contains("spam"))
        message.PreventOriginal();
}
```

> **API 15 breaking change:** `XivChatType` is now properly parsed; its packed
> relation data moved to `XivChatRelationKind SourceKind` / `TargetKind`. The enum
> values are now genuine `LogKind` sheet RowIds. Code relying on out-of-range
> values (>110) must be updated. Also consider the `LogMessage` event (API 14+)
> for intercepting system messages.

### Server info bar (DTR)

```csharp
var entry = DtrBar.Get("MyPlugin");
entry.Text = new SeString(new TextPayload("42 things"));
entry.OnClick = (AddonMouseEventData e) =>
{
    if (e.IsLeftClick && e.IsControlHeld) DoSomething();
};
// Dispose the entry on unload.
```

### Notifications

```csharp
NotificationManager.AddNotification(new Notification
{
    Title    = "MyPlugin",
    Content  = "Operation complete.",
    Type     = NotificationType.Success,
    InitialDuration = TimeSpan.FromSeconds(5),
    IconTexture = TextureProvider.GetFromGameIcon(new GameIconLookup(60074)),
    Minimized = false,
});
```

Also supports `Progress`, `HardExpiry`, `UserDismissable`, `RespectUiHidden`,
`ShowIndeterminateIfNoExpiry`, `MinimizedText`, and the returned
`IActiveNotification` can be updated or dismissed later.

### Context menus

```csharp
ContextMenu.OnMenuOpened += args =>
{
    if (args.AddonName != "ContactList") return;
    args.AddMenuItem(new MenuItem
    {
        Name       = "Do the thing",
        PrefixChar = 'M',
        OnClicked  = clicked => DoTheThing(clicked),
    });
};
```

### Nameplates

```csharp
NamePlateGui.OnNamePlateUpdate += (context, handlers) =>
{
    foreach (var h in handlers)
    {
        if (h.NamePlateKind != NamePlateKind.PlayerCharacter) continue;
        h.NameParts.Text = /* SeString */;
    }
};
```

`OnDataUpdate` fires when the underlying data changes; `OnNamePlateUpdate` on
draw-time updates. The `Post` variants fire after the game's handling.

### Toasts and fly text

```csharp
ToastGui.ShowNormal("Normal toast");
ToastGui.ShowQuest("Quest toast", new QuestToastOptions { PlaySound = true });
ToastGui.ShowError("Error toast");
```

---

## 9. Tier 2 — FFXIVClientStructs

CS is shipped with Dalamud. Anything reachable from its singletons is free.

```csharp
using FFXIVClientStructs.FFXIV.Client.Game.UI;

public unsafe bool IsPlayerMentor()
{
    var playerState = PlayerState.Instance();
    return playerState->IsMentor();
}
```

Common entry points:

| Type | Reach |
| --- | --- |
| `PlayerState.Instance()` | Player flags, unlocks, mentor state, GC rank |
| `UIState.Instance()` | Unlock links, hunting log, aether currents, telepo |
| `AgentModule.Instance()` / `AgentXyz.Instance()` | Per-UI controllers |
| `RaptureAtkModule.Instance()` | Addon/atk arrays |
| `RaptureShellModule.Instance()` | Execute macros/commands |
| `InventoryManager.Instance()` | Inventories, item counts |
| `ActionManager.Instance()` | Action status/recast (read-only use only — see restrictions!) |
| `Framework.Instance()` | The game's root framework, `UIModule`, task manager |
| `AtkStage.Instance()` | Tooltip manager, atk arrays, focus |

Docs: <https://ffxiv.wildwolf.dev>. Incomplete — expect gaps.

### Using a custom ClientStructs build

Discouraged for shipping, useful for testing CS contributions.

```xml
<PropertyGroup>
  <Use_Dalamud_FFXIVClientStructs>false</Use_Dalamud_FFXIVClientStructs>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\FFXIVClientStructs\FFXIVClientStructs\FFXIVClientStructs.csproj" Private="True" />
  <ProjectReference Include="..\FFXIVClientStructs\InteropGenerator.Runtime\InteropGenerator.Runtime.csproj" Private="True" />
</ItemGroup>
```

`Private` must be `true` or unset so MSBuild copies the DLL to your output (which
is what makes it win over Dalamud's copy). You then own resolver initialization:

```csharp
InteropGenerator.Runtime.Resolver.GetInstance.Setup(
    SigScanner.SearchBase,
    DataManager.GameData.Repositories["ffxiv"].Version,
    new FileInfo(Path.Join(pluginInterface.ConfigDirectory.FullName, "SigCache.json")));
FFXIVClientStructs.Interop.Generated.Addresses.Register();
InteropGenerator.Runtime.Resolver.GetInstance.Resolve();
```

Run `dotnet clean` when switching to/from a custom build. Revert when done so you
don't ship a stale DLL.

---

## 10. Tier 3 — Signatures, delegates, hooks

### Signatures explained

Function *offsets* (e.g. `ffxiv_dx11.exe+4BC200`, or `1404BC200` with the compiler's
`0x140000000` base) change every single game version — useless in a plugin.

A **signature** is a hex byte string uniquely identifying either the start of a
function (*direct signature*) or a reference to it (*indirect signature*, typically
starting with `E8` — a `call`). Example: `E8 ?? ?? ?? ?? 41 88 84 2C`. `??` is a
wildcard for bytes that vary (relative offsets, etc.).

Signatures survive game patches unless Square Enix changes that code. It's common
for a signature to last several major patches. Generate them with
[Caraxi's SigMaker-x64](https://github.com/Caraxi/SigMaker-x64) or by hand.

### Calling a game function — `[Signature]` + delegate

```csharp
public class GameFunctions
{
    private delegate byte IsQuestCompletedDelegate(ushort questId);

    [Signature("E8 ?? ?? ?? ?? 41 88 84 2C")]
    private readonly IsQuestCompletedDelegate? _isQuestCompleted = null;

    public GameFunctions()
    {
        Plugin.GameInteropProvider.InitializeFromAttributes(this);
    }

    public bool IsQuestCompleted(ushort questId)
    {
        if (_isQuestCompleted == null)
            throw new InvalidOperationException("IsQuestCompleted signature wasn't found!");
        return _isQuestCompleted(questId) > 0;
    }
}
```

`InitializeFromAttributes(this)` reflects over the object, resolves every
`[Signature]` member, and injects the pointer. Unresolved signatures leave the
field `null` — handle it.

### Function pointer variant (no delegate declaration)

```csharp
[Signature("E8 ?? ?? ?? ?? 41 88 84 2C")]
private readonly delegate* unmanaged<ushort, byte> _isQuestCompleted;
```

Inside `unmanaged<>`, the **last** type is the return type; all preceding are
arguments in order. `unmanaged<uint, string, byte>` ≡ `byte F(uint, string)`.

### Manual SigScanner

```csharp
public unsafe class SomeSigWrapper
{
    private readonly delegate* unmanaged<ushort, byte> _isQuestCompleted;

    public SomeSigWrapper()
    {
        var fptr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? 41 88 84 2C");
        _isQuestCompleted = (delegate* unmanaged<ushort, byte>)fptr;
    }
}
```

`ScanText` throws if not found; `TryScanText` returns a bool. Other methods:
`ScanModule`, `ScanData`, `GetStaticAddressFromSig`.

### Hooking — from a ClientStructs address (preferred)

```csharp
using SetSavePendingDelegate = RaptureMacroModule.Delegates.SetSavePendingFlag;

public unsafe class MyHook : IDisposable
{
    private readonly Hook<SetSavePendingDelegate> _macroSaveHook;

    public MyHook()
    {
        _macroSaveHook = Plugin.GameInteropProvider.HookFromAddress<SetSavePendingDelegate>(
            RaptureMacroModule.MemberFunctionPointers.SetSavePendingFlag,
            SetSavePendingDetour);
        _macroSaveHook.Enable();
    }

    public void Dispose() => _macroSaveHook.Dispose();  // disables + cleans up

    private void SetSavePendingDetour(RaptureMacroModule* self, bool needsSave, uint set)
    {
        try
        {
            Plugin.Log.Information("A macro save happened!");
            // your logic
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error handling macro save.");
        }

        _macroSaveHook.Original(self, needsSave, set);
    }
}
```

Reuse the CS-declared delegate rather than redeclaring it.

### Hooking — from a signature

```csharp
public unsafe class MySiggedHook : IDisposable
{
    private delegate void SetSavePendingDelegate(RaptureMacroModule* self, bool needsSave, uint set);

    [Signature("45 85 C0 75 04 88 51 3D", DetourName = nameof(SetSavePendingDetour))]
    private Hook<SetSavePendingDelegate>? _macroSaveHook;

    public MySiggedHook()
    {
        Plugin.GameInteropProvider.InitializeFromAttributes(this);
        _macroSaveHook?.Enable();   // may be null if the sig failed
    }

    public void Dispose() => _macroSaveHook?.Dispose();

    private void SetSavePendingDetour(RaptureMacroModule* self, bool needsSave, uint set)
    {
        try { /* ... */ }
        catch (Exception ex) { Plugin.Log.Error(ex, "..."); }
        _macroSaveHook!.Original(self, needsSave, set);
    }
}
```

### All `IGameInteropProvider` hook factories

```csharp
void InitializeFromAttributes(object self);

Hook<T> HookFromSignature<T>(string signature, T detour, HookBackend backend = Automatic);
Hook<T> HookFromAddress<T>(nint procAddress, T detour, HookBackend backend = Automatic);
Hook<T> HookFromAddress<T>(nuint procAddress, T detour, HookBackend backend = Automatic);
unsafe Hook<T> HookFromAddress<T>(void* procAddress, T detour, HookBackend backend = Automatic);
Hook<T> HookFromSymbol<T>(string moduleName, string exportName, T detour, HookBackend backend = Automatic);
Hook<T> HookFromFunctionPointerVariable<T>(nint address, T detour);
Hook<T> HookFromImport<T>(ProcessModule? module, string moduleName, string functionName, uint hintOrOrdinal, T detour);
```

`HookBackend` is `Automatic | Reloaded | MinHook`. Leave it on `Automatic`
(backend selection is slated for removal in API 16).

### Hooking rules — read these twice

- Hooking is **highly invasive**. You are replacing game code.
- An **unhandled exception inside a detour will most likely crash the game.**
  Wrap your detour body in try/catch. Always.
- Hooks are **blocking**. The game waits for you. Keep them fast; no I/O, no
  `.Wait()`, no long loops.
- **Multiple plugins may hook the same function**, including yours multiple times.
  Prefer *not* to modify arguments or interrupt flow. There are valid exceptions,
  but be aware you may not be alone.
- **Hook order is inverse load order** — the last plugin to enable a hook gets
  control first.
- Always call `Original(...)` unless you deliberately intend to cancel the call.
- Corrupt-state exceptions (`AccessViolationException`) are **no longer caught by
  the CLR** as of API 12 (.NET 9). An AV in your detour is a crash.

### Polling as an alternative

Sometimes simpler and perfectly fine:

```csharp
public class HealthWatcher : IDisposable
{
    private uint _lastHealth;

    public HealthWatcher() => Plugin.Framework.Update += OnFrameworkTick;
    public void Dispose()   => Plugin.Framework.Update -= OnFrameworkTick;

    private void OnFrameworkTick(IFramework framework)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return;

        var hp = player.CurrentHp;
        if (hp == _lastHealth) return;

        _lastHealth = hp;
        Plugin.Log.Information("Health updated to {health}.", hp);
    }
}
```

Runs once per frame. Cheap checks only — you're on the critical path. If a
per-second cadence is enough, gate it with a timestamp or use
`Framework.RunOnTick(..., delay: TimeSpan.FromSeconds(1))`.

---

## 11. Native UI: AddonLifecycle & AddonEventManager

An **Addon** is a game UI window (`AtkUnitBase`); an **Agent** is its controller.

### IAddonLifecycle

Listen to any addon's events **by name** — no addresses, no per-addon reversing,
works even for addons that don't implement their own Draw.

```csharp
public interface IAddonLifecycle
{
    public delegate void AddonEventDelegate(AddonEvent type, AddonArgs args);

    void RegisterListener(AddonEvent eventType, IEnumerable<string> addonNames, AddonEventDelegate handler);
    void RegisterListener(AddonEvent eventType, string addonName, AddonEventDelegate handler);
    void RegisterListener(AddonEvent eventType, AddonEventDelegate handler);

    void UnregisterListener(AddonEvent eventType, IEnumerable<string> addonNames, [Optional] AddonEventDelegate handler);
    void UnregisterListener(AddonEvent eventType, string addonName, [Optional] AddonEventDelegate handler);
    void UnregisterListener(AddonEvent eventType, [Optional] AddonEventDelegate handler);
    void UnregisterListener(params AddonEventDelegate[] handlers);
}
```

```csharp
AddonLifecycle.RegisterListener(AddonEvent.PreDraw,   "FieldMarker", OnPreDraw);
AddonLifecycle.RegisterListener(AddonEvent.PostUpdate,"FieldMarker", OnPostUpdate);
AddonLifecycle.RegisterListener(AddonEvent.PostDraw,
    new[] { "Character", "FieldMarker", "NamePlate" }, OnPostDraw);

// Unregister either way:
AddonLifecycle.UnregisterListener(AddonEvent.PostDraw,
    new[] { "Character", "FieldMarker", "NamePlate" }, OnPostDraw);
AddonLifecycle.UnregisterListener(OnPreDraw, OnPostUpdate);
```

**Always name the addons you care about.** Registering with no names fires for
*every* addon.

Events come in Pre/Post pairs: Setup, Update, Draw, Refresh, RequestedUpdate,
ReceiveEvent, Finalize, plus (API 14+) **Open, Close, Show, Hide, Move, MouseOver,
MouseOut, Focus**. Notes:
- There is **no PostFinalize** — the addon is freed by then.
- `Focus` only fires for certain popups (SelectYesno etc.).
- `Move` fires when a move *completes*, not during the drag.

Args are downcast to event-specific types; check `args.Type` if unsure:

```csharp
private void OnPostSetup(AddonEvent type, AddonArgs args)
{
    if (args is AddonSetupArgs setupArgs)
    {
        var valueCount = setupArgs.AtkValueCount;
        var values     = (AtkValue*)setupArgs.AtkValues;
        var valueSpan  = setupArgs.AtkValueSpan;
    }
}
```

**API 15:** `PreventOriginal()` lets you stop the game from processing the event
(e.g. prevent a window from opening). `/xldev` has an "Enable Addon Lifecycle"
toggle to restore original vtables for debugging.

**API 14 rework caveats:** the service now swaps addon virtual tables instead of
hooking vfunc callsites. Consequences: it is **no longer recursion-safe** (calling
`Refresh` inside an `OnRefresh` listener will recurse into your own listener), and
`ReceiveEvent` messages are relayed more accurately (and thus slightly differently)
than before.

**API 13:** `AddonArgs.Addon` is now `AtkUnitBasePtr`.

### IAddonEventManager

Attach real event handlers to native UI nodes. Dalamud tracks and auto-removes
them when your plugin unloads or the addon is finalized.

```csharp
public interface IAddonEventManager
{
    public delegate void AddonEventHandler(AddonEventType atkEventType, nint atkUnitBase, nint atkResNode);

    IAddonEventHandle? AddEvent(nint atkUnitBase, nint atkResNode, AddonEventType eventType, AddonEventHandler eventHandler);
    void RemoveEvent(IAddonEventHandle eventHandle);
    void SetCursor(AddonCursorType cursor);
    void ResetCursor();
}
```

**Critical:** you must register a **Node**, never a **Component**. Registering a
Component's address **will crash the game**. Valid node types end in `Node` — from
an `AtkComponentButton*`, use its `OwnerNode`.

Pairs naturally with AddonLifecycle:

```csharp
AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MonsterNote", OnPostSetup);

private void OnPostSetup(AddonEvent type, AddonArgs args)
{
    var addon      = (AtkUnitBase*)args.Addon;
    var targetNode = addon->GetNodeById(22);

    // The node must be told to emit events:
    targetNode->NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.RespondToMouse | NodeFlags.HasCollision;

    MouseOver = EventManager.AddEvent((nint)addon, (nint)targetNode, AddonEventType.MouseOver, TooltipHandler);
    MouseOut  = EventManager.AddEvent((nint)addon, (nint)targetNode, AddonEventType.MouseOut,  TooltipHandler);
}

private void TooltipHandler(AddonEventType type, IntPtr addon, IntPtr node)
{
    var addonId = ((AtkUnitBase*)addon)->ID;
    switch (type)
    {
        case AddonEventType.MouseOver:
            AtkStage.GetSingleton()->TooltipManager.ShowTooltip(addonId, (AtkResNode*)node, "This is a tooltip.");
            break;
        case AddonEventType.MouseOut:
            AtkStage.GetSingleton()->TooltipManager.HideTooltip(addonId);
            break;
    }
}
```

Cursor feedback for clickable custom elements:

```csharp
case AddonEventType.MouseOver:  EventManager.SetCursor(AddonCursorType.Clickable); break;
case AddonEventType.MouseOut:   EventManager.ResetCursor(); break;
case AddonEventType.MouseClick: /* your click logic */ break;
```

**Unregistering:** non-persistent addons (open/close) clean up automatically.
**Persistent addons** (`_BagWidget`, `NamePlate`, …) require you to keep the
`IAddonEventHandle` and call `RemoveEvent`. Everything is removed on plugin unload
regardless. Event add/remove is logged verbosely under `AddonEventManager`.

### IAgentLifecycle

Same shape as AddonLifecycle but for Agents. API 15 adds `PreventOriginal()` (e.g.
suppress specific `ReceiveEvent` calls) and an `/xldev` toggle.

### Custom node debugging (API 15)

The UiDebug widget shows custom nodes by typename. To label your own allocated
nodes, publish a `DataShare` named `StringMappedCustomNodes` of type
`ConcurrentDictionary<nint, string>` mapping address → label.

---

## 12. Your own UI: ImGui, WindowSystem, ImRaii

Dalamud renders ImGui over the game. **If it looks like a window, use the
Windowing API** — this is a stated approval criterion, not a suggestion. It gives
you native close-order integration, pinning, clickthrough, opacity, and background
blur for free.

### Bindings

Since API 13 the bindings are Dalamud's own (derived from Hexa.NET.ImGui):

| Old | New |
| --- | --- |
| `ImGuiNET` | `Dalamud.Bindings.ImGui` |
| `ImPlotNET` | `Dalamud.Bindings.ImPlot` |
| `ImGuizmoNET` | `Dalamud.Bindings.ImGuizmo` |
| — | `Dalamud.Bindings.ImAnim` (API 14+, animation engine) |

Other API 13 binding changes:
- `IDalamudTextureWrap.ImGuiHandle` → `.Handle`
- `ImGui.Text` calls `TextUnformatted` internally; `ImGui.TextWrapped` is safe now
  (`ImGuiHelpers.SafeTextWrapped` obsoleted; new `ImGui.TextColoredWrapped`)
- UTF-8 string literals supported (including in ImRaii)
- `ImGuiListClipperPtr` → `ImGui.ImGuiListClipper()`
- Enum names are PascalCase (`ImGuiColorEditFlags.DisplayRGB` → `DisplayRgb`)
- `out` parameters became pointer/`ref` parameters
- Internals live under `Dalamud.Bindings.ImGui.ImGuiP` (unstable)

### A window

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    // "##" gives the window a stable ImGui ID while showing a friendly title
    public MainWindow(Plugin plugin)
        : base("My Window##MyPluginMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Hello!");

        using (var child = ImRaii.Child("scroller", Vector2.Zero, true))
        {
            if (!child.Success) return;
            // ...
        }
    }
}
```

Registered via `WindowSystem.AddWindow(window)`, drawn by hooking
`PluginInterface.UiBuilder.Draw += WindowSystem.Draw`.

**`Window` members worth knowing:**
`IsOpen`, `Toggle()`, `BringToFront()`, `RequestFocus`, `IsTopMost`, `IsFocused`,
`IsHovered`, `Position`/`PositionCondition`, `Size`/`SizeCondition`,
`SizeConstraints`, `Collapsed`/`CollapsedCondition`, `Flags`, `BgAlpha`,
`ForceMainWindow`, `ShowCloseButton`, `AllowPinning`, `AllowClickthrough`,
`AllowBackgroundBlur`, `IsPinned`, `IsClickthrough`, `RespectCloseHotkey`,
`InhibitAtkCollision`, `DisableWindowSounds`, `OnOpenSfxId`/`OnCloseSfxId`,
`DisableFadeInFadeOut`, `TitleBarButtons`.

**Overridable hooks:** `PreOpenCheck()`, `DrawConditions()`, `PreDraw()`,
`Draw()` (abstract), `PostDraw()`, `OnOpen()`, `OnClose()`, `OnSafeToRemove()`,
`Update()`.

> Since API 14, a window that throws inside `Draw()` stops rendering and shows an
> error message instead of spamming the log.

### ImRaii — scope-based ImGui cleanup

```csharp
using (ImRaii.Child("id", Vector2.Zero, true)) { /* auto EndChild */ }
using (ImRaii.PushIndent(55f))                 { /* auto Unindent */ }
using (ImRaii.Table("t", 3))                   { /* auto EndTable */ }
using (ImRaii.Tooltip())                       { /* auto EndTooltip */ }
using (ImRaii.Disabled(condition))             { /* auto EndDisabled */ }
```

**API 15 change:** `IEndObject` was removed (it boxed values and created GC
pressure). The RAII objects are now `ref struct`s. If you used `var`, nothing
changes. `ColorDisposable` / `StyleDisposable` replace `IEndObject` for properties
used across `PreDraw`/`PostDraw`. `Group`, `Tooltip`, and `Disabled` have **no bool
conversion** — they cannot fail.

New in API 15: `ImRaii.Header(label, flags)`, `ImRaii.Header(label, ref visible, flags)`,
`ImRaii.PushColor(ImGuiCol, Vector4?, bool condition = true)`,
`ImRaii.Columns(count, id, border)`, `ImRaii.ChildFrame(id, size[, flags])`.

Only `Push...` functions remain classes that can be carried across scopes.

### Scaling — mandatory for correctness

Never hardcode pixel values without scaling; users run HUD scales above and below
100%.

```csharp
ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
ImGuiHelpers.ScaledDummy(20.0f);
```

### Textures

```csharp
// From a file next to your DLL
var tex = TextureProvider.GetFromFile(path).GetWrapOrDefault();
if (tex != null) ImGui.Image(tex.Handle, tex.Size);

// From a game icon ID
var icon = TextureProvider.GetFromGameIcon(new GameIconLookup(62100 + classJobId)).GetWrapOrEmpty();
ImGui.Image(icon.Handle, new Vector2(28, 28) * ImGuiHelpers.GlobalScale);
```

Everything returns `ISharedImmediateTexture` — an async-loading handle.

- `GetWrapOrEmpty()` — transparent 4×4 placeholder until loaded (easiest)
- `GetWrapOrDefault()` — `null` until loaded
- `TryGetWrap(out wrap, out ex)` — explicit
- `RentAsync()` — a wrap that stays valid; **legacy**, only for off-main-thread use

**Do not build your own texture cache.** Caching `IDalamudTextureWrap` is
explicitly discouraged — it defeats the provider's async loading and optimizations.

`ITextureReadbackProvider` gets raw RGBA out or saves to disk.
`ITextureSubstitutionProvider` intercepts game texture loads.

### UiBuilder

Events: `Draw`, `ResizeBuffers`, `OpenConfigUi`, `OpenMainUi`, `ShowUi`, `HideUi`,
`DefaultGlobalScaleChanged`, `DefaultFontChanged`, `DefaultStyleChanged`.

Properties: `DefaultFontHandle`, `IconFontHandle`, `MonoFontHandle`,
`IconFontFixedWidthHandle`, `DefaultFontSpec`, `FontDefaultSizePt`/`Px`,
`FontDefault`/`FontIcon`/`FontMono`, `FontAtlas`, `DeviceHandle`,
`WindowHandlePtr`, `FrameCount`, `CutsceneActive`, `ShouldModifyUi`, `UiPrepared`,
`ShouldUseReducedMotion`, `PluginUISoundEffectsEnabled` (API 14+),
`DisableAutomaticUiHide` / `DisableUserUiHide` / `DisableCutsceneUiHide` /
`DisableGposeUiHide`, `OverrideGameCursor`.

Methods: `LoadUld(path)`, `Task WaitForUi()`, `RunWhenUiPrepared<T>(func, runInFrameworkThread)`.

### Fonts

API 15 unified the language-specific Noto Sans fonts into `NotoSansCjkRegular` and
`NotoSansCjkMedium`. The `.ttc` format requires specifying a face via `FontNo` on
`IFontSpec`: `0 = Japanese, 1 = Traditional Chinese, 2 = Simplified Chinese, 3 = Korean`.

Font Awesome was updated 6.4.2 → 7.1.0 in API 14 (`InstagramSquare` and
`VectorSquare` were removed; ~23 icons added).

---

## 13. Game data: Lumina & Excel sheets

**Use Lumina, not XIVAPI.** Lumina reads your local game files: always current,
always accurate, dramatically faster than HTTP.

```csharp
using Lumina.Excel.Sheets;

var territoryId = ClientState.TerritoryType;
if (DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
{
    var placeName = row.PlaceName.Value.Name.ToString();
}
```

### Lumina 5 (API 11+) — what changed

- **Rows are `readonly struct`s**, not classes. 24–32 bytes, no GC pressure, copy
  freely. Created on demand instead of cached.
- **Columns are read on demand** from the underlying page. The JIT optimizes this
  away; cost is effectively a byteswap.
- **Array columns are `Collection<T>`** — lightweight, evaluated ad hoc.
- **`LazyRow<T>` → `RowRef<T>`** (plus `SubrowRef<T>` and untyped `RowRef`). No
  lazy evaluation remains, hence the rename.
  - `IsValid` — does the row exist
  - `Value` — throws if invalid; `ValueNullable` — returns null
  - `RowRef.CreateUntyped`, `RowRef.GetFirstValidRowOrUntyped`
    (was `EmptyLazyRow.GetFirstLazyRowOrEmpty`)
  - Removed: `RawRow`/`IsValueCreated` properties, `ILazyRow`, `EmptyLazyRow`
- **`ExcelModule`:** `GetSheetNames()` → `SheetNames` property; `GetSheet<T>()` +
  new `GetSubrowSheet<T>()`; `GetSheetRaw()` → `GetRawSheet()`; new `GetBaseSheet()`;
  `RemoveSheetFromCache<T>()` → `UnloadTypedCache()`.
- **New exceptions:** `MismatchedColumnHashException`, `SheetAttributeMissingException`,
  `SheetNameEmptyException`, `SheetNotFoundException`, `UnsupportedLanguageException`.
- **RSVs resolve transparently.** (Dalamud only knows RSVs the game has already
  received; unsent/other-language ones stay as `_rsv_9999_-1_1_C0_0...`.)

### Writing your own sheet definition

```csharp
using Lumina.Excel;
using Lumina.Text.ReadOnly;

[Sheet("ActionComboRoute", 0xE732FD5B)]
public unsafe readonly struct ActionComboRoute(ExcelPage page, uint offset, uint row)
    : IExcelRow<ActionComboRoute>
{
    public uint RowId => row;

    public readonly ReadOnlySeString Name => page.ReadString(offset, offset);
    public readonly Collection<RowRef<Action>> Action =>
        new(page, parentOffset: offset, offset: offset, &ActionCtor, size: 7);
    public readonly sbyte Unknown3 => page.ReadInt8(offset + 18);
    public readonly bool Unknown4  => page.ReadPackedBool(offset + 19, 0);

    private static RowRef<Action> ActionCtor(ExcelPage page, uint parentOffset, uint offset, uint i) =>
        new(page.Module, (uint)page.ReadUInt16(offset + 4 + i * 2), page.Language);

    static ActionComboRoute IExcelRow<ActionComboRoute>.Create(ExcelPage page, uint offset, uint row) =>
        new(page, offset, row);
}
```

Notes: reading a string needs both the row offset and the string offset;
`Collection<T>` requires a *static* constructor (no lambdas — for performance);
the static `Create` is mandatory; `unsafe` exists only for the `&Ctor`.

**Subrows** implement `IExcelSubrow<T>` with an extra `ushort subrow` parameter and
a 4-arg `Create`. **Substructs** are nested readonly structs built by a `Collection<T>`
constructor.

**Raw column access** when you don't want a typed struct:

```csharp
var sheet = DataManager.GameData.GetExcelSheet<RawRow>(name: "GatheringType")!;
var name  = sheet.GetRow(1).ReadStringColumn(0);   // "Quarrying"
```

**Performance tip:** `GetFirstValidRowOrUntyped` is ~3× slower without caching.
Precompute a `RowRef.CreateTypeHash` of your candidate type list and pass it in.

### Coordinates

World ↔ map coordinate conversion is documented in
[ffxiv-datamining/docs/MapCoordinates.md](https://github.com/xivapi/ffxiv-datamining/blob/master/docs/MapCoordinates.md).

---

## 14. SeString: the game's string format

SeString (really `Utf8String` per the game's RTTI) is a null-terminated string that
carries **binary payloads**: colors, icons, links, conditionals, parameter lookups.

### Which implementation to use

**Use Lumina's:** `ReadOnlySeString` (owning), `ReadOnlySeStringSpan` (non-owning
view, ideal for parsing from a raw pointer), and `SeStringBuilder`.

**Do not use** `Lumina.Text.SeString` (old) — and prefer to avoid
`Dalamud.Game.Text.SeStringHandling.SeString` too: it's missing payload types, has
incorrect payload/macro names, and has no expression support. Many Dalamud APIs
still take it, so convert with the `ToDalamudString()` extension in
`Dalamud.Utility`.

### Payload structure

```
0x02              start byte
<macro code>      1 byte
<length>          integer expression
<expressions>     macro-specific
0x03              end byte
```

Example — `<bold(1)>Player Name<bold(0)>`:

```c
0x57 0x65 0x6C 0x63 0x6F 0x6D 0x65 0x20   // "Welcome "
0x02  0x19  0x02  0x02  0x03              // bold on
0x50 ... 0x65                             // "Player Name"
0x02  0x19  0x02  0x01  0x03              // bold off
0x21                                       // "!"
```

Three equivalent builder styles:

```csharp
// explicit
new SeStringBuilder().Append("Welcome ")
  .BeginMacro(MacroCode.Bold).AppendIntExpression(1).EndMacro()
  .Append("Player Name")
  .BeginMacro(MacroCode.Bold).AppendIntExpression(0).EndMacro()
  .Append("!").ToReadOnlySeString();

// helper
new SeStringBuilder().Append("Welcome ").AppendSetBold(true)
  .Append("Player Name").AppendSetBold(false).Append("!").ToReadOnlySeString();

// wrapper
new SeStringBuilder().Append("Welcome ").AppendBold("Player Name").Append("!")
  .ToReadOnlySeString();
```

### Expression types (by leading byte)

| Range | Kind |
| --- | --- |
| `0x01`–`0xCF` | **Integer**, value = byte − 1 (0x00 avoided; it'd be a terminator) |
| `0xD0`–`0xDF`, `0xEC` | **Placeholder** — `t_msec`, `t_sec`, `t_min`, `t_hour`, `t_day`, `t_wday`, `t_mon`, … (contextual time storage) |
| `0xE0`–`0xE5` | **Binary comparison** — `>=`, `>`, `<=`, `<`, `==`, `!=`, each followed by two sub-expressions |
| `0xE8`–`0xEB` | **Parameter** — `lnum#`, `gnum#`, `lstr#`, `gstr#` (index byte follows; 1-based) |
| `0xF0`–`0xFE` | **Variable-length integer**, byte count in the low nibble |
| `0xFF` | **String** — length expression then a nested SeString |

**Local parameters** are passed to the evaluator. **Global parameters** are
resolved automatically by the game's MacroDecoder — `gstr1` is the player name,
`gnum4` player sex, `gnum11/12` Eorzea hour/minute, `gnum68` ClassJobId,
`gnum69` level, `gnum93` TerritoryType, and ~100 more (mostly chat color config).

### Macro codes (selection)

`0x06` SetResetTime · `0x07` SetTime · `0x08` If · `0x09` Switch · `0x0A` PcName ·
`0x0B` IfPcGender · `0x0C` IfPcName · `0x0D` Josa · `0x0E` Josaro · `0x0F` IfSelf ·
`0x10` NewLine · `0x11` Wait · `0x12` Icon · `0x13` Color · `0x14` EdgeColor ·
`0x15` ShadowColor · `0x16` SoftHyphen · `0x17` Key · `0x18` Scale · `0x19` Bold ·
`0x1A` Italic · `0x1B` Edge · `0x1C` Shadow · `0x1D` NonBreakingSpace ·
`0x1E` Icon2 · `0x1F` Hyphen · `0x20` Num · `0x21` Hex · `0x22` Kilo · `0x23` Byte ·
`0x24` Sec · `0x25` Time · `0x26` Float · `0x27` Link · `0x28` Sheet ·
`0x29` String · `0x2A` Caps · `0x2B` Head · `0x2C` Split · `0x2D` HeadAll ·
`0x2E` Fixed · `0x2F` Lower · `0x30`–`0x34` Ja/En/De/Fr/Ch Noun ·
`0x40` LowerHead · `0x41` SheetSub · `0x42` SwitchPlatform · `0x48` ColorType ·
`0x49` EdgeColorType · `0x4A` Ruby · `0x50` Digit · `0x51` Ordinal · `0x60` Sound ·
`0x61` LevelPos

### Evaluating

```csharp
var template = ReadOnlySeString.FromMacroString(
    "The current time is: <settime(lnum1)><num(t_hour)>:<num(t_min)>");
var result = SeStringEvaluator.Evaluate(template, [1743880207]);
// -> "The current time is: 21:10"
```

`ISeStringEvaluator` (stable since API 13; supports SheetSub and SwitchPlatform).

- **Always passed through:** Hyphen, Icon, Icon2, Link, NewLine, NonBreakingSpace,
  SoftHyphen, Sound, Wait
- **Expressions evaluated, payload preserved** (formatting): Color, EdgeColor,
  ShadowColor, Bold, Italic, ColorType, EdgeColorType
- **Not yet supported, passed through:** Byte, ChNoun, Edge, Josa, Josaro, Key,
  Ruby, Scale, Shadow, Time

### Rendering

```csharp
ImGuiHelpers.SeStringWrapped(readOnlySeString);          // ReadOnlySeString / Span
ImGuiHelpers.CompileSeStringWrapped("<bold(1)>Hi!");     // macro string
```

These handle soft hyphens and formatting correctly.
`ToString()` strips all payloads except NewLine, NonBreakingSpace and Hyphen —
only safe when you know there are no macro payloads.
`ToMacroString()` gives you the macro-string representation.

API 14 additions: `ITextureProvider.CreateTextureFromSeString()` renders a SeString
to a texture. When passing `SeStringDrawParams.TargetDrawList`, you must also set
`Font`, `FontSize`, and `ScreenOffset` (needed to render outside an ImGui draw context).

**Tooling:** the SeString Creator widget in `/xldata` lets you experiment with
macro strings and inspect the evaluated result live.

---

## 15. Configuration & file storage

```csharp
using Dalamud.Configuration;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool SomeSetting { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
```

```csharp
Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
```

Stored under `PluginInterface.ConfigFile` / `ConfigDirectory` (named after your
`InternalName` — another reason it can't change).

`Version` exists for your own migrations: bump it and translate old shapes on load.

### Reliable file storage (API 14+)

For data you cannot afford to lose to a power cut or bad disk:

```csharp
[PluginService] internal static IReliableFileStorage Files { get; private set; } = null!;
```

All reads/writes are mirrored to a secondary virtual filesystem and recovered
automatically. Because everything is duplicated, **there is an enforced size
limit** — don't put large blobs here.

---

## 16. Plugin-to-plugin IPC

Dalamud's "CallGate" lets plugins expose typed functions and events to each other
across assembly-load-context boundaries. Supports up to 8 arguments + return.

### Provider (the plugin exposing the API)

```csharp
private ICallGateProvider<string, int, bool>? _doThing;

public void Register()
{
    _doThing = PluginInterface.GetIpcProvider<string, int, bool>("MyPlugin.DoThing");
    _doThing.RegisterFunc(DoThing);          // callable
    // or RegisterAction(...) for void
}

private bool DoThing(string name, int count) => true;

public void Dispose()
{
    _doThing?.UnregisterFunc();              // or UnregisterAction()
}
```

Providers can also broadcast: `_evt.SendMessage(arg1, arg2)`.
`SubscriptionCount` tells you how many listeners exist; `GetContext()` returns the
calling `IpcContext`.

### Subscriber (the plugin consuming it)

```csharp
var sub = PluginInterface.GetIpcSubscriber<string, int, bool>("MyPlugin.DoThing");

if (sub.HasFunction)
    var ok = sub.InvokeFunc("thing", 3);

// Events:
sub.Subscribe(OnThingHappened);
// Dispose:
sub.Unsubscribe(OnThingHappened);
```

`HasAction` / `HasFunction` let you degrade gracefully when the other plugin isn't
installed. Wrap invocations in try/catch — the provider may vanish mid-session.

### Conventions

- Name gates `PluginName.FunctionName` (or `PluginName.Category.Function`).
- Version your gate: expose `MyPlugin.ApiVersion` returning `(int major, int minor)`
  and check it before calling anything else.
- `PluginInterface.ActivePluginsChanged` (API 13+ fires on individual
  Loaded/Unloaded with `PluginListInvalidationKind`) is how you notice a peer
  appearing or disappearing. Note that enabling/disabling a *collection* triggers
  one event per plugin.
- Use `PluginInterface.InstalledPlugins` (`IExposedPlugin`) to detect peers.

> **Rule:** a mainline plugin **may not hard-depend on a plugin that violates the
> guidelines.** Soft/optional integration is fine.

---

## 17. Threading rules

This is where most crashes come from.

### The framework thread is the game's main thread

- `IFramework.Update` fires once per frame **on the main thread**.
- Hook detours run on whatever thread the game called them from — usually main.
- ImGui `Draw` callbacks run on the render path.

### Since API 12, this is enforced

`IObjectTable` and certain `IClientState` properties **throw
`InvalidOperationException`** when touched off the main thread. This is deliberate.

### Getting onto the main thread

```csharp
await Framework.RunOnFrameworkThread(() => DoGameThing());
await Framework.Run(() => DoGameThing(), cancellationToken);
await Framework.RunOnTick(() => DoGameThing(), delay: TimeSpan.FromSeconds(1));
await Framework.RunOnTick(() => DoGameThing(), delayTicks: 5);
await Framework.DelayTicks(10, cancellationToken);

if (Framework.IsInFrameworkUpdateThread) { /* already safe */ }
```

Also available: `GetTaskFactory()` (a `TaskFactory` bound to the framework thread)
and `CreateDebouncer(delay, action)`.

### Other rules

- **Never block the framework thread.** No `.Result`, no `.Wait()`, no sync I/O,
  no network calls. Use `Task` + `RunOnFrameworkThread` to come back.
- **Never block inside a hook.** Detours are on the critical path.
- Off-thread work that needs a texture must use `ISharedImmediateTexture.RentAsync()`.
- `IFramework.IsFrameworkUnloading` tells you when to stop scheduling work.
- Corrupt-state exceptions are no longer caught (API 12+). An access violation
  crashes the process — validate your pointers.

---

## 18. Reverse engineering workflow

The docs are explicit that they can't teach you RE — but here's the shape of it.

### The problem

FFXIV ships as stripped compiled machine code (`ffxiv_dx11.exe`). No source, no
symbols. FFXIVClientStructs is the community's accumulated map of it.

### Static analysis

Tools: [IDA Pro](https://hex-rays.com/) (gold standard), [Ghidra](https://ghidra-sre.org/)
(free, powerful, clunky), [Binary Ninja](https://binary.ninja/). Most Dalamud
tooling targets IDA or Ghidra; functionally either is fine.

**First step after loading `ffxiv_dx11.exe`:** run the
[FFXIVClientStructs data files/scripts](https://github.com/aers/FFXIVClientStructs/tree/main/ida).
This populates your database with community findings for the current version and
saves enormous time.

Static analysis tools infer arguments from the
[x64 calling convention](https://learn.microsoft.com/en-us/cpp/build/x64-calling-convention).
Unknown args show as `a3`, etc.

### Dynamic analysis

Tools: [Cheat Engine](https://www.cheatengine.org/), [x64dbg](https://x64dbg.com/),
[ReClass.NET](https://github.com/ReClassNET/ReClass.NET). Find interesting
addresses, set breakpoints, watch what writes to a struct field.
[pohky's XivReClassPlugin](https://github.com/pohky/XivReClassPlugin) wires ReClass
into the ClientStructs database.

Most people use both approaches — each supplies context for the other.

### Offsets vs signatures

- Offset: `ffxiv_dx11.exe+4BC200` (or `1404BC200` = `/BASE 0x140000000` + offset).
  **Changes every game version. Never usable in a plugin.**
- Signature: a byte pattern uniquely identifying the function or a call to it.
  Stable across patches unless SE changes that code.

Generate with [SigMaker-x64](https://github.com/Caraxi/SigMaker-x64) or by hand.

### Then

Either **hook** it (intercept/modify/cancel/observe) or **delegate** to it (call it
yourself). See §10. For structures, port the C struct to a C# `struct` with
explicit layout — or fall back to pointer math and `Marshal` if the layout isn't
known yet. The docs encourage writing out the struct properly instead.

### Server protocol

The Dalamud project deliberately stays away from client↔server packet work (see
restrictions). `IGameNetwork` was **removed in API 13** — opcodes change every
patch and the packet data proved unreliable. **Hook the functions that process
the data instead.** For protocol understanding,
[Sapphire](https://github.com/SapphireServer/Sapphire) is the reference.

### Give back

Plugin devs who reverse things are encouraged to upstream findings to
FFXIVClientStructs.

---

## 19. Debugging, logging, hot reload

### Logging

```csharp
Log.Verbose("...");
Log.Debug("...");
Log.Information("Health updated to {health}.", hp);   // Serilog-style templates
Log.Warning("...");
Log.Error(ex, "Failed to do the thing.");
Log.Fatal("...");
```

Output goes to `/xllog` and the on-disk log, tagged with your `InternalName`.

### Hot reload (dev plugin locations)

Dalamud Settings → Experimental → **Dev Plugin Locations**. Add either your
plugin's output folder or the DLL itself. If you add a folder, Dalamud loads every
DLL in it. Rebuild → the plugin reloads.

### Attaching a debugger

The game ships with anti-debug protection on.

1. In game: `/xldev` → **Dalamud → Enable AntiDebug** (toggles it off; persisted
   across launches).
2. Visual Studio: Debug → Options → Debugging → General → **uncheck "Enable Just My
   Code"**.
3. Debug → **Attach to Process** (Ctrl+Alt+P) → select `ffxiv_dx11.exe`.
4. In "Attach to", click Select and check **both** "Managed (.NET Core, .NET 5+)"
   **and** "Native". Without both you only get half the picture.
5. Debug → Windows → **Exception Settings** → uncheck "Common Language Runtime
   Exceptions" (otherwise you'll break on every internal exception).

Debugger attach is supported **only for debugging your own plugins**.

### Reporting a crash

Include all of:
- **Call stack** — right-click the Call Stack window → Select All → Copy
- **Exception** — Debug → Windows → Output, copy the last error
- **Base address** — Debug → Windows → Modules → `ffxiv_dx11.exe` → right-click →
  Copy. **Must be from the same game session** — it changes every restart.
- **Dalamud log**

### Performance

`/xldev` → **Plugins → Open Plugin Stats**. This is the first stop for "is my
plugin the thing making the game stutter". You should aim to not measurably impact
game performance.

### Exception handling

Since API 14, .NET errors that used to CTD now surface in the crash handler window
properly, and windows that throw in `Draw()` stop rendering with an error message
instead of spamming.

---

## 20. Plugin manifest & metadata

The manifest controls how your plugin appears in `/xlplugins`. Three ways to
declare it — pick one.

### A. JSON / YAML file named after your InternalName

`MyPlugin.json` or `MyPlugin.yaml`. YAML uses `snake_case` where JSON uses
`CamelCase` (`RepoUrl` → `repo_url`).

```yaml
name: Test Plugin
author: You
punchline: Does nothing! # one-line summary
description: |-
  This is a test plugin - this first line is a summary.

  Down here is a more detailed explanation of what the plugin
  does, manually wrapped to make sure it stays visible in the
  installer.
repo_url: https://example.com
```

Minimum for DalamudPackager:

```json
{
  "Name": "My Awesome Plugin",
  "Author": "You!",
  "Punchline": "An awesome plugin that does cool things.",
  "Description": "Did you ever feel like your game could be even more awesome?",
  "RepoUrl": "https://github.com/AwesomePluginDev/MyAwesomePlugin"
}
```

### B. csproj properties (API 14+ SDK — no JSON file needed)

```xml
<PropertyGroup>
    <Author>your name here</Author>
    <Name>Sample Plugin</Name>
    <InternalName>SamplePlugin</InternalName>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <MinimumDalamudVersion>13.0.0</MinimumDalamudVersion>
    <Punchline>A short one-liner that shows up in /xlplugins.</Punchline>
    <Description>A description that shows up in /xlplugins. List any major slash-command(s).</Description>
    <ApplicableVersion>2025.10.30.0000.0000</ApplicableVersion>
    <RepoUrl>https://github.com/goatcorp/SamplePlugin</RepoUrl>
    <Tags>sample;plugin;goats</Tags>
    <CategoryTags>debug;test</CategoryTags>
    <DalamudApiLevel>14</DalamudApiLevel>
    <LoadRequiredState>1</LoadRequiredState>
    <LoadSync>true</LoadSync>
    <CanUnloadAsync>true</CanUnloadAsync>
    <LoadPriority>1</LoadPriority>
    <ImageUrls>https://.../image1.png;https://.../image2.png</ImageUrls>
    <IconUrl>https://.../icon.png</IconUrl>
    <Changelog>CHANGES!</Changelog>
    <AcceptsFeedback>true</AcceptsFeedback>
    <FeedbackMessage>Be nice.</FeedbackMessage>
</PropertyGroup>
```

### Field reference

**Required:** `Name`, `Author`, `Description`, `Punchline`

**Optional:** `ApplicableVersion`, `RepoUrl`, `Tags`, `CategoryTags`,
`LoadRequiredState`, `LoadSync`, `CanUnloadAsync`, `LoadPriority`, `ImageUrls`,
`IconUrl`, `Changelog`, `AcceptsFeedback`, `FeedbackMessage`

**Auto-filled by DalamudPackager — do not set these yourself:**
`AssemblyVersion`, `InternalName`, `DalamudApiLevel`

**Never set manually:** `Dip17Channel` and similar plumbing fields.

### Changelogs — resolution order

1. `changelog` field in `manifest.toml` (in DalamudPluginsD17)
2. Pull request description
3. `Changelog` key in your plugin manifest

First available wins. **For the changelog to show in the plugin installer it must
be in `manifest.toml` or the plugin manifest — a PR-description changelog is not
displayed there.**

### Discord announcement

Plugin updates are auto-posted to the XIVLauncher & Dalamud Discord. To suppress
the bot post (e.g. you want to write your own), **start your PR description with
the word `nofranz`.** The installer changelog still shows.

### API 15: your distributed manifest must be accurate

Previously Dalamud overwrote the `InternalName.json` inside the plugin zip with the
repo's manifest on install. **It no longer does.** Your zip must contain a manifest
and it must be correct.

### Testing plugins (API 14+)

Testing plugins must set `TestingDalamudApiLevel` in their manifest.

---

## 21. Publishing to the official repo (D17)

### How the pipeline works

- **All mainline plugins are open source.** No closed-source submissions.
- You submit a **commit hash** — a cryptographic pointer to an exact version of
  your source. Change the code, get a new hash, get re-reviewed.
- **Plogon** (the cloud build system) downloads that source, builds it, and emits a
  **diff of everything that changed**. The builder has **no internet access**, so
  you cannot pull extra code at build time.
- A member of the **Plugin Approval Committee** reviews the diff.

Net effect: the binary on a user's machine provably came from a public commit that
a human reviewed.

### Directory structure in D17

```
<track>/MyPluginName/
 |- manifest.toml
 |- images/
     |- icon.png
     |- image1.png   [OPTIONAL, up to image5.png]
```

- `icon.png`: **1:1 aspect ratio, between 64×64 and 512×512.**
- Up to five optional marketing images, `image1.png`–`image5.png`.

### `manifest.toml`

```toml
[plugin]
repository = "https://github.com/goatcorp/SamplePlugin.git"
commit = "765d9bb434ac99a27e9a3f2ba0a555b55fe6269d"
owners = ["goaaats"]
project_path = "SamplePlugin"
changelog = "Added Herobrine"
```

(`maintainers` is also supported alongside `owners`.)

### Tracks

- **`testing/live/`** — experimental/new versions. Sub-tracks exist occasionally
  (e.g. `testing/net8`), documented in the D17 README and site news.
- **`stable/`** — public, supported, relatively bug-free builds.

**New plugins MUST be submitted to `testing/live/` first.** No exceptions.

### Submission rules

- **One plugin per PR.** One PR per branch (protects against merge conflicts).
- Updates: new PR from a new branch, changing at least the `commit` field. The
  commit must be publicly reachable in the repo but need not be on any branch.
- **Changing tracks:** copy or move the manifest directory. No version bump or
  commit change required. When a plugin exists in multiple targetable tracks, the
  **highest `AssemblyVersion`** wins — which is why devs often leave old versions
  in tracks to re-enable them quickly.
- Ask the bot to rebuild by commenting **`bleatbot, rebuild`**.
- Disclose AI usage in the PR description if applicable (see §23).

### Approval

- **New plugins:** the ~6-person committee votes. **4 yes votes** approves it. Any
  member may **veto**, blocking merge until resolved (has never happened).
- **Updates:** a **single** committee member's approval suffices.
- Reviews check guidelines compliance, informal code review, clean install,
  correct config-window behavior, working base functionality, valid JSON, no
  personal-data upload, and technical criteria.
- **Expect a new plugin to sit in the queue for a week or more.** Everyone doing
  this is a volunteer.

### Technical criteria (from the D17 README)

- `images/icon.png`, 64×64 to 512×512
- Regular windows must use the Dalamud Windowing API
- **Version numbers must not be timestamps or continually-increasing build
  numbers.** Use real semantic versions.

### Emergency kill switch

To globally disable a broken plugin version, open a PR against
[`bannedplugin.json`](https://github.com/goatcorp/DalamudAssets/blob/master/UIRes/bannedplugin.json)
in DalamudAssets:

```json
{ "Name": "MyPlugin", "AssemblyVersion": "1.2.3.0", "Reason": "Crashes on zone change" }
```

- `Name` — the internal/assembly name (for **custom repo** plugins, the **uppercase
  SHA256** of the internal name)
- `AssemblyVersion` — bans that version **and all below**. Omit it to ban all
  versions permanently.
- Must be opened by the maintainer (or, for custom repos, someone directly
  associated with it); the Dalamud team verifies identity.

Unban by publishing a version **greater than** the banned one. Entries are
generally not removed. Banned plugins show a warning and won't load, but data is
preserved and nothing is uninstalled.

This is a **safety tool, not a moderation tool.** The Dalamud team reserves the
right to ban mainline plugins without consent for game-breaking or user-safety
issues (with a reasonable attempt to contact the maintainer), and will **not** do
so for custom repo plugins.

### Abandonment & adoption

- Want out? Say so in Discord `#plugin-dev` and it goes up for adoption.
- Behind the current API level **> 3 months**: others may adopt after making
  reasonable efforts to reach you.
- Behind **> 6 months**: others may adopt without your permission.
- Adopting = announce in `#plugin-dev`, PR yourself as owner, update the repo URL.

---

## 22. Custom repositories

> The Dalamud project offers **minimal support** for custom repos, including
> setting one up. Strongly consider mainline instead.

A repository is just a URL returning a JSON array of store entries over plain HTTP
`GET`. Query parameters are allowed; **authentication/authorization is not
supported.**

```json5
[
  {
    Author: 'A Plugin Developer',
    Name: 'A Custom Plugin',
    Description: 'A long description shown when the installer entry is expanded.',
    InternalName: 'ACustomPlugin',
    AssemblyVersion: '1.0.0.0',
    TestingAssemblyVersion: null,
    RepoUrl: 'https://github.com/APluginDeveloper/ACustomPlugin',
    ApplicableVersion: 'any',
    DalamudApiLevel: 15,
    Punchline: 'A short blurb about what this plugin is.',
    IsHide: false,
    IsTestingExclusive: false,
    DownloadLinkInstall: 'https://example.com/path/to/release/output.zip',
    DownloadLinkTesting: 'https://example.com/path/to/testing/output.zip',
    DownloadLinkUpdate: 'https://example.com/path/to/release/output.zip',
    LastUpdate: '1701231234',
  },
]
```

All plugin-manifest keys are supported, plus store-only keys:
`IsHide` (hide without removing), `DownloadCount`, `DownloadLinkInstall`,
`DownloadLinkUpdate` (separate URL, useful for update-count tracking), `ImageUrls`,
`IconUrl`.

Testing keys: `IsTestingExclusive`, `TestingAssemblyVersion` (only used if greater
than the release version), `TestingChangelog`, `TestingDalamudApiLevel`,
`DownloadLinkTesting`.

---

## 23. Rules, restrictions & the AI policy

These aren't bureaucracy — they're the reason the ecosystem still exists. Read
them before you write code, not after.

### The core principle

> Dalamud plugins should **enhance** the experience, not radically alter it. Your
> plugin should not do anything a human player could not do.

### Hard restrictions

Your plugin must not:

- **Interact with game servers automatically** (polling or requesting without
  direct user interaction) or **outside spec** (submitting things not possible by
  normal means).
- **Augment, alter, or interfere with combat** — unless it only re-presents
  information about your own party/alliance that's already available.
  **Contact the approval team *before* building anything combat-adjacent.**
  Unannounced combat plugin submissions are not accepted. Existing exceptions are
  grandfathered; new ones won't be.
- **Interfere with Square Enix's monetary interests** (e.g. granting Mog Station
  items, avoiding Fantasia).
- **Provide parsing, raid logging, DPS meters**, or any information beyond what
  players traditionally have.
- **Collect account IDs of any character but your own**, in any form, regardless
  of intended use or whether it's user-visible.
- **Hard-depend on a plugin that violates the guidelines.**
- **Be useful only in out-of-spec scenarios** (out-of-bounds areas etc.) — even if
  it doesn't directly break a rule, it tacitly encourages unsupportable behavior.
- **Give any advantage in competitive or PvP environments.**

### Known non-starters

Emote/expression looping · skip cutscenes · skip dialog boxes · automated crafting ·
autoroll on loot · friend list login/logout alerts (technically impossible anyway) ·
visible AoE markers for non-telegraphed mechanics · camera zoom beyond normal
bounds · FFLogs integration · damage parsers / ACT-as-a-plugin · avoiding Fantasia ·
additional XIVCombo-style combos (XIVCombo is specifically curated and
PAC-greenlit) · AoE recoloring · **anything in PvP**.

Note: enhancing loot UI *without* automation (e.g. "Select Next Loot Item Tweak")
is allowed. The line is automation, not UI improvement.

### Backend servers — allowed, with conditions

If your plugin talks to a server you run:

- **Send the minimum data necessary.** Hash player identifiers (Content ID, name)
  **client-side** where feasible so a breach doesn't leak them.
- **Telemetry is opt-in.** Users must get a chance to review what's collected and
  why (a config option or welcome-wizard forced choice is fine).
- Extra collection must serve the **public interest** — improve the plugin, produce
  public statistics/dashboards, or otherwise augment the game.
- Analytics must use a **pseudo-random identifier or none**. It must not derive
  from personal information and must be **resettable client-side at any time**.
  Design so users can't be deanonymized even with full raw dataset access.
- Data must be **topical** to the plugin. (A Party Finder plugin may correlate face
  type with Ultimate clears; it may not just record popular face types.)
- **Never expose a list of plugin users** or make it easy to test whether a
  specific user runs a plugin. Opt-in public directories are OK if risks are
  disclosed.
- **HTTPS/TLS with a trusted CA** (Let's Encrypt etc.) is mandatory.
- **Connect via DNS hostname, not IP address.**

Strong recommendations (not rules): allow user-defined backend servers; open-source
and easily self-hostable server; dual-stack IPv4/IPv6 with IPv6-aware rate limits;
WebSocket retry logic; version checking between client and server plus MOTD /
outage / deprecation notifications.

Appropriateness is judged case by case by the Plugin Approval Committee. Expect
design feedback from PAC *and* the community.

### AI usage policy (official repo submissions only)

Applies to DalamudPluginsD17 submissions. **Does not apply** to contributions to
Dalamud/XIVLauncher itself.

**TL;DR:** Use AI if it helps, but you must **understand, test, and be able to
explain** your code. **Disclose your level of AI use.** Entirely AI-generated
submissions are auto-rejected; undisclosed AI use gets you banned.

Disclosure levels (from [AI-DECLARATION.md](https://ai-declaration.md/)):

| Level | Meaning | Disclose? |
| --- | --- | --- |
| **None** | No AI at any point | No |
| **Hint** | Autocomplete / inline suggestions only | No |
| **Assist** | Human-led; AI on demand for specific tasks | **Yes** |
| **Pair** | Active collaboration, roughly equal contribution | **Yes** |
| **Copilot** | AI implements, human plans and reviews | **Yes** |
| **Auto** | AI acts autonomously with minimal direction | **Yes** |

Requirements: personally test before submitting; the answer to "why did you
implement it this way?" must never be "I'm not sure, the AI did it"; **verify AI
output — it frequently gets Dalamud and adjacent APIs wrong**; be receptive to
review feedback.

Enforcement: entirely AI-generated plugin → auto-reject (twice → ban). Undisclosed
AI use in a demonstrably AI-written submission → ban. AI-generated mistakes with
clear human intent → closed with an opportunity to fix and resubmit.

**Assets:** disclose AI-generated icons/images/audio/textures **in the plugin
description** (users interact with them directly and community sentiment is often
negative). For icons the team explicitly prefers a **crude MS Paint icon over an
AI-generated one** and may ask you to replace it; ask in Discord and someone will
likely make you one. AI-generated audio should be toggleable or replaceable.

**Translations:** AI-assisted is acceptable, especially as placeholders — but
consult native speakers, seek community translators, use platforms like Crowdin
that support human review, and accept corrections.

### If you're unsure

**Ask in the [Dalamud Discord](https://discord.gg/holdshift) before you start
building.** PAC/staff will evaluate the idea. Nobody enjoys rejecting a finished
plugin. There's a developer role that unlocks dev channels.

### Also applies

The [Code of Conduct](https://dalamud.dev/code-of-conduct) covers all community
spaces — Discord and GitHub included.

---

## 24. Versions, API levels & migration

### Channels

| Channel | Branch | API | Stability | For |
| --- | --- | --- | --- | --- |
| **Release** | `master` | 15 | Highest | Most users (default) |
| **Canary** | `master` | 14 | Very high | Small auto-assigned subset — catches release bugs early |
| **Staging (`stg`)** | `master` | 15 | Medium | Core/plugin devs, testers |

Switch via `/xldev` → **Dalamud → Branch Switcher**. Channels ≠ branches; all three
track `master` at different cadences.

### API levels

The API level increments on any breaking API change. From v9 onward, **API level
always equals the major version**. A plugin compiled for an older API level will
**not load**.

### When the game patches

1. Wait for Dalamud to update for the new game version.
2. Update your plugin to the latest API, verify non-Dalamud interop (signatures,
   struct offsets) still works.
3. Repackage and resubmit.

Dalamud is disabled after every patch until verified. **Manual injection to bypass
this is developer-only and unsupported** — you will crash and no one will help you.
To help test, get the tester role in Discord.

To stay ahead of breaking changes: be in the Discord. After your first accepted
submission you get the Plugin Developer role and get pinged on breaking changes.

### Migration cheat sheet by API level

**→ API 15 (7.5, .NET 10)**
- `IAsyncDalamudPlugin` available (experimental, stable interface)
- `IChatGui` events take `IChatMessage` / `IMutableChatMessage` / `IHandleableChatMessage`;
  `XivChatType` properly parsed with `XivChatRelationKind SourceKind`/`TargetKind`;
  values >110 no longer valid
- `ImRaii` `IEndObject` removed → `ref struct`s; `Group`/`Tooltip`/`Disabled` have no
  bool conversion; new `Header`, `PushColor`, `Columns`, `ChildFrame`
- `IClientState.LocalPlayer` and `LocalContentId` **removed** → use `IPlayerState`
  (or `IObjectTable.LocalPlayer`)
- `ICharacter.Customize` is `Span<byte>` not `byte[]`
- `IPartyMember.ContentId` is `ulong`
- 32-bit members removed from `BaseAddressResolver` / `ISigScanner`
- `IAddonLifecycle` / `IAgentLifecycle` gain `PreventOriginal()`
- `IGameGui.OpenMapWithMapLink(uint territory, uint map, Vector3 worldPos)` overload
- **Distributed zip manifest must be accurate** (no longer overwritten on install)
- Many enums re-synced to their FFXIVClientStructs counterparts (ObjectKind,
  BattleNpcSubKind, FateState, all JobGauge enums, NamePlateKind, PartyFinder
  enums, GameInventoryType, AtkValueType, PlayerAttribute, XivChatRelationKind…);
  `HoverActionKind` renamed to `DetailKind`
- Noto Sans fonts unified → `NotoSansCjkRegular`/`NotoSansCjkMedium` with `FontNo`
- Known issue: plugins that fail to load during update show as not installed

**→ API 14 (7.4, .NET 10)**
- **Requires .NET SDK 10.0.101+** and VS 2026 / Rider 2025.3
- All service interfaces moved to `Dalamud.Plugin.Services`
  (`ISigScanner`, `ITargetManager`, `ISelfTestRegistry`)
- New services: `IPlayerState`, `IUnlockState` (experimental), `IReliableFileStorage`
- `IClientState.LocalPlayer` → obsolete (`IObjectTable.LocalPlayer`);
  `LocalContentId` → obsolete (`IPlayerState.ContentId`)
- `IDalamudPluginInterface` implements `IServiceProvider`
- `IGameGui.AgentUpdate` event; `IUiBuilder.PluginUISoundEffectsEnabled`
- `IAddonLifecycle` reworked (vtable replacement): new Open/Close/Show/Hide/Move/
  MouseOver/MouseOut/Focus events; **no longer recursion-safe**; `ICloneable`,
  `AddonDrawArgs`/`AddonFinalizeArgs`/`AddonUpdateArgs` and the public `AddonArgs`
  constructor removed; `AddonReceiveEventArgs.Data` → `AtkEventData`
- Enumerable services return struct enumerators; several data classes became
  `readonly struct`; `Status` gains `IStatus`; `Fate.IsValid` removed
- Version info via `IDalamudPluginInterface.GetDalamudVersion()` (Utils removed)
- `SharpDX.*` removed → use TerraFX.Interop.Windows
- `SeStringRenderer`: `ITextureProvider.CreateTextureFromSeString()`;
  `TargetDrawList` now requires `Font`, `FontSize`, `ScreenOffset`
- Testing plugins must set `TestingDalamudApiLevel`
- csproj manifest properties supported
- `targets/` folder removed
- New `Dalamud.Bindings.ImAnim`
- Font Awesome 6.4.2 → 7.1.0
- Known issue: harmless `MSB3277 WindowsBase` version conflict warning

**→ API 13 (7.3, .NET 9)**
- ImGui bindings: `ImGuiNET`→`Dalamud.Bindings.ImGui`, `ImPlotNET`→`Dalamud.Bindings.ImPlot`,
  `ImGuizmoNET`→`Dalamud.Bindings.ImGuizmo`; `ImGuiHandle`→`Handle`; PascalCase enums;
  `out` params → pointer/`ref`; internals under `ImGuiP`
- **`IGameNetwork` removed** — hook the processing functions instead
- `AddonArgs.Addon` → `AtkUnitBasePtr`; `GetAddonByName`/`GetAgentById`/`GetUIModule`
  return the new wrapper ptr types
- `AddChatLinkHandler`/`RemoveChatLinkHandler` moved to `IChatGui`
- `IDtrBar.OnClick` receives `AddonMouseEventData`; `TriggerClickAction` removed
- `IObjectTable` gains `PlayerObjects`/`CharacterManagerObjects`/`ClientObjects`/
  `EventObjects`/`StandObjects`/`ReactionEventObjects`
- `IUiBuilder` gains `DefaultGlobalScaleChanged`/`DefaultFontChanged`/`DefaultStyleChanged`
  and font size/handle properties
- `ISeStringEvaluator` no longer experimental
- Lumina `ToString()` now returns text only; macro output moved to `ToMacroString()`
- `ItemPayload.ItemKind` moved to `Dalamud.Utility`

**→ API 12 (7.2, .NET 9)**
- `IObjectTable` + some `IClientState` properties **throw off the main thread**
- Corrupt-state exceptions no longer caught by the CLR
- New `ISeStringEvaluator` (experimental at the time)
- `InteropGenerator.Runtime.dll` auto-referenced by the SDK
- Textures normalized to `ISharedImmediateTexture` (notifications, title screen menu)
- Job gauge enums SNAKE_CASE → PascalCase; `SummonerGauge`/`DarkKnightGauge` fields
- `Easing#Value` deprecated → `ValueClamped`/`ValueUnclamped`
- `PluginLoadReason` is now `[Flags]`
- `GameInventoryItem.ItemId` includes offsets; `BaseItemId` is the raw ID

**→ API 11 (7.1, .NET 8)** — Lumina 5 (see §13); SeString renderer added;
`IClientState.ClassJobChanged`/`LevelChanged`; `IGameInventory.GetInventoryItems`;
`ITextureProvider.ConvertToKernelTexture`

**→ API 10 (7.0, .NET 8)** — everything became interfaces
(`DalamudPluginInterface`→`IDalamudPluginInterface`, `UiBuilder`→`IUiBuilder`,
`GameObject`→`IGameObject`, `PlayerCharacter`→`IPlayerCharacter`, …);
`ITextureProvider` reworked around `ISharedImmediateTexture`;
`ITextureReadbackProvider` added; **`[RequiredVersion]` removed**;
`DalamudTextureWrap` class removed; new `IConsole` and `IMarketBoard`

### FFXIVClientStructs breaking changes

CS ships its own per-patch breaking changes, documented at
`https://ffxiv.wildwolf.dev/docs/breaking/<patch>.html`.

### Legacy error: "Nothing inherits from IDalamudPlugin"

Caused by shipping Dalamud's own DLLs next to your plugin. If you're not on the
SDK, add `<Private>false</Private>` to every `<Reference>`, clean the output
folder, rebuild. (Using Dalamud.NET.Sdk avoids this entirely.)

---

## 25. Recipes: common plugin patterns

### React to zone changes

```csharp
ClientState.TerritoryChanged += OnTerritoryChanged;
// Dispose: ClientState.TerritoryChanged -= OnTerritoryChanged;

private void OnTerritoryChanged(uint territoryId)
{
    if (DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
        Log.Information("Now in {place}", row.PlaceName.Value.Name.ToString());
}
```

Richer variant: subscribe to `ZoneInit` for `ZoneInitEventArgs` (RowRefs +
`IReadOnlyList<FestivalEntry> ActiveFestivals` as of API 15).

### Only act while in a duty / out of combat

```csharp
if (Condition[ConditionFlag.BoundByDuty]) return;
if (Condition[ConditionFlag.InCombat])   return;
if (!ClientState.IsClientIdle(out var blockingFlag)) { /* blockingFlag says why */ }
```

### Iterate nearby objects (main thread only!)

```csharp
foreach (var obj in ObjectTable.PlayerObjects)
{
    if (obj.ObjectKind != ObjectKind.Player) continue;
    var dist = Vector3.Distance(obj.Position, ObjectTable.LocalPlayer!.Position);
}
```

### Throttle expensive work

```csharp
private DateTime _next = DateTime.MinValue;

private void OnFrameworkTick(IFramework framework)
{
    if (DateTime.UtcNow < _next) return;
    _next = DateTime.UtcNow.AddSeconds(1);
    DoExpensiveThing();
}
```

Or use `Framework.CreateDebouncer(TimeSpan.FromMilliseconds(250), action)`.

### Background work that touches the game

```csharp
_ = Task.Run(async () =>
{
    var data = await httpClient.GetStringAsync(url);           // off-thread OK
    await Framework.RunOnFrameworkThread(() => Apply(data));   // back on main
});
```

### Add a tooltip to an existing game UI element

Combine AddonLifecycle (know when the addon exists) + AddonEventManager (attach the
event) + `AtkStage.TooltipManager`. Full example in §11.

### Read a game Excel sheet with a subrow

```csharp
var sheet = DataManager.GetSubrowExcelSheet<SatisfactionSupply>();
foreach (var subrow in sheet.GetRow(rowId))
    Log.Debug("{item}", subrow.Item.Value.Name.ToString());
```

### Expose a versioned IPC API

```csharp
private ICallGateProvider<(int, int)>? _apiVersion;
private ICallGateProvider<string, bool>? _doThing;

_apiVersion = PluginInterface.GetIpcProvider<(int, int)>("MyPlugin.ApiVersion");
_apiVersion.RegisterFunc(() => (1, 0));
_doThing = PluginInterface.GetIpcProvider<string, bool>("MyPlugin.DoThing");
_doThing.RegisterFunc(DoThing);
```

Consumer:

```csharp
try
{
    var (major, _) = PluginInterface.GetIpcSubscriber<(int, int)>("MyPlugin.ApiVersion").InvokeFunc();
    if (major != 1) return;  // incompatible
    PluginInterface.GetIpcSubscriber<string, bool>("MyPlugin.DoThing").InvokeFunc("x");
}
catch (Exception) { /* not installed / not ready */ }
```

### Ship an embedded resource instead of a loose file

```csharp
var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, "goat.png");
```

works, but the SamplePlugin comment is right: for production you usually want an
embedded resource loaded from the manifest stream.

---

## 26. Glossary & internal names

### Technical terms

| Term | Meaning |
| --- | --- |
| `AccountId` | Unique ID shared by all characters on an account, **valid only for the current game session**. Used by the blacklist system. **Collecting other players' account IDs is forbidden.** |
| `Addon` | A UI window — another name for `AtkUnitBase`. Probably named after WoW's AddOns. |
| `Agent` | The controller that manages Addons, handling events and callbacks. |
| `Atk` | FFXIV's UI library. Presumed "Addon Toolkit". |
| `BNpc` / `BattleNpc` | NPCs with combat abilities (enemies, pets). |
| `ContentId` | Unique ID for a player character. Used for local settings, crafted item signatures, Eternity Ring. |
| `ENpc` / `EventNpc` | EventHandler-controlled NPCs (quest givers, vendors). |
| `EObj` / `EventObject` | EventHandler-controlled interactables (entrances, aether currents). |
| `EntityId` | Unique ID for an entity in the current territory. Empty = `0xE0000000`. Formerly `ObjectId`. |
| `Gfd` | Gaiji Fontdata — sizes/positions for the chat fonticon sprite textures. |
| `Pet` | Carbuncle, Eos/Selene, Rook/Queen, Lilybell. **Not** chocobo companions or minions. |
| `Rapture` | The internal codename for FFXIV. |

### Player-facing system → internal name

| System | Internal |
| --- | --- |
| Accessories | Ornament |
| Adventurer Plate | CharaCard |
| Aetherial Reduction | Purify |
| Armoire | Cabinet |
| Beastmaster | XBM |
| Blue Mage | AOZ |
| Bozja | MYC |
| Chat Bubbles | MiniTalk |
| Chocobo Companion | Buddy |
| Chocobo Porter | ChocoboTaxi |
| Chocobo Racing | RaceChocobo |
| Collection | McGuffin |
| Cosmic Exploration | WKS / MassivePcContent |
| Crafting Log | RecipeNote |
| Crystarium Deliveries | HugeCraftworksSupply |
| Custom Deliveries | SatisfactionSupply |
| Doman Enclave Reconstruction | Reconstruction |
| Doman Mahjong | EMJ |
| Dreamfitting | FittingShop |
| Duty Finder | ContentsFinder |
| Duty Recorder | ContentsReplay |
| Duty Support | DawnStory |
| Exploratory Missions (Old Diadem) | SkyIsland |
| Facewear | Glasses |
| Faux Hollows | WeeklyPuzzle |
| Fellowships | Circle |
| GATEs | GFATE |
| Gathering Log | GatheringNote |
| Glamour Dresser | MiragePrismBox |
| Glamour Plate | MiragePrismPlate |
| Hall of the Novice | BeginnerTraining |
| Hunt Bills | MobHunt |
| Hunt Marks | NotoriousMonster |
| Hunting Log | MonsterNote |
| Ishgardian Restoration | HwdDev |
| Island Sanctuary | MJI |
| Key Items | EventItem |
| Lord of Verminion | Lovm |
| Market Board | ItemSearch |
| Mini Cactpot | LotteryDaily |
| Minion Guide | MinionNoteBook |
| Moogle Delivery Service | Letter |
| Moogle Treasure Trove / Mogpendium | CSBonus / MoogleCollection |
| Mount Guide | MountNoteBook |
| Novice Network | BeginnerChat |
| Occult Crescent | MKD |
| Ocean Fishing | IKD |
| Party Finder | LookingForGroup |
| Portraits | Banner |
| Rival Wings | Maneuvers |
| Server Info (HUD) | DTR |
| Shared FATE | FateProgress |
| Sightseeing Log | AdventureNote |
| Squadron | GcArmy |
| Stone, Sky, Sea | DpsChallenge |
| Strategy Board | Tofu |
| Studium Deliveries | SharlayanCraftworksSupply |
| Trust | Dawn |
| UI theme | UIColor |
| Unending Codex | AkatsukiNote |
| Variant/Criterion Dungeons | VVD |
| Wachumeqimeqi Deliveries | BankaCraftworksSupply |
| Waymarks | FieldMarker |
| Wondrous Tails | WeeklyBingo |

Collaborations: FINAL FANTASY XVI = `SXT` · Fall Guys = `FGS` · Yo-kai Watch = `YKW`

---

## 27. Reference links

**Docs**
- Dalamud developer docs — <https://dalamud.dev>
- API reference — <https://dalamud.dev/api/>
- Versions & channels — <https://dalamud.dev/versions/>
- Publishing — <https://dalamud.dev/plugin-publishing/>
- AI policy — <https://dalamud.dev/plugin-publishing/ai-policy>
- Restrictions — <https://dalamud.dev/plugin-publishing/restrictions>
- Code of Conduct — <https://dalamud.dev/code-of-conduct>
- FFXIVClientStructs docs — <https://ffxiv.wildwolf.dev>
- CS breaking changes — `https://ffxiv.wildwolf.dev/docs/breaking/<patch>.html`

**Repos**
- Dalamud — <https://github.com/goatcorp/Dalamud>
- SamplePlugin — <https://github.com/goatcorp/SamplePlugin>
- Dalamud.NET.Sdk — <https://github.com/goatcorp/Dalamud.NET.Sdk> · [NuGet](https://www.nuget.org/packages/Dalamud.NET.Sdk)
- DalamudPackager — <https://github.com/goatcorp/DalamudPackager>
- DalamudPluginsD17 — <https://github.com/goatcorp/DalamudPluginsD17>
- DalamudAssets (bannedplugin.json) — <https://github.com/goatcorp/DalamudAssets>
- Plogon — <https://github.com/goatcorp/Plogon>
- XIVLauncher — <https://github.com/goatcorp/FFXIVQuickLauncher>
- FFXIVClientStructs — <https://github.com/aers/FFXIVClientStructs>
- Lumina — <https://github.com/NotAdam/Lumina>
- EXDSchema — <https://github.com/xivdev/EXDSchema>
- docs source — <https://github.com/goatcorp/dalamud-docs>

**RE tooling**
- IDA Pro — <https://hex-rays.com/> · Ghidra — <https://ghidra-sre.org/> · Binary Ninja — <https://binary.ninja/>
- CS IDA scripts — <https://github.com/aers/FFXIVClientStructs/tree/main/ida>
- SigMaker-x64 (Caraxi) — <https://github.com/Caraxi/SigMaker-x64>
- x64dbg — <https://x64dbg.com/> · Cheat Engine — <https://www.cheatengine.org/>
- ReClass.NET — <https://github.com/ReClassNET/ReClass.NET> · XivReClassPlugin — <https://github.com/pohky/XivReClassPlugin>
- Sapphire (server protocol) — <https://github.com/SapphireServer/Sapphire>
- Map coordinates — <https://github.com/xivapi/ffxiv-datamining/blob/master/docs/MapCoordinates.md>

**Community**
- Discord — <https://discord.gg/holdshift> (`#dev` / `#plugin-dev`)

---

## Appendix: the checklist

**Before writing code**
- [ ] Idea checked against §23 restrictions; asked in Discord if combat/PvP/data adjacent
- [ ] InternalName chosen and final
- [ ] .NET 10 SDK ≥ 10.0.101 installed; VS 2026 or Rider 2025.3

**While writing**
- [ ] `Dalamud.NET.Sdk/15.0.0`, no manual references
- [ ] Every `+=` has a `-=`; every registration has a teardown
- [ ] Every hook detour wrapped in try/catch and calls `Original`
- [ ] No blocking work on the framework thread or in detours
- [ ] `IObjectTable`/`IClientState` accessed only on the main thread
- [ ] Windows use `WindowSystem`, not raw `ImGui.Begin`
- [ ] Pixel values multiplied by `ImGuiHelpers.GlobalScale`
- [ ] Game data via Lumina, not XIVAPI
- [ ] No `IDalamudTextureWrap` caching
- [ ] Checked in `/xldev` → Plugin Stats that you're not eating frame time

**Before submitting**
- [ ] Manifest has Name / Author / Punchline / Description / RepoUrl
- [ ] Manifest is present and accurate inside the built zip (API 15 requirement)
- [ ] `icon.png` is 1:1, 64×64–512×512, hand-made if at all possible
- [ ] Version is semantic, not a timestamp or build counter
- [ ] Personally tested end-to-end
- [ ] AI usage disclosed in the PR body if at Assist level or above
- [ ] Submitting to `testing/live/`, one plugin, own branch
- [ ] Changelog in `manifest.toml` or the plugin manifest (not just the PR body)
