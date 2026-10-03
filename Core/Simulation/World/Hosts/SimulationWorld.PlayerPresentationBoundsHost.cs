namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPlayerPresentationBoundsHost
{
    CombatRuntimeState IPlayerPresentationBoundsHost.CombatRuntime => CombatRuntime;
    bool IPlayerPresentationBoundsHost.IsPlayerHumiliated(PlayerEntity player)
        => IsPlayerHumiliated(player);
}
