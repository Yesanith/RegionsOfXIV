using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Tracks the minimap's on-screen rect so a companion label can be pinned to it.
// GW2 gets compass geometry from the Mumble link; FFXIV's minimap is a normal
// addon the user can move, resize and hide, so it is read off the AtkUnitBase and
// driven by IAddonLifecycle events rather than polled.
//
// ROADMAP Phase 0/7: verify whether _NaviMap already shows the current area
// before building this out. If it does, IDtrBar is the cheaper surface.
internal sealed class NaviMapAnchor : IDisposable
{
    private const string AddonName = "_NaviMap";

    // Screen-space rect. Zero-size when unavailable.
    public Vector4 Rect { get; private set; }

    public bool IsAvailable => this.Rect.Z > 0 && this.Rect.W > 0;

    public NaviMapAnchor()
    {
        // Setup/Refresh/Move only — PostUpdate fires every frame on a persistent
        // addon and there is nothing to recompute between moves. PostMove (API 14)
        // fires once the drag completes.
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, OnAddonEvent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, OnAddonEvent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostMove, AddonName, OnAddonEvent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostShow, AddonName, OnAddonEvent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostHide, AddonName, OnAddonEvent);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnAddonEvent);
    }

    private unsafe void OnAddonEvent(AddonEvent type, AddonArgs args)
    {
        try
        {
            // AddonArgs.Addon is an AtkUnitBasePtr since API 13, not a raw nint.
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon == null || !addon->IsVisible)
            {
                this.Rect = Vector4.Zero;
                return;
            }

            var scale = addon->Scale;
            var width = addon->RootNode != null ? addon->RootNode->Width * scale : 0f;
            var height = addon->RootNode != null ? addon->RootNode->Height * scale : 0f;

            this.Rect = new Vector4(addon->X, addon->Y, width, height);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to read _NaviMap geometry.");
            this.Rect = Vector4.Zero;
        }
    }
}
