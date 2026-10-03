namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IClassRulesHost
{
    bool IClassRulesHost.IsVipModeActive => VipRules.IsVipModeActive;
    MatchSettingsState IClassRulesHost.MatchSettings => MatchSettings;
    NetworkPlayerSystem IClassRulesHost.NetworkPlayerRules => NetworkPlayerRules;
    NetworkPlayerRegistry IClassRulesHost.PlayerRegistry => PlayerRegistry;
    SpawnSystem IClassRulesHost.Spawns => Spawns;
    VipState IClassRulesHost.VipState => VipState;
}
