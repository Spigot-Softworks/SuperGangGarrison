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
        _gameplayManager.Session.BeginHostedGame(
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

    public bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
    {
        return _gameplayManager.Session.TryConnectToServer(host, port, addConsoleFeedback);
    }

    public bool TryConnectLegacyGg2Server(string host, int port, bool addConsoleFeedback)
    {
        return _gameplayManager.Session.TryConnectLegacyGg2Server(host, port, addConsoleFeedback);
    }

    public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback)
    {
        return _gameplayManager.Session.TryConnectToServer(endpoint, addConsoleFeedback);
    }

    public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback, OnlineConnectionIntent intent)
    {
        return _gameplayManager.Session.TryConnectToServer(endpoint, addConsoleFeedback, intent);
    }

    public bool TryPlayLegacyReplay(string replayPath, bool addConsoleFeedback, bool clearQueuedReplays = true)
    {
        return _gameplayManager.Session.TryPlayLegacyReplay(replayPath, addConsoleFeedback, clearQueuedReplays);
    }

    public bool TryPlayOpenGarrisonDemo(string demoPath, bool addConsoleFeedback)
    {
        return _gameplayManager.Session.TryPlayOpenGarrisonDemo(demoPath, addConsoleFeedback);
    }

    private bool TrySeekOpenGarrisonDemo(int deltaMilliseconds, out int targetMilliseconds, out string error)
    {
        return _gameplayManager.Session.TrySeekOpenGarrisonDemo(deltaMilliseconds, out targetMilliseconds, out error);
    }

    private void ShowAutoBalanceNotice(string text, int seconds)
    {
        _sessionManager.Connection.ShowAutoBalanceNotice(text, seconds);
    }

    public void CloseManualConnectMenu(bool clearStatus)
    {
        _sessionManager.Connection.CloseManualConnectMenu(clearStatus);
    }

}
