namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="AdminCommandsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IAdminCommandsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    void KillPlayer(
        PlayerEntity player,
        bool gibbed = false,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default,
        string? deathCamMessage = null,
        SentryEntity? deathCamSentry = null,
        string? killFeedMessage = null,
        bool createDeathCam = true,
        bool spawnRemains = true,
        bool forceCorpseRemains = false,
        bool recordKillFeed = true,
        int assistingPlayerIdOverride = -1,
        bool completingLastToDieSpyAfterlifeDeath = false);
    void RegisterVisualEffect(
        string effectName,
        float x,
        float y,
        float directionDegrees = 0f,
        int count = 1,
        bool normalizeDirection = true);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    PlayerInputSnapshot ResolveNetworkPlayerInput(byte slot);
    bool TryBuildJumpPad(PlayerEntity player, bool ignoreMetalCost = false);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
    bool TrySetNetworkPlayerSpawnOverride(byte slot, float x, float y);
}
