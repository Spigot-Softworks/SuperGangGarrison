using System;
using System.Collections.Generic;
using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotContributionPlanner
{
    private const int PlayerUpdatePriority = 1500;
    private const int PlayerDetailUpdatePriority = 900;
    private const int AddedPlayerUpdatePriorityBonus = 200;
    private const int LocalPlayerUpdatePriorityBonus = 300;
    private const int RemovedPlayerEstimatedBytes = 6;
    private const int SnapshotPlayerFixedBytes = 220;
    private const int SnapshotPlayerMovementBytes = 32;

    public static List<SnapshotDeltaBudgeter.Contribution> BuildContributions(
        ClientSession client,
        SnapshotMessage fullSnapshot,
        ISnapshotBaselineState? baseline,
        SimulationWorld world)
    {
        var focus = GetClientFocusPoint(client, world);
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>();

        AddPlayerDelta(
            contributions,
            fullSnapshot.Players,
            baseline?.Players ?? Array.Empty<SnapshotPlayerState>(),
            PlayerUpdatePriority,
            client.Slot,
            focus);

        AddEntityDelta(
            contributions,
            fullSnapshot.Sentries,
            baseline?.Sentries,
            priority: 1200,
            estimateUpdatedBytes: static state => 60,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Sentries.Add(state),
            static (builder, id) => builder.RemovedSentryIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.JumpPads,
            baseline?.JumpPads,
            priority: 1195,
            estimateUpdatedBytes: static state => 22,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.JumpPads.Add(state),
            static (builder, id) => builder.RemovedJumpPadIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Rockets,
            baseline?.Rockets,
            priority: 1120,
            estimateUpdatedBytes: EstimateRocketBytes,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Rockets.Add(state),
            static (builder, id) => builder.RemovedRocketIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Flames,
            baseline?.Flames,
            priority: 1110,
            estimateUpdatedBytes: static state => 57,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Flames.Add(state),
            static (builder, id) => builder.RemovedFlameIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Flares,
            baseline?.Flares,
            priority: 1105,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Flares.Add(state),
            static (builder, id) => builder.RemovedFlareIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Mines,
            baseline?.Mines,
            priority: 1100,
            estimateUpdatedBytes: static state => 31,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Mines.Add(state),
            static (builder, id) => builder.RemovedMineIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Shots,
            baseline?.Shots,
            priority: 1080,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Shots.Add(state),
            static (builder, id) => builder.RemovedShotIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Needles,
            baseline?.Needles,
            priority: 1070,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Needles.Add(state),
            static (builder, id) => builder.RemovedNeedleIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.RevolverShots,
            baseline?.RevolverShots,
            priority: 1060,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.RevolverShots.Add(state),
            static (builder, id) => builder.RemovedRevolverShotIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Bubbles,
            baseline?.Bubbles,
            priority: 1050,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Bubbles.Add(state),
            static (builder, id) => builder.RemovedBubbleIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Blades,
            baseline?.Blades,
            priority: 1040,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Blades.Add(state),
            static (builder, id) => builder.RemovedBladeIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.DeadBodies,
            baseline?.DeadBodies,
            priority: 440,
            estimateUpdatedBytes: static state => 40,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.DeadBodies.Add(state),
            static (builder, id) => builder.RemovedDeadBodyIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.SentryGibs,
            baseline?.SentryGibs,
            priority: 360,
            estimateUpdatedBytes: static state => 17,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.SentryGibs.Add(state),
            static (builder, id) => builder.RemovedSentryGibIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.PlayerGibs,
            baseline?.PlayerGibs,
            priority: 320,
            estimateUpdatedBytes: EstimatePlayerGibBytes,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.PlayerGibs.Add(state),
            static (builder, id) => builder.RemovedPlayerGibIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.BloodDrops,
            baseline?.BloodDrops,
            priority: 240,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.BloodDrops.Add(state),
            static (builder, id) => builder.RemovedBloodDropIds.Add(id));
        AddPointEventContributions(
            contributions,
            fullSnapshot.SoundEvents,
            priority: 1300,
            estimateBytes: EstimateSoundEventBytes,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.SoundEvents.Add(state));
        AddPointEventContributions(
            contributions,
            fullSnapshot.VisualEvents,
            priority: 1290,
            estimateBytes: EstimateVisualEventBytes,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.VisualEvents.Add(state));
        AddPointEventContributions(
            contributions,
            fullSnapshot.DamageEvents,
            priority: 1285,
            estimateBytes: static state => 42,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.DamageEvents.Add(state));
        AddOrderedContributions(
            contributions,
            fullSnapshot.KillFeed,
            priority: 1180,
            estimateBytes: EstimateKillFeedBytes,
            static (builder, entry) => builder.KillFeed.Add(entry));

        return contributions;
    }

    private static (float X, float Y) GetClientFocusPoint(ClientSession client, SimulationWorld world)
    {
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
            && world.TryGetNetworkPlayer(client.Slot, out var player)
            && player.IsAlive)
        {
            return (player.X, player.Y);
        }

        var deathCam = world.GetNetworkPlayerDeathCam(client.Slot);
        if (deathCam is not null)
        {
            return (deathCam.FocusX, deathCam.FocusY);
        }

        return (world.Bounds.Width / 2f, world.Bounds.Height / 2f);
    }

    private static void AddPlayerDelta(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<SnapshotPlayerState> currentPlayers,
        IReadOnlyList<SnapshotPlayerState> baselinePlayers,
        int priority,
        byte viewerSlot,
        (float X, float Y) focus)
    {
        var currentBySlot = new Dictionary<int, SnapshotPlayerState>(currentPlayers.Count);
        for (var index = 0; index < currentPlayers.Count; index += 1)
        {
            var player = currentPlayers[index];
            currentBySlot[player.Slot] = player;
        }

        var baselineBySlot = new Dictionary<int, SnapshotPlayerState>(baselinePlayers.Count);
        for (var index = 0; index < baselinePlayers.Count; index += 1)
        {
            var player = baselinePlayers[index];
            baselineBySlot[player.Slot] = player;
            if (currentBySlot.ContainsKey(player.Slot))
            {
                continue;
            }

            var removedSlot = player.Slot;
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority + AddedPlayerUpdatePriorityBonus,
                0f,
                RemovedPlayerEstimatedBytes,
                builder => builder.RemovedPlayerIds.Add(removedSlot),
                SnapshotDeltaBudgeter.ContributionKind.PlayerRemoval));
        }

        for (var index = 0; index < currentPlayers.Count; index += 1)
        {
            var player = currentPlayers[index];
            var isAddedPlayer = !baselineBySlot.TryGetValue(player.Slot, out var baselinePlayer);
            if (!isAddedPlayer && EqualityComparer<SnapshotPlayerState>.Default.Equals(player, baselinePlayer))
            {
                continue;
            }

            var playerPriority = player.Slot == viewerSlot
                ? priority + LocalPlayerUpdatePriorityBonus
                : priority;
            if (isAddedPlayer)
            {
                playerPriority += AddedPlayerUpdatePriorityBonus;

                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    playerPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    EstimatePlayerBytes(player),
                    builder => builder.Players.Add(player),
                    SnapshotDeltaBudgeter.ContributionKind.PlayerFirstAppearance));
                continue;
            }

            if (IsPlayerRosterCriticalChange(player, baselinePlayer!))
            {
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    playerPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    EstimatePlayerBytes(player),
                    builder => builder.Players.Add(player),
                    SnapshotDeltaBudgeter.ContributionKind.PlayerRosterUpdate));
                continue;
            }

            if (HasPlayerMovementChanged(player, baselinePlayer!))
            {
                var movementState = ToPlayerMovementState(player);
                var movementKind = player.Slot == viewerSlot
                    ? SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate
                    : SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate;
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    playerPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    SnapshotPlayerMovementBytes,
                    builder => builder.PlayerMovementStates.Add(movementState),
                    movementKind));
            }

            if (HasPlayerNonMovementDetailChanged(player, baselinePlayer!))
            {
                var detailPriority = player.Slot == viewerSlot
                    ? PlayerDetailUpdatePriority + 50
                    : PlayerDetailUpdatePriority;
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    detailPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    EstimatePlayerBytes(player),
                    builder => builder.Players.Add(player)));
            }
        }
    }

    private static SnapshotPlayerMovementState ToPlayerMovementState(SnapshotPlayerState player)
    {
        return new SnapshotPlayerMovementState(
            player.Slot,
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            player.RemainingAirJumps,
            player.FacingDirectionX,
            player.AimDirectionDegrees,
            player.MovementState);
    }

    private static bool HasPlayerMovementChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.X != baselinePlayer.X
            || player.Y != baselinePlayer.Y
            || player.HorizontalSpeed != baselinePlayer.HorizontalSpeed
            || player.VerticalSpeed != baselinePlayer.VerticalSpeed
            || player.IsGrounded != baselinePlayer.IsGrounded
            || player.RemainingAirJumps != baselinePlayer.RemainingAirJumps
            || player.FacingDirectionX != baselinePlayer.FacingDirectionX
            || player.AimDirectionDegrees != baselinePlayer.AimDirectionDegrees
            || player.MovementState != baselinePlayer.MovementState;
    }

    private static bool HasPlayerNonMovementDetailChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        var playerWithBaselineMovement = player with
        {
            X = baselinePlayer.X,
            Y = baselinePlayer.Y,
            HorizontalSpeed = baselinePlayer.HorizontalSpeed,
            VerticalSpeed = baselinePlayer.VerticalSpeed,
            IsGrounded = baselinePlayer.IsGrounded,
            RemainingAirJumps = baselinePlayer.RemainingAirJumps,
            FacingDirectionX = baselinePlayer.FacingDirectionX,
            AimDirectionDegrees = baselinePlayer.AimDirectionDegrees,
            MovementState = baselinePlayer.MovementState,
        };

        return !EqualityComparer<SnapshotPlayerState>.Default.Equals(playerWithBaselineMovement, baselinePlayer);
    }

    private static bool IsPlayerRosterCriticalChange(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.PlayerId != baselinePlayer.PlayerId
            || !string.Equals(player.Name, baselinePlayer.Name, StringComparison.Ordinal)
            || player.Team != baselinePlayer.Team
            || player.ClassId != baselinePlayer.ClassId
            || player.IsAlive != baselinePlayer.IsAlive
            || player.IsAwaitingJoin != baselinePlayer.IsAwaitingJoin
            || player.IsSpectator != baselinePlayer.IsSpectator
            || player.PlayerScale != baselinePlayer.PlayerScale;
    }

    private static void AddEntityDelta<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        int priority,
        Func<T, int> estimateUpdatedBytes,
        int estimatedRemovedBytes,
        (float X, float Y) focus,
        Func<T, int> idSelector,
        Func<T, float> xSelector,
        Func<T, float> ySelector,
        Action<SnapshotDeltaBudgeter.Builder, T> addState,
        Action<SnapshotDeltaBudgeter.Builder, int> addRemovedId)
    {
        var delta = DiffEntities(currentStates, baselineStates, idSelector);
        for (var index = 0; index < delta.RemovedIds.Count; index += 1)
        {
            var removedId = delta.RemovedIds[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority + 100,
                DistanceSquared(focus.X, focus.Y, focus.X, focus.Y),
                estimatedRemovedBytes,
                builder => addRemovedId(builder, removedId)));
        }

        for (var index = 0; index < delta.UpdatedStates.Count; index += 1)
        {
            var state = delta.UpdatedStates[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority,
                DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state)),
                estimateUpdatedBytes(state),
                builder => addState(builder, state)));
        }
    }

    private static void AddPointEventContributions<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
        Func<T, int> estimateBytes,
        (float X, float Y) focus,
        Func<T, float> xSelector,
        Func<T, float> ySelector,
        Action<SnapshotDeltaBudgeter.Builder, T> addState)
    {
        for (var index = states.Count - 1; index >= 0; index -= 1)
        {
            var state = states[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority - ((states.Count - 1) - index),
                DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state)),
                estimateBytes(state),
                builder => addState(builder, state)));
        }
    }

    private static void AddOrderedContributions<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
        Func<T, int> estimateBytes,
        Action<SnapshotDeltaBudgeter.Builder, T> addState)
    {
        for (var index = states.Count - 1; index >= 0; index -= 1)
        {
            var state = states[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority - ((states.Count - 1) - index),
                0f,
                estimateBytes(state),
                builder => addState(builder, state)));
        }
    }

    private static int EstimateSoundEventBytes(SnapshotSoundEvent state)
    {
        return 26 + state.SoundName.Length;
    }

    private static int EstimateVisualEventBytes(SnapshotVisualEvent state)
    {
        return 26 + state.EffectName.Length;
    }

    private static int EstimateKillFeedBytes(SnapshotKillFeedEntry entry)
    {
        return 35
            + entry.KillerName.Length
            + entry.WeaponSpriteName.Length
            + entry.VictimName.Length
            + entry.MessageText.Length;
    }

    private static int EstimatePlayerBytes(SnapshotPlayerState player)
    {
        return SnapshotPlayerFixedBytes
            + EstimateStringBytes(player.Name)
            + EstimateStringBytes(player.GameplayModPackId)
            + EstimateStringBytes(player.GameplayLoadoutId)
            + EstimateStringBytes(player.GameplayPrimaryItemId)
            + EstimateStringBytes(player.GameplaySecondaryItemId)
            + EstimateStringBytes(player.GameplayUtilityItemId)
            + EstimateStringBytes(player.GameplayEquippedItemId)
            + EstimateStringBytes(player.GameplayAcquiredItemId)
            + EstimateGameplayIdListBytes(player.OwnedGameplayItemIds)
            + EstimateReplicatedStateEntryBytes(player.ReplicatedStates);
    }

    private static int EstimateGameplayIdListBytes(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return 1;
        }

        var bytes = 1;
        for (var index = 0; index < ids.Count; index += 1)
        {
            bytes += EstimateStringBytes(ids[index]);
        }

        return bytes;
    }

    private static int EstimateReplicatedStateEntryBytes(IReadOnlyList<SnapshotReplicatedStateEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return 1;
        }

        var bytes = 1;
        for (var index = 0; index < entries.Count; index += 1)
        {
            var entry = entries[index];
            bytes += EstimateStringBytes(entry.OwnerId)
                + EstimateStringBytes(entry.Key)
                + 10;
        }

        return bytes;
    }

    private static int EstimateStringBytes(string value)
    {
        return 2 + Encoding.UTF8.GetByteCount(value);
    }

    private static int EstimatePlayerGibBytes(SnapshotPlayerGibState state)
    {
        return 42 + state.SpriteName.Length;
    }

    private static int EstimateRocketBytes(SnapshotRocketState state)
    {
        var passedFriendlyPlayerCount = state.PassedFriendlyPlayerIds?.Count ?? 0;
        return 68 + (passedFriendlyPlayerCount * 4);
    }

    private static EntityDelta<T> DiffEntities<T>(
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        Func<T, int> idSelector)
    {
        var updatedStates = new List<T>(currentStates.Count);
        if (baselineStates is null || baselineStates.Count == 0)
        {
            updatedStates.AddRange(currentStates);
            return new EntityDelta<T>(updatedStates, []);
        }

        var currentById = new Dictionary<int, T>(currentStates.Count);
        for (var index = 0; index < currentStates.Count; index += 1)
        {
            var state = currentStates[index];
            currentById[idSelector(state)] = state;
        }

        var baselineById = new Dictionary<int, T>(baselineStates.Count);
        for (var index = 0; index < baselineStates.Count; index += 1)
        {
            var state = baselineStates[index];
            baselineById[idSelector(state)] = state;
        }

        var removedIds = new List<int>();
        foreach (var baselineState in baselineStates)
        {
            var id = idSelector(baselineState);
            if (!currentById.ContainsKey(id))
            {
                removedIds.Add(id);
            }
        }

        for (var index = 0; index < currentStates.Count; index += 1)
        {
            var state = currentStates[index];
            var id = idSelector(state);
            if (!baselineById.TryGetValue(id, out var baselineState)
                || !EqualityComparer<T>.Default.Equals(state, baselineState))
            {
                updatedStates.Add(state);
            }
        }

        return new EntityDelta<T>(updatedStates, removedIds);
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private sealed record EntityDelta<T>(List<T> UpdatedStates, List<int> RemovedIds);
}
