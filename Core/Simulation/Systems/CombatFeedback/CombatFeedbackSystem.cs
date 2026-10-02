namespace OpenGarrison.Core;

/// <summary>
/// Combat feedback rules: combo hits, kill streaks and multi-kill announcements,
/// dominations and revenge, afterburn alert bubbles, and the flames an airblast
/// knocks off a burning target.
/// </summary>
internal sealed partial class CombatFeedbackSystem
{
    private readonly ICombatFeedbackHost _host;

    public CombatFeedbackSystem(ICombatFeedbackHost host)
    {
        _host = host;
    }
}
