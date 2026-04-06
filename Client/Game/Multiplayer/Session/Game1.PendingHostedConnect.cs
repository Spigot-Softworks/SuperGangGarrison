#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdatePendingHostedConnect()
    {
        if (_pendingHostedConnectTicks < 0)
        {
            return;
        }

        if (_networkClient.IsConnected)
        {
            CancelPendingHostedLocalConnect();
            return;
        }

        if (_hostedServerRuntime.HasTrackedProcessExited)
        {
            CancelPendingHostedLocalConnect("Local server exited before connect.");
            return;
        }

        if (_pendingHostedConnectTicks > 0)
        {
            _pendingHostedConnectTicks -= 1;
            return;
        }

        CancelPendingHostedLocalConnect();
        TryConnectToServer("127.0.0.1", _pendingHostedConnectPort, addConsoleFeedback: false);
    }
}
