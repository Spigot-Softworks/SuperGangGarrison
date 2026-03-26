using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool ApplySnapshot(SnapshotMessage snapshot, byte localPlayerSlot = 1)
    {
        if ((!string.Equals(Level.Name, snapshot.LevelName, StringComparison.OrdinalIgnoreCase)
                || Level.MapAreaIndex != snapshot.MapAreaIndex)
            && !TryLoadLevel(snapshot.LevelName, snapshot.MapAreaIndex, preservePlayerStats: false))
        {
            return false;
        }

        MatchRules = MatchRules with
        {
            Mode = (GameModeKind)snapshot.GameMode,
            TimeLimitTicks = Math.Max(snapshot.TimeRemainingTicks, MatchRules.TimeLimitTicks),
        };
        MatchState = new MatchState(
            (MatchPhase)snapshot.MatchPhase,
            snapshot.TimeRemainingTicks,
            snapshot.WinnerTeam == 0 ? null : (PlayerTeam)snapshot.WinnerTeam);
        LocalDeathCam = snapshot.LocalDeathCam is null
            ? null
            : new LocalDeathCamState(
                snapshot.LocalDeathCam.FocusX,
                snapshot.LocalDeathCam.FocusY,
                snapshot.LocalDeathCam.KillMessage,
                snapshot.LocalDeathCam.KillerName,
                snapshot.LocalDeathCam.KillerTeam == 0 ? null : (PlayerTeam)snapshot.LocalDeathCam.KillerTeam,
                snapshot.LocalDeathCam.Health,
                snapshot.LocalDeathCam.MaxHealth,
                snapshot.LocalDeathCam.RemainingTicks,
                snapshot.LocalDeathCam.InitialTicks);
        RedCaps = snapshot.RedCaps;
        BlueCaps = snapshot.BlueCaps;
        SpectatorCount = Math.Max(0, snapshot.SpectatorCount);
        _spectatorNames.Clear();
        for (var playerIndex = 0; playerIndex < snapshot.Players.Count; playerIndex += 1)
        {
            var player = snapshot.Players[playerIndex];
            if (!player.IsSpectator || string.IsNullOrWhiteSpace(player.Name))
            {
                continue;
            }

            _spectatorNames.Add(player.Name);
        }
        ApplySnapshotControlPoints(snapshot);
        ApplySnapshotGenerators(snapshot);
        RedIntel.ApplyNetworkState(snapshot.RedIntel.X, snapshot.RedIntel.Y, snapshot.RedIntel.IsAtBase, snapshot.RedIntel.IsDropped, snapshot.RedIntel.ReturnTicksRemaining);
        BlueIntel.ApplyNetworkState(snapshot.BlueIntel.X, snapshot.BlueIntel.Y, snapshot.BlueIntel.IsAtBase, snapshot.BlueIntel.IsDropped, snapshot.BlueIntel.ReturnTicksRemaining);
        _killFeed.Clear();
        for (var killFeedIndex = 0; killFeedIndex < snapshot.KillFeed.Count; killFeedIndex += 1)
        {
            var entry = snapshot.KillFeed[killFeedIndex];
            _killFeed.Add(new KillFeedEntry(
                entry.KillerName,
                (PlayerTeam)entry.KillerTeam,
                entry.WeaponSpriteName,
                entry.VictimName,
                (PlayerTeam)entry.VictimTeam,
                entry.MessageText,
                entry.KillerPlayerId,
                entry.VictimPlayerId,
                (KillFeedSpecialType)entry.SpecialType,
                entry.EventId));
        }
        _killFeedTrimTicks = _killFeed.Count > 0 ? KillFeedLifetimeTicks : 0;

        var localPlayerState = snapshot.Players.FirstOrDefault(player => player.Slot == localPlayerSlot);
        var isSpectatorSnapshot = localPlayerState?.IsSpectator ?? !IsPlayableNetworkPlayerSlot(localPlayerSlot);
        if (localPlayerState is null && !isSpectatorSnapshot)
        {
            return false;
        }

        if (localPlayerState is not null && !localPlayerState.IsSpectator)
        {
            ApplySnapshotPlayer(LocalPlayer, localPlayerState);
            TrySetNetworkPlayerAwaitingJoin(LocalPlayerSlot, localPlayerState.IsAwaitingJoin);
            TrySetNetworkPlayerRespawnTicks(LocalPlayerSlot, localPlayerState.RespawnTicks);
        }
        else
        {
            TrySetNetworkPlayerAwaitingJoin(LocalPlayerSlot, true);
            TrySetNetworkPlayerRespawnTicks(LocalPlayerSlot, 0);
            LocalDeathCam = null;
            LocalPlayer.ClearMedicHealingTarget();
            LocalPlayer.Kill();
        }

        var remotePlayerStates = snapshot.Players
            .Where(player => !player.IsSpectator)
            .Where(player => IsPlayableNetworkPlayerSlot(player.Slot))
            .Where(player => isSpectatorSnapshot || player.Slot != localPlayerSlot)
            .OrderBy(player => player.Slot)
            .ToList();
        EnemyPlayerEnabled = false;
        _enemyDummyRespawnTicks = 0;
        ClearEnemyInputOverride();
        EnemyPlayer.Kill();
        FriendlyDummyEnabled = false;
        FriendlyDummy.Kill();
        SyncRemoteSnapshotPlayers(remotePlayerStates);

        if (localPlayerState is not null && !localPlayerState.IsSpectator)
        {
            TrySetNetworkPlayerConfiguredTeam(LocalPlayerSlot, LocalPlayer.Team);
        }
        ApplySnapshotTransientEntities(snapshot);
        _combatTraces.Clear();
        for (var traceIndex = 0; traceIndex < snapshot.CombatTraces.Count; traceIndex += 1)
        {
            var trace = snapshot.CombatTraces[traceIndex];
            _combatTraces.Add(new CombatTrace(
                trace.StartX,
                trace.StartY,
                trace.EndX,
                trace.EndY,
                trace.TicksRemaining,
                trace.HitCharacter,
                (PlayerTeam)trace.Team,
                trace.IsSniperTracer));
        }
        _pendingSoundEvents.Clear();
        for (var soundIndex = 0; soundIndex < snapshot.SoundEvents.Count; soundIndex += 1)
        {
            var soundEvent = snapshot.SoundEvents[soundIndex];
            _pendingSoundEvents.Add(new WorldSoundEvent(
                soundEvent.SoundName,
                soundEvent.X,
                soundEvent.Y,
                soundEvent.EventId,
                soundEvent.SourceFrame));
        }

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
            snapshotPlayer.HeavyEatCooldownTicksRemaining);
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
                entity.ClassId == (PlayerClass)state.ClassId
                && entity.Team == (PlayerTeam)state.Team
                && entity.Width == state.Width
                && entity.Height == state.Height
                && entity.FacingLeft == state.FacingLeft,
            state => new DeadBodyEntity(
                state.Id,
                (PlayerClass)state.ClassId,
                (PlayerTeam)state.Team,
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
