namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
    {
        var shot = new ShotProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            killFeedWeaponSpriteNameOverride);
        _shots.Add(shot);
        _entities.Add(shot.Id, shot);
    }

    private void SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
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
        owner.IncrementQuoteBubbleCount();
        _bubbles.Add(bubble);
        _entities.Add(bubble.Id, bubble);
    }

    private void SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage)
    {
        var blade = new BladeProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            hitDamage);
        owner.IncrementQuoteBladeCount();
        _blades.Add(blade);
        _entities.Add(blade.Id, blade);
    }

    private void SpawnNeedle(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
    {
        var needle = new NeedleProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY);
        _needles.Add(needle);
        _entities.Add(needle.Id, needle);
    }

    private void SpawnRevolverShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
    {
        var shot = new RevolverProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            killFeedWeaponSpriteNameOverride);
        _revolverShots.Add(shot);
        _entities.Add(shot.Id, shot);
    }

    private void SpawnStabAnimation(PlayerEntity owner, float directionDegrees)
    {
        var stabAnimation = new StabAnimEntity(
            AllocateEntityId(),
            owner.Id,
            owner.Team,
            owner.X,
            owner.Y,
            directionDegrees);
        _stabAnimations.Add(stabAnimation);
        _entities.Add(stabAnimation.Id, stabAnimation);
        RegisterVisualEffect(
            owner.Team == PlayerTeam.Blue ? "BackstabBlue" : "BackstabRed",
            owner.X,
            owner.Y,
            directionDegrees,
            owner.Id);
    }

    private void SpawnStabMask(PlayerEntity owner, float directionDegrees)
    {
        var stabMask = new StabMaskEntity(
            AllocateEntityId(),
            owner.Id,
            owner.Team,
            owner.X,
            owner.Y,
            directionDegrees);
        _stabMasks.Add(stabMask);
        _entities.Add(stabMask.Id, stabMask);
        RegisterWorldSoundEvent("KnifeSnd", stabMask.X, stabMask.Y);
    }

    private void SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
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
            isPerseverant: _random.Next(8) == 0);
        _flames.Add(flame);
        _entities.Add(flame.Id, flame);
    }

    private void SpawnFlare(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
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
        _entities.Add(flare.Id, flare);
    }

    private void SpawnRocket(
        PlayerEntity owner,
        float x,
        float y,
        float speed,
        float directionRadians,
        RocketCombatDefinition? rocketCombat = null,
        bool explodeImmediately = false,
        bool canGrantExperimentalInstantReloadOnHit = true,
        string? killFeedWeaponSpriteNameOverride = null)
    {
        var rocket = new RocketProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            speed,
            directionRadians,
            rocketCombat: rocketCombat,
            rangeAnchorOwnerId: owner.Id,
            lastKnownRangeOriginX: owner.X,
            lastKnownRangeOriginY: owner.Y,
            canGrantExperimentalInstantReloadOnHit: canGrantExperimentalInstantReloadOnHit,
            killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride);
        if (explodeImmediately)
        {
            rocket.DelayExplosionUntilNextTick();
        }

        _rockets.Add(rocket);
        _entities.Add(rocket.Id, rocket);
        _pendingNewRocketIds.Add(rocket.Id);
    }

    private void AdvancePendingRocketsForOwner(int ownerId)
    {
        RocketProjectileSystem.AdvancePendingForOwner(this, ownerId);
    }

    private void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
    {
        var mine = new MineProjectileEntity(
            AllocateEntityId(),
            owner.Team,
            owner.Id,
            x,
            y,
            velocityX,
            velocityY,
            killFeedWeaponSpriteNameOverride);
        _mines.Add(mine);
        _entities.Add(mine.Id, mine);
    }

    private int GetSimulationTicksFromSourceTicks(float sourceTicks)
    {
        return Math.Max(1, (int)MathF.Ceiling(sourceTicks * Config.TicksPerSecond / LegacyMovementModel.SourceTicksPerSecond));
    }
}
