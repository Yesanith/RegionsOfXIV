using Dalamud.Game.ClientState.Conditions;

namespace RegionsOfXIV.Services;

internal interface IGameState
{
    bool IsLoggedIn { get; }

    bool IsBetweenAreas { get; }

    bool IsInCutscene { get; }

    bool IsPvP { get; }

    bool IsGPosing { get; }

    bool IsInCombat { get; }

    bool IsBoundByDuty { get; }
}

internal sealed class DalamudGameState : IGameState
{
    public bool IsLoggedIn => Plugin.ClientState.IsLoggedIn;

    public bool IsBetweenAreas =>
        Plugin.Condition[ConditionFlag.BetweenAreas] ||
        Plugin.Condition[ConditionFlag.BetweenAreas51];

    public bool IsInCutscene =>
        Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
        Plugin.Condition[ConditionFlag.WatchingCutscene] ||
        Plugin.Condition[ConditionFlag.WatchingCutscene78];

    public bool IsPvP => Plugin.ClientState.IsPvP;

    public bool IsGPosing => Plugin.ClientState.IsGPosing;

    public bool IsInCombat => Plugin.Condition[ConditionFlag.InCombat];

    public bool IsBoundByDuty => Plugin.Condition[ConditionFlag.BoundByDuty];
}
