namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="PlayerRemainsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IPlayerRemainsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    bool ClientPredictionMode { get; }
    EntityStore EntityStore { get; }
    bool LocalGoreEffectsEnabled { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PresentationEventLog PresentationEvents { get; }
    SimulationRandomStreams Randoms { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    int ScaleBloodDropLifetimeTicks();
}
