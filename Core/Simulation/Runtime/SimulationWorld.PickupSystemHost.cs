using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPickupHost
{
    WorldBounds IPickupHost.Bounds => Bounds;
    EntityStore IPickupHost.EntityStore => EntityStore;
    LastToDieState IPickupHost.LastToDieState => LastToDieState;
    SimpleLevel IPickupHost.Level => Level;
    PlayerTeam IPickupHost.LocalPlayerTeam => LocalPlayerTeam;
    SimulationRandomStreams IPickupHost.Randoms => Randoms;
    WorldObjectStore IPickupHost.WorldObjects => WorldObjects;

    int IPickupHost.AllocateEntityId() => AllocateEntityId();
    int IPickupHost.ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName, float soundX, float soundY) => DamageRules.ApplyHealingWithFeedback(target, healing, soundName, soundX, soundY);
    IEnumerable<PlayerEntity> IPickupHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    ExperimentalGameplaySettings IPickupHost.GetLastToDieGameplaySettings(PlayerEntity? player) => LastToDieRules.GetLastToDieGameplaySettings(player);
    bool IPickupHost.IsExperimentalPracticePowerOwner(PlayerEntity? player) => ExperimentalRules.IsExperimentalPracticePowerOwner(player);
    void IPickupHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId) => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    bool IPickupHost.ShouldCancelPickup(WorldPickupKind kind, PlayerEntity player, int pickupEntityId, string pickupValue, float x, float y) => ShouldCancelPickup(kind, player, pickupEntityId, pickupValue, x, y);
}
