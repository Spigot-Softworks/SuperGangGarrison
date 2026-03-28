namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float SourceExplosionKnockbackCap = 15f;

    private static void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse)
    {
        if (impulse <= 0.0001f)
        {
            return;
        }

        var deltaX = player.X - originX;
        var deltaY = player.Y - originY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
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

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = DistanceBetween(mine.X, mine.Y, player.X, player.Y);
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

            if (player.Team != mine.Team || player.Id == mine.OwnerId)
            {
                RegisterBloodEffect(player.X, player.Y, PointDirectionDegrees(mine.X, mine.Y, player.X, player.Y) - 180f, 3);
                var damage = mine.ExplosionDamage * factor;
                if (player.Id == mine.OwnerId && player.Team == mine.Team)
                {
                    damage *= MineProjectileEntity.SelfDamageScale;
                }

                if (ApplyPlayerContinuousDamage(player, damage, owner, PlayerEntity.SpyMineRevealAlpha))
                {
                    KillPlayer(player, gibbed: true, killer: owner, weaponSpriteName: "MineKL");
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
                DestroySentry(sentry);
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

        foreach (var deadBody in _deadBodies)
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

        foreach (var gib in _playerGibs)
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

    private void ApplyMineExplosionImpulse(PlayerEntity player, float originX, float originY, float distanceFactor)
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

        var deltaX = player.X - originX;
        var deltaY = player.Y - originY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
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

}

