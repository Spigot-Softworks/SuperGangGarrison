using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

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

public readonly record struct OpenGarrisonServerObjectiveStateInfo(
    IReadOnlyList<OpenGarrisonServerControlPointInfo> ControlPoints,
    IReadOnlyList<OpenGarrisonServerGeneratorInfo> Generators,
    IReadOnlyList<OpenGarrisonServerIntelligenceInfo> Intelligence);

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

public readonly record struct OpenGarrisonServerMapBoundsInfo(
    float Width,
    float Height);

public readonly record struct OpenGarrisonServerMapSolidInfo(
    int Index,
    float WorldX,
    float WorldY,
    float Width,
    float Height);

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

public readonly record struct OpenGarrisonServerMapRegionInfo(
    OpenGarrisonServerMapBoundsInfo Bounds,
    float CenterX,
    float CenterY,
    float Radius,
    IReadOnlyList<OpenGarrisonServerMapSolidInfo> Solids,
    IReadOnlyList<OpenGarrisonServerMapRoomObjectInfo> RoomObjects,
    bool IsTruncated);

public readonly record struct OpenGarrisonServerVisibilityInfo(
    float OriginX,
    float OriginY,
    float TargetX,
    float TargetY,
    PlayerTeam? Team,
    bool HasLineOfSight);

public readonly record struct OpenGarrisonServerGameplayLoadoutInfo(
    string LoadoutId,
    string DisplayName,
    string PrimaryItemId,
    string? SecondaryItemId,
    string? UtilityItemId,
    bool IsSelected,
    bool IsAvailableToPlayer);

public readonly record struct OpenGarrisonServerGameplayModPackInfo(
    string ModPackId,
    string DisplayName,
    string Version,
    int ItemCount,
    int ClassCount,
    bool IsBoundToPlayableClasses);

public readonly record struct OpenGarrisonServerGameplayClassInfo(
    string ModPackId,
    string ClassId,
    string DisplayName,
    string DefaultLoadoutId,
    int LoadoutCount);

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
