using System;
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
        var gameplayClassId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayClassCacheId, snapshotPlayer.GameplayClassId);
        var classDefinition = ResolveSnapshotClassDefinition(snapshotPlayer, gameplayClassId);

        ApplySnapshotNetworkPlayerReady(snapshotPlayer.Slot, snapshotPlayer.IsReady);
        player.SetDisplayName(snapshotPlayer.Name);

        // Preserve the locally-advancing taunt frame when the player is still taunting.
        // When the server stops sending IsTaunting (taunt finished server-side), allow the
        // local animation to complete naturally rather than cutting it off mid-way — this
        // corrects for the latency offset where the client started counting from 0 while
        // the server was already partway through. Only hard-stop when the player dies.
        var localIsTaunting = snapshotPlayer.IsTaunting
            || (player.IsTaunting && snapshotPlayer.IsAlive);
        var tauntFrameIndex = localIsTaunting && player.IsTaunting
            ? player.TauntFrameIndex
            : 0f;

        // Preserve the locally-advancing sniper charge when the player is still scoped;
        // reset to 0 on scope start so the simulation begins from 0.
        var sniperChargeTicks = player.IsSniperScoped && snapshotPlayer.IsSniperScoped
            ? player.SniperChargeTicks
            : 0;

        player.ApplyNetworkState(
            (PlayerTeam)snapshotPlayer.Team,
            classDefinition,
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
            sniperChargeTicks,
            snapshotPlayer.IsUsingBinoculars,
            snapshotPlayer.BinocularsFocusX,
            snapshotPlayer.BinocularsFocusY,
            snapshotPlayer.FacingDirectionX,
            snapshotPlayer.AimDirectionDegrees,
            snapshotPlayer.AimWorldX,
            snapshotPlayer.AimWorldY,
            localIsTaunting,
            tauntFrameIndex,
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
            offhandReloadTicks: snapshotPlayer.OffhandReloadTicks,
            gibDeaths: snapshotPlayer.GibDeaths,
            isTypingChatMessage: snapshotPlayer.IsTypingChatMessage);
    }

    private static CharacterClassDefinition ResolveSnapshotClassDefinition(SnapshotPlayerState snapshotPlayer, string gameplayClassId)
    {
        if (!string.IsNullOrWhiteSpace(gameplayClassId)
            && CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(gameplayClassId, out _))
        {
            return CharacterClassCatalog.GetDefinition(gameplayClassId);
        }

        var playerClass = Enum.IsDefined(typeof(PlayerClass), (int)snapshotPlayer.ClassId)
            ? (PlayerClass)snapshotPlayer.ClassId
            : PlayerClass.Scout;
        return CharacterClassCatalog.GetDefinition(playerClass);
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
            snapshot.RemovedShotIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Shots),
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
            },
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining));
        ApplySnapshotShots(
            snapshot.Bubbles,
            snapshot.RemovedBubbleIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Bubbles),
            _bubbles,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                return new BubbleProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
            },
            static (entity, state) => entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining),
            shouldApplyExistingState: static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining));
        ApplySnapshotShots(
            snapshot.Blades,
            snapshot.RemovedBladeIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Blades),
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
            },
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId, bloodCount: 6),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining));
        ApplySnapshotShots(
            snapshot.Needles,
            snapshot.RemovedNeedleIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Needles),
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
            },
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining));
        ApplySnapshotShots(
            snapshot.RevolverShots,
            snapshot.RemovedRevolverShotIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.RevolverShots),
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
            },
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining));
        ApplySnapshotRockets(
            snapshot.Rockets,
            snapshot.RemovedRocketIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Rockets));
        ApplySnapshotRocketSpawnEvents(snapshot.RocketSpawnEvents);
        ApplySnapshotFlames(
            snapshot.Flames,
            snapshot.RemovedFlameIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Flames));
        ApplySnapshotShots(
            snapshot.Flares,
            snapshot.RemovedFlareIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Flares),
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
            },
            shouldApplyExistingState: static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining));
        ApplySnapshotMines(
            snapshot.Mines,
            snapshot.RemovedMineIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Mines));
        ApplySnapshotGrenades(
            snapshot.Grenades,
            snapshot.RemovedGrenadeIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Grenades));
        ApplySnapshotGibSpawnEvents(snapshot.GibSpawnEvents);
        // Blood drops are now generated locally on the client - not synced from server
        ApplySnapshotDeadBodies(snapshot.DeadBodies);
        ApplySnapshotSentryGibs(snapshot.SentryGibs);
        ApplySnapshotJumpPadGibs(snapshot.JumpPadGibs);
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
                state.HasLanded,
                state.IsBuilt));
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
        IReadOnlyList<int> removedShotIds,
        bool collectionIsComplete,
        List<T> target,
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
                if (!_clientPredictedProjectileIds.Contains(entity.Id))
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
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _clientPredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity
                    && shouldApplyExistingState is not null
                    && !shouldApplyExistingState(entity, state))
                {
                    return;
                }

                applyState(entity, state);
            });
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
        if (state.IsCritical && !entity.IsCritical)
        {
            entity.SetCritical();
        }
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
        if (state.IsCritical && !entity.IsCritical)
        {
            entity.SetCritical();
        }
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
        if (state.IsCritical && !entity.IsCritical)
        {
            entity.SetCritical();
        }
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
            GrenadeProjectileEntity.BaseExplosionDamage,
            state.FuseTicksLeft);
        if (state.IsCritical && !entity.IsCritical)
        {
            entity.SetCritical();
        }
    }

    private static bool ShouldApplyExistingRocketState(RocketProjectileEntity entity, SnapshotRocketState state)
    {
        if (ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining))
        {
            return true;
        }

        return (state.IsCritical && !entity.IsCritical)
            || (!entity.IsFading && state.IsFading);
    }

    private static bool ShouldApplyExistingFlameState(FlameProjectileEntity entity, SnapshotFlameState state)
    {
        var attachedPlayerId = state.AttachedPlayerId < 0 ? (int?)null : state.AttachedPlayerId;
        if (ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining))
        {
            return true;
        }

        return (state.IsCritical && !entity.IsCritical)
            || (attachedPlayerId.HasValue && entity.AttachedPlayerId != attachedPlayerId);
    }

    private static bool ShouldApplyExistingMineState(MineProjectileEntity entity, SnapshotMineState state)
    {
        return (!entity.IsStickied && state.IsStickied)
            || (!entity.IsDestroyed && state.IsDestroyed)
            || state.IsCritical && !entity.IsCritical;
    }

    private static bool ShouldApplyExistingGrenadeState(GrenadeProjectileEntity entity, SnapshotGrenadeState state)
    {
        return ShouldApplyLocallySimulatedProjectileState(entity.FuseTicksLeft, state.FuseTicksLeft)
            || state.IsCritical && !entity.IsCritical;
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

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || !CanTeamDamagePlayer(team, ownerId, player) || player.Id == ownerId)
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
            RegisterBloodEffect(
                nearestPlayer.X, nearestPlayer.Y,
                MathF.Atan2(dirY, dirX) * (180f / MathF.PI) - 180f,
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
        if (_terminatedProjectileIds.Count == 0)
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
            static (entity, state) => ApplyRocketSnapshotState(entity, state),
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _clientPredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingRocketState(entity, state))
                {
                    return;
                }

                ApplyRocketSnapshotState(entity, state);
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
                e.PassedFriendlyPlayerIds ?? Array.Empty<int>());
            if (e.ExplodeImmediately)
            {
                rocket.DelayExplosionUntilNextTick(RocketProjectileEntity.DelayedExplosionReasonSpawnBlocked);
            }

            _rockets.Add(rocket);
            _entities.Add(rocket.Id, rocket);
            if (ShouldTrackSnapshotProjectileForClientPrediction(e.OwnerId))
            {
                _clientPredictedProjectileIds.Add(rocket.Id);
            }
        }
    }

    private void ApplySnapshotFlames(
        IReadOnlyList<SnapshotFlameState> flames,
        IReadOnlyList<int> removedFlameIds,
        bool collectionIsComplete)
    {
        SyncSnapshotEntities(
            FilterTerminatedProjectiles(flames, static state => state.Id),
            removedFlameIds,
            collectionIsComplete,
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
            static (entity, state) => ApplyFlameSnapshotState(entity, state),
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _clientPredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingFlameState(entity, state))
                {
                    return;
                }

                ApplyFlameSnapshotState(entity, state);
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

    private void ApplySnapshotMines(
        IReadOnlyList<SnapshotMineState> mines,
        IReadOnlyList<int> removedMineIds,
        bool collectionIsComplete)
    {
        SyncSnapshotEntities(
            FilterTerminatedProjectiles(mines, static state => state.Id),
            removedMineIds,
            collectionIsComplete,
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
            static (entity, state) => ApplyMineSnapshotState(entity, state),
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _clientPredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingMineState(entity, state))
                {
                    return;
                }

                ApplyMineSnapshotState(entity, state);
            });
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
            static (entity, state) => ApplyGrenadeSnapshotState(entity, state),
            (entity, state, isNewEntity) =>
            {
                if (isNewEntity && ShouldTrackSnapshotProjectileForClientPrediction(state.OwnerId))
                {
                    _clientPredictedProjectileIds.Add(state.Id);
                }

                if (!isNewEntity && !ShouldApplyExistingGrenadeState(entity, state))
                {
                    return;
                }

                ApplyGrenadeSnapshotState(entity, state);
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

    private void ApplySnapshotJumpPadGibs(IReadOnlyList<SnapshotJumpPadGibState> jumpPadGibs)
    {
        SyncSnapshotEntities(
            jumpPadGibs,
            _jumpPadGibs,
            static state => state.Id,
            static (entity, state) =>
                entity.Team == (PlayerTeam)state.Team,
            state => new JumpPadGibEntity(
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

    internal void AdvanceRemoteSnapshotPlayerTauntStates()
    {
        if (ClientPredictionMode && LocalPlayer.IsAlive && LocalPlayer.IsTaunting)
        {
            LocalPlayer.AdvanceTauntFrameLocally(Config.FixedDeltaSeconds);
        }

        for (var index = 0; index < _remoteSnapshotPlayers.Count; index += 1)
        {
            var player = _remoteSnapshotPlayers[index];
            if (player.IsAlive && player.IsTaunting)
            {
                player.AdvanceTauntFrameLocally(Config.FixedDeltaSeconds);
            }
        }
    }

    private void SyncRemoteSnapshotPlayers(IEnumerable<SnapshotPlayerState> snapshotPlayers)
    {
        _snapshotSeenRemotePlayerSlots.Clear();
        _remoteSnapshotPlayers.Clear();
        _remoteSnapshotScoreboardPlayers.Clear();
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
                var gameplayClassId = _snapshotStringCache.Resolve(snapshotPlayer.GameplayClassCacheId, snapshotPlayer.GameplayClassId);
                player = new PlayerEntity(
                    snapshotPlayer.PlayerId,
                    ResolveSnapshotClassDefinition(snapshotPlayer, gameplayClassId),
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
            if (!_remoteSnapshotPlayersBySlot.TryGetValue(slot, out var removedPlayer))
            {
                continue;
            }

            if (ShouldRetainMissingRemoteSnapshotPlayerForScoreboard(removedPlayer))
            {
                continue;
            }

            if (_remoteSnapshotPlayersBySlot.Remove(slot))
            {
                ApplySnapshotNetworkPlayerReady(slot, ready: false);
                _presentedNetworkGibDeathCountsByPlayerId.Remove(removedPlayer.Id);
            }
        }

        for (var slot = LocalPlayerSlot; slot <= MaxPlayableNetworkPlayers; slot += 1)
        {
            if (_remoteSnapshotPlayersBySlot.TryGetValue((byte)slot, out var player))
            {
                _remoteSnapshotScoreboardPlayers.Add(player);
            }
        }
    }

    private bool ShouldRetainMissingRemoteSnapshotPlayerForScoreboard(PlayerEntity player)
    {
        return LocalPlayer.IsAlive
            && player.Team != LocalPlayer.Team
            && player.ClassId == PlayerClass.Spy
            && (!player.IsSpyVisibleToEnemies || player.IsSpyBackstabAnimating)
            && IsRemoteSpyHiddenFromLocalPlayer(player);
    }

    private bool IsRemoteSpyHiddenFromLocalPlayer(PlayerEntity spy)
    {
        var radians = MathF.PI * LocalPlayer.AimDirectionDegrees / 180f;
        var viewerFacingSign = MathF.Cos(radians) < 0f ? -1 : 1;
        return Math.Sign(spy.X - LocalPlayer.X) == -viewerFacingSign;
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

    public bool TryPresentNetworkGibDeath(int playerId, int gibDeaths, float? spawnX = null, float? spawnY = null)
    {
        var player = FindNetworkPresentationPlayerById(playerId);
        if (player is null)
        {
            return false;
        }

        if (!TryMarkNetworkGibDeathPresented(playerId, gibDeaths))
        {
            return false;
        }

        SpawnClientPlayerGibsFromNetworkDeath(player, spawnX, spawnY);
        return true;
    }

    private static bool IsSnapshotEntityCollectionComplete(
        SnapshotMessage snapshot,
        SnapshotEntityCollectionCompletenessFlags flag)
    {
        return (snapshot.EntityCollectionCompletenessFlags & flag) != 0;
    }

    private PlayerEntity? FindNetworkPresentationPlayerById(int playerId)
    {
        if (LocalPlayer.Id == playerId)
        {
            return LocalPlayer;
        }

        foreach (var player in _remoteSnapshotPlayersBySlot.Values)
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return FindPlayerById(playerId);
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
        SyncSnapshotEntities(
            snapshotStates,
            Array.Empty<int>(),
            collectionIsComplete: true,
            target,
            idSelector,
            canReuse,
            factory,
            applyState,
            (entity, state, isNew) => applyState(entity, state));
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
        SyncSnapshotEntities(
            snapshotStates,
            Array.Empty<int>(),
            collectionIsComplete: true,
            target,
            idSelector,
            canReuse,
            factory,
            applyState,
            applyStateForNewEntity);
    }

    private void SyncSnapshotEntities<TState, TEntity>(
        IReadOnlyList<TState> snapshotStates,
        IReadOnlyList<int> removedEntityIds,
        bool collectionIsComplete,
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
        List<TEntity>? retainedEntities = null;
        for (var index = 0; index < target.Count; index += 1)
        {
            var entityId = target[index].Id;
            var explicitlyRemoved = ContainsEntityId(removedEntityIds, entityId);
            if (explicitlyRemoved || (collectionIsComplete && !_snapshotSeenEntityIds.Contains(entityId)))
            {
                _snapshotStaleEntityIds.Add(entityId);
                continue;
            }

            if (!_snapshotSeenEntityIds.Contains(entityId))
            {
                retainedEntities ??= new List<TEntity>();
                retainedEntities.Add(target[index]);
            }
        }

        target.Clear();
        if (retainedEntities is not null)
        {
            target.AddRange(retainedEntities);
        }

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
                SuppressProjectileRespawn(staleId, NetworkProjectileRemovalSuppressionTicks);
            }
        }
    }

    private static bool ContainsEntityId(IReadOnlyList<int> ids, int entityId)
    {
        for (var index = 0; index < ids.Count; index += 1)
        {
            if (ids[index] == entityId)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsProjectileRespawnSuppressed(int projectileId)
    {
        if (!_terminatedProjectileIds.Contains(projectileId))
        {
            return false;
        }

        if (_terminatedProjectileExpiryFrames.TryGetValue(projectileId, out var expiryFrame)
            && Frame > expiryFrame)
        {
            _terminatedProjectileIds.Remove(projectileId);
            _terminatedProjectileExpiryFrames.Remove(projectileId);
            return false;
        }

        return true;
    }

    private void SuppressProjectileRespawn(int projectileId, int suppressionTicks = 0)
    {
        _terminatedProjectileIds.Add(projectileId);
        if (suppressionTicks > 0)
        {
            _terminatedProjectileExpiryFrames[projectileId] = Frame + suppressionTicks;
            return;
        }

        _terminatedProjectileExpiryFrames.Remove(projectileId);
    }

    private void ReserveEntityId(int entityId)
    {
        if (entityId >= _nextEntityId)
        {
            _nextEntityId = entityId + 1;
        }
    }
}
