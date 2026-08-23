using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// The game's full-screen banners live in four persistent addons that are created at login and
// hidden between uses, so there is no setup event to hook -- the only way to notice one is to
// look at them every frame and watch for a change.
//
// That makes this the most frequently run code in the plugin, which is why it gives up
// immediately when the feature is off.
internal sealed class BannerWatcher : IBannerSource, IDisposable
{
    private static readonly string[] ImageAddons = ["_Image", "_Image2", "_Image3", "_Image4"];

    private readonly Configuration config;

    private readonly Dictionary<nint, uint> showing = [];

    private static readonly HashSet<uint> Unnamed = [];

    public event Action<uint, string>? OnBannerShown;

    public BannerWatcher(Configuration config)
    {
        this.config = config;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, ImageAddons, OnImage);
    }

    public void Dispose() =>
        Plugin.AddonLifecycle.UnregisterListener(OnImage);

    private unsafe void OnImage(AddonEvent type, AddonArgs args)
    {
        if (!this.config.BannerNotificationEnabled)
        {
            if (this.showing.Count > 0)
                this.showing.Clear();

            return;
        }

        var addon = (AddonImage*)args.Addon.Address;
        if (addon == null)
            return;

        var which = (nint)addon;
        var icon = addon->IsVisible ? IconOf(addon->ImageNode) : 0;

        var name = icon == 0 ? null : BannerNameResolver.Resolve(icon);
        var taking = name is not null;

        if (taking && this.config.HideNativeBanner)
            Hide(addon);

        if (this.showing.TryGetValue(which, out var last) && last == icon)
            return;

        this.showing[which] = icon;

        if (taking)
        {
            OnBannerShown?.Invoke(icon, name!);
            return;
        }

        if (icon != 0 && name == null && Unnamed.Add(icon))
            Log.Information(
                $"Banner icon {icon} appeared on {args.AddonName} with no name for it yet. "
                + "Report this id and it can be added.");
    }

    // Made transparent rather than hidden. Setting IsVisible would make the addon lie about its
    // own state, and the game turns the banner off again on its own once it is done -- which also
    // resets the alpha, so this needs no undo.
    private static unsafe void Hide(AddonImage* addon)
    {
        var root = addon->RootNode;

        if (root != null)
            root->Color.A = 0;
    }

    // IconId is only populated while the texture is loading. Once it has settled the id survives
    // nowhere but the file name of the loaded resource, so a path like ui/icon/120000/120001_hr1.tex
    // has to be parsed back into 120001.
    private static unsafe uint IconOf(AtkImageNode* node)
    {
        if (node == null)
            return 0;

        var parts = node->PartsList;
        if (parts == null || node->PartId >= parts->PartCount)
            return 0;

        var asset = parts->Parts[node->PartId].UldAsset;
        if (asset == null)
            return 0;

        var texture = asset->AtkTexture;
        if (texture.Resource == null)
            return 0;

        if (texture.Resource->IconId != 0)
            return texture.Resource->IconId;

        var handle = texture.Resource->TexFileResourceHandle;

        return handle == null ? 0 : IconInPath(handle->ResourceHandle.FileName.ToString());
    }

    private static uint IconInPath(string path)
    {
        var slash = path.LastIndexOf('/');
        var file = slash < 0 ? path : path[(slash + 1)..];

        var end = 0;
        while (end < file.Length && char.IsAsciiDigit(file[end]))
            end++;

        return end > 0 && uint.TryParse(file[..end], out var icon) ? icon : 0;
    }
}
