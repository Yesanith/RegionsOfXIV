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

        this.windowSystem.RemoveAllWindows();
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
