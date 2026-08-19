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
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
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
    private readonly ChangelogWindow changelogWindow;
    private readonly AnnouncementCoordinator coordinator;
    private readonly UiVisibilityGuard uiVisibilityGuard;

    public Plugin()
    {
        var (loaded, isFirstRun) = LoadConfiguration();
        this.config = loaded;

        this.fonts = new FontService(this.config);
        this.nativeUiSuppressor = new NativeUiSuppressor(this.config);
        this.uiVisibilityGuard = new UiVisibilityGuard();

        // Before the overlay, which takes the handles.
        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);

        this.overlay = new NotificationOverlay(this.config, this.fonts);
        this.coordinator = new AnnouncementCoordinator(this.config, this.nativeUiSuppressor, this.overlay);

        this.configWindow = new ConfigWindow(
            this.config,
            new ConfigActions(
                this.overlay.PreviewOnce,
                this.overlay.TouchPreview,
                this.overlay.HoldPreview,
                RebuildFonts,
                this.nativeUiSuppressor.RestoreAreaText,
                this.nativeUiSuppressor.RestoreLoadingTitle));

        this.changelogWindow = new ChangelogWindow();

        this.windowSystem.AddWindow(this.overlay);
        this.windowSystem.AddWindow(this.configWindow);
        this.windowSystem.AddWindow(this.changelogWindow);

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
            this.configWindow.IsOpen = true;

        // Saves in both cases — see below for why that is what makes each of them
        // happen once rather than every load.
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

    // Decides whether this load has a changelog to show, and records the version
    // either way.
    //
    // Recording is what makes it once. The window is opened here and the version
    // written in the same breath, rather than when the player closes the window —
    // otherwise a reload with it still open would show it again, and quitting the
    // game without closing it would mean it never stopped.
    //
    // A first install is stamped but shown nothing. Somebody who has never run this
    // plugin has not missed anything, and the config window is already opening in
    // front of them; two unrequested windows at once is one too many.
    private void SettleVersion(bool isFirstRun)
    {
        var current = Changelog.Current.ToString();

        // The ordinary case, every load after the first on a given build.
        if (this.config.LastSeenVersion == current)
            return;

        if (!isFirstRun)
        {
            this.changelogWindow.ShowSince(Changelog.Parse(this.config.LastSeenVersion));

            if (this.changelogWindow.IsOpen)
                Log.Information($"Updated from {this.config.LastSeenVersion ?? "an earlier build"} to {current}.");
        }

        // Reached whether or not anything was shown, which is the point: a build
        // that adds no changelog entry still moves the marker, so the release after
        // it is not compared against a version two behind. It also writes the file
        // that makes a first install a first install exactly once.
        this.config.LastSeenVersion = current;
        this.config.Save();
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
        this.changelogWindow.Dispose();
        this.configWindow.Dispose();
        this.overlay.Dispose();

        this.uiVisibilityGuard.Dispose();
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
            {
                // Written back immediately, so the migration is paid once rather
                // than on every load until some unrelated setting happens to be
                // changed.
                if (stored.Migrate())
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

        // On demand, because the automatic showing is deliberately once and there is
        // otherwise no way back to it — the version is stamped the moment the window
        // opens, so by the time anyone thinks "what did that say?" it is unreachable
        // short of hand-editing the config file.
        if (argument.Equals("changelog", StringComparison.OrdinalIgnoreCase))
        {
            this.changelogWindow.ShowAll();
            return;
        }

        ToggleConfigUi();
    }

    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void RebuildFonts() =>
        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);
}
