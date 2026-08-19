using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

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
