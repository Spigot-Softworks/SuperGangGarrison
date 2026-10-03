namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="AdminCommandsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IAdminCommandsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    NetworkPlayerSystem NetworkPlayers { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    StructureSystem Structures { get; }
    WorldEffectsSystem WorldEffects { get; }
}
