#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class HostingManager
{
    private readonly IHostingContext _context;

    public HostingManager(IHostingContext context)
    {
        _context = context;
        HostSetup = new HostSetupFlowController(context);
    }

    public HostSetupFlowController HostSetup { get; }
}
