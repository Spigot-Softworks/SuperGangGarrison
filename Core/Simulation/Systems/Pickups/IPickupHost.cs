using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="PickupSystem"/>.
/// </summary>
internal interface IPickupHost
{
    WorldBounds Bounds { get; }
    EntityStore EntityStore { get; }
    LastToDieState LastToDieState { get; }
    SimpleLevel Level { get; }
    PlayerTeam LocalPlayerTeam { get; }
    SimulationRandomStreams Randoms { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    bool IsExperimentalPracticePowerOwner(PlayerEntity? player);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    bool ShouldCancelPickup(WorldPickupKind kind, PlayerEntity player, int pickupEntityId, string pickupValue, float x, float y);
}
