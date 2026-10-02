using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientSnapshotBaselinePinTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void ClientResolvesMoreThanRollingHistoryOfDeltasAgainstPinnedBaselineThenSwitches()
    {
        var game = CreateHistoryHarness();
        var baseline = CreateSnapshot(1);
        RememberSnapshotState(game, baseline, referencedBaselineFrame: 0);

        for (ulong frame = 2; frame <= 120; frame += 1)
        {
            var rawDelta = baseline with
            {
                Frame = frame,
                IsDelta = true,
                BaselineFrame = 1,
                Players = [],
            };
            var pinnedBaseline = GetSnapshotState(game, 1);
            var resolved = SnapshotDelta.ToFullSnapshot(rawDelta, pinnedBaseline);

            Assert.Equal(frame, resolved.Frame);
            Assert.Single(resolved.Players);
            RememberSnapshotState(game, resolved, referencedBaselineFrame: 1);
        }

        Assert.Equal(Game1.SnapshotStateHistoryLimit, GetHistory(game).Count);
        Assert.Equal((ulong)1, GetSnapshotState(game, 1).Frame);

        var switchedRawDelta = baseline with
        {
            Frame = 121,
            IsDelta = true,
            BaselineFrame = 120,
            Players = [],
        };
        var switchedResolved = SnapshotDelta.ToFullSnapshot(switchedRawDelta, GetSnapshotState(game, 120));
        RememberSnapshotState(game, switchedResolved, referencedBaselineFrame: 120);

        Assert.Equal((ulong)120, GetPinnedFrame(game));
        Assert.Equal((ulong)120, GetSnapshotState(game, 120).Frame);
        Assert.False(TryGetSnapshotState(game, 1, out _));
    }

    [Fact]
    public void RememberingResolvedBaselineRetainsTheSuppliedWrapperIdentity()
    {
        var game = CreateHistoryHarness();
        var snapshot = CreateSnapshot(1);
        var baseline = SnapshotBaselineState.FromSnapshot(snapshot);

        RememberSnapshotState(game, baseline, referencedBaselineFrame: 0);

        Assert.Same(baseline, GetSnapshotState(game, snapshot.Frame));
        Assert.Same(baseline, typeof(Game1)
            .GetField("_pinnedServerSnapshotBaseline", InstanceMembers)!
            .GetValue(game));
        Assert.Same(snapshot.Players, baseline.Players);
    }

    [Fact]
    public void StringMappingsSurviveGameplayResetAndStaleMapSnapshotButClearOnReconnect()
    {
        var (game, world, networkClient) = CreateGameplayHarness();
        world.ClientSnapshots.StringCache.ApplyCacheUpdates(new Dictionary<ushort, string> { [7] = "stock" });
        SetField(game, "_snapshotStringCacheConnectionGeneration", networkClient.ConnectionGeneration);
        SetField(game, "_hasSnapshotStringCacheConnectionGeneration", true);

        game.ResetSnapshotStateHistory();
        Assert.Equal("stock", world.ClientSnapshots.StringCache.Resolve(7, "fallback"));

        var staleSnapshot = CreateSnapshot(1) with { LevelName = "old_map" };
        var resolvedEntryType = typeof(Game1).GetNestedType("ResolvedSnapshotEntry", BindingFlags.NonPublic)!;
        var batchSnapshots = new Dictionary<ulong, SnapshotBaselineState>();
        var resolvedBatch = Activator.CreateInstance(typeof(List<>).MakeGenericType(resolvedEntryType));
        object?[] handleArguments = [staleSnapshot, 2UL, null, batchSnapshots, resolvedBatch];
        var accepted = (bool)typeof(Game1)
            .GetMethod("TryHandleSnapshotMessage", InstanceMembers)!
            .Invoke(game, handleArguments)!;
        Assert.False(accepted);
        Assert.Equal("stock", world.ClientSnapshots.StringCache.Resolve(7, "fallback"));

        networkClient.Disconnect();
        typeof(Game1).GetMethod("EnsureSnapshotStringCacheConnectionGeneration", InstanceMembers)!
            .Invoke(game, null);
        Assert.Equal("fallback", world.ClientSnapshots.StringCache.Resolve(7, "fallback"));
    }

    private static Game1 CreateHistoryHarness()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        SetField(game, "_snapshotStatesByFrame", new Dictionary<ulong, SnapshotBaselineState>());
        SetField(game, "_snapshotStateFrameOrder", new Queue<ulong>());
        SetField(game, "_pinnedServerSnapshotBaseline", null);
        SetField(game, "_pinnedServerSnapshotBaselineFrame", 0UL);
        return game;
    }

    private static (Game1 Game, SimulationWorld World, NetworkGameClient NetworkClient) CreateGameplayHarness()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld();
        var networkClient = new NetworkGameClient();
        var services = new ClientServiceContainer();
        SetField(game, "_world", world);
        SetField(game, "_config", world.Config);
        SetField(game, "_networkClient", networkClient);
        SetField(game, "_services", services);
        SetField(game, "_snapshotStatesByFrame", new Dictionary<ulong, SnapshotBaselineState>());
        SetField(game, "_snapshotStateFrameOrder", new Queue<ulong>());
        var queuedSnapshotType = typeof(Game1).GetNestedType("QueuedAuthoritativeSnapshot", BindingFlags.NonPublic)!;
        SetField(game, "_queuedAuthoritativeSnapshots", Activator.CreateInstance(typeof(Queue<>).MakeGenericType(queuedSnapshotType)));
        var gameplayManager = new GameplayManager((IGameplayContext)game);
        services.Register(gameplayManager);
        return (game, world, networkClient);
    }

    private static SnapshotMessage CreateSnapshot(ulong frame)
    {
        var builder = typeof(ClientSessionSnapshotHistoryTests).GetMethod(
            "CreateSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (SnapshotMessage)builder.Invoke(null, [frame])!;
    }

    private static void RememberSnapshotState(Game1 game, SnapshotMessage snapshot, ulong referencedBaselineFrame)
        => typeof(Game1)
            .GetMethod(
                "RememberSnapshotState",
                InstanceMembers,
                binder: null,
                types: [typeof(SnapshotMessage), typeof(ulong)],
                modifiers: null)!
            .Invoke(game, [snapshot, referencedBaselineFrame]);

    private static void RememberSnapshotState(Game1 game, SnapshotBaselineState snapshot, ulong referencedBaselineFrame)
        => typeof(Game1)
            .GetMethod(
                "RememberSnapshotState",
                InstanceMembers,
                binder: null,
                types: [typeof(SnapshotBaselineState), typeof(ulong)],
                modifiers: null)!
            .Invoke(game, [snapshot, referencedBaselineFrame]);

    private static SnapshotBaselineState GetSnapshotState(Game1 game, ulong frame)
    {
        Assert.True(TryGetSnapshotState(game, frame, out var snapshot));
        return snapshot;
    }

    private static bool TryGetSnapshotState(Game1 game, ulong frame, out SnapshotBaselineState snapshot)
    {
        object?[] arguments = [frame, null];
        var found = (bool)typeof(Game1)
            .GetMethod("TryGetSnapshotState", InstanceMembers)!
            .Invoke(game, arguments)!;
        snapshot = (SnapshotBaselineState?)arguments[1]!;
        return found;
    }

    private static Dictionary<ulong, SnapshotBaselineState> GetHistory(Game1 game)
        => (Dictionary<ulong, SnapshotBaselineState>)typeof(Game1)
            .GetField("_snapshotStatesByFrame", InstanceMembers)!
            .GetValue(game)!;

    private static ulong GetPinnedFrame(Game1 game)
        => (ulong)typeof(Game1)
            .GetField("_pinnedServerSnapshotBaselineFrame", InstanceMembers)!
            .GetValue(game)!;

    private static void SetField(Game1 game, string name, object? value)
        => typeof(Game1).GetField(name, InstanceMembers)!.SetValue(game, value);
}
