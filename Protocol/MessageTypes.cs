using System;
using System.Collections.Generic;

namespace OpenGarrison.Protocol;

/// <summary>The protocol message type.</summary>
public enum MessageType : byte
{
    /// <summary>The hello message.</summary>
    Hello = 1,
    /// <summary>The welcome message.</summary>
    Welcome = 2,
    /// <summary>The input state message.</summary>
    InputState = 3,
    /// <summary>The snapshot message.</summary>
    Snapshot = 4,
    /// <summary>The control command message.</summary>
    ControlCommand = 5,
    /// <summary>The control ack message.</summary>
    ControlAck = 6,
    /// <summary>The connection denied message.</summary>
    ConnectionDenied = 7,
    /// <summary>The session slot changed message.</summary>
    SessionSlotChanged = 8,
    /// <summary>The server status request message.</summary>
    ServerStatusRequest = 9,
    /// <summary>The server status response message.</summary>
    ServerStatusResponse = 10,
    /// <summary>The password request message.</summary>
    PasswordRequest = 11,
    /// <summary>The password submit message.</summary>
    PasswordSubmit = 12,
    /// <summary>The password result message.</summary>
    PasswordResult = 13,
    /// <summary>The auto balance notice message.</summary>
    AutoBalanceNotice = 14,
    /// <summary>The chat submit message.</summary>
    ChatSubmit = 15,
    /// <summary>The chat relay message.</summary>
    ChatRelay = 16,
    /// <summary>The snapshot ack message.</summary>
    SnapshotAck = 17,
    /// <summary>The player profile update message.</summary>
    PlayerProfileUpdate = 18,
    /// <summary>The client plugin message message.</summary>
    ClientPluginMessage = 19,
    /// <summary>The server plugin message message.</summary>
    ServerPluginMessage = 20,
    /// <summary>The player social profile update message.</summary>
    PlayerSocialProfileUpdate = 21,
    /// <summary>The server details request message.</summary>
    ServerDetailsRequest = 22,
    /// <summary>The server details response message.</summary>
    ServerDetailsResponse = 23,
    /// <summary>The custom bubble upload message.</summary>
    CustomBubbleUpload = 24,
    /// <summary>The custom bubble state message.</summary>
    CustomBubbleState = 25,
    /// <summary>The custom bubble clear message.</summary>
    CustomBubbleClear = 26,
    /// <summary>The ping request message.</summary>
    PingRequest = 27,
    /// <summary>The ping response message.</summary>
    PingResponse = 28,
    /// <summary>The last to die command message.</summary>
    LastToDieCommand = 29,
    /// <summary>The last to die command result message.</summary>
    LastToDieCommandResult = 30,
    /// <summary>The last to die run snapshot message.</summary>
    LastToDieRunSnapshot = 31,
    /// <summary>The last to die run snapshot ack message.</summary>
    LastToDieRunSnapshotAck = 32,
    /// <summary>The gameplay account attach request message.</summary>
    GameplayAccountAttachRequest = 48,
    /// <summary>The gameplay account attach result message.</summary>
    GameplayAccountAttachResult = 49,
    /// <summary>The player points state message.</summary>
    PlayerPointsState = 50,
    /// <summary>The vote command message.</summary>
    VoteCommand = 51,
    /// <summary>The vote state message.</summary>
    VoteState = 52,
    /// <summary>The vote menu message.</summary>
    VoteMenu = 53,
    /// <summary>The voice submit message.</summary>
    VoiceSubmit = 54,
    /// <summary>The audio relay message.</summary>
    AudioRelay = 55,
    /// <summary>The server audio state message.</summary>
    ServerAudioState = 56,
    /// <summary>The voice channel membership message.</summary>
    VoiceChannelMembership = 57,
}

/// <summary>The connection intent.</summary>
public enum ConnectionIntent : byte
{
    /// <summary>The join intent.</summary>
    Join = 0,
    /// <summary>The watch intent.</summary>
    Watch = 1,
}

/// <summary>The plugin message payload format.</summary>
public enum PluginMessagePayloadFormat : byte
{
    /// <summary>The text payload format.</summary>
    Text = 0,
    /// <summary>The json payload format.</summary>
    Json = 1,
}

/// <summary>The kind of a control command.</summary>
public enum ControlCommandKind : byte
{
    /// <summary>The select team command.</summary>
    SelectTeam = 1,
    /// <summary>The select class command.</summary>
    SelectClass = 2,
    /// <summary>The spectate command.</summary>
    Spectate = 3,
    /// <summary>The select gameplay loadout command.</summary>
    SelectGameplayLoadout = 4,
}

[Flags]
/// <summary>The input buttons.</summary>
public enum InputButtons : uint
{
    /// <summary>No input.</summary>
    None = 0,
    /// <summary>The left input.</summary>
    Left = 1 << 0,
    /// <summary>The right input.</summary>
    Right = 1 << 1,
    /// <summary>The up input.</summary>
    Up = 1 << 2,
    /// <summary>The down input.</summary>
    Down = 1 << 3,
    /// <summary>The build sentry input.</summary>
    BuildSentry = 1 << 4,
    /// <summary>The taunt input.</summary>
    Taunt = 1 << 5,
    /// <summary>The fire primary input.</summary>
    FirePrimary = 1 << 6,
    /// <summary>The fire secondary input.</summary>
    FireSecondary = 1 << 7,
    /// <summary>The debug kill input.</summary>
    DebugKill = 1 << 8,
    /// <summary>The destroy sentry input.</summary>
    DestroySentry = 1 << 9,
    /// <summary>The drop intel input.</summary>
    DropIntel = 1 << 10,
    /// <summary>The use ability input.</summary>
    UseAbility = 1 << 11,
    /// <summary>The interact weapon input.</summary>
    InteractWeapon = 1 << 12,
    /// <summary>The swap weapon input.</summary>
    SwapWeapon = 1 << 13,
    /// <summary>The ready up input.</summary>
    ReadyUp = 1 << 14,
    /// <summary>The is typing chat message input.</summary>
    IsTypingChatMessage = 1 << 15,
    /// <summary>The build dispenser input.</summary>
    BuildDispenser = 1 << 16,
    /// <summary>The destroy dispenser input.</summary>
    DestroyDispenser = 1 << 17,
    /// <summary>The toggle secondary weapon input.</summary>
    ToggleSecondaryWeapon = 1 << 18,
    /// <summary>The build jump pad input.</summary>
    BuildJumpPad = 1 << 19,
    /// <summary>The destroy jump pad input.</summary>
    DestroyJumpPad = 1 << 20,
}

/// <summary>A protocol message.</summary>
public interface IProtocolMessage
{
    MessageType Type { get; }
}

/// <summary>The kind of a vote command.</summary>
public enum VoteCommandKind : byte
{
    /// <summary>The open menu command.</summary>
    OpenMenu = 1,
    /// <summary>The start map now command.</summary>
    StartMapNow = 2,
    /// <summary>The start map next round command.</summary>
    StartMapNextRound = 3,
    /// <summary>The start vip command.</summary>
    StartVip = 4,
    /// <summary>The cast yes command.</summary>
    CastYes = 5,
    /// <summary>The cast no command.</summary>
    CastNo = 6,
    /// <summary>The cancel command.</summary>
    Cancel = 7,
    /// <summary>The request status command.</summary>
    RequestStatus = 8,
    /// <summary>The start kick command.</summary>
    StartKick = 9,
    /// <summary>The start mute command.</summary>
    StartMute = 10,
    /// <summary>The start scramble command.</summary>
    StartScramble = 11,
    /// <summary>The start custom command.</summary>
    StartCustom = 12,
}

/// <summary>The kind of a server vote.</summary>
public enum ServerVoteKind : byte
{
    /// <summary>No vote kind.</summary>
    None = 0,
    /// <summary>The change map now vote kind.</summary>
    ChangeMapNow = 1,
    /// <summary>The change map next round vote kind.</summary>
    ChangeMapNextRound = 2,
    /// <summary>The select vip vote kind.</summary>
    SelectVip = 3,
    /// <summary>The kick player vote kind.</summary>
    KickPlayer = 4,
    /// <summary>The mute player vote kind.</summary>
    MutePlayer = 5,
    /// <summary>The scramble teams vote kind.</summary>
    ScrambleTeams = 6,
    /// <summary>The plugin defined vote kind.</summary>
    PluginDefined = 7,
}

/// <summary>The kind of a server vote event.</summary>
public enum ServerVoteEventKind : byte
{
    /// <summary>The snapshot event.</summary>
    Snapshot = 0,
    /// <summary>The started event.</summary>
    Started = 1,
    /// <summary>The yes event.</summary>
    Yes = 2,
    /// <summary>The no event.</summary>
    No = 3,
    /// <summary>The passed event.</summary>
    Passed = 4,
    /// <summary>The failed event.</summary>
    Failed = 5,
    /// <summary>The expired event.</summary>
    Expired = 6,
    /// <summary>The canceled event.</summary>
    Canceled = 7,
    /// <summary>The action failed event.</summary>
    ActionFailed = 8,
}

/// <summary>The kind of a vote menu target.</summary>
public enum VoteMenuTargetKind : byte
{
    /// <summary>No target kind.</summary>
    None = 0,
    /// <summary>The player target kind.</summary>
    Player = 1,
    /// <summary>The map target kind.</summary>
    Map = 2,
}

/// <summary>The gameplay account attach request message.</summary>
/// <param name="RequestId">The request id.</param>
/// <param name="GameplayToken">The gameplay token.</param>
public sealed record GameplayAccountAttachRequestMessage(ulong RequestId, string GameplayToken) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.GameplayAccountAttachRequest;
}

/// <summary>The gameplay account attach result message.</summary>
/// <param name="RequestId">The request id.</param>
/// <param name="Attached">Whether the account was attached.</param>
/// <param name="Reason">The reason.</param>
/// <param name="FriendCode">The friend code.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="LifetimePoints">The lifetime points.</param>
/// <param name="WalletBalance">The wallet balance.</param>
/// <param name="ProfileRevision">The profile revision.</param>
public sealed record GameplayAccountAttachResultMessage(
    ulong RequestId,
    bool Attached,
    string Reason,
    string FriendCode,
    string DisplayName,
    long LifetimePoints,
    long WalletBalance,
    long ProfileRevision) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.GameplayAccountAttachResult;
}

/// <summary>The player points state message.</summary>
/// <param name="LifetimePoints">The lifetime points.</param>
/// <param name="WalletBalance">The wallet balance.</param>
/// <param name="GlobalRank">The global rank.</param>
/// <param name="ProfileRevision">The profile revision.</param>
public sealed record PlayerPointsStateMessage(
    long LifetimePoints,
    long WalletBalance,
    int GlobalRank,
    long ProfileRevision) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PlayerPointsState;
}

/// <summary>The vote command message.</summary>
/// <param name="Command">The command.</param>
/// <param name="Target">The target.</param>
/// <param name="AreaIndex">The area index.</param>
/// <param name="TargetSlot">The target slot.</param>
/// <param name="Team">The team.</param>
/// <param name="VoteId">The vote id.</param>
/// <param name="Argument">The argument.</param>
public sealed record VoteCommandMessage(
    VoteCommandKind Command,
    string Target = "",
    int AreaIndex = 1,
    byte TargetSlot = 0,
    byte Team = 0,
    ulong VoteId = 0,
    string Argument = "") : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.VoteCommand;
}

/// <summary>The vote state message.</summary>
/// <param name="VoteId">The vote id.</param>
/// <param name="Revision">The revision.</param>
/// <param name="Event">The event.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Subject">The subject.</param>
/// <param name="InitiatorName">The initiator name.</param>
/// <param name="ActorName">The actor name.</param>
/// <param name="YesVotes">The yes votes.</param>
/// <param name="NoVotes">The no votes.</param>
/// <param name="RequiredYesVotes">The required yes votes.</param>
/// <param name="EligibleVoters">The eligible voters.</param>
/// <param name="RemainingTicks">The remaining ticks.</param>
/// <param name="Message">The message.</param>
public sealed record VoteStateMessage(
    ulong VoteId,
    uint Revision,
    ServerVoteEventKind Event,
    ServerVoteKind Kind,
    string Subject,
    string InitiatorName,
    string ActorName,
    int YesVotes,
    int NoVotes,
    int RequiredYesVotes,
    int EligibleVoters,
    int RemainingTicks,
    string Message) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.VoteState;
}

/// <summary>The vote menu map entry.</summary>
/// <param name="LevelName">The level name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="AreaCount">The area count.</param>
public sealed record VoteMenuMapEntry(string LevelName, string DisplayName, int AreaCount);

/// <summary>The vote menu player entry.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Team">The team.</param>
/// <param name="IsMuted">Whether the player is muted.</param>
public sealed record VoteMenuPlayerEntry(byte Slot, string DisplayName, byte Team, bool IsMuted = false);

/// <summary>The vote menu custom entry.</summary>
/// <param name="Id">The id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Description">The description.</param>
/// <param name="TargetKind">The target kind.</param>
public sealed record VoteMenuCustomEntry(
    string Id,
    string DisplayName,
    string Description,
    byte TargetKind);

/// <summary>The vote menu message.</summary>
/// <param name="Maps">The maps.</param>
/// <param name="Players">The players.</param>
/// <param name="VipVoteAvailable">Whether the VIP vote option is available.</param>
/// <param name="VoteActive">Whether a vote is currently active.</param>
/// <param name="CooldownTicksRemaining">The cooldown ticks remaining.</param>
/// <param name="ActiveVoteId">The active vote id.</param>
/// <param name="KickVoteAvailable">Whether the kick vote option is available.</param>
/// <param name="MuteVoteAvailable">Whether the mute vote option is available.</param>
/// <param name="ScrambleVoteAvailable">Whether the scramble vote option is available.</param>
/// <param name="RegisteredVotes">The registered votes.</param>
public sealed record VoteMenuMessage(
    IReadOnlyList<VoteMenuMapEntry> Maps,
    IReadOnlyList<VoteMenuPlayerEntry> Players,
    bool VipVoteAvailable,
    bool VoteActive,
    int CooldownTicksRemaining,
    ulong ActiveVoteId,
    bool KickVoteAvailable = false,
    bool MuteVoteAvailable = false,
    bool ScrambleVoteAvailable = false,
    IReadOnlyList<VoteMenuCustomEntry>? RegisteredVotes = null) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.VoteMenu;

    /// <summary>Gets the custom votes.</summary>
    public IReadOnlyList<VoteMenuCustomEntry> CustomVotes => RegisteredVotes ?? Array.Empty<VoteMenuCustomEntry>();
}

/// <summary>The hello message.</summary>
/// <param name="Name">The name.</param>
/// <param name="Version">The version.</param>
/// <param name="BadgeMask">The badge mask.</param>
/// <param name="FriendCode">The friend code.</param>
/// <param name="PlayerCardJson">The player card json.</param>
/// <param name="Intent">The intent.</param>
/// <param name="ClientInstanceId">The client instance id.</param>
public sealed record HelloMessage(
    string Name,
    int Version,
    ulong BadgeMask,
    string FriendCode = "",
    string PlayerCardJson = "",
    ConnectionIntent Intent = ConnectionIntent.Join,
    Guid ClientInstanceId = default) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.Hello;
}

/// <summary>The welcome message.</summary>
/// <param name="ServerName">The server name.</param>
/// <param name="Version">The version.</param>
/// <param name="TickRate">The tick rate.</param>
/// <param name="LevelName">The level name.</param>
/// <param name="PlayerSlot">The player slot.</param>
/// <param name="MaxPlayerCount">The max player count.</param>
/// <param name="IsCustomMap">Whether the map is a custom map.</param>
/// <param name="MapDownloadUrl">The map download url.</param>
/// <param name="MapContentHash">The map content hash.</param>
/// <param name="MapScale">The map scale.</param>
/// <param name="LocalPredictionEnabled">Whether local prediction is enabled.</param>
public sealed record WelcomeMessage(
    string ServerName,
    int Version,
    int TickRate,
    string LevelName,
    byte PlayerSlot,
    int MaxPlayerCount,
    bool IsCustomMap = false,
    string MapDownloadUrl = "",
    string MapContentHash = "",
    float MapScale = 1f,
    bool LocalPredictionEnabled = false) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.Welcome;
}

/// <summary>The connection denied message.</summary>
/// <param name="Reason">The reason.</param>
public sealed record ConnectionDeniedMessage(string Reason) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ConnectionDenied;
}

public sealed record PasswordRequestMessage : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PasswordRequest;
}

/// <summary>The password submit message.</summary>
/// <param name="Password">The password.</param>
public sealed record PasswordSubmitMessage(string Password) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PasswordSubmit;
}

/// <summary>The password result message.</summary>
/// <param name="Accepted">Whether the request was accepted.</param>
/// <param name="Reason">The reason.</param>
public sealed record PasswordResultMessage(bool Accepted, string Reason) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PasswordResult;
}

/// <summary>The chat submit message.</summary>
/// <param name="Text">The text.</param>
/// <param name="TeamOnly">Whether the message is team-only.</param>
public sealed record ChatSubmitMessage(string Text, bool TeamOnly = false) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ChatSubmit;
}

/// <summary>The chat relay message.</summary>
/// <param name="Team">The team.</param>
/// <param name="PlayerName">The player name.</param>
/// <param name="Text">The text.</param>
/// <param name="TeamOnly">Whether the message is team-only.</param>
/// <param name="PlayerSlot">The player slot.</param>
public sealed record ChatRelayMessage(
    byte Team,
    string PlayerName,
    string Text,
    bool TeamOnly = false,
    byte PlayerSlot = 0) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ChatRelay;
}

/// <summary>The kind of an auto-balance notice.</summary>
public enum AutoBalanceNoticeKind : byte
{
    /// <summary>The pending notice kind.</summary>
    Pending = 1,
    /// <summary>The applied notice kind.</summary>
    Applied = 2,
}

/// <summary>The auto balance notice message.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="PlayerName">The player name.</param>
/// <param name="FromTeam">The from team.</param>
/// <param name="ToTeam">The to team.</param>
/// <param name="DelaySeconds">The delay seconds.</param>
public sealed record AutoBalanceNoticeMessage(
    AutoBalanceNoticeKind Kind,
    string PlayerName,
    byte FromTeam,
    byte ToTeam,
    int DelaySeconds) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.AutoBalanceNotice;
}

/// <summary>The session slot changed message.</summary>
/// <param name="PlayerSlot">The player slot.</param>
public sealed record SessionSlotChangedMessage(byte PlayerSlot) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.SessionSlotChanged;
}

public sealed record ServerStatusRequestMessage : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ServerStatusRequest;
}

/// <summary>The server status response message.</summary>
/// <param name="ServerName">The server name.</param>
/// <param name="LevelName">The level name.</param>
/// <param name="GameMode">The game mode.</param>
/// <param name="PlayerCount">The player count.</param>
/// <param name="MaxPlayerCount">The max player count.</param>
/// <param name="SpectatorCount">The spectator count.</param>
public sealed record ServerStatusResponseMessage(
    string ServerName,
    string LevelName,
    byte GameMode,
    int PlayerCount,
    int MaxPlayerCount,
    int SpectatorCount) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ServerStatusResponse;
}

public sealed record ServerDetailsRequestMessage : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ServerDetailsRequest;
}

/// <summary>The server details roster entry.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="Name">The name.</param>
/// <param name="Team">The team.</param>
/// <param name="ClassId">The class id.</param>
/// <param name="IsSpectator">Whether the player is a spectator.</param>
/// <param name="IsAlive">Whether the player is alive.</param>
/// <param name="IsAwaitingJoin">Whether the player is awaiting join.</param>
/// <param name="Health">The health.</param>
/// <param name="MaxHealth">The max health.</param>
/// <param name="Kills">The kills.</param>
/// <param name="Deaths">The deaths.</param>
/// <param name="Assists">The assists.</param>
/// <param name="Caps">The caps.</param>
/// <param name="Points">The points.</param>
public sealed record ServerDetailsRosterEntry(
    byte Slot,
    string Name,
    byte Team,
    byte ClassId,
    bool IsSpectator,
    bool IsAlive,
    bool IsAwaitingJoin,
    short Health,
    short MaxHealth,
    short Kills,
    short Deaths,
    short Assists,
    short Caps,
    float Points);

/// <summary>The server details response message.</summary>
/// <param name="ServerName">The server name.</param>
/// <param name="LevelName">The level name.</param>
/// <param name="GameMode">The game mode.</param>
/// <param name="PlayerCount">The player count.</param>
/// <param name="MaxPlayerCount">The max player count.</param>
/// <param name="SpectatorCount">The spectator count.</param>
/// <param name="RedScore">The red score.</param>
/// <param name="BlueScore">The blue score.</param>
/// <param name="TimeRemainingTicks">The time remaining ticks.</param>
/// <param name="TimeLimitTicks">The time limit ticks.</param>
/// <param name="TickRate">The tick rate.</param>
/// <param name="Roster">The roster.</param>
public sealed record ServerDetailsResponseMessage(
    string ServerName,
    string LevelName,
    byte GameMode,
    int PlayerCount,
    int MaxPlayerCount,
    int SpectatorCount,
    int RedScore,
    int BlueScore,
    int TimeRemainingTicks,
    int TimeLimitTicks,
    int TickRate,
    IReadOnlyList<ServerDetailsRosterEntry> Roster) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ServerDetailsResponse;
}

/// <summary>The input state message.</summary>
/// <param name="Sequence">The sequence.</param>
/// <param name="Buttons">The buttons.</param>
/// <param name="AimRelX">The relative aim X.</param>
/// <param name="AimRelY">The relative aim Y.</param>
/// <param name="ChatBubbleFrameIndex">The chat bubble frame index.</param>
/// <param name="IsUsingBinoculars">Whether the player is using binoculars.</param>
/// <param name="BinocularsFocusX">The binoculars focus X coordinate.</param>
/// <param name="BinocularsFocusY">The binoculars focus Y coordinate.</param>
/// <param name="PingMilliseconds">The ping milliseconds.</param>
public sealed record InputStateMessage(
    uint Sequence,
    InputButtons Buttons,
    float AimRelX,
    float AimRelY,
    int ChatBubbleFrameIndex,
    bool IsUsingBinoculars = false,
    float BinocularsFocusX = 0f,
    float BinocularsFocusY = 0f,
    int PingMilliseconds = -1) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.InputState;
}

/// <summary>The control command message.</summary>
/// <param name="Sequence">The sequence.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Value">The value.</param>
/// <param name="TextValue">The text value.</param>
public sealed record ControlCommandMessage(
    uint Sequence,
    ControlCommandKind Kind,
    byte Value = 0,
    string TextValue = "") : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ControlCommand;
}

/// <summary>The control ack message.</summary>
/// <param name="Sequence">The sequence.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Accepted">Whether the request was accepted.</param>
public sealed record ControlAckMessage(
    uint Sequence,
    ControlCommandKind Kind,
    bool Accepted) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ControlAck;
}

/// <summary>The snapshot ack message.</summary>
/// <param name="Frame">The frame.</param>
public sealed record SnapshotAckMessage(ulong Frame) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.SnapshotAck;
}

/// <summary>The ping request message.</summary>
/// <param name="Sequence">The sequence.</param>
public sealed record PingRequestMessage(uint Sequence) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PingRequest;
}

/// <summary>The ping response message.</summary>
/// <param name="Sequence">The sequence.</param>
public sealed record PingResponseMessage(uint Sequence) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PingResponse;
}

/// <summary>The player profile update message.</summary>
/// <param name="Name">The name.</param>
/// <param name="BadgeMask">The badge mask.</param>
/// <param name="FriendCode">The friend code.</param>
/// <param name="PlayerCardJson">The player card json.</param>
public sealed record PlayerProfileUpdateMessage(
    string Name,
    ulong BadgeMask,
    string FriendCode = "",
    string PlayerCardJson = "") : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PlayerProfileUpdate;
}

/// <summary>The player social profile state.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="FriendCode">The friend code.</param>
/// <param name="PlayerCardJson">The player card json.</param>
public sealed record PlayerSocialProfileState(
    byte Slot,
    string DisplayName,
    string FriendCode,
    string PlayerCardJson);

/// <summary>The player server title state.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="Text">The text.</param>
/// <param name="ColorRgb">The color RGB value.</param>
/// <param name="Rainbow">Whether the title uses rainbow coloring.</param>
public sealed record PlayerServerTitleState(
    byte Slot,
    string Text,
    uint ColorRgb,
    bool Rainbow);

/// <summary>The player social profile update message.</summary>
/// <param name="Profiles">The profiles.</param>
/// <param name="RemovedSlots">The removed slots.</param>
/// <param name="Titles">The titles.</param>
public sealed record PlayerSocialProfileUpdateMessage(
    IReadOnlyList<PlayerSocialProfileState> Profiles,
    IReadOnlyList<byte> RemovedSlots,
    IReadOnlyList<PlayerServerTitleState>? Titles = null) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.PlayerSocialProfileUpdate;
}

/// <summary>The custom bubble upload message.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="Revision">The revision.</param>
/// <param name="Rgba64Pixels">The RGBA64 pixels.</param>
public sealed record CustomBubbleUploadMessage(
    byte Slot,
    uint Revision,
    byte[] Rgba64Pixels) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.CustomBubbleUpload;
}

/// <summary>The custom bubble state message.</summary>
/// <param name="PlayerSlot">The player slot.</param>
/// <param name="Slot">The slot.</param>
/// <param name="Revision">The revision.</param>
/// <param name="Rgba64Pixels">The RGBA64 pixels.</param>
public sealed record CustomBubbleStateMessage(
    byte PlayerSlot,
    byte Slot,
    uint Revision,
    byte[] Rgba64Pixels) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.CustomBubbleState;
}

/// <summary>The custom bubble clear message.</summary>
/// <param name="PlayerSlot">The player slot.</param>
public sealed record CustomBubbleClearMessage(byte PlayerSlot) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.CustomBubbleClear;
}

/// <summary>The client plugin message.</summary>
/// <param name="SourcePluginId">The source plugin id.</param>
/// <param name="TargetPluginId">The target plugin id.</param>
/// <param name="MessageTypeName">The message type name.</param>
/// <param name="Payload">The payload.</param>
/// <param name="PayloadFormat">The payload format.</param>
/// <param name="SchemaVersion">The schema version.</param>
public sealed record ClientPluginMessage(
    string SourcePluginId,
    string TargetPluginId,
    string MessageTypeName,
    string Payload,
    PluginMessagePayloadFormat PayloadFormat = PluginMessagePayloadFormat.Text,
    ushort SchemaVersion = 1) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ClientPluginMessage;
}

/// <summary>The server plugin message.</summary>
/// <param name="SourcePluginId">The source plugin id.</param>
/// <param name="TargetPluginId">The target plugin id.</param>
/// <param name="MessageTypeName">The message type name.</param>
/// <param name="Payload">The payload.</param>
/// <param name="PayloadFormat">The payload format.</param>
/// <param name="SchemaVersion">The schema version.</param>
public sealed record ServerPluginMessage(
    string SourcePluginId,
    string TargetPluginId,
    string MessageTypeName,
    string Payload,
    PluginMessagePayloadFormat PayloadFormat = PluginMessagePayloadFormat.Text,
    ushort SchemaVersion = 1) : IProtocolMessage
{
    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.ServerPluginMessage;
}

/// <summary>The snapshot player state.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="PlayerId">The player id.</param>
/// <param name="Name">The name.</param>
/// <param name="Team">The team.</param>
/// <param name="ClassId">The class id.</param>
/// <param name="IsAlive">Whether the player is alive.</param>
/// <param name="IsAwaitingJoin">Whether the player is awaiting join.</param>
/// <param name="IsSpectator">Whether the player is a spectator.</param>
/// <param name="RespawnTicks">The respawn ticks.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="HorizontalSpeed">The horizontal speed.</param>
/// <param name="VerticalSpeed">The vertical speed.</param>
/// <param name="Health">The health.</param>
/// <param name="MaxHealth">The max health.</param>
/// <param name="Ammo">The ammo.</param>
/// <param name="MaxAmmo">The max ammo.</param>
/// <param name="Kills">The kills.</param>
/// <param name="Deaths">The deaths.</param>
/// <param name="Caps">The caps.</param>
/// <param name="Points">The points.</param>
/// <param name="HealPoints">The heal points.</param>
/// <param name="ActiveDominationCount">The active domination count.</param>
/// <param name="IsDominatingLocalViewer">Whether dominating local viewer.</param>
/// <param name="IsDominatedByLocalViewer">Whether dominated by local viewer.</param>
/// <param name="Metal">The metal.</param>
/// <param name="IsGrounded">Whether grounded.</param>
/// <param name="RemainingAirJumps">The remaining air jumps.</param>
/// <param name="IsCarryingIntel">Whether carrying intel.</param>
/// <param name="IntelRechargeTicks">The intel recharge ticks.</param>
/// <param name="IsSpyCloaked">Whether spy cloaked.</param>
/// <param name="SpyCloakAlpha">The spy cloak alpha.</param>
/// <param name="IsSpySuperjumping">Whether spy superjumping.</param>
/// <param name="SpySuperjumpHorizontalVelocity">The spy superjump horizontal velocity.</param>
/// <param name="SpySuperjumpCooldownTicksRemaining">The spy superjump cooldown ticks remaining.</param>
/// <param name="SpyBackstabVisualTicksRemaining">The spy backstab visual ticks remaining.</param>
/// <param name="IsUbered">Whether ubered.</param>
/// <param name="IsKritzCritBoosted">Whether kritz crit boosted.</param>
/// <param name="IsHeavyEating">Whether heavy eating.</param>
/// <param name="HeavyEatTicksRemaining">The heavy eat ticks remaining.</param>
/// <param name="IsSniperScoped">Whether sniper scoped.</param>
/// <param name="IsUsingBinoculars">Whether the player is using binoculars.</param>
/// <param name="BinocularsFocusX">The binoculars focus X coordinate.</param>
/// <param name="BinocularsFocusY">The binoculars focus Y coordinate.</param>
/// <param name="FacingDirectionX">The facing direction X component.</param>
/// <param name="AimDirectionDegrees">The aim direction in degrees.</param>
/// <param name="IsTaunting">Whether taunting.</param>
/// <param name="IsChatBubbleVisible">Whether chat bubble visible.</param>
/// <param name="ChatBubbleFrameIndex">The chat bubble frame index.</param>
/// <param name="ChatBubbleAlpha">The chat bubble alpha.</param>
/// <param name="IsTypingChatMessage">Whether typing chat message.</param>
/// <param name="BurnIntensity">The burn intensity.</param>
/// <param name="BurnDurationSourceTicks">The burn duration source ticks.</param>
/// <param name="BurnDecayDelaySourceTicksRemaining">The burn decay delay source ticks remaining.</param>
/// <param name="BurnIntensityDecayPerSourceTick">The burn intensity decay per source tick.</param>
/// <param name="BurnedByPlayerId">The burned by player id.</param>
/// <param name="MovementState">The movement state.</param>
/// <param name="PrimaryCooldownTicks">The primary cooldown ticks.</param>
/// <param name="ReloadTicksUntilNextShell">The reload ticks until next shell.</param>
/// <param name="MedicNeedleCooldownTicks">The medic needle cooldown ticks.</param>
/// <param name="MedicNeedleRefillTicks">The medic needle refill ticks.</param>
/// <param name="PyroAirblastCooldownTicks">The pyro airblast cooldown ticks.</param>
/// <param name="PyroFlareCooldownTicks">The pyro flare cooldown ticks.</param>
/// <param name="PyroPrimaryFuelScaled">The pyro primary fuel scaled.</param>
/// <param name="IsPyroPrimaryRefilling">Whether pyro primary refilling.</param>
/// <param name="PyroFlameLoopTicksRemaining">The pyro flame loop ticks remaining.</param>
/// <param name="PyroPrimaryRequiresReleaseAfterEmpty">Whether pyro primary requires release after empty.</param>
/// <param name="HeavyEatCooldownTicksRemaining">The heavy eat cooldown ticks remaining.</param>
/// <param name="Assists">The assists.</param>
/// <param name="BadgeMask">The badge mask.</param>
/// <param name="IsMedicHealing">Whether medic healing.</param>
/// <param name="MedicHealTargetId">The medic heal target id.</param>
/// <param name="MedicUberCharge">The medic uber charge.</param>
/// <param name="IsMedicUberReady">Whether medic uber ready.</param>
/// <param name="GameplayModPackId">The gameplay mod pack id.</param>
/// <param name="GameplayLoadoutId">The gameplay loadout id.</param>
/// <param name="GameplayPrimaryItemId">The gameplay primary item id.</param>
/// <param name="GameplaySecondaryItemId">The gameplay secondary item id.</param>
/// <param name="GameplayUtilityItemId">The gameplay utility item id.</param>
/// <param name="GameplayEquippedSlot">The gameplay equipped slot.</param>
/// <param name="GameplayEquippedItemId">The gameplay equipped item id.</param>
/// <param name="GameplayAcquiredItemId">The gameplay acquired item id.</param>
/// <param name="GameplayModPackCacheId">The gameplay mod pack cache id.</param>
/// <param name="GameplayLoadoutCacheId">The gameplay loadout cache id.</param>
/// <param name="GameplayPrimaryItemCacheId">The gameplay primary item cache id.</param>
/// <param name="GameplaySecondaryItemCacheId">The gameplay secondary item cache id.</param>
/// <param name="GameplayUtilityItemCacheId">The gameplay utility item cache id.</param>
/// <param name="GameplayEquippedItemCacheId">The gameplay equipped item cache id.</param>
/// <param name="GameplayAcquiredItemCacheId">The gameplay acquired item cache id.</param>
/// <param name="OwnedGameplayItemIds">The owned gameplay item ids.</param>
/// <param name="ReplicatedStates">The replicated states.</param>
/// <param name="PlayerScale">The player scale.</param>
/// <param name="AimWorldX">The world aim X.</param>
/// <param name="AimWorldY">The world aim Y.</param>
/// <param name="OffhandCooldownTicks">The offhand cooldown ticks.</param>
/// <param name="OffhandReloadTicks">The offhand reload ticks.</param>
/// <param name="GibDeaths">The gib deaths.</param>
/// <param name="IsReady">Whether ready.</param>
/// <param name="GameplayClassId">The gameplay class id.</param>
/// <param name="GameplayClassCacheId">The gameplay class cache id.</param>
/// <param name="PingMilliseconds">The ping milliseconds.</param>
/// <param name="LastToDieSpyCloakMeterUnits">The last to die spy cloak meter units.</param>
/// <param name="LastToDieSpyRogueRampStacks">The last to die spy rogue ramp stacks.</param>
/// <param name="LastToDieSpyRogueRampTicks">The last to die spy rogue ramp ticks.</param>
/// <param name="SpySuperjumpAvailableCharges">The spy superjump available charges.</param>
/// <param name="SpySuperjumpMaximumCharges">The spy superjump maximum charges.</param>
/// <param name="SpySuperjumpChargeTicks">The spy superjump charge ticks.</param>
/// <param name="SpySuperjumpChargeDirectionDegrees">The spy superjump charge direction degrees.</param>
/// <param name="SpySuperjumpChargeStartMovementButtons">The spy superjump charge start movement buttons.</param>
/// <param name="SpySuperjumpChargeStartBlockedUntilAbilityRelease">Whether spy superjump charge start blocked until ability release.</param>
/// <param name="MedicUberDeliveryState">The medic uber delivery state.</param>
/// <param name="KritzCritBoostProviderPlayerId">The kritz crit boost provider player id.</param>
/// <param name="KritzCritBoostProviderSlot">The kritz crit boost provider slot.</param>
/// <param name="KritzCritBoostDamageMultiplier">The kritz crit boost damage multiplier.</param>
/// <param name="IsDispenserBuffed">Whether dispenser buffed.</param>
/// <param name="DispenserAttackReloadSpeedMultiplier">The dispenser attack reload speed multiplier.</param>
/// <param name="RageCharge">The rage charge.</param>
/// <param name="IsRageReady">Whether rage ready.</param>
/// <param name="RageTicksRemaining">The rage ticks remaining.</param>
/// <param name="IsBot">Whether bot.</param>
/// <param name="CurrentCombo">The current combo.</param>
/// <param name="ComboTicksRemaining">The combo ticks remaining.</param>
/// <param name="ExperimentalCryoSlowTicksRemaining">The experimental cryo slow ticks remaining.</param>
/// <param name="ExperimentalCryoFreezeTicksRemaining">The experimental cryo freeze ticks remaining.</param>
/// <param name="ExperimentalCryoExposureFraction">The experimental cryo exposure fraction.</param>
/// <param name="ExperimentalGhostVisibilityTicksRemaining">The experimental ghost visibility ticks remaining.</param>
/// <param name="ExperimentalGhostTrailAlpha">The experimental ghost trail alpha.</param>
public sealed record SnapshotPlayerState(
    byte Slot,
    int PlayerId,
    string Name,
    byte Team,
    byte ClassId,
    bool IsAlive,
    bool IsAwaitingJoin,
    bool IsSpectator,
    int RespawnTicks,
    float X,
    float Y,
    float HorizontalSpeed,
    float VerticalSpeed,
    short Health,
    short MaxHealth,
    short Ammo,
    short MaxAmmo,
    short Kills,
    short Deaths,
    short Caps,
    float Points,
    short HealPoints,
    short ActiveDominationCount,
    bool IsDominatingLocalViewer,
    bool IsDominatedByLocalViewer,
    float Metal,
    bool IsGrounded,
    int RemainingAirJumps,
    bool IsCarryingIntel,
    float IntelRechargeTicks,
    bool IsSpyCloaked,
    float SpyCloakAlpha,
    bool IsSpySuperjumping,
    float SpySuperjumpHorizontalVelocity,
    int SpySuperjumpCooldownTicksRemaining,
    int SpyBackstabVisualTicksRemaining,
    bool IsUbered,
    bool IsKritzCritBoosted,
    bool IsHeavyEating,
    int HeavyEatTicksRemaining,
    bool IsSniperScoped,
    bool IsUsingBinoculars,
    float BinocularsFocusX,
    float BinocularsFocusY,
    float FacingDirectionX,
    float AimDirectionDegrees,
    bool IsTaunting,
    bool IsChatBubbleVisible,
    int ChatBubbleFrameIndex,
    float ChatBubbleAlpha,
    bool IsTypingChatMessage = false,
    float BurnIntensity = 0f,
    float BurnDurationSourceTicks = 0f,
    float BurnDecayDelaySourceTicksRemaining = 0f,
    float BurnIntensityDecayPerSourceTick = 0f,
    int BurnedByPlayerId = -1,
    byte MovementState = 0,
    int PrimaryCooldownTicks = 0,
    int ReloadTicksUntilNextShell = 0,
    int MedicNeedleCooldownTicks = 0,
    int MedicNeedleRefillTicks = 0,
    int PyroAirblastCooldownTicks = 0,
    int PyroFlareCooldownTicks = 0,
    int PyroPrimaryFuelScaled = 0,
    bool IsPyroPrimaryRefilling = false,
    int PyroFlameLoopTicksRemaining = 0,
    bool PyroPrimaryRequiresReleaseAfterEmpty = false,
    int HeavyEatCooldownTicksRemaining = 0,
    short Assists = 0,
    ulong BadgeMask = 0,
    bool IsMedicHealing = false,
    int MedicHealTargetId = -1,
    float MedicUberCharge = 0f,
    bool IsMedicUberReady = false,
    string GameplayModPackId = "",
    string GameplayLoadoutId = "",
    string GameplayPrimaryItemId = "",
    string GameplaySecondaryItemId = "",
    string GameplayUtilityItemId = "",
    byte GameplayEquippedSlot = 0,
    string GameplayEquippedItemId = "",
    string GameplayAcquiredItemId = "",
    // Cached string IDs (0 = not cached, use string value)
    ushort GameplayModPackCacheId = 0,
    ushort GameplayLoadoutCacheId = 0,
    ushort GameplayPrimaryItemCacheId = 0,
    ushort GameplaySecondaryItemCacheId = 0,
    ushort GameplayUtilityItemCacheId = 0,
    ushort GameplayEquippedItemCacheId = 0,
    ushort GameplayAcquiredItemCacheId = 0,
    IReadOnlyList<string>? OwnedGameplayItemIds = null,
    IReadOnlyList<SnapshotReplicatedStateEntry>? ReplicatedStates = null,
    float PlayerScale = 1f,
    float AimWorldX = 0f,
    float AimWorldY = 0f,
    // Offhand weapon animation state (e.g. soldier shotgun). Delivered via movement delta so animations
    // are visible to other players without waiting for the budget-limited full-state update.
    int OffhandCooldownTicks = 0,
    int OffhandReloadTicks = 0,
    short GibDeaths = 0,
    bool IsReady = false,
    string GameplayClassId = "",
    ushort GameplayClassCacheId = 0,
    int PingMilliseconds = -1,
    ushort LastToDieSpyCloakMeterUnits = 0,
    byte LastToDieSpyRogueRampStacks = 0,
    ushort LastToDieSpyRogueRampTicks = 0,
    byte SpySuperjumpAvailableCharges = 1,
    byte SpySuperjumpMaximumCharges = 1,
    ushort SpySuperjumpChargeTicks = 0,
    float SpySuperjumpChargeDirectionDegrees = 0f,
    byte SpySuperjumpChargeStartMovementButtons = 0,
    bool SpySuperjumpChargeStartBlockedUntilAbilityRelease = false,
    byte MedicUberDeliveryState = 0,
    int KritzCritBoostProviderPlayerId = 0,
    int KritzCritBoostProviderSlot = int.MaxValue,
    float KritzCritBoostDamageMultiplier = 1f,
    // Optional-at-end normal gameplay buff state. Older constructor call
    // sites remain valid and decode as an unbuffed player.
    bool IsDispenserBuffed = false,
    float DispenserAttackReloadSpeedMultiplier = 1f,
    // Authoritative Last to Die Rage state. The optional-at-end placement
    // keeps existing constructor call sites source-compatible.
    float RageCharge = 0f,
    bool IsRageReady = false,
    int RageTicksRemaining = 0,
    // Authoritative roster identity. This is deliberately metadata rather
    // than a name heuristic so practice, hosted, and dedicated bots render
    // consistently on every client.
    bool IsBot = false,
    int CurrentCombo = 0,
    int ComboTicksRemaining = 0,
    int ExperimentalCryoSlowTicksRemaining = 0,
    int ExperimentalCryoFreezeTicksRemaining = 0,
    float ExperimentalCryoExposureFraction = 0f,
    int ExperimentalGhostVisibilityTicksRemaining = 0,
    float ExperimentalGhostTrailAlpha = 0f);

/// <summary>The snapshot player movement state.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="HorizontalSpeed">The horizontal speed.</param>
/// <param name="VerticalSpeed">The vertical speed.</param>
/// <param name="IsGrounded">Whether grounded.</param>
/// <param name="RemainingAirJumps">The remaining air jumps.</param>
/// <param name="FacingDirectionX">The facing direction X component.</param>
/// <param name="AimDirectionDegrees">The aim direction in degrees.</param>
/// <param name="MovementState">The movement state.</param>
/// <param name="IsTaunting">Whether taunting.</param>
/// <param name="BurnIntensity">The burn intensity.</param>
/// <param name="GameplayEquippedSlot">The gameplay equipped slot.</param>
/// <param name="PrimaryCooldownTicks">The primary cooldown ticks.</param>
/// <param name="ReloadTicksUntilNextShell">The reload ticks until next shell.</param>
/// <param name="OffhandCooldownTicks">The offhand cooldown ticks.</param>
/// <param name="OffhandReloadTicks">The offhand reload ticks.</param>
/// <param name="MedicHealTargetId">The medic heal target id.</param>
/// <param name="IsMedicHealing">Whether medic healing.</param>
public sealed record SnapshotPlayerMovementState(
    byte Slot,
    float X,
    float Y,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    int RemainingAirJumps,
    float FacingDirectionX,
    float AimDirectionDegrees,
    byte MovementState,
    bool IsTaunting,
    float BurnIntensity,
    // Animation-critical weapon state. Included in movement delta so weapon switches and
    // recoil/reload animations are visible to all players every tick, not subject to budget trimming.
    byte GameplayEquippedSlot = 0,
    int PrimaryCooldownTicks = 0,
    int ReloadTicksUntilNextShell = 0,
    int OffhandCooldownTicks = 0,
    int OffhandReloadTicks = 0,
    int MedicHealTargetId = -1,
    bool IsMedicHealing = false);

/// <summary>The snapshot player status state.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="Health">The health.</param>
/// <param name="MaxHealth">The max health.</param>
/// <param name="Ammo">The ammo.</param>
/// <param name="MaxAmmo">The max ammo.</param>
/// <param name="Metal">The metal.</param>
/// <param name="IsCarryingIntel">Whether carrying intel.</param>
/// <param name="IntelRechargeTicks">The intel recharge ticks.</param>
/// <param name="SecondaryAmmoStates">The secondary ammo states.</param>
/// <param name="CurrentCombo">The current combo.</param>
/// <param name="ComboTicksRemaining">The combo ticks remaining.</param>
public sealed record SnapshotPlayerStatusState(
    byte Slot,
    short Health,
    short MaxHealth,
    short Ammo,
    short MaxAmmo,
    float Metal,
    bool IsCarryingIntel,
    float IntelRechargeTicks,
    // Compact runtime replicated states that must not wait for a full-player update
    // (secondary ammo, ability cooldowns, and short-lived presentation toggles).
    IReadOnlyList<SnapshotReplicatedStateEntry>? SecondaryAmmoStates = null,
    int CurrentCombo = 0,
    int ComboTicksRemaining = 0);

/// <summary>The snapshot player chat bubble state.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="IsChatBubbleVisible">Whether chat bubble visible.</param>
/// <param name="ChatBubbleFrameIndex">The chat bubble frame index.</param>
/// <param name="ChatBubbleAlpha">The chat bubble alpha.</param>
/// <param name="IsTypingChatMessage">Whether typing chat message.</param>
public sealed record SnapshotPlayerChatBubbleState(
    byte Slot,
    bool IsChatBubbleVisible,
    int ChatBubbleFrameIndex,
    float ChatBubbleAlpha,
    bool IsTypingChatMessage = false);

/// <summary>The snapshot player extended status state.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="IsSpyCloaked">Whether spy cloaked.</param>
/// <param name="SpyCloakAlpha">The spy cloak alpha.</param>
/// <param name="IsSpySuperjumping">Whether spy superjumping.</param>
/// <param name="SpySuperjumpHorizontalVelocity">The spy superjump horizontal velocity.</param>
/// <param name="SpySuperjumpCooldownTicksRemaining">The spy superjump cooldown ticks remaining.</param>
/// <param name="SpyBackstabVisualTicksRemaining">The spy backstab visual ticks remaining.</param>
/// <param name="IsUbered">Whether ubered.</param>
/// <param name="IsKritzCritBoosted">Whether kritz crit boosted.</param>
/// <param name="IsHeavyEating">Whether heavy eating.</param>
/// <param name="HeavyEatTicksRemaining">The heavy eat ticks remaining.</param>
/// <param name="IsSniperScoped">Whether sniper scoped.</param>
/// <param name="MedicNeedleCooldownTicks">The medic needle cooldown ticks.</param>
/// <param name="MedicNeedleRefillTicks">The medic needle refill ticks.</param>
/// <param name="PyroAirblastCooldownTicks">The pyro airblast cooldown ticks.</param>
/// <param name="PyroFlareCooldownTicks">The pyro flare cooldown ticks.</param>
/// <param name="PyroPrimaryFuelScaled">The pyro primary fuel scaled.</param>
/// <param name="IsPyroPrimaryRefilling">Whether pyro primary refilling.</param>
/// <param name="PyroFlameLoopTicksRemaining">The pyro flame loop ticks remaining.</param>
/// <param name="PyroPrimaryRequiresReleaseAfterEmpty">Whether pyro primary requires release after empty.</param>
/// <param name="HeavyEatCooldownTicksRemaining">The heavy eat cooldown ticks remaining.</param>
/// <param name="MedicUberCharge">The medic uber charge.</param>
/// <param name="IsMedicUberReady">Whether medic uber ready.</param>
/// <param name="LastToDieSpyCloakMeterUnits">The last to die spy cloak meter units.</param>
/// <param name="LastToDieSpyRogueRampStacks">The last to die spy rogue ramp stacks.</param>
/// <param name="LastToDieSpyRogueRampTicks">The last to die spy rogue ramp ticks.</param>
/// <param name="SpySuperjumpAvailableCharges">The spy superjump available charges.</param>
/// <param name="SpySuperjumpMaximumCharges">The spy superjump maximum charges.</param>
/// <param name="SpySuperjumpChargeTicks">The spy superjump charge ticks.</param>
/// <param name="SpySuperjumpChargeDirectionDegrees">The spy superjump charge direction degrees.</param>
/// <param name="SpySuperjumpChargeStartMovementButtons">The spy superjump charge start movement buttons.</param>
/// <param name="SpySuperjumpChargeStartBlockedUntilAbilityRelease">Whether spy superjump charge start blocked until ability release.</param>
/// <param name="MedicUberDeliveryState">The medic uber delivery state.</param>
/// <param name="KritzCritBoostProviderPlayerId">The kritz crit boost provider player id.</param>
/// <param name="KritzCritBoostProviderSlot">The kritz crit boost provider slot.</param>
/// <param name="KritzCritBoostDamageMultiplier">The kritz crit boost damage multiplier.</param>
/// <param name="IsDispenserBuffed">Whether dispenser buffed.</param>
/// <param name="DispenserAttackReloadSpeedMultiplier">The dispenser attack reload speed multiplier.</param>
/// <param name="RageCharge">The rage charge.</param>
/// <param name="IsRageReady">Whether rage ready.</param>
/// <param name="RageTicksRemaining">The rage ticks remaining.</param>
/// <param name="ExperimentalCryoSlowTicksRemaining">The experimental cryo slow ticks remaining.</param>
/// <param name="ExperimentalCryoFreezeTicksRemaining">The experimental cryo freeze ticks remaining.</param>
/// <param name="ExperimentalCryoExposureFraction">The experimental cryo exposure fraction.</param>
/// <param name="ExperimentalGhostVisibilityTicksRemaining">The experimental ghost visibility ticks remaining.</param>
/// <param name="ExperimentalGhostTrailAlpha">The experimental ghost trail alpha.</param>
public sealed record SnapshotPlayerExtendedStatusState(
    byte Slot,
    bool IsSpyCloaked,
    float SpyCloakAlpha,
    bool IsSpySuperjumping,
    float SpySuperjumpHorizontalVelocity,
    int SpySuperjumpCooldownTicksRemaining,
    int SpyBackstabVisualTicksRemaining,
    bool IsUbered,
    bool IsKritzCritBoosted,
    bool IsHeavyEating,
    int HeavyEatTicksRemaining,
    bool IsSniperScoped,
    int MedicNeedleCooldownTicks = 0,
    int MedicNeedleRefillTicks = 0,
    int PyroAirblastCooldownTicks = 0,
    int PyroFlareCooldownTicks = 0,
    int PyroPrimaryFuelScaled = 0,
    bool IsPyroPrimaryRefilling = false,
    int PyroFlameLoopTicksRemaining = 0,
    bool PyroPrimaryRequiresReleaseAfterEmpty = false,
    int HeavyEatCooldownTicksRemaining = 0,
    float MedicUberCharge = 0f,
    bool IsMedicUberReady = false,
    ushort LastToDieSpyCloakMeterUnits = 0,
    byte LastToDieSpyRogueRampStacks = 0,
    ushort LastToDieSpyRogueRampTicks = 0,
    byte SpySuperjumpAvailableCharges = 1,
    byte SpySuperjumpMaximumCharges = 1,
    ushort SpySuperjumpChargeTicks = 0,
    float SpySuperjumpChargeDirectionDegrees = 0f,
    byte SpySuperjumpChargeStartMovementButtons = 0,
    bool SpySuperjumpChargeStartBlockedUntilAbilityRelease = false,
    byte MedicUberDeliveryState = 0,
    int KritzCritBoostProviderPlayerId = 0,
    int KritzCritBoostProviderSlot = int.MaxValue,
    float KritzCritBoostDamageMultiplier = 1f,
    bool IsDispenserBuffed = false,
    float DispenserAttackReloadSpeedMultiplier = 1f,
    float RageCharge = 0f,
    bool IsRageReady = false,
    int RageTicksRemaining = 0,
    int ExperimentalCryoSlowTicksRemaining = 0,
    int ExperimentalCryoFreezeTicksRemaining = 0,
    float ExperimentalCryoExposureFraction = 0f,
    int ExperimentalGhostVisibilityTicksRemaining = 0,
    float ExperimentalGhostTrailAlpha = 0f);

/// <summary>The snapshot intel state.</summary>
/// <param name="Team">The team.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="IsAtBase">Whether at base.</param>
/// <param name="IsDropped">Whether dropped.</param>
/// <param name="ReturnTicksRemaining">The return ticks remaining.</param>
public sealed record SnapshotIntelState(
    byte Team,
    float X,
    float Y,
    bool IsAtBase,
    bool IsDropped,
    int ReturnTicksRemaining);

/// <summary>The snapshot sentry state.</summary>
/// <param name="Id">The id.</param>
/// <param name="OwnerPlayerId">The owner player id.</param>
/// <param name="Team">The team.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="Health">The health.</param>
/// <param name="IsBuilt">Whether built.</param>
/// <param name="FacingDirectionX">The facing direction X component.</param>
/// <param name="AimDirectionDegrees">The aim direction in degrees.</param>
/// <param name="ShotTraceTicksRemaining">The shot trace ticks remaining.</param>
/// <param name="HasLanded">Whether landed.</param>
/// <param name="HasActiveTarget">Whether active target.</param>
/// <param name="LastShotTargetX">The last shot target X coordinate.</param>
/// <param name="LastShotTargetY">The last shot target Y coordinate.</param>
/// <param name="IsDispenser">Whether dispenser.</param>
/// <param name="DispenserRampTicks">The dispenser ramp ticks.</param>
public sealed record SnapshotSentryState(
    int Id,
    int OwnerPlayerId,
    byte Team,
    float X,
    float Y,
    int Health,
    bool IsBuilt,
    float FacingDirectionX,
    float AimDirectionDegrees,
    int ShotTraceTicksRemaining,
    bool HasLanded,
    bool HasActiveTarget,
    float LastShotTargetX,
    float LastShotTargetY,
    bool IsDispenser = false,
    int DispenserRampTicks = 0);

/// <summary>
/// Lightweight sentry update for delta compression. Contains only frequently-changing fields.
/// </summary>
public sealed record SnapshotSentryUpdateState(
    int Id,
    float X,
    float Y,
    int Health,
    float FacingDirectionX,
    float AimDirectionDegrees,
    int ShotTraceTicksRemaining,
    bool HasActiveTarget,
    float LastShotTargetX,
    float LastShotTargetY,
    int DispenserRampTicks = 0);

/// <summary>The snapshot shot state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="OwnerId">The owner id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="VelocityX">The X velocity.</param>
/// <param name="VelocityY">The Y velocity.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="IsCritical">Whether critical.</param>
/// <param name="IsMedicHealNeedle">Whether medic heal needle.</param>
/// <param name="IsArrow">Whether arrow.</param>
/// <param name="ArrowFakeSpeedMultiplier">The arrow fake speed multiplier.</param>
/// <param name="IsLanded">Whether landed.</param>
/// <param name="AppliesLastToDieGuardian">Whether applies last to die guardian.</param>
/// <param name="PiercesPlayers">Whether pierces players.</param>
/// <param name="AppliesLastToDieTranqDarts">Whether applies last to die tranq darts.</param>
/// <param name="LastToDiePoisonDamagePerSecond">The last to die poison damage per second.</param>
/// <param name="LastToDieGhostDamageMultiplier">The last to die ghost damage multiplier.</param>
/// <param name="AppliesLastToDieDecapitator">Whether applies last to die decapitator.</param>
/// <param name="IsLastToDieDecapitatorFullyCharged">Whether last to die decapitator fully charged.</param>
/// <param name="LastToDieAttachedHeadClassId">The last to die attached head class id.</param>
/// <param name="LastToDieAttachedHeadTeam">The last to die attached head team.</param>
/// <param name="AppliesLastToDieExplosiveTip">Whether applies last to die explosive tip.</param>
/// <param name="DamageValue">The damage value.</param>
/// <param name="LastToDieRevolverProfile">The last to die revolver profile.</param>
/// <param name="AppliesLuckyStrikeStun">Whether applies lucky strike stun.</param>
/// <param name="LastToDieMedicKritzM2Payload">The last to die medic kritz m2 payload.</param>
/// <param name="IsLastToDieMedicJavelinAnchored">Whether last to die medic javelin anchored.</param>
/// <param name="LastToDieMedicJavelinFuseTicksRemaining">The last to die medic javelin fuse ticks remaining.</param>
/// <param name="HasLastToDieMedicJavelinExploded">Whether last to die medic javelin exploded.</param>
/// <param name="CriticalDamageMultiplier">The critical damage multiplier.</param>
/// <param name="PlayerKnockbackImpulse">The player knockback impulse.</param>
/// <param name="PlayerKnockbackAirborneVerticalScale">The player knockback airborne vertical scale.</param>
/// <param name="PlayerKnockbackGroundedVerticalScale">The player knockback grounded vertical scale.</param>
/// <param name="FlareStyle">The flare style.</param>
public sealed record SnapshotShotState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    int TicksRemaining,
    bool IsCritical = false,
    bool IsMedicHealNeedle = false,
    bool IsArrow = false,
    float ArrowFakeSpeedMultiplier = 1f,
    bool IsLanded = false,
    bool AppliesLastToDieGuardian = false,
    bool PiercesPlayers = false,
    bool AppliesLastToDieTranqDarts = false,
    float LastToDiePoisonDamagePerSecond = 0f,
    float LastToDieGhostDamageMultiplier = 1f,
    bool AppliesLastToDieDecapitator = false,
    bool IsLastToDieDecapitatorFullyCharged = false,
    byte LastToDieAttachedHeadClassId = 0,
    byte LastToDieAttachedHeadTeam = 0,
    bool AppliesLastToDieExplosiveTip = false,
    float DamageValue = 0f,
    int LastToDieRevolverProfile = 0,
    bool AppliesLuckyStrikeStun = false,
    byte LastToDieMedicKritzM2Payload = 0,
    bool IsLastToDieMedicJavelinAnchored = false,
    int LastToDieMedicJavelinFuseTicksRemaining = 0,
    bool HasLastToDieMedicJavelinExploded = false,
    float CriticalDamageMultiplier = 1f,
    float PlayerKnockbackImpulse = 0f,
    float PlayerKnockbackAirborneVerticalScale = 1f,
    float PlayerKnockbackGroundedVerticalScale = 1f,
    byte FlareStyle = 0,
    bool IsBoomstickPellet = false);

/// <summary>The snapshot grenade state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="OwnerId">The owner id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="PreviousX">The previous X coordinate.</param>
/// <param name="PreviousY">The previous Y coordinate.</param>
/// <param name="VelocityX">The X velocity.</param>
/// <param name="VelocityY">The Y velocity.</param>
/// <param name="FuseTicksLeft">The fuse ticks left.</param>
/// <param name="IsCritical">Whether critical.</param>
/// <param name="CriticalDamageMultiplier">The critical damage multiplier.</param>
public sealed record SnapshotGrenadeState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float PreviousX,
    float PreviousY,
    float VelocityX,
    float VelocityY,
    int FuseTicksLeft,
    bool IsCritical = false,
    float CriticalDamageMultiplier = 1f);

/// <summary>The snapshot rocket state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="OwnerId">The owner id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="PreviousX">The previous X coordinate.</param>
/// <param name="PreviousY">The previous Y coordinate.</param>
/// <param name="DirectionRadians">The direction in radians.</param>
/// <param name="Speed">The speed.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="ReducedKnockbackSourceTicksRemaining">The reduced knockback source ticks remaining.</param>
/// <param name="ZeroKnockbackSourceTicksRemaining">The zero knockback source ticks remaining.</param>
/// <param name="RangeAnchorOwnerId">The range anchor owner id.</param>
/// <param name="LastKnownRangeOriginX">The last known range origin X coordinate.</param>
/// <param name="LastKnownRangeOriginY">The last known range origin Y coordinate.</param>
/// <param name="DistanceToTravel">The distance to travel.</param>
/// <param name="IsFading">Whether fading.</param>
/// <param name="FadeSourceTicksRemaining">The fade source ticks remaining.</param>
/// <param name="PassedFriendlyPlayerIds">The passed friendly player ids.</param>
/// <param name="IsCritical">Whether critical.</param>
/// <param name="CriticalDamageMultiplier">The critical damage multiplier.</param>
/// <param name="IsBallistic">Whether ballistic.</param>
/// <param name="BallisticGravityPerTick">The ballistic gravity per tick.</param>
/// <param name="SuppressSmokeTrail">Whether suppress smoke trail.</param>
public sealed record SnapshotRocketState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float PreviousX,
    float PreviousY,
    float DirectionRadians,
    float Speed,
    int TicksRemaining,
    float ReducedKnockbackSourceTicksRemaining = 20f,
    float ZeroKnockbackSourceTicksRemaining = 30f,
    int RangeAnchorOwnerId = -1,
    float LastKnownRangeOriginX = 0f,
    float LastKnownRangeOriginY = 0f,
    float DistanceToTravel = 800f,
    bool IsFading = false,
    float FadeSourceTicksRemaining = 0f,
    IReadOnlyList<int>? PassedFriendlyPlayerIds = null,
    bool IsCritical = false,
    float CriticalDamageMultiplier = 1f,
    bool IsBallistic = false,
    float BallisticGravityPerTick = 0f,
    bool SuppressSmokeTrail = false);

/// <summary>The snapshot rocket spawn event.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="OwnerId">The owner id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="PreviousX">The previous X coordinate.</param>
/// <param name="PreviousY">The previous Y coordinate.</param>
/// <param name="DirectionRadians">The direction in radians.</param>
/// <param name="Speed">The speed.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="ReducedKnockbackSourceTicksRemaining">The reduced knockback source ticks remaining.</param>
/// <param name="ZeroKnockbackSourceTicksRemaining">The zero knockback source ticks remaining.</param>
/// <param name="RangeAnchorOwnerId">The range anchor owner id.</param>
/// <param name="LastKnownRangeOriginX">The last known range origin X coordinate.</param>
/// <param name="LastKnownRangeOriginY">The last known range origin Y coordinate.</param>
/// <param name="DistanceToTravel">The distance to travel.</param>
/// <param name="IsFading">Whether fading.</param>
/// <param name="FadeSourceTicksRemaining">The fade source ticks remaining.</param>
/// <param name="ExplodeImmediately">Whether explode immediately.</param>
/// <param name="IsCritical">Whether critical.</param>
/// <param name="EventId">The event id.</param>
/// <param name="PassedFriendlyPlayerIds">The passed friendly player ids.</param>
/// <param name="CriticalDamageMultiplier">The critical damage multiplier.</param>
/// <param name="IsBallistic">Whether ballistic.</param>
/// <param name="BallisticGravityPerTick">The ballistic gravity per tick.</param>
/// <param name="SuppressSmokeTrail">Whether suppress smoke trail.</param>
public sealed record SnapshotRocketSpawnEvent(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float PreviousX,
    float PreviousY,
    float DirectionRadians,
    float Speed,
    int TicksRemaining,
    float ReducedKnockbackSourceTicksRemaining = 20f,
    float ZeroKnockbackSourceTicksRemaining = 30f,
    int RangeAnchorOwnerId = -1,
    float LastKnownRangeOriginX = 0f,
    float LastKnownRangeOriginY = 0f,
    float DistanceToTravel = 800f,
    bool IsFading = false,
    float FadeSourceTicksRemaining = 0f,
    bool ExplodeImmediately = false,
    bool IsCritical = false,
    ulong EventId = 0,
    IReadOnlyList<int>? PassedFriendlyPlayerIds = null,
    float CriticalDamageMultiplier = 1f,
    bool IsBallistic = false,
    float BallisticGravityPerTick = 0f,
    bool SuppressSmokeTrail = false);

/// <summary>The snapshot flame state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="OwnerId">The owner id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="PreviousX">The previous X coordinate.</param>
/// <param name="PreviousY">The previous Y coordinate.</param>
/// <param name="VelocityX">The X velocity.</param>
/// <param name="VelocityY">The Y velocity.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="AttachedPlayerId">The attached player id.</param>
/// <param name="AttachedOffsetX">The attached offset X coordinate.</param>
/// <param name="AttachedOffsetY">The attached offset Y coordinate.</param>
/// <param name="IsCritical">Whether critical.</param>
/// <param name="CriticalDamageMultiplier">The critical damage multiplier.</param>
public sealed record SnapshotFlameState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float PreviousX,
    float PreviousY,
    float VelocityX,
    float VelocityY,
    int TicksRemaining,
    int AttachedPlayerId,
    float AttachedOffsetX,
    float AttachedOffsetY,
    bool IsCritical = false,
    float CriticalDamageMultiplier = 1f);

/// <summary>The snapshot mine state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="OwnerId">The owner id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="VelocityX">The X velocity.</param>
/// <param name="VelocityY">The Y velocity.</param>
/// <param name="IsStickied">Whether stickied.</param>
/// <param name="IsDestroyed">Whether destroyed.</param>
/// <param name="ExplosionDamage">The explosion damage.</param>
/// <param name="IsCritical">Whether critical.</param>
/// <param name="CriticalDamageMultiplier">The critical damage multiplier.</param>
public sealed record SnapshotMineState(
    int Id,
    byte Team,
    int OwnerId,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    bool IsStickied,
    bool IsDestroyed,
    float ExplosionDamage,
    bool IsCritical = false,
    float CriticalDamageMultiplier = 1f);

/// <summary>The snapshot control point state.</summary>
/// <param name="Index">The index.</param>
/// <param name="Team">The team.</param>
/// <param name="CappingTeam">The capping team.</param>
/// <param name="CappingTicks">The capping ticks.</param>
/// <param name="CapTimeTicks">The cap time ticks.</param>
/// <param name="Cappers">The cappers.</param>
/// <param name="IsLocked">Whether locked.</param>
/// <param name="HasHealingAura">Whether healing aura.</param>
public sealed record SnapshotControlPointState(
    byte Index,
    byte Team,
    byte CappingTeam,
    ushort CappingTicks,
    ushort CapTimeTicks,
    byte Cappers,
    bool IsLocked,
    bool HasHealingAura = false);

/// <summary>The snapshot generator state.</summary>
/// <param name="Team">The team.</param>
/// <param name="Health">The health.</param>
/// <param name="MaxHealth">The max health.</param>
public sealed record SnapshotGeneratorState(
    byte Team,
    short Health,
    short MaxHealth);

/// <summary>The snapshot dead body state.</summary>
/// <param name="Id">The id.</param>
/// <param name="SourcePlayerId">The source player id.</param>
/// <param name="Team">The team.</param>
/// <param name="ClassId">The class id.</param>
/// <param name="AnimationKind">The animation kind.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="HorizontalSpeed">The horizontal speed.</param>
/// <param name="VerticalSpeed">The vertical speed.</param>
/// <param name="FacingLeft">Whether facing left.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="GameplayClassId">The gameplay class id.</param>
public sealed record SnapshotDeadBodyState(
    int Id,
    int SourcePlayerId,
    byte Team,
    byte ClassId,
    byte AnimationKind,
    float X,
    float Y,
    float Width,
    float Height,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool FacingLeft,
    int TicksRemaining,
    string GameplayClassId = "");

/// <summary>The snapshot sentry gib state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="IsDispenser">Whether dispenser.</param>
public sealed record SnapshotSentryGibState(
    int Id,
    byte Team,
    float X,
    float Y,
    int TicksRemaining,
    bool IsDispenser = false);

/// <summary>The snapshot jump pad gib state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Team">The team.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
public sealed record SnapshotJumpPadGibState(
    int Id,
    byte Team,
    float X,
    float Y,
    int TicksRemaining);

/// <summary>The snapshot jump pad state.</summary>
/// <param name="Id">The id.</param>
/// <param name="OwnerPlayerId">The owner player id.</param>
/// <param name="Team">The team.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="Health">The health.</param>
/// <param name="HasLanded">Whether landed.</param>
/// <param name="IsBuilt">Whether built.</param>
public sealed record SnapshotJumpPadState(
    int Id,
    int OwnerPlayerId,
    byte Team,
    float X,
    float Y,
    int Health,
    bool HasLanded,
    bool IsBuilt = false);

/// <summary>The snapshot civil defense turret state.</summary>
/// <param name="Id">The id.</param>
/// <param name="OwnerPlayerId">The owner player id.</param>
/// <param name="Team">The team.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="Health">The health.</param>
/// <param name="HasLanded">Whether landed.</param>
/// <param name="IsBuilt">Whether built.</param>
/// <param name="FacingDirectionX">The facing direction X component.</param>
/// <param name="AimDirectionDegrees">The aim direction in degrees.</param>
/// <param name="ReloadTicksRemaining">The reload ticks remaining.</param>
/// <param name="ShotTraceTicksRemaining">The shot trace ticks remaining.</param>
/// <param name="LastShotTargetX">The last shot target X coordinate.</param>
/// <param name="LastShotTargetY">The last shot target Y coordinate.</param>
/// <param name="LifetimeTicksRemaining">The lifetime ticks remaining.</param>
public sealed record SnapshotCivilDefenseTurretState(
    int Id, int OwnerPlayerId, byte Team, float X, float Y, int Health,
    bool HasLanded, bool IsBuilt, float FacingDirectionX, float AimDirectionDegrees,
    int ReloadTicksRemaining, int ShotTraceTicksRemaining, float LastShotTargetX, float LastShotTargetY,
    int LifetimeTicksRemaining = -1);

public sealed record SnapshotPlayerGibState(
    int Id,
    string SpriteName,
    int FrameIndex,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    float RotationDegrees,
    float RotationSpeedDegrees,
    int TicksRemaining,
    float BloodChance);

/// <summary>The snapshot gib spawn event.</summary>
/// <param name="SpriteName">The sprite name.</param>
/// <param name="FrameIndex">The frame index.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="VelocityX">The X velocity.</param>
/// <param name="VelocityY">The Y velocity.</param>
/// <param name="RotationSpeedDegrees">The rotation speed in degrees.</param>
/// <param name="HorizontalFriction">The horizontal friction.</param>
/// <param name="RotationFriction">The rotation friction.</param>
/// <param name="LifetimeTicks">The lifetime ticks.</param>
/// <param name="BloodChance">The blood chance.</param>
/// <param name="EventId">The event id.</param>
public sealed record SnapshotGibSpawnEvent(
    string SpriteName,
    int FrameIndex,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    float RotationSpeedDegrees,
    float HorizontalFriction,
    float RotationFriction,
    int LifetimeTicks,
    float BloodChance,
    ulong EventId);

public sealed record SnapshotBloodDropState(
    int Id,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    bool IsStuck,
    int TicksRemaining,
    float Scale);

/// <summary>The snapshot death cam state.</summary>
/// <param name="FocusX">The focus X coordinate.</param>
/// <param name="FocusY">The focus Y coordinate.</param>
/// <param name="KillMessage">The kill message.</param>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer team.</param>
/// <param name="Health">The health.</param>
/// <param name="MaxHealth">The max health.</param>
/// <param name="RemainingTicks">The remaining ticks.</param>
/// <param name="InitialTicks">The initial ticks.</param>
public sealed record SnapshotDeathCamState(
    float FocusX,
    float FocusY,
    string KillMessage,
    string KillerName,
    byte KillerTeam,
    int Health,
    int MaxHealth,
    int RemainingTicks,
    int InitialTicks = 0);

/// <summary>The snapshot combat trace state.</summary>
/// <param name="StartX">The start X coordinate.</param>
/// <param name="StartY">The start Y coordinate.</param>
/// <param name="EndX">The end X coordinate.</param>
/// <param name="EndY">The end Y coordinate.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="HitCharacter">Whether hit character.</param>
/// <param name="Team">The team.</param>
/// <param name="IsSniperTracer">Whether sniper tracer.</param>
/// <param name="IsCritical">Whether critical.</param>
public sealed record SnapshotCombatTraceState(
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    int TicksRemaining,
    bool HitCharacter,
    byte Team,
    bool IsSniperTracer,
    bool IsCritical = false);

/// <summary>The snapshot sniper aim indicator state.</summary>
/// <param name="SniperPlayerId">The sniper player id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="Team">The team.</param>
/// <param name="Transparency">The transparency.</param>
public sealed record SnapshotSniperAimIndicatorState(
    int SniperPlayerId,
    float X,
    float Y,
    byte Team,
    float Transparency);

/// <summary>The snapshot sound event.</summary>
/// <param name="SoundName">The sound name.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="EventId">The event id.</param>
/// <param name="SourceFrame">The source frame.</param>
/// <param name="SourcePlayerId">The source player id.</param>
public sealed record SnapshotSoundEvent(
    string SoundName,
    float X,
    float Y,
    ulong EventId = 0,
    ulong SourceFrame = 0,
    int SourcePlayerId = -1);

/// <summary>The snapshot visual event.</summary>
/// <param name="EffectName">The effect name.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="DirectionDegrees">The direction in degrees.</param>
/// <param name="Count">The count.</param>
/// <param name="EventId">The event id.</param>
/// <param name="SourceFrame">The source frame.</param>
public sealed record SnapshotVisualEvent(
    string EffectName,
    float X,
    float Y,
    float DirectionDegrees,
    int Count,
    ulong EventId = 0,
    ulong SourceFrame = 0);

/// <summary>The snapshot damage event.</summary>
/// <param name="Amount">The amount.</param>
/// <param name="AttackerPlayerId">The attacker player id.</param>
/// <param name="AssistedByPlayerId">The assisted by player id.</param>
/// <param name="TargetKind">The target kind.</param>
/// <param name="TargetEntityId">The target entity id.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="WasFatal">Whether fatal.</param>
/// <param name="EventId">The event id.</param>
/// <param name="SourceFrame">The source frame.</param>
/// <param name="Flags">The flags.</param>
public sealed record SnapshotDamageEvent(
    int Amount,
    int AttackerPlayerId,
    int AssistedByPlayerId,
    byte TargetKind,
    int TargetEntityId,
    float X,
    float Y,
    bool WasFatal,
    ulong EventId = 0,
    ulong SourceFrame = 0,
    byte Flags = 0);

/// <summary>The snapshot health pack state.</summary>
/// <param name="Id">The id.</param>
/// <param name="Size">The size.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="VelocityX">The X velocity.</param>
/// <param name="VelocityY">The Y velocity.</param>
/// <param name="TicksRemaining">The ticks remaining.</param>
/// <param name="SourceSpawnIndex">The source spawn index.</param>
/// <param name="RespawnTicksRemaining">The respawn ticks remaining.</param>
/// <param name="Active">Whether active.</param>
public sealed record SnapshotHealthPackState(
    int Id,
    byte Size,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    int TicksRemaining,
    int SourceSpawnIndex,
    int RespawnTicksRemaining,
    bool Active);

/// <summary>The kind of a snapshot replicated state value.</summary>
public enum SnapshotReplicatedStateValueKind : byte
{
    /// <summary>The whole value kind.</summary>
    Whole = 1,
    /// <summary>The scalar value kind.</summary>
    Scalar = 2,
    /// <summary>The toggle value kind.</summary>
    Toggle = 3,
}

/// <summary>The snapshot replicated state entry.</summary>
/// <param name="OwnerId">The owner id.</param>
/// <param name="Key">The key.</param>
/// <param name="Kind">The kind.</param>
/// <param name="IntValue">The int value.</param>
/// <param name="FloatValue">The float value.</param>
/// <param name="BoolValue">Whether bool value.</param>
public sealed record SnapshotReplicatedStateEntry(
    string OwnerId,
    string Key,
    SnapshotReplicatedStateValueKind Kind,
    int IntValue = 0,
    float FloatValue = 0f,
    bool BoolValue = false);

/// <summary>The snapshot kill feed entry.</summary>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer team.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim team.</param>
/// <param name="MessageText">The message text.</param>
/// <param name="MessageHighlightStart">The message highlight start.</param>
/// <param name="MessageHighlightLength">The message highlight length.</param>
/// <param name="KillerPlayerId">The killer player id.</param>
/// <param name="VictimPlayerId">The victim player id.</param>
/// <param name="SpecialType">The special type.</param>
/// <param name="EventId">The event id.</param>
public sealed record SnapshotKillFeedEntry(
    string KillerName,
    byte KillerTeam,
    string WeaponSpriteName,
    string VictimName,
    byte VictimTeam,
    string MessageText = "",
    int MessageHighlightStart = 0,
    int MessageHighlightLength = 0,
    int KillerPlayerId = -1,
    int VictimPlayerId = -1,
    KillFeedSpecialType SpecialType = KillFeedSpecialType.None,
    ulong EventId = 0)
{
    /// <summary>Gets the assist name.</summary>
    public string AssistName { get; init; } = "";
    /// <summary>Gets the assist team.</summary>
    public byte AssistTeam { get; init; }
    /// <summary>Gets the assist player id.</summary>
    public int AssistPlayerId { get; init; } = -1;
    /// <summary>Gets the involved player ids.</summary>
    public IReadOnlyList<int> InvolvedPlayerIds { get; init; } = Array.Empty<int>();
}

[Flags]
/// <summary>Which snapshot entity collections are complete.</summary>
public enum SnapshotEntityCollectionCompletenessFlags : ushort
{
    /// <summary>No collection flag.</summary>
    None = 0,
    /// <summary>The shots collection flag.</summary>
    Shots = 1 << 0,
    /// <summary>The bubbles collection flag.</summary>
    Bubbles = 1 << 1,
    /// <summary>The blades collection flag.</summary>
    Blades = 1 << 2,
    /// <summary>The needles collection flag.</summary>
    Needles = 1 << 3,
    /// <summary>The revolver shots collection flag.</summary>
    RevolverShots = 1 << 4,
    /// <summary>The rockets collection flag.</summary>
    Rockets = 1 << 5,
    /// <summary>The flames collection flag.</summary>
    Flames = 1 << 6,
    /// <summary>The flares collection flag.</summary>
    Flares = 1 << 7,
    /// <summary>The mines collection flag.</summary>
    Mines = 1 << 8,
    /// <summary>The grenades collection flag.</summary>
    Grenades = 1 << 9,
    /// <summary>All projectile collection flags.</summary>
    AllProjectiles = Shots
        | Bubbles
        | Blades
        | Needles
        | RevolverShots
        | Rockets
        | Flames
        | Flares
        | Mines
        | Grenades,
}

/// <summary>The snapshot message.</summary>
/// <param name="Frame">The frame.</param>
/// <param name="TickRate">The tick rate.</param>
/// <param name="LevelName">The level name.</param>
/// <param name="MapAreaIndex">The map area index.</param>
/// <param name="MapAreaCount">The map area count.</param>
/// <param name="GameMode">The game mode.</param>
/// <param name="MatchPhase">The match phase.</param>
/// <param name="WinnerTeam">The winner team.</param>
/// <param name="TimeRemainingTicks">The time remaining ticks.</param>
/// <param name="RedCaps">The red caps.</param>
/// <param name="BlueCaps">The blue caps.</param>
/// <param name="SpectatorCount">The spectator count.</param>
/// <param name="LastProcessedInputSequence">The last processed input sequence.</param>
/// <param name="RedIntel">The red intel.</param>
/// <param name="BlueIntel">The blue intel.</param>
/// <param name="Players">The players.</param>
/// <param name="CombatTraces">The combat traces.</param>
/// <param name="SniperAimIndicators">The sniper aim indicators.</param>
/// <param name="Sentries">The sentries.</param>
/// <param name="Shots">The shots.</param>
/// <param name="Bubbles">The bubbles.</param>
/// <param name="Blades">The blades.</param>
/// <param name="Needles">The needles.</param>
/// <param name="RevolverShots">The revolver shots.</param>
/// <param name="Rockets">The rockets.</param>
/// <param name="Flames">The flames.</param>
/// <param name="Flares">The flares.</param>
/// <param name="Mines">The mines.</param>
/// <param name="DeadBodies">The dead bodies.</param>
/// <param name="ControlPointSetupTicksRemaining">The control point setup ticks remaining.</param>
/// <param name="KothUnlockTicksRemaining">The koth unlock ticks remaining.</param>
/// <param name="KothRedTimerTicksRemaining">The koth red timer ticks remaining.</param>
/// <param name="KothBlueTimerTicksRemaining">The koth blue timer ticks remaining.</param>
/// <param name="ControlPoints">The control points.</param>
/// <param name="Generators">The generators.</param>
/// <param name="LocalDeathCam">The local death cam.</param>
/// <param name="KillFeed">The kill feed.</param>
/// <param name="VisualEvents">The visual events.</param>
/// <param name="DamageEvents">The damage events.</param>
/// <param name="SoundEvents">The sound events.</param>
/// <param name="StringCacheUpdates">The string cache updates.</param>
/// <param name="IsCustomMap">Whether the map is a custom map.</param>
/// <param name="MapDownloadUrl">The map download url.</param>
/// <param name="MapContentHash">The map content hash.</param>
/// <param name="MapScale">The map scale.</param>
public sealed record SnapshotMessage(
    ulong Frame,
    int TickRate,
    string LevelName,
    byte MapAreaIndex,
    byte MapAreaCount,
    byte GameMode,
    byte MatchPhase,
    byte WinnerTeam,
    int TimeRemainingTicks,
    int RedCaps,
    int BlueCaps,
    int SpectatorCount,
    uint LastProcessedInputSequence,
    SnapshotIntelState RedIntel,
    SnapshotIntelState BlueIntel,
    IReadOnlyList<SnapshotPlayerState> Players,
    IReadOnlyList<SnapshotCombatTraceState> CombatTraces,
    IReadOnlyList<SnapshotSniperAimIndicatorState> SniperAimIndicators,
    IReadOnlyList<SnapshotSentryState> Sentries,
    IReadOnlyList<SnapshotShotState> Shots,
    IReadOnlyList<SnapshotShotState> Bubbles,
    IReadOnlyList<SnapshotShotState> Blades,
    IReadOnlyList<SnapshotShotState> Needles,
    IReadOnlyList<SnapshotShotState> RevolverShots,
    IReadOnlyList<SnapshotRocketState> Rockets,
    IReadOnlyList<SnapshotFlameState> Flames,
    IReadOnlyList<SnapshotShotState> Flares,
    IReadOnlyList<SnapshotMineState> Mines,
    IReadOnlyList<SnapshotDeadBodyState> DeadBodies,
    int ControlPointSetupTicksRemaining,
    int KothUnlockTicksRemaining,
    int KothRedTimerTicksRemaining,
    int KothBlueTimerTicksRemaining,
    IReadOnlyList<SnapshotControlPointState> ControlPoints,
    IReadOnlyList<SnapshotGeneratorState> Generators,
    SnapshotDeathCamState? LocalDeathCam,
    IReadOnlyList<SnapshotKillFeedEntry> KillFeed,
    IReadOnlyList<SnapshotVisualEvent> VisualEvents,
    IReadOnlyList<SnapshotDamageEvent> DamageEvents,
    IReadOnlyList<SnapshotSoundEvent> SoundEvents,
    IReadOnlyDictionary<ushort, string>? StringCacheUpdates = null,
    bool IsCustomMap = false,
    string MapDownloadUrl = "",
    string MapContentHash = "",
    float MapScale = 1f) : IProtocolMessage, ISnapshotBaselineState
{
    /// <summary>Gets the time limit ticks.</summary>
    public int TimeLimitTicks { get; init; }
    /// <summary>Gets the arena unlock ticks remaining.</summary>
    public int ArenaUnlockTicksRemaining { get; init; }
    /// <summary>Gets the arena point team.</summary>
    public byte ArenaPointTeam { get; init; }
    /// <summary>Gets the arena capping team.</summary>
    public byte ArenaCappingTeam { get; init; }
    /// <summary>Gets the arena capping ticks.</summary>
    public float ArenaCappingTicks { get; init; }
    /// <summary>Gets the arena cappers.</summary>
    public int ArenaCappers { get; init; }
    /// <summary>Gets the arena red consecutive wins.</summary>
    public int ArenaRedConsecutiveWins { get; init; }
    /// <summary>Gets the arena blue consecutive wins.</summary>
    public int ArenaBlueConsecutiveWins { get; init; }
    /// <summary>Gets the competitive ready up phase.</summary>
    public byte CompetitiveReadyUpPhase { get; init; }
    /// <summary>Gets the competitive ready up ticks remaining.</summary>
    public int CompetitiveReadyUpTicksRemaining { get; init; }
    /// <summary>Gets the cap limit.</summary>
    public int CapLimit { get; init; }

    /// <summary>Gets the baseline frame.</summary>
    public ulong BaselineFrame { get; init; }
    /// <summary>Gets the is delta.</summary>
    public bool IsDelta { get; init; }
    /// <summary>Gets the player movement states.</summary>
    public IReadOnlyList<SnapshotPlayerMovementState> PlayerMovementStates { get; init; } = Array.Empty<SnapshotPlayerMovementState>();
    /// <summary>Gets the player status states.</summary>
    public IReadOnlyList<SnapshotPlayerStatusState> PlayerStatusStates { get; init; } = Array.Empty<SnapshotPlayerStatusState>();
    /// <summary>Gets the player chat bubble states.</summary>
    public IReadOnlyList<SnapshotPlayerChatBubbleState> PlayerChatBubbleStates { get; init; } = Array.Empty<SnapshotPlayerChatBubbleState>();
    /// <summary>Gets the player extended status states.</summary>
    public IReadOnlyList<SnapshotPlayerExtendedStatusState> PlayerExtendedStatusStates { get; init; } = Array.Empty<SnapshotPlayerExtendedStatusState>();
    /// <summary>Gets the scoreboard players.</summary>
    public IReadOnlyList<SnapshotPlayerState> ScoreboardPlayers { get; init; } = Array.Empty<SnapshotPlayerState>();
    /// <summary>Gets the sentry update states.</summary>
    public IReadOnlyList<SnapshotSentryUpdateState> SentryUpdateStates { get; init; } = Array.Empty<SnapshotSentryUpdateState>();
    /// <summary>Gets the removed player ids.</summary>
    public IReadOnlyList<int> RemovedPlayerIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed sentry ids.</summary>
    public IReadOnlyList<int> RemovedSentryIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed shot ids.</summary>
    public IReadOnlyList<int> RemovedShotIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed bubble ids.</summary>
    public IReadOnlyList<int> RemovedBubbleIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed blade ids.</summary>
    public IReadOnlyList<int> RemovedBladeIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed needle ids.</summary>
    public IReadOnlyList<int> RemovedNeedleIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed revolver shot ids.</summary>
    public IReadOnlyList<int> RemovedRevolverShotIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed rocket ids.</summary>
    public IReadOnlyList<int> RemovedRocketIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed flame ids.</summary>
    public IReadOnlyList<int> RemovedFlameIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed flare ids.</summary>
    public IReadOnlyList<int> RemovedFlareIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed mine ids.</summary>
    public IReadOnlyList<int> RemovedMineIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the grenades.</summary>
    public IReadOnlyList<SnapshotGrenadeState> Grenades { get; init; } = Array.Empty<SnapshotGrenadeState>();
    /// <summary>Gets the removed grenade ids.</summary>
    public IReadOnlyList<int> RemovedGrenadeIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed player gib ids.</summary>
    public IReadOnlyList<int> RemovedPlayerGibIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed dead body ids.</summary>
    public IReadOnlyList<int> RemovedDeadBodyIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed sentry gib ids.</summary>
    public IReadOnlyList<int> RemovedSentryGibIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the sentry gibs.</summary>
    public IReadOnlyList<SnapshotSentryGibState> SentryGibs { get; init; } = Array.Empty<SnapshotSentryGibState>();
    /// <summary>Gets the jump pads.</summary>
    public IReadOnlyList<SnapshotJumpPadState> JumpPads { get; init; } = Array.Empty<SnapshotJumpPadState>();
    /// <summary>Gets the civil defense turrets.</summary>
    public IReadOnlyList<SnapshotCivilDefenseTurretState> CivilDefenseTurrets { get; init; } = Array.Empty<SnapshotCivilDefenseTurretState>();
    /// <summary>Gets the removed jump pad ids.</summary>
    public IReadOnlyList<int> RemovedJumpPadIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the removed civil defense turret ids.</summary>
    public IReadOnlyList<int> RemovedCivilDefenseTurretIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the jump pad gibs.</summary>
    public IReadOnlyList<SnapshotJumpPadGibState> JumpPadGibs { get; init; } = Array.Empty<SnapshotJumpPadGibState>();
    /// <summary>Gets the removed jump pad gib ids.</summary>
    public IReadOnlyList<int> RemovedJumpPadGibIds { get; init; } = Array.Empty<int>();
    /// <summary>Gets the health packs.</summary>
    public IReadOnlyList<SnapshotHealthPackState> HealthPacks { get; init; } = Array.Empty<SnapshotHealthPackState>();
    /// <summary>Gets the removed health pack ids.</summary>
    public IReadOnlyList<int> RemovedHealthPackIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<SnapshotPlayerGibState> PlayerGibs { get; init; } = Array.Empty<SnapshotPlayerGibState>();
    /// <summary>Gets the gib spawn events.</summary>
    public IReadOnlyList<SnapshotGibSpawnEvent> GibSpawnEvents { get; init; } = Array.Empty<SnapshotGibSpawnEvent>();
    /// <summary>Gets the rocket spawn events.</summary>
    public IReadOnlyList<SnapshotRocketSpawnEvent> RocketSpawnEvents { get; init; } = Array.Empty<SnapshotRocketSpawnEvent>();
    /// <summary>Gets the entity collection completeness flags.</summary>
    public SnapshotEntityCollectionCompletenessFlags EntityCollectionCompletenessFlags { get; init; } =
        SnapshotEntityCollectionCompletenessFlags.AllProjectiles;

    /// <summary>Gets the message type.</summary>
    public MessageType Type => MessageType.Snapshot;
}
