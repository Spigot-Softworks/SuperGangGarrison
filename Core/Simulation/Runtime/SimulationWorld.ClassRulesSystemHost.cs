namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IClassRulesHost
{
    CharacterClassDefinition IClassRulesHost.GetNetworkPlayerClassDefinition(byte slot)
        => NetworkPlayerRules.GetNetworkPlayerClassDefinition(slot);
    PlayerTeam IClassRulesHost.GetNetworkPlayerConfiguredTeam(byte slot)
        => NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(slot);
    bool IClassRulesHost.IsNetworkPlayerAwaitingJoin(byte slot)
        => NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot);
    bool IClassRulesHost.IsNetworkPlayerEnabled(byte slot)
        => NetworkPlayerRules.IsNetworkPlayerEnabled(slot);
    bool IClassRulesHost.IsVipModeActive => IsVipModeActive;
    MatchSettingsState IClassRulesHost.MatchSettings => MatchSettings;
    NetworkPlayerRegistry IClassRulesHost.PlayerRegistry => PlayerRegistry;
    bool IClassRulesHost.TryFindSafeObjectiveSpawnPosition(PlayerEntity player, PlayerTeam team, float objectiveX, float objectiveY, out float spawnX, out float spawnY)
        => Spawns.TryFindSafeObjectiveSpawnPosition(player, team, objectiveX, objectiveY, out spawnX, out spawnY);
    VipState IClassRulesHost.VipState => VipState;
}
