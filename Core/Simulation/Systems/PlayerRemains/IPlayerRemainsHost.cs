namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="PlayerRemainsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IPlayerRemainsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    bool ClientPredictionMode { get; }
    EntityStore EntityStore { get; }
    bool LocalGoreEffectsEnabled { get; }
    PresentationEventLog PresentationEvents { get; }
    SimulationRandomStreams Randoms { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    void RegisterVisualEffect(
        string effectName,
        float x,
        float y,
        float directionDegrees = 0f,
        int count = 1,
        bool normalizeDirection = true);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    int ScaleBloodDropLifetimeTicks();
    void SpawnDeadBody(
        PlayerEntity player,
        DeadBodyAnimationKind animationKind = DeadBodyAnimationKind.Default,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        SentryEntity? knockbackOriginSentry = null);
}
