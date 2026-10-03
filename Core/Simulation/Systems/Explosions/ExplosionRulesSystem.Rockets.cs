namespace OpenGarrison.Core;

internal sealed partial class ExplosionRulesSystem
{
    internal void ExplodeRocket(
        RocketProjectileEntity rocket,
        PlayerEntity? directHitPlayer,
        SentryEntity? directHitSentry,
        GeneratorState? directHitGenerator,
        int directHitDamageableZoneRoomObjectIndex = -1)
    {
        if (!rocket.TryMarkExplosionConsumed())
        {
            return;
        }

        var owner = _host.FindPlayerById(rocket.OwnerId);
        var blastRadius = CombatSystem.ResolveExplosiveSplashRadius(
            rocket.BlastRadiusValue
                * rocket.ExperimentalStingerBlastRadiusMultiplier
                * MathF.Max(0.1f, owner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));
        RemoveExplodedRocket(rocket.Id);
        if (_host.ClientPredictionMode)
        {
            _host.WorldEffects.RegisterWorldSoundEvent("ExplosionSnd", rocket.X, rocket.Y);
            _host.WorldEffects.RegisterVisualEffect("Explosion", rocket.X, rocket.Y);
            return;
        }

        var hitEnemyPlayer = ApplyDirectHitDamage(rocket,
            owner,
            directHitPlayer,
            directHitSentry,
            directHitGenerator,
            directHitDamageableZoneRoomObjectIndex);

        _host.WorldEffects.RegisterWorldSoundEvent("ExplosionSnd", rocket.X, rocket.Y);
        _host.WorldEffects.RegisterVisualEffect("Explosion", rocket.X, rocket.Y);
        ApplyDeadBodyExplosionImpulse(rocket.X, rocket.Y, blastRadius, 10f);
        ApplyPlayerGibExplosionImpulse(rocket.X, rocket.Y, blastRadius, 15f);
        RegisterExplosionTraces(rocket.X, rocket.Y);

        hitEnemyPlayer |= ApplySplashDamageToPlayers(rocket, owner, blastRadius, directHitPlayer);
        ApplySplashDamageToSentries(rocket, owner, blastRadius);
        ApplySplashDamageToGenerators(rocket, owner, blastRadius);
        ApplyExplosiveDamageToJumpPads(
            rocket.X,
            rocket.Y,
            blastRadius,
            rocket.ExplosionDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier,
            rocket.Team,
            rocket.MinimumSplashDamageValue);
        ApplySplashDamageToDamageableZones(rocket, blastRadius, directHitDamageableZoneRoomObjectIndex);
        TriggerMinesInBlast(rocket, blastRadius);
        DestroyBubblesInBlast(rocket, blastRadius);
        _host.ExperimentalRules.TryApplyExperimentalSoldierRocketHitReloadReward(owner, rocket, hitEnemyPlayer);
    }

    private void RemoveExplodedRocket(int rocketId)
    {
        for (var rocketIndex = _host.Rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            if (_host.Rockets[rocketIndex].Id == rocketId)
            {
                _host.RemoveRocketAt(rocketIndex);
                break;
            }
        }
    }

    private bool ApplyDirectHitDamage(
        RocketProjectileEntity rocket,
        PlayerEntity? owner,
        PlayerEntity? directHitPlayer,
        SentryEntity? directHitSentry,
        GeneratorState? directHitGenerator,
        int directHitDamageableZoneRoomObjectIndex)
    {
        var hitEnemyPlayer = false;
        if (directHitPlayer is not null && !ReferenceEquals(directHitPlayer, owner))
        {
            hitEnemyPlayer = directHitPlayer.Team != rocket.Team;
            var hitDamage = _host.ExperimentalRules.ApplyExperimentalAirshotDamageMultiplier(
                owner,
                directHitPlayer,
                Math.Max(1, (int)MathF.Round(rocket.DirectHitDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier)),
                out var damageFlags);
            var infiltrateBlockedDirectHit =
                directHitPlayer.IsLastToDieSpyInfiltrateProjectileImmune;
            if (_host.ApplyPlayerDamageWithContext(
                    directHitPlayer,
                    hitDamage,
                    owner,
                    PlayerEntity.SpyDamageRevealAlpha,
                    damageFlags,
                    civvieUmbrellaThreatSourceX: rocket.X,
                    civvieUmbrellaThreatSourceY: rocket.Y,
                    civvieUmbrellaDrainTicks: PlayerEntity.CivvieUmbrellaDirectExplosionDrainTicks,
                    civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(rocket.CriticalDamageMultiplier),
                    civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                    additionalTraits: PlayerDamageTraits.DirectProjectile))
            {
                _host.PlayerDeaths.KillPlayer(
                    directHitPlayer,
                    gibbed: true,
                    killer: owner,
                    weaponSpriteName: rocket.KillFeedWeaponSpriteNameOverride ?? "RocketKL");
            }

            if (rocket.CanIgniteTargets
                && directHitPlayer.IsAlive
                && !infiltrateBlockedDirectHit)
            {
                directHitPlayer.IgniteAfterburn(
                    owner?.Id ?? 0,
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierNapalmAfterburnDurationSourceTicks,
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierNapalmAfterburnIntensity,
                    afterburnFalloff: false,
                    burnFalloffAmount: 1f,
                    applyNapalm: true);
            }

            if (hitEnemyPlayer && owner is not null && rocket.DirectHitHealAmountValue > 0f)
            {
                var appliedHealing = _host.DamageRules.ApplyHealingWithFeedback(owner, rocket.DirectHitHealAmountValue);
                _host.Scorekeeping.AwardHealingPoints(owner, appliedHealing);
            }
        }

        if (directHitSentry is not null)
        {
            var sentryDamage = Math.Max(1, (int)MathF.Round(rocket.DirectHitDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier));
            if (_host.Combat.ApplySentryDamage(directHitSentry, sentryDamage, owner))
            {
                _host.Structures.DestroySentry(directHitSentry, owner);
            }
        }

        if (directHitGenerator is not null)
        {
            _host.ObjectiveRules.TryDamageGenerator(
                directHitGenerator.Team,
                rocket.DirectHitDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier,
                owner);
        }

        if (directHitDamageableZoneRoomObjectIndex >= 0)
        {
            _host.MapLogic.TryApplyDamageableZoneDamage(
                directHitDamageableZoneRoomObjectIndex,
                rocket.DirectHitDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier,
                rocket.Team);
        }

        return hitEnemyPlayer;
    }

    private void ApplySplashDamageToDamageableZones(
        RocketProjectileEntity rocket,
        float blastRadius,
        int excludeRoomObjectIndex)
    {
        _host.MapLogic.ApplyExplosiveDamageToDamageableZones(
            rocket.X,
            rocket.Y,
            blastRadius,
            rocket.ExplosionDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier,
            0f,
            excludeRoomObjectIndex,
            rocket.Team,
            rocket.MinimumSplashDamageValue);
    }

    private bool ApplySplashDamageToPlayers(
        RocketProjectileEntity rocket,
        PlayerEntity? owner,
        float blastRadius,
        PlayerEntity? directHitPlayer)
    {
        var hitEnemyPlayer = false;
        var attackerWasGrounded = owner?.IsGrounded;
        var playersSnapshot = _host.EnumerateSimulatedPlayers()
            .Select(player => (Player: player, WasGrounded: player.IsGrounded))
            .ToArray();
        foreach (var playerSnapshot in playersSnapshot)
        {
            var player = playerSnapshot.Player;
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = ReferenceEquals(player, directHitPlayer)
                ? 0f
                : GetExplosionDistanceToPlayer(player, rocket.X, rocket.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            if (ShouldIgnoreFriendlyGroundedBlast(player, rocket.Team, rocket.OwnerId))
            {
                continue;
            }

            var distanceFactor = 1f - (distance / blastRadius);
            if (distanceFactor <= 0f)
            {
                continue;
            }

            if (ShouldSkipFriendlyExplosionBoost(player, rocket.Team, rocket.OwnerId))
            {
                continue;
            }

            if (ReferenceEquals(player, directHitPlayer)
                && ShouldRedirectDescendingMortarDirectHitTowardOwner(player, owner, rocket))
            {
                ApplyDescendingMortarDirectHitImpulse(player, owner!, rocket, distanceFactor);
            }
            else
            {
                ApplyPlayerImpulse(player, rocket, distanceFactor);
            }
            ApplyMovementState(player, rocket);
            var receivedBlastLiftBonus = player.Id != rocket.OwnerId && ShouldApplyBlastLiftBonus(player, rocket.X, rocket.Y);
            if (receivedBlastLiftBonus)
            {
                player.AddImpulse(0f, -4f * distanceFactor * LegacyMovementModel.SourceTicksPerSecond);
            }

            ApplySpeedAdjustments(player, rocket, receivedBlastLiftBonus);

            if (!_host.CanTeamDamagePlayer(rocket.Team, rocket.OwnerId, player))
            {
                continue;
            }

            var critMultiplier = (player.Id == rocket.OwnerId && player.Team == rocket.Team) ? 1f : rocket.CriticalDamageMultiplier;
            var maxSplashDamage = rocket.ExplosionDamageValue * rocket.ExperimentalStingerDamageMultiplier * critMultiplier;
            var minimumSplashDamage = player.Id == rocket.OwnerId && player.Team == rocket.Team
                ? CombatSystem.ExplosiveSplashMinimumDamage
                : rocket.MinimumSplashDamageValue;
            var appliedDamage = CombatSystem.ResolveExplosiveSplashDamage(
                maxSplashDamage,
                distanceFactor,
                minimumSplashDamage);
            if (player.Id == rocket.OwnerId && player.Team == rocket.Team)
            {
                appliedDamage *= rocket.SelfDamageMultiplier;
            }
            _host.WorldEffects.RegisterBloodEffect(player.X, player.Y, SimulationMath.PointDirectionDegrees(rocket.X, rocket.Y, player.X, player.Y) - 180f, 3);
            hitEnemyPlayer |= player.Team != rocket.Team;
            var umbrellaDrainTicks = ReferenceEquals(player, directHitPlayer)
                ? PlayerEntity.CivvieUmbrellaRocketDirectHitSplashDrainTicks
                : PlayerEntity.GetCivvieUmbrellaSplashExplosionDrainTicksFromDamage(appliedDamage, maxSplashDamage);
            if (_host.ApplyPlayerContinuousDamageWithContext(
                    player,
                    appliedDamage,
                    owner,
                    PlayerEntity.SpyDamageRevealAlpha,
                    civvieUmbrellaThreatSourceX: rocket.X,
                    civvieUmbrellaThreatSourceY: rocket.Y,
                    civvieUmbrellaDrainTicks: umbrellaDrainTicks,
                    civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(critMultiplier),
                    civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                    attackerWasGrounded: attackerWasGrounded,
                    targetWasGrounded: playerSnapshot.WasGrounded))
            {
                _host.PlayerDeaths.KillPlayer(
                    player,
                    gibbed: true,
                    killer: owner,
                    weaponSpriteName: rocket.KillFeedWeaponSpriteNameOverride ?? "RocketKL");
            }

            if (rocket.CanIgniteTargets)
            {
                player.IgniteAfterburn(
                    owner?.Id ?? 0,
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierNapalmAfterburnDurationSourceTicks,
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierNapalmAfterburnIntensity,
                    afterburnFalloff: false,
                    burnFalloffAmount: 1f,
                    applyNapalm: true);
            }
        }

        return hitEnemyPlayer;
    }

    private void ApplyPlayerImpulse(PlayerEntity player, RocketProjectileEntity rocket, float distanceFactor)
    {
        var impulse = ExplosionGeometry.GetExplosionImpulseMagnitude(
            player,
            rocket.X,
            rocket.Y,
            rocket.CurrentKnockback,
            distanceFactor,
            useMineVectorProfile: false);
        if (player.Id == rocket.OwnerId
            && player.Team == rocket.Team
            && rocket.EnableExperimentalStingerTracking
            && string.Equals(
                rocket.DelayedExplosionReason,
                RocketProjectileEntity.DelayedExplosionReasonManualDetonation,
                StringComparison.Ordinal))
        {
            impulse *= global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierStingerManualDetonationSelfKnockbackMultiplier;
        }

        ExplosionGeometry.ApplyExplosionImpulse(player, rocket.X, rocket.Y, impulse);
    }

    private bool ShouldRedirectDescendingMortarDirectHitTowardOwner(
        PlayerEntity player,
        PlayerEntity? owner,
        RocketProjectileEntity rocket)
    {
        if (owner is null
            || ReferenceEquals(player, owner)
            || !rocket.IsBallistic
            || DeterministicMath.Sin(rocket.DirectionRadians) <= 0.0001f)
        {
            return false;
        }

        var playerCenterY = player.Y
            + ((player.CollisionTopOffset + player.CollisionBottomOffset) * 0.5f);
        return rocket.Y <= playerCenterY;
    }

    private void ApplyDescendingMortarDirectHitImpulse(
        PlayerEntity player,
        PlayerEntity owner,
        RocketProjectileEntity rocket,
        float distanceFactor)
    {
        var impulse = ExplosionGeometry.GetExplosionImpulseMagnitude(
            player,
            rocket.X,
            rocket.Y,
            rocket.CurrentKnockback,
            distanceFactor,
            useMineVectorProfile: false);
        if (impulse <= 0.0001f)
        {
            return;
        }

        var towardOwnerX = owner.X - player.X;
        var towardOwnerY = owner.Y - player.Y;
        var distanceToOwner = MathF.Sqrt(
            (towardOwnerX * towardOwnerX) + (towardOwnerY * towardOwnerY));
        if (distanceToOwner <= 0.0001f)
        {
            var fallbackDirectionX = -DeterministicMath.Cos(rocket.DirectionRadians);
            player.AddImpulse(MathF.Sign(fallbackDirectionX) * impulse, 0f);
            return;
        }

        player.AddImpulse(
            (towardOwnerX / distanceToOwner) * impulse,
            (towardOwnerY / distanceToOwner) * impulse);
    }

    private void ApplyMovementState(PlayerEntity player, RocketProjectileEntity rocket)
    {
        if (player.Id == rocket.OwnerId && player.Team == rocket.Team)
        {
            player.SetMovementStateIfAirborne(LegacyMovementState.ExplosionRecovery);
            return;
        }

        player.SetMovementStateIfAirborne(player.Team == rocket.Team
            ? LegacyMovementState.FriendlyJuggle
            : LegacyMovementState.RocketJuggle);
    }

    private void ApplySpeedAdjustments(PlayerEntity player, RocketProjectileEntity rocket, bool receivedBlastLiftBonus)
    {
        if (player.Id == rocket.OwnerId && player.Team == rocket.Team)
        {
            player.ScaleVelocity(player.IsUbered ? 1.055f : 1.06f);
            return;
        }

        if (receivedBlastLiftBonus)
        {
            player.ScaleVelocity(1.3f);
        }
    }

    private bool ShouldApplyBlastLiftBonus(PlayerEntity player, float originX, float originY)
    {
        var offsetAngle = ToGameMakerDegrees(SimulationMath.PointDirectionDegrees(player.X, player.Y + 5f, originX, originY - 5f));
        var baseAngle = ToGameMakerDegrees(SimulationMath.PointDirectionDegrees(player.X, player.Y, originX, originY));
        return offsetAngle > 210f && baseAngle < 330f;
    }

    private float ToGameMakerDegrees(float worldDegrees)
    {
        return SimulationMath.NormalizeAngleDegrees(360f - worldDegrees);
    }

    private void ApplySplashDamageToSentries(RocketProjectileEntity rocket, PlayerEntity? owner, float blastRadius)
    {
        for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
            var distance = SimulationMath.DistanceBetween(rocket.X, rocket.Y, sentry.X, sentry.Y);
            if (distance >= blastRadius || sentry.Team == rocket.Team)
            {
                continue;
            }

            var damage = CombatSystem.ResolveExplosiveSplashDamage(
                rocket.ExplosionDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier,
                1f - (distance / blastRadius),
                rocket.MinimumSplashDamageValue);
            if (_host.Combat.ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
            {
                _host.Structures.DestroySentry(sentry, owner);
            }
        }
    }

    private void ApplySplashDamageToGenerators(RocketProjectileEntity rocket, PlayerEntity? owner, float blastRadius)
    {
        for (var generatorIndex = 0; generatorIndex < _host.WorldObjects.Generators.Count; generatorIndex += 1)
        {
            var generator = _host.WorldObjects.Generators[generatorIndex];
            var distance = SimulationMath.DistanceBetween(rocket.X, rocket.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance >= blastRadius || generator.Team == rocket.Team || generator.IsDestroyed)
            {
                continue;
            }

            var damage = CombatSystem.ResolveExplosiveSplashDamage(
                rocket.ExplosionDamageValue * rocket.ExperimentalStingerDamageMultiplier * rocket.CriticalDamageMultiplier,
                1f - (distance / blastRadius),
                rocket.MinimumSplashDamageValue);
            _host.ObjectiveRules.TryDamageGenerator(generator.Team, damage, owner);
        }
    }

    private void TriggerMinesInBlast(RocketProjectileEntity rocket, float blastRadius)
    {
        var queuedMineIds = new List<int>();
        foreach (var mine in _host.Mines)
        {
            if ((mine.Team == rocket.Team && mine.OwnerId != rocket.OwnerId)
                || SimulationMath.DistanceBetween(rocket.X, rocket.Y, mine.X, mine.Y) >= blastRadius * 0.66f)
            {
                continue;
            }

            queuedMineIds.Add(mine.Id);
        }

        for (var index = 0; index < queuedMineIds.Count; index += 1)
        {
            var mine = FindMineById(queuedMineIds[index]);
            if (mine is not null)
            {
                ExplodeMine(mine);
            }
        }
    }

    private void DestroyBubblesInBlast(RocketProjectileEntity rocket, float blastRadius)
    {
        for (var bubbleIndex = _host.Bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (SimulationMath.DistanceBetween(rocket.X, rocket.Y, _host.Bubbles[bubbleIndex].X, _host.Bubbles[bubbleIndex].Y) < blastRadius * 0.66f)
            {
                _host.RemoveBubbleAt(bubbleIndex);
            }
        }
    }
}
