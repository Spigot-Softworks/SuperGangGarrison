namespace OpenGarrison.Core;

// Test-only entry points. Tests call these instead of reflecting on private state.
// Seams that belong to one system live in that system's *.TestSeams.cs partial.
public sealed partial class SimulationWorld
{
    internal void TestSetLevel(SimpleLevel level)
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

    // Swaps the level without the reset TestSetLevel performs.
    internal void TestReplaceLevel(SimpleLevel level) => Level = level;

    internal void TestSetMatchState(MatchState matchState) => MatchState = matchState;

    internal void TestSetFrame(long frame) => Frame = frame;

    internal void TestAddSentry(SentryEntity sentry)
    {
        WorldObjects.Sentries.Add(sentry);
        EntityStore.Set(sentry.Id, sentry);
    }

    internal void TestExplodeRocket(PlayerEntity owner, float x, float y)
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

    internal void TestExplodeRocket(RocketProjectileEntity rocket)
    {
        ExplosionRules.ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
    }

    internal RocketProjectileEntity TestSpawnRocket(PlayerEntity owner, float x, float y, float speed = 0f, float directionRadians = 0f)
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

    internal MineProjectileEntity TestSpawnMine(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f, bool stickied = false)
    {
        var mine = new MineProjectileEntity(AllocateEntityId(), owner.Team, owner.Id, x, y, velocityX, velocityY);
        if (stickied)
        {
            mine.Stick();
        }

        Projectiles.AddProjectileEntity(mine);
        return mine;
    }

    internal GrenadeProjectileEntity TestSpawnGrenade(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f)
    {
        var grenade = new GrenadeProjectileEntity(AllocateEntityId(), owner.Team, owner.Id, x, y, velocityX, velocityY);
        Projectiles.AddProjectileEntity(grenade);
        return grenade;
    }

    internal void TestExplodeGrenade(GrenadeProjectileEntity grenade)
    {
        ExplodeGrenade(grenade);
    }

    internal FlameProjectileEntity TestSpawnFlame(PlayerEntity owner, float x, float y, float velocityX = 0f, float velocityY = 0f)
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

    internal FlareProjectileEntity TestSpawnFlare(
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

    internal void TestExplodeMine(MineProjectileEntity mine)
    {
        ExplosionRules.ExplodeMine(mine);
    }

    internal bool TestHasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => GeometryResolver.HasLineOfSight(attacker, target);

    internal bool TestHasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => GeometryResolver.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    internal bool TestIsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam)
        => GeometryResolver.IsProjectileSpawnBlocked(originX, originY, targetX, targetY, shotTeam);

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, int HitDamageableZoneRoomObjectIndex)? TestGetNearestStabHit(
        StabMaskEntity mask,
        float directionX,
        float directionY)
    {
        var hit = GeometryResolver.GetNearestStabHit(mask, directionX, directionY);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitDamageableZoneRoomObjectIndex)
            : null;
    }

    internal (float Left, float Top, float Right, float Bottom) TestGetPlayerPresentationHitBounds(PlayerEntity player)
    {
        PresentationBounds.GetPlayerPresentationHitBounds(player, out var left, out var top, out var right, out var bottom);
        return (left, top, right, bottom);
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? TestGetNearestShotHit(
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

    internal (float Distance, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator) TestResolveRifleHit(
        PlayerEntity attacker,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = GeometryResolver.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
        return (hit.Distance, hit.HitPlayer, hit.HitSentry, hit.HitGenerator);
    }

    internal bool TestIsFriendlyPlayerFirstRifleContact(
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

    internal void TestAddCivvieMoneyPickup(
        int ownerPlayerId,
        PlayerTeam team,
        float x,
        float y,
        int ticksRemaining = CivvieMoneyTrailRules.PickupLifetimeTicks)
    {
        CombatRuntime.CivvieMoneyTrailTracker.TestAddPickup(ownerPlayerId, team, x, y, ticksRemaining);
    }

    internal int TestCivvieMoneyPickupCount => CombatRuntime.CivvieMoneyTrailTracker.PickupCount;
}
