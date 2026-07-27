using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Hides the game's own location text where this plugin stands in for it:
//
//   "_AreaText"           the in-world area flash, replaced from TerritoryInfo
//                         by way of LocationTracker.
//   "_LocationTitle"      the loading-screen title, replaced from the
//   "_LocationTitleShort" IClientState.ZoneInit event.
//
// Both replacements are driven by game data rather than by reading these addons,
// so this class only suppresses — nothing downstream depends on it for content.
//
// Neither addon has a dedicated FFXIVClientStructs struct; both are plain
// AtkUnitBase, so setting IsVisible is enough.
internal sealed class NativeUiSuppressor : IDisposable
{
    private static readonly string[] AreaTextAddons = ["_AreaText"];

    private static readonly string[] LoadingTitleAddons = ["_LocationTitle", "_LocationTitleShort"];

    private readonly Configuration config;

    // Raised when the game puts up its own area text, carrying whatever it was
    // about to display. Primarily a signal that an announcement is due; the text
    // is the tie-breaker for when TerritoryInfo disagrees or has nothing.
    public event Action<string?>? AreaTextShown;

    public NativeUiSuppressor(Configuration config)
    {
        this.config = config;

        // PostSetup hides an addon before it ever paints. PreDraw catches it
        // re-showing itself partway through its own timeline, and only fires while
        // the addon is drawing, so it costs nothing the rest of the time.
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

    // Called when a setting is switched off, so the game's own text returns
    // without needing a reload.
    public void RestoreAreaText() => SetVisible(AreaTextAddons, true);

    public void RestoreLoadingTitle() => SetVisible(LoadingTitleAddons, true);

    private unsafe void OnAreaText(AddonEvent type, AddonArgs args)
    {
        // PreDraw fires every frame the addon is up, so only the content events
        // count as "the game has decided to announce something".
        var isContentEvent = type != AddonEvent.PreDraw;

        var addon = (AtkUnitBase*)args.Addon.Address;
        var text = isContentEvent && addon != null ? ReadLargestText(addon) : null;

        Suppress(type, args, this.config.HideNativeAreaText);

        if (isContentEvent)
            AreaTextShown?.Invoke(text);
    }

    // Walk the node list for the first text node with content. Read before the
    // addon is hidden, though hiding does not clear the node text either way.
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
