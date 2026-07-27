using Dalamud.Game.ClientState.Conditions;

namespace RegionsOfXIV.Services;

// The game's state, reduced to the questions NotificationGate actually asks of
// it.
//
// This exists so the gate can be reasoned about on its own. The gate holds real
// policy — which tiers count, how long a coarse announcement keeps a finer one
// quiet, which moments are bad ones — and that policy is worth checking, but it
// was unreachable while every answer came from statics that throw when touched
// off the framework thread.
//
// Nothing here decides anything. It reports, and the gate judges.
internal interface IGameState
{
    bool IsLoggedIn { get; }

    // Mid-loading-screen. Both flags, because neither is set across the whole of
    // a duty transition on its own.
    bool IsBetweenAreas { get; }

    bool IsInCutscene { get; }

    bool IsPvP { get; }

    bool IsGPosing { get; }

    bool IsInCombat { get; }

    bool IsBoundByDuty { get; }
}

// Framework thread only: ICondition and IClientState throw when touched from
// anywhere else.
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
