namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IDamageRulesHost
{
    LastToDieRulesSystem IDamageRulesHost.LastToDieRules => LastToDieRules;
    MatchSettingsState IDamageRulesHost.MatchSettings => MatchSettings;
    MatchState IDamageRulesHost.MatchState => MatchState;
    PresentationEventLog IDamageRulesHost.PresentationEvents => PresentationEvents;
    WorldEffectsSystem IDamageRulesHost.WorldEffects => WorldEffects;
}
