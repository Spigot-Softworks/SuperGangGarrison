namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IWorldEffectsHost
{
    int IWorldEffectsHost.AllocateEntityId()
        => AllocateEntityId();
    EntityStore IWorldEffectsHost.EntityStore => EntityStore;
    CombatResolver IWorldEffectsHost.GeometryResolver => GeometryResolver;
    bool IWorldEffectsHost.LocalGoreEffectsEnabled => LocalGoreEffectsEnabled;
    PresentationEventLog IWorldEffectsHost.PresentationEvents => PresentationEvents;
    SimulationRandomStreams IWorldEffectsHost.Randoms => Randoms;
    int IWorldEffectsHost.ScaleBloodDropLifetimeTicks()
        => ScaleBloodDropLifetimeTicks();
    bool IWorldEffectsHost.SniperAimIndicatorEnabled => SniperAimIndicatorEnabled;
    WorldObjectStore IWorldEffectsHost.WorldObjects => WorldObjects;
}
