#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void TryConnectFromMenu()
    {
        if (!TryParseManualConnectTarget(out var host, out var port))
        {
            return;
        }

        TryConnectToServer(host, port, addConsoleFeedback: false);
    }

    private bool TryParseManualConnectTarget(out string host, out int port)
    {
        host = _connectHostBuffer.Trim();
        port = 0;

        if (string.IsNullOrWhiteSpace(host))
        {
            _menuStatusMessage = "Host is required.";
            return false;
        }

        if (!int.TryParse(_connectPortBuffer.Trim(), out port) || port is <= 0 or > 65535)
        {
            _menuStatusMessage = "Port must be 1-65535.";
            return false;
        }

        return true;
    }
}
