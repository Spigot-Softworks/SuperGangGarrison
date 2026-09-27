using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

/// <summary>A hello was received from a connecting client.</summary>
/// <param name="PlayerName">The connecting player's name.</param>
/// <param name="EndPoint">The client's network endpoint.</param>
/// <param name="Version">The client's protocol version.</param>
public readonly record struct HelloReceivedEvent(string PlayerName, string EndPoint, int Version);

/// <summary>A client connected to the server.</summary>
/// <param name="Slot">The client's slot.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="EndPoint">The client's network endpoint.</param>
/// <param name="IsAuthorized">Whether the client is authorized.</param>
/// <param name="IsSpectator">Whether the client is spectating.</param>
public readonly record struct ClientConnectedEvent(
    byte Slot,
    string PlayerName,
    string EndPoint,
    bool IsAuthorized,
    bool IsSpectator);

/// <summary>A client disconnected from the server.</summary>
/// <param name="Slot">The client's slot.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="EndPoint">The client's network endpoint.</param>
/// <param name="Reason">The disconnect reason.</param>
/// <param name="WasAuthorized">Whether the client was authorized.</param>
public readonly record struct ClientDisconnectedEvent(
    byte Slot,
    string PlayerName,
    string EndPoint,
    string Reason,
    bool WasAuthorized);

/// <summary>A client-provided password was accepted.</summary>
/// <param name="Slot">The client's slot.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="EndPoint">The client's network endpoint.</param>
public readonly record struct PasswordAcceptedEvent(byte Slot, string PlayerName, string EndPoint);

/// <summary>A player's team changed.</summary>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="Team">The new team.</param>
public readonly record struct PlayerTeamChangedEvent(byte Slot, string PlayerName, PlayerTeam Team);

/// <summary>A player's class changed.</summary>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="PlayerClass">The new class.</param>
public readonly record struct PlayerClassChangedEvent(byte Slot, string PlayerName, PlayerClass PlayerClass);

/// <summary>A chat message was received.</summary>
/// <param name="Slot">The sender's slot.</param>
/// <param name="PlayerName">The sender's name.</param>
/// <param name="Text">The message text.</param>
/// <param name="Team">The sender's team, if known.</param>
/// <param name="TeamOnly">Whether the message is team-only.</param>
public readonly record struct ChatReceivedEvent(byte Slot, string PlayerName, string Text, PlayerTeam? Team, bool TeamOnly = false);

/// <summary>The map is changing.</summary>
/// <param name="CurrentLevelName">The current level name.</param>
/// <param name="CurrentAreaIndex">The current area index.</param>
/// <param name="CurrentAreaCount">The current area count.</param>
/// <param name="NextLevelName">The next level name.</param>
/// <param name="NextAreaIndex">The next area index.</param>
/// <param name="PreservePlayerStats">Whether player stats are preserved.</param>
/// <param name="WinnerTeam">The winning team, if the change follows a round end.</param>
public readonly record struct MapChangingEvent(
    string CurrentLevelName,
    int CurrentAreaIndex,
    int CurrentAreaCount,
    string NextLevelName,
    int NextAreaIndex,
    bool PreservePlayerStats,
    PlayerTeam? WinnerTeam);

/// <summary>The map changed.</summary>
/// <param name="LevelName">The new level name.</param>
/// <param name="AreaIndex">The new area index.</param>
/// <param name="AreaCount">The new area count.</param>
/// <param name="Mode">The game mode.</param>
public readonly record struct MapChangedEvent(
    string LevelName,
    int AreaIndex,
    int AreaCount,
    GameModeKind Mode);

/// <summary>The score changed.</summary>
/// <param name="RedCaps">The red team caps.</param>
/// <param name="BlueCaps">The blue team caps.</param>
/// <param name="Mode">The game mode.</param>
public readonly record struct ScoreChangedEvent(
    int RedCaps,
    int BlueCaps,
    GameModeKind Mode);

/// <summary>A round ended.</summary>
/// <param name="Mode">The game mode.</param>
/// <param name="WinnerTeam">The winning team, if any.</param>
/// <param name="RedCaps">The red team caps.</param>
/// <param name="BlueCaps">The blue team caps.</param>
/// <param name="Frame">The frame the round ended on.</param>
public readonly record struct RoundEndedEvent(
    GameModeKind Mode,
    PlayerTeam? WinnerTeam,
    int RedCaps,
    int BlueCaps,
    long Frame);

/// <summary>A kill feed entry was produced.</summary>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer's team.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim's team.</param>
/// <param name="MessageText">The kill message text.</param>
public readonly record struct KillFeedEvent(
    string KillerName,
    PlayerTeam KillerTeam,
    string WeaponSpriteName,
    string VictimName,
    PlayerTeam VictimTeam,
    string MessageText);
