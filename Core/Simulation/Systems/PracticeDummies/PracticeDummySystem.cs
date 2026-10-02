namespace OpenGarrison.Core;

/// <summary>
/// Practice dummies: the local enemy and friendly training dummies, their respawn
/// and positioning, and the DPS meter. State lives in <see cref="PracticeDummyState"/>;
/// this system owns the rules.
/// </summary>
internal sealed partial class PracticeDummySystem
{
    private readonly IPracticeDummyHost _host;

    public PracticeDummySystem(IPracticeDummyHost host)
    {
        _host = host;
    }
}
