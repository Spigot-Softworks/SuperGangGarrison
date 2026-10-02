namespace OpenGarrison.Core;

/// <summary>
/// Class selection rules: class limits, VIP duplicate-class rules, the per-player
/// capture speed setting, and map spawn-class behaviors (forced classes, team-change
/// locks and manual spawn points).
/// </summary>
internal sealed partial class ClassRulesSystem
{
    private readonly IClassRulesHost _host;

    public ClassRulesSystem(IClassRulesHost host)
    {
        _host = host;
    }
}
