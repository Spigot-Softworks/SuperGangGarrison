using System;
using System.Collections.Generic;

namespace OpenGarrison.Protocol;

public static class SnapshotDelta
{
    private const int SnapshotPlayerSlotLookupSize = 256;

    public static SnapshotMessage ToFullSnapshot(SnapshotMessage snapshot, ISnapshotBaselineState? baseline = null)
    {
        if (!snapshot.IsDelta)
        {
            return Normalize(snapshot);
        }

        if (snapshot.BaselineFrame != 0)
        {
            if (baseline is null)
            {
                throw new InvalidOperationException($"Missing baseline snapshot for frame {snapshot.BaselineFrame}.");
            }

            if (baseline.Frame != snapshot.BaselineFrame)
            {
                throw new InvalidOperationException(
                    $"Baseline frame mismatch. Expected {snapshot.BaselineFrame}, got {baseline.Frame}.");
            }
        }

        return snapshot with
        {
            BaselineFrame = 0,
            IsDelta = false,
            EntityCollectionCompletenessFlags = snapshot.EntityCollectionCompletenessFlags,
            Players = MergePlayers(
                baseline?.Players,
                snapshot.Players,
                snapshot.PlayerMovementStates,
                snapshot.PlayerStatusStates,
                snapshot.PlayerExtendedStatusStates,
                snapshot.PlayerChatBubbleStates,
                snapshot.RemovedPlayerIds),
            Sentries = MergeSentries(baseline?.Sentries, snapshot.Sentries, snapshot.SentryUpdateStates, snapshot.RemovedSentryIds),
            Shots = MergeEntities(baseline?.Shots, snapshot.Shots, snapshot.RemovedShotIds, static state => state.Id),
            Bubbles = MergeEntities(baseline?.Bubbles, snapshot.Bubbles, snapshot.RemovedBubbleIds, static state => state.Id),
            Blades = MergeEntities(baseline?.Blades, snapshot.Blades, snapshot.RemovedBladeIds, static state => state.Id),
            Needles = MergeEntities(baseline?.Needles, snapshot.Needles, snapshot.RemovedNeedleIds, static state => state.Id),
            RevolverShots = MergeEntities(baseline?.RevolverShots, snapshot.RevolverShots, snapshot.RemovedRevolverShotIds, static state => state.Id),
            Rockets = MergeEntities(baseline?.Rockets, snapshot.Rockets, snapshot.RemovedRocketIds, static state => state.Id),
            Flames = MergeEntities(baseline?.Flames, snapshot.Flames, snapshot.RemovedFlameIds, static state => state.Id),
            Flares = MergeEntities(baseline?.Flares, snapshot.Flares, snapshot.RemovedFlareIds, static state => state.Id),
            Mines = MergeEntities(baseline?.Mines, snapshot.Mines, snapshot.RemovedMineIds, static state => state.Id),
            Grenades = MergeEntities(baseline?.Grenades, snapshot.Grenades, snapshot.RemovedGrenadeIds, static state => state.Id),
            DeadBodies = MergeEntities(baseline?.DeadBodies, snapshot.DeadBodies, snapshot.RemovedDeadBodyIds, static state => state.Id),
            SentryGibs = MergeEntities(baseline?.SentryGibs, snapshot.SentryGibs, snapshot.RemovedSentryGibIds, static state => state.Id),
            PlayerGibs = MergeEntities(baseline?.PlayerGibs, snapshot.PlayerGibs, snapshot.RemovedPlayerGibIds, static state => state.Id),
            JumpPads = MergeEntities(baseline?.JumpPads, snapshot.JumpPads, snapshot.RemovedJumpPadIds, static state => state.Id),
            JumpPadGibs = MergeEntities(baseline?.JumpPadGibs, snapshot.JumpPadGibs, snapshot.RemovedJumpPadGibIds, static state => state.Id),
            PlayerMovementStates = Array.Empty<SnapshotPlayerMovementState>(),
            PlayerStatusStates = Array.Empty<SnapshotPlayerStatusState>(),
            PlayerExtendedStatusStates = Array.Empty<SnapshotPlayerExtendedStatusState>(),
            PlayerChatBubbleStates = Array.Empty<SnapshotPlayerChatBubbleState>(),
            RemovedPlayerIds = Array.Empty<int>(),
            RemovedSentryIds = Array.Empty<int>(),
            RemovedShotIds = snapshot.RemovedShotIds,
            RemovedBubbleIds = snapshot.RemovedBubbleIds,
            RemovedBladeIds = snapshot.RemovedBladeIds,
            RemovedNeedleIds = snapshot.RemovedNeedleIds,
            RemovedRevolverShotIds = snapshot.RemovedRevolverShotIds,
            RemovedRocketIds = snapshot.RemovedRocketIds,
            RemovedFlameIds = snapshot.RemovedFlameIds,
            RemovedFlareIds = snapshot.RemovedFlareIds,
            RemovedMineIds = snapshot.RemovedMineIds,
            RemovedGrenadeIds = snapshot.RemovedGrenadeIds,
            RemovedPlayerGibIds = Array.Empty<int>(),
            RemovedDeadBodyIds = Array.Empty<int>(),
            RemovedSentryGibIds = Array.Empty<int>(),
            RemovedJumpPadGibIds = Array.Empty<int>(),
            RemovedJumpPadIds = Array.Empty<int>(),
        };
    }

    private static SnapshotMessage Normalize(SnapshotMessage snapshot)
    {
        return snapshot with
        {
            BaselineFrame = 0,
            IsDelta = false,
            PlayerMovementStates = Array.Empty<SnapshotPlayerMovementState>(),
            PlayerStatusStates = Array.Empty<SnapshotPlayerStatusState>(),
            PlayerExtendedStatusStates = Array.Empty<SnapshotPlayerExtendedStatusState>(),
            PlayerChatBubbleStates = Array.Empty<SnapshotPlayerChatBubbleState>(),
            SentryUpdateStates = Array.Empty<SnapshotSentryUpdateState>(),
            RemovedPlayerIds = Array.Empty<int>(),
            RemovedSentryIds = Array.Empty<int>(),
            RemovedShotIds = Array.Empty<int>(),
            RemovedBubbleIds = Array.Empty<int>(),
            RemovedBladeIds = Array.Empty<int>(),
            RemovedNeedleIds = Array.Empty<int>(),
            RemovedRevolverShotIds = Array.Empty<int>(),
            RemovedRocketIds = Array.Empty<int>(),
            RemovedFlameIds = Array.Empty<int>(),
            RemovedFlareIds = Array.Empty<int>(),
            RemovedMineIds = Array.Empty<int>(),
            RemovedGrenadeIds = Array.Empty<int>(),
            RemovedPlayerGibIds = Array.Empty<int>(),
            RemovedDeadBodyIds = Array.Empty<int>(),
            RemovedSentryGibIds = Array.Empty<int>(),
            RemovedJumpPadGibIds = Array.Empty<int>(),
            RemovedJumpPadIds = Array.Empty<int>(),
        };
    }

    private static SnapshotPlayerState[] MergePlayers(
        IReadOnlyList<SnapshotPlayerState>? baseline,
        IReadOnlyList<SnapshotPlayerState> updates,
        IReadOnlyList<SnapshotPlayerMovementState> movementUpdates,
        IReadOnlyList<SnapshotPlayerStatusState> statusUpdates,
        IReadOnlyList<SnapshotPlayerExtendedStatusState> extendedStatusUpdates,
        IReadOnlyList<SnapshotPlayerChatBubbleState> chatBubbleUpdates,
        IReadOnlyList<int> removedIds)
    {
        var removedSlots = CreatePlayerSlotRemovalLookup(removedIds);
        var mergedBySlot = new SnapshotPlayerState?[SnapshotPlayerSlotLookupSize];

        if (baseline is not null)
        {
            for (var index = 0; index < baseline.Count; index += 1)
            {
                var player = baseline[index];
                if (IsRemovedPlayerSlot(removedSlots, player.Slot))
                {
                    continue;
                }

                mergedBySlot[player.Slot] = player;
            }
        }

        for (var index = 0; index < updates.Count; index += 1)
        {
            var player = updates[index];
            if (IsRemovedPlayerSlot(removedSlots, player.Slot))
            {
                continue;
            }

            mergedBySlot[player.Slot] = player;
        }

        for (var index = 0; index < movementUpdates.Count; index += 1)
        {
            var movement = movementUpdates[index];
            var player = mergedBySlot[movement.Slot];
            if (IsRemovedPlayerSlot(removedSlots, movement.Slot)
                || player is null)
            {
                continue;
            }

            var aimRadians = movement.AimDirectionDegrees * (MathF.PI / 180f);
            const float AimProjectionDistance = 2048f;
            mergedBySlot[movement.Slot] = player with
            {
                X = movement.X,
                Y = movement.Y,
                HorizontalSpeed = movement.HorizontalSpeed,
                VerticalSpeed = movement.VerticalSpeed,
                IsGrounded = movement.IsGrounded,
                RemainingAirJumps = movement.RemainingAirJumps,
                FacingDirectionX = movement.FacingDirectionX,
                AimDirectionDegrees = movement.AimDirectionDegrees,
                AimWorldX = movement.X + MathF.Cos(aimRadians) * AimProjectionDistance,
                AimWorldY = movement.Y + MathF.Sin(aimRadians) * AimProjectionDistance,
                MedicHealTargetId = movement.MedicHealTargetId,
                IsMedicHealing = movement.IsMedicHealing,
                MovementState = movement.MovementState,
                IsTaunting = movement.IsTaunting,
                BurnIntensity = movement.BurnIntensity,
                GameplayEquippedSlot = movement.GameplayEquippedSlot,
                PrimaryCooldownTicks = movement.PrimaryCooldownTicks,
                ReloadTicksUntilNextShell = movement.ReloadTicksUntilNextShell,
                OffhandCooldownTicks = movement.OffhandCooldownTicks,
                OffhandReloadTicks = movement.OffhandReloadTicks,
            };
        }

        for (var index = 0; index < statusUpdates.Count; index += 1)
        {
            var status = statusUpdates[index];
            var player = mergedBySlot[status.Slot];
            if (IsRemovedPlayerSlot(removedSlots, status.Slot)
                || player is null)
            {
                continue;
            }

            mergedBySlot[status.Slot] = player with
            {
                Health = status.Health,
                MaxHealth = status.MaxHealth,
                Ammo = status.Ammo,
                MaxAmmo = status.MaxAmmo,
                Metal = status.Metal,
                IsCarryingIntel = status.IsCarryingIntel,
                IntelRechargeTicks = status.IntelRechargeTicks,
                ReplicatedStates = MergeRuntimeReplicatedStateUpdates(
                    player.ReplicatedStates,
                    status.SecondaryAmmoStates ?? Array.Empty<SnapshotReplicatedStateEntry>()),
            };
        }

        for (var index = 0; index < extendedStatusUpdates.Count; index += 1)
        {
            var status = extendedStatusUpdates[index];
            var player = mergedBySlot[status.Slot];
            if (IsRemovedPlayerSlot(removedSlots, status.Slot)
                || player is null)
            {
                continue;
            }

            mergedBySlot[status.Slot] = player with
            {
                IsSpyCloaked = status.IsSpyCloaked,
                SpyCloakAlpha = status.SpyCloakAlpha,
                IsSpySuperjumping = status.IsSpySuperjumping,
                SpySuperjumpHorizontalVelocity = status.SpySuperjumpHorizontalVelocity,
                SpySuperjumpCooldownTicksRemaining = status.SpySuperjumpCooldownTicksRemaining,
                SpyBackstabVisualTicksRemaining = status.SpyBackstabVisualTicksRemaining,
                IsUbered = status.IsUbered,
                IsKritzCritBoosted = status.IsKritzCritBoosted,
                IsHeavyEating = status.IsHeavyEating,
                HeavyEatTicksRemaining = status.HeavyEatTicksRemaining,
                IsSniperScoped = status.IsSniperScoped,
                MedicNeedleCooldownTicks = status.MedicNeedleCooldownTicks,
                MedicNeedleRefillTicks = status.MedicNeedleRefillTicks,
                PyroAirblastCooldownTicks = status.PyroAirblastCooldownTicks,
                PyroFlareCooldownTicks = status.PyroFlareCooldownTicks,
                PyroPrimaryFuelScaled = status.PyroPrimaryFuelScaled,
                IsPyroPrimaryRefilling = status.IsPyroPrimaryRefilling,
                PyroFlameLoopTicksRemaining = status.PyroFlameLoopTicksRemaining,
                PyroPrimaryRequiresReleaseAfterEmpty = status.PyroPrimaryRequiresReleaseAfterEmpty,
                HeavyEatCooldownTicksRemaining = status.HeavyEatCooldownTicksRemaining,
                MedicUberCharge = status.MedicUberCharge,
                IsMedicUberReady = status.IsMedicUberReady,
            };
        }

        for (var index = 0; index < chatBubbleUpdates.Count; index += 1)
        {
            var chatBubble = chatBubbleUpdates[index];
            var player = mergedBySlot[chatBubble.Slot];
            if (IsRemovedPlayerSlot(removedSlots, chatBubble.Slot)
                || player is null)
            {
                continue;
            }

            mergedBySlot[chatBubble.Slot] = player with
            {
                IsChatBubbleVisible = chatBubble.IsChatBubbleVisible,
                ChatBubbleFrameIndex = chatBubble.ChatBubbleFrameIndex,
                ChatBubbleAlpha = chatBubble.ChatBubbleAlpha,
                IsTypingChatMessage = chatBubble.IsTypingChatMessage,
            };
        }

        var mergedCount = 0;
        for (var slot = 0; slot < mergedBySlot.Length; slot += 1)
        {
            if (mergedBySlot[slot] is not null)
            {
                mergedCount += 1;
            }
        }

        var merged = new SnapshotPlayerState[mergedCount];
        var outputIndex = 0;
        for (var slot = 0; slot < mergedBySlot.Length; slot += 1)
        {
            if (mergedBySlot[slot] is { } player)
            {
                merged[outputIndex++] = player;
            }
        }

        return merged;
    }

    // Merges compact runtime ReplicatedState entries from a status update into the player's existing
    // ReplicatedStates. Runtime weapon and ability states are cleared first so old baselines cannot
    // mask the lightweight update path.
    private static SnapshotReplicatedStateEntry[] MergeRuntimeReplicatedStateUpdates(
        IReadOnlyList<SnapshotReplicatedStateEntry>? baseline,
        IReadOnlyList<SnapshotReplicatedStateEntry> updates)
    {
        var baselineCount = baseline?.Count ?? 0;
        var merged = new List<SnapshotReplicatedStateEntry>(baselineCount + updates.Count);
        for (var i = 0; i < baselineCount; i++)
        {
            var entry = baseline![i];
            if (IsRuntimeReplicatedState(entry))
            {
                continue;
            }

            var isReplaced = false;
            for (var j = 0; j < updates.Count; j++)
            {
                if (string.Equals(entry.OwnerId, updates[j].OwnerId, StringComparison.Ordinal)
                    && string.Equals(entry.Key, updates[j].Key, StringComparison.Ordinal))
                {
                    isReplaced = true;
                    break;
                }
            }
            if (!isReplaced)
            {
                merged.Add(entry);
            }
        }
        for (var j = 0; j < updates.Count; j++)
        {
            merged.Add(updates[j]);
        }
        return merged.ToArray();
    }

    private static bool IsRuntimeReplicatedState(SnapshotReplicatedStateEntry entry)
    {
        return IsSecondaryWeaponRuntimeReplicatedState(entry)
            || IsCoreAbilityRuntimeReplicatedState(entry);
    }

    private static bool IsSecondaryWeaponRuntimeReplicatedState(SnapshotReplicatedStateEntry entry)
    {
        if (!string.Equals(entry.OwnerId, "core.player", StringComparison.Ordinal))
        {
            return false;
        }

        return entry.Key.IndexOf("_ammo", StringComparison.Ordinal) >= 0
            || entry.Key.EndsWith("_reload_ticks", StringComparison.Ordinal)
            || entry.Key.EndsWith("_cooldown_ticks", StringComparison.Ordinal);
    }

    private static bool IsCoreAbilityRuntimeReplicatedState(SnapshotReplicatedStateEntry entry)
    {
        if (!string.Equals(entry.OwnerId, "core.ability", StringComparison.Ordinal))
        {
            return false;
        }

        return entry.Key is "heavy_dash_cooldown_ticks"
            or "heavy_dash_active"
            or "heavy_dash_visible"
            or "heavy_dash_trail_alpha";
    }

    private static IReadOnlyList<SnapshotSentryState> MergeSentries(
        IReadOnlyList<SnapshotSentryState>? baseline,
        IReadOnlyList<SnapshotSentryState> updates,
        IReadOnlyList<SnapshotSentryUpdateState> lightweightUpdates,
        IReadOnlyList<int> removedIds)
    {
        if ((baseline is null || baseline.Count == 0) && updates.Count == 0)
        {
            return Array.Empty<SnapshotSentryState>();
        }

        if ((baseline is null || baseline.Count == 0) && lightweightUpdates.Count == 0)
        {
            if (removedIds.Count == 0)
            {
                return updates;
            }

            return FilterEntitiesByRemoval(updates, removedIds, static state => state.Id);
        }

        if (updates.Count == 0 && lightweightUpdates.Count == 0)
        {
            if (baseline is null || baseline.Count == 0)
            {
                return Array.Empty<SnapshotSentryState>();
            }

            if (removedIds.Count == 0)
            {
                return baseline;
            }

            return FilterEntitiesByRemoval(baseline, removedIds, static state => state.Id);
        }

        var removed = CreateRemovedEntityLookup(removedIds);
        var mergedById = new Dictionary<int, SnapshotSentryState>((baseline?.Count ?? 0) + updates.Count);

        if (baseline is not null)
        {
            for (var index = 0; index < baseline.Count; index += 1)
            {
                var sentry = baseline[index];
                if (IsRemovedEntity(removedIds, removed, sentry.Id))
                {
                    continue;
                }

                mergedById[sentry.Id] = sentry;
            }
        }

        // Apply full state updates
        for (var index = 0; index < updates.Count; index += 1)
        {
            var sentry = updates[index];
            if (IsRemovedEntity(removedIds, removed, sentry.Id))
            {
                continue;
            }

            mergedById[sentry.Id] = sentry;
        }

        // Apply lightweight updates to dynamic fields only
        for (var index = 0; index < lightweightUpdates.Count; index += 1)
        {
            var update = lightweightUpdates[index];
            if (IsRemovedEntity(removedIds, removed, update.Id)
                || !mergedById.TryGetValue(update.Id, out var sentry))
            {
                continue;
            }

            mergedById[update.Id] = sentry with
            {
                X = update.X,
                Y = update.Y,
                Health = update.Health,
                FacingDirectionX = update.FacingDirectionX,
                AimDirectionDegrees = update.AimDirectionDegrees,
                ShotTraceTicksRemaining = update.ShotTraceTicksRemaining,
                HasActiveTarget = update.HasActiveTarget,
                LastShotTargetX = update.LastShotTargetX,
                LastShotTargetY = update.LastShotTargetY,
            };
        }

        var merged = new SnapshotSentryState[mergedById.Count];
        var mergedIndex = 0;
        foreach (var entry in mergedById)
        {
            merged[mergedIndex++] = entry.Value;
        }

        Array.Sort(merged, static (left, right) => left.Id.CompareTo(right.Id));
        return merged;
    }

    private static IReadOnlyList<T> MergeEntities<T>(
        IReadOnlyList<T>? baseline,
        IReadOnlyList<T> updates,
        IReadOnlyList<int> removedIds,
        Func<T, int> keySelector)
    {
        if (baseline is null || baseline.Count == 0)
        {
            if (updates.Count == 0)
            {
                return Array.Empty<T>();
            }

            if (removedIds.Count == 0)
            {
                return updates;
            }

            return FilterEntitiesByRemoval(updates, removedIds, keySelector);
        }

        if (updates.Count == 0)
        {
            if (removedIds.Count == 0)
            {
                return baseline;
            }

            return FilterEntitiesByRemoval(baseline, removedIds, keySelector);
        }

        var removed = CreateRemovedEntityLookup(removedIds);
        var updatesById = new Dictionary<int, T>(updates.Count);
        for (var index = 0; index < updates.Count; index += 1)
        {
            var update = updates[index];
            updatesById[keySelector(update)] = update;
        }

        var capacity = (baseline?.Count ?? 0) + updates.Count;
        var merged = new List<T>(capacity);
        if (baseline is not null)
        {
            for (var index = 0; index < baseline.Count; index += 1)
            {
                var state = baseline[index];
                var id = keySelector(state);
                if (IsRemovedEntity(removedIds, removed, id))
                {
                    continue;
                }

                if (updatesById.Remove(id, out var updated))
                {
                    merged.Add(updated);
                    continue;
                }

                merged.Add(state);
            }
        }

        for (var index = 0; index < updates.Count; index += 1)
        {
            var update = updates[index];
            var id = keySelector(update);
            if (IsRemovedEntity(removedIds, removed, id))
            {
                continue;
            }

            if (updatesById.Remove(id, out var appended))
            {
                merged.Add(appended);
            }
        }

        return merged;
    }

    private static bool[]? CreatePlayerSlotRemovalLookup(IReadOnlyList<int> removedIds)
    {
        if (removedIds.Count == 0)
        {
            return null;
        }

        var removedSlots = new bool[SnapshotPlayerSlotLookupSize];
        for (var index = 0; index < removedIds.Count; index += 1)
        {
            var removedId = removedIds[index];
            if ((uint)removedId < SnapshotPlayerSlotLookupSize)
            {
                removedSlots[removedId] = true;
            }
        }

        return removedSlots;
    }

    private static bool IsRemovedPlayerSlot(bool[]? removedSlots, int slot)
    {
        return removedSlots is not null
            && (uint)slot < removedSlots.Length
            && removedSlots[slot];
    }

    private static HashSet<int>? CreateRemovedEntityLookup(IReadOnlyList<int> removedIds)
    {
        return removedIds.Count > 4 ? new HashSet<int>(removedIds) : null;
    }

    private static bool IsRemovedEntity(IReadOnlyList<int> removedIds, HashSet<int>? removedLookup, int id)
    {
        if (removedLookup is not null)
        {
            return removedLookup.Contains(id);
        }

        for (var index = 0; index < removedIds.Count; index += 1)
        {
            if (removedIds[index] == id)
            {
                return true;
            }
        }

        return false;
    }

    private static T[] FilterEntitiesByRemoval<T>(
        IReadOnlyList<T> states,
        IReadOnlyList<int> removedIds,
        Func<T, int> keySelector)
    {
        var removedLookup = CreateRemovedEntityLookup(removedIds);
        var keptCount = 0;
        for (var index = 0; index < states.Count; index += 1)
        {
            if (!IsRemovedEntity(removedIds, removedLookup, keySelector(states[index])))
            {
                keptCount += 1;
            }
        }

        if (keptCount == 0)
        {
            return Array.Empty<T>();
        }

        var filtered = new T[keptCount];
        var outputIndex = 0;
        for (var index = 0; index < states.Count; index += 1)
        {
            var state = states[index];
            if (IsRemovedEntity(removedIds, removedLookup, keySelector(state)))
            {
                continue;
            }

            filtered[outputIndex++] = state;
        }

        return filtered;
    }
}
