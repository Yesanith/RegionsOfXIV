using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

internal sealed class NativeUiSuppressor : IDisposable
{
    private static readonly string[] AreaTextAddons = ["_AreaText"];

    private static readonly string[] LoadingTitleAddons = ["_LocationTitle", "_LocationTitleShort"];

    private readonly Configuration config;

    public event Action<string?>? OnAreaTextShown;

    public NativeUiSuppressor(Configuration config)
    {
        this.config = config;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AreaTextAddons, OnAreaText);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AreaTextAddons, OnAreaText);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AreaTextAddons, OnAreaText);

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, LoadingTitleAddons, OnLoadingTitle);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, LoadingTitleAddons, OnLoadingTitle);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, LoadingTitleAddons, OnLoadingTitle);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnAreaText, OnLoadingTitle);

        SetVisible(AreaTextAddons, true);
        SetVisible(LoadingTitleAddons, true);
    }

    public void RestoreAreaText() => SetVisible(AreaTextAddons, true);

    public void RestoreLoadingTitle() => SetVisible(LoadingTitleAddons, true);

    private unsafe void OnAreaText(AddonEvent type, AddonArgs args)
    {
        var isContentEvent = type != AddonEvent.PreDraw;

        var addon = (AtkUnitBase*)args.Addon.Address;
        var text = isContentEvent && addon != null ? ReadLargestText(addon) : null;

        Suppress(type, args, this.config.HideNativeAreaText);

        if (isContentEvent)
            OnAreaTextShown?.Invoke(text);
    }

    private static unsafe string? ReadLargestText(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text)
                continue;

            var text = ((AtkTextNode*)node)->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return null;
    }

    private void OnLoadingTitle(AddonEvent type, AddonArgs args) =>
        Suppress(type, args, this.config.HideNativeLoadingTitle);

    private static unsafe void Suppress(AddonEvent type, AddonArgs args, bool enabled)
    {
        if (!enabled)
            return;

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
