namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly record struct ShotHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct FlameHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct RocketHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct MineHitResult(float Distance, float HitX, float HitY, bool DestroyOnHit);
    private readonly record struct RifleHitResult(float Distance, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct RectangleHitbox(float Left, float Top, float Right, float Bottom);

    internal void CombatTestSetLevel(SimpleLevel level)
    {
        Level = level;
        MatchRules = CreateDefaultMatchRules(level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
        ResetModeStateForNewRound();
    }

    internal void CombatTestAddSentry(SentryEntity sentry)
    {
        _sentries.Add(sentry);
        _entities[sentry.Id] = sentry;
    }

    internal DeadBodyEntity CombatTestSpawnDeadBody(PlayerEntity sourcePlayer, float x, float y, float horizontalSpeed = 0f, float verticalSpeed = 0f, bool? facingLeft = null)
    {
        var deadBody = new DeadBodyEntity(
            AllocateEntityId(),
            sourcePlayer.ClassId,
            sourcePlayer.Team,
            x,
            y,
            sourcePlayer.Width,
            sourcePlayer.Height,
            horizontalSpeed,
            verticalSpeed,
            facingLeft ?? sourcePlayer.FacingDirectionX < 0f);
        _deadBodies.Add(deadBody);
        _entities[deadBody.Id] = deadBody;
        return deadBody;
    }

    internal PlayerGibEntity CombatTestSpawnPlayerGib(
        string spriteName,
        int frameIndex,
        float x,
        float y,
        float velocityX = 0f,
        float velocityY = 0f,
        float rotationSpeedDegrees = 0f,
        int lifetimeTicks = 250,
        float bloodChance = PlayerGibEntity.DefaultBloodChance)
    {
        var gib = new PlayerGibEntity(
            AllocateEntityId(),
            spriteName,
            frameIndex,
            x,
            y,
            velocityX,
            velocityY,
            rotationSpeedDegrees,
            horizontalFriction: 0.4f,
            rotationFriction: 0.6f,
            lifetimeTicks,
            bloodChance);
        _playerGibs.Add(gib);
        _entities[gib.Id] = gib;
        return gib;
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
        _rockets.Add(rocket);
        _entities[rocket.Id] = rocket;
        ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
    }

    internal void CombatTestExplodeRocket(RocketProjectileEntity rocket)
    {
        ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
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
        _rockets.Add(rocket);
        _entities[rocket.Id] = rocket;
        return rocket;
    }

    internal MineProjectileEntity CombatTestSpawnMine(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f, bool stickied = false)
    {
        var mine = new MineProjectileEntity(AllocateEntityId(), owner.Team, owner.Id, x, y, velocityX, velocityY);
        if (stickied)
        {
            mine.Stick();
        }

        _mines.Add(mine);
        _entities[mine.Id] = mine;
        return mine;
    }

    internal BubbleProjectileEntity CombatTestSpawnBubble(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f)
    {
        var bubble = new BubbleProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            GetSimulationTicksFromSourceTicks(BubbleProjectileEntity.LifetimeTicks));
        _bubbles.Add(bubble);
        _entities[bubble.Id] = bubble;
        return bubble;
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
            GetSimulationTicksFromSourceTicks(FlameProjectileEntity.AirLifetimeTicks));
        _flames.Add(flame);
        _entities[flame.Id] = flame;
        return flame;
    }

    internal FlareProjectileEntity CombatTestSpawnFlare(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f)
    {
        var flare = new FlareProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY);
        _flares.Add(flare);
        _entities[flare.Id] = flare;
        return flare;
    }

    internal void CombatTestExplodeMine(MineProjectileEntity mine)
    {
        ExplodeMine(mine);
    }

    internal void CombatTestRecordKillFeedEntry(
        PlayerEntity victim,
        PlayerEntity? killer,
        string weaponSpriteName,
        string? messageText = null,
        KillFeedSpecialType specialType = KillFeedSpecialType.None)
    {
        RecordKillFeedEntry(victim, killer, weaponSpriteName, messageText, specialType);
    }

    internal bool CombatTestHasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => Combat.HasLineOfSight(attacker, target);

    internal bool CombatTestHasSentryLineOfSight(SentryEntity sentry, PlayerEntity target)
        => Combat.HasSentryLineOfSight(sentry, target);

    internal bool CombatTestHasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        => Combat.HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);

    internal bool CombatTestHasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => Combat.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    internal bool CombatTestIsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team)
        => Combat.IsFlameSpawnBlocked(originX, originY, spawnX, spawnY, team);

    internal bool CombatTestIsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY)
        => Combat.IsProjectileSpawnBlocked(originX, originY, targetX, targetY);

    internal static float? CombatTestGetLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance)
        => CombatResolver.GetLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance);

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestShotHit(
        ShotProjectileEntity shot,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestShotHit(shot, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestNeedleHit(
        NeedleProjectileEntity needle,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestNeedleHit(needle, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestRevolverHit(
        RevolverProjectileEntity shot,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestRevolverHit(shot, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestBladeHit(
        BladeProjectileEntity blade,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestBladeHit(blade, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry)? CombatTestGetNearestStabHit(
        StabMaskEntity mask,
        float directionX,
        float directionY)
    {
        var hit = Combat.GetNearestStabHit(mask, directionX, directionY);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestRocketHit(
        RocketProjectileEntity rocket,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestRocketHit(rocket, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, bool DestroyOnHit)? CombatTestGetNearestMineHit(
        MineProjectileEntity mine,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestMineHit(mine, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.DestroyOnHit)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestFlameHit(
        FlameProjectileEntity flame,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestFlameHit(flame, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator) CombatTestResolveRifleHit(
        PlayerEntity attacker,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
        return (hit.Distance, hit.HitPlayer, hit.HitSentry, hit.HitGenerator);
    }

    private bool HasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => Combat.HasLineOfSight(attacker, target);

    private bool HasSentryLineOfSight(SentryEntity sentry, PlayerEntity target)
        => Combat.HasSentryLineOfSight(sentry, target);

    private bool HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        => Combat.HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);

    private bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => Combat.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    private bool IsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team)
        => Combat.IsFlameSpawnBlocked(originX, originY, spawnX, spawnY, team);

    private bool IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY)
        => Combat.IsProjectileSpawnBlocked(originX, originY, targetX, targetY);

    private static float? GetLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance)
        => CombatResolver.GetLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance);

    private ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestShotHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestNeedleHit(needle, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestRevolverHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestBladeHit(blade, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        => Combat.GetNearestStabHit(mask, directionX, directionY);

    private RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestRocketHit(rocket, directionX, directionY, maxDistance);

    private MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestMineHit(mine, directionX, directionY, maxDistance);

    private FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestFlameHit(flame, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestFlareHit(flare, directionX, directionY, maxDistance);

    private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
        => Combat.ResolveRifleHit(attacker, directionX, directionY, maxDistance);

    private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance)
        => Combat.ResolveRifleHit(attacker, originX, originY, directionX, directionY, maxDistance);
}
