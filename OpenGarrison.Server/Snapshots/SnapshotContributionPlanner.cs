using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotContributionPlanner
{
    public static List<SnapshotDeltaBudgeter.Contribution> BuildContributions(
        ClientSession client,
        SnapshotMessage fullSnapshot,
        SnapshotMessage? baseline,
        SimulationWorld world)
    {
        var focus = GetClientFocusPoint(client, world);
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>();

        AddEntityDelta(
            contributions,
            fullSnapshot.Sentries,
            baseline?.Sentries,
            priority: 1200,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Sentries.Add(state),
            static (builder, id) => builder.RemovedSentryIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Rockets,
            baseline?.Rockets,
            priority: 1120,
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
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.SoundEvents.Add(state));
        AddPointEventContributions(
            contributions,
            fullSnapshot.VisualEvents,
            priority: 1290,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.VisualEvents.Add(state));
        AddPointEventContributions(
            contributions,
            fullSnapshot.DamageEvents,
            priority: 1285,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.DamageEvents.Add(state));
        AddOrderedContributions(
            contributions,
            fullSnapshot.KillFeed,
            priority: 1180,
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

    private static void AddEntityDelta<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        int priority,
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
                builder => addRemovedId(builder, removedId)));
        }

        for (var index = 0; index < delta.UpdatedStates.Count; index += 1)
        {
            var state = delta.UpdatedStates[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority,
                DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state)),
                builder => addState(builder, state)));
        }
    }

    private static void AddPointEventContributions<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
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
                builder => addState(builder, state)));
        }
    }

    private static void AddOrderedContributions<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
        Action<SnapshotDeltaBudgeter.Builder, T> addState)
    {
        for (var index = states.Count - 1; index >= 0; index -= 1)
        {
            var state = states[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority - ((states.Count - 1) - index),
                0f,
                builder => addState(builder, state)));
        }
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
