using System;
using System.Collections.Generic;
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
    private readonly WindowFont windowFont;
    private readonly NativeUiSuppressor nativeUiSuppressor;
    private readonly NotificationOverlay overlay;
    private readonly ConfigWindow configWindow;
    private readonly ChangelogWindow changelogWindow;
#if DEBUG
    private readonly IconBrowserWindow iconBrowserWindow;
    private readonly BannerPreviewWindow bannerPreviewWindow;
#endif
    private readonly AnnouncementCoordinator coordinator;
    private readonly UiVisibilityGuard uiVisibilityGuard;
    private readonly LocationTracker locations;
    private readonly WeatherTracker weather;
    private readonly BannerWatcher banners;
    private readonly GameZoneArrivals zones;
    private readonly CommandRouter commands;

    public Plugin()
    {
        var (loaded, isFirstRun) = ConfigurationStore.Load();
        this.config = loaded;

        // Before anything can draw.
        ApplyLanguage();
        PluginInterface.LanguageChanged += OnLanguageChanged;

        BannerNameResolver.Language = this.config.BannerNameLanguage;

        this.fonts = new FontService(this.config);
        this.windowFont = new WindowFont();
        this.nativeUiSuppressor = new NativeUiSuppressor(this.config);
        this.uiVisibilityGuard = new UiVisibilityGuard();

        this.fonts.Rebuild();

        this.overlay = new NotificationOverlay(this.config, this.fonts);

        var game = new DalamudGameState();

        // One gate, owned here, because two things need the same answer from it: the coordinator
        // decides whether to announce, and the watcher has to know that decision before it hides
        // the game's own banner -- otherwise a refused banner leaves nothing on screen at all.
        var gate = new NotificationGate(this.config, game);

        this.locations = new LocationTracker(game);
        this.weather = new WeatherTracker(game);
        this.banners = new BannerWatcher(this.config, gate.BannerBlockReason);
        this.zones = new GameZoneArrivals();

        this.weather.Start();

        this.coordinator = new AnnouncementCoordinator(
            this.config,
            gate,
            this.overlay,
            new AnnouncementSources(
                this.locations,
                this.weather,
                this.nativeUiSuppressor,
                this.banners,
                this.zones,
                new GamePlaceNames(),
                new GameWeatherNames()));

        this.changelogWindow = new ChangelogWindow(this.windowFont);

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
                this.nativeUiSuppressor.RestoreLoadingTitle,
                ApplyLanguage),
            this.windowFont);

        this.windowSystem.AddWindow(this.overlay);
        this.windowSystem.AddWindow(this.configWindow);
        this.windowSystem.AddWindow(this.changelogWindow);

#if DEBUG

        this.iconBrowserWindow = new IconBrowserWindow();
        this.windowSystem.AddWindow(this.iconBrowserWindow);

        this.bannerPreviewWindow = new BannerPreviewWindow(this.overlay, gate);
        this.windowSystem.AddWindow(this.bannerPreviewWindow);
#endif

        if (isFirstRun)
            this.configWindow.IsOpen = true;

        SettleVersion(isFirstRun);

        var subcommands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
        {
            ["test"] = this.coordinator.PushPreview,
            ["changelog"] = this.changelogWindow.ShowAll,
        };
#if DEBUG
        subcommands["icons"] = this.iconBrowserWindow.Toggle;
        subcommands["preview"] = this.bannerPreviewWindow.Toggle;
        subcommands["banners"] = SheetSearch.Banners;
#endif

        this.commands = new CommandRouter(ToggleConfigUi, subcommands);

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

    // The argument is deliberately ignored. Dalamud reports its own new setting, but the config
    // may name a language that overrides it, so the answer is recomputed from both rather than
    // taken from the event -- otherwise changing Dalamud's language would quietly undo an
    // override the player set here.
    private void OnLanguageChanged(string languageCode) => ApplyLanguage();

    private void ApplyLanguage() => Loc.Use(this.config.Language ?? PluginInterface.UiLanguage);

    public void Dispose()
    {
        PluginInterface.LanguageChanged -= OnLanguageChanged;

        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.DefaultGlobalScaleChanged -= RebuildFonts;

        this.commands.Dispose();

        this.coordinator.Dispose();

        this.zones.Dispose();
        this.banners.Dispose();
        this.weather.Dispose();
        this.locations.Dispose();

        this.windowSystem.RemoveAllWindows();
#if DEBUG
        this.bannerPreviewWindow.Dispose();
        this.iconBrowserWindow.Dispose();
#endif
        this.changelogWindow.Dispose();
        this.configWindow.Dispose();
        this.overlay.Dispose();

        this.uiVisibilityGuard.Dispose();
        this.nativeUiSuppressor.Dispose();
        this.windowFont.Dispose();
        this.fonts.Dispose();
    }

    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void RebuildFonts() =>
        this.fonts.Rebuild();
}
