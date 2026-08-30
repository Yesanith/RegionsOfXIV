using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// What each _Image addon is currently showing, and what was decided about it. Kept apart from the
// addon pointers so the ordering rule in OnImage can be exercised without the game running.
internal sealed class BannerDecisions
{
    private readonly Dictionary<nint, (uint Icon, bool Taken)> showing = [];

    public int Count => this.showing.Count;

    public void Clear() => this.showing.Clear();

    // True when this addon is showing something it was not showing last frame. That transition is
    // the only moment the gate may be consulted -- see OnImage.
    public bool IsNew(nint addon, uint icon) =>
        !this.showing.TryGetValue(addon, out var state) || state.Icon != icon;

    public void Record(nint addon, uint icon, bool taken) => this.showing[addon] = (icon, taken);

    public bool IsTaken(nint addon) =>
        this.showing.TryGetValue(addon, out var state) && state.Taken;
}

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

    // Why a banner would be held back right now, or None. A delegate rather than the gate itself,
    // so this stays ignorant of what decides -- and of AnnouncementCoordinator, which reaches back
    // the other way through OnBannerShown.
    private readonly Func<BannerBlock> blockReason;

    private readonly BannerDecisions decisions = new();

    // Per watcher rather than static. A static set outlives the plugin being reloaded, so an id
    // logged once would stay silent for the rest of the game session -- including across the
    // reload someone does precisely to go and look at it.
    private readonly HashSet<uint> unnamed = [];

    public event Action<uint, string, BannerBlock>? OnBannerShown;

    public BannerWatcher(Configuration config, Func<BannerBlock> blockReason)
    {
        this.config = config;
        this.blockReason = blockReason;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, ImageAddons, OnImage);
    }

    public void Dispose() =>
        Plugin.AddonLifecycle.UnregisterListener(OnImage);

    private unsafe void OnImage(AddonEvent type, AddonArgs args)
    {
        if (!this.config.BannerNotificationEnabled)
        {
            if (this.decisions.Count > 0)
                this.decisions.Clear();

            return;
        }

        var addon = (AddonImage*)args.Addon.Address;
        if (addon == null)
            return;

        var which = (nint)addon;
        var icon = addon->IsVisible ? IconOf(addon->ImageNode) : 0;

        if (this.decisions.IsNew(which, icon))
            Decide(which, icon, args.AddonName);

        // Hidden from the decision recorded at the transition, never from a fresh one. This runs
        // on PreDraw for every frame the banner is up, and asking again would give a different
        // answer: announcing sets the gate's cooldown, so the very next frame would say no, the
        // alpha would stop being reset, and the game's banner would fade back in underneath our
        // own notification halfway through it.
        if (this.config.HideNativeBanner && this.decisions.IsTaken(which))
            Hide(addon);
    }

    // The one place the gate is consulted, for the whole plugin, and only when the icon has just
    // changed. The answer rides out on the event so the coordinator acts on this reading rather
    // than taking its own: the two would be separate reads of the clock, and a cooldown expiring
    // between them would leave the game's banner up while the replacement was pushed over it.
    //
    // It is asked before the event goes out rather than after, because handling that event is what
    // sets the cooldown -- the question here is whether this banner gets replaced, not whether a
    // second one could follow it.
    private void Decide(nint which, uint icon, string addonName)
    {
        var name = icon == 0 ? null : BannerNameResolver.Resolve(icon);

        if (name is null)
        {
            this.decisions.Record(which, icon, taken: false);

            if (icon != 0 && this.unnamed.Add(icon))
                Log.Information(
                    $"Banner icon {icon} appeared on {addonName} with no name for it yet. "
                    + "Report this id and it can be added.");

            return;
        }

        var reason = this.blockReason();

        this.decisions.Record(which, icon, reason == BannerBlock.None);

        // Raised even when the gate refuses. The coordinator logs every banner it hears about, and
        // that line is the only view there is of what gets turned down and why.
        OnBannerShown?.Invoke(icon, name, reason);
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
