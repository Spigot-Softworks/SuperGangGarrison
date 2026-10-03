namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IAdminCommandsHost
{
    NetworkPlayerSystem IAdminCommandsHost.NetworkPlayers => NetworkPlayers;
    PlayerDeathSystem IAdminCommandsHost.PlayerDeaths => PlayerDeaths;
    StructureSystem IAdminCommandsHost.Structures => Structures;
    WorldEffectsSystem IAdminCommandsHost.WorldEffects => WorldEffects;
}
