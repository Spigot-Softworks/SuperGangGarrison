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
        string rconPassword,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        bool secondaryAbilitiesEnabled,
        string? requestedMap,
        string? mapRotationFile)
    {
        _gameplaySessionController.BeginHostedGame(
            serverName,
            port,
            maxPlayers,
            password,
            rconPassword,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance,
            secondaryAbilitiesEnabled,
            requestedMap,
            mapRotationFile);
    }

    private bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
    {
        return _gameplaySessionController.TryConnectToServer(host, port, addConsoleFeedback);
    }

    private bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback)
    {
        return _gameplaySessionController.TryConnectToServer(endpoint, addConsoleFeedback);
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
