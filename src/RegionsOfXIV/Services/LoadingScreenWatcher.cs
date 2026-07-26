using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace RegionsOfXIV.Services;

// Tracks whether a zone-transition loading screen is up.
//
// BetweenAreas covers ordinary zone changes; BetweenAreas51 covers the variants
// the game uses for instanced content and a few scripted transitions. Either one
// means the player is looking at a loading screen rather than the world.
internal sealed class LoadingScreenWatcher : IDisposable
{
    public event Action? LoadingStarted;

    public event Action? LoadingEnded;

    public bool IsLoading { get; private set; }

    public LoadingScreenWatcher()
    {
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose() => Plugin.Framework.Update -= OnFrameworkUpdate;

    private void OnFrameworkUpdate(IFramework framework)
    {
        var loading = Plugin.Condition[ConditionFlag.BetweenAreas]
                      || Plugin.Condition[ConditionFlag.BetweenAreas51];

        if (loading == IsLoading)
            return;

        IsLoading = loading;

        if (loading)
            LoadingStarted?.Invoke();
        else
            LoadingEnded?.Invoke();
    }
}
