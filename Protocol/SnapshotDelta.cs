using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Protocol;

public static class SnapshotDelta
{
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
            Players = MergePlayers(baseline?.Players, snapshot.Players, snapshot.PlayerMovementStates, snapshot.RemovedPlayerIds),
            Sentries = MergeEntities(baseline?.Sentries, snapshot.Sentries, snapshot.RemovedSentryIds, static state => state.Id),
            Shots = MergeEntities(baseline?.Shots, snapshot.Shots, snapshot.RemovedShotIds, static state => state.Id),
            Bubbles = MergeEntities(baseline?.Bubbles, snapshot.Bubbles, snapshot.RemovedBubbleIds, static state => state.Id),
            Blades = MergeEntities(baseline?.Blades, snapshot.Blades, snapshot.RemovedBladeIds, static state => state.Id),
            Needles = MergeEntities(baseline?.Needles, snapshot.Needles, snapshot.RemovedNeedleIds, static state => state.Id),
            RevolverShots = MergeEntities(baseline?.RevolverShots, snapshot.RevolverShots, snapshot.RemovedRevolverShotIds, static state => state.Id),
            Rockets = MergeEntities(baseline?.Rockets, snapshot.Rockets, snapshot.RemovedRocketIds, static state => state.Id),
            Flames = MergeEntities(baseline?.Flames, snapshot.Flames, snapshot.RemovedFlameIds, static state => state.Id),
            Flares = MergeEntities(baseline?.Flares, snapshot.Flares, snapshot.RemovedFlareIds, static state => state.Id),
            Mines = MergeEntities(baseline?.Mines, snapshot.Mines, snapshot.RemovedMineIds, static state => state.Id),
            PlayerGibs = MergeEntities(baseline?.PlayerGibs, snapshot.PlayerGibs, snapshot.RemovedPlayerGibIds, static state => state.Id),
            DeadBodies = MergeEntities(baseline?.DeadBodies, snapshot.DeadBodies, snapshot.RemovedDeadBodyIds, static state => state.Id),
            SentryGibs = MergeEntities(baseline?.SentryGibs, snapshot.SentryGibs, snapshot.RemovedSentryGibIds, static state => state.Id),
            JumpPads = MergeEntities(baseline?.JumpPads, snapshot.JumpPads, snapshot.RemovedJumpPadIds, static state => state.Id),
            PlayerMovementStates = Array.Empty<SnapshotPlayerMovementState>(),
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
            RemovedPlayerGibIds = Array.Empty<int>(),
            RemovedDeadBodyIds = Array.Empty<int>(),
            RemovedSentryGibIds = Array.Empty<int>(),
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
            RemovedPlayerGibIds = Array.Empty<int>(),
            RemovedDeadBodyIds = Array.Empty<int>(),
            RemovedSentryGibIds = Array.Empty<int>(),
            RemovedJumpPadIds = Array.Empty<int>(),
        };
    }

    private static List<SnapshotPlayerState> MergePlayers(
        IReadOnlyList<SnapshotPlayerState>? baseline,
        IReadOnlyList<SnapshotPlayerState> updates,
        IReadOnlyList<SnapshotPlayerMovementState> movementUpdates,
        IReadOnlyList<int> removedIds)
    {
        var removed = removedIds.Count == 0 ? null : new HashSet<int>(removedIds);
        var mergedBySlot = new Dictionary<int, SnapshotPlayerState>((baseline?.Count ?? 0) + updates.Count);

        if (baseline is not null)
        {
            for (var index = 0; index < baseline.Count; index += 1)
            {
                var player = baseline[index];
                if (removed?.Contains(player.Slot) == true)
                {
                    continue;
                }

                mergedBySlot[player.Slot] = player;
            }
        }

        for (var index = 0; index < updates.Count; index += 1)
        {
            var player = updates[index];
            if (removed?.Contains(player.Slot) == true)
            {
                continue;
            }

            mergedBySlot[player.Slot] = player;
        }

        for (var index = 0; index < movementUpdates.Count; index += 1)
        {
            var movement = movementUpdates[index];
            if (removed?.Contains(movement.Slot) == true
                || !mergedBySlot.TryGetValue(movement.Slot, out var player))
            {
                continue;
            }

            // Only update TauntFrameIndex if taunt is starting (wasn't taunting before)
            // Otherwise let client simulation advance the frame locally
            var isTauntStarting = !player.IsTaunting && movement.IsTaunting;
            
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
                MovementState = movement.MovementState,
                IsTaunting = movement.IsTaunting,
                TauntFrameIndex = isTauntStarting ? movement.TauntFrameIndex : player.TauntFrameIndex,
                BurnIntensity = movement.BurnIntensity,
                GameplayEquippedSlot = movement.GameplayEquippedSlot,
                PrimaryCooldownTicks = movement.PrimaryCooldownTicks,
                ReloadTicksUntilNextShell = movement.ReloadTicksUntilNextShell,
                OffhandCooldownTicks = movement.OffhandCooldownTicks,
                OffhandReloadTicks = movement.OffhandReloadTicks,
            };
        }

        return mergedBySlot
            .OrderBy(static entry => entry.Key)
            .Select(static entry => entry.Value)
            .ToList();
    }

    private static List<T> MergeEntities<T>(
        IReadOnlyList<T>? baseline,
        IReadOnlyList<T> updates,
        IReadOnlyList<int> removedIds,
        Func<T, int> keySelector)
    {
        var removed = removedIds.Count == 0 ? null : new HashSet<int>(removedIds);
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
                if (removed?.Contains(id) == true)
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
            if (removed?.Contains(id) == true)
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
}
