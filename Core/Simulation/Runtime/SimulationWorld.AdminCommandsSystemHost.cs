namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IAdminCommandsHost
{
    NetworkPlayerSystem IAdminCommandsHost.NetworkPlayerRules => NetworkPlayerRules;
    PlayerDeathSystem IAdminCommandsHost.PlayerDeaths => PlayerDeaths;
    StructureSystem IAdminCommandsHost.Structures => Structures;
    WorldEffectsSystem IAdminCommandsHost.WorldEffects => WorldEffects;
}
