using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public delegate void ProjectilePresentationHitBoundsProvider(
    PlayerEntity player,
    out float left,
    out float top,
    out float right,
    out float bottom);

public delegate bool ProjectileGrenadeDamageableZoneContactProvider(
    GrenadeProjectileEntity grenade,
    float directionX,
    float directionY,
    float maxDistance,
    out float hitX,
    out float hitY,
    out int roomObjectIndex);

public readonly record struct ShotHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)
{
    public JumpPadEntity? HitJumpPad { get; init; }
    public int HitDamageableZoneRoomObjectIndex { get; init; } = -1;
    public bool IsLastToDieHeadshot { get; init; }
}

public readonly record struct FlameHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)
{
    public JumpPadEntity? HitJumpPad { get; init; }
}

public readonly record struct RocketHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)
{
    public JumpPadEntity? HitJumpPad { get; init; }
    public int HitDamageableZoneRoomObjectIndex { get; init; } = -1;
}

public readonly record struct MineHitResult(float Distance, float HitX, float HitY, bool DestroyOnHit);
public readonly record struct GrenadeEnvironmentHit(float Distance, float HitX, float HitY, float NormalX, float NormalY);
public readonly record struct RectangleHitbox(float Left, float Top, float Right, float Bottom);

internal readonly record struct RifleHitResult(float Distance, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)
{
    public JumpPadEntity? HitJumpPad { get; init; }
}

internal readonly record struct OrderedRiflePlayerHit(float Distance, PlayerEntity Player, bool IsFriendlySupport, bool IsLastToDieHeadshot = false);
internal readonly record struct OrderedRifleHitResult(float Distance, IReadOnlyList<OrderedRiflePlayerHit> PlayerHits, SentryEntity? HitSentry, GeneratorState? HitGenerator)
{
    public JumpPadEntity? HitJumpPad { get; init; }
}

internal readonly record struct RifleTracePolicy(bool IgnoreOrdinaryGeometry, bool AllowFriendlySupport, int MaximumEnemyPlayerHits, bool DetectLastToDieHeadshots = false, bool PierceFriendlyPlayers = false);

/// <summary>
/// World services used by <see cref="ProjectileSystem"/>. The system owns
/// projectile state and movement; the coordinator supplies only the queries
/// and presentation/game-mode consequences that are not projectile state.
/// </summary>
public sealed record ProjectileSystemDependencies
{
    public Func<int> AllocateEntityId { get; init; } = static () => 0;
    public Func<SimpleLevel> GetLevel { get; init; } = static () => SimpleLevelFactory.CreateScoutPrototypeLevel(1f);
    public SimulationConfig Config { get; init; } = new();
    public Func<long> CurrentFrame { get; init; } = static () => 0L;
    public Func<float> GetGravityScale { get; init; } = static () => 1f;
    public Func<bool> IsClientPredictionMode { get; init; } = static () => false;
    public Func<int?> GetAuthoritativeLocalPlayerId { get; init; } = static () => null;
    public Func<int> GetLocalPlayerId { get; init; } = static () => 0;
    public int LocalProjectileTerminationSuppressionTicks { get; init; } = 12;
    public HashSet<int> ClientPredictedProjectileIds { get; init; } = new();
    public Action<int, int> SuppressProjectileRespawn { get; init; } = static (_, _) => { };
    public Func<IEnumerable<PlayerEntity>> EnumerateSimulatedPlayers { get; init; } = static () => [];
    public Func<int, PlayerEntity?> FindPlayerById { get; init; } = static _ => null;
    public Func<PlayerTeam, int, PlayerEntity, bool> CanTeamDamagePlayer { get; init; } =
        static (team, attackerId, target) => target.IsAlive && (attackerId == target.Id || team != target.Team);
    public Func<int, int> GetSimulationTicksFromSourceTicks { get; init; } = static ticks => Math.Max(0, (int)MathF.Round(ticks));
    public Func<int, int> NextRandomInt { get; init; } = static _ => 0;
    public Func<float> NextRandomSingle { get; init; } = static () => 0f;

    public IReadOnlyList<SentryEntity> Sentries { get; init; } = [];
    public IReadOnlyList<JumpPadEntity> JumpPads { get; init; } = [];
    public IReadOnlyList<GeneratorState> Generators { get; init; } = [];

    public Action<int> MarkProjectileTerminated { get; init; } = static _ => { };
    public Action<float, float, float, float, float, bool, PlayerTeam, bool, bool> RegisterCombatTrace { get; init; } = static (_, _, _, _, _, _, _, _, _) => { };
    public Action<float, float, float, int> RegisterBloodEffect { get; init; } = static (_, _, _, _) => { };
    public Action<string, float, float, float, int, bool> RegisterVisualEffect { get; init; } = static (_, _, _, _, _, _) => { };
    public Action<string, float, float, int> RegisterWorldSoundEvent { get; init; } = static (_, _, _, _) => { };
    public Action<float, float, float> RegisterImpactEffect { get; init; } = static (_, _, _) => { };
    public Action<float, float, float, float, ArrowProjectileEntity> RegisterStuckArrowEffect { get; init; } = static (_, _, _, _, _) => { };

    public Func<ShotProjectileEntity, float, float, float, ShotHitResult?> GetNearestShotHit { get; init; } = static (_, _, _, _) => null;
    public Func<NeedleProjectileEntity, float, float, float, ShotHitResult?> GetNearestNeedleHit { get; init; } = static (_, _, _, _) => null;
    public Func<MedicHealNeedleProjectileEntity, float, float, float, ShotHitResult?> GetNearestMedicHealNeedleHit { get; init; } = static (_, _, _, _) => null;
    public Func<RevolverProjectileEntity, float, float, float, ShotHitResult?> GetNearestRevolverHit { get; init; } = static (_, _, _, _) => null;
    public Func<BladeProjectileEntity, float, float, float, ShotHitResult?> GetNearestBladeHit { get; init; } = static (_, _, _, _) => null;
    public Func<StabMaskEntity, float, float, ShotHitResult?> GetNearestStabHit { get; init; } = static (_, _, _) => null;
    public Func<StabMaskEntity, float, float, ShotHitResult?> GetNearestHealstabHit { get; init; } = static (_, _, _) => null;
    public Func<float, float, float, float, bool> HasStabChainLineOfSight { get; init; } = static (_, _, _, _) => true;
    public Func<RocketProjectileEntity, float, float, float, RocketHitResult?> GetNearestRocketHit { get; init; } = static (_, _, _, _) => null;
    public Func<MineProjectileEntity, float, float, float, MineHitResult?> GetNearestMineHit { get; init; } = static (_, _, _, _) => null;
    public Func<GrenadeProjectileEntity, float, float, float, GrenadeEnvironmentHit?> GetNearestGrenadeEnvironmentHit { get; init; } = static (_, _, _, _) => null;
    public Func<GrenadeProjectileEntity, float, float, float, PlayerEntity?> GetNearestGrenadePlayerHit { get; init; } = static (_, _, _, _) => null;
    public ProjectileGrenadeDamageableZoneContactProvider TryGetGrenadeDamageableZoneContact { get; init; } = static (_, _, _, _, out hitX, out hitY, out roomObjectIndex) =>
    {
        hitX = 0f;
        hitY = 0f;
        roomObjectIndex = -1;
        return false;
    };
    public Func<FlameProjectileEntity, float, float, float, FlameHitResult?> GetNearestFlameHit { get; init; } = static (_, _, _, _) => null;
    public Func<FlareProjectileEntity, float, float, float, ShotHitResult?> GetNearestFlareHit { get; init; } = static (_, _, _, _) => null;
    public Func<FlareProjectileEntity, float, float, float, bool, ShotHitResult?> GetNearestFlareHitWithPlayerFilter { get; init; } = static (_, _, _, _, _) => null;
    public Func<FlareProjectileEntity, float, float, float, ShotHitResult?, ShotHitResult?> GetNearestFlarePlayerHit { get; init; } = static (_, _, _, _, _) => null;
    public Func<float, float, float, float, PlayerTeam, bool> IsProjectilePathBlocked { get; init; } = static (_, _, _, _, _) => false;
    public ProjectilePresentationHitBoundsProvider GetCachedPlayerPresentationHitBounds { get; init; } = static (player, out left, out top, out right, out bottom) =>
    {
        left = player.Left;
        top = player.Top;
        right = player.Right;
        bottom = player.Bottom;
    };

    public Func<PlayerTeam, float, float, float, float, float, bool> TryInterceptWithCivilDefenseTurret { get; init; } = static (_, _, _, _, _, _) => false;
    public Func<ShotHitResult, float, PlayerTeam, bool> TryHandleProjectileDamageableZoneHit { get; init; } = static (_, _, _) => false;
    public Func<int, float, PlayerTeam?, bool> TryApplyDamageableZoneDamage { get; init; } = static (_, _, _) => false;
    public Func<int, bool> BlocksProjectileDamageableZone { get; init; } = static _ => false;

    public Func<PlayerEntity?, PlayerEntity, int, (int Damage, DamageEventFlags Flags)> ApplyExperimentalAirshotDamageMultiplier { get; init; } = static (_, _, damage) => (damage, DamageEventFlags.None);
    internal Action<SentryEntity, PlayerEntity, PlayerEntity, int, PlayerDamageTraits, bool, bool, float?, float?, BulletKnockbackPayload?, float?, float?> ApplyExperimentalSentryPlayerHit { get; init; } = static (_, _, _, _, _, _, _, _, _, _, _, _) => { };
    public Action<SentryEntity, PlayerEntity, int> ApplyExperimentalSentryDamageRewards { get; init; } = static (_, _, _) => { };

    public Action<PlayerEntity, bool, PlayerEntity?, string?, DeadBodyAnimationKind> KillPlayer { get; init; } = static (_, _, _, _, _) => { };
    public Action<SentryEntity, PlayerEntity?> DestroySentry { get; init; } = static (_, _) => { };
    public Func<PlayerTeam, float, PlayerEntity?, bool> TryDamageGenerator { get; init; } = static (_, _, _) => false;
    public Func<SentryEntity, int, PlayerEntity?, bool> ApplySentryDamage { get; init; } = static (_, _, _) => false;
    public Func<GeneratorState, float, PlayerEntity?, bool> ApplyGeneratorDamage { get; init; } = static (_, _, _) => false;
    public Action<JumpPadEntity, int> ApplyJumpPadDamage { get; init; } = static (_, _) => { };
    public Action<float, float, float, float, PlayerTeam, float> ApplyExplosiveDamageToJumpPads { get; init; } = static (_, _, _, _, _, _) => { };
    public Func<PlayerEntity, float, string?, float, float, int> ApplyHealingWithFeedback { get; init; } = static (_, _, _, _, _) => 0;

    public Action<RocketProjectileEntity, PlayerEntity?, SentryEntity?, GeneratorState?, int> ExplodeRocket { get; init; } = static (_, _, _, _, _) => { };
    public Action<float, float, float, float, float?> ApplyDeadBodyExplosionImpulse { get; init; } = static (_, _, _, _, _) => { };
    public Action<float, float, float, float, float?> ApplyPlayerGibExplosionImpulse { get; init; } = static (_, _, _, _, _) => { };
    public Action<float, float> RegisterExplosionTraces { get; init; } = static (_, _) => { };
    public Func<PlayerEntity, PlayerTeam, int, bool> ShouldSkipFriendlyExplosionBoost { get; init; } = static (_, _, _) => false;
    public Func<PlayerEntity, PlayerTeam, int, bool> ShouldIgnoreFriendlyGroundedBlast { get; init; } = static (_, _, _) => false;
    public Action<PlayerEntity, float, float, float> ApplyMineExplosionImpulse { get; init; } = static (_, _, _, _) => { };
    public Func<PlayerEntity, float, float, float> GetExplosionDistanceToPlayer { get; init; } = static (_, _, _) => 0f;

    public Func<PlayerEntity?, string?> GetKillFeedWeaponSprite { get; init; } = static _ => null;
    public Func<PlayerEntity?, ExperimentalGameplaySettings> GetLastToDieGameplaySettings { get; init; } = static _ => new();
    public Func<PlayerEntity, bool> IsExperimentalPracticePowerOwner { get; init; } = static _ => false;
    public Func<PlayerEntity, PlayerEntity, bool> TryApplyLastToDieSniperGuardian { get; init; } = static (_, _) => false;
    internal Action<PlayerEntity?, PlayerEntity, MedicHealNeedleProjectileEntity> ApplyMedicHealNeedleTeammateHit { get; init; } = static (_, _, _) => { };
    internal Action<ShotProjectileEntity> ExplodeBoomstickPellet { get; init; } = static _ => { };
    public Func<RocketProjectileEntity, PlayerEntity, (bool Success, float DirectionRadians)> ResolveExperimentalEngineerRocketTrackingDirection { get; init; } = static (_, _) => (false, 0f);
    public Func<float> GetExperimentalSoldierStingerTurnRateRadians { get; init; } = static () => 0f;
    public Func<float> GetExperimentalEngineerCaveatTurnRateRadians { get; init; } = static () => 0f;
    public Func<PlayerEntity, LastToDieMedicKritzM2Payload> CaptureLastToDieMedicKritzM2Payload { get; init; } = static _ => default;
    public Func<int, int> GetPlayerScaleTicks { get; init; } = static ticks => ticks;
    public Action<PlayerEntity, PlayerEntity, bool, float> TryApplyLastToDieSniperStatusPayload { get; init; } = static (_, _, _, _) => { };
    public Func<int, int, LastToDieStatusEffectSpec, bool> TryApplyLastToDieStatusEffect { get; init; } = static (_, _, _) => false;
    public Func<PlayerEntity, PlayerEntity, float, bool, bool> TryApplySpyBackstabDamage { get; init; } = static (_, _, _, _) => false;
    public Func<PlayerEntity, PlayerEntity, float, bool> ApplyLastToDieMultistab { get; init; } = static (_, _, _) => false;
    public Func<PlayerEntity, float, string?, float, float, int> RegisterHealingWithFeedback { get; init; } = static (_, _, _, _, _) => 0;
    public Func<ArrowProjectileEntity, float, float, bool> TryExplodeLastToDieSniperArrow { get; init; } = static (_, _, _) => false;
    public Func<MedicHealNeedleProjectileEntity, bool> TryExplodeLastToDieMedicJavelin { get; init; } = static _ => false;
    public Action<PlayerEntity, float, float> TrySpawnExperimentalDemoknightDecapitationRemains { get; init; } = static (_, _, _) => { };
    public Action<float, float, float, float, float, int, PlayerTeam?, float> ApplyExplosiveDamageToDamageableZones { get; init; } = static (_, _, _, _, _, _, _, _) => { };
    public Action<JumpPadEntity> DestroyJumpPad { get; init; } = static _ => { };
}

/// <summary>Owns projectile entities and their complete spawn/advance/impact tick.</summary>
public sealed partial class ProjectileSystem
{
    private sealed class SharedRandom
    {
        private readonly ProjectileSystemDependencies _dependencies;

        public SharedRandom(ProjectileSystemDependencies dependencies) => _dependencies = dependencies;
        public int Next(int maximumExclusive) => _dependencies.NextRandomInt(maximumExclusive);
        public float NextSingle() => _dependencies.NextRandomSingle();
    }

    private readonly EntityStore _entities;
    private readonly CombatSystem _combat;
    private readonly ProjectileSystemDependencies _dependencies;
    private readonly SharedRandom _random;
    private readonly List<ShotProjectileEntity> _shots = new();
    private readonly List<BubbleProjectileEntity> _bubbles = new();
    private readonly List<BladeProjectileEntity> _blades = new();
    private readonly List<NeedleProjectileEntity> _needles = new();
    private readonly List<RevolverProjectileEntity> _revolverShots = new();
    private readonly List<StabAnimEntity> _stabAnimations = new();
    private readonly List<StabMaskEntity> _stabMasks = new();
    private readonly List<FlameProjectileEntity> _flames = new();
    private readonly List<FlareProjectileEntity> _flares = new();
    private readonly List<RocketProjectileEntity> _rockets = new();
    private readonly List<int> _pendingNewRocketIds = new();
    private readonly List<MineProjectileEntity> _mines = new();
    private readonly List<GrenadeProjectileEntity> _grenades = new();
    private readonly List<WorldRocketSpawnEvent> _pendingRocketSpawnEvents = new();

    public ProjectileSystem(EntityStore entities, CombatSystem combat)
        : this(entities, combat, null)
    {
    }

    public ProjectileSystem(EntityStore entities, CombatSystem combat, ProjectileSystemDependencies? dependencies)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _dependencies = dependencies ?? new ProjectileSystemDependencies
        {
            EnumerateSimulatedPlayers = () => _entities.All().OfType<PlayerEntity>(),
            FindPlayerById = id => _entities.Get(id) as PlayerEntity,
        };
        _random = new SharedRandom(_dependencies);
    }

    public IReadOnlyList<ShotProjectileEntity> Shots => _shots;
    public IReadOnlyList<BubbleProjectileEntity> Bubbles => _bubbles;
    public IReadOnlyList<BladeProjectileEntity> Blades => _blades;
    public IReadOnlyList<NeedleProjectileEntity> Needles => _needles;
    public IReadOnlyList<RevolverProjectileEntity> RevolverShots => _revolverShots;
    public IReadOnlyList<StabAnimEntity> StabAnimations => _stabAnimations;
    public IReadOnlyList<StabMaskEntity> StabMasks => _stabMasks;
    public IReadOnlyList<FlameProjectileEntity> Flames => _flames;
    public IReadOnlyList<FlareProjectileEntity> Flares => _flares;
    public IReadOnlyList<RocketProjectileEntity> Rockets => _rockets;
    public IReadOnlyList<MineProjectileEntity> Mines => _mines;
    public IReadOnlyList<GrenadeProjectileEntity> Grenades => _grenades;
    public IReadOnlyList<WorldRocketSpawnEvent> PendingRocketSpawnEvents => _pendingRocketSpawnEvents;

    internal List<WorldRocketSpawnEvent> PendingRocketSpawnEventsInternal => _pendingRocketSpawnEvents;

    public void ClearPendingNewRocketIds() => _pendingNewRocketIds.Clear();

    internal void AddProjectileEntity(SimulationEntity entity, bool requireUniqueEntityId = false)
    {
        switch (entity)
        {
            case ShotProjectileEntity value: _shots.Add(value); break;
            case BubbleProjectileEntity value: _bubbles.Add(value); break;
            case BladeProjectileEntity value: _blades.Add(value); break;
            case NeedleProjectileEntity value: _needles.Add(value); break;
            case RevolverProjectileEntity value: _revolverShots.Add(value); break;
            case StabAnimEntity value: _stabAnimations.Add(value); break;
            case StabMaskEntity value: _stabMasks.Add(value); break;
            case FlameProjectileEntity value: _flames.Add(value); break;
            case FlareProjectileEntity value: _flares.Add(value); break;
            case RocketProjectileEntity value: _rockets.Add(value); break;
            case MineProjectileEntity value: _mines.Add(value); break;
            case GrenadeProjectileEntity value: _grenades.Add(value); break;
            default: throw new ArgumentOutOfRangeException(nameof(entity));
        }

        if (requireUniqueEntityId)
        {
            _entities.Add(entity);
        }
        else
        {
            _entities.Set(entity.Id, entity);
        }
    }

    internal bool RemoveProjectileEntity(int entityId)
    {
        var removed = false;
        removed |= RemoveProjectileEntity(_shots, entityId);
        removed |= RemoveProjectileEntity(_bubbles, entityId);
        removed |= RemoveProjectileEntity(_blades, entityId);
        removed |= RemoveProjectileEntity(_needles, entityId);
        removed |= RemoveProjectileEntity(_revolverShots, entityId);
        removed |= RemoveProjectileEntity(_flames, entityId);
        removed |= RemoveProjectileEntity(_flares, entityId);
        removed |= RemoveProjectileEntity(_rockets, entityId);
        removed |= RemoveProjectileEntity(_mines, entityId);
        removed |= RemoveProjectileEntity(_grenades, entityId);
        removed |= _entities.Remove(entityId);
        return removed;
    }

    internal void ClearProjectileCollection<T>(IReadOnlyList<T> collection)
        where T : SimulationEntity
    {
        if (collection is not List<T> mutableCollection)
        {
            throw new ArgumentException("The collection is not owned by this projectile system.", nameof(collection));
        }

        mutableCollection.Clear();
    }

    internal void AddProjectileToCollection<T>(IReadOnlyList<T> collection, T entity)
        where T : SimulationEntity
    {
        if (collection is not List<T> mutableCollection)
        {
            throw new ArgumentException("The collection is not owned by this projectile system.", nameof(collection));
        }

        mutableCollection.Add(entity);
    }

    internal void RemoveAllProjectiles<T>(IReadOnlyList<T> collection)
        where T : SimulationEntity
    {
        for (var index = 0; index < collection.Count; index += 1)
        {
            _entities.Remove(collection[index].Id);
        }

        ClearProjectileCollection(collection);
    }

    private static bool RemoveProjectileEntity<T>(List<T> entities, int entityId)
        where T : SimulationEntity
    {
        for (var index = entities.Count - 1; index >= 0; index -= 1)
        {
            if (entities[index].Id != entityId)
            {
                continue;
            }

            entities.RemoveAt(index);
            return true;
        }

        return false;
    }

    private SimpleLevel Level => _dependencies.GetLevel();
    private EntityStore EntityStore => _entities;
    private SimulationConfig Config => _dependencies.Config;
    private long Frame => _dependencies.CurrentFrame();
    private float _configuredGravityScale => _dependencies.GetGravityScale();
    private bool ClientPredictionMode => _dependencies.IsClientPredictionMode();
    private IReadOnlyList<SentryEntity> _sentries => _dependencies.Sentries;
    private IReadOnlyList<JumpPadEntity> _jumpPads => _dependencies.JumpPads;
    private IReadOnlyList<GeneratorState> _generators => _dependencies.Generators;
    private IReadOnlyList<CivilDefenseTurretEntity> _civilDefenseTurrets => [];

    private PlayerEntity? FindPlayerById(int id) => _dependencies.FindPlayerById(id);
    private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers() => _dependencies.EnumerateSimulatedPlayers();
    private bool CanTeamDamagePlayer(PlayerTeam team, int attackerId, PlayerEntity player) => _dependencies.CanTeamDamagePlayer(team, attackerId, player);
    private int AllocateEntityId() => _dependencies.AllocateEntityId();
    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
        => MathF.Atan2(y2 - y1, x2 - x1) * (180f / MathF.PI);

    private void RegisterCombatTrace(float x, float y, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team = PlayerTeam.Red, bool isSniperTracer = false, bool isCritical = false)
        => _dependencies.RegisterCombatTrace(x, y, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    private void RegisterBloodEffect(float x, float y, float direction, int count = 1) => _dependencies.RegisterBloodEffect(x, y, direction, count);
    private void RegisterVisualEffect(string effect, float x, float y, float direction = 0f, int count = 1, bool normalizeDirection = true) => _dependencies.RegisterVisualEffect(effect, x, y, direction, count, normalizeDirection);
    private void RegisterWorldSoundEvent(string sound, float x, float y, int sourcePlayerId = -1) => _dependencies.RegisterWorldSoundEvent(sound, x, y, sourcePlayerId);
    private void RegisterImpactEffect(float x, float y, float direction) => _dependencies.RegisterImpactEffect(x, y, direction);
    // The burst direction is the surface's outward normal; 270 degrees bursts straight up.
    private void RegisterStrongDrinkShatterEffect(float x, float y, PlayerTeam team, float burstDirectionDegrees = 270f)
        => RegisterVisualEffect("BottleShards", x, y, burstDirectionDegrees, count: (int)team);
    private void RegisterStuckArrowEffect(float x, float y, float directionX, float directionY, ArrowProjectileEntity arrow) => _dependencies.RegisterStuckArrowEffect(x, y, directionX, directionY, arrow);

    private ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float dx, float dy, float distance) => _dependencies.GetNearestShotHit(shot, dx, dy, distance);
    private ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float dx, float dy, float distance) => _dependencies.GetNearestNeedleHit(needle, dx, dy, distance);
    private ShotHitResult? GetNearestMedicHealNeedleHit(MedicHealNeedleProjectileEntity needle, float dx, float dy, float distance) => _dependencies.GetNearestMedicHealNeedleHit(needle, dx, dy, distance);
    private ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float dx, float dy, float distance) => _dependencies.GetNearestRevolverHit(shot, dx, dy, distance);
    private ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float dx, float dy, float distance) => _dependencies.GetNearestBladeHit(blade, dx, dy, distance);
    private ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float dx, float dy) => _dependencies.GetNearestStabHit(mask, dx, dy);
    private ShotHitResult? GetNearestHealstabHit(StabMaskEntity mask, float dx, float dy) => _dependencies.GetNearestHealstabHit(mask, dx, dy);
    private bool HasStabChainLineOfSight(float x1, float y1, float x2, float y2) => _dependencies.HasStabChainLineOfSight(x1, y1, x2, y2);
    private RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float dx, float dy, float distance) => _dependencies.GetNearestRocketHit(rocket, dx, dy, distance);
    private MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float dx, float dy, float distance) => _dependencies.GetNearestMineHit(mine, dx, dy, distance);
    private GrenadeEnvironmentHit? GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float dx, float dy, float distance) => _dependencies.GetNearestGrenadeEnvironmentHit(grenade, dx, dy, distance);
    private PlayerEntity? GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float dx, float dy, float distance) => _dependencies.GetNearestGrenadePlayerHit(grenade, dx, dy, distance);
    private bool TryGetGrenadeDamageableZoneContact(GrenadeProjectileEntity grenade, float dx, float dy, float distance, out float hitX, out float hitY, out int roomObjectIndex)
        => _dependencies.TryGetGrenadeDamageableZoneContact(grenade, dx, dy, distance, out hitX, out hitY, out roomObjectIndex);
    private FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float dx, float dy, float distance) => _dependencies.GetNearestFlameHit(flame, dx, dy, distance);
    private ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float dx, float dy, float distance) => _dependencies.GetNearestFlareHit(flare, dx, dy, distance);
    private ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float dx, float dy, float distance, bool includePlayers)
        => _dependencies.GetNearestFlareHitWithPlayerFilter(flare, dx, dy, distance, includePlayers);
    private ShotHitResult? GetNearestFlarePlayerHit(FlareProjectileEntity flare, float dx, float dy, float distance, ShotHitResult? blockingHit)
        => _dependencies.GetNearestFlarePlayerHit(flare, dx, dy, distance, blockingHit);
    private bool IsProjectilePathBlocked(float x1, float y1, float x2, float y2, PlayerTeam team) => _dependencies.IsProjectilePathBlocked(x1, y1, x2, y2, team);
    private void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom) => _dependencies.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);

    private bool TryInterceptWithCivilDefenseTurret(PlayerTeam team, float x, float y, float dx, float dy, float distance)
        => _dependencies.TryInterceptWithCivilDefenseTurret(team, x, y, dx, dy, distance);
    private bool TryHandleProjectileDamageableZoneHit(in ShotHitResult hit, float damage, PlayerTeam team) => _dependencies.TryHandleProjectileDamageableZoneHit(hit, damage, team);
    private bool TryApplyDamageableZoneDamage(int index, float damage, PlayerTeam? team = null) => _dependencies.TryApplyDamageableZoneDamage(index, damage, team);
    private bool BlocksProjectileDamageableZone(int index) => _dependencies.BlocksProjectileDamageableZone(index);
    private bool TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target) => _dependencies.TryApplyLastToDieSniperGuardian(sniper, target);
    private void ApplyMedicHealNeedleTeammateHit(PlayerEntity? medic, PlayerEntity target, MedicHealNeedleProjectileEntity needle)
        => _dependencies.ApplyMedicHealNeedleTeammateHit(medic, target, needle);
    private void ExplodeBoomstickPellet(ShotProjectileEntity shot) => _dependencies.ExplodeBoomstickPellet(shot);

    private int ApplyExperimentalAirshotDamageMultiplier(PlayerEntity? owner, PlayerEntity target, int damage, out DamageEventFlags flags)
    {
        var result = _dependencies.ApplyExperimentalAirshotDamageMultiplier(owner, target, damage);
        flags = result.Flags;
        return result.Damage;
    }

    private bool ApplyPlayerDamageWithContext(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost = true,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool? attackerWasGrounded = null,
        bool? targetWasGrounded = null,
        int sourceEntityId = 0,
        ulong attackId = 0,
        int attackerPlayerIdOverride = -1)
        => _combat.ApplyPlayerDamageWithContext(
            target, damage, attacker, spyRevealAlpha, damageFlags,
            allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield,
            civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits,
            attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId,
            attackerPlayerIdOverride);

    private bool ApplyPlayerContinuousDamageWithContext(
        PlayerEntity target,
        float damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost = true,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool? attackerWasGrounded = null,
        bool? targetWasGrounded = null)
        => _combat.ApplyPlayerContinuousDamageWithContext(
            target, damage, attacker, spyRevealAlpha, damageFlags,
            allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield,
            civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits,
            attackerWasGrounded, targetWasGrounded);

    private PlayerDamageResolution ResolvePlayerDamage(PlayerEntity target, in PlayerDamageRequest request)
        => _combat.ResolvePlayerDamage(target, request);

    private PlayerDamageResolution ResolvePlayerDamageWithContext(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost = true,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool? attackerWasGrounded = null,
        bool? targetWasGrounded = null,
        int sourceEntityId = 0,
        ulong attackId = 0,
        int attackerPlayerIdOverride = -1)
        => _combat.ResolvePlayerDamageWithContext(
            target, damage, attacker, spyRevealAlpha, damageFlags,
            allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield,
            civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits,
            attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId,
            attackerPlayerIdOverride);

    private bool TryAbsorbCivvieUmbrellaProjectileContact(
        PlayerEntity target,
        int ownerId,
        float hitX,
        float hitY,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool criticalBoost = false)
        => _combat.TryAbsorbCivvieUmbrellaProjectileContact(target, ownerId, hitX, hitY, damageFlags, criticalBoost);
    private void ApplyExperimentalSentryPlayerHit(SentryEntity sentry, PlayerEntity owner, PlayerEntity target, int damage, PlayerDamageTraits additionalTraits, bool criticalBoost, bool useLiveAttackerCriticalBoost, float? threatSourceX, float? threatSourceY, BulletKnockbackPayload? knockbackPayload, float? impactDirectionX, float? impactDirectionY)
        => _dependencies.ApplyExperimentalSentryPlayerHit(sentry, owner, target, damage, additionalTraits, criticalBoost, useLiveAttackerCriticalBoost, threatSourceX, threatSourceY, knockbackPayload, impactDirectionX, impactDirectionY);
    private void ApplyExperimentalSentryDamageRewards(SentryEntity sentry, PlayerEntity owner, int damage) => _dependencies.ApplyExperimentalSentryDamageRewards(sentry, owner, damage);
    private bool ApplySentryDamage(SentryEntity sentry, int damage, PlayerEntity? owner) => _dependencies.ApplySentryDamage(sentry, damage, owner);
    private bool ApplyGeneratorDamage(GeneratorState generator, float damage, PlayerEntity? owner) => _dependencies.ApplyGeneratorDamage(generator, damage, owner);
    private void ApplyJumpPadDamage(JumpPadEntity jumpPad, int damage) => _dependencies.ApplyJumpPadDamage(jumpPad, damage);
    private bool TryDamageGenerator(PlayerTeam team, float damage, PlayerEntity? owner = null) => _dependencies.TryDamageGenerator(team, damage, owner);
    private void DestroySentry(SentryEntity sentry, PlayerEntity? owner = null) => _dependencies.DestroySentry(sentry, owner);
    private void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null, DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default)
        => _dependencies.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind);
    private int ApplyHealingWithFeedback(PlayerEntity player, float healing, string? sound = null, float x = 0f, float y = 0f) => _dependencies.ApplyHealingWithFeedback(player, healing, sound, x, y);
    private void ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? player, SentryEntity? sentry, GeneratorState? generator, int damageableZoneIndex = -1) => _dependencies.ExplodeRocket(rocket, player, sentry, generator, damageableZoneIndex);
    private void ApplyExplosiveDamageToJumpPads(float x, float y, float radius, float damage, PlayerTeam team, float minimumDamage)
        => _dependencies.ApplyExplosiveDamageToJumpPads(x, y, radius, damage, team, minimumDamage);
    private void ApplyExplosiveDamageToDamageableZones(float x, float y, float radius, float damage, float splashThresholdFactor = 0f, int excludeRoomObjectIndex = -1, PlayerTeam? damagingTeam = null, float minimumSplashDamage = 0f)
        => _dependencies.ApplyExplosiveDamageToDamageableZones(x, y, radius, damage, splashThresholdFactor, excludeRoomObjectIndex, damagingTeam, minimumSplashDamage);
    private void DestroyJumpPad(JumpPadEntity pad) => _dependencies.DestroyJumpPad(pad);

    private const float ExplosiveJumpPadDamageMultiplier = 1.5f;
    private const float ExplosiveSplashMinimumDamage = CombatSystem.ExplosiveSplashMinimumDamage;

    private static float ResolveExplosiveSplashRadius(float radius) => CombatSystem.ResolveExplosiveSplashRadius(radius);
    private static float ResolveExplosiveSplashDamage(float damage, float factor) => CombatSystem.ResolveExplosiveSplashDamage(damage, factor);
    private void ApplyDeadBodyExplosionImpulse(float x, float y, float radius, float impulse, float? falloff = null) => _dependencies.ApplyDeadBodyExplosionImpulse(x, y, radius, impulse, falloff);
    private void ApplyPlayerGibExplosionImpulse(float x, float y, float radius, float impulse, float? falloff = null) => _dependencies.ApplyPlayerGibExplosionImpulse(x, y, radius, impulse, falloff);
    private void RegisterExplosionTraces(float x, float y) => _dependencies.RegisterExplosionTraces(x, y);
    private bool ShouldSkipFriendlyExplosionBoost(PlayerEntity player, PlayerTeam team, int ownerId) => _dependencies.ShouldSkipFriendlyExplosionBoost(player, team, ownerId);
    private bool ShouldIgnoreFriendlyGroundedBlast(PlayerEntity player, PlayerTeam team, int ownerId) => _dependencies.ShouldIgnoreFriendlyGroundedBlast(player, team, ownerId);
    private void ApplyMineExplosionImpulse(PlayerEntity player, float x, float y, float factor) => _dependencies.ApplyMineExplosionImpulse(player, x, y, factor);
    private static float GetExplosionDistanceToPlayer(ProjectileSystem system, PlayerEntity player, float x, float y) => system._dependencies.GetExplosionDistanceToPlayer(player, x, y);

    private string? GetKillFeedWeaponSprite(PlayerEntity? owner) => _dependencies.GetKillFeedWeaponSprite(owner);
    private ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player) => _dependencies.GetLastToDieGameplaySettings(player);
    private bool IsExperimentalPracticePowerOwner(PlayerEntity player) => _dependencies.IsExperimentalPracticePowerOwner(player);
    private bool TryResolveExperimentalEngineerRocketTrackingDirection(RocketProjectileEntity rocket, PlayerEntity player, out float direction)
    {
        var result = _dependencies.ResolveExperimentalEngineerRocketTrackingDirection(rocket, player);
        direction = result.DirectionRadians;
        return result.Success;
    }
    private static float GetExperimentalSoldierStingerTurnRateRadians() => 0f;
    private static float GetExperimentalEngineerCaveatTurnRateRadians() => 0f;
    private LastToDieMedicKritzM2Payload CaptureLastToDieMedicKritzM2Payload(PlayerEntity owner) => _dependencies.CaptureLastToDieMedicKritzM2Payload(owner);
    private void TryApplyLastToDieSniperStatusPayload(PlayerEntity owner, PlayerEntity target, bool tranq, float poison) => _dependencies.TryApplyLastToDieSniperStatusPayload(owner, target, tranq, poison);
    private bool TryApplyLastToDieStatusEffect(int targetId, int ownerId, LastToDieStatusEffectSpec spec) => _dependencies.TryApplyLastToDieStatusEffect(targetId, ownerId, spec);
    private bool TryExplodeLastToDieSniperArrow(ArrowProjectileEntity arrow, float x = 0f, float y = 0f) => _dependencies.TryExplodeLastToDieSniperArrow(arrow, x, y);
    private bool TryExplodeLastToDieMedicJavelin(MedicHealNeedleProjectileEntity needle, float x = 0f, float y = 0f) => _dependencies.TryExplodeLastToDieMedicJavelin(needle);
    private void TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity player, float dx, float dy) => _dependencies.TrySpawnExperimentalDemoknightDecapitationRemains(player, dx, dy);
}
