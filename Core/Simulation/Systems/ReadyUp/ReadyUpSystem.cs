namespace OpenGarrison.Core;

/// <summary>
/// Competitive ready-up: ready flags, the skirmish, countdown, setup and live
/// phases, and objective setup holds. State lives in the ready-up store; this
/// system owns the rules.
/// </summary>
internal sealed partial class ReadyUpSystem
{
    private readonly IReadyUpHost _host;

    public ReadyUpSystem(IReadyUpHost host)
    {
        _host = host;
    }
}
