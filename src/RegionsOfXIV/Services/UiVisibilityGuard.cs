using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Dalamud hides plugin windows during cutscenes and whenever the game UI is hidden. The overlay
// opts out of that so a notification survives a cutscene, which then means it also has to be
// suppressed manually while a loading screen is up -- otherwise it draws over the black.
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

        if (loading)
        {
            Log.Debug(
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
