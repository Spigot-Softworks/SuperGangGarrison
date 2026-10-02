namespace OpenGarrison.Core;

internal sealed partial class WeaponFireHandler
{
    private const float CivvieUmbrellaPivotXOffset = -6f;
    private const float CivvieUmbrellaPivotYOffset = -7f;
    private const float CivvieUmbrellaTipForwardOffset = 33f;

    private readonly IWeaponFireHost _host;

    public WeaponFireHandler(IWeaponFireHost host)
    {
        _host = host;
    }

    private Random _random => _host.Random;

    private SimulationConfig Config => _host.Config;

    private void RegisterCombatTrace(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float distance,
        bool hitCharacter,
        PlayerTeam team,
        bool isSniperTracer = false,
        bool isCritical = false)
    {
        _host.RegisterCombatTrace(originX, originY, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    }

    private void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1)
    {
        _host.RegisterBloodEffect(x, y, directionDegrees, count);
    }

    private void RegisterImpactEffect(float x, float y, float directionDegrees)
    {
        _host.RegisterImpactEffect(x, y, directionDegrees);
    }

    private void RegisterSoundEvent(PlayerEntity attacker, string soundName)
    {
        _host.RegisterSoundEvent(attacker, soundName);
    }

    private bool ApplyPlayerDamage(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        bool? targetWasGrounded = null)
    {
        return _host.ApplyPlayerDamageWithContext(
            target,
            damage,
            attacker,
            spyRevealAlpha,
            targetWasGrounded: targetWasGrounded);
    }

    private bool ApplySentryDamage(SentryEntity sentry, int damage, PlayerEntity? attacker)
    {
        return _host.ApplySentryDamage(sentry, damage, attacker);
    }

    private bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker)
    {
        return _host.TryDamageGenerator(targetTeam, damage, attacker);
    }

    private void KillPlayer(
        PlayerEntity player,
        bool gibbed = false,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default)
    {
        _host.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind);
    }

    private void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null)
    {
        _host.DestroySentry(sentry, attacker);
    }

    private int CountOwnedMines(int ownerId)
    {
        return _host.CountOwnedMines(ownerId);
    }

    private bool IsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team)
    {
        return _host.IsFlameSpawnBlocked(originX, originY, spawnX, spawnY, team);
    }

    private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
    {
        return _host.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
    }

    private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance)
    {
        return _host.ResolveRifleHit(attacker, originX, originY, directionX, directionY, maxDistance);
    }

    private OrderedRifleHitResult ResolveOrderedRifleHits(
        PlayerEntity attacker,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        RifleTracePolicy policy)
    {
        return _host.ResolveOrderedRifleHits(
            attacker,
            originX,
            originY,
            directionX,
            directionY,
            maxDistance,
            policy);
    }

    private void SpawnShot(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = ShotProjectileEntity.DamagePerHit,
        bool forceGibOnKill = false,
        string? killFeedWeaponSpriteNameOverride = null,
        float playerKnockbackScale = 1f,
        float? playerSlowMovementMultiplier = null,
        int playerSlowRefreshTicks = 0,
        float? playerKnockbackImpulse = null,
        float playerKnockbackAirborneVerticalScale = 1f,
        float playerKnockbackGroundedVerticalScale = 1f,
        bool isBoomstickPellet = false)
    {
        _host.SpawnShot(
            owner,
            x,
            y,
            velocityX,
            velocityY,
            damagePerHit,
            forceGibOnKill,
            killFeedWeaponSpriteNameOverride,
            playerKnockbackScale: playerKnockbackScale,
            playerSlowMovementMultiplier: playerSlowMovementMultiplier,
            playerSlowRefreshTicks: playerSlowRefreshTicks,
            playerKnockbackImpulse: playerKnockbackImpulse,
            playerKnockbackAirborneVerticalScale: playerKnockbackAirborneVerticalScale,
            playerKnockbackGroundedVerticalScale: playerKnockbackGroundedVerticalScale,
            isBoomstickPellet: isBoomstickPellet);
    }

    private void SpawnBubble(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
    {
        _host.SpawnBubble(owner, x, y, velocityX, velocityY);
    }

    private void SpawnBlade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int hitDamage, int lifetimeTicks = PlayerEntity.QuoteBladeLifetimeTicks)
    {
        _host.SpawnBlade(owner, x, y, velocityX, velocityY, hitDamage, lifetimeTicks);
    }

    private void SpawnFlame(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float directHitDamage = FlameProjectileEntity.DirectHitDamage,
        float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick)
    {
        _host.SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    }

    private void SpawnFlare(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = FlareProjectileEntity.DefaultDamagePerHit,
        string killFeedWeaponSpriteName = "FlareKL",
        FlareProjectileStyle style = FlareProjectileStyle.Standard,
        int lifetimeTicks = FlareProjectileEntity.LifetimeTicks)
    {
        _host.SpawnFlare(owner, x, y, velocityX, velocityY, damagePerHit, killFeedWeaponSpriteName, style, lifetimeTicks);
    }

    private void SpawnRocket(
        PlayerEntity owner,
        float x,
        float y,
        float speed,
        float directionRadians,
        RocketCombatDefinition? rocketCombat = null,
        float directHitHealAmount = 0f,
        bool explodeImmediately = false,
        bool canGrantExperimentalInstantReloadOnHit = true,
        float knockbackScale = 1f,
        bool canIgniteTargets = false,
        bool enableExperimentalStingerTracking = false,
        bool enableExperimentalCaveatTracking = false,
        float experimentalVisualScale = 1f,
        int experimentalTrackingLockTicksRemaining = 0,
        bool isBallistic = false,
        float ballisticGravityPerTick = 0f,
        bool suppressSmokeTrail = false,
        string? killFeedWeaponSpriteNameOverride = null)
    {
        _host.SpawnRocket(
            owner,
            x,
            y,
            speed,
            directionRadians,
            rocketCombat: rocketCombat,
            directHitHealAmount: directHitHealAmount,
            explodeImmediately: explodeImmediately,
            canGrantExperimentalInstantReloadOnHit: canGrantExperimentalInstantReloadOnHit,
            knockbackScale: knockbackScale,
            canIgniteTargets: canIgniteTargets,
            enableExperimentalStingerTracking: enableExperimentalStingerTracking,
            enableExperimentalCaveatTracking: enableExperimentalCaveatTracking,
            experimentalVisualScale: experimentalVisualScale,
            experimentalTrackingLockTicksRemaining: experimentalTrackingLockTicksRemaining,
            isBallistic: isBallistic,
            ballisticGravityPerTick: ballisticGravityPerTick,
            suppressSmokeTrail: suppressSmokeTrail,
            killFeedWeaponSpriteNameOverride: killFeedWeaponSpriteNameOverride);
    }

    private void SpawnRevolverShot(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = RevolverProjectileEntity.DamagePerHit,
        string? killFeedWeaponSpriteNameOverride = null,
        global::OpenGarrison.Core.LastToDie.LastToDieSpyRevolverProfile? lastToDieProfile = null,
        bool forceCritical = false,
        bool appliesLuckyStrikeStun = false,
        float playerKnockbackImpulse = 0f,
        float playerKnockbackAirborneVerticalScale = 1f,
        float playerKnockbackGroundedVerticalScale = 1f)
    {
        _host.SpawnRevolverShot(
            owner,
            x,
            y,
            velocityX,
            velocityY,
            damagePerHit,
            killFeedWeaponSpriteNameOverride,
            lastToDieProfile,
            forceCritical,
            appliesLuckyStrikeStun,
            playerKnockbackImpulse,
            playerKnockbackAirborneVerticalScale,
            playerKnockbackGroundedVerticalScale);
    }

    private void SpawnMine(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
    {
        _host.SpawnMine(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
    }

    private void SpawnGrenade(PlayerEntity owner, float x, float y, float velocityX, float velocityY, string? killFeedWeaponSpriteNameOverride = null)
    {
        _host.SpawnGrenade(owner, x, y, velocityX, velocityY, killFeedWeaponSpriteNameOverride);
    }

    private void SpawnGrenade(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        string? killFeedWeaponSpriteNameOverride,
        bool isStrongDrink,
        int fuseTicks,
        float initialSpinSpeed,
        float gravityPerTick = GrenadeProjectileEntity.StrongDrinkGravityPerTick)
    {
        _host.SpawnGrenade(
            owner,
            x,
            y,
            velocityX,
            velocityY,
            killFeedWeaponSpriteNameOverride,
            isStrongDrink,
            fuseTicks,
            initialSpinSpeed,
            gravityPerTick);
    }

    private void SpawnNeedle(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int damagePerHit = NeedleProjectileEntity.DamagePerHit,
        string killFeedWeaponSpriteName = "NeedleKL")
    {
        _host.SpawnNeedle(
            owner,
            x,
            y,
            velocityX,
            velocityY,
            damagePerHit,
            killFeedWeaponSpriteName);
    }

    private void SpawnMedicHealNeedle(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit,
        int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit)
    {
        _host.SpawnMedicHealNeedle(owner, x, y, velocityX, velocityY, healPerHit, enemyDamagePerHit);
    }

    private void SpawnNail(PlayerEntity owner, float x, float y, float velocityX, float velocityY)
    {
        _host.SpawnNail(owner, x, y, velocityX, velocityY);
    }

    private void SpawnArrow(PlayerEntity owner, float x, float y, float velocityX, float velocityY, int damage, float fakeSpeedMultiplier)
    {
        _host.SpawnArrow(owner, x, y, velocityX, velocityY, damage, fakeSpeedMultiplier);
    }

    private static float DegreesToRadians(float degrees)
    {
        return SimulationMath.DegreesToRadians(degrees);
    }

    private int GetExperimentalProjectilesPerShot(PlayerEntity attacker, int baseCount)
    {
        if (!_host.IsExperimentalPracticePowerOwner(attacker) || _host.ExperimentalGameplaySettings.BonusProjectilesPerShot <= 0)
        {
            return Math.Max(1, baseCount);
        }

        return Math.Max(1, baseCount + _host.ExperimentalGameplaySettings.BonusProjectilesPerShot);
    }

    private static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
    {
        return SimulationMath.PointDirectionDegrees(x1, y1, x2, y2);
    }

    private readonly record struct SourceWeaponOrigin(float BaseX, float BaseY, float WeaponYOffset, float EquipmentOffset);

    private readonly record struct WeaponPivotRay(float PivotX, float PivotY, float DirectionX, float DirectionY, float AngleRadians);

    private SourceWeaponOrigin GetSourceWeaponOrigin(PlayerEntity attacker)
    {
        return GetSourceWeaponOrigin(attacker, attacker.ClassId);
    }

    private SourceWeaponOrigin GetSourceWeaponOrigin(PlayerEntity attacker, PlayerClass weaponClassId)
    {
        return new SourceWeaponOrigin(
            MathF.Round(attacker.X),
            MathF.Round(attacker.Y),
            GetSourceWeaponYOffset(weaponClassId),
            GetSourceEquipmentOffset(attacker));
    }

    private static float GetPyroOriginY(SourceWeaponOrigin weaponOrigin)
    {
        return weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + weaponOrigin.EquipmentOffset;
    }

    private static WeaponPivotRay GetWeaponPivotRay(
        float originX,
        float originY,
        float aimWorldX,
        float aimWorldY,
        float fallbackFacingDirectionX,
        float pivotOffsetX,
        float pivotOffsetY)
    {
        var aimDeltaX = aimWorldX - originX;
        var aimDeltaY = aimWorldY - originY;
        if (aimDeltaX == 0f && aimDeltaY == 0f)
        {
            aimDeltaX = fallbackFacingDirectionX == 0f ? 1f : fallbackFacingDirectionX;
        }

        var initialAngle = DeterministicMath.Atan2(aimDeltaY, aimDeltaX);
        var facingScale = DeterministicMath.Cos(initialAngle) < 0f ? -1f : 1f;
        var pivotX = originX + (pivotOffsetX * facingScale);
        var pivotY = originY + pivotOffsetY;

        var pivotAimDeltaX = aimWorldX - pivotX;
        var pivotAimDeltaY = aimWorldY - pivotY;
        if (pivotAimDeltaX == 0f && pivotAimDeltaY == 0f)
        {
            pivotAimDeltaX = facingScale;
        }

        var angleRadians = DeterministicMath.Atan2(pivotAimDeltaY, pivotAimDeltaX);
        return new WeaponPivotRay(
            pivotX,
            pivotY,
            DeterministicMath.Cos(angleRadians),
            DeterministicMath.Sin(angleRadians),
            angleRadians);
    }

    private static (float X, float Y) GetPointAlongWeaponPivotRay(WeaponPivotRay pivotRay, float distance)
    {
        return (
            pivotRay.PivotX + (pivotRay.DirectionX * distance),
            pivotRay.PivotY + (pivotRay.DirectionY * distance));
    }

    public (float X, float Y) GetPyroSecondaryOrigin(PlayerEntity attacker)
    {
        var weaponClassId = attacker.IsAcquiredWeaponEquipped && attacker.AcquiredWeaponClassId == PlayerClass.Pyro
            ? PlayerClass.Pyro
            : attacker.ClassId;
        var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
        return (weaponOrigin.BaseX, GetPyroOriginY(weaponOrigin));
    }

    public (float X, float Y, float AimRadians) GetSoldierRocketLauncherTip(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        var weaponOrigin = GetSourceWeaponOrigin(attacker, PlayerClass.Soldier);
        var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
        var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
        if (aimDeltaX == 0f && aimDeltaY == 0f)
        {
            aimDeltaX = attacker.FacingDirectionX == 0f ? 1f : attacker.FacingDirectionX;
        }

        var aimRadians = DeterministicMath.Atan2(aimDeltaY, aimDeltaX);
        return (
            weaponOrigin.BaseX + DeterministicMath.Cos(aimRadians) * 20f,
            weaponOrigin.BaseY + DeterministicMath.Sin(aimRadians) * 20f,
            aimRadians);
    }

    public (float SourceX, float SourceY, float TipX, float TipY, float AimRadians) GetCivvieUmbrellaRay(
        PlayerEntity player,
        float aimWorldX,
        float aimWorldY)
    {
        var weaponOrigin = GetSourceWeaponOrigin(player, PlayerClass.Quote);
        var aimDeltaXFromPlayer = aimWorldX - weaponOrigin.BaseX;
        var facingScale = MathF.Abs(aimDeltaXFromPlayer) > 0.001f
            ? (aimDeltaXFromPlayer < 0f ? -1f : 1f)
            : (player.FacingDirectionX < 0f ? -1f : 1f);
        var sourceX = weaponOrigin.BaseX + (CivvieUmbrellaPivotXOffset * facingScale);
        var sourceY = weaponOrigin.BaseY + CivvieUmbrellaPivotYOffset;
        var aimRadians = SimulationMath.PointDirectionRadians(sourceX, sourceY, aimWorldX, aimWorldY, player.FacingDirectionX);
        return (
            sourceX,
            sourceY,
            sourceX + DeterministicMath.Cos(aimRadians) * CivvieUmbrellaTipForwardOffset,
            sourceY + DeterministicMath.Sin(aimRadians) * CivvieUmbrellaTipForwardOffset,
            aimRadians);
    }

    public (float X, float Y, float AimRadians) GetCivvieUmbrellaTip(PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var ray = GetCivvieUmbrellaRay(player, aimWorldX, aimWorldY);
        return (ray.TipX, ray.TipY, ray.AimRadians);
    }

    private static bool IsCivvieUmbrellaPrimary(PrimaryWeaponDefinition weaponDefinition, PlayerClass weaponClassId)
    {
        return weaponClassId == PlayerClass.Quote
            && CharacterClassCatalog.RuntimeRegistry.TryResolvePrimaryWeaponItemId(weaponDefinition, out var itemId)
            && string.Equals(itemId, "weapon.umbrella", StringComparison.Ordinal);
    }

    private static float GetSourceWeaponYOffset(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => -4f,
            PlayerClass.Engineer => -2f,
            PlayerClass.Pyro => 4f,
            PlayerClass.Soldier => -10f,
            PlayerClass.Demoman => -2f,
            PlayerClass.Heavy => 0f,
            PlayerClass.Sniper => -8f,
            PlayerClass.Medic => 0f,
            PlayerClass.Spy => -6f,
            PlayerClass.Quote => -3f,
            _ => 0f,
        };
    }

    private float GetSourceEquipmentOffset(PlayerEntity attacker)
    {
        var horizontalSourceStepSpeed = MathF.Abs(attacker.HorizontalSpeed) / LegacyMovementModel.SourceTicksPerSecond;
        if (!attacker.IsGrounded
            || attacker.IsTaunting
            || attacker.ClassId == PlayerClass.Quote
            || attacker.IsSniperScoped
            || horizontalSourceStepSpeed >= 0.2f)
        {
            return 0f;
        }

        // The simulation does not track the body animation phase yet, so this keeps the stable pose-based
        // portion of the source equipment offset aligned without introducing guessed run-bob phases.
        return GetSourceLeanYOffset(attacker);
    }

    private float GetSourceLeanYOffset(PlayerEntity attacker)
    {
        var bottom = attacker.Bottom + 2f;
        var openRight = !IsPointBlockedForPlayer(attacker, attacker.X + 6f, bottom)
            && !IsPointBlockedForPlayer(attacker, attacker.X + 2f, bottom);
        var openLeft = !IsPointBlockedForPlayer(attacker, attacker.X - 7f, bottom)
            && !IsPointBlockedForPlayer(attacker, attacker.X - 3f, bottom);

        if (openRight && openLeft)
        {
            openRight = !IsPointBlockedForPlayer(attacker, attacker.Right - 1f, bottom);
            openLeft = !IsPointBlockedForPlayer(attacker, attacker.Left, bottom);
        }

        return openRight ^ openLeft ? 6f : 0f;
    }

    private bool IsPointBlockedForPlayer(PlayerEntity player, float x, float y)
    {
        foreach (var solid in _host.Level.Solids)
        {
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return true;
            }
        }

        foreach (var gate in _host.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
            {
                return true;
            }
        }

        foreach (var wall in _host.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
            {
                return true;
            }
        }

        if (SimpleLevelBarrierCollision.BlocksPointForPlayer(_host.Level, player.Team, player.IsCarryingIntel, x, y))
        {
            return true;
        }

        return false;
    }

}
