namespace OpenGarrison.Core;

/// <summary>
/// Experimental gameplay rules: engineer perks and experimental sentry fire,
/// practice-power damage/kill/healing rewards and multipliers, and rage and
/// ghost-dash timing. State lives on players, sentries and
/// <see cref="CombatRuntimeState"/>; this system owns the rules.
/// </summary>
internal sealed partial class ExperimentalRulesSystem
{
    private readonly IExperimentalRulesHost _host;

    public ExperimentalRulesSystem(IExperimentalRulesHost host)
    {
        _host = host;
    }
}
