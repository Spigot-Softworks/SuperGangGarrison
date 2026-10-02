namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    internal void CombatTestSetLevel(SimpleLevel level)
    {
        Level = level;
        MatchRules = CreateDefaultMatchRules(level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
        MapLogic.RebuildForegroundJungleSpriteCache();
        ResetModeStateForNewRound();
        ResetMovingPlatformsForLevel();
        Pickups.ResetHealthPackSpawnsForLevel();
        Structures.ResetJumpPadSpawnsForLevel();
    }

    // Swaps the level without the reset CombatTestSetLevel performs.
    internal void CombatTestReplaceLevel(SimpleLevel level) => Level = level;

    internal void CombatTestSetMatchState(MatchState matchState) => MatchState = matchState;

    internal void CombatTestSetFrame(long frame) => Frame = frame;

    internal void CombatTestAddSentry(SentryEntity sentry)
    {
        WorldObjects.Sentries.Add(sentry);
        EntityStore.Set(sentry.Id, sentry);
    }

    internal void CombatTestExplodeRocket(PlayerEntity owner, float x, float y)
    {
        var rocket = new RocketProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            0f,
            0f,
            rangeAnchorOwnerId: owner.Id,
            lastKnownRangeOriginX: owner.X,
            lastKnownRangeOriginY: owner.Y);
        Projectiles.AddProjectileEntity(rocket);
        ExplosionRules.ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
    }

    internal void CombatTestExplodeRocket(RocketProjectileEntity rocket)
    {
        ExplosionRules.ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
    }

    internal RocketProjectileEntity CombatTestSpawnRocket(PlayerEntity owner, float x, float y, float speed = 0f, float directionRadians = 0f)
    {
        var rocket = new RocketProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            speed,
            directionRadians,
            rangeAnchorOwnerId: owner.Id,
            lastKnownRangeOriginX: owner.X,
            lastKnownRangeOriginY: owner.Y);
        Projectiles.AddProjectileEntity(rocket);
        return rocket;
    }

    internal MineProjectileEntity CombatTestSpawnMine(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f, bool stickied = false)
    {
        var mine = new MineProjectileEntity(AllocateEntityId(), owner.Team, owner.Id, x, y, velocityX, velocityY);
        if (stickied)
        {
            mine.Stick();
        }

        Projectiles.AddProjectileEntity(mine);
        return mine;
    }

    internal GrenadeProjectileEntity CombatTestSpawnGrenade(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f)
    {
        var grenade = new GrenadeProjectileEntity(AllocateEntityId(), owner.Team, owner.Id, x, y, velocityX, velocityY);
        Projectiles.AddProjectileEntity(grenade);
        return grenade;
    }

    internal void CombatTestExplodeGrenade(GrenadeProjectileEntity grenade)
    {
        ExplodeGrenade(grenade);
    }

    internal FlameProjectileEntity CombatTestSpawnFlame(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f)
    {
        var flame = new FlameProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            GetSimulationTicksFromSourceTicks(FlameProjectileEntity.AirLifetimeTicks),
            isPerseverant: false);
        Projectiles.AddProjectileEntity(flame);
        return flame;
    }

    internal FlareProjectileEntity CombatTestSpawnFlare(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX = 0f,
        float velocityY = 0f,
        float damagePerHit = FlareProjectileEntity.DefaultDamagePerHit,
        FlareProjectileStyle style = FlareProjectileStyle.Standard,
        int lifetimeTicks = FlareProjectileEntity.LifetimeTicks)
    {
        var flare = new FlareProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            ticksRemaining: lifetimeTicks,
            damagePerHit: damagePerHit,
            style: style);
        Projectiles.AddProjectileEntity(flare);
        return flare;
    }

    internal void CombatTestExplodeMine(MineProjectileEntity mine)
    {
        ExplosionRules.ExplodeMine(mine);
    }

    internal bool CombatTestHasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => GeometryResolver.HasLineOfSight(attacker, target);

    internal bool CombatTestHasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => GeometryResolver.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    internal bool CombatTestIsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam)
        => GeometryResolver.IsProjectileSpawnBlocked(originX, originY, targetX, targetY, shotTeam);

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestShotHit(
        ShotProjectileEntity shot,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = GeometryResolver.GetNearestShotHit(shot, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Left, float Top, float Right, float Bottom) CombatTestGetPlayerPresentationHitBounds(PlayerEntity player)
    {
        GetPlayerPresentationHitBounds(this, player, out var left, out var top, out var right, out var bottom);
        return (left, top, right, bottom);
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, int HitDamageableZoneRoomObjectIndex)? CombatTestGetNearestStabHit(
        StabMaskEntity mask,
        float directionX,
        float directionY)
    {
        var hit = GeometryResolver.GetNearestStabHit(mask, directionX, directionY);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitDamageableZoneRoomObjectIndex)
            : null;
    }

    internal (float Distance, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator) CombatTestResolveRifleHit(
        PlayerEntity attacker,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = GeometryResolver.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
        return (hit.Distance, hit.HitPlayer, hit.HitSentry, hit.HitGenerator);
    }

    internal bool CombatTestIsFriendlyPlayerFirstRifleContact(
        PlayerEntity attacker,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance)
    {
        return GeometryResolver.IsFriendlyPlayerFirstRifleContact(
            attacker,
            originX,
            originY,
            directionX,
            directionY,
            maxDistance);
    }

    private bool HasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => GeometryResolver.HasLineOfSight(attacker, target);

    private bool HasSentryLineOfSight(SentryEntity sentry, PlayerEntity target)
        => GeometryResolver.HasSentryLineOfSight(sentry, target);

    private bool HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        => GeometryResolver.HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);

    private bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => GeometryResolver.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    private bool IsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team)
        => GeometryResolver.IsFlameSpawnBlocked(originX, originY, spawnX, spawnY, team);

    private bool IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam)
        => GeometryResolver.IsProjectileSpawnBlocked(originX, originY, targetX, targetY, shotTeam);

    private bool IsProjectilePathBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam)
        => GeometryResolver.IsProjectilePathBlocked(originX, originY, targetX, targetY, shotTeam);

    private float? GetLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance)
        => GeometryResolver.GetLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance);

    private float? GetThickLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance,
        float thicknessRadius)
        => GeometryResolver.GetThickLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance, thicknessRadius);

    private ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestShotHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestNeedleHit(needle, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestMedicHealNeedleHit(MedicHealNeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestMedicHealNeedleHit(needle, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestRevolverHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestBladeHit(blade, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        => GeometryResolver.GetNearestStabHit(mask, directionX, directionY);

    private ShotHitResult? GetNearestHealstabHit(StabMaskEntity mask, float directionX, float directionY)
        => GeometryResolver.GetNearestHealstabHit(mask, directionX, directionY);

    private bool HasStabChainLineOfSight(float originX, float originY, float targetX, float targetY)
        => GeometryResolver.HasStabChainLineOfSight(originX, originY, targetX, targetY);

    private RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestRocketHit(rocket, directionX, directionY, maxDistance);

    private MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestMineHit(mine, directionX, directionY, maxDistance);

    private GrenadeEnvironmentHit? GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestGrenadeEnvironmentHit(grenade, directionX, directionY, maxDistance);

    private bool TryGetGrenadeDamageableZoneContact(
        GrenadeProjectileEntity grenade,
        float directionX,
        float directionY,
        float maxDistance,
        out float hitX,
        out float hitY,
        out int roomObjectIndex)
        => GeometryResolver.TryGetGrenadeDamageableZoneContact(grenade, directionX, directionY, maxDistance, out hitX, out hitY, out roomObjectIndex);

    private PlayerEntity? GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestGrenadePlayerHit(grenade, directionX, directionY, maxDistance);

    private FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestFlameHit(flame, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestFlareHit(flare, directionX, directionY, maxDistance);

    private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
        => GeometryResolver.ResolveRifleHit(attacker, directionX, directionY, maxDistance);

    private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance)
        => GeometryResolver.ResolveRifleHit(attacker, originX, originY, directionX, directionY, maxDistance);

    private OrderedRifleHitResult ResolveOrderedRifleHits(
        PlayerEntity attacker,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        RifleTracePolicy policy)
        => GeometryResolver.ResolveOrderedRifleHits(
            attacker,
            originX,
            originY,
            directionX,
            directionY,
            maxDistance,
            policy);
}
