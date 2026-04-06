using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool ApplySnapshot(SnapshotMessage snapshot, byte localPlayerSlot = 1)
    {
        if (!EnsureSnapshotLevelLoaded(snapshot))
        {
            return false;
        }

        ApplySnapshotWorldState(snapshot);

        if (!TryResolveSnapshotLocalPlayerState(
                snapshot,
                localPlayerSlot,
                out var localPlayerState,
                out var isSpectatorSnapshot))
        {
            return false;
        }

        ApplySnapshotPlayerState(snapshot, localPlayerSlot, localPlayerState, isSpectatorSnapshot);
        ApplySnapshotTransientEntities(snapshot);
        ApplySnapshotEventQueues(snapshot);

        return true;
    }

    private static void ApplySnapshotPlayer(PlayerEntity player, SnapshotPlayerState snapshotPlayer)
    {
        player.SetDisplayName(snapshotPlayer.Name);
        player.ApplyNetworkState(
            (PlayerTeam)snapshotPlayer.Team,
            CharacterClassCatalog.GetDefinition((PlayerClass)snapshotPlayer.ClassId),
            snapshotPlayer.IsAlive,
            snapshotPlayer.X,
            snapshotPlayer.Y,
            snapshotPlayer.HorizontalSpeed,
            snapshotPlayer.VerticalSpeed,
            snapshotPlayer.Health,
            snapshotPlayer.Ammo,
            snapshotPlayer.Kills,
            snapshotPlayer.Deaths,
            snapshotPlayer.Caps,
            snapshotPlayer.Points,
            snapshotPlayer.HealPoints,
            snapshotPlayer.ActiveDominationCount,
            snapshotPlayer.IsDominatingLocalViewer,
            snapshotPlayer.IsDominatedByLocalViewer,
            snapshotPlayer.Metal,
            snapshotPlayer.IsGrounded,
            snapshotPlayer.RemainingAirJumps,
            snapshotPlayer.IsCarryingIntel,
            snapshotPlayer.IntelRechargeTicks,
            snapshotPlayer.IsSpyCloaked,
            snapshotPlayer.SpyCloakAlpha,
            snapshotPlayer.IsUbered,
            snapshotPlayer.IsHeavyEating,
            snapshotPlayer.HeavyEatTicksRemaining,
            snapshotPlayer.IsSniperScoped,
            snapshotPlayer.SniperChargeTicks,
            snapshotPlayer.FacingDirectionX,
            snapshotPlayer.AimDirectionDegrees,
            snapshotPlayer.IsTaunting,
            snapshotPlayer.TauntFrameIndex,
            snapshotPlayer.IsChatBubbleVisible,
            snapshotPlayer.ChatBubbleFrameIndex,
            snapshotPlayer.ChatBubbleAlpha,
            snapshotPlayer.BurnIntensity,
            snapshotPlayer.BurnDurationSourceTicks,
            snapshotPlayer.BurnDecayDelaySourceTicksRemaining,
            snapshotPlayer.BurnIntensityDecayPerSourceTick,
            snapshotPlayer.BurnedByPlayerId,
            snapshotPlayer.MovementState,
            snapshotPlayer.PrimaryCooldownTicks,
            snapshotPlayer.ReloadTicksUntilNextShell,
            snapshotPlayer.MedicNeedleCooldownTicks,
            snapshotPlayer.MedicNeedleRefillTicks,
            snapshotPlayer.PyroAirblastCooldownTicks,
            snapshotPlayer.PyroFlareCooldownTicks,
            snapshotPlayer.PyroPrimaryFuelScaled,
            snapshotPlayer.IsPyroPrimaryRefilling,
            snapshotPlayer.PyroFlameLoopTicksRemaining,
            snapshotPlayer.PyroPrimaryRequiresReleaseAfterEmpty,
            snapshotPlayer.HeavyEatCooldownTicksRemaining,
            snapshotPlayer.Assists,
            snapshotPlayer.BadgeMask);
    }

    private void ApplySnapshotTransientEntities(SnapshotMessage snapshot)
    {
        ApplySnapshotSentries(snapshot.Sentries);
        ApplySnapshotShots(
            snapshot.Shots,
            _shots,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                return new ShotProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
            },
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining));
        ApplySnapshotShots(
            snapshot.Bubbles,
            _bubbles,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                return new BubbleProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
            },
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining));
        ApplySnapshotShots(
            snapshot.Blades,
            _blades,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                return new BladeProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY, hitDamage: 0);
            },
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining, hitDamage: 0));
        ApplySnapshotShots(
            snapshot.Needles,
            _needles,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                return new NeedleProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
            },
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining));
        ApplySnapshotShots(
            snapshot.RevolverShots,
            _revolverShots,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                return new RevolverProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
            },
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining));
        ApplySnapshotRockets(snapshot.Rockets);
        ApplySnapshotFlames(snapshot.Flames);
        ApplySnapshotShots(
            snapshot.Flares,
            _flares,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                return new FlareProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
            },
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining));
        ApplySnapshotMines(snapshot.Mines);
        ApplySnapshotPlayerGibs(snapshot.PlayerGibs);
        ApplySnapshotBloodDrops(snapshot.BloodDrops);
        ApplySnapshotDeadBodies(snapshot.DeadBodies);
        ApplySnapshotSentryGibs(snapshot.SentryGibs);
    }

    private void ApplySnapshotSentries(IReadOnlyList<SnapshotSentryState> sentries)
    {
        SyncSnapshotEntities(
            sentries,
            _sentries,
            static state => state.Id,
            static (entity, state) => entity.OwnerPlayerId == state.OwnerPlayerId && entity.Team == (PlayerTeam)state.Team,
            state => new SentryEntity(
                state.Id,
                state.OwnerPlayerId,
                (PlayerTeam)state.Team,
                state.X,
                state.Y,
                state.FacingDirectionX),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.Health,
                state.IsBuilt,
                state.FacingDirectionX,
                state.DesiredFacingDirectionX,
                state.AimDirectionDegrees,
                state.ReloadTicksRemaining,
                state.AlertTicksRemaining,
                state.ShotTraceTicksRemaining,
                state.HasLanded,
                state.HasActiveTarget,
                state.CurrentTargetPlayerId < 0 ? null : state.CurrentTargetPlayerId,
                state.LastShotTargetX,
                state.LastShotTargetY));
    }

    private void ApplySnapshotShots<T>(
        IReadOnlyList<SnapshotShotState> shots,
        List<T> target,
        Func<T, SnapshotShotState, bool> canReuse,
        Func<SnapshotShotState, T> factory,
        Action<T, SnapshotShotState> applyState)
        where T : SimulationEntity
    {
        SyncSnapshotEntities(shots, target, static state => state.Id, canReuse, factory, applyState);
    }

    private void ApplySnapshotRockets(IReadOnlyList<SnapshotRocketState> rockets)
    {
        SyncSnapshotEntities(
            rockets,
            _rockets,
            static state => state.Id,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state => new RocketProjectileEntity(
                state.Id,
                (PlayerTeam)state.Team,
                state.OwnerId,
                state.X,
                state.Y,
                state.Speed,
                state.DirectionRadians,
                state.ReducedKnockbackSourceTicksRemaining,
                state.ZeroKnockbackSourceTicksRemaining,
                state.RangeAnchorOwnerId,
                state.LastKnownRangeOriginX,
                state.LastKnownRangeOriginY,
                state.DistanceToTravel,
                state.IsFading,
                state.FadeSourceTicksRemaining,
                state.PassedFriendlyPlayerIds),
            static (entity, state) => entity.ApplyNetworkState(
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
                state.PassedFriendlyPlayerIds));
    }

    private void ApplySnapshotFlames(IReadOnlyList<SnapshotFlameState> flames)
    {
        SyncSnapshotEntities(
            flames,
            _flames,
            static state => state.Id,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state => new FlameProjectileEntity(
                state.Id,
                (PlayerTeam)state.Team,
                state.OwnerId,
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.PreviousX,
                state.PreviousY,
                state.VelocityX,
                state.VelocityY,
                state.TicksRemaining,
                state.AttachedPlayerId < 0 ? null : state.AttachedPlayerId,
                state.AttachedOffsetX,
                state.AttachedOffsetY));
    }

    private void ApplySnapshotBloodDrops(IReadOnlyList<SnapshotBloodDropState> bloodDrops)
    {
        SyncSnapshotEntities(
            bloodDrops,
            _bloodDrops,
            static state => state.Id,
            static (_, _) => true,
            state => new BloodDropEntity(
                state.Id,
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                state.Scale),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                state.IsStuck,
                state.TicksRemaining,
                state.Scale));
    }

    private void ApplySnapshotMines(IReadOnlyList<SnapshotMineState> mines)
    {
        SyncSnapshotEntities(
            mines,
            _mines,
            static state => state.Id,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state => new MineProjectileEntity(
                state.Id,
                (PlayerTeam)state.Team,
                state.OwnerId,
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                state.IsStickied,
                state.IsDestroyed,
                state.ExplosionDamage));
    }

    private void ApplySnapshotDeadBodies(IReadOnlyList<SnapshotDeadBodyState> deadBodies)
    {
        SyncSnapshotEntities(
            deadBodies,
            _deadBodies,
            static state => state.Id,
            static (entity, state) =>
                entity.SourcePlayerId == state.SourcePlayerId
                && entity.ClassId == (PlayerClass)state.ClassId
                && entity.Team == (PlayerTeam)state.Team
                && entity.AnimationKind == (DeadBodyAnimationKind)state.AnimationKind
                && entity.Width == state.Width
                && entity.Height == state.Height
                && entity.FacingLeft == state.FacingLeft,
            state => new DeadBodyEntity(
                state.Id,
                state.SourcePlayerId,
                (PlayerClass)state.ClassId,
                (PlayerTeam)state.Team,
                (DeadBodyAnimationKind)state.AnimationKind,
                state.X,
                state.Y,
                state.Width,
                state.Height,
                state.HorizontalSpeed,
                state.VerticalSpeed,
                state.FacingLeft),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.HorizontalSpeed,
                state.VerticalSpeed,
                state.TicksRemaining));
    }

    private void ApplySnapshotSentryGibs(IReadOnlyList<SnapshotSentryGibState> sentryGibs)
    {
        SyncSnapshotEntities(
            sentryGibs,
            _sentryGibs,
            static state => state.Id,
            static (entity, state) =>
                entity.Team == (PlayerTeam)state.Team,
            state => new SentryGibEntity(
                state.Id,
                (PlayerTeam)state.Team,
                state.X,
                state.Y),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.TicksRemaining));
    }

    private void ApplySnapshotPlayerGibs(IReadOnlyList<SnapshotPlayerGibState> playerGibs)
    {
        SyncSnapshotEntities(
            playerGibs,
            _playerGibs,
            static state => state.Id,
            static (entity, state) =>
                string.Equals(entity.SpriteName, state.SpriteName, StringComparison.Ordinal)
                && entity.FrameIndex == state.FrameIndex
                && entity.BloodChance == state.BloodChance,
            state => new PlayerGibEntity(
                state.Id,
                state.SpriteName,
                state.FrameIndex,
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                state.RotationSpeedDegrees,
                horizontalFriction: 0.4f,
                rotationFriction: 0.6f,
                lifetimeTicks: state.TicksRemaining,
                bloodChance: state.BloodChance),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                state.RotationDegrees,
                state.RotationSpeedDegrees,
                state.TicksRemaining));
    }

    private void SyncRemoteSnapshotPlayers(IEnumerable<SnapshotPlayerState> snapshotPlayers)
    {
        _snapshotSeenRemotePlayerSlots.Clear();
        _remoteSnapshotPlayers.Clear();
        foreach (var snapshotPlayer in snapshotPlayers)
        {
            _snapshotSeenRemotePlayerSlots.Add(snapshotPlayer.Slot);
            if (!_remoteSnapshotPlayersBySlot.TryGetValue(snapshotPlayer.Slot, out var player))
            {
                ReserveEntityId(snapshotPlayer.PlayerId);
                player = new PlayerEntity(
                    snapshotPlayer.PlayerId,
                    CharacterClassCatalog.GetDefinition((PlayerClass)snapshotPlayer.ClassId),
                    snapshotPlayer.Name);
                _remoteSnapshotPlayersBySlot[snapshotPlayer.Slot] = player;
            }

            ApplySnapshotPlayer(player, snapshotPlayer);
            _remoteSnapshotPlayers.Add(player);
        }

        _snapshotStaleRemotePlayerSlots.Clear();
        foreach (var entry in _remoteSnapshotPlayersBySlot)
        {
            if (_snapshotSeenRemotePlayerSlots.Contains(entry.Key))
            {
                continue;
            }

            _snapshotStaleRemotePlayerSlots.Add(entry.Key);
        }

        for (var index = 0; index < _snapshotStaleRemotePlayerSlots.Count; index += 1)
        {
            _remoteSnapshotPlayersBySlot.Remove(_snapshotStaleRemotePlayerSlots[index]);
        }
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        List<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState)
        where TEntity : SimulationEntity
    {
        _snapshotSeenEntityIds.Clear();
        for (var index = 0; index < snapshotStates.Count; index += 1)
        {
            _snapshotSeenEntityIds.Add(idSelector(snapshotStates[index]));
        }

        _snapshotStaleEntityIds.Clear();
        for (var index = 0; index < target.Count; index += 1)
        {
            var entityId = target[index].Id;
            if (!_snapshotSeenEntityIds.Contains(entityId))
            {
                _snapshotStaleEntityIds.Add(entityId);
            }
        }

        target.Clear();
        for (var index = 0; index < snapshotStates.Count; index += 1)
        {
            var state = snapshotStates[index];
            var entityId = idSelector(state);
            ReserveEntityId(entityId);

            TEntity entity;
            if (_entities.TryGetValue(entityId, out var existingEntity)
                && existingEntity is TEntity typedEntity
                && canReuse(typedEntity, state))
            {
                entity = typedEntity;
            }
            else
            {
                if (existingEntity is not null)
                {
                    _entities.Remove(entityId);
                }

                entity = factory(state);
            }

            applyState(entity, state);
            target.Add(entity);
            _entities[entityId] = entity;
        }

        for (var index = 0; index < _snapshotStaleEntityIds.Count; index += 1)
        {
            _entities.Remove(_snapshotStaleEntityIds[index]);
        }
    }

    private void ReserveEntityId(int entityId)
    {
        if (entityId >= _nextEntityId)
        {
            _nextEntityId = entityId + 1;
        }
    }
}
