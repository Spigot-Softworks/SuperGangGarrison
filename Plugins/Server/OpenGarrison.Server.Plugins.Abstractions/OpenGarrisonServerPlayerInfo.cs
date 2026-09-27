using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Summary of the current match state.
/// </summary>
/// <param name="ServerName">The server name.</param>
/// <param name="LevelName">The level name.</param>
/// <param name="MapAreaIndex">The current map area index.</param>
/// <param name="MapAreaCount">The number of map areas.</param>
/// <param name="MapScale">The map scale.</param>
/// <param name="GameMode">The game mode.</param>
/// <param name="MatchPhase">The match phase.</param>
/// <param name="RedCaps">The red team caps.</param>
/// <param name="BlueCaps">The blue team caps.</param>
/// <param name="PlayerCount">The total player count.</param>
/// <param name="ActivePlayerCount">The active (non-spectator) player count.</param>
/// <param name="SpectatorCount">The spectator count.</param>
public readonly record struct OpenGarrisonServerMatchStateInfo(
    string ServerName,
    string LevelName,
    int MapAreaIndex,
    int MapAreaCount,
    float MapScale,
    GameModeKind GameMode,
    MatchPhase MatchPhase,
    int RedCaps,
    int BlueCaps,
    int PlayerCount,
    int ActivePlayerCount,
    int SpectatorCount);

/// <summary>
/// Snapshot of a player's state on the server.
/// </summary>
/// <param name="Slot">The player's slot.</param>
/// <param name="UserId">The player's user id.</param>
/// <param name="Name">The player's name.</param>
/// <param name="IsSpectator">Whether the player is spectating.</param>
/// <param name="IsAuthorized">Whether the player is authorized.</param>
/// <param name="IsGagged">Whether the player is gagged.</param>
/// <param name="IsAlive">Whether the player is alive.</param>
/// <param name="PlayerId">The player id, if assigned.</param>
/// <param name="Team">The player's team, if any.</param>
/// <param name="PlayerClass">The player's class, if any.</param>
/// <param name="PlayerScale">The player scale.</param>
/// <param name="EndPoint">The player's network endpoint.</param>
/// <param name="GameplayLoadoutId">The gameplay loadout id.</param>
/// <param name="GameplaySecondaryItemId">The gameplay secondary item id.</param>
/// <param name="GameplayAcquiredItemId">The gameplay acquired item id.</param>
/// <param name="GameplayEquippedSlot">The equipped gameplay slot.</param>
/// <param name="GameplayEquippedItemId">The equipped gameplay item id.</param>
/// <param name="MovementSpeedScale">The movement speed scale.</param>
/// <param name="HasMovementSpeedScaleOverride">Whether a movement speed scale override is active.</param>
/// <param name="GravityScale">The gravity scale.</param>
/// <param name="HasGravityScaleOverride">Whether a gravity scale override is active.</param>
/// <param name="WorldX">The world X, if known.</param>
/// <param name="WorldY">The world Y, if known.</param>
/// <param name="HorizontalSpeed">The horizontal speed, if known.</param>
/// <param name="VerticalSpeed">The vertical speed, if known.</param>
/// <param name="Health">The health, if known.</param>
/// <param name="MaxHealth">The maximum health, if known.</param>
/// <param name="CurrentAmmo">The current ammo, if known.</param>
/// <param name="MaxAmmo">The maximum ammo, if known.</param>
/// <param name="Kills">The kill count, if known.</param>
/// <param name="Deaths">The death count, if known.</param>
/// <param name="Assists">The assist count, if known.</param>
/// <param name="Caps">The cap count, if known.</param>
/// <param name="Points">The points, if known.</param>
/// <param name="IsCarryingIntel">Whether the player is carrying the intelligence.</param>
/// <param name="IsInSpawnRoom">Whether the player is in a spawn room.</param>
public readonly record struct OpenGarrisonServerPlayerInfo(
    byte Slot,
    int UserId,
    string Name,
    bool IsSpectator,
    bool IsAuthorized,
    bool IsGagged,
    bool IsAlive,
    int? PlayerId,
    PlayerTeam? Team,
    PlayerClass? PlayerClass,
    float PlayerScale,
    string EndPoint,
    string GameplayLoadoutId,
    string GameplaySecondaryItemId,
    string GameplayAcquiredItemId,
    GameplayEquipmentSlot GameplayEquippedSlot,
    string GameplayEquippedItemId,
    float MovementSpeedScale = 1f,
    bool HasMovementSpeedScaleOverride = false,
    float GravityScale = 1f,
    bool HasGravityScaleOverride = false,
    float? WorldX = null,
    float? WorldY = null,
    float? HorizontalSpeed = null,
    float? VerticalSpeed = null,
    int? Health = null,
    int? MaxHealth = null,
    int? CurrentAmmo = null,
    int? MaxAmmo = null,
    int? Kills = null,
    int? Deaths = null,
    int? Assists = null,
    int? Caps = null,
    float? Points = null,
    bool IsCarryingIntel = false,
    bool IsInSpawnRoom = false);

/// <summary>
/// Snapshot of a control point's state.
/// </summary>
/// <param name="Index">The control point index.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Team">The owning team, if any.</param>
/// <param name="CappingTeam">The capping team, if any.</param>
/// <param name="CappingTicks">The capping progress in ticks.</param>
/// <param name="CapTimeTicks">The cap time in ticks.</param>
/// <param name="RedCappers">The red capper count.</param>
/// <param name="BlueCappers">The blue capper count.</param>
/// <param name="Cappers">The total capper count.</param>
/// <param name="IsLocked">Whether the point is locked.</param>
/// <param name="HasHealingAura">Whether the point has a healing aura.</param>
public readonly record struct OpenGarrisonServerControlPointInfo(
    int Index,
    float WorldX,
    float WorldY,
    float Width,
    float Height,
    PlayerTeam? Team,
    PlayerTeam? CappingTeam,
    float CappingTicks,
    int CapTimeTicks,
    int RedCappers,
    int BlueCappers,
    int Cappers,
    bool IsLocked,
    bool HasHealingAura);

/// <summary>
/// Snapshot of a generator's state.
/// </summary>
/// <param name="Team">The generator's team.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Health">The current health.</param>
/// <param name="MaxHealth">The maximum health.</param>
/// <param name="IsDestroyed">Whether the generator is destroyed.</param>
/// <param name="HealthFraction">The health fraction (0 to 1).</param>
/// <param name="DamageStage">The damage stage.</param>
public readonly record struct OpenGarrisonServerGeneratorInfo(
    PlayerTeam Team,
    float WorldX,
    float WorldY,
    float Width,
    float Height,
    int Health,
    int MaxHealth,
    bool IsDestroyed,
    float HealthFraction,
    int DamageStage);

/// <summary>
/// Snapshot of an intelligence's state.
/// </summary>
/// <param name="Team">The owning team.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
/// <param name="HomeX">The home X.</param>
/// <param name="HomeY">The home Y.</param>
/// <param name="IsAtBase">Whether the intelligence is at base.</param>
/// <param name="IsDropped">Whether the intelligence is dropped.</param>
/// <param name="IsCarried">Whether the intelligence is carried.</param>
/// <param name="ReturnTicksRemaining">The ticks remaining until return.</param>
public readonly record struct OpenGarrisonServerIntelligenceInfo(
    PlayerTeam Team,
    float WorldX,
    float WorldY,
    float HomeX,
    float HomeY,
    bool IsAtBase,
    bool IsDropped,
    bool IsCarried,
    int ReturnTicksRemaining);

/// <summary>
/// Snapshot of the objective state (control points, generators, intelligence).
/// </summary>
/// <param name="ControlPoints">The control points.</param>
/// <param name="Generators">The generators.</param>
/// <param name="Intelligence">The intelligence items.</param>
public readonly record struct OpenGarrisonServerObjectiveStateInfo(
    IReadOnlyList<OpenGarrisonServerControlPointInfo> ControlPoints,
    IReadOnlyList<OpenGarrisonServerGeneratorInfo> Generators,
    IReadOnlyList<OpenGarrisonServerIntelligenceInfo> Intelligence);

/// <summary>
/// Snapshot of a buildable's state.
/// </summary>
/// <param name="Kind">The buildable kind.</param>
/// <param name="Id">The buildable id.</param>
/// <param name="OwnerPlayerId">The owning player id.</param>
/// <param name="Team">The buildable's team.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
/// <param name="Health">The current health.</param>
/// <param name="MaxHealth">The maximum health.</param>
/// <param name="IsBuilt">Whether the buildable is built.</param>
/// <param name="IsDead">Whether the buildable is dead.</param>
/// <param name="HasLanded">Whether the buildable has landed.</param>
/// <param name="HasActiveTarget">Whether the buildable has an active target.</param>
public readonly record struct OpenGarrisonServerBuildableInfo(
    string Kind,
    int Id,
    int OwnerPlayerId,
    PlayerTeam Team,
    float WorldX,
    float WorldY,
    int Health,
    int MaxHealth,
    bool IsBuilt,
    bool IsDead,
    bool HasLanded,
    bool HasActiveTarget);

/// <summary>
/// Snapshot of a projectile's state.
/// </summary>
/// <param name="Kind">The projectile kind.</param>
/// <param name="Id">The projectile id.</param>
/// <param name="OwnerPlayerId">The owning player id.</param>
/// <param name="Team">The projectile's team.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
/// <param name="PreviousWorldX">The previous world X.</param>
/// <param name="PreviousWorldY">The previous world Y.</param>
/// <param name="VelocityX">The horizontal velocity, if known.</param>
/// <param name="VelocityY">The vertical velocity, if known.</param>
/// <param name="DirectionRadians">The direction in radians, if known.</param>
/// <param name="Speed">The speed, if known.</param>
/// <param name="TicksRemaining">The ticks remaining, if known.</param>
/// <param name="IsCritical">Whether the projectile is critical.</param>
/// <param name="IsDestroyed">Whether the projectile is destroyed.</param>
public readonly record struct OpenGarrisonServerProjectileInfo(
    string Kind,
    int Id,
    int OwnerPlayerId,
    PlayerTeam Team,
    float WorldX,
    float WorldY,
    float PreviousWorldX,
    float PreviousWorldY,
    float? VelocityX,
    float? VelocityY,
    float? DirectionRadians,
    float? Speed,
    int? TicksRemaining,
    bool IsCritical,
    bool IsDestroyed);

/// <summary>
/// Snapshot of a recent gameplay event.
/// </summary>
/// <param name="Kind">The event kind.</param>
/// <param name="Name">The event name.</param>
/// <param name="EventId">The event id.</param>
/// <param name="SourceFrame">The source frame.</param>
/// <param name="WorldX">The world X, if known.</param>
/// <param name="WorldY">The world Y, if known.</param>
/// <param name="Amount">The amount, if any.</param>
/// <param name="TargetEntityId">The target entity id, if any.</param>
/// <param name="TargetPlayerId">The target player id, if any.</param>
/// <param name="AttackerPlayerId">The attacking player id, if any.</param>
/// <param name="WasFatal">Whether the event was fatal.</param>
public readonly record struct OpenGarrisonServerRecentEventInfo(
    string Kind,
    string Name,
    ulong EventId,
    ulong SourceFrame,
    float? WorldX,
    float? WorldY,
    int? Amount,
    int? TargetEntityId,
    int? TargetPlayerId,
    int? AttackerPlayerId,
    bool WasFatal);

/// <summary>
/// The bounds of a map region.
/// </summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct OpenGarrisonServerMapBoundsInfo(
    float Width,
    float Height);

/// <summary>
/// A solid in a map region.
/// </summary>
/// <param name="Index">The solid index.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct OpenGarrisonServerMapSolidInfo(
    int Index,
    float WorldX,
    float WorldY,
    float Width,
    float Height);

/// <summary>
/// A room object in a map region.
/// </summary>
/// <param name="Index">The object index.</param>
/// <param name="Kind">The object kind.</param>
/// <param name="WorldX">The world X.</param>
/// <param name="WorldY">The world Y.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Team">The owning team, if any.</param>
/// <param name="SourceName">The source name.</param>
/// <param name="Value">The object value.</param>
public readonly record struct OpenGarrisonServerMapRoomObjectInfo(
    int Index,
    string Kind,
    float WorldX,
    float WorldY,
    float Width,
    float Height,
    PlayerTeam? Team,
    string SourceName,
    float Value);

/// <summary>
/// Map geometry around a center point.
/// </summary>
/// <param name="Bounds">The map bounds.</param>
/// <param name="CenterX">The region center X.</param>
/// <param name="CenterY">The region center Y.</param>
/// <param name="Radius">The region radius.</param>
/// <param name="Solids">The solids in the region.</param>
/// <param name="RoomObjects">The room objects in the region.</param>
/// <param name="IsTruncated">Whether the region was truncated by the entry limit.</param>
public readonly record struct OpenGarrisonServerMapRegionInfo(
    OpenGarrisonServerMapBoundsInfo Bounds,
    float CenterX,
    float CenterY,
    float Radius,
    IReadOnlyList<OpenGarrisonServerMapSolidInfo> Solids,
    IReadOnlyList<OpenGarrisonServerMapRoomObjectInfo> RoomObjects,
    bool IsTruncated);

/// <summary>
/// The result of a line-of-sight test.
/// </summary>
/// <param name="OriginX">The origin X.</param>
/// <param name="OriginY">The origin Y.</param>
/// <param name="TargetX">The target X.</param>
/// <param name="TargetY">The target Y.</param>
/// <param name="Team">The viewing team, if any.</param>
/// <param name="HasLineOfSight">Whether there is line of sight.</param>
public readonly record struct OpenGarrisonServerVisibilityInfo(
    float OriginX,
    float OriginY,
    float TargetX,
    float TargetY,
    PlayerTeam? Team,
    bool HasLineOfSight);

/// <summary>
/// Snapshot of a gameplay loadout.
/// </summary>
/// <param name="LoadoutId">The loadout id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="PrimaryItemId">The primary item id.</param>
/// <param name="SecondaryItemId">The secondary item id, if any.</param>
/// <param name="UtilityItemId">The utility item id, if any.</param>
/// <param name="IsSelected">Whether the loadout is selected.</param>
/// <param name="IsAvailableToPlayer">Whether the loadout is available to the player.</param>
public readonly record struct OpenGarrisonServerGameplayLoadoutInfo(
    string LoadoutId,
    string DisplayName,
    string PrimaryItemId,
    string? SecondaryItemId,
    string? UtilityItemId,
    bool IsSelected,
    bool IsAvailableToPlayer);

/// <summary>
/// Snapshot of a gameplay mod pack.
/// </summary>
/// <param name="ModPackId">The mod pack id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Version">The version.</param>
/// <param name="ItemCount">The item count.</param>
/// <param name="ClassCount">The class count.</param>
/// <param name="IsBoundToPlayableClasses">Whether the pack is bound to playable classes.</param>
public readonly record struct OpenGarrisonServerGameplayModPackInfo(
    string ModPackId,
    string DisplayName,
    string Version,
    int ItemCount,
    int ClassCount,
    bool IsBoundToPlayableClasses);

/// <summary>
/// Snapshot of a gameplay class.
/// </summary>
/// <param name="ModPackId">The mod pack id.</param>
/// <param name="ClassId">The class id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="DefaultLoadoutId">The default loadout id.</param>
/// <param name="LoadoutCount">The loadout count.</param>
public readonly record struct OpenGarrisonServerGameplayClassInfo(
    string ModPackId,
    string ClassId,
    string DisplayName,
    string DefaultLoadoutId,
    int LoadoutCount);

/// <summary>
/// Snapshot of a gameplay item.
/// </summary>
/// <param name="ModPackId">The mod pack id.</param>
/// <param name="ItemId">The item id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Slot">The equipment slot.</param>
/// <param name="BehaviorId">The behavior id.</param>
/// <param name="TracksOwnership">Whether ownership is tracked.</param>
/// <param name="DefaultGranted">Whether the item is granted by default.</param>
/// <param name="GrantOnAcquire">Whether the item is granted on acquire.</param>
/// <param name="GrantKey">The grant key, if any.</param>
public readonly record struct OpenGarrisonServerGameplayItemInfo(
    string ModPackId,
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    bool TracksOwnership,
    bool DefaultGranted,
    bool GrantOnAcquire,
    string? GrantKey);

/// <summary>
/// Snapshot of a gameplay ability.
/// </summary>
/// <param name="ModPackId">The mod pack id.</param>
/// <param name="ItemId">The item id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Slot">The equipment slot.</param>
/// <param name="BehaviorId">The behavior id.</param>
/// <param name="Category">The ability category.</param>
/// <param name="Activation">The activation kind.</param>
/// <param name="ExecutorId">The executor id.</param>
/// <param name="Tags">The ability tags.</param>
/// <param name="Parameters">The ability parameters.</param>
public readonly record struct OpenGarrisonServerGameplayAbilityInfo(
    string ModPackId,
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    string Category,
    string Activation,
    string ExecutorId,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// A selectable gameplay item available to a player.
/// </summary>
/// <param name="ItemId">The item id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Slot">The equipment slot.</param>
/// <param name="BehaviorId">The behavior id.</param>
/// <param name="IsCurrentlySelected">Whether the item is currently selected.</param>
/// <param name="IsOwnedByPlayer">Whether the item is owned by the player.</param>
/// <param name="IsOwnershipTracked">Whether ownership is tracked.</param>
/// <param name="IsDefaultGranted">Whether the item is granted by default.</param>
/// <param name="IsGrantOnAcquire">Whether the item is granted on acquire.</param>
/// <param name="GrantKey">The grant key, if any.</param>
public readonly record struct OpenGarrisonServerGameplaySelectableItemInfo(
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    bool IsCurrentlySelected,
    bool IsOwnedByPlayer,
    bool IsOwnershipTracked,
    bool IsDefaultGranted,
    bool IsGrantOnAcquire,
    string? GrantKey);
