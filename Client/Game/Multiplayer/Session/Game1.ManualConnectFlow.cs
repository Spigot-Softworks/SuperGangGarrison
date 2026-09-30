#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    public void TryConnectFromMenu()
    {
        _sessionManager.Connection.TryConnectFromMenu();
    }

    private bool TryParseManualConnectTarget(out string host, out int port)
    {
        return _sessionManager.Connection.TryParseManualConnectTarget(out host, out port);
    }

    private bool TryParseManualConnectTarget(out NetworkEndpoint endpoint)
    {
        return _sessionManager.Connection.TryParseManualConnectTarget(out endpoint);
    }
}
