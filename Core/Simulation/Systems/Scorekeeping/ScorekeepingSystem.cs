namespace OpenGarrison.Core;

/// <summary>
/// Round points: kills, assists, stabs, intel defense, objective captures, building
/// destruction, uber activation and healing.
/// </summary>
internal sealed partial class ScorekeepingSystem
{
    private readonly IScorekeepingHost _host;

    public ScorekeepingSystem(IScorekeepingHost host)
    {
        _host = host;
    }
}
