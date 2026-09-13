using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ProjectilePresentationCollisionTests
{
    [Theory]
    [InlineData(120f)]
    [InlineData(300f)]
    [InlineData(900f)]
    public void ExtrapolatedRocketStopsBeforeAThinWallRegardlessOfNetworkGap(float desiredX)
    {
        var world = CreateWorld([new LevelSolid(100f, 0f, 1f, 512f)], []);
        var result = world.ClampProjectilePresentationPath(PlayerTeam.Red, 20f, 50f, desiredX, 50f,
            RocketProjectileEntity.EnvironmentCollisionBackoffDistance);

        Assert.Equal(100f - RocketProjectileEntity.EnvironmentCollisionBackoffDistance, result.X, precision: 3);
        Assert.Equal(50f, result.Y);
        Assert.Empty(world.PendingDamageEvents);
        Assert.Empty(world.PendingVisualEvents);
        Assert.Empty(world.Rockets);
    }

    [Fact]
    public void PresentationSweepRespectsTheSameTeamGateAsProjectileCollision()
    {
        var gate = new RoomObjectMarker(RoomObjectType.TeamGate, 100f, 0f, 20f, 512f, "RedGate", PlayerTeam.Red);
        var world = CreateWorld([], [gate]);

        var friendly = world.ClampProjectilePresentationPath(PlayerTeam.Red, 20f, 50f, 200f, 50f);
        var hostile = world.ClampProjectilePresentationPath(PlayerTeam.Blue, 20f, 50f, 200f, 50f);

        // Team gates admit friendly players, but block projectiles of both teams.
        Assert.Equal(100f, friendly.X);
        Assert.Equal(100f, hostile.X);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void ImpactWaitsForItsSourceTickInsteadOfRetransmissionArrival(int tickRate)
    {
        var impactFrame = (ulong)(tickRate * 4);
        Assert.False(NetworkInterpolationPolicy.IsSourceFrameReady(impactFrame, tickRate, 3.99));
        Assert.True(NetworkInterpolationPolicy.IsSourceFrameReady(impactFrame, tickRate, 4.0));
        Assert.True(NetworkInterpolationPolicy.IsSourceFrameReady(impactFrame, tickRate, 4.3));
    }

    [Fact]
    public void RemovedProjectilePresentationRetainsUntilTerminalTickWithoutExtendingOnReplay()
    {
        const int tickRate = 30;
        const ulong terminalFrame = 120;

        Assert.True(Game1.ShouldRetainRemovedProjectilePresentation(terminalFrame, tickRate, 3.99));
        Assert.False(Game1.ShouldRetainRemovedProjectilePresentation(terminalFrame, tickRate, 4.0));
        Assert.Equal(
            terminalFrame,
            Game1.ResolveRetainedProjectilePresentationSourceFrame(terminalFrame, terminalFrame + 30));
    }

    [Fact]
    public void Protocol64RemovalCapturesDetachedProxyBeforeWorldRemovalAndExpiresItOnce()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var state = new Protocol64ProjectileState(
            EntityId: 7,
            Generation: 1,
            EntityKind: Protocol64ProjectileKind.Rocket,
            StateTick: 100,
            OwnerSlot: 1,
            OwnerGeneration: 1,
            X: 40f,
            Y: 50f,
            VelocityX: 8f,
            VelocityY: 0f,
            Rotation: 0f,
            IsActive: true,
            RemainingLifetimeTicks: 30,
            Damage: 90f);
        var lifecycle = new Protocol64ProjectileLifecycle(
            Protocol64ProjectileLifecycleKind.Despawn,
            EntityId: 7,
            Generation: 1,
            EntityKind: Protocol64ProjectileKind.Rocket,
            StateTick: 120,
            OwnerSlot: 1,
            OwnerGeneration: 1,
            X: 40f,
            Y: 50f,
            VelocityX: 8f,
            VelocityY: 0f,
            Rotation: 0f,
            IsActive: false,
            RemainingLifetimeTicks: 0,
            Damage: 90f);
        var applier = new Protocol64StateApplier();
        Assert.Equal(Protocol64StateApplyStatus.Applied, applier.ApplyProjectileState(state).Status);
        applier.ApplyToWorld(world);
        Assert.Single(world.Rockets);

        var game = CreatePresentationHarness(world);
        InvokePrivate(
            game,
            "CaptureProtocol64RemovedProjectilePresentationEntities",
            new[] { lifecycle },
            lifecycle.StateTick);
        applier.ApplyProjectileLifecycle(lifecycle);
        applier.ApplyToWorld(world);
        Assert.Empty(world.Rockets);

        var retained = GetPrivateField<IDictionary<int, RocketProjectileEntity>>(
            game,
            "_retainedRocketPresentationEntities");
        Assert.Contains(7, retained.Keys);

        var terminalTime = lifecycle.StateTick / (double)world.Config.TicksPerSecond;
        InvokePrivate(game, "UpdateRetainedProjectilePresentationEntities", terminalTime - 0.01d);
        Assert.Contains(7, retained.Keys);
        InvokePrivate(game, "UpdateRetainedProjectilePresentationEntities", terminalTime);
        Assert.DoesNotContain(7, retained.Keys);
        Assert.False(world.Entities.ContainsKey(7));

        // A locally predicted impact removes its projectile before the
        // authoritative lifecycle reaches this hook. It must not leave a
        // renderer-only proxy behind beneath the immediate local effect.
        var locallyResolvedState = state with { EntityId = 8 };
        Assert.True(world.ApplyProtocol64ProjectileState(locallyResolvedState));
        Assert.True(world.RemoveProtocol64Projectile(8));
        InvokePrivate(
            game,
            "CaptureProtocol64RemovedProjectilePresentationEntities",
            new[] { lifecycle with { EntityId = 8 } },
            lifecycle.StateTick);
        Assert.DoesNotContain(8, retained.Keys);
    }

    private static SimulationWorld CreateWorld(LevelSolid[] solids, RoomObjectMarker[] markers)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(20f, 50f);
        var level = new SimpleLevel("presentation-sweep", GameModeKind.TeamDeathmatch,
            new WorldBounds(1024f, 512f), 1f, null, 1, 1, spawn, [spawn], [spawn], [], markers,
            512f, solids, false);
        typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [level]);
        _ = world.DrainPendingVisualEvents();
        _ = world.DrainPendingDamageEvents();
        return world;
    }

    private static object CreatePresentationHarness(SimulationWorld world)
    {
        var game = RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        SetPrivateField(game, "_config", world.Config);
        SetPrivateField(game, "_world", world);
        SetPrivateField(game, "_retainedRocketPresentationEntities", new Dictionary<int, RocketProjectileEntity>());
        SetPrivateField(game, "_retainedFlarePresentationEntities", new Dictionary<int, FlareProjectileEntity>());
        SetPrivateField(game, "_retainedProjectilePresentationSourceFrames", new Dictionary<int, ulong>());
        SetPrivateFieldToNewCollection(game, "_entitySnapshotHistories");
        SetPrivateFieldToNewCollection(game, "_entitySnapshotHistoryKinds");
        SetPrivateFieldToNewCollection(game, "_entityInterpolationTracks");
        SetPrivateFieldToNewCollection(game, "_interpolatedEntityPositions");
        SetPrivateFieldToNewCollection(game, "_activeInterpolatedEntityIds");
        SetPrivateFieldToNewCollection(game, "_staleInterpolatedEntityIds");
        SetPrivateField(game, "_localProjectileLaunchOriginOffsets", new Dictionary<int, Microsoft.Xna.Framework.Vector2>());
        return game;
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        var method = typeof(Game1).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(Game1).FullName, methodName);
        method.Invoke(target, arguments);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = typeof(Game1).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Game1).FullName, fieldName);
        return (T)(field.GetValue(target) ?? throw new InvalidOperationException(fieldName));
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = typeof(Game1).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Game1).FullName, fieldName);
        field.SetValue(target, value);
    }

    private static void SetPrivateFieldToNewCollection(object target, string fieldName)
    {
        var field = typeof(Game1).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Game1).FullName, fieldName);
        field.SetValue(target, Activator.CreateInstance(field.FieldType));
    }
}
