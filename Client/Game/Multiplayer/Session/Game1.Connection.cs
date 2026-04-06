#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _pendingHostedConnectTicks = -1;
    private int _pendingHostedConnectPort = 8190;
    private string? _recentConnectHost;
    private int _recentConnectPort;

    private void BeginHostedGame(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        string? requestedMap,
        string? mapRotationFile)
    {
        PrepareHostedServerLaunchUi(closeHostSetup: true, disconnectNetworkClient: true);

        if (!TryStartHostedServerBackground(
                serverName,
                port,
                maxPlayers,
                password,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance,
                requestedMap,
                mapRotationFile,
                resetConsole: false,
                out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        BeginPendingHostedLocalConnect(port, delayTicks: 20, "Starting local server...");
    }

    private bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
    {
        if (_networkClient.Connect(host, port, _world.LocalPlayer.DisplayName, _world.LocalPlayer.BadgeMask, out var error))
        {
            RecordRecentConnection(host, port);
            ResetGameplayRuntimeState();
            CloseLobbyBrowser(clearStatus: false);
            SetNetworkStatus($"Connecting to {host}:{port}...");
            if (addConsoleFeedback)
            {
                AddNetworkConsoleLine($"connecting to {host}:{port} over udp");
            }

            return true;
        }

        SetNetworkStatus($"Connect failed: {error}");
        if (addConsoleFeedback)
        {
            AddNetworkConsoleLine($"connect failed: {error}");
        }

        return false;
    }

    private void ShowAutoBalanceNotice(string text, int seconds)
    {
        _autoBalanceNoticeText = text;
        _autoBalanceNoticeTicks = Math.Max(1, seconds * _config.TicksPerSecond);
    }

    private void CloseManualConnectMenu(bool clearStatus)
    {
        _manualConnectOpen = false;
        _editingConnectHost = false;
        _editingConnectPort = false;
        if (clearStatus)
        {
            _menuStatusMessage = string.Empty;
        }
    }

}
