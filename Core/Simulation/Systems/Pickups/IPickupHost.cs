using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="PickupSystem"/>.
/// </summary>
internal interface IPickupHost
{
    WorldBounds Bounds { get; }
    DamageRulesSystem DamageRules { get; }
    DecisionGate DecisionGate { get; }
    EntityStore EntityStore { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    LastToDieState LastToDieState { get; }
    SimpleLevel Level { get; }
    PlayerTeam LocalPlayerTeam { get; }
    SimulationRandomStreams Randoms { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
}
