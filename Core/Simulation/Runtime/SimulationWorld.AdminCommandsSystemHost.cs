namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IAdminCommandsHost
{
    void IAdminCommandsHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath)
        => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    void IAdminCommandsHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IAdminCommandsHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    PlayerInputSnapshot IAdminCommandsHost.ResolveNetworkPlayerInput(byte slot)
        => NetworkPlayerRules.ResolveNetworkPlayerInput(slot);
    bool IAdminCommandsHost.TryBuildJumpPad(PlayerEntity player, bool ignoreMetalCost)
        => Structures.TryBuildJumpPad(player, ignoreMetalCost);
    bool IAdminCommandsHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player)
        => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
    bool IAdminCommandsHost.TrySetNetworkPlayerSpawnOverride(byte slot, float x, float y)
        => NetworkPlayerRules.TrySetNetworkPlayerSpawnOverride(slot, x, y);
}
