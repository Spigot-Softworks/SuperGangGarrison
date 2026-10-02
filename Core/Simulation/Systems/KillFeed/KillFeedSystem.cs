namespace OpenGarrison.Core;

/// <summary>
/// The kill feed and objective log: kill entries, announcements, and capture,
/// defense, intel and generator log lines.
/// </summary>
internal sealed partial class KillFeedSystem
{
    private readonly IKillFeedHost _host;

    public KillFeedSystem(IKillFeedHost host)
    {
        _host = host;
    }
}
