namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IKillFeedHost
{
    PresentationEventLog IKillFeedHost.PresentationEvents => PresentationEvents;
}
