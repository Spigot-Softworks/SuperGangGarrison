namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ClassRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IClassRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    bool IsVipModeActive { get; }
    MatchSettingsState MatchSettings { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    VipState VipState { get; }

    CharacterClassDefinition GetNetworkPlayerClassDefinition(byte slot);
    PlayerTeam GetNetworkPlayerConfiguredTeam(byte slot);
    bool IsNetworkPlayerAwaitingJoin(byte slot);
    bool IsNetworkPlayerEnabled(byte slot);
    bool TryFindSafeObjectiveSpawnPosition(
        PlayerEntity player,
        PlayerTeam team,
        float objectiveX,
        float objectiveY,
        out float spawnX,
        out float spawnY);
}
