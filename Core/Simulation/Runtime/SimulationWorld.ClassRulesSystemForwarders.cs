namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public int GetClassLimit(PlayerClass playerClass)
        => ClassRules.GetClassLimit(playerClass);
    public int GetUniformClassLimit()
        => ClassRules.GetUniformClassLimit();
    public void SetAllClassLimits(int limit)
        => ClassRules.SetAllClassLimits(limit);
    public void SetCaptureSpeedMultiplierPerPlayer(float multiplier)
        => ClassRules.SetCaptureSpeedMultiplierPerPlayer(multiplier);
    public void SetClassLimit(PlayerClass playerClass, int limit)
        => ClassRules.SetClassLimit(playerClass, limit);
    public void SetVipAllowDuplicateClasses(bool enabled)
        => ClassRules.SetVipAllowDuplicateClasses(enabled);
    public bool CanNetworkPlayerChangeTeamByMapBehavior(byte slot)
        => ClassRules.CanNetworkPlayerChangeTeamByMapBehavior(slot);
    public bool CanNetworkPlayerSelectClassByMapBehavior(byte slot, CharacterClassDefinition definition)
        => ClassRules.CanNetworkPlayerSelectClassByMapBehavior(slot, definition);
    public void SetNetworkPlayerMapSpawnClassBehaviorBypass(byte slot, bool bypass)
        => ClassRules.SetNetworkPlayerMapSpawnClassBehaviorBypass(slot, bypass);
    public bool TryGetMapSpawnClassBehavior(PlayerTeam team, out SpawnClassBehaviorMarker marker)
        => ClassRules.TryGetMapSpawnClassBehavior(team, out marker);
}
