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
            ClearHostedSocialPresenceEndpoint();
            _lastToDieConnectionPresentationPending = false;
            CancelPendingHostedLocalConnect(BuildHostedServerExitMessage());
            return;
        }

        if (_pendingHostedConnectTicks > 0)
        {
            _pendingHostedConnectTicks -= 1;
            return;
        }

        // The server selects and reserves its actual port. Never connect to
        // the requested port while another instance may still own it.
        if (_hostedServerRuntime.ReadyPort is not { } readyPort) return;
        _pendingHostedConnectPort = readyPort;
        if (_hostedSocialPresenceUdpPort > 0 || !string.IsNullOrEmpty(_hostedSocialPresenceRelayGuestUrl))
            SetHostedSocialPresenceEndpoint(readyPort, _hostedSocialPresenceRelayGuestUrl);
        CancelPendingHostedLocalConnect();
        if (!TryConnectToServer(NetworkEndpoint.ForUdp("127.0.0.1", _pendingHostedConnectPort), addConsoleFeedback: false))
        {
            // An immediate socket/permission failure never reaches the connected
            // frame pump. Release the owned server and the LTD loading state here.
            var returnToLastToDie = _lastToDieConnectionPresentationPending;
            var error = _menuStatusMessage;
            ReturnToMainMenuWithNetworkStatus(error);
            if (returnToLastToDie) OpenLastToDieMenu(error);
        }
    }
}
