using System;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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
    private readonly PlaceNameResolver resolver;
    private readonly NotificationGate gate;
    private readonly FontService fonts;
    private readonly SoundService sounds;
    private readonly NaviMapAnchor naviMap;
    private readonly NativeAreaTextHider nativeAreaText;
    private readonly NotificationOverlay overlay;
    private readonly CompassLabel compassLabel;
    private readonly ConfigWindow configWindow;

    public Plugin()
    {
        this.config = LoadConfiguration();

        this.resolver = new PlaceNameResolver();
        this.gate = new NotificationGate(this.config);
        this.fonts = new FontService(this.config);
        this.sounds = new SoundService(this.config);
        this.naviMap = new NaviMapAnchor();
        this.nativeAreaText = new NativeAreaTextHider(this.config);

        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);

        this.overlay = new NotificationOverlay(this.config, this.fonts, this.sounds);
        this.compassLabel = new CompassLabel(this.config, this.naviMap);
        this.configWindow = new ConfigWindow(
            this.config,
            new ConfigActions(
                this.overlay.Push,
                RebuildFonts,
                this.nativeAreaText.Restore,
                () => this.fonts.EffectiveCeilingPx));

        this.windowSystem.AddWindow(this.overlay);
        this.windowSystem.AddWindow(this.compassLabel);
        this.windowSystem.AddWindow(this.configWindow);

        this.tracker = new LocationTracker();
        this.tracker.LocationChanged += OnLocationChanged;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Regions of XIV settings. \"/regions test\" fires a sample notification.",
        });

        ClientState.Logout += OnLogout;

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

        CommandManager.RemoveHandler(CommandName);

        this.tracker.LocationChanged -= OnLocationChanged;
        this.tracker.Dispose();

        this.windowSystem.RemoveAllWindows();

        this.configWindow.Dispose();
        this.compassLabel.Dispose();
        this.overlay.Dispose();

        this.nativeAreaText.Dispose();
        this.naviMap.Dispose();
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
            var names = this.resolver.Resolve(this.tracker.Current);
            this.overlay.Push(names.Area ?? names.Place ?? "Middle La Noscea",
                              names.SubArea ?? names.Area ?? "Summerford Farms");
            return;
        }

        ToggleConfigUi();
    }

    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void OnLogout(int type, int code)
    {
        this.gate.Reset();
        this.compassLabel.Set(null);
    }

    private void RebuildFonts() =>
        this.fonts.Rebuild(this.config.DisplayFontSize, this.config.HeaderFontSize);

    // Runs on the framework thread — LocationTracker polls from IFramework.Update,
    // so reading game state here is safe.
    private void OnLocationChanged(LocationSnapshot previous, LocationSnapshot current)
    {
        var tier = current.DiffTier(previous);
        var names = this.resolver.Resolve(current);

        Log.Debug(
            $"Location changed [{tier}]: {names.Region} / {names.Zone} / {names.Place} " +
            $"/ {names.Area} / {names.SubArea}");

        // The companion label tracks position regardless of notification gating.
        this.compassLabel.Set(names.Finest);

        if (!this.gate.ShouldAnnounce(previous, current, tier))
            return;

        var (header, text) = BuildNotificationText(tier, names);
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
            LocationTier.Area => (names.Place, names.Area),
            _ => (names.Region, names.Place ?? names.Zone),
        };

        if (!this.config.IncludeParentTierAsHeader)
            header = null;

        if (header is not null && string.Equals(header, text, StringComparison.OrdinalIgnoreCase))
            header = null;

        return (header, text ?? string.Empty);
    }
}
