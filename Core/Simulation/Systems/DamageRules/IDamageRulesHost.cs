namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="DamageRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IDamageRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    MatchSettingsState MatchSettings { get; }
    MatchState MatchState { get; }
    PresentationEventLog PresentationEvents { get; }

    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
}
