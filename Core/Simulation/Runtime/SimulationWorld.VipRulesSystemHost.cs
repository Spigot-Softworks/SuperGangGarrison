namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IVipRulesHost
{
    bool IVipRulesHost.CanNetworkPlayerChangeTeamByMapBehavior(byte slot)
        => ClassRules.CanNetworkPlayerChangeTeamByMapBehavior(slot);
    bool IVipRulesHost.CanNetworkPlayerSelectClassByMapBehavior(byte slot, CharacterClassDefinition definition)
        => ClassRules.CanNetworkPlayerSelectClassByMapBehavior(slot, definition);
    bool IVipRulesHost.ControlPointSetupActive => ControlPointSetupActive;
    IEnumerable<(byte Slot, PlayerEntity Player)> IVipRulesHost.EnumerateActiveNetworkPlayers()
        => NetworkPlayerRules.EnumerateActiveNetworkPlayers();
    bool IVipRulesHost.IsNetworkPlayerAwaitingJoin(byte slot)
        => NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot);
    PlayerEntity IVipRulesHost.LocalPlayer => LocalPlayer;
    MatchRules IVipRulesHost.MatchRules => MatchRules;
    MatchState IVipRulesHost.MatchState { get => MatchState; set => MatchState = value; }
    ObjectiveStateStore IVipRulesHost.Objectives => Objectives;
    SimulationRandomStreams IVipRulesHost.Randoms => Randoms;
    void IVipRulesHost.SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player)
        => SyncExperimentalGameplayLoadout(slot, player);
    bool IVipRulesHost.TryApplyNetworkPlayerClassChange(byte slot, CharacterClassDefinition definition, bool enforceClassLimit)
        => NetworkPlayerRules.TryApplyNetworkPlayerClassChange(slot, definition, enforceClassLimit);
    bool IVipRulesHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player)
        => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
    bool IVipRulesHost.TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot)
        => NetworkPlayerRules.TryGetNetworkPlayerSlot(player, out slot);
    bool IVipRulesHost.TrySetNetworkPlayerClassDefinition(byte slot, CharacterClassDefinition definition)
        => NetworkPlayerRules.TrySetNetworkPlayerClassDefinition(slot, definition);
    bool IVipRulesHost.TrySetNetworkPlayerTeam(byte slot, PlayerTeam team, bool respawnLivePlayerImmediately)
        => NetworkPlayerRules.TrySetNetworkPlayerTeam(slot, team, respawnLivePlayerImmediately);
    VipState IVipRulesHost.VipState => VipState;
}
