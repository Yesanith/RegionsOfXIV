using System;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI;

namespace RegionsOfXIV;

// Composition root. Dalamud fills the service properties by reflection before the constructor
// runs, which is why they are static -- it saves threading a dozen handles through every class.
//
// Construction order matters: the config is loaded and migrated first, fonts are built before
// anything can draw, and the coordinator is wired last because it needs every source to exist.
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/regions";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("RegionsOfXIV");

    private readonly Configuration config;
    private readonly FontService fonts;
    private readonly NativeUiSuppressor nativeUiSuppressor;
    private readonly NotificationOverlay overlay;
    private readonly ConfigWindow configWindow;
    private readonly ChangelogWindow changelogWindow;
#if DEBUG
    private readonly IconBrowserWindow iconBrowserWindow;
#endif
    private readonly AnnouncementCoordinator coordinator;
    private readonly UiVisibilityGuard uiVisibilityGuard;
    private readonly LocationTracker locations;
    private readonly WeatherTracker weather;
    private readonly BannerWatcher banners;
    private readonly GameZoneArrivals zones;

    public Plugin()
    {
        var (loaded, isFirstRun) = LoadConfiguration();
        this.config = loaded;

        this.fonts = new FontService(this.config);
        this.nativeUiSuppressor = new NativeUiSuppressor(this.config);
        this.uiVisibilityGuard = new UiVisibilityGuard();

        this.fonts.Rebuild();

        this.overlay = new NotificationOverlay(this.config, this.fonts);

        var game = new DalamudGameState();

        this.locations = new LocationTracker(game);
        this.weather = new WeatherTracker(game);
        this.banners = new BannerWatcher(this.config);
        this.zones = new GameZoneArrivals();

        this.weather.Start();

        this.coordinator = new AnnouncementCoordinator(
            this.config,
            game,
            this.overlay,
            new AnnouncementSources(
                this.locations,
                this.weather,
                this.nativeUiSuppressor,
                this.banners,
                this.zones,
                new GamePlaceNames(),
                new GameWeatherNames()));

        this.changelogWindow = new ChangelogWindow();

        this.configWindow = new ConfigWindow(
            this.config,
            new ConfigActions(
                this.overlay.PreviewOnce,
                this.overlay.TouchPreview,
                this.overlay.HoldPreview,
                RebuildFonts,
                this.fonts.ProblemWith,
                this.changelogWindow.ShowAll,
                this.nativeUiSuppressor.RestoreAreaText,
                this.nativeUiSuppressor.RestoreLoadingTitle));

        this.windowSystem.AddWindow(this.overlay);
        this.windowSystem.AddWindow(this.configWindow);
        this.windowSystem.AddWindow(this.changelogWindow);

#if DEBUG

        this.iconBrowserWindow = new IconBrowserWindow();
        this.windowSystem.AddWindow(this.iconBrowserWindow);
#endif

        if (isFirstRun)
            this.configWindow.IsOpen = true;

        SettleVersion(isFirstRun);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Regions of XIV settings. \"/regions test\" fires a sample notification, "
                          + "\"/regions changelog\" shows what has changed.",
        });

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;
        PluginInterface.UiBuilder.DefaultGlobalScaleChanged += RebuildFonts;

        Log.Information("Regions of XIV loaded.");
    }

    private void SettleVersion(bool isFirstRun)
    {
        var current = Changelog.Current.ToString();

        if (this.config.LastSeenVersion == current)
            return;

        if (!isFirstRun)
        {
            this.changelogWindow.ShowSince(Changelog.Parse(this.config.LastSeenVersion));

            if (this.changelogWindow.IsOpen)
                Log.Information($"Updated from {this.config.LastSeenVersion ?? "an earlier build"} to {current}.");
        }

        this.config.LastSeenVersion = current;
        this.config.Save();
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.DefaultGlobalScaleChanged -= RebuildFonts;

        CommandManager.RemoveHandler(CommandName);

        this.coordinator.Dispose();

        this.zones.Dispose();
        this.banners.Dispose();
        this.weather.Dispose();
        this.locations.Dispose();

        this.windowSystem.RemoveAllWindows();
#if DEBUG
        this.iconBrowserWindow.Dispose();
#endif
        this.changelogWindow.Dispose();
        this.configWindow.Dispose();
        this.overlay.Dispose();

        this.uiVisibilityGuard.Dispose();
        this.nativeUiSuppressor.Dispose();
        this.fonts.Dispose();
    }

    private static (Configuration Config, bool IsFirstRun) LoadConfiguration()
    {
        try
        {
            if (PluginInterface.GetPluginConfig() is Configuration stored)
            {
                var changed = stored.Migrate();
                changed |= stored.RepairFaintColors();

                if (changed)
                    stored.Save();

                return (stored, false);
            }

            return (new Configuration(), true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not read the stored configuration; falling back to defaults.");
            QuarantineBrokenConfig();
            return (new Configuration(), false);
        }
    }

    // A config that cannot be parsed is moved aside rather than deleted or overwritten, so the
    // plugin starts on defaults and whatever the user had is still recoverable by hand.
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
        var argument = args.Trim();

        if (argument.Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            this.coordinator.PushPreview();
            return;
        }

#if DEBUG
        if (argument.Equals("icons", StringComparison.OrdinalIgnoreCase))
        {
            this.iconBrowserWindow.Toggle();
            return;
        }

        if (argument.Equals("banners", StringComparison.OrdinalIgnoreCase))
        {
            SheetSearch.Banners();
            return;
        }

        if (argument.StartsWith("find ", StringComparison.OrdinalIgnoreCase))
        {
            SheetSearch.Run(argument[5..].Trim());
            return;
        }
#endif

        if (argument.Equals("changelog", StringComparison.OrdinalIgnoreCase))
        {
            this.changelogWindow.ShowAll();
            return;
        }

        ToggleConfigUi();
    }

    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void RebuildFonts() =>
        this.fonts.Rebuild();
}
