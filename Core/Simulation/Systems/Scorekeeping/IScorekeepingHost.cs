namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ScorekeepingSystem"/> needs from the world coordinator.
/// </summary>
internal interface IScorekeepingHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    MatchState MatchState { get; }
}
