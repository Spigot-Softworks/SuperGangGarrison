namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="VipRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IVipRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    bool ControlPointSetupActive { get; }
    PlayerEntity LocalPlayer { get; }
    MatchRules MatchRules { get; }
    MatchState MatchState { get; set; }
    ObjectiveStateStore Objectives { get; }
    SimulationRandomStreams Randoms { get; }
    VipState VipState { get; }

    bool CanNetworkPlayerChangeTeamByMapBehavior(byte slot);
    bool CanNetworkPlayerSelectClassByMapBehavior(byte slot, CharacterClassDefinition definition);
    IEnumerable<(byte Slot, PlayerEntity Player)> EnumerateActiveNetworkPlayers();
    bool IsNetworkPlayerAwaitingJoin(byte slot);
    void SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player);
    bool TryApplyNetworkPlayerClassChange(byte slot, CharacterClassDefinition definition, bool enforceClassLimit = true);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
    bool TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot);
    bool TrySetNetworkPlayerClassDefinition(byte slot, CharacterClassDefinition definition);
    bool TrySetNetworkPlayerTeam(byte slot, PlayerTeam team, bool respawnLivePlayerImmediately = false);
}
