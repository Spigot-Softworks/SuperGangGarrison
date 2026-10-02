using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>Owns the configurable match rules and tuning scales that the host applies to a simulation.</summary>
internal sealed class MatchSettingsState(int timeLimitMinutes, int capLimit, int respawnSeconds, int respawnTicks)
{
    public int TimeLimitMinutes { get; set; } = timeLimitMinutes;

    public int CapLimit { get; set; } = capLimit;

    public int RespawnSeconds { get; set; } = respawnSeconds;

    public int RespawnTicks { get; set; } = respawnTicks;

    public float PlayerScale { get; set; } = 1f;

    public float MapScale { get; set; } = 1f;

    public float MovementSpeedScale { get; set; } = 1f;

    public float ProjectileSpeedScale { get; set; } = 1f;

    public float DamageScale { get; set; } = 1f;

    public float GravityScale { get; set; } = 1f;

    public float HorizontalSpeedClampPerTick { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    public float VerticalSpeedClampPerTick { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    public float CaptureSpeedMultiplierPerPlayer { get; set; } = 2f;

    public bool RoundEndFriendlyFireEnabled { get; set; }

    public Dictionary<PlayerClass, int> ClassLimits { get; } = new();
}

/// <summary>Owns the bookkeeping a client keeps while reconciling authoritative snapshots with local prediction.</summary>
internal sealed class ClientSnapshotState
{
    public HashSet<int> SeenEntityIds { get; } = new();

    public List<int> StaleEntityIds { get; } = new();

    public HashSet<ulong> ProcessedGibSpawnEventIds { get; } = new();

    public HashSet<ulong> ProcessedImmediateRocketSpawnEventIds { get; } = new();

    public Dictionary<int, int> PresentedGibDeathCountsByPlayerId { get; } = new();

    public HashSet<int> PredictedProjectileIds { get; } = new();

    public int? AuthoritativeLocalPlayerId { get; set; }

    public HashSet<int> TerminatedProjectileIds { get; } = new();

    public Dictionary<int, long> TerminatedProjectileExpiryFrames { get; } = new();

    public ClientSnapshotStringCache StringCache { get; } = new();

    public List<ScoreboardSpectatorEntry> Spectators { get; } = new();
}

/// <summary>Owns per-map runtime state: damageable zones, map logic activators, and sprite-sheet playback.</summary>
internal sealed class MapRuntimeState
{
    public float[] DamageableZoneHealth { get; set; } = [];

    public PlayerTeam?[] DamageableZoneLastDamagingTeam { get; set; } = [];

    public int[] ForegroundJungleRoomObjectIndices { get; set; } = [];

    public bool[] LogicActivatorStartApplied { get; set; } = [];

    public MapLogicActivatorRuntimeState LogicActivatorRuntimeState { get; set; } = new();

    public ulong LogicControlPointInputSignature { get; set; }

    public long LogicTimersLastFrame { get; set; } = -1;

    public MapLogicActivatorRuntimeState LogicScoreTriggerRuntimeState { get; set; } = new();

    public SpritesheetPlaybackRuntimeState SpritesheetPlaybackRuntimeState { get; set; } = new();

    public bool EndMatchOnRedTeamIntelCapture { get; set; }
}

/// <summary>Owns transient combat bookkeeping: queued bursts, danger-close explosions, spread indices, and hit-bounds caches.</summary>
internal sealed class CombatRuntimeState
{
    public int RageEnemyHumiliationTicksRemaining { get; set; }

    public List<QueuedExperimentalRocketBurst> QueuedRocketBursts { get; } = new();

    public Queue<DangerCloseExplosionRequest> PendingDangerCloseExplosions { get; } = new();

    public bool ProcessingDangerCloseExplosions { get; set; }

    public Dictionary<int, int> SpreadShotIndexByPlayerId { get; } = new();

    public int LastAfterburnAlertSourceFrame { get; set; } = -1;

    public CivvieMoneyTrailTracker CivvieMoneyTrailTracker { get; } = new();

    public Dictionary<int, PresentationHitBoundsCacheEntry> PresentationHitBoundsCache { get; } = new();

    public long PresentationHitBoundsCacheFrame { get; set; } = long.MinValue;
}

/// <summary>Owns the local player's input, class selection, and join state.</summary>
internal sealed class LocalSimulationState
{
    public PlayerInputSnapshot Input { get; set; }

    public PlayerInputSnapshot PreviousInput { get; set; }

    public CharacterClassDefinition PlayerClassDefinition { get; set; } = CharacterClassCatalog.Scout;

    public CharacterClassDefinition FriendlyDummyClassDefinition { get; } = CharacterClassCatalog.Heavy;

    public bool PlayerAwaitingJoin { get; set; }
}

/// <summary>Owns spawn rotation and pending map-change state.</summary>
internal sealed class MatchLifecycleState
{
    public int NextRedSpawnIndex { get; set; }

    public int NextBlueSpawnIndex { get; set; }

    public int PendingMapChangeTicks { get; set; } = -1;

    public bool MapChangeReady { get; set; }

    public bool AutoRestartOnMapChange { get; set; } = true;
}

/// <summary>Owns the deterministic random streams used by gameplay rules and presentation choices.</summary>
internal sealed class SimulationRandomStreams
{
    public Random Gameplay { get; } = new(1337);

    public Random DeathCamPhrase { get; } = new(0x474732);

    public Random BotAwareness { get; } = new(0xB07A);
}

internal readonly record struct QueuedExperimentalRocketBurst(
    int TicksRemaining,
    int OwnerId,
    float X,
    float Y,
    float Speed,
    float DirectionRadians,
    RocketCombatDefinition? RocketCombat,
    float DirectHitHealAmount,
    bool CanGrantExperimentalInstantReloadOnHit,
    float KnockbackScale,
    bool CanIgniteTargets,
    bool EnableStingerTracking,
    string? KillFeedWeaponSpriteNameOverride);

internal readonly record struct DangerCloseExplosionRequest(float CenterX, float CenterY, int OwnerPlayerId);

internal readonly record struct PresentationHitBoundsCacheKey(
    float X,
    float Y,
    float HorizontalSpeed,
    float VerticalSpeed,
    float PlayerScale,
    PlayerClass ClassId,
    string GameplayClassId,
    PlayerTeam Team,
    bool IsAlive,
    bool IsGrounded,
    bool IsHeavyEating,
    bool IsTaunting,
    bool IsSniperScoped,
    bool IsSourceFacingLeft,
    bool IsCarryingIntel,
    bool IsHumiliated);

internal readonly record struct PresentationHitBoundsCacheEntry(
    PresentationHitBoundsCacheKey Key,
    float Left,
    float Top,
    float Right,
    float Bottom);