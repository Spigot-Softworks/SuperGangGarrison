using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPickupHost
{
    int IPickupHost.AllocateEntityId() => AllocateEntityId();
    WorldBounds IPickupHost.Bounds => Bounds;
    DamageRulesSystem IPickupHost.DamageRules => DamageRules;
    DecisionGate IPickupHost.DecisionGate => DecisionGate;
    EntityStore IPickupHost.EntityStore => EntityStore;
    IEnumerable<PlayerEntity> IPickupHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    ExperimentalRulesSystem IPickupHost.ExperimentalRules => ExperimentalRules;
    LastToDieRulesSystem IPickupHost.LastToDieRules => LastToDieRules;
    LastToDieState IPickupHost.LastToDieState => LastToDieState;
    SimpleLevel IPickupHost.Level => Level;
    PlayerTeam IPickupHost.LocalPlayerTeam => LocalPlayerTeam;
    SimulationRandomStreams IPickupHost.Randoms => Randoms;
    WorldEffectsSystem IPickupHost.WorldEffects => WorldEffects;
    WorldObjectStore IPickupHost.WorldObjects => WorldObjects;
}
