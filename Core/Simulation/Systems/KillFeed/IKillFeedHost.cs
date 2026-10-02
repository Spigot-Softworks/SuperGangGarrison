namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="KillFeedSystem"/> needs from the world coordinator.
/// </summary>
internal interface IKillFeedHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    PresentationEventLog PresentationEvents { get; }
}
