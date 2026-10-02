namespace OpenGarrison.Core;

internal sealed partial class ExplosionRulesSystem
{
    internal void RegisterExplosionTraces(float centerX, float centerY)
    {
        const int traceCount = 8;
        for (var index = 0; index < traceCount; index += 1)
        {
            var angle = (MathF.PI * 2f * index) / traceCount;
            _host.RegisterCombatTrace(
                centerX,
                centerY,
                DeterministicMath.Cos(angle),
                DeterministicMath.Sin(angle),
                RocketProjectileEntity.BlastRadius * 0.5f,
                true);
        }
    }

    internal void DetonateOwnedMines(int ownerId)
    {
        var queuedMineIds = new Queue<int>();
        foreach (var mine in _host.Mines)
        {
            if (mine.OwnerId == ownerId && CanPlayerDetonateMine(mine))
            {
                queuedMineIds.Enqueue(mine.Id);
            }
        }

        while (queuedMineIds.Count > 0)
        {
            var mineId = queuedMineIds.Dequeue();
            var mine = FindMineById(mineId);
            if (mine is null || !CanPlayerDetonateMine(mine))
            {
                continue;
            }

            foreach (var chainedMine in GetTriggeredMines(mine))
            {
                if (CanPlayerDetonateMine(chainedMine))
                {
                    queuedMineIds.Enqueue(chainedMine.Id);
                }
            }

            ExplodeMine(mine);
        }
    }

    private bool CanPlayerDetonateMine(MineProjectileEntity mine)
    {
        return mine.CreatedFrame < _host.Frame;
    }

    internal void ExplodeMine(MineProjectileEntity mine, bool triggerNearbyMines = true)
    {
        var owner = _host.FindPlayerById(mine.OwnerId);
        var blastRadius = CombatSystem.ResolveExplosiveSplashRadius(
            MineProjectileEntity.BlastRadius
                * MathF.Max(0.1f, owner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));
        for (var mineIndex = _host.Mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            if (_host.Mines[mineIndex].Id == mine.Id)
            {
                _host.RemoveMineAt(mineIndex);
                break;
            }
        }

        _host.RegisterWorldSoundEvent("ExplosionSnd", mine.X, mine.Y);
        _host.RegisterVisualEffect("Explosion", mine.X, mine.Y);
        ApplyDeadBodyExplosionImpulse(mine.X, mine.Y, MineProjectileEntity.AffectRadius * 0.75f, 10f, MineProjectileEntity.AffectRadius);
        ApplyPlayerGibExplosionImpulse(mine.X, mine.Y, MineProjectileEntity.AffectRadius * 0.75f, 15f, MineProjectileEntity.AffectRadius);
        RegisterExplosionTraces(mine.X, mine.Y);

        var playersSnapshot = _host.EnumerateSimulatedPlayers().ToArray();
        var attackerWasGrounded = owner?.IsGrounded;
        foreach (var player in playersSnapshot)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(player, mine.X, mine.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            if (ShouldIgnoreFriendlyGroundedBlast(player, mine.Team, mine.OwnerId))
            {
                continue;
            }

            var factor = 1f - (distance / blastRadius);
            if (factor <= 0f)
            {
                continue;
            }

            if (ShouldSkipFriendlyExplosionBoost(player, mine.Team, mine.OwnerId))
            {
                continue;
            }

            var targetWasGrounded = player.IsGrounded;
            ExplosionGeometry.ApplyMineExplosionImpulse(player, mine.X, mine.Y, factor);
            if (player.Id == mine.OwnerId && player.Team == mine.Team)
            {
                player.SetMovementStateIfAirborne(LegacyMovementState.ExplosionRecovery);
            }
            else
            {
                player.SetMovementStateIfAirborne(LegacyMovementState.FriendlyJuggle);
            }

            if (_host.CanTeamDamagePlayer(mine.Team, mine.OwnerId, player))
            {
                _host.RegisterBloodEffect(player.X, player.Y, SimulationMath.PointDirectionDegrees(mine.X, mine.Y, player.X, player.Y) - 180f, 3);
                var critMultiplier = (player.Id == mine.OwnerId && player.Team == mine.Team) ? 1f : mine.CriticalDamageMultiplier;
                var maximumDamage = mine.ExplosionDamage * critMultiplier;
                if (player.Id == mine.OwnerId && player.Team == mine.Team)
                {
                    maximumDamage *= MineProjectileEntity.SelfDamageScale;
                }

                var damage = CombatSystem.ResolveExplosiveSplashDamage(maximumDamage, factor);

                if (_host.ApplyPlayerContinuousDamageWithContext(
                        player,
                        damage,
                        owner,
                        PlayerEntity.SpyMineRevealAlpha,
                        civvieUmbrellaThreatSourceX: mine.X,
                        civvieUmbrellaThreatSourceY: mine.Y,
                        civvieUmbrellaDrainTicks: PlayerEntity.CivvieUmbrellaDirectExplosionDrainTicks,
                        civvieUmbrellaCriticalBoost: PlayerEntity.IsCriticalDamageMultiplierBoosted(critMultiplier),
                        civvieUmbrellaUseLiveAttackerCriticalBoost: false,
                        attackerWasGrounded: attackerWasGrounded,
                        targetWasGrounded: targetWasGrounded))
                {
                    _host.KillPlayer(
                        player,
                        gibbed: true,
                        killer: owner,
                        weaponSpriteName: mine.KillFeedWeaponSpriteNameOverride ?? "MineKL");
                }
            }
        }

        for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
            var distance = SimulationMath.DistanceBetween(mine.X, mine.Y, sentry.X, sentry.Y);
            if (distance >= blastRadius || sentry.Team == mine.Team)
            {
                continue;
            }

            var factor = 1f - (distance / blastRadius);
            if (factor <= 0f)
            {
                continue;
            }

            var damage = CombatSystem.ResolveExplosiveSplashDamage(
                mine.ExplosionDamage * MineProjectileEntity.SentryDamageMultiplier * mine.CriticalDamageMultiplier,
                factor);
            if (_host.ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
            {
                _host.DestroySentry(sentry, owner);
            }
        }

        for (var generatorIndex = 0; generatorIndex < _host.WorldObjects.Generators.Count; generatorIndex += 1)
        {
            var generator = _host.WorldObjects.Generators[generatorIndex];
            var distance = SimulationMath.DistanceBetween(mine.X, mine.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance >= blastRadius || generator.Team == mine.Team || generator.IsDestroyed)
            {
                continue;
            }

            var damageFactor = 1f - (distance / blastRadius);
            if (damageFactor <= 0f)
            {
                continue;
            }

            var damage = CombatSystem.ResolveExplosiveSplashDamage(
                mine.ExplosionDamage * mine.CriticalDamageMultiplier,
                damageFactor);
            _host.TryDamageGenerator(generator.Team, damage, owner);
        }

        ApplyExplosiveDamageToJumpPads(
            mine.X,
            mine.Y,
            blastRadius,
            mine.ExplosionDamage * mine.CriticalDamageMultiplier,
            mine.Team);

        if (triggerNearbyMines)
        {
            TriggerNearbyMines(mine);
        }

        AffectRocketsInMineBlast(mine);
        DestroyBubblesInMineBlast(mine);
    }

    internal MineProjectileEntity? FindMineById(int mineId)
    {
        foreach (var mine in _host.Mines)
        {
            if (mine.Id == mineId)
            {
                return mine;
            }
        }

        return null;
    }

    internal void ExplodeOldestMine(int ownerId, bool triggerNearbyMines = true)
    {
        MineProjectileEntity? oldestMine = null;
        foreach (var mine in _host.Mines)
        {
            if (mine.OwnerId == ownerId)
            {
                // Find the oldest mine (first one in the list, as they're added chronologically)
                oldestMine = mine;
                break;
            }
        }

        if (oldestMine is not null)
        {
            ExplodeMine(oldestMine, triggerNearbyMines);
        }
    }

    private IEnumerable<MineProjectileEntity> GetTriggeredMines(MineProjectileEntity sourceMine)
    {
        var sourceOwner = _host.FindPlayerById(sourceMine.OwnerId);
        var blastRadius = CombatSystem.ResolveExplosiveSplashRadius(
            MineProjectileEntity.BlastRadius
                * MathF.Max(0.1f, sourceOwner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));
        foreach (var mine in _host.Mines)
        {
            if (mine.Id == sourceMine.Id)
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(sourceMine.X, sourceMine.Y, mine.X, mine.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / blastRadius);
            if (distanceFactor <= 0f)
            {
                continue;
            }

            if (mine.Team != sourceMine.Team || mine.OwnerId == sourceMine.OwnerId)
            {
                yield return mine;
            }
        }
    }

    internal void ApplyDeadBodyExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius = null)
    {
        var resolvedFalloffRadius = falloffRadius.GetValueOrDefault(blastRadius);
        if (blastRadius <= 0f || resolvedFalloffRadius <= 0f)
        {
            return;
        }

        var deadBodiesSnapshot = _host.WorldObjects.DeadBodies.ToArray();
        foreach (var deadBody in deadBodiesSnapshot)
        {
            var distance = SimulationMath.DistanceBetween(originX, originY, deadBody.X, deadBody.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var impulseScale = 1f - (distance / resolvedFalloffRadius);
            var angle = DeterministicMath.Atan2(deadBody.Y - originY, deadBody.X - originX);
            deadBody.AddImpulse(DeterministicMath.Cos(angle) * maxImpulse * impulseScale, DeterministicMath.Sin(angle) * maxImpulse * impulseScale);
        }
    }

    internal void ApplyPlayerGibExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse, float? falloffRadius = null)
    {
        var resolvedFalloffRadius = falloffRadius.GetValueOrDefault(blastRadius);
        if (blastRadius <= 0f || resolvedFalloffRadius <= 0f)
        {
            return;
        }

        var playerGibsSnapshot = _host.WorldObjects.PlayerGibs.ToArray();
        foreach (var gib in playerGibsSnapshot)
        {
            var distance = SimulationMath.DistanceBetween(originX, originY, gib.X, gib.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var impulseScale = 1f - (distance / resolvedFalloffRadius);
            var angle = DeterministicMath.Atan2(gib.Y - originY, gib.X - originX);
            gib.AddImpulse(
                DeterministicMath.Cos(angle) * maxImpulse * impulseScale,
                DeterministicMath.Sin(angle) * maxImpulse * impulseScale,
                ((_host.Randoms.Gameplay.NextSingle() * 151f) - 75f) * impulseScale);
        }
    }

    internal bool ShouldIgnoreFriendlyGroundedBlast(PlayerEntity player, PlayerTeam explosiveTeam, int explosiveOwnerId)
    {
        if (player.Team != explosiveTeam || player.Id == explosiveOwnerId)
        {
            return false;
        }

        if (_host.ExperimentalGameplaySettings.EnableFriendlyExplosionBoost)
        {
            return false;
        }

        return !_host.CanTeamDamagePlayer(explosiveTeam, explosiveOwnerId, player)
            && !player.CanOccupy(_host.Level, player.Team, player.X, player.Y + 1f);
    }

    internal bool ShouldSkipFriendlyExplosionBoost(PlayerEntity player, PlayerTeam explosiveTeam, int explosiveOwnerId)
    {
        return !_host.ExperimentalGameplaySettings.EnableFriendlyExplosionBoost
            && player.Team == explosiveTeam
            && player.Id != explosiveOwnerId;
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
        for (var rocketIndex = 0; rocketIndex < _host.Rockets.Count; rocketIndex += 1)
        {
            var rocket = _host.Rockets[rocketIndex];
            if ((mine.Team == rocket.Team && mine.OwnerId != rocket.OwnerId))
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(mine.X, mine.Y, rocket.X, rocket.Y);
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

            var angle = DeterministicMath.Atan2(rocket.Y - mine.Y, rocket.X - mine.X);
            rocket.ApplyImpulse(DeterministicMath.Cos(angle) * impulse, DeterministicMath.Sin(angle) * impulse);
        }

        for (var index = 0; index < rocketsToExplode.Count; index += 1)
        {
            var rocketId = rocketsToExplode[index];
            for (var rocketIndex = _host.Rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (_host.Rockets[rocketIndex].Id != rocketId)
                {
                    continue;
                }

                ExplodeRocket(_host.Rockets[rocketIndex], directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                break;
            }
        }
    }

    internal void TryTriggerExperimentalDangerCloseExplosion(PlayerEntity victim, PlayerEntity? killer)
    {
        if (killer is null
            || !_host.GetLastToDieGameplaySettings(killer).EnableSoldierDangerClose
            || !_host.IsExperimentalPracticePowerOwner(killer)
            || killer.ClassId != PlayerClass.Soldier
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team)
        {
            return;
        }

        _host.CombatRuntime.PendingDangerCloseExplosions.Enqueue(new DangerCloseExplosionRequest(victim.X, victim.Y, killer.Id));
        ProcessPendingDangerCloseExplosions();
    }

    private void ProcessPendingDangerCloseExplosions()
    {
        if (_host.CombatRuntime.ProcessingDangerCloseExplosions)
        {
            return;
        }

        _host.CombatRuntime.ProcessingDangerCloseExplosions = true;
        try
        {
            while (_host.CombatRuntime.PendingDangerCloseExplosions.Count > 0)
            {
                var request = _host.CombatRuntime.PendingDangerCloseExplosions.Dequeue();
                var owner = _host.FindPlayerById(request.OwnerPlayerId);
                if (owner is null)
                {
                    continue;
                }

                TriggerExperimentalDangerCloseExplosion(request.CenterX, request.CenterY, owner);
            }
        }
        finally
        {
            _host.CombatRuntime.ProcessingDangerCloseExplosions = false;
        }
    }

    private void TriggerExperimentalDangerCloseExplosion(float centerX, float centerY, PlayerEntity owner)
    {
        var blastRadius = CombatSystem.ResolveExplosiveSplashRadius(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDangerCloseBlastRadius);
        var blastDamage = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDangerCloseExplosionDamage;
        var knockbackPerTick = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultDangerCloseKnockbackPerTick;
        var playersSnapshot = _host.EnumerateSimulatedPlayers().ToArray();
        var attackerWasGrounded = owner.IsGrounded;

        _host.RegisterWorldSoundEvent("ExplosionSnd", centerX, centerY);
        _host.RegisterVisualEffect("Explosion", centerX, centerY);
        ApplyDeadBodyExplosionImpulse(centerX, centerY, blastRadius, 10f);
        ApplyPlayerGibExplosionImpulse(centerX, centerY, blastRadius, 15f);
        RegisterExplosionTraces(centerX, centerY);

        foreach (var player in playersSnapshot)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(player, centerX, centerY);
            if (distance >= blastRadius)
            {
                continue;
            }

            if (ShouldIgnoreFriendlyGroundedBlast(player, owner.Team, owner.Id))
            {
                continue;
            }

            var distanceFactor = 1f - (distance / blastRadius);
            if (distanceFactor <= 0f)
            {
                continue;
            }

            if (ShouldSkipFriendlyExplosionBoost(player, owner.Team, owner.Id))
            {
                continue;
            }

            var targetWasGrounded = player.IsGrounded;
            var impulse = ExplosionGeometry.GetExplosionImpulseMagnitude(
                player,
                centerX,
                centerY,
                knockbackPerTick,
                distanceFactor,
                useMineVectorProfile: false);
            ExplosionGeometry.ApplyExplosionImpulse(player, centerX, centerY, impulse);
            player.SetMovementState(player.Team == owner.Team
                ? LegacyMovementState.FriendlyJuggle
                : LegacyMovementState.RocketJuggle);

            if (!_host.CanTeamDamagePlayer(owner.Team, owner.Id, player))
            {
                continue;
            }

            var appliedDamage = CombatSystem.ResolveExplosiveSplashDamage(blastDamage, distanceFactor);
            _host.RegisterBloodEffect(player.X, player.Y, SimulationMath.PointDirectionDegrees(centerX, centerY, player.X, player.Y) - 180f, 3);
            if (_host.ApplyPlayerContinuousDamageWithContext(
                    player,
                    appliedDamage,
                    owner,
                    PlayerEntity.SpyDamageRevealAlpha,
                    civvieUmbrellaThreatSourceX: centerX,
                    civvieUmbrellaThreatSourceY: centerY,
                    civvieUmbrellaDrainTicks: PlayerEntity.GetCivvieUmbrellaSplashExplosionDrainTicksFromDamage(appliedDamage, blastDamage),
                    attackerWasGrounded: attackerWasGrounded,
                    targetWasGrounded: targetWasGrounded))
            {
                _host.KillPlayer(
                    player,
                    gibbed: true,
                    killer: owner,
                    weaponSpriteName: "ExplodeKL");
            }
        }

        for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
            var distance = SimulationMath.DistanceBetween(centerX, centerY, sentry.X, sentry.Y);
            if (distance >= blastRadius || sentry.Team == owner.Team)
            {
                continue;
            }

            var damage = CombatSystem.ResolveExplosiveSplashDamage(blastDamage, 1f - (distance / blastRadius));
            if (_host.ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
            {
                _host.DestroySentry(sentry, owner);
            }
        }

        for (var generatorIndex = 0; generatorIndex < _host.WorldObjects.Generators.Count; generatorIndex += 1)
        {
            var generator = _host.WorldObjects.Generators[generatorIndex];
            var distance = SimulationMath.DistanceBetween(centerX, centerY, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance >= blastRadius || generator.Team == owner.Team || generator.IsDestroyed)
            {
                continue;
            }

            var damage = CombatSystem.ResolveExplosiveSplashDamage(blastDamage, 1f - (distance / blastRadius));
            _host.TryDamageGenerator(generator.Team, damage, owner);
        }

        ApplyExplosiveDamageToJumpPads(
            centerX,
            centerY,
            blastRadius,
            blastDamage,
            owner.Team);

        var rocketIdsToExplode = new List<int>();
        for (var rocketIndex = 0; rocketIndex < _host.Rockets.Count; rocketIndex += 1)
        {
            if (SimulationMath.DistanceBetween(centerX, centerY, _host.Rockets[rocketIndex].X, _host.Rockets[rocketIndex].Y) < blastRadius * 0.66f)
            {
                rocketIdsToExplode.Add(_host.Rockets[rocketIndex].Id);
            }
        }

        for (var index = 0; index < rocketIdsToExplode.Count; index += 1)
        {
            for (var rocketIndex = _host.Rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (_host.Rockets[rocketIndex].Id == rocketIdsToExplode[index])
                {
                    ExplodeRocket(_host.Rockets[rocketIndex], directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                    break;
                }
            }
        }

        var mineIdsToExplode = new List<int>();
        for (var mineIndex = 0; mineIndex < _host.Mines.Count; mineIndex += 1)
        {
            if (SimulationMath.DistanceBetween(centerX, centerY, _host.Mines[mineIndex].X, _host.Mines[mineIndex].Y) < blastRadius)
            {
                mineIdsToExplode.Add(_host.Mines[mineIndex].Id);
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

        for (var bubbleIndex = _host.Bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (SimulationMath.DistanceBetween(centerX, centerY, _host.Bubbles[bubbleIndex].X, _host.Bubbles[bubbleIndex].Y) < blastRadius)
            {
                _host.RemoveBubbleAt(bubbleIndex);
            }
        }
    }

    /// <summary>
    /// Applies ordinary explosive splash to buildable jump pads. Pads use the
    /// same radial falloff as other structures, with the established 1.5x
    /// structure multiplier. Neutral map pads can be damaged by either team;
    /// team-owned pads keep the existing friendly-fire rule.
    /// </summary>
    internal void ApplyExplosiveDamageToJumpPads(
        float centerX,
        float centerY,
        float blastRadius,
        float maximumDamage,
        PlayerTeam sourceTeam,
        float minimumDamage = CombatSystem.ExplosiveSplashMinimumDamage)
    {
        if (blastRadius <= 0f || maximumDamage <= 0f)
        {
            return;
        }

        for (var jumpPadIndex = _host.WorldObjects.JumpPads.Count - 1; jumpPadIndex >= 0; jumpPadIndex -= 1)
        {
            var jumpPad = _host.WorldObjects.JumpPads[jumpPadIndex];
            if (jumpPad.IsDead
                || (!jumpPad.IsNeutral && jumpPad.Team == sourceTeam))
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(centerX, centerY, jumpPad.X, jumpPad.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / blastRadius);
            if (distanceFactor <= 0f)
            {
                continue;
            }

            var damage = CombatSystem.ResolveExplosiveSplashDamage(
                maximumDamage * ExplosionGeometry.ExplosiveJumpPadDamageMultiplier,
                distanceFactor,
                minimumDamage);
            jumpPad.TakeDamage((int)MathF.Ceiling(damage));
            if (jumpPad.IsDead)
            {
                _host.DestroyJumpPad(jumpPad);
            }
        }
    }

    private void DestroyBubblesInMineBlast(MineProjectileEntity mine)
    {
        var owner = _host.FindPlayerById(mine.OwnerId);
        var blastRadius = CombatSystem.ResolveExplosiveSplashRadius(
            MineProjectileEntity.BlastRadius
                * MathF.Max(0.1f, owner?.LastToDieUniversalModifiers.ExplosionScale ?? 1f));
        for (var bubbleIndex = _host.Bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (SimulationMath.DistanceBetween(mine.X, mine.Y, _host.Bubbles[bubbleIndex].X, _host.Bubbles[bubbleIndex].Y) < blastRadius + BubbleProjectileEntity.SelfPopRadius)
            {
                _host.RemoveBubbleAt(bubbleIndex);
            }
        }
    }
}
