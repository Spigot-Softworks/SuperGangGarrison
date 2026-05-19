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

        // Apply string cache updates from server
        _snapshotStringCache.ApplyCacheUpdates(snapshot.StringCacheUpdates);

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

    private void ApplySnapshotPlayer(PlayerEntity player, SnapshotPlayerState snapshotPlayer)
    {
        // Resolve cached strings using cache IDs
        var modPackId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayModPackCacheId, snapshotPlayer.GameplayModPackId);
        var loadoutId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayLoadoutCacheId, snapshotPlayer.GameplayLoadoutId);
        var primaryItemId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayPrimaryItemCacheId, snapshotPlayer.GameplayPrimaryItemId);
        var secondaryItemId = _snapshotStringCache.Resolve(snapshotPlayer.GameplaySecondaryItemCacheId, snapshotPlayer.GameplaySecondaryItemId);
        var utilityItemId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayUtilityItemCacheId, snapshotPlayer.GameplayUtilityItemId);
        var equippedItemId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayEquippedItemCacheId, snapshotPlayer.GameplayEquippedItemId);
        var acquiredItemId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayAcquiredItemCacheId, snapshotPlayer.GameplayAcquiredItemId);

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
            snapshotPlayer.IsSpySuperjumping,
            snapshotPlayer.SpySuperjumpHorizontalVelocity,
            snapshotPlayer.SpySuperjumpCooldownTicksRemaining,
            snapshotPlayer.SpyBackstabVisualTicksRemaining,
            snapshotPlayer.IsUbered,
            snapshotPlayer.IsKritzCritBoosted,
            snapshotPlayer.IsHeavyEating,
            snapshotPlayer.HeavyEatTicksRemaining,
            snapshotPlayer.IsSniperScoped,
            snapshotPlayer.SniperChargeTicks,
            snapshotPlayer.IsUsingBinoculars,
            snapshotPlayer.BinocularsFocusX,
            snapshotPlayer.BinocularsFocusY,
            snapshotPlayer.FacingDirectionX,
            snapshotPlayer.AimDirectionDegrees,
            snapshotPlayer.AimWorldX,
            snapshotPlayer.AimWorldY,
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
            snapshotPlayer.BadgeMask,
            snapshotPlayer.IsMedicHealing,
            snapshotPlayer.MedicHealTargetId,
            snapshotPlayer.MedicUberCharge,
            snapshotPlayer.IsMedicUberReady,
            modPackId,
            loadoutId,
            primaryItemId,
            secondaryItemId,
            utilityItemId,
            snapshotPlayer.GameplayEquippedSlot,
            equippedItemId,
            acquiredItemId,
            snapshotPlayer.OwnedGameplayItemIds,
            ConvertReplicatedStateEntries(snapshotPlayer.ReplicatedStates),
            snapshotPlayer.PlayerScale,
            offhandCooldownTicks: snapshotPlayer.OffhandCooldownTicks,
            offhandReloadTicks: snapshotPlayer.OffhandReloadTicks);
    }

    private static GameplayReplicatedStateEntry[] ConvertReplicatedStateEntries(IReadOnlyList<SnapshotReplicatedStateEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        var result = new GameplayReplicatedStateEntry[entries.Count];
        for (var index = 0; index < entries.Count; index += 1)
        {
            var entry = entries[index];
            result[index] = new GameplayReplicatedStateEntry(
                entry.OwnerId,
                entry.Key,
                entry.Kind switch
                {
                    SnapshotReplicatedStateValueKind.Whole => GameplayReplicatedStateValueKind.Whole,
                    SnapshotReplicatedStateValueKind.Scalar => GameplayReplicatedStateValueKind.Scalar,
                    _ => GameplayReplicatedStateValueKind.Toggle,
                },
                entry.IntValue,
                entry.FloatValue,
                entry.BoolValue);
        }

        return result;
    }

    private void ApplySnapshotTransientEntities(SnapshotMessage snapshot)
    {
        ApplySnapshotSentries(snapshot.Sentries);
        ApplySnapshotSentryUpdates(snapshot.SentryUpdateStates);
        ApplySnapshotJumpPads(snapshot.JumpPads);
        ApplySnapshotShots(
            snapshot.Shots,
            _shots,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                var shot = new ShotProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
                if (state.IsCritical)
                    shot.SetCritical();
                return shot;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining);
                if (state.IsCritical && !entity.IsCritical)
                    entity.SetCritical();
            });
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
                var blade = new BladeProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY, hitDamage: 0);
                if (state.IsCritical)
                    blade.SetCritical();
                return blade;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining, hitDamage: 0);
                if (state.IsCritical && !entity.IsCritical)
                    entity.SetCritical();
            });
        ApplySnapshotShots(
            snapshot.Needles,
            _needles,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                var needle = new NeedleProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
                if (state.IsCritical)
                    needle.SetCritical();
                return needle;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining);
                if (state.IsCritical && !entity.IsCritical)
                    entity.SetCritical();
            });
        ApplySnapshotShots(
            snapshot.RevolverShots,
            _revolverShots,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                var shot = new RevolverProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
                if (state.IsCritical)
                    shot.SetCritical();
                return shot;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining);
                if (state.IsCritical && !entity.IsCritical)
                    entity.SetCritical();
            });
        ApplySnapshotRockets(snapshot.Rockets);
        ApplySnapshotRocketSpawnEvents(snapshot.RocketSpawnEvents);
        ApplySnapshotFlames(snapshot.Flames);
        ApplySnapshotShots(
            snapshot.Flares,
            _flares,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                var flare = new FlareProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
                if (state.IsCritical)
                    flare.SetCritical();
                return flare;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining);
                if (state.IsCritical && !entity.IsCritical)
                    entity.SetCritical();
            });
        ApplySnapshotMines(snapshot.Mines);
        ApplySnapshotGrenades(snapshot.Grenades);
        ApplySnapshotGibSpawnEvents(snapshot.GibSpawnEvents);
        // Blood drops are now generated locally on the client - not synced from server
        ApplySnapshotDeadBodies(snapshot.DeadBodies);
        ApplySnapshotSentryGibs(snapshot.SentryGibs);
    }

    private void ApplySnapshotJumpPads(IReadOnlyList<SnapshotJumpPadState> jumpPads)
    {
        SyncSnapshotEntities(
            jumpPads,
            _jumpPads,
            static state => state.Id,
            static (entity, state) => entity.OwnerPlayerId == state.OwnerPlayerId && entity.Team == (PlayerTeam)state.Team,
            state => new JumpPadEntity(
                state.Id,
                state.OwnerPlayerId,
                (PlayerTeam)state.Team,
                state.X,
                state.Y),
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.Health,
                state.HasLanded));
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
                state.AimDirectionDegrees,
                state.ShotTraceTicksRemaining,
                state.HasLanded,
                state.HasActiveTarget,
                state.LastShotTargetX,
                state.LastShotTargetY));
    }

    private void ApplySnapshotSentryUpdates(IReadOnlyList<SnapshotSentryUpdateState> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        // Apply lightweight updates to existing sentries
        for (var index = 0; index < updates.Count; index += 1)
        {
            var update = updates[index];
            var sentry = _sentries.FirstOrDefault(s => s.Id == update.Id);
            if (sentry is not null)
            {
                // Apply only the dynamic fields
                sentry.ApplyNetworkState(
                    update.X,
                    update.Y,
                    update.Health,
                    sentry.IsBuilt, // Keep existing static fields
                    update.FacingDirectionX,
                    update.AimDirectionDegrees,
                    update.ShotTraceTicksRemaining,
                    sentry.HasLanded, // Keep existing static fields
                    update.HasActiveTarget,
                    update.LastShotTargetX,
                    update.LastShotTargetY);
            }
        }
    }

    private void ApplySnapshotShots<T>(
        IReadOnlyList<SnapshotShotState> shots,
        List<T> target,
        Func<T, SnapshotShotState, bool> canReuse,
        Func<SnapshotShotState, T> factory,
        Action<T, SnapshotShotState> applyState)
        where T : SimulationEntity
    {
        SyncSnapshotEntities(
            shots,
            target,
            static state => state.Id,
            canReuse,
            factory,
            applyState,
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && LocalPlayer is not null)
                {
                    // Track new projectiles for client-side prediction
                    _clientPredictedProjectileIds.Add(state.Id);
                }
                // Only apply spawn state - client simulates projectile movement
                if (isNewEntity)
                {
                    applyState(entity, state);
                }
            });
    }

    private void ApplySnapshotRockets(IReadOnlyList<SnapshotRocketState> rockets)
    {
        SyncSnapshotEntities(
            rockets,
            _rockets,
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
                    passedFriendlyPlayerIds: state.PassedFriendlyPlayerIds);
                if (state.IsCritical)
                    rocket.SetCritical();
                return rocket;
            },
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
                state.PassedFriendlyPlayerIds),
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && LocalPlayer is not null)
                {
                    // Track new projectiles for client-side prediction
                    _clientPredictedProjectileIds.Add(state.Id);
                }
                // Only apply spawn state - client simulates projectile movement
                if (isNewEntity)
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
                }
            });
    }

    private void ApplySnapshotRocketSpawnEvents(IReadOnlyList<SnapshotRocketSpawnEvent> rocketSpawnEvents)
    {
        for (var index = 0; index < rocketSpawnEvents.Count; index += 1)
        {
            var e = rocketSpawnEvents[index];
            if (e.ExplodeImmediately
                && e.EventId != 0
                && !_processedImmediateNetworkRocketSpawnEventIds.Add(e.EventId))
            {
                continue;
            }

            if (_entities.ContainsKey(e.Id))
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
                fadeSourceTicksRemaining: e.FadeSourceTicksRemaining);
            if (e.IsCritical)
            {
                rocket.SetCritical();
            }

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
                Array.Empty<int>());
            if (e.ExplodeImmediately)
            {
                rocket.DelayExplosionUntilNextTick(RocketProjectileEntity.DelayedExplosionReasonSpawnBlocked);
            }

            _rockets.Add(rocket);
            _entities.Add(rocket.Id, rocket);
            _clientPredictedProjectileIds.Add(rocket.Id);
        }
    }

    private void ApplySnapshotFlames(IReadOnlyList<SnapshotFlameState> flames)
    {
        SyncSnapshotEntities(
            flames,
            _flames,
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
                if (state.IsCritical)
                    flame.SetCritical();
                return flame;
            },
            static (entity, state) =>
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
                if (state.IsCritical && !entity.IsCritical)
                    entity.SetCritical();
            },
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && LocalPlayer is not null)
                {
                    // Track new projectiles for client-side prediction
                    _clientPredictedProjectileIds.Add(state.Id);
                }
                // Only apply spawn state - client simulates projectile movement
                if (isNewEntity)
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
                }
            });
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
                state.Scale),
            static (entity, state, isNewEntity) =>
            {
                if (isNewEntity)
                {
                    entity.ApplyNetworkState(
                        state.X,
                        state.Y,
                        state.VelocityX,
                        state.VelocityY,
                        state.IsStuck,
                        state.TicksRemaining,
                        state.Scale);
                }
            });
    }

    private void ApplySnapshotMines(IReadOnlyList<SnapshotMineState> mines)
    {
        SyncSnapshotEntities(
            mines,
            _mines,
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
                if (state.IsCritical)
                    mine.SetCritical();
                return mine;
            },
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.VelocityX,
                state.VelocityY,
                state.IsStickied,
                state.IsDestroyed,
                state.ExplosionDamage),
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && LocalPlayer is not null)
                {
                    // Track new projectiles for client-side prediction
                    _clientPredictedProjectileIds.Add(state.Id);
                }
                // Only apply spawn state - client simulates projectile movement
                if (isNewEntity)
                {
                    entity.ApplyNetworkState(
                        state.X,
                        state.Y,
                        state.VelocityX,
                        state.VelocityY,
                        state.IsStickied,
                        state.IsDestroyed,
                        state.ExplosionDamage);
                }
            });
    }

    private void ApplySnapshotGrenades(IReadOnlyList<SnapshotGrenadeState> grenades)
    {
        SyncSnapshotEntities(
            grenades,
            _grenades,
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
                if (state.IsCritical)
                    grenade.SetCritical();
                return grenade;
            },
            static (entity, state) => entity.ApplyNetworkState(
                state.X,
                state.Y,
                state.PreviousX,
                state.PreviousY,
                state.VelocityX,
                state.VelocityY,
                isDestroyed: false,
                GrenadeProjectileEntity.BaseExplosionDamage,
                state.FuseTicksLeft),
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && LocalPlayer is not null)
                {
                    // Track new projectiles for client-side prediction
                    _clientPredictedProjectileIds.Add(state.Id);
                }
                // Only apply spawn state - client simulates grenade physics locally
                if (isNewEntity)
                {
                    entity.ApplyNetworkState(
                        state.X,
                        state.Y,
                        state.PreviousX,
                        state.PreviousY,
                        state.VelocityX,
                        state.VelocityY,
                        isDestroyed: false,
                        GrenadeProjectileEntity.BaseExplosionDamage,
                        state.FuseTicksLeft);
                }
            });
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
                state.TicksRemaining),
            static (entity, state, isNewEntity) =>
            {
                if (isNewEntity)
                {
                    entity.ApplyNetworkState(
                        state.X,
                        state.Y,
                        state.TicksRemaining);
                }
            });
    }

    private void ApplySnapshotGibSpawnEvents(IReadOnlyList<SnapshotGibSpawnEvent> gibSpawnEvents)
    {
        for (var index = 0; index < gibSpawnEvents.Count; index += 1)
        {
            var e = gibSpawnEvents[index];
            if (e.EventId != 0 && !_processedNetworkGibSpawnEventIds.Add(e.EventId))
            {
                continue;
            }

            var gib = new PlayerGibEntity(
                AllocateEntityId(),
                e.SpriteName,
                e.FrameIndex,
                e.X,
                e.Y,
                e.VelocityX,
                e.VelocityY,
                e.RotationSpeedDegrees,
                e.HorizontalFriction,
                e.RotationFriction,
                e.LifetimeTicks,
                e.BloodChance);
            _playerGibs.Add(gib);
            _entities.Add(gib.Id, gib);
        }
    }

    private void SyncRemoteSnapshotPlayers(IEnumerable<SnapshotPlayerState> snapshotPlayers)
    {
        _snapshotSeenRemotePlayerSlots.Clear();
        _remoteSnapshotPlayers.Clear();
        _remoteSnapshotAwaitingJoinSlots.Clear();
        _remoteSnapshotAwaitingJoinPlayerIds.Clear();
        foreach (var snapshotPlayer in snapshotPlayers)
        {
            _snapshotSeenRemotePlayerSlots.Add(snapshotPlayer.Slot);
            var hadRemotePlayer = _remoteSnapshotPlayersBySlot.TryGetValue(snapshotPlayer.Slot, out var existingPlayer);
            PlayerEntity player;
            if (!hadRemotePlayer)
            {
                ReserveEntityId(snapshotPlayer.PlayerId);
                player = new PlayerEntity(
                    snapshotPlayer.PlayerId,
                    CharacterClassCatalog.GetDefinition((PlayerClass)snapshotPlayer.ClassId),
                    snapshotPlayer.Name);
                _remoteSnapshotPlayersBySlot[snapshotPlayer.Slot] = player;
            }
            else
            {
                player = existingPlayer!;
            }

            var wasAlive = player.IsAlive;
            var previousGibDeaths = player.GibDeaths;
            SynchronizeNetworkGibDeathPresentationCount(player.Id, snapshotPlayer.GibDeaths);
            ApplySnapshotPlayer(player, snapshotPlayer);
            if (hadRemotePlayer
                && wasAlive
                && !player.IsAlive
                && snapshotPlayer.GibDeaths > previousGibDeaths
                && TryMarkNetworkGibDeathPresented(player.Id, snapshotPlayer.GibDeaths))
            {
                SpawnClientPlayerGibsFromNetworkDeath(player);
            }

            if (snapshotPlayer.IsAwaitingJoin)
            {
                _remoteSnapshotAwaitingJoinSlots.Add(snapshotPlayer.Slot);
                _remoteSnapshotAwaitingJoinPlayerIds.Add(snapshotPlayer.PlayerId);
            }
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
            var slot = _snapshotStaleRemotePlayerSlots[index];
            if (_remoteSnapshotPlayersBySlot.Remove(slot, out var removedPlayer))
            {
                _presentedNetworkGibDeathCountsByPlayerId.Remove(removedPlayer.Id);
            }
        }
    }

    private void SynchronizeNetworkGibDeathPresentationCount(int playerId, int observedGibDeaths)
    {
        if (!_presentedNetworkGibDeathCountsByPlayerId.TryGetValue(playerId, out var presentedGibDeaths)
            || observedGibDeaths >= presentedGibDeaths)
        {
            return;
        }

        if (observedGibDeaths <= 0)
        {
            _presentedNetworkGibDeathCountsByPlayerId.Remove(playerId);
            return;
        }

        _presentedNetworkGibDeathCountsByPlayerId[playerId] = observedGibDeaths;
    }

    private bool TryMarkNetworkGibDeathPresented(int playerId, int gibDeaths)
    {
        if (gibDeaths <= 0)
        {
            return false;
        }

        if (_presentedNetworkGibDeathCountsByPlayerId.TryGetValue(playerId, out var presentedGibDeaths)
            && gibDeaths <= presentedGibDeaths)
        {
            return false;
        }

        _presentedNetworkGibDeathCountsByPlayerId[playerId] = gibDeaths;
        return true;
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
        SyncSnapshotEntities(snapshotStates, target, idSelector, canReuse, factory, applyState, (entity, state, isNew) => applyState(entity, state));
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        List<TEntity> target,
        Func<TState, int> idSelector,
        Func<TEntity, TState, bool> canReuse,
        Func<TState, TEntity> factory,
        Action<TEntity, TState> applyState,
        Action<TEntity, TState, bool> applyStateForNewEntity)
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
            var isNewEntity = false;
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
                isNewEntity = true;
            }

            if (isNewEntity)
            {
                applyStateForNewEntity(entity, state, true);
            }
            else
            {
                applyStateForNewEntity(entity, state, false);
            }

            target.Add(entity);
            _entities[entityId] = entity;
        }

        for (var index = 0; index < _snapshotStaleEntityIds.Count; index += 1)
        {
            var staleId = _snapshotStaleEntityIds[index];
            _entities.Remove(staleId);
            if (_clientPredictedProjectileIds.Remove(staleId))
            {
                _terminatedProjectileIds.Add(staleId);
            }
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
