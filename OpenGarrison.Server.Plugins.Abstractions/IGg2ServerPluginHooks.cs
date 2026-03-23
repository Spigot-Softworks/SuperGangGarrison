using System;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerLifecycleHooks
{
    void OnServerStarting();

    void OnServerStarted();

    void OnServerStopping();

    void OnServerStopped();
}

public interface IOpenGarrisonServerUpdateHooks
{
    void OnServerHeartbeat(TimeSpan uptime);
}

public interface IOpenGarrisonServerClientHooks
{
    void OnHelloReceived(HelloReceivedEvent e);

    void OnClientConnected(ClientConnectedEvent e);

    void OnClientDisconnected(ClientDisconnectedEvent e);

    void OnPasswordAccepted(PasswordAcceptedEvent e);

    void OnPlayerTeamChanged(PlayerTeamChangedEvent e);

    void OnPlayerClassChanged(PlayerClassChangedEvent e);
}

public interface IOpenGarrisonServerChatHooks
{
    void OnChatReceived(ChatReceivedEvent e);
}

public interface IOpenGarrisonServerChatCommandHooks
{
    bool TryHandleChatMessage(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e);
}

public interface IOpenGarrisonServerMapHooks
{
    void OnMapChanging(MapChangingEvent e);

    void OnMapChanged(MapChangedEvent e);
}

public interface IOpenGarrisonServerGameplayHooks
{
    void OnScoreChanged(ScoreChangedEvent e);

    void OnRoundEnded(RoundEndedEvent e);

    void OnKillFeedEntry(KillFeedEvent e);
}
