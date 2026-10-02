namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="PlayerPresentationBoundsSystem"/> needs from the world coordinator.
/// </summary>
internal interface IPlayerPresentationBoundsHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    CombatRuntimeState CombatRuntime { get; }

    bool IsPlayerHumiliated(PlayerEntity player);
}
