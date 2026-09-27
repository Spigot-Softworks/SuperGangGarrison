using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// The kind of item picked up in a pickup event.
/// </summary>
public enum ClientGameplayPickupKind : byte
{
    /// <summary>Unknown pickup kind.</summary>
    Unknown = 0,
    /// <summary>The intelligence was picked up.</summary>
    Intel = 1,
}

/// <summary>
/// The kind of objective in an objective event.
/// </summary>
public enum ClientObjectiveEventKind : byte
{
    /// <summary>Unknown objective kind.</summary>
    Unknown = 0,
    /// <summary>An intelligence objective.</summary>
    Intel = 1,
    /// <summary>A control point objective.</summary>
    ControlPoint = 2,
    /// <summary>A generator objective.</summary>
    Generator = 3,
}

/// <summary>
/// The phase of a round.
/// </summary>
public enum ClientRoundPhase : byte
{
    /// <summary>Unknown phase.</summary>
    Unknown = 0,
    /// <summary>The round is running.</summary>
    Running = 1,
    /// <summary>The round has ended.</summary>
    Ended = 2,
}

/// <summary>
/// A shot-fired event observed by the client.
/// </summary>
/// <param name="PlayerId">The shooting player id, if known.</param>
/// <param name="PlayerClass">The shooting player's class.</param>
/// <param name="WorldPosition">The world position of the shot.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientShotFiredEvent(
    int? PlayerId,
    ClientPluginClass PlayerClass,
    Vector2 WorldPosition,
    ulong WorldFrame);

/// <summary>
/// A hit-confirmed event observed by the client.
/// </summary>
/// <param name="Amount">The damage amount.</param>
/// <param name="TargetKind">The kind of entity hit.</param>
/// <param name="TargetEntityId">The hit entity id.</param>
/// <param name="TargetWorldPosition">The target's world position.</param>
/// <param name="WasFatal">Whether the hit was fatal.</param>
/// <param name="AttackerPlayerId">The attacking player id.</param>
/// <param name="AssistedByPlayerId">The assisting player id.</param>
/// <param name="Flags">Additional damage flags.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientHitConfirmedEvent(
    int Amount,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    Vector2 TargetWorldPosition,
    bool WasFatal,
    int AttackerPlayerId,
    int AssistedByPlayerId,
    LocalDamageFlags Flags,
    ulong WorldFrame);

/// <summary>
/// A local-player kill event observed by the client.
/// </summary>
/// <param name="VictimPlayerId">The victim player id.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim's team.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
/// <param name="MessageText">The kill message text.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientLocalKillEvent(
    int VictimPlayerId,
    string VictimName,
    ClientPluginTeam VictimTeam,
    string WeaponSpriteName,
    string MessageText,
    ulong WorldFrame);

/// <summary>
/// A local-player death event observed by the client.
/// </summary>
/// <param name="KillerPlayerId">The killer player id.</param>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer's team.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
/// <param name="MessageText">The death message text.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientLocalDeathEvent(
    int KillerPlayerId,
    string KillerName,
    ClientPluginTeam KillerTeam,
    string WeaponSpriteName,
    string MessageText,
    ulong WorldFrame);

/// <summary>
/// An item pickup event observed by the client.
/// </summary>
/// <param name="Kind">The pickup kind.</param>
/// <param name="WorldPosition">The world position of the pickup.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientPickupEvent(
    ClientGameplayPickupKind Kind,
    Vector2 WorldPosition,
    ulong WorldFrame);

/// <summary>
/// A heal event observed by the client.
/// </summary>
/// <param name="Amount">The healed amount.</param>
/// <param name="HealthAfter">The health after healing.</param>
/// <param name="MaxHealth">The maximum health.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientHealEvent(
    int Amount,
    int HealthAfter,
    int MaxHealth,
    ulong WorldFrame);

/// <summary>
/// An ignite event observed by the client.
/// </summary>
/// <param name="BurnedByPlayerId">The player id that caused the burn.</param>
/// <param name="BurnIntensity">The burn intensity.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientIgniteEvent(
    int BurnedByPlayerId,
    float BurnIntensity,
    ulong WorldFrame);

/// <summary>
/// An extinguish event observed by the client.
/// </summary>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientExtinguishEvent(ulong WorldFrame);

/// <summary>
/// An objective state change observed by the client.
/// </summary>
/// <param name="Kind">The objective kind.</param>
/// <param name="ObjectiveId">The objective id.</param>
/// <param name="Team">The team owning the objective.</param>
/// <param name="CappingTeam">The team currently capping, if any.</param>
/// <param name="Progress">The capture progress (0 to 1).</param>
/// <param name="IsLocked">Whether the objective is locked.</param>
/// <param name="WorldPosition">The objective's world position.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientObjectiveStateEvent(
    ClientObjectiveEventKind Kind,
    int ObjectiveId,
    ClientPluginTeam Team,
    ClientPluginTeam CappingTeam,
    float Progress,
    bool IsLocked,
    Vector2 WorldPosition,
    ulong WorldFrame);

/// <summary>
/// An intelligence state change observed by the client.
/// </summary>
/// <param name="IntelTeam">The team owning the intelligence.</param>
/// <param name="CarrierTeam">The team carrying the intelligence.</param>
/// <param name="IsAtBase">Whether the intelligence is at base.</param>
/// <param name="IsDropped">Whether the intelligence is dropped.</param>
/// <param name="ReturnProgress">The return progress (0 to 1).</param>
/// <param name="WorldPosition">The intelligence's world position.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientIntelStateEvent(
    ClientPluginTeam IntelTeam,
    ClientPluginTeam CarrierTeam,
    bool IsAtBase,
    bool IsDropped,
    float ReturnProgress,
    Vector2 WorldPosition,
    ulong WorldFrame);

/// <summary>
/// A generator state change observed by the client.
/// </summary>
/// <param name="Team">The generator's team.</param>
/// <param name="Health">The generator's current health.</param>
/// <param name="MaxHealth">The generator's maximum health.</param>
/// <param name="IsDestroyed">Whether the generator is destroyed.</param>
/// <param name="WorldPosition">The generator's world position.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientGeneratorStateEvent(
    ClientPluginTeam Team,
    int Health,
    int MaxHealth,
    bool IsDestroyed,
    Vector2 WorldPosition,
    ulong WorldFrame);

/// <summary>
/// A round phase change observed by the client.
/// </summary>
/// <param name="PreviousPhase">The previous phase.</param>
/// <param name="CurrentPhase">The current phase.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientRoundPhaseChangedEvent(
    ClientRoundPhase PreviousPhase,
    ClientRoundPhase CurrentPhase,
    ulong WorldFrame);

/// <summary>
/// A kill feed entry observed by the client.
/// </summary>
/// <param name="KillerPlayerId">The killer player id.</param>
/// <param name="KillerName">The killer name.</param>
/// <param name="KillerTeam">The killer's team.</param>
/// <param name="VictimPlayerId">The victim player id.</param>
/// <param name="VictimName">The victim name.</param>
/// <param name="VictimTeam">The victim's team.</param>
/// <param name="WeaponSpriteName">The weapon sprite name.</param>
/// <param name="MessageText">The kill message text.</param>
/// <param name="WorldFrame">The world frame.</param>
public readonly record struct ClientKillFeedEvent(
    int KillerPlayerId,
    string KillerName,
    ClientPluginTeam KillerTeam,
    int VictimPlayerId,
    string VictimName,
    ClientPluginTeam VictimTeam,
    string WeaponSpriteName,
    string MessageText,
    ulong WorldFrame);

/// <summary>
/// Hooks a client plugin can implement to receive semantic gameplay events.
/// </summary>
public interface IOpenGarrisonClientSemanticGameplayHooks
{
    /// <summary>Called when a shot is fired.</summary>
    /// <param name="e">The shot-fired event.</param>
    void OnShotFired(ClientShotFiredEvent e) { }

    /// <summary>Called when a hit is confirmed.</summary>
    /// <param name="e">The hit-confirmed event.</param>
    void OnHitConfirmed(ClientHitConfirmedEvent e) { }

    /// <summary>Called when the local player gets a kill.</summary>
    /// <param name="e">The kill event.</param>
    void OnLocalKill(ClientLocalKillEvent e) { }

    /// <summary>Called when the local player dies.</summary>
    /// <param name="e">The death event.</param>
    void OnLocalDeath(ClientLocalDeathEvent e) { }

    /// <summary>Called when an item is picked up.</summary>
    /// <param name="e">The pickup event.</param>
    void OnPickup(ClientPickupEvent e) { }

    /// <summary>Called when healing is received.</summary>
    /// <param name="e">The heal event.</param>
    void OnHeal(ClientHealEvent e) { }

    /// <summary>Called when the local player is ignited.</summary>
    /// <param name="e">The ignite event.</param>
    void OnIgnited(ClientIgniteEvent e) { }

    /// <summary>Called when the local player is extinguished.</summary>
    /// <param name="e">The extinguish event.</param>
    void OnExtinguished(ClientExtinguishEvent e) { }

    /// <summary>Called when an objective's state changes.</summary>
    /// <param name="e">The objective state event.</param>
    void OnObjectiveStateChanged(ClientObjectiveStateEvent e) { }

    /// <summary>Called when the intelligence state changes.</summary>
    /// <param name="e">The intelligence state event.</param>
    void OnIntelStateChanged(ClientIntelStateEvent e) { }

    /// <summary>Called when a generator's state changes.</summary>
    /// <param name="e">The generator state event.</param>
    void OnGeneratorStateChanged(ClientGeneratorStateEvent e) { }

    /// <summary>Called when the round phase changes.</summary>
    /// <param name="e">The round phase event.</param>
    void OnRoundPhaseChanged(ClientRoundPhaseChangedEvent e) { }

    /// <summary>Called when a kill feed entry appears.</summary>
    /// <param name="e">The kill feed event.</param>
    void OnKillFeed(ClientKillFeedEvent e) { }
}
