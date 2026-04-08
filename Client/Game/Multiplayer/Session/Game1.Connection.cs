#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
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
        _gameplaySessionController.BeginHostedGame(
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
            mapRotationFile);
    }

    private bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
    {
        return _gameplaySessionController.TryConnectToServer(host, port, addConsoleFeedback);
    }

    private void ShowAutoBalanceNotice(string text, int seconds)
    {
        _connectionFlowController.ShowAutoBalanceNotice(text, seconds);
    }

    private void CloseManualConnectMenu(bool clearStatus)
    {
        _connectionFlowController.CloseManualConnectMenu(clearStatus);
    }

}
