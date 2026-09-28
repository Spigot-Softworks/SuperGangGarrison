#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class SessionManager
{
    private readonly ISessionContext _context;

    public SessionManager(ISessionContext context)
    {
        _context = context;
        Connection = new ConnectionFlowController(context);
    }

    public ConnectionFlowController Connection { get; }
}
