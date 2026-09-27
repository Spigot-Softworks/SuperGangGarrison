using System;
using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Hooks a server plugin can implement to observe server lifecycle transitions.
/// </summary>
public interface IOpenGarrisonServerLifecycleHooks
{
    /// <summary>Called when the server is starting, before it is ready.</summary>
    void OnServerStarting();

    /// <summary>Called after the server has started.</summary>
    void OnServerStarted();

    /// <summary>Called when the server is stopping.</summary>
    void OnServerStopping();

    /// <summary>Called after the server has stopped.</summary>
    void OnServerStopped();
}

/// <summary>
/// Hooks a server plugin can implement to receive periodic server heartbeats.
/// </summary>
public interface IOpenGarrisonServerUpdateHooks
{
    /// <summary>
    /// Called on each server heartbeat.
    /// </summary>
    /// <param name="uptime">The server uptime.</param>
    void OnServerHeartbeat(TimeSpan uptime);
}

/// <summary>
/// Hooks a server plugin can implement to observe client connection and identity changes.
/// </summary>
public interface IOpenGarrisonServerClientHooks
{
    /// <summary>Called when a hello is received from a connecting client.</summary>
    /// <param name="e">The hello event.</param>
    void OnHelloReceived(HelloReceivedEvent e);

    /// <summary>Called when a client connects.</summary>
    /// <param name="e">The connect event.</param>
    void OnClientConnected(ClientConnectedEvent e);

    /// <summary>Called when a client disconnects.</summary>
    /// <param name="e">The disconnect event.</param>
    void OnClientDisconnected(ClientDisconnectedEvent e);

    /// <summary>Called when a client's password is accepted.</summary>
    /// <param name="e">The password event.</param>
    void OnPasswordAccepted(PasswordAcceptedEvent e);

    /// <summary>Called when a player's team changes.</summary>
    /// <param name="e">The team change event.</param>
    void OnPlayerTeamChanged(PlayerTeamChangedEvent e);

    /// <summary>Called when a player's class changes.</summary>
    /// <param name="e">The class change event.</param>
    void OnPlayerClassChanged(PlayerClassChangedEvent e);
}

/// <summary>
/// Hooks a server plugin can implement to observe chat.
/// </summary>
public interface IOpenGarrisonServerChatHooks
{
    /// <summary>
    /// Called when a chat message is received.
    /// </summary>
    /// <param name="e">The chat event.</param>
    void OnChatReceived(ChatReceivedEvent e);
}

/// <summary>
/// Hooks a server plugin can implement to handle chat messages as commands.
/// </summary>
public interface IOpenGarrisonServerChatCommandHooks
{
    /// <summary>
    /// Tries to handle a chat message as a plugin command.
    /// </summary>
    /// <param name="context">The chat message context.</param>
    /// <param name="e">The chat event.</param>
    /// <returns>True when the plugin handled the message.</returns>
    bool TryHandleChatMessage(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e);
}

/// <summary>
/// A requested team change, used by decision hooks.
/// </summary>
/// <param name="Slot">The player's slot.</param>
/// <param name="Team">The requested team.</param>
public readonly record struct OpenGarrisonServerTeamChangeRequest(
    byte Slot,
    PlayerTeam Team);

/// <summary>
/// A requested class change, used by decision hooks.
/// </summary>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerClass">The requested class.</param>
public readonly record struct OpenGarrisonServerClassChangeRequest(
    byte Slot,
    PlayerClass PlayerClass);

/// <summary>
/// A requested loadout change, used by decision hooks.
/// </summary>
/// <param name="Slot">The player's slot.</param>
/// <param name="LoadoutId">The requested loadout id.</param>
public readonly record struct OpenGarrisonServerLoadoutChangeRequest(
    byte Slot,
    string LoadoutId);

/// <summary>
/// A requested map change, used by decision hooks.
/// </summary>
/// <param name="LevelName">The requested level name.</param>
/// <param name="MapAreaIndex">The requested map area index.</param>
/// <param name="PreservePlayerStats">Whether player stats are preserved.</param>
public readonly record struct OpenGarrisonServerMapChangeRequest(
    string LevelName,
    int MapAreaIndex,
    bool PreservePlayerStats);

/// <summary>
/// A requested player spawn, used by decision hooks.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerId">The player id.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="Team">The player's team.</param>
/// <param name="PlayerClass">The player's class.</param>
/// <param name="WorldX">The spawn world X.</param>
/// <param name="WorldY">The spawn world Y.</param>
/// <param name="IsRespawn">Whether this is a respawn.</param>
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

/// <summary>
/// A requested damage application, used by decision hooks.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="TargetKind">The kind of entity targeted.</param>
/// <param name="TargetEntityId">The target entity id.</param>
/// <param name="TargetPlayerId">The target player id.</param>
/// <param name="TargetTeam">The target's team, if any.</param>
/// <param name="AttackerPlayerId">The attacking player id.</param>
/// <param name="AttackerTeam">The attacker's team, if any.</param>
/// <param name="Amount">The damage amount.</param>
/// <param name="WouldBeFatal">Whether the damage would be fatal.</param>
/// <param name="WorldX">The world X of the damage.</param>
/// <param name="WorldY">The world Y of the damage.</param>
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

/// <summary>
/// A requested player death, used by decision hooks.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Slot">The victim's slot.</param>
/// <param name="VictimPlayerId">The victim player id.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim's team.</param>
/// <param name="VictimClass">The victim's class.</param>
/// <param name="KillerPlayerId">The killer player id.</param>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer's team, if any.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
/// <param name="Gibbed">Whether the victim was gibbed.</param>
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

/// <summary>
/// A requested item pickup, used by decision hooks.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Kind">The pickup kind.</param>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerId">The player id.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="Team">The player's team.</param>
/// <param name="PickupEntityId">The pickup entity id.</param>
/// <param name="PickupValue">The pickup value.</param>
/// <param name="WorldX">The world X of the pickup.</param>
/// <param name="WorldY">The world Y of the pickup.</param>
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

/// <summary>
/// A requested score change, used by decision hooks.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Team">The scoring team.</param>
/// <param name="Delta">The score delta.</param>
/// <param name="RedCaps">The red team caps.</param>
/// <param name="BlueCaps">The blue team caps.</param>
/// <param name="ActorPlayerId">The acting player id.</param>
/// <param name="Reason">The reason for the score change.</param>
public readonly record struct OpenGarrisonServerScoreRequest(
    long Frame,
    PlayerTeam Team,
    int Delta,
    int RedCaps,
    int BlueCaps,
    int ActorPlayerId,
    string Reason);

/// <summary>
/// A requested round end, used by decision hooks.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="GameMode">The game mode.</param>
/// <param name="WinnerTeam">The winning team, if any.</param>
/// <param name="RedCaps">The red team caps.</param>
/// <param name="BlueCaps">The blue team caps.</param>
/// <param name="Reason">The reason the round ended.</param>
public readonly record struct OpenGarrisonServerRoundEndRequest(
    long Frame,
    GameModeKind GameMode,
    PlayerTeam? WinnerTeam,
    int RedCaps,
    int BlueCaps,
    string Reason);

/// <summary>
/// The result of a decision hook: continue with or cancel the requested action.
/// </summary>
/// <param name="IsCancelled">Whether the action is cancelled.</param>
/// <param name="Reason">The reason for cancellation.</param>
public readonly record struct OpenGarrisonServerDecisionResult(
    bool IsCancelled,
    string Reason = "")
{
    /// <summary>
    /// Gets a result that lets the action continue.
    /// </summary>
    public static OpenGarrisonServerDecisionResult Continue { get; } = new(false);

    /// <summary>
    /// Creates a result that cancels the action.
    /// </summary>
    /// <param name="reason">The cancellation reason.</param>
    /// <returns>The cancellation result.</returns>
    public static OpenGarrisonServerDecisionResult Cancel(string reason = "") => new(true, reason);
}

/// <summary>
/// Hooks a server plugin can implement to allow or cancel requested game actions.
/// </summary>
public interface IOpenGarrisonServerDecisionHooks
{
    /// <summary>Decides whether a chat message may be sent.</summary>
    /// <param name="e">The chat event.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeChatMessage(ChatReceivedEvent e);

    /// <summary>Decides whether a team change may proceed.</summary>
    /// <param name="e">The team change request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeTeamChange(OpenGarrisonServerTeamChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a class change may proceed.</summary>
    /// <param name="e">The class change request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeClassChange(OpenGarrisonServerClassChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a loadout change may proceed.</summary>
    /// <param name="e">The loadout change request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeLoadoutChange(OpenGarrisonServerLoadoutChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a map change may proceed.</summary>
    /// <param name="e">The map change request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeMapChange(OpenGarrisonServerMapChangeRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a spawn may proceed.</summary>
    /// <param name="e">The spawn request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeSpawn(OpenGarrisonServerSpawnRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether damage may be applied.</summary>
    /// <param name="e">The damage request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeDamage(OpenGarrisonServerDamageRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a death may proceed.</summary>
    /// <param name="e">The death request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeDeath(OpenGarrisonServerDeathRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a pickup may proceed.</summary>
    /// <param name="e">The pickup request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforePickup(OpenGarrisonServerPickupRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a score change may proceed.</summary>
    /// <param name="e">The score request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeScore(OpenGarrisonServerScoreRequest e) => OpenGarrisonServerDecisionResult.Continue;

    /// <summary>Decides whether a round end may proceed.</summary>
    /// <param name="e">The round end request.</param>
    /// <returns>The decision result.</returns>
    OpenGarrisonServerDecisionResult BeforeRoundEnd(OpenGarrisonServerRoundEndRequest e) => OpenGarrisonServerDecisionResult.Continue;
}

/// <summary>
/// Hooks a server plugin can implement to observe map changes.
/// </summary>
public interface IOpenGarrisonServerMapHooks
{
    /// <summary>Called when the map is changing.</summary>
    /// <param name="e">The map changing event.</param>
    void OnMapChanging(MapChangingEvent e);

    /// <summary>Called after the map changed.</summary>
    /// <param name="e">The map changed event.</param>
    void OnMapChanged(MapChangedEvent e);
}

/// <summary>
/// Hooks a server plugin can implement to observe score, round, and kill feed changes.
/// </summary>
public interface IOpenGarrisonServerGameplayHooks
{
    /// <summary>Called when the score changes.</summary>
    /// <param name="e">The score event.</param>
    void OnScoreChanged(ScoreChangedEvent e);

    /// <summary>Called when a round ends.</summary>
    /// <param name="e">The round ended event.</param>
    void OnRoundEnded(RoundEndedEvent e);

    /// <summary>Called when a kill feed entry is produced.</summary>
    /// <param name="e">The kill feed event.</param>
    void OnKillFeedEntry(KillFeedEvent e);
}
