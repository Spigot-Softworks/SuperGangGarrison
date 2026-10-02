namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="WorldEffectsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IWorldEffectsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    EntityStore EntityStore { get; }
    CombatResolver GeometryResolver { get; }
    bool LocalGoreEffectsEnabled { get; }
    PresentationEventLog PresentationEvents { get; }
    SimulationRandomStreams Randoms { get; }
    bool SniperAimIndicatorEnabled { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    int ScaleBloodDropLifetimeTicks();
}
