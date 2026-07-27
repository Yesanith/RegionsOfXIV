using System;
using System.IO;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using RegionsOfXIV.Models;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI;

namespace RegionsOfXIV;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/regions";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("RegionsOfXIV");

    private readonly Configuration config;
    private readonly LocationTracker tracker;
    private readonly NotificationGate gate;
    private readonly FontService fonts;
    private readonly NativeUiSuppressor nativeUiSuppressor;
    private readonly NotificationOverlay overlay;
    private readonly ConfigWindow configWindow;

    // Set for the duration of one _AreaText event, so the announcement being built
    // in response can compare itself against what the game is showing.
    private string? pendingNativeAreaText;

    // The last name the game flashed, kept beyond that event. Sanctuaries are the
    // reason: TerritoryInfo does not reliably name them, and by the time we notice
    // we are inside one the addon is long gone.
    private string? lastNativeAreaText;

    public Plugin()
    {
        this.config = LoadConfiguration();

        this.gate = new NotificationGate(this.config);
        this.fonts = new FontService(this.config);
        this.nativeUiSuppressor = new NativeUiSuppressor(this.config);

        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);

        this.overlay = new NotificationOverlay(this.config, this.fonts);
        this.configWindow = new ConfigWindow(
            this.config,
            new ConfigActions(
                this.overlay.Push,
                RebuildFonts,
                this.nativeUiSuppressor.RestoreAreaText,
                this.nativeUiSuppressor.RestoreLoadingTitle));

        this.windowSystem.AddWindow(this.overlay);
        this.windowSystem.AddWindow(this.configWindow);

        this.tracker = new LocationTracker();
        this.tracker.LocationChanged += OnLocationChanged;
        this.tracker.SanctuaryChanged += OnSanctuaryChanged;
        this.nativeUiSuppressor.AreaTextShown += OnAreaTextShown;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Regions of XIV settings. \"/regions test\" fires a sample notification.",
        });

        ClientState.Logout += OnLogout;
        ClientState.ZoneInit += OnZoneInit;

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;
        PluginInterface.UiBuilder.DefaultGlobalScaleChanged += RebuildFonts;

        Log.Information("Regions of XIV loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.DefaultGlobalScaleChanged -= RebuildFonts;

        ClientState.Logout -= OnLogout;
        ClientState.ZoneInit -= OnZoneInit;

        CommandManager.RemoveHandler(CommandName);

        this.nativeUiSuppressor.AreaTextShown -= OnAreaTextShown;
        this.tracker.SanctuaryChanged -= OnSanctuaryChanged;
        this.tracker.LocationChanged -= OnLocationChanged;
        this.tracker.Dispose();

        this.windowSystem.RemoveAllWindows();

        this.configWindow.Dispose();
        this.overlay.Dispose();

        this.nativeUiSuppressor.Dispose();
        this.fonts.Dispose();
    }

    // GetPluginConfig throws if the stored JSON no longer matches this type — a
    // renamed field, a changed type, a half-written file after a crash. Left
    // unhandled that bricks the plugin on every subsequent load with no recovery
    // path short of the user finding the file in AppData themselves, so preserve
    // the bad config and carry on with defaults.
    private static Configuration LoadConfiguration()
    {
        try
        {
            return PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not read the stored configuration; falling back to defaults.");
            QuarantineBrokenConfig();
            return new Configuration();
        }
    }

    private static void QuarantineBrokenConfig()
    {
        try
        {
            var file = PluginInterface.ConfigFile;
            if (!file.Exists)
                return;

            var target = Path.Combine(
                file.DirectoryName!,
                $"{Path.GetFileNameWithoutExtension(file.Name)}.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            file.MoveTo(target, overwrite: true);
            Log.Information($"Moved the unreadable configuration to {target}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set aside the unreadable configuration.");
        }
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            // Bypasses the gate deliberately: this is for checking the visuals,
            // not the suppression rules.
            var names = ResolveLocation(this.tracker.Current);
            this.overlay.Push(names.Area ?? names.Place ?? "Middle La Noscea",
                              names.SubArea ?? names.Area ?? "Summerford Farms");
            return;
        }

        ToggleConfigUi();
    }

    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void OnLogout(int type, int code) => this.gate.Reset();

    // In-world, the game itself decides when an area announcement is warranted, so
    // take that as the cue and read TerritoryInfo immediately rather than waiting
    // out the poll interval.
    //
    // Where the two agree, TerritoryInfo wins: same name, but with the parent tier
    // available for the header. Where they disagree — or where TerritoryInfo does
    // not move at all, which is what settlements and sanctuaries appear to do —
    // the game's own string is the one on screen, so it takes precedence.
    //
    // The poll stays running underneath as a backstop for sub-area changes the
    // game never flashes; the gate's dedup keeps whichever arrives second quiet.
    private void OnAreaTextShown(string? nativeText)
    {
        if (string.IsNullOrWhiteSpace(nativeText))
        {
            this.tracker.Poll();
            return;
        }

        this.pendingNativeAreaText = nativeText;
        this.lastNativeAreaText = nativeText;
        try
        {
            // Raises LocationChanged inline when TerritoryInfo moved, which clears
            // the pending text by way of ReconcileWithNative.
            this.tracker.Poll();

            if (this.pendingNativeAreaText is not null && this.gate.ShouldAnnounceNativeAreaText())
            {
                Log.Debug($"Native area text only (TerritoryInfo unchanged): {nativeText}");
                this.overlay.Push(null, nativeText);
                this.gate.MarkAnnounced(this.tracker.Current, LocationTier.SubArea, this.overlay.EstimatedDuration);
            }
        }
        finally
        {
            this.pendingNativeAreaText = null;
        }
    }

    // ZoneInit names the territory — "Western La Noscea" — but says nothing about
    // landing inside a settlement within it. The sanctuary flag is what closes
    // that gap: once the loading screen is down and the tracker sees we are inside
    // one, the finer name follows the zone name and supersedes it on screen.
    //
    // On the way out there is no flash from the game at all, so TerritoryInfo is
    // the only source, and the area we have stepped into is what to announce.
    private void OnSanctuaryChanged(bool inSanctuary)
    {
        if (!this.gate.ShouldAnnounceSanctuary())
            return;

        var names = ResolveLocation(this.tracker.Current);

        // Entering, the sanctuary's own name is wanted, and TerritoryInfo may not
        // carry it — the game's last flash is the fallback. Leaving, TerritoryInfo
        // is authoritative again and the stale flash would name where we just were.
        var text = inSanctuary
            ? names.SubArea ?? names.Area ?? this.lastNativeAreaText
            : names.Area ?? names.Place;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var header = inSanctuary
            ? names.Area ?? names.Place
            : names.Place;

        if (!this.config.IncludeParentTierAsHeader)
            header = null;

        if (header is not null && string.Equals(header, text, StringComparison.OrdinalIgnoreCase))
            header = null;

        Log.Debug($"Sanctuary {(inSanctuary ? "entered" : "left")}: {header} / {text}");

        this.overlay.Push(header, text);
        this.gate.MarkAnnounced(this.tracker.Current, LocationTier.SubArea, this.overlay.EstimatedDuration);
    }

    // Applied only while the game's area flash is going up in the same breath.
    private (string? Header, string Text) ReconcileWithNative(
        string? header, string text, in ResolvedLocation names)
    {
        if (this.pendingNativeAreaText is not { } native)
            return (header, text);

        // Consumed either way: this decides the one announcement being built.
        this.pendingNativeAreaText = null;

        if (string.Equals(text, native, StringComparison.OrdinalIgnoreCase))
            return (header, text);

        Log.Debug($"TerritoryInfo says \"{text}\", the game says \"{native}\" — taking the game's.");

        // The area still makes a usable header, as long as it is not the thing
        // being announced. Anything finer cannot be trusted here: it just
        // disagreed with the game.
        var parent = names.Area is not null
                     && !string.Equals(names.Area, native, StringComparison.OrdinalIgnoreCase)
            ? names.Area
            : null;

        return (this.config.IncludeParentTierAsHeader ? parent : null, native);
    }

    // The zone being entered, handed to us as data while the loading screen is
    // still up — well before ClientState.TerritoryType catches up, which is why
    // LocationTracker cannot serve this. Stands in for the suppressed
    // "_LocationTitle".
    private void OnZoneInit(ZoneInitEventArgs args)
    {
        if (args.TerritoryType.ValueNullable is not { } territory)
            return;

        // Both read from the event rather than from current game state, which at
        // this point still describes the zone being left.
        var isDuty = args.ContentFinderCondition.RowId != 0;
        if (!this.gate.ShouldAnnounceZoneEntry(territory.IsPvpZone, isDuty))
            return;

        var text = ResolvePlaceName(territory.PlaceName.RowId)
                   ?? ResolvePlaceName(territory.PlaceNameZone.RowId);

        if (string.IsNullOrWhiteSpace(text))
            return;

        var header = ResolvePlaceName(territory.PlaceNameRegion.RowId);

        if (!this.config.IncludeParentTierAsHeader)
            header = null;

        if (header is not null && string.Equals(header, text, StringComparison.OrdinalIgnoreCase))
            header = null;

        Log.Debug($"ZoneInit [{territory.RowId}]: {header} / {text} (duty={isDuty})");

        this.overlay.Push(header, text);
        this.gate.MarkZoneAnnounced(this.overlay.EstimatedDuration);
    }

    private void RebuildFonts() =>
        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);

    // Runs on the framework thread — LocationTracker polls from IFramework.Update,
    // so reading game state here is safe.
    private void OnLocationChanged(LocationSnapshot previous, LocationSnapshot current)
    {
        var tier = current.DiffTier(previous);
        var names = ResolveLocation(current);

        // Raw ids alongside the names: a blank tier is ambiguous otherwise, since
        // "the game does not track this place" and "the row resolved to nothing"
        // read identically once the ids are gone.
        Log.Debug(
            $"Location changed [{tier}]: {names.Region} / {names.Zone} / {names.Place} " +
            $"/ {names.Area} / {names.SubArea} " +
            $"[ids {current.TerritoryTypeId}/{current.RegionPlaceNameId}/{current.ZonePlaceNameId}" +
            $"/{current.PlacePlaceNameId}/{current.AreaPlaceNameId}/{current.SubAreaPlaceNameId}]");

        if (!this.gate.ShouldAnnounce(previous, current, tier))
            return;

        var (header, text) = BuildNotificationText(tier, names);
        (header, text) = ReconcileWithNative(header, text, names);

        if (string.IsNullOrWhiteSpace(text))
            return;

        this.overlay.Push(header, text);
        this.gate.MarkAnnounced(current, tier, this.overlay.EstimatedDuration);
    }

    private (string? Header, string Text) BuildNotificationText(LocationTier tier, in ResolvedLocation names)
    {
        var (header, text) = tier switch
        {
            LocationTier.SubArea => (names.Area ?? names.Place, names.SubArea),

            // Arriving at a settlement moves the area and the sub-area at once —
            // Skull Valley and Aleport, say. DiffTier reports the coarser of the
            // two, but the sub-area is the name that identifies the place, and the
            // one the game itself puts on screen. Announce that, with the area as
            // the header, and fall back to the area only when there is no
            // sub-area to show.
            LocationTier.Area => names.SubArea is not null
                ? (names.Area ?? names.Place, names.SubArea)
                : (names.Place, names.Area),

            _ => (names.Region, names.Place ?? names.Zone),
        };

        if (!this.config.IncludeParentTierAsHeader)
            header = null;

        if (header is not null && string.Equals(header, text, StringComparison.OrdinalIgnoreCase))
            header = null;

        return (header, text ?? string.Empty);
    }

    // --- Name resolution (inlined from PlaceNameResolver) ------------------

    private static string? ResolvePlaceName(uint placeNameRowId)
    {
        if (placeNameRowId == 0)
            return null;

        if (!DataManager.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var row))
            return null;

        // Name is a ReadOnlySeString. ToString() strips payloads, which is what we
        // want for display; ToMacroString() is the one to reach for when debugging
        // an odd-looking name.
        var name = row.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private static ResolvedLocation ResolveLocation(in LocationSnapshot snapshot) => new(
        ResolvePlaceName(snapshot.RegionPlaceNameId),
        ResolvePlaceName(snapshot.ZonePlaceNameId),
        ResolvePlaceName(snapshot.PlacePlaceNameId),
        ResolvePlaceName(snapshot.AreaPlaceNameId),
        ResolvePlaceName(snapshot.SubAreaPlaceNameId));

    private readonly record struct ResolvedLocation(
        string? Region,
        string? Zone,
        string? Place,
        string? Area,
        string? SubArea);
}
