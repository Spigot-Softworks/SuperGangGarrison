namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="RoomEffectsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IRoomEffectsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    PlayerDeathSystem PlayerDeaths { get; }
    WorldEffectsSystem WorldEffects { get; }
}
