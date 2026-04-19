namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float SourceExplosionKnockbackCap = 15f;
    private readonly record struct DangerCloseExplosionRequest(float CenterX, float CenterY, int OwnerPlayerId);

    private static void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse)
    {
        if (impulse <= 0.0001f)
        {
            return;
        }

        GetExplosionDirection(player, originX, originY, out var deltaX, out var deltaY, out var distance);
        if (distance <= 0.0001f)
        {
            player.AddImpulse(0f, -impulse);
            return;
        }

        player.AddImpulse((deltaX / distance) * impulse, (deltaY / distance) * impulse);
    }

    private void RegisterExplosionTraces(float centerX, float centerY)
    {
        const int traceCount = 8;
        for (var index = 0; index < traceCount; index += 1)
        {
            var angle = (MathF.PI * 2f * index) / traceCount;
            RegisterCombatTrace(
                centerX,
                centerY,
                MathF.Cos(angle),
                MathF.Sin(angle),
                RocketProjectileEntity.BlastRadius * 0.5f,
                true);
        }
    }

    private void DetonateOwnedMines(int ownerId)
    {
        var queuedMineIds = new Queue<int>();
        foreach (var mine in _mines)
        {
            if (mine.OwnerId == ownerId)
            {
                queuedMineIds.Enqueue(mine.Id);
            }
        }

        while (queuedMineIds.Count > 0)
        {
            var mineId = queuedMineIds.Dequeue();
            var mine = FindMineById(mineId);
            if (mine is null)
            {
                continue;
            }

            foreach (var chainedMine in GetTriggeredMines(mine))
            {
                queuedMineIds.Enqueue(chainedMine.Id);
            }

            ExplodeMine(mine);
        }
    }

    private void ExplodeMine(MineProjectileEntity mine)
    {
        var owner = FindPlayerById(mine.OwnerId);
        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            if (_mines[mineIndex].Id == mine.Id)
            {
                RemoveMineAt(mineIndex);
                break;
            }
        }

        RegisterWorldSoundEvent("ExplosionSnd", mine.X, mine.Y);
        RegisterVisualEffect("Explosion", mine.X, mine.Y);
        ApplyDeadBodyExplosionImpulse(mine.X, mine.Y, MineProjectileEntity.AffectRadius * 0.75f, 10f, MineProjectileEntity.AffectRadius);
        ApplyPlayerGibExplosionImpulse(mine.X, mine.Y, MineProjectileEntity.AffectRadius * 0.75f, 15f, MineProjectileEntity.AffectRadius);
        RegisterExplosionTraces(mine.X, mine.Y);

        var playersSnapshot = EnumerateSimulatedPlayers().ToArray();
        foreach (var player in playersSnapshot)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(this, player, mine.X, mine.Y);
            if (distance >= MineProjectileEntity.BlastRadius)
            {
                continue;
            }

            if (ShouldIgnoreFriendlyGroundedBlast(player, mine.Team, mine.OwnerId))
            {
                continue;
            }

            var factor = 1f - (distance / MineProjectileEntity.BlastRadius);
            if (factor <= MineProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            ApplyMineExplosionImpulse(player, mine.X, mine.Y, factor);
            if (player.Id == mine.OwnerId && player.Team == mine.Team)
            {
                player.SetMovementState(LegacyMovementState.ExplosionRecovery);
            }
            else
            {
                player.SetMovementState(LegacyMovementState.FriendlyJuggle);
            }

            if (CanTeamDamagePlayer(mine.Team, mine.OwnerId, player))
            {
                RegisterBloodEffect(player.X, player.Y, PointDirectionDegrees(mine.X, mine.Y, player.X, player.Y) - 180f, 3);
                var damage = mine.ExplosionDamage * factor;
                if (player.Id == mine.OwnerId && player.Team == mine.Team)
                {
                    damage *= MineProjectileEntity.SelfDamageScale;
                }

                if (ApplyPlayerContinuousDamage(player, damage, owner, PlayerEntity.SpyMineRevealAlpha))
                {
                    KillPlayer(
                        player,
                        gibbed: true,
                        killer: owner,
                        weaponSpriteName: mine.KillFeedWeaponSpriteNameOverride ?? "MineKL");
                }
            }
        }

        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            var distance = DistanceBetween(mine.X, mine.Y, sentry.X, sentry.Y);
            if (distance >= MineProjectileEntity.BlastRadius || sentry.Team == mine.Team)
            {
                continue;
            }

            var factor = 1f - (distance / MineProjectileEntity.BlastRadius);
            if (factor <= MineProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            var damage = mine.ExplosionDamage * MineProjectileEntity.SentryDamageMultiplier * factor;
            if (ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
            {
                DestroySentry(sentry, owner);
            }
        }

        for (var generatorIndex = 0; generatorIndex < _generators.Count; generatorIndex += 1)
        {
            var generator = _generators[generatorIndex];
            var distance = DistanceBetween(mine.X, mine.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance >= MineProjectileEntity.BlastRadius || generator.Team == mine.Team || generator.IsDestroyed)
            {
                continue;
            }

            var damageFactor = 1f - (distance / MineProjectileEntity.BlastRadius);
            if (damageFactor <= MineProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            var damage = mine.ExplosionDamage * damageFactor;
            TryDamageGenerator(generator.Team, damage, owner);
        }

        TriggerNearbyMines(mine);
        AffectRocketsInMineBlast(mine);
        DestroyBubblesInMineBlast(mine);
    }

    private MineProjectileEntity? FindMineById(int mineId)
    {
        foreach (var mine in _mines)
        {
            if (mine.Id == mineId)
            {
                return mine;
            }
        }

        return null;
    }

    private IEnumerable<MineProjectileEntity> GetTriggeredMines(MineProjectileEntity sourceMine)
    {
        foreach (var mine in _mines)
        {
            if (mine.Id == sourceMine.Id)
            {
                continue;
            }

            var distance = DistanceBetween(sourceMine.X, sourceMine.Y, mine.X, mine.Y);
            if (distance >= MineProjectileEntity.BlastRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / MineProjectileEntity.BlastRadius);
            if (distanceFactor <= MineProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            if (mine.Team != sourceMine.Team || mine.OwnerId == sourceMine.OwnerId)
            {
                yield return mine;
            }
        }
    }

    private void ApplyDeadBodyExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius = null)
    {
        var resolvedFalloffRadius = falloffRadius.GetValueOrDefault(blastRadius);
        if (blastRadius <= 0f || resolvedFalloffRadius <= 0f)
        {
            return;
        }

        var deadBodiesSnapshot = _deadBodies.ToArray();
        foreach (var deadBody in deadBodiesSnapshot)
        {
            var distance = DistanceBetween(originX, originY, deadBody.X, deadBody.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var impulseScale = 1f - (distance / resolvedFalloffRadius);
            var angle = MathF.Atan2(deadBody.Y - originY, deadBody.X - originX);
            deadBody.AddImpulse(MathF.Cos(angle) * maxImpulse * impulseScale, MathF.Sin(angle) * maxImpulse * impulseScale);
        }
    }

    private void ApplyPlayerGibExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius = null)
    {
        var resolvedFalloffRadius = falloffRadius.GetValueOrDefault(blastRadius);
        if (blastRadius <= 0f || resolvedFalloffRadius <= 0f)
        {
            return;
        }

        var playerGibsSnapshot = _playerGibs.ToArray();
        foreach (var gib in playerGibsSnapshot)
        {
            var distance = DistanceBetween(originX, originY, gib.X, gib.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var impulseScale = 1f - (distance / resolvedFalloffRadius);
            var angle = MathF.Atan2(gib.Y - originY, gib.X - originX);
            gib.AddImpulse(
                MathF.Cos(angle) * maxImpulse * impulseScale,
                MathF.Sin(angle) * maxImpulse * impulseScale,
                ((_random.NextSingle() * 151f) - 75f) * impulseScale);
        }
    }

    private bool ShouldIgnoreFriendlyGroundedBlast(PlayerEntity player, PlayerTeam explosiveTeam, int explosiveOwnerId)
    {
        return player.Team == explosiveTeam
            && player.Id != explosiveOwnerId
            && !CanTeamDamagePlayer(explosiveTeam, explosiveOwnerId, player)
            && !player.CanOccupy(Level, player.Team, player.X, player.Y + 1f);
    }

    private void TriggerNearbyMines(MineProjectileEntity sourceMine)
    {
        var queuedMineIds = new List<int>();
        foreach (var mine in GetTriggeredMines(sourceMine))
        {
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

    private void AffectRocketsInMineBlast(MineProjectileEntity mine)
    {
        var rocketsToExplode = new List<int>();
        for (var rocketIndex = 0; rocketIndex < _rockets.Count; rocketIndex += 1)
        {
            var rocket = _rockets[rocketIndex];
            if ((mine.Team == rocket.Team && mine.OwnerId != rocket.OwnerId))
            {
                continue;
            }

            var distance = DistanceBetween(mine.X, mine.Y, rocket.X, rocket.Y);
            if (distance >= MineProjectileEntity.AffectRadius * 0.75f)
            {
                continue;
            }

            if (distance < MineProjectileEntity.AffectRadius * 0.25f)
            {
                rocketsToExplode.Add(rocket.Id);
                continue;
            }

            var distanceFactor = 1f - (distance / MineProjectileEntity.AffectRadius);
            var impulse = 10f * distanceFactor;
            if (impulse <= 0f)
            {
                continue;
            }

            var angle = MathF.Atan2(rocket.Y - mine.Y, rocket.X - mine.X);
            rocket.ApplyImpulse(MathF.Cos(angle) * impulse, MathF.Sin(angle) * impulse);
        }

        for (var index = 0; index < rocketsToExplode.Count; index += 1)
        {
            var rocketId = rocketsToExplode[index];
            for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (_rockets[rocketIndex].Id != rocketId)
                {
                    continue;
                }

                ExplodeRocket(_rockets[rocketIndex], directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                break;
            }
        }
    }

    private void TryTriggerExperimentalDangerCloseExplosion(PlayerEntity victim, PlayerEntity? killer)
    {
        if (!ExperimentalGameplaySettings.EnableSoldierDangerClose
            || killer is null
            || !IsExperimentalPracticePowerOwner(killer)
            || killer.ClassId != PlayerClass.Soldier
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team)
        {
            return;
        }

        _pendingDangerCloseExplosions.Enqueue(new DangerCloseExplosionRequest(victim.X, victim.Y, killer.Id));
        ProcessPendingDangerCloseExplosions();
    }

    private void ProcessPendingDangerCloseExplosions()
    {
        if (_processingDangerCloseExplosions)
        {
            return;
        }

        _processingDangerCloseExplosions = true;
        try
        {
            while (_pendingDangerCloseExplosions.Count > 0)
            {
                var request = _pendingDangerCloseExplosions.Dequeue();
                var owner = FindPlayerById(request.OwnerPlayerId);
                if (owner is null)
                {
                    continue;
                }

                TriggerExperimentalDangerCloseExplosion(request.CenterX, request.CenterY, owner);
            }
        }
        finally
        {
            _processingDangerCloseExplosions = false;
        }
    }

    private void TriggerExperimentalDangerCloseExplosion(float centerX, float centerY, PlayerEntity owner)
    {
        var blastRadius = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDangerCloseBlastRadius;
        var blastDamage = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDangerCloseExplosionDamage;
        var knockbackPerTick = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDangerCloseKnockbackPerTick;
        var playersSnapshot = EnumerateSimulatedPlayers().ToArray();

        RegisterWorldSoundEvent("ExplosionSnd", centerX, centerY);
        RegisterVisualEffect("Explosion", centerX, centerY);
        ApplyDeadBodyExplosionImpulse(centerX, centerY, blastRadius, 10f);
        ApplyPlayerGibExplosionImpulse(centerX, centerY, blastRadius, 15f);
        RegisterExplosionTraces(centerX, centerY);

        foreach (var player in playersSnapshot)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(this, player, centerX, centerY);
            if (distance >= blastRadius)
            {
                continue;
            }

            if (ShouldIgnoreFriendlyGroundedBlast(player, owner.Team, owner.Id))
            {
                continue;
            }

            var distanceFactor = 1f - (distance / blastRadius);
            if (distanceFactor <= RocketProjectileEntity.SplashThresholdFactor)
            {
                continue;
            }

            var impulse = GetExplosionImpulseMagnitude(
                player,
                centerX,
                centerY,
                knockbackPerTick,
                distanceFactor,
                useMineVectorProfile: false);
            ApplyExplosionImpulse(player, centerX, centerY, impulse);
            player.SetMovementState(player.Team == owner.Team
                ? LegacyMovementState.FriendlyJuggle
                : LegacyMovementState.RocketJuggle);

            if (!CanTeamDamagePlayer(owner.Team, owner.Id, player))
            {
                continue;
            }

            var appliedDamage = blastDamage * distanceFactor;
            RegisterBloodEffect(player.X, player.Y, PointDirectionDegrees(centerX, centerY, player.X, player.Y) - 180f, 3);
            if (ApplyPlayerContinuousDamage(player, appliedDamage, owner, PlayerEntity.SpyDamageRevealAlpha))
            {
                KillPlayer(
                    player,
                    gibbed: true,
                    killer: owner,
                    weaponSpriteName: "ExplodeKL");
            }
        }

        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            var distance = DistanceBetween(centerX, centerY, sentry.X, sentry.Y);
            if (distance >= blastRadius || sentry.Team == owner.Team)
            {
                continue;
            }

            var damage = blastDamage * (1f - (distance / blastRadius));
            if (ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
            {
                DestroySentry(sentry, owner);
            }
        }

        for (var generatorIndex = 0; generatorIndex < _generators.Count; generatorIndex += 1)
        {
            var generator = _generators[generatorIndex];
            var distance = DistanceBetween(centerX, centerY, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance >= blastRadius || generator.Team == owner.Team || generator.IsDestroyed)
            {
                continue;
            }

            var damage = blastDamage * (1f - (distance / blastRadius));
            TryDamageGenerator(generator.Team, damage, owner);
        }

        var rocketIdsToExplode = new List<int>();
        for (var rocketIndex = 0; rocketIndex < _rockets.Count; rocketIndex += 1)
        {
            if (DistanceBetween(centerX, centerY, _rockets[rocketIndex].X, _rockets[rocketIndex].Y) < blastRadius * 0.66f)
            {
                rocketIdsToExplode.Add(_rockets[rocketIndex].Id);
            }
        }

        for (var index = 0; index < rocketIdsToExplode.Count; index += 1)
        {
            for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (_rockets[rocketIndex].Id == rocketIdsToExplode[index])
                {
                    ExplodeRocket(_rockets[rocketIndex], directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                    break;
                }
            }
        }

        var mineIdsToExplode = new List<int>();
        for (var mineIndex = 0; mineIndex < _mines.Count; mineIndex += 1)
        {
            if (DistanceBetween(centerX, centerY, _mines[mineIndex].X, _mines[mineIndex].Y) < blastRadius)
            {
                mineIdsToExplode.Add(_mines[mineIndex].Id);
            }
        }

        for (var index = 0; index < mineIdsToExplode.Count; index += 1)
        {
            var mine = FindMineById(mineIdsToExplode[index]);
            if (mine is not null)
            {
                ExplodeMine(mine);
            }
        }

        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (DistanceBetween(centerX, centerY, _bubbles[bubbleIndex].X, _bubbles[bubbleIndex].Y) < blastRadius)
            {
                RemoveBubbleAt(bubbleIndex);
            }
        }
    }

    private void DestroyBubblesInMineBlast(MineProjectileEntity mine)
    {
        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (DistanceBetween(mine.X, mine.Y, _bubbles[bubbleIndex].X, _bubbles[bubbleIndex].Y) < MineProjectileEntity.BlastRadius + BubbleProjectileEntity.SelfPopRadius)
            {
                RemoveBubbleAt(bubbleIndex);
            }
        }
    }

    private static void ApplyMineExplosionImpulse(PlayerEntity player, float originX, float originY, float distanceFactor)
    {
        var impulse = GetExplosionImpulseMagnitude(
            player,
            originX,
            originY,
            MineProjectileEntity.BlastImpulse,
            distanceFactor,
            useMineVectorProfile: true);
        ApplyExplosionImpulse(player, originX, originY, impulse);
    }

    private static float GetExplosionImpulseMagnitude(
        PlayerEntity player,
        float originX,
        float originY,
        float knockbackPerTick,
        float distanceFactor,
        bool useMineVectorProfile)
    {
        if (distanceFactor <= 0f)
        {
            return 0f;
        }

        GetExplosionDirection(player, originX, originY, out var deltaX, out var deltaY, out var distance);
        if (distance <= 0.0001f)
        {
            return MathF.Min(SourceExplosionKnockbackCap, knockbackPerTick * distanceFactor) * LegacyMovementModel.SourceTicksPerSecond;
        }

        var unitX = deltaX / distance;
        var unitY = deltaY / distance;
        var vectorFactor = useMineVectorProfile
            ? MathF.Sqrt((unitY * unitY) + (0.64f * unitX * unitX))
            : MathF.Sqrt((unitX * unitX * unitX * unitX) + (unitY * unitY * unitY * unitY));

        return MathF.Min(SourceExplosionKnockbackCap, knockbackPerTick * distanceFactor) * vectorFactor * LegacyMovementModel.SourceTicksPerSecond;
    }

    private static float GetExplosionDistanceToPlayer(SimulationWorld world, PlayerEntity player, float originX, float originY)
    {
        GetPlayerPresentationHitBounds(world, player, out var left, out var top, out var right, out var bottom);
        var deltaX = 0f;
        if (originX < left)
        {
            deltaX = left - originX;
        }
        else if (originX > right)
        {
            deltaX = originX - right;
        }

        var deltaY = 0f;
        if (originY < top)
        {
            deltaY = top - originY;
        }
        else if (originY > bottom)
        {
            deltaY = originY - bottom;
        }

        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static void GetExplosionDirection(PlayerEntity player, float originX, float originY, out float deltaX, out float deltaY, out float distance)
    {
        var centerX = player.X + ((player.CollisionLeftOffset + player.CollisionRightOffset) * 0.5f);
        var centerY = player.Y + ((player.CollisionTopOffset + player.CollisionBottomOffset) * 0.5f);
        deltaX = centerX - originX;
        deltaY = centerY - originY;
        distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

}
