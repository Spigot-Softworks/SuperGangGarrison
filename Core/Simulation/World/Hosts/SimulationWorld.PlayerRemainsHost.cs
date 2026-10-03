namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPlayerRemainsHost
{
    int IPlayerRemainsHost.AllocateEntityId()
        => AllocateEntityId();
    bool IPlayerRemainsHost.ClientPredictionMode => ClientPredictionMode;
    EntityStore IPlayerRemainsHost.EntityStore => EntityStore;
    bool IPlayerRemainsHost.LocalGoreEffectsEnabled => LocalGoreEffectsEnabled;
    PlayerDeathSystem IPlayerRemainsHost.PlayerDeaths => PlayerDeaths;
    PresentationEventLog IPlayerRemainsHost.PresentationEvents => PresentationEvents;
    SimulationRandomStreams IPlayerRemainsHost.Randoms => Randoms;
    int IPlayerRemainsHost.ScaleBloodDropLifetimeTicks()
        => ScaleBloodDropLifetimeTicks();
    WorldEffectsSystem IPlayerRemainsHost.WorldEffects => WorldEffects;
    WorldObjectStore IPlayerRemainsHost.WorldObjects => WorldObjects;
}
