using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Suppresses the game's own location text where this plugin is standing in for it.
//
// Two separate targets, with different rules:
//
//   "_AreaText"  the in-world area flash. Suppressed whenever the setting is on.
//   "_Image"     carries the loading-screen title. Suppressed ONLY while a loading
//                screen is actually up — the name is generic and the addon is used
//                in other contexts, so hiding it unconditionally would break things
//                that have nothing to do with us.
//
// Neither has a dedicated FFXIVClientStructs struct; both are plain AtkUnitBase,
// so setting IsVisible is enough.
//
// Loading-screen detection (previously LoadingScreenWatcher) is inlined here:
// BetweenAreas covers ordinary zone changes; BetweenAreas51 covers the variants
// the game uses for instanced content and a few scripted transitions.
internal sealed class NativeUiSuppressor : IDisposable
{
    private static readonly string[] AreaTextAddons = ["_AreaText"];
    private static readonly string[] LoadingTitleAddons = ["_Image"];

    private readonly Configuration config;
    private bool isLoading;

    public NativeUiSuppressor(Configuration config)
    {
        this.config = config;

        // PostSetup hides it before it ever paints; PreDraw catches the addon
        // re-showing itself partway through its own timeline. PreDraw only fires
        // while the addon is drawing, so it costs nothing the rest of the time.
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AreaTextAddons, OnAreaText);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AreaTextAddons, OnAreaText);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AreaTextAddons, OnAreaText);

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, LoadingTitleAddons, OnLoadingTitle);

        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;

        Plugin.AddonLifecycle.UnregisterListener(OnAreaText, OnLoadingTitle);

        SetVisible(AreaTextAddons, true);
        SetVisible(LoadingTitleAddons, true);
    }

    // Called when a setting is switched off, so the game's own text returns without
    // needing a reload.
    public void RestoreAreaText() => SetVisible(AreaTextAddons, true);

    private void RestoreLoadingTitle() => SetVisible(LoadingTitleAddons, true);

    // Tracks whether a zone-transition loading screen is up, and restores the
    // loading title addon when the screen ends.
    private void OnFrameworkUpdate(IFramework framework)
    {
        var loading = Plugin.Condition[ConditionFlag.BetweenAreas]
                      || Plugin.Condition[ConditionFlag.BetweenAreas51];

        if (loading == this.isLoading)
            return;

        this.isLoading = loading;

        if (!loading)
            RestoreLoadingTitle();
    }

    private unsafe void OnAreaText(AddonEvent type, AddonArgs args)
    {
        if (!this.config.HideNativeAreaText)
            return;

        Hide(args);
    }

    private unsafe void OnLoadingTitle(AddonEvent type, AddonArgs args)
    {
        if (!this.config.HideNativeLoadingTitle || !this.isLoading)
            return;

        Hide(args);
    }

    private static unsafe void Hide(AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon != null)
            addon->IsVisible = false;
    }

    private static unsafe void SetVisible(string[] names, bool visible)
    {
        foreach (var name in names)
        {
            var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(name).Address;
            if (addon != null)
                addon->IsVisible = visible;
        }
    }
}
