using System;
using OpenGarrison.Core;

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

public readonly record struct OpenGarrisonServerTeamChangeRequest(
    byte Slot,
    PlayerTeam Team);

public readonly record struct OpenGarrisonServerClassChangeRequest(
    byte Slot,
    PlayerClass PlayerClass);

public readonly record struct OpenGarrisonServerLoadoutChangeRequest(
    byte Slot,
    string LoadoutId);

public readonly record struct OpenGarrisonServerMapChangeRequest(
    string LevelName,
    int MapAreaIndex,
    bool PreservePlayerStats);

public readonly record struct OpenGarrisonServerSpawnRequest(
    long Frame,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    float WorldX,
    float WorldY,
    bool IsRespawn);

public readonly record struct OpenGarrisonServerDamageRequest(
    long Frame,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    int TargetPlayerId,
    PlayerTeam? TargetTeam,
    int AttackerPlayerId,
    PlayerTeam? AttackerTeam,
    int Amount,
    bool WouldBeFatal,
    float WorldX,
    float WorldY);

public readonly record struct OpenGarrisonServerDeathRequest(
    long Frame,
    byte Slot,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam VictimTeam,
    PlayerClass VictimClass,
    int KillerPlayerId,
    string KillerName,
    PlayerTeam? KillerTeam,
    string WeaponSpriteName,
    bool Gibbed);

public readonly record struct OpenGarrisonServerPickupRequest(
    long Frame,
    string Kind,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    int PickupEntityId,
    string PickupValue,
    float WorldX,
    float WorldY);

public readonly record struct OpenGarrisonServerScoreRequest(
    long Frame,
    PlayerTeam Team,
    int Delta,
    int RedCaps,
    int BlueCaps,
    int ActorPlayerId,
    string Reason);

public readonly record struct OpenGarrisonServerRoundEndRequest(
    long Frame,
    GameModeKind GameMode,
    PlayerTeam? WinnerTeam,
    int RedCaps,
    int BlueCaps,
    string Reason);

public readonly record struct OpenGarrisonServerDecisionResult(
    bool IsCancelled,
    string Reason = "")
{
    public static OpenGarrisonServerDecisionResult Continue { get; } = new(false);

    public static OpenGarrisonServerDecisionResult Cancel(string reason = "") => new(true, reason);
}

public interface IOpenGarrisonServerDecisionHooks
{
    OpenGarrisonServerDecisionResult BeforeChatMessage(ChatReceivedEvent e);

    OpenGarrisonServerDecisionResult BeforeTeamChange(OpenGarrisonServerTeamChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeClassChange(OpenGarrisonServerClassChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeLoadoutChange(OpenGarrisonServerLoadoutChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeMapChange(OpenGarrisonServerMapChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeSpawn(OpenGarrisonServerSpawnRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeDamage(OpenGarrisonServerDamageRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeDeath(OpenGarrisonServerDeathRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforePickup(OpenGarrisonServerPickupRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeScore(OpenGarrisonServerScoreRequest e) => OpenGarrisonServerDecisionResult.Continue;

    OpenGarrisonServerDecisionResult BeforeRoundEnd(OpenGarrisonServerRoundEndRequest e) => OpenGarrisonServerDecisionResult.Continue;
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
