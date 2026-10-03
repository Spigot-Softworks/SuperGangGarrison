using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private void ApplySnapshotShots<T>(
        IReadOnlyList<SnapshotShotState> shots,
        IReadOnlyList<int> removedShotIds,
        bool collectionIsComplete,
        IReadOnlyList<T> target,
        Func<T, SnapshotShotState, bool> canReuse,
        Func<SnapshotShotState, T> factory,
        Action<T, SnapshotShotState> applyState,
        Action<T>? onServerTerminated = null,
        Func<T, SnapshotShotState, bool>? shouldApplyExistingState = null)
        where T : SimulationEntity
    {
        var filteredShots = FilterTerminatedProjectiles(shots, static state => state.Id);

        // When connected as a multiplayer client, fire hit feedback for client-predicted
        // projectiles that the server removed before the local simulation detected the hit.
        // This happens when the server's authoritative enemy positions are ahead of the
        // client's local estimates, causing a hit on the server a tick or two before the
        // client's simulation would detect it. Without this, blood effects and plugin-based
        // damage sounds are silently dropped even though damage was dealt.
        if (onServerTerminated is not null)
        {
            for (var i = 0; i < target.Count; i++)
            {
                var entity = target[i];
                if (!_host.ClientSnapshots.PredictedProjectileIds.Contains(entity.Id))
                    continue;

                var foundInFilteredShots = false;
                for (var j = 0; j < filteredShots.Count; j++)
                {
                    if (filteredShots[j].Id == entity.Id)
                    {
                        foundInFilteredShots = true;
                        break;
                    }
                }

                if (!foundInFilteredShots)
                {
                    onServerTerminated(entity);
                }
            }
        }

        SyncSnapshotEntities(
            filteredShots,
            removedShotIds,
            collectionIsComplete,
            target,
            static state => state.Id,
            canReuse,
            factory,
            applyState,
            true,
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && _host.Projectiles.ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _host.ClientSnapshots.PredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity
                    && shouldApplyExistingState is not null
                    && !shouldApplyExistingState(entity, state))
                {
                    return;
                }

                applyState(entity, state);
            },
            () => _host.Projectiles.ClearProjectileCollection(target),
            entity => _host.Projectiles.AddProjectileToCollection(target, entity));
    }

    private static bool ShouldApplyLocallySimulatedProjectileState(int localTicksRemaining, int snapshotTicksRemaining)
    {
        return snapshotTicksRemaining <= localTicksRemaining;
    }

    private static void ApplyRocketSnapshotState(RocketProjectileEntity entity, SnapshotRocketState state)
    {
        entity.ApplyNetworkState(
            state.X,
            state.Y,
            state.PreviousX,
            state.PreviousY,
            state.DirectionRadians,
            state.Speed,
            state.TicksRemaining,
            state.ReducedKnockbackSourceTicksRemaining,
            state.ZeroKnockbackSourceTicksRemaining,
            state.RangeAnchorOwnerId,
            state.LastKnownRangeOriginX,
            state.LastKnownRangeOriginY,
            state.DistanceToTravel,
            state.IsFading,
            state.FadeSourceTicksRemaining,
            state.PassedFriendlyPlayerIds);
        entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
        entity.HydrateBallisticState(state.IsBallistic, state.BallisticGravityPerTick, state.SuppressSmokeTrail);
    }

    private static void ApplyFlameSnapshotState(FlameProjectileEntity entity, SnapshotFlameState state)
    {
        entity.ApplyNetworkState(
            state.X,
            state.Y,
            state.PreviousX,
            state.PreviousY,
            state.VelocityX,
            state.VelocityY,
            state.TicksRemaining,
            state.AttachedPlayerId < 0 ? null : state.AttachedPlayerId,
            state.AttachedOffsetX,
            state.AttachedOffsetY);
        entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
        entity.HydrateSettleState(state.SettlesOnGround, state.IsGrounded);
    }

    private static void ApplyMineSnapshotState(MineProjectileEntity entity, SnapshotMineState state)
    {
        entity.ApplyNetworkState(
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            state.IsStickied,
            state.IsDestroyed,
            state.ExplosionDamage);
        entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
    }

    private static void ApplyGrenadeSnapshotState(GrenadeProjectileEntity entity, SnapshotGrenadeState state)
    {
        entity.ApplyNetworkState(
            state.X,
            state.Y,
            state.PreviousX,
            state.PreviousY,
            state.VelocityX,
            state.VelocityY,
            isDestroyed: false,
            state.IsStrongDrink
                ? RocketProjectileEntity.ExplosionDamage
                : GrenadeProjectileEntity.BaseExplosionDamage,
            state.FuseTicksLeft);
        entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
        entity.HydrateStrongDrink(state.IsStrongDrink);
    }

    private static bool ShouldApplyExistingRocketState(RocketProjectileEntity entity, SnapshotRocketState state)
    {
        if (ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining))
        {
            return true;
        }

        return entity.IsCritical != state.IsCritical
            || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier
            || (!entity.IsFading && state.IsFading);
    }

    private static bool ShouldApplyExistingFlameState(FlameProjectileEntity entity, SnapshotFlameState state)
    {
        var attachedPlayerId = state.AttachedPlayerId < 0 ? (int?)null : state.AttachedPlayerId;
        if (ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining))
        {
            return true;
        }

        return entity.IsCritical != state.IsCritical
            || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier
            || (attachedPlayerId.HasValue && entity.AttachedPlayerId != attachedPlayerId)
            || entity.SettlesOnGround != state.SettlesOnGround
            || entity.IsGrounded != state.IsGrounded;
    }

    private static bool ShouldApplyExistingMineState(MineProjectileEntity entity, SnapshotMineState state)
    {
        return (!entity.IsStickied && state.IsStickied)
            || (!entity.IsDestroyed && state.IsDestroyed)
            || entity.IsCritical != state.IsCritical
            || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier;
    }

    private static bool ShouldApplyExistingGrenadeState(GrenadeProjectileEntity entity, SnapshotGrenadeState state)
    {
        return ShouldApplyLocallySimulatedProjectileState(entity.FuseTicksLeft, state.FuseTicksLeft)
            || entity.IsCritical != state.IsCritical
            || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier
            || entity.IsStrongDrink != state.IsStrongDrink;
    }

    /// <summary>
    /// When a client-predicted projectile is removed by the server snapshot before the local
    /// simulation detected the hit, fire a blood effect at the nearest enemy player along the
    /// projectile's backward trajectory. This recovers visual feedback (blood, plugin damage
    /// sounds) that would otherwise be silently dropped due to latency-induced position
    /// divergence between server and client.
    /// </summary>
    private void TryRegisterServerTerminatedProjectilePlayerHitEffect(
        float shotX, float shotY, float prevShotX, float prevShotY,
        PlayerTeam team, int ownerId, int bloodCount = 1)
    {
        var dirX = shotX - prevShotX;
        var dirY = shotY - prevShotY;
        var dist = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (dist < 0.0001f)
            return;

        dirX /= dist;
        dirY /= dist;

        // Cast a segment extending backward from the shot's current position to find a player
        // the shot may have already passed through due to server/client position divergence.
        // Also extends slightly forward in case the server was one tick ahead.
        const float BackwardReach = 300f;
        const float ForwardReach = 50f;
        const float HitRadius = 25f;

        var rayOriginX = shotX - dirX * BackwardReach;
        var rayOriginY = shotY - dirY * BackwardReach;
        var totalRayLength = BackwardReach + ForwardReach;

        PlayerEntity? nearestPlayer = null;
        var nearestProjection = float.PositiveInfinity;

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || !_host.CanTeamDamagePlayer(team, ownerId, player) || player.Id == ownerId)
                continue;

            var dx = player.X - rayOriginX;
            var dy = player.Y - rayOriginY;
            var projection = (dx * dirX) + (dy * dirY);
            if (projection < 0f || projection > totalRayLength)
                continue;

            var perpDistSq = (dx * dx + dy * dy) - (projection * projection);
            if (perpDistSq > HitRadius * HitRadius)
                continue;

            if (projection < nearestProjection)
            {
                nearestProjection = projection;
                nearestPlayer = player;
            }
        }

        if (nearestPlayer is not null)
        {
            _host.WorldEffects.RegisterBloodEffect(
                nearestPlayer.X, nearestPlayer.Y,
                DeterministicMath.Atan2(dirY, dirX) * (180f / MathF.PI) - 180f,
                bloodCount);
        }
    }

    /// <summary>
    /// Returns the input list with any entries whose ID is currently suppressed removed.
    /// Baseline-preserved states for locally-terminated projectiles must not ghost-respawn
    /// them while the server's EntityRemoval contribution is still in transit. Local
    /// suppression expires quickly so authoritative server state can restore a projectile
    /// when the client predicted the hit/removal incorrectly.
    /// Allocates a new list only when at least one entry is filtered; otherwise returns
    /// the original reference (zero allocation on the common path).
    /// </summary>
    private IReadOnlyList<TState> FilterTerminatedProjectiles<TState>(
        IReadOnlyList<TState> states,
        Func<TState, int> idSelector)
    {
        if (_host.ClientSnapshots.TerminatedProjectileIds.Count == 0)
        {
            return states;
        }

        List<TState>? filtered = null;
        for (var i = 0; i < states.Count; i++)
        {
            if (IsProjectileRespawnSuppressed(idSelector(states[i])))
            {
                if (filtered is null)
                {
                    filtered = new List<TState>(states.Count - 1);
                    for (var j = 0; j < i; j++)
                        filtered.Add(states[j]);
                }
            }
            else
            {
                filtered?.Add(states[i]);
            }
        }

        return filtered ?? states;
    }

    private void ApplySnapshotRockets(
        IReadOnlyList<SnapshotRocketState> rockets,
        IReadOnlyList<int> removedRocketIds,
        bool collectionIsComplete)
    {
        SyncSnapshotEntities(
            FilterTerminatedProjectiles(rockets, static state => state.Id),
            removedRocketIds,
            collectionIsComplete,
            _host.Rockets,
            static state => state.Id,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
            {
                var rocket = new RocketProjectileEntity(
                    state.Id,
                    (PlayerTeam)state.Team,
                    state.OwnerId,
                    state.X,
                    state.Y,
                    state.Speed,
                    state.DirectionRadians,
                    reducedKnockbackSourceTicksRemaining: state.ReducedKnockbackSourceTicksRemaining,
                    zeroKnockbackSourceTicksRemaining: state.ZeroKnockbackSourceTicksRemaining,
                    rangeAnchorOwnerId: state.RangeAnchorOwnerId,
                    lastKnownRangeOriginX: state.LastKnownRangeOriginX,
                    lastKnownRangeOriginY: state.LastKnownRangeOriginY,
                    distanceToTravel: state.DistanceToTravel,
                    isFading: state.IsFading,
                    fadeSourceTicksRemaining: state.FadeSourceTicksRemaining,
                    passedFriendlyPlayerIds: state.PassedFriendlyPlayerIds,
                    isBallistic: state.IsBallistic,
                    ballisticGravityPerTick: state.BallisticGravityPerTick,
                    suppressSmokeTrail: state.SuppressSmokeTrail);
                rocket.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return rocket;
            },
            static (entity, state) => ApplyRocketSnapshotState(entity, state),
            true,
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && _host.Projectiles.ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _host.ClientSnapshots.PredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingRocketState(entity, state))
                {
                    return;
                }

                ApplyRocketSnapshotState(entity, state);
            },
            () => _host.Projectiles.ClearProjectileCollection(_host.Rockets),
            entity => _host.Projectiles.AddProjectileToCollection(_host.Rockets, entity));
    }

    private void ApplySnapshotRocketSpawnEvents(IReadOnlyList<SnapshotRocketSpawnEvent> rocketSpawnEvents)
    {
        for (var index = 0; index < rocketSpawnEvents.Count; index += 1)
        {
            var e = rocketSpawnEvents[index];
            if (e.ExplodeImmediately
                && e.EventId != 0
                && !_host.ClientSnapshots.ProcessedImmediateRocketSpawnEventIds.Add(e.EventId))
            {
                continue;
            }

            if (_host.EntityStore.Contains(e.Id))
            {
                continue;
            }

            // Skip re-spawning rockets that were already terminated on the client.
            // RocketSpawnEvents are retained and replayed for several seconds by the server, so
            // the same spawn event can arrive after the rocket has already been removed (e.g. it
            // exploded and ApplySnapshotRockets cleaned it up). Without this guard the rocket
            // would be recreated from its birth position every snapshot frame until the event
            // expires, producing a ghost rocket that repeatedly flickers near the fire point.
            if (IsProjectileRespawnSuppressed(e.Id))
            {
                continue;
            }

            ReserveEntityId(e.Id);
            var rocket = new RocketProjectileEntity(
                e.Id,
                (PlayerTeam)e.Team,
                e.OwnerId,
                e.X,
                e.Y,
                e.Speed,
                e.DirectionRadians,
                reducedKnockbackSourceTicksRemaining: e.ReducedKnockbackSourceTicksRemaining,
                zeroKnockbackSourceTicksRemaining: e.ZeroKnockbackSourceTicksRemaining,
                rangeAnchorOwnerId: e.RangeAnchorOwnerId,
                lastKnownRangeOriginX: e.LastKnownRangeOriginX,
                lastKnownRangeOriginY: e.LastKnownRangeOriginY,
                distanceToTravel: e.DistanceToTravel,
                isFading: e.IsFading,
                fadeSourceTicksRemaining: e.FadeSourceTicksRemaining,
                isBallistic: e.IsBallistic,
                ballisticGravityPerTick: e.BallisticGravityPerTick,
                suppressSmokeTrail: e.SuppressSmokeTrail);
            rocket.HydrateCritical(e.IsCritical, e.CriticalDamageMultiplier);

            rocket.ApplyNetworkState(
                e.X,
                e.Y,
                e.PreviousX,
                e.PreviousY,
                e.DirectionRadians,
                e.Speed,
                e.TicksRemaining,
                e.ReducedKnockbackSourceTicksRemaining,
                e.ZeroKnockbackSourceTicksRemaining,
                e.RangeAnchorOwnerId,
                e.LastKnownRangeOriginX,
                e.LastKnownRangeOriginY,
                e.DistanceToTravel,
                e.IsFading,
                e.FadeSourceTicksRemaining,
                e.PassedFriendlyPlayerIds ?? Array.Empty<int>());
            if (e.ExplodeImmediately)
            {
                rocket.DelayExplosionUntilNextTick(RocketProjectileEntity.DelayedExplosionReasonSpawnBlocked);
            }

            _host.Projectiles.AddProjectileEntity(rocket, requireUniqueEntityId: true);
            if (_host.Projectiles.ShouldTrackSnapshotProjectileForClientPrediction(e.OwnerId))
            {
                _host.ClientSnapshots.PredictedProjectileIds.Add(rocket.Id);
            }
        }
    }

    internal void ApplySnapshotFlames(
        IReadOnlyList<SnapshotFlameState> flames,
        IReadOnlyList<int> removedFlameIds,
        bool collectionIsComplete)
    {
        SyncSnapshotEntities(
            FilterTerminatedProjectiles(flames, static state => state.Id),
            removedFlameIds,
            collectionIsComplete,
            _host.Flames,
            static state => state.Id,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
            {
                var flame = new FlameProjectileEntity(
                    state.Id,
                    (PlayerTeam)state.Team,
                    state.OwnerId,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY);
                flame.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return flame;
            },
            static (entity, state) => ApplyFlameSnapshotState(entity, state),
            true,
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && _host.Projectiles.ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _host.ClientSnapshots.PredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingFlameState(entity, state))
                {
                    return;
                }

                ApplyFlameSnapshotState(entity, state);
            },
            () => _host.Projectiles.ClearProjectileCollection(_host.Flames),
            entity => _host.Projectiles.AddProjectileToCollection(_host.Flames, entity));
    }

    private void ApplySnapshotMines(
        IReadOnlyList<SnapshotMineState> mines,
        IReadOnlyList<int> removedMineIds,
        bool collectionIsComplete)
    {
        SyncSnapshotEntities(
            FilterTerminatedProjectiles(mines, static state => state.Id),
            removedMineIds,
            collectionIsComplete,
            _host.Mines,
            static state => state.Id,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
            {
                var mine = new MineProjectileEntity(
                    state.Id,
                    (PlayerTeam)state.Team,
                    state.OwnerId,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY);
                mine.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return mine;
            },
            static (entity, state) => ApplyMineSnapshotState(entity, state),
            true,
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && _host.Projectiles.ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _host.ClientSnapshots.PredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingMineState(entity, state))
                {
                    return;
                }

                ApplyMineSnapshotState(entity, state);
            },
            () => _host.Projectiles.ClearProjectileCollection(_host.Mines),
            entity => _host.Projectiles.AddProjectileToCollection(_host.Mines, entity));
    }

    private void ApplySnapshotGrenades(
        IReadOnlyList<SnapshotGrenadeState> grenades,
        IReadOnlyList<int> removedGrenadeIds,
        bool collectionIsComplete)
    {
        SyncSnapshotEntities(
            FilterTerminatedProjectiles(grenades, static state => state.Id),
            removedGrenadeIds,
            collectionIsComplete,
            _host.Grenades,
            static state => state.Id,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
            {
                var grenade = new GrenadeProjectileEntity(
                    state.Id,
                    (PlayerTeam)state.Team,
                    state.OwnerId,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY);
                grenade.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return grenade;
            },
            static (entity, state) => ApplyGrenadeSnapshotState(entity, state),
            true,
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && _host.Projectiles.ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _host.ClientSnapshots.PredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingGrenadeState(entity, state))
                {
                    return;
                }

                ApplyGrenadeSnapshotState(entity, state);
            },
            () => _host.Projectiles.ClearProjectileCollection(_host.Grenades),
            entity => _host.Projectiles.AddProjectileToCollection(_host.Grenades, entity));
    }

    private bool IsProjectileRespawnSuppressed(int projectileId)
    {
        if (!_host.ClientSnapshots.TerminatedProjectileIds.Contains(projectileId))
        {
            return false;
        }

        if (_host.ClientSnapshots.TerminatedProjectileExpiryFrames.TryGetValue(projectileId, out var expiryFrame)
            && _host.Frame > expiryFrame)
        {
            _host.ClientSnapshots.TerminatedProjectileIds.Remove(projectileId);
            _host.ClientSnapshots.TerminatedProjectileExpiryFrames.Remove(projectileId);
            return false;
        }

        return true;
    }

    internal void SuppressProjectileRespawn(int projectileId, int suppressionTicks = 0)
    {
        _host.ClientSnapshots.TerminatedProjectileIds.Add(projectileId);
        if (suppressionTicks > 0)
        {
            _host.ClientSnapshots.TerminatedProjectileExpiryFrames[projectileId] = _host.Frame + suppressionTicks;
            return;
        }

        _host.ClientSnapshots.TerminatedProjectileExpiryFrames.Remove(projectileId);
    }
}
