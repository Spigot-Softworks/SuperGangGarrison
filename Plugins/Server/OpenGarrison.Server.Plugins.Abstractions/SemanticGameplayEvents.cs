using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// The kind of buildable in a buildable event.
/// </summary>
public enum OpenGarrisonServerBuildableKind : byte
{
    /// <summary>Unknown buildable kind.</summary>
    Unknown = 0,
    /// <summary>A sentry.</summary>
    Sentry = 1,
    /// <summary>A generator.</summary>
    Generator = 2,
}

/// <summary>
/// The kind of intelligence event.
/// </summary>
public enum OpenGarrisonServerIntelEventKind : byte
{
    /// <summary>Unknown event kind.</summary>
    Unknown = 0,
    /// <summary>The intelligence was picked up.</summary>
    PickedUp = 1,
    /// <summary>The intelligence was dropped.</summary>
    Dropped = 2,
    /// <summary>The intelligence was returned.</summary>
    Returned = 3,
    /// <summary>The intelligence was captured.</summary>
    Captured = 4,
}

/// <summary>
/// A damage event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Amount">The damage amount.</param>
/// <param name="TargetKind">The kind of entity damaged.</param>
/// <param name="TargetEntityId">The damaged entity id.</param>
/// <param name="WasFatal">Whether the damage was fatal.</param>
/// <param name="AttackerPlayerId">The attacking player id.</param>
/// <param name="AttackerName">The attacking player name.</param>
/// <param name="AttackerTeam">The attacking player's team, if any.</param>
/// <param name="AssistedByPlayerId">The assisting player id.</param>
/// <param name="AssistedByName">The assisting player name.</param>
/// <param name="AssistedByTeam">The assisting player's team, if any.</param>
/// <param name="VictimPlayerId">The victim player id.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim's team, if any.</param>
/// <param name="WorldX">The world X of the damage.</param>
/// <param name="WorldY">The world Y of the damage.</param>
/// <param name="Flags">Additional damage flags.</param>
public readonly record struct OpenGarrisonServerDamageEvent(
    long Frame,
    int Amount,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    bool WasFatal,
    int AttackerPlayerId,
    string AttackerName,
    PlayerTeam? AttackerTeam,
    int AssistedByPlayerId,
    string AssistedByName,
    PlayerTeam? AssistedByTeam,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam? VictimTeam,
    float WorldX,
    float WorldY,
    DamageEventFlags Flags);

/// <summary>
/// A player death event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="VictimPlayerId">The victim player id.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim's team.</param>
/// <param name="KillerPlayerId">The killer player id.</param>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer's team, if any.</param>
/// <param name="AssistedByPlayerId">The assisting player id.</param>
/// <param name="AssistedByName">The assisting player name.</param>
/// <param name="AssistedByTeam">The assisting player's team, if any.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
/// <param name="MessageText">The death message text.</param>
public readonly record struct OpenGarrisonServerDeathEvent(
    long Frame,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam VictimTeam,
    int KillerPlayerId,
    string KillerName,
    PlayerTeam? KillerTeam,
    int AssistedByPlayerId,
    string AssistedByName,
    PlayerTeam? AssistedByTeam,
    string WeaponSpriteName,
    string MessageText);

/// <summary>
/// A kill assist event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="AssistantPlayerId">The assisting player id.</param>
/// <param name="AssistantName">The assisting player name.</param>
/// <param name="AssistantTeam">The assisting player's team.</param>
/// <param name="KillerPlayerId">The killer player id.</param>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer's team.</param>
/// <param name="VictimPlayerId">The victim player id.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim's team.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
public readonly record struct OpenGarrisonServerAssistEvent(
    long Frame,
    int AssistantPlayerId,
    string AssistantName,
    PlayerTeam AssistantTeam,
    int KillerPlayerId,
    string KillerName,
    PlayerTeam KillerTeam,
    int VictimPlayerId,
    string VictimName,
    PlayerTeam VictimTeam,
    string WeaponSpriteName);

/// <summary>
/// A buildable built or destroyed event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Kind">The buildable kind.</param>
/// <param name="EntityId">The buildable entity id.</param>
/// <param name="OwnerPlayerId">The owning player id.</param>
/// <param name="OwnerName">The owning player name.</param>
/// <param name="Team">The buildable's team, if any.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
public readonly record struct OpenGarrisonServerBuildableEvent(
    long Frame,
    OpenGarrisonServerBuildableKind Kind,
    int EntityId,
    int OwnerPlayerId,
    string OwnerName,
    PlayerTeam? Team,
    float WorldX,
    float WorldY);

/// <summary>
/// An intelligence event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Kind">The event kind.</param>
/// <param name="IntelTeam">The team owning the intelligence.</param>
/// <param name="ActingTeam">The acting player's team.</param>
/// <param name="ActingPlayerId">The acting player id.</param>
/// <param name="ActingPlayerName">The acting player name.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
public readonly record struct OpenGarrisonServerIntelEvent(
    long Frame,
    OpenGarrisonServerIntelEventKind Kind,
    PlayerTeam IntelTeam,
    PlayerTeam ActingTeam,
    int ActingPlayerId,
    string ActingPlayerName,
    float WorldX,
    float WorldY);

/// <summary>
/// A control point state change observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Index">The control point index.</param>
/// <param name="Team">The owning team, if any.</param>
/// <param name="CappingTeam">The capping team, if any.</param>
/// <param name="Cappers">The capper count.</param>
/// <param name="Progress">The capture progress (0 to 1).</param>
/// <param name="IsLocked">Whether the point is locked.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
public readonly record struct OpenGarrisonServerControlPointStateEvent(
    long Frame,
    int Index,
    PlayerTeam? Team,
    PlayerTeam? CappingTeam,
    int Cappers,
    float Progress,
    bool IsLocked,
    float WorldX,
    float WorldY);

/// <summary>
/// A player spawn event observed by the server.
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
public readonly record struct OpenGarrisonServerPlayerSpawnEvent(
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
/// A player join event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="EndPoint">The player's network endpoint.</param>
/// <param name="IsAuthorized">Whether the player is authorized.</param>
/// <param name="IsSpectator">Whether the player is spectating.</param>
public readonly record struct OpenGarrisonServerPlayerJoinedEvent(
    long Frame,
    byte Slot,
    string PlayerName,
    string EndPoint,
    bool IsAuthorized,
    bool IsSpectator);

/// <summary>
/// A player leave event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="EndPoint">The player's network endpoint.</param>
/// <param name="Reason">The leave reason.</param>
/// <param name="WasAuthorized">Whether the player was authorized.</param>
public readonly record struct OpenGarrisonServerPlayerLeftEvent(
    long Frame,
    byte Slot,
    string PlayerName,
    string EndPoint,
    string Reason,
    bool WasAuthorized);

/// <summary>
/// A player respawn event observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerId">The player id.</param>
/// <param name="PlayerName">The player's name.</param>
/// <param name="Team">The player's team.</param>
/// <param name="PlayerClass">The player's class.</param>
/// <param name="WorldX">The respawn world X.</param>
/// <param name="WorldY">The respawn world Y.</param>
public readonly record struct OpenGarrisonServerPlayerRespawnEvent(
    long Frame,
    byte Slot,
    int PlayerId,
    string PlayerName,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    float WorldX,
    float WorldY);

/// <summary>
/// A gameplay ability input event that plugins can cancel before the ability runs.
/// </summary>
/// <param name="frame">The frame.</param>
/// <param name="playerId">The player id.</param>
/// <param name="classId">The player's class.</param>
/// <param name="team">The player's team.</param>
/// <param name="itemId">The ability item id.</param>
/// <param name="behaviorId">The behavior id.</param>
/// <param name="abilityCategory">The ability category.</param>
/// <param name="activation">The activation kind.</param>
/// <param name="executorId">The executor id.</param>
/// <param name="phase">The ability phase.</param>
/// <param name="tags">The ability tags.</param>
public sealed class OpenGarrisonServerGameplayAbilityInputEvent(
    long frame,
    int playerId,
    PlayerClass classId,
    PlayerTeam team,
    string itemId,
    string behaviorId,
    string abilityCategory,
    string activation,
    string executorId,
    string phase,
    IReadOnlyList<string> tags)
{
    /// <summary>Gets the frame.</summary>
    public long Frame { get; } = frame;

    /// <summary>Gets the player id.</summary>
    public int PlayerId { get; } = playerId;

    /// <summary>Gets the player's class.</summary>
    public PlayerClass ClassId { get; } = classId;

    /// <summary>Gets the player's team.</summary>
    public PlayerTeam Team { get; } = team;

    /// <summary>Gets the ability item id.</summary>
    public string ItemId { get; } = itemId;

    /// <summary>Gets the behavior id.</summary>
    public string BehaviorId { get; } = behaviorId;

    /// <summary>Gets the ability category.</summary>
    public string AbilityCategory { get; } = abilityCategory;

    /// <summary>Gets the activation kind.</summary>
    public string Activation { get; } = activation;

    /// <summary>Gets the executor id.</summary>
    public string ExecutorId { get; } = executorId;

    /// <summary>Gets the ability phase.</summary>
    public string Phase { get; } = phase;

    /// <summary>Gets the ability tags.</summary>
    public IReadOnlyList<string> Tags { get; } = tags;

    /// <summary>Gets whether the input was cancelled.</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>
    /// Cancels the ability input so the ability does not run.
    /// </summary>
    public void Cancel()
    {
        IsCancelled = true;
    }
}

/// <summary>
/// A gameplay ability use observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="PlayerId">The player id.</param>
/// <param name="ClassId">The player's class.</param>
/// <param name="Team">The player's team.</param>
/// <param name="ItemId">The ability item id.</param>
/// <param name="BehaviorId">The behavior id.</param>
/// <param name="AbilityCategory">The ability category.</param>
/// <param name="Activation">The activation kind.</param>
/// <param name="ExecutorId">The executor id.</param>
/// <param name="Phase">The ability phase.</param>
/// <param name="Tags">The ability tags.</param>
/// <param name="Handled">Whether the ability was handled.</param>
/// <param name="ConsumedInput">Whether the input was consumed.</param>
public readonly record struct OpenGarrisonServerGameplayAbilityUsedEvent(
    long Frame,
    int PlayerId,
    PlayerClass ClassId,
    PlayerTeam Team,
    string ItemId,
    string BehaviorId,
    string AbilityCategory,
    string Activation,
    string ExecutorId,
    string Phase,
    IReadOnlyList<string> Tags,
    bool Handled,
    bool ConsumedInput);

/// <summary>
/// A gameplay ability replicated state change observed by the server.
/// </summary>
/// <param name="Frame">The frame.</param>
/// <param name="PlayerId">The player id.</param>
/// <param name="ClassId">The player's class.</param>
/// <param name="Team">The player's team.</param>
/// <param name="OwnerId">The state owner id.</param>
/// <param name="StateKey">The state key.</param>
/// <param name="ValueKind">The value kind.</param>
/// <param name="HasPreviousValue">Whether a previous value exists.</param>
/// <param name="PreviousIntValue">The previous integer value.</param>
/// <param name="CurrentIntValue">The current integer value.</param>
/// <param name="PreviousFloatValue">The previous float value.</param>
/// <param name="CurrentFloatValue">The current float value.</param>
/// <param name="PreviousBoolValue">The previous boolean value.</param>
/// <param name="CurrentBoolValue">The current boolean value.</param>
public readonly record struct OpenGarrisonServerGameplayAbilityStateChangedEvent(
    long Frame,
    int PlayerId,
    PlayerClass ClassId,
    PlayerTeam Team,
    string OwnerId,
    string StateKey,
    GameplayReplicatedStateValueKind ValueKind,
    bool HasPreviousValue,
    int PreviousIntValue,
    int CurrentIntValue,
    float PreviousFloatValue,
    float CurrentFloatValue,
    bool PreviousBoolValue,
    bool CurrentBoolValue);

/// <summary>
/// Hooks a server plugin can implement to receive semantic gameplay events.
/// </summary>
public interface IOpenGarrisonServerSemanticGameplayHooks
{
    /// <summary>Called when damage is dealt.</summary>
    /// <param name="e">The damage event.</param>
    void OnDamage(OpenGarrisonServerDamageEvent e) { }

    /// <summary>Called when a player dies.</summary>
    /// <param name="e">The death event.</param>
    void OnDeath(OpenGarrisonServerDeathEvent e) { }

    /// <summary>Called when a kill assist is credited.</summary>
    /// <param name="e">The assist event.</param>
    void OnAssist(OpenGarrisonServerAssistEvent e) { }

    /// <summary>Called when a buildable is built.</summary>
    /// <param name="e">The buildable event.</param>
    void OnBuild(OpenGarrisonServerBuildableEvent e) { }

    /// <summary>Called when a buildable is destroyed.</summary>
    /// <param name="e">The buildable event.</param>
    void OnDestroy(OpenGarrisonServerBuildableEvent e) { }

    /// <summary>Called on intelligence events (pickup, drop, return, capture).</summary>
    /// <param name="e">The intelligence event.</param>
    void OnIntelEvent(OpenGarrisonServerIntelEvent e) { }

    /// <summary>Called when a control point's state changes.</summary>
    /// <param name="e">The control point event.</param>
    void OnControlPointStateChanged(OpenGarrisonServerControlPointStateEvent e) { }

    /// <summary>Called when a player joins.</summary>
    /// <param name="e">The join event.</param>
    void OnPlayerJoined(OpenGarrisonServerPlayerJoinedEvent e) { }

    /// <summary>Called when a player leaves.</summary>
    /// <param name="e">The leave event.</param>
    void OnPlayerLeft(OpenGarrisonServerPlayerLeftEvent e) { }

    /// <summary>Called when a player spawns.</summary>
    /// <param name="e">The spawn event.</param>
    void OnPlayerSpawned(OpenGarrisonServerPlayerSpawnEvent e) { }

    /// <summary>Called when a player respawns.</summary>
    /// <param name="e">The respawn event.</param>
    void OnPlayerRespawned(OpenGarrisonServerPlayerRespawnEvent e) { }

    /// <summary>Called before a gameplay ability input is processed; may be cancelled.</summary>
    /// <param name="e">The ability input event.</param>
    void OnGameplayAbilityInput(OpenGarrisonServerGameplayAbilityInputEvent e) { }

    /// <summary>Called when a gameplay ability is used.</summary>
    /// <param name="e">The ability used event.</param>
    void OnGameplayAbilityUsed(OpenGarrisonServerGameplayAbilityUsedEvent e) { }

    /// <summary>Called when gameplay ability replicated state changes.</summary>
    /// <param name="e">The state changed event.</param>
    void OnGameplayAbilityStateChanged(OpenGarrisonServerGameplayAbilityStateChangedEvent e) { }
}
