namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ClassRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IClassRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    bool IsVipModeActive { get; }
    MatchSettingsState MatchSettings { get; }
    NetworkPlayerSystem NetworkPlayerRules { get; }
    NetworkPlayerRegistry PlayerRegistry { get; }
    SpawnSystem Spawns { get; }
    VipState VipState { get; }

}
