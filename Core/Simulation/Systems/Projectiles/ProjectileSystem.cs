using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

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
public readonly record struct GrenadeEnvironmentHit(
    float Distance,
    float HitX,
    float HitY,
    float NormalX,
    float NormalY,
    bool StartsOverlapping = false,
    float OverlapDistance = 0f);
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

/// <summary>Entity allocation and client-prediction state for spawning and advancing projectiles.</summary>
internal interface IProjectileSpawnContext
{
    float GravityScale { get; }
    bool IsClientPredictionMode { get; }
    int? AuthoritativeLocalPlayerId { get; }
    int LocalPlayerId { get; }
    int LocalProjectileTerminationSuppressionTicks { get; }
    HashSet<int> ClientPredictedProjectileIds { get; }
    int AllocateEntityId();
    void SuppressProjectileRespawn(int projectileId, int ticks);
}

/// <summary>Geometry and hitbox queries that resolve what a moving projectile touches.</summary>
internal interface IProjectileHitQueries
{
    ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float distance);
    ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float distance);
    ShotHitResult? GetNearestMedicHealNeedleHit(MedicHealNeedleProjectileEntity needle, float directionX, float directionY, float distance);
    ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float distance);
    ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float distance);
    ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY);
    ShotHitResult? GetNearestHealstabHit(StabMaskEntity mask, float directionX, float directionY);
    bool HasStabChainLineOfSight(float x1, float y1, float x2, float y2);
    RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float distance);
    MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float distance);
    GrenadeEnvironmentHit? GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float distance);
    PlayerEntity? GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float distance);
    bool TryGetGrenadeDamageableZoneContact(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance, out float hitX, out float hitY, out int roomObjectIndex);
    FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float distance);
    ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float distance);
    ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float distance, bool includePlayers);
    ShotHitResult? GetNearestFlarePlayerHit(FlareProjectileEntity flare, float directionX, float directionY, float distance, ShotHitResult? blockingHit);
    bool IsProjectilePathBlocked(float x1, float y1, float x2, float y2, PlayerTeam team);
    bool TryInterceptWithCivilDefenseTurret(PlayerTeam team, float x, float y, float directionX, float directionY, float distance);
    void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom);
    float GetExplosionDistanceToPlayer(PlayerEntity player, float x, float y);
}

/// <summary>Damage, healing, and destruction applied to whatever a projectile hits.</summary>
internal interface IProjectileImpactTargets
{
    void KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind);
    void DestroySentry(SentryEntity sentry, PlayerEntity? attacker);
    bool ApplySentryDamage(SentryEntity sentry, int damage, PlayerEntity? attacker);
    bool ApplyGeneratorDamage(GeneratorState generator, float damage, PlayerEntity? attacker);
    bool TryDamageGenerator(PlayerTeam team, float damage, PlayerEntity? attacker);
    void ApplyJumpPadDamage(JumpPadEntity jumpPad, int damage);
    void ApplyExplosiveDamageToJumpPads(float x, float y, float radius, float damage, PlayerTeam team, float minimumDamage);
    void ApplyExplosiveDamageToDamageableZones(float x, float y, float radius, float damage, float splashThresholdFactor, int excludeRoomObjectIndex, PlayerTeam? damagingTeam, float minimumSplashDamage);
    bool TryHandleProjectileDamageableZoneHit(in ShotHitResult hit, float damage, PlayerTeam team);
    bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? team);
    bool BlocksProjectileDamageableZone(int roomObjectIndex);
    int ApplyHealingWithFeedback(PlayerEntity player, float healing, string? soundName, float x, float y);
}

/// <summary>Explosion resolution and the physical impulses it imparts.</summary>
internal interface IProjectileExplosionEffects
{
    void ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator, int damageableZoneIndex);
    void ApplyDeadBodyExplosionImpulse(float x, float y, float radius, float impulse, float? falloff);
    void ApplyPlayerGibExplosionImpulse(float x, float y, float radius, float impulse, float? falloff);
    void ApplyMineExplosionImpulse(PlayerEntity player, float x, float y, float factor);
    bool ShouldSkipFriendlyExplosionBoost(PlayerEntity player, PlayerTeam team, int ownerId);
    bool ShouldIgnoreFriendlyGroundedBlast(PlayerEntity player, PlayerTeam team, int ownerId);
}

/// <summary>Kill-feed, experimental, and Last-To-Die behavior layered onto projectile hits.</summary>
internal interface IProjectileGameplayRules
{
    float ExperimentalSoldierStingerTurnRateRadians { get; }
    float ExperimentalEngineerCaveatTurnRateRadians { get; }
    string? GetKillFeedWeaponSprite(PlayerEntity? owner);
    int ApplyExperimentalAirshotDamageMultiplier(PlayerEntity? owner, PlayerEntity target, int damage, out DamageEventFlags flags);
    void ApplyExperimentalSentryPlayerHit(SentryEntity sentry, PlayerEntity owner, PlayerEntity target, int damage, PlayerDamageTraits additionalTraits, bool criticalBoost, bool useLiveAttackerCriticalBoost, float? threatSourceX, float? threatSourceY, BulletKnockbackPayload? knockbackPayload, float? impactDirectionX, float? impactDirectionY);
    void ApplyExperimentalSentryDamageRewards(SentryEntity sentry, PlayerEntity owner, int damage);
    bool TryResolveExperimentalEngineerRocketTrackingDirection(RocketProjectileEntity rocket, PlayerEntity player, out float directionRadians);
    void TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity player, float directionX, float directionY);
    void ApplyMedicHealNeedleTeammateHit(PlayerEntity? medic, PlayerEntity target, MedicHealNeedleProjectileEntity needle);
    void ExplodeBoomstickPellet(ShotProjectileEntity shot);
    bool TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target);
    void TryApplyLastToDieSniperStatusPayload(PlayerEntity owner, PlayerEntity target, bool tranquilize, float poison);
    bool TryApplyLastToDieStatusEffect(int targetId, int sourceId, LastToDieStatusEffectSpec spec);
    LastToDieMedicKritzM2Payload CaptureLastToDieMedicKritzM2Payload(PlayerEntity owner);
    bool TryExplodeLastToDieSniperArrow(ArrowProjectileEntity arrow, float x, float y);
    bool TryExplodeLastToDieMedicJavelin(MedicHealNeedleProjectileEntity needle);
}

/// <summary>
/// Everything <see cref="ProjectileSystem"/> needs from the world. The system
/// owns projectile state and movement; the host supplies only the queries and
/// presentation/game-mode consequences that are not projectile state.
/// </summary>
internal interface IProjectileSystemHost :
    ISimulationWorldState,
    ISimulationPlayerDirectory,
    ISimulationRandomSource,
    ISimulationStructures,
    ISimulationPresentationEvents,
    ISimulationExperimentalRules,
    IProjectileSpawnContext,
    IProjectileHitQueries,
    IProjectileImpactTargets,
    IProjectileExplosionEffects,
    IProjectileGameplayRules
{
}

/// <summary>Owns projectile entities and their complete spawn/advance/impact tick.</summary>
public sealed partial class ProjectileSystem
{
    private readonly EntityStore _entities;
    private readonly CombatSystem _combat;
    private readonly IProjectileSystemHost _host;
    private readonly ISimulationRandomSource _random;
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
        : this(entities, combat, new DetachedSimulationHost(entities))
    {
    }

    internal ProjectileSystem(EntityStore entities, CombatSystem combat, IProjectileSystemHost host)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _random = host;
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

    /// <summary>Returns the queued rocket spawn events and empties the queue.</summary>
    public IReadOnlyList<WorldRocketSpawnEvent> DrainPendingRocketSpawnEvents()
    {
        if (_pendingRocketSpawnEvents.Count == 0)
        {
            return [];
        }

        var rocketSpawnEvents = _pendingRocketSpawnEvents.ToArray();
        _pendingRocketSpawnEvents.Clear();
        return rocketSpawnEvents;
    }

    public void ClearPendingRocketSpawnEvents() => _pendingRocketSpawnEvents.Clear();

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

    private SimpleLevel Level => _host.Level;
    private EntityStore EntityStore => _entities;
    private SimulationConfig Config => _host.Config;
    private long Frame => _host.Frame;
    private float _configuredGravityScale => _host.GravityScale;
    private bool ClientPredictionMode => _host.IsClientPredictionMode;
    private IReadOnlyList<SentryEntity> _sentries => _host.Sentries;
    private IReadOnlyList<JumpPadEntity> _jumpPads => _host.JumpPads;
    private IReadOnlyList<GeneratorState> _generators => _host.Generators;
    private IReadOnlyList<CivilDefenseTurretEntity> _civilDefenseTurrets => [];

    private PlayerEntity? FindPlayerById(int id) => _host.FindPlayerById(id);
    private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers() => _host.EnumerateSimulatedPlayers();
    private bool CanTeamDamagePlayer(PlayerTeam team, int attackerId, PlayerEntity player) => _host.CanTeamDamagePlayer(team, attackerId, player);
    private int AllocateEntityId() => _host.AllocateEntityId();
    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
        => DeterministicMath.Atan2(y2 - y1, x2 - x1) * (180f / MathF.PI);

    private void RegisterCombatTrace(float x, float y, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team = PlayerTeam.Red, bool isSniperTracer = false, bool isCritical = false)
        => _host.RegisterCombatTrace(x, y, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    private void RegisterBloodEffect(float x, float y, float direction, int count = 1) => _host.RegisterBloodEffect(x, y, direction, count);
    private void RegisterVisualEffect(string effect, float x, float y, float direction = 0f, int count = 1, bool normalizeDirection = true) => _host.RegisterVisualEffect(effect, x, y, direction, count, normalizeDirection);
    private void RegisterWorldSoundEvent(string sound, float x, float y, int sourcePlayerId = -1) => _host.RegisterWorldSoundEvent(sound, x, y, sourcePlayerId);
    private void RegisterImpactEffect(float x, float y, float direction) => _host.RegisterImpactEffect(x, y, direction);
    // The burst direction is the surface's outward normal; 270 degrees bursts straight up.
    private void RegisterStrongDrinkShatterEffect(float x, float y, PlayerTeam team, float burstDirectionDegrees = 270f)
        => RegisterVisualEffect("BottleShards", x, y, burstDirectionDegrees, count: (int)team);
    private void RegisterStuckArrowEffect(float x, float y, float directionX, float directionY, ArrowProjectileEntity arrow) => _host.RegisterStuckArrowEffect(x, y, directionX, directionY, arrow);

    private ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float dx, float dy, float distance) => _host.GetNearestShotHit(shot, dx, dy, distance);
    private ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float dx, float dy, float distance) => _host.GetNearestNeedleHit(needle, dx, dy, distance);
    private ShotHitResult? GetNearestMedicHealNeedleHit(MedicHealNeedleProjectileEntity needle, float dx, float dy, float distance) => _host.GetNearestMedicHealNeedleHit(needle, dx, dy, distance);
    private ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float dx, float dy, float distance) => _host.GetNearestRevolverHit(shot, dx, dy, distance);
    private ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float dx, float dy, float distance) => _host.GetNearestBladeHit(blade, dx, dy, distance);
    private ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float dx, float dy) => _host.GetNearestStabHit(mask, dx, dy);
    private ShotHitResult? GetNearestHealstabHit(StabMaskEntity mask, float dx, float dy) => _host.GetNearestHealstabHit(mask, dx, dy);
    private bool HasStabChainLineOfSight(float x1, float y1, float x2, float y2) => _host.HasStabChainLineOfSight(x1, y1, x2, y2);
    private RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float dx, float dy, float distance) => _host.GetNearestRocketHit(rocket, dx, dy, distance);
    private MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float dx, float dy, float distance) => _host.GetNearestMineHit(mine, dx, dy, distance);
    private GrenadeEnvironmentHit? GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float dx, float dy, float distance) => _host.GetNearestGrenadeEnvironmentHit(grenade, dx, dy, distance);
    private PlayerEntity? GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float dx, float dy, float distance) => _host.GetNearestGrenadePlayerHit(grenade, dx, dy, distance);
    private bool TryGetGrenadeDamageableZoneContact(GrenadeProjectileEntity grenade, float dx, float dy, float distance, out float hitX, out float hitY, out int roomObjectIndex)
        => _host.TryGetGrenadeDamageableZoneContact(grenade, dx, dy, distance, out hitX, out hitY, out roomObjectIndex);
    private FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float dx, float dy, float distance) => _host.GetNearestFlameHit(flame, dx, dy, distance);
    private ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float dx, float dy, float distance) => _host.GetNearestFlareHit(flare, dx, dy, distance);
    private ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float dx, float dy, float distance, bool includePlayers)
        => _host.GetNearestFlareHit(flare, dx, dy, distance, includePlayers);
    private ShotHitResult? GetNearestFlarePlayerHit(FlareProjectileEntity flare, float dx, float dy, float distance, ShotHitResult? blockingHit)
        => _host.GetNearestFlarePlayerHit(flare, dx, dy, distance, blockingHit);
    private bool IsProjectilePathBlocked(float x1, float y1, float x2, float y2, PlayerTeam team) => _host.IsProjectilePathBlocked(x1, y1, x2, y2, team);
    private void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom) => _host.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);

    private bool TryInterceptWithCivilDefenseTurret(PlayerTeam team, float x, float y, float dx, float dy, float distance)
        => _host.TryInterceptWithCivilDefenseTurret(team, x, y, dx, dy, distance);
    private bool TryHandleProjectileDamageableZoneHit(in ShotHitResult hit, float damage, PlayerTeam team) => _host.TryHandleProjectileDamageableZoneHit(hit, damage, team);
    private bool TryApplyDamageableZoneDamage(int index, float damage, PlayerTeam? team = null) => _host.TryApplyDamageableZoneDamage(index, damage, team);
    private bool BlocksProjectileDamageableZone(int index) => _host.BlocksProjectileDamageableZone(index);
    private bool TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target) => _host.TryApplyLastToDieSniperGuardian(sniper, target);
    private void ApplyMedicHealNeedleTeammateHit(PlayerEntity? medic, PlayerEntity target, MedicHealNeedleProjectileEntity needle)
        => _host.ApplyMedicHealNeedleTeammateHit(medic, target, needle);
    private void ExplodeBoomstickPellet(ShotProjectileEntity shot) => _host.ExplodeBoomstickPellet(shot);

    private int ApplyExperimentalAirshotDamageMultiplier(PlayerEntity? owner, PlayerEntity target, int damage, out DamageEventFlags flags)
        => _host.ApplyExperimentalAirshotDamageMultiplier(owner, target, damage, out flags);

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
        => _host.ApplyExperimentalSentryPlayerHit(sentry, owner, target, damage, additionalTraits, criticalBoost, useLiveAttackerCriticalBoost, threatSourceX, threatSourceY, knockbackPayload, impactDirectionX, impactDirectionY);
    private void ApplyExperimentalSentryDamageRewards(SentryEntity sentry, PlayerEntity owner, int damage) => _host.ApplyExperimentalSentryDamageRewards(sentry, owner, damage);
    private bool ApplySentryDamage(SentryEntity sentry, int damage, PlayerEntity? owner) => _host.ApplySentryDamage(sentry, damage, owner);
    private bool ApplyGeneratorDamage(GeneratorState generator, float damage, PlayerEntity? owner) => _host.ApplyGeneratorDamage(generator, damage, owner);
    private void ApplyJumpPadDamage(JumpPadEntity jumpPad, int damage) => _host.ApplyJumpPadDamage(jumpPad, damage);
    private bool TryDamageGenerator(PlayerTeam team, float damage, PlayerEntity? owner = null) => _host.TryDamageGenerator(team, damage, owner);
    private void DestroySentry(SentryEntity sentry, PlayerEntity? owner = null) => _host.DestroySentry(sentry, owner);
    private void KillPlayer(PlayerEntity player, bool gibbed = false, PlayerEntity? killer = null, string? weaponSpriteName = null, DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default)
        => _host.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind);
    private int ApplyHealingWithFeedback(PlayerEntity player, float healing, string? sound = null, float x = 0f, float y = 0f) => _host.ApplyHealingWithFeedback(player, healing, sound, x, y);
    private void ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? player, SentryEntity? sentry, GeneratorState? generator, int damageableZoneIndex = -1) => _host.ExplodeRocket(rocket, player, sentry, generator, damageableZoneIndex);
    private void ApplyExplosiveDamageToJumpPads(float x, float y, float radius, float damage, PlayerTeam team, float minimumDamage)
        => _host.ApplyExplosiveDamageToJumpPads(x, y, radius, damage, team, minimumDamage);
    private void ApplyExplosiveDamageToDamageableZones(float x, float y, float radius, float damage, float splashThresholdFactor = 0f, int excludeRoomObjectIndex = -1, PlayerTeam? damagingTeam = null, float minimumSplashDamage = 0f)
        => _host.ApplyExplosiveDamageToDamageableZones(x, y, radius, damage, splashThresholdFactor, excludeRoomObjectIndex, damagingTeam, minimumSplashDamage);
    private void DestroyJumpPad(JumpPadEntity pad) => _host.DestroyJumpPad(pad);

    private const float ExplosiveJumpPadDamageMultiplier = 1.5f;
    private const float ExplosiveSplashMinimumDamage = CombatSystem.ExplosiveSplashMinimumDamage;

    private static float ResolveExplosiveSplashRadius(float radius) => CombatSystem.ResolveExplosiveSplashRadius(radius);
    private static float ResolveExplosiveSplashDamage(float damage, float factor) => CombatSystem.ResolveExplosiveSplashDamage(damage, factor);
    private void ApplyDeadBodyExplosionImpulse(float x, float y, float radius, float impulse, float? falloff = null) => _host.ApplyDeadBodyExplosionImpulse(x, y, radius, impulse, falloff);
    private void ApplyPlayerGibExplosionImpulse(float x, float y, float radius, float impulse, float? falloff = null) => _host.ApplyPlayerGibExplosionImpulse(x, y, radius, impulse, falloff);
    private void RegisterExplosionTraces(float x, float y) => _host.RegisterExplosionTraces(x, y);
    private bool ShouldSkipFriendlyExplosionBoost(PlayerEntity player, PlayerTeam team, int ownerId) => _host.ShouldSkipFriendlyExplosionBoost(player, team, ownerId);
    private bool ShouldIgnoreFriendlyGroundedBlast(PlayerEntity player, PlayerTeam team, int ownerId) => _host.ShouldIgnoreFriendlyGroundedBlast(player, team, ownerId);
    private void ApplyMineExplosionImpulse(PlayerEntity player, float x, float y, float factor) => _host.ApplyMineExplosionImpulse(player, x, y, factor);
    private static float GetExplosionDistanceToPlayer(ProjectileSystem system, PlayerEntity player, float x, float y) => system._host.GetExplosionDistanceToPlayer(player, x, y);

    private string? GetKillFeedWeaponSprite(PlayerEntity? owner) => _host.GetKillFeedWeaponSprite(owner);
    private ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player) => _host.GetLastToDieGameplaySettings(player);
    private bool IsExperimentalPracticePowerOwner(PlayerEntity player) => _host.IsExperimentalPracticePowerOwner(player);
    private bool TryResolveExperimentalEngineerRocketTrackingDirection(RocketProjectileEntity rocket, PlayerEntity player, out float direction)
        => _host.TryResolveExperimentalEngineerRocketTrackingDirection(rocket, player, out direction);
    private float GetExperimentalSoldierStingerTurnRateRadians() => _host.ExperimentalSoldierStingerTurnRateRadians;
    private float GetExperimentalEngineerCaveatTurnRateRadians() => _host.ExperimentalEngineerCaveatTurnRateRadians;
    private LastToDieMedicKritzM2Payload CaptureLastToDieMedicKritzM2Payload(PlayerEntity owner) => _host.CaptureLastToDieMedicKritzM2Payload(owner);
    private void TryApplyLastToDieSniperStatusPayload(PlayerEntity owner, PlayerEntity target, bool tranq, float poison) => _host.TryApplyLastToDieSniperStatusPayload(owner, target, tranq, poison);
    private bool TryApplyLastToDieStatusEffect(int targetId, int ownerId, LastToDieStatusEffectSpec spec) => _host.TryApplyLastToDieStatusEffect(targetId, ownerId, spec);
    private bool TryExplodeLastToDieSniperArrow(ArrowProjectileEntity arrow, float x = 0f, float y = 0f) => _host.TryExplodeLastToDieSniperArrow(arrow, x, y);
    private bool TryExplodeLastToDieMedicJavelin(MedicHealNeedleProjectileEntity needle, float x = 0f, float y = 0f) => _host.TryExplodeLastToDieMedicJavelin(needle);
    private void TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity player, float dx, float dy) => _host.TrySpawnExperimentalDemoknightDecapitationRemains(player, dx, dy);
}
