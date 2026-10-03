namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="DamageRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IDamageRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    LastToDieRulesSystem LastToDieRules { get; }
    MatchSettingsState MatchSettings { get; }
    MatchState MatchState { get; }
    PresentationEventLog PresentationEvents { get; }
    WorldEffectsSystem WorldEffects { get; }

}
