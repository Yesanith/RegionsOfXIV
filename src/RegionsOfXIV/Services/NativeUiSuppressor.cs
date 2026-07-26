using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Intercepts the game's "_AreaText" addon — the native in-world area flash.
// Reads the text the game was about to display, hides the native element, and
// raises AreaTextDetected so the plugin can re-render it in its own style.
internal sealed class NativeUiSuppressor : IDisposable
{
    private static readonly string[] AreaTextAddons = ["_AreaText"];

    private readonly Configuration config;

    private string? lastDetectedText;

    // Raised with the area name the game was about to display.
    public event Action<string>? AreaTextDetected;

    public NativeUiSuppressor(Configuration config)
    {
        this.config = config;

        // PostSetup fires once when the addon is created; PostRefresh fires when
        // its content changes (new area). Both are good moments to read the text.
        // PreDraw keeps it hidden frame-by-frame.
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AreaTextAddons, OnAreaTextContent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AreaTextAddons, OnAreaTextContent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AreaTextAddons, OnAreaTextDraw);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnAreaTextContent, OnAreaTextDraw);
        SetVisible(AreaTextAddons, true);
    }

    // Called when the config setting is switched off, so the game's own text
    // returns without needing a reload.
    public void RestoreAreaText() => SetVisible(AreaTextAddons, true);

    // Read the text, hide the addon, raise the event.
    private unsafe void OnAreaTextContent(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null)
            return;

        var text = ReadAreaText(addon);

        if (this.config.HideNativeAreaText)
            addon->IsVisible = false;

        // Dedup: don't fire again if the game re-raises for the same text.
        if (string.IsNullOrWhiteSpace(text) || text == this.lastDetectedText)
            return;

        this.lastDetectedText = text;
        AreaTextDetected?.Invoke(text!);
    }

    // Keep it hidden on every draw frame while the setting is on.
    private unsafe void OnAreaTextDraw(AddonEvent type, AddonArgs args)
    {
        if (!this.config.HideNativeAreaText)
            return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon != null)
            addon->IsVisible = false;
    }

    // Walk the addon's node list for the first text node with content.
    private static unsafe string? ReadAreaText(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text)
                continue;

            var textNode = (AtkTextNode*)node;
            var text = textNode->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
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
