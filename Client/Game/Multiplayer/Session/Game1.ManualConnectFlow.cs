#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void TryConnectFromMenu()
    {
        _connectionFlowController.TryConnectFromMenu();
    }

    private bool TryParseManualConnectTarget(out string host, out int port)
    {
        return _connectionFlowController.TryParseManualConnectTarget(out host, out port);
    }
}
