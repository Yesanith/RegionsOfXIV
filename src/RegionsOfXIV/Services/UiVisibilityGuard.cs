using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Owns when this plugin is allowed to draw, against Dalamud's automatic hiding.
//
// Dalamud hides every plugin's UI when the game hides its own, during cutscenes,
// and in gpose. Two of those need answering, for different reasons and on
// different timescales.
//
// **Loading screens**, suspended while one is up. Entering a duty runs the
// transition in two phases, and the second — the phase that brings the HUD back,
// where _DTR and _StatusCustom reappear — trips one of those conditions, which
// took our notification with it partway through. That moment is the entire point
// of the ZoneInit path.
//
// "Loading" is deliberately two tests. BetweenAreas covers ordinary zoning, but it
// is not reliably set across both phases of a duty transition; the NowLoading
// addon is present throughout, so it catches what the condition flag misses.
//
// **Cutscenes**, disabled outright. The animation is frame-driven — Update runs
// from Draw — so a hidden notification does not expire, it freezes, and resumes
// mid-animation whenever drawing comes back. Entering a dungeon showed this
// plainly: the text appeared, vanished as the cutscene began, then reappeared
// afterwards to finish an animation for somewhere you had already left.
//
// Drawing through the cutscene instead lets it play out and end on time. It does
// not make the plugin chatty during cutscenes: NotificationGate still refuses to
// start anything new while one is running, so the only thing that can appear is a
// notification that was already on screen when it began.
//
// DisableCutsceneUiHide rather than DisableAutomaticUiHide, which is the master
// switch and would take gpose and the user's own UI toggle with it.
internal sealed class UiVisibilityGuard : IDisposable
{
    private bool suspended;

    public UiVisibilityGuard()
    {
        Plugin.PluginInterface.UiBuilder.DisableCutsceneUiHide = true;

        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;

        Plugin.PluginInterface.UiBuilder.DisableCutsceneUiHide = false;

        // Never leave the hide suspended: it is global to this plugin, and a stale
        // "true" would keep the overlay drawing through gpose forever.
        if (this.suspended)
            Plugin.PluginInterface.UiBuilder.DisableAutomaticUiHide = false;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var loading = Plugin.Condition[ConditionFlag.BetweenAreas]
                      || Plugin.Condition[ConditionFlag.BetweenAreas51]
                      || IsLoadingScreenUp();

        if (loading == this.suspended)
            return;

        this.suspended = loading;
        Plugin.PluginInterface.UiBuilder.DisableAutomaticUiHide = loading;

        // Which of the three conditions actually fired is worth knowing — the fix
        // covers all of them, but only one is the real cause.
        if (loading)
        {
            Plugin.Log.Debug(
                "Loading screen up, suspending automatic UI hide " +
                $"(gameUiHidden={Plugin.GameGui.GameUiHidden}, " +
                $"cutscene={Plugin.PluginInterface.UiBuilder.CutsceneActive}, " +
                $"gpose={Plugin.ClientState.IsGPosing})");
        }
    }

    private static unsafe bool IsLoadingScreenUp()
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("NowLoading").Address;
        return addon != null && addon->IsVisible;
    }
}
