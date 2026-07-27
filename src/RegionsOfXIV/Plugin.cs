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

// Entry point and composition root. Constructs the services, wires the Dalamud
// lifecycle, and handles the command — nothing here decides what gets announced;
// that is AnnouncementCoordinator's job.
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
    private readonly FontService fonts;
    private readonly NativeUiSuppressor nativeUiSuppressor;
    private readonly NotificationOverlay overlay;
    private readonly ConfigWindow configWindow;
    private readonly AnnouncementCoordinator coordinator;
    private readonly LoadingScreenUiGuard loadingScreenUiGuard;

    public Plugin()
    {
        var (loaded, isFirstRun) = LoadConfiguration();
        this.config = loaded;

        this.fonts = new FontService(this.config);
        this.nativeUiSuppressor = new NativeUiSuppressor(this.config);
        this.loadingScreenUiGuard = new LoadingScreenUiGuard();

        // Before the overlay, which takes the handles.
        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);

        this.overlay = new NotificationOverlay(this.config, this.fonts);
        this.coordinator = new AnnouncementCoordinator(this.config, this.nativeUiSuppressor, this.overlay);

        this.configWindow = new ConfigWindow(
            this.config,
            new ConfigActions(
                this.overlay.Push,
                RebuildFonts,
                this.nativeUiSuppressor.RestoreAreaText,
                this.nativeUiSuppressor.RestoreLoadingTitle));

        this.windowSystem.AddWindow(this.overlay);
        this.windowSystem.AddWindow(this.configWindow);

        // Nothing on screen announces that a freshly installed plugin has settings,
        // and this one's defaults change what the game itself draws — it hides the
        // native area text and the loading-screen title out of the box. Showing the
        // window once makes that discoverable and reversible.
        //
        // Saving immediately is what makes it once: the config file's absence is the
        // first-run signal, so writing it now is what stops this repeating on every
        // load. Deleting the config to reset therefore also brings the window back,
        // which is the behaviour you would want.
        if (isFirstRun)
        {
            this.configWindow.IsOpen = true;
            this.config.Save();
        }

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Regions of XIV settings. \"/regions test\" fires a sample notification.",
        });

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;
        PluginInterface.UiBuilder.DefaultGlobalScaleChanged += RebuildFonts;

        Log.Information("Regions of XIV loaded.");
    }

    // Reverse construction order: the coordinator unsubscribes from the suppressor,
    // so it has to go first.
    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.DefaultGlobalScaleChanged -= RebuildFonts;

        CommandManager.RemoveHandler(CommandName);

        this.coordinator.Dispose();

        this.windowSystem.RemoveAllWindows();
        this.configWindow.Dispose();
        this.overlay.Dispose();

        this.loadingScreenUiGuard.Dispose();
        this.nativeUiSuppressor.Dispose();
        this.fonts.Dispose();
    }

    // GetPluginConfig throws if the stored JSON no longer matches this type — a
    // renamed field, a changed type, a half-written file after a crash. Left
    // unhandled that bricks the plugin on every subsequent load with no recovery
    // path short of the user finding the file in AppData themselves, so preserve
    // the bad config and carry on with defaults.
    //
    // Returns whether this looks like a first install: no stored config at all.
    // Recovering from an unreadable one does not count — the user has had this
    // plugin for a while and does not need introducing to it.
    private static (Configuration Config, bool IsFirstRun) LoadConfiguration()
    {
        try
        {
            if (PluginInterface.GetPluginConfig() is Configuration stored)
                return (stored, false);

            return (new Configuration(), true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not read the stored configuration; falling back to defaults.");
            QuarantineBrokenConfig();
            return (new Configuration(), false);
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
            this.coordinator.PushPreview();
            return;
        }

        ToggleConfigUi();
    }

    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void RebuildFonts() =>
        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);
}
