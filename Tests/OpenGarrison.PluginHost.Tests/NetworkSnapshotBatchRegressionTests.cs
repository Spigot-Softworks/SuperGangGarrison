using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NetworkSnapshotBatchRegressionTests
{
    [Fact]
    public void ChainedDeltaBatchRetainsStateAndEachSnapshotsTransientEvents()
    {
        var fullSnapshot = CreateSnapshot(1);
        var firstDelta = fullSnapshot with
        {
            Frame = 2,
            IsDelta = true,
            BaselineFrame = 1,
            Players = [],
            Shots = [],
            VisualEvents = [new SnapshotVisualEvent("spark", 1, 2, 0, 1, EventId: 21)],
            SoundEvents = [new SnapshotSoundEvent("shot", 1, 2, EventId: 31)],
        };
        var firstResolved = SnapshotDelta.ToFullSnapshot(
            firstDelta,
            SnapshotBaselineState.FromSnapshot(fullSnapshot));

        var secondDelta = fullSnapshot with
        {
            Frame = 3,
            IsDelta = true,
            BaselineFrame = 2,
            Players = [],
            Shots = [],
            VisualEvents = [new SnapshotVisualEvent("spark", 3, 4, 0, 1, EventId: 22)],
            SoundEvents = [new SnapshotSoundEvent("shot", 3, 4, EventId: 32)],
        };
        var secondResolved = SnapshotDelta.ToFullSnapshot(
            secondDelta,
            SnapshotBaselineState.FromSnapshot(firstResolved));

        Assert.Equal((ulong)2, firstResolved.Frame);
        Assert.Equal((ulong)3, secondResolved.Frame);
        Assert.Single(firstResolved.Shots);
        Assert.Single(secondResolved.Shots);
        Assert.Equal(fullSnapshot.Shots[0].Id, secondResolved.Shots[0].Id);
        Assert.Equal((ulong)21, Assert.Single(firstResolved.VisualEvents).EventId);
        Assert.Equal((ulong)22, Assert.Single(secondResolved.VisualEvents).EventId);
        Assert.Equal((ulong)31, Assert.Single(firstResolved.SoundEvents).EventId);
        Assert.Equal((ulong)32, Assert.Single(secondResolved.SoundEvents).EventId);
    }

    [Fact]
    public void ResolvingSnapshotQueuesEventsAndSharesBatchBaselineWrapper()
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld();
        var networkClient = new NetworkGameClient();
        var services = new ClientServiceContainer();
        SetField(game, "_world", world);
        SetField(game, "_config", world.Config);
        SetField(game, "_networkClient", networkClient);
        SetField(game, "_services", services);
        SetField(game, "_gameplaySessionState", new Game1.GameplaySessionState());
        SetField(game, "_processedNetworkVisualEventIds", new HashSet<ulong>());
        SetField(game, "_processedNetworkVisualEventOrder", new Queue<ulong>());
        SetField(game, "_snapshotStringCacheConnectionGeneration", networkClient.ConnectionGeneration);
        SetField(game, "_hasSnapshotStringCacheConnectionGeneration", true);
        var gameplay = new GameplayManager((IGameplayContext)game);
        services.Register(gameplay);
        services.Register(new AudioManager((IAudioContext)game));
        services.Register(new PluginManager((IPluginContext)game));

        var snapshot = CreateSnapshot(1) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = checked((byte)world.Level.MapAreaIndex),
            VisualEvents = [new SnapshotVisualEvent("spark", 4, 5, 0, 1, EventId: 91)],
            SoundEvents = [new SnapshotSoundEvent("shot", 4, 5, EventId: 92)],
        };
        var entryType = typeof(Game1).GetNestedType("ResolvedSnapshotEntry", PrivateInstance)!;
        var batch = Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType))!;
        var baselinesByFrame = new Dictionary<ulong, SnapshotBaselineState>();
        object?[] arguments = [snapshot, 0UL, null, baselinesByFrame, batch];

        var accepted = (bool)typeof(Game1)
            .GetMethod("TryHandleSnapshotMessage", PrivateInstance)!
            .Invoke(game, arguments)!;

        Assert.True(accepted);
        Assert.Single(baselinesByFrame);
        var baseline = baselinesByFrame[snapshot.Frame];
        var entry = Assert.IsAssignableFrom<IList>(batch)[0]!;
        var entryBaseline = (SnapshotBaselineState)entryType
            .GetProperty("BaselineState", PrivateInstance | BindingFlags.Public)!
            .GetValue(entry)!;
        Assert.Same(baseline, entryBaseline);

        var visuals = (List<SnapshotVisualEvent>)typeof(GameplayVisualEventController)
            .GetField("_pendingNetworkVisualEvents", PrivateInstance)!
            .GetValue(gameplay.VisualEvents)!;
        var sounds = (List<WorldSoundEvent>)typeof(GameplayAudioEventController)
            .GetField("_pendingNetworkSoundEvents", PrivateInstance)!
            .GetValue(services.Get<AudioManager>().Events)!;
        Assert.Equal((ulong)1, Assert.Single(visuals).SourceFrame);
        Assert.Equal((ulong)91, visuals[0].EventId);
        Assert.Equal((ulong)1, Assert.Single(sounds).SourceFrame);
        Assert.Equal((ulong)92, sounds[0].EventId);
    }

    private static SnapshotMessage CreateSnapshot(ulong frame)
    {
        var builder = typeof(ClientSessionSnapshotHistoryTests).GetMethod(
            "CreateSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (SnapshotMessage)builder.Invoke(null, [frame])!;
    }

    private static void SetField(Game1 game, string name, object? value)
        => typeof(Game1).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(game, value);
}
