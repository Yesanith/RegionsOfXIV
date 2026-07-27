using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Keeps the loading-screen notification on screen for the whole loading screen.
//
// Dalamud hides every plugin's UI when the game hides its own, during cutscenes,
// and in gpose. Entering a duty runs the transition in two phases, and the second
// one — the phase that brings the HUD back, where _DTR and _StatusCustom reappear
// — trips one of those conditions, which takes our notification with it partway
// through.
//
// That moment is the entire point of the ZoneInit path, so the automatic hide is
// suspended while a loading screen is up and restored the instant it ends. Real
// cutscenes and gpose are unaffected: neither happens behind a loading screen.
//
// "Loading" is deliberately two tests. BetweenAreas covers ordinary zoning, but it
// is not reliably set across both phases of a duty transition; the NowLoading
// addon is present throughout, so it catches what the condition flag misses.
internal sealed class LoadingScreenUiGuard : IDisposable
{
    private bool suspended;

    public LoadingScreenUiGuard() => Plugin.Framework.Update += OnFrameworkUpdate;

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;

        // Never leave the hide suspended: it is global to this plugin, and a stale
        // "true" would keep the overlay drawing through cutscenes forever.
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
