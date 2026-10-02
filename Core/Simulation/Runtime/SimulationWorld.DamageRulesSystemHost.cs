namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IDamageRulesHost
{
    ExperimentalGameplaySettings IDamageRulesHost.GetLastToDieGameplaySettings(PlayerEntity? player)
        => LastToDieRules.GetLastToDieGameplaySettings(player);
    MatchSettingsState IDamageRulesHost.MatchSettings => MatchSettings;
    MatchState IDamageRulesHost.MatchState => MatchState;
    PresentationEventLog IDamageRulesHost.PresentationEvents => PresentationEvents;
    void IDamageRulesHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
}
