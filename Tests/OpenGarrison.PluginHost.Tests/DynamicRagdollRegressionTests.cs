using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class DynamicRagdollRegressionTests
{
    [Fact]
    public void InfiniteCorpseKeepsItsLastDynamicPoseWhenTheWorldExpiresIt()
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = CreateGame(Level());
        Set(game, "_corpseDurationMode", ClientSettings.CorpseDurationInfinite);
        var world = (SimulationWorld)typeof(Game1).GetField("_world", instance)!.GetValue(game)!;
        var corpses = (List<DeadBodyEntity>)typeof(SimulationWorld).GetField("_deadBodies", instance)!.GetValue(world)!;
        var corpse = new DeadBodyEntity(1, 1, PlayerClass.Scout, PlayerTeam.Red, DeadBodyAnimationKind.Default,
            100, 100, 24, 12, 0, 0, false, "scout");
        corpse.ApplyNetworkState(100, 100, 0, 0, 1);
        corpses.Add(corpse);
        var pose = Body(false);
        pose.X = 70;
        pose.Y = 80;
        pose.RotationDegrees = 21;
        GetBodies(game).Add(1, pose);
        Invoke(game, "SyncRetainedDeadBodies");
        corpses.Clear();
        Invoke(game, "SyncRetainedDeadBodies");
        Invoke(game, "SyncDynamicRagdollsWithDeadBodies");
        Assert.Same(pose, GetBodies(game)[1]);
        Assert.True(pose.SimulationFrozen);
        Invoke(game, "AdvanceDynamicRagdolls");
        Assert.Equal(70f, pose.X);
        Assert.Equal(80f, pose.Y);
        Assert.Equal(21f, pose.RotationDegrees);
    }
    [Fact]
    public void ImmediatePoseTransfersToTheRealCorpseWithoutSnappingAndUsesTheRealDeathDirection()
    {
        var game = CreateGame(Level());
        var pose = Body(false, 19);
        pose.DeadBodyId = -19;
        pose.X = 80;
        pose.Y = 90;
        pose.VelocityX = -2;
        GetBodies(game).Add(-19, pose);
        var world = (SimulationWorld)typeof(Game1).GetField("_world", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(game)!;
        var corpses = (List<DeadBodyEntity>)typeof(SimulationWorld).GetField("_deadBodies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(world)!;
        corpses.Add(new(99, 19, PlayerClass.Scout, PlayerTeam.Red, DeadBodyAnimationKind.Default,
            100, 100, 24, 12, 6, 2, false, "scout"));
        Invoke(game, "SyncDynamicRagdollsWithDeadBodies");
        Assert.False(GetBodies(game).ContainsKey(-19));
        Assert.Same(pose, GetBodies(game)[99]);
        Assert.Equal(99, pose.DeadBodyId);
        Assert.Equal(80f, pose.X);
        Assert.Equal(90f, pose.Y);
        Assert.Equal(6f, pose.VelocityX);
        Assert.Equal(2f, pose.VelocityY);
    }

    [Fact]
    public void CorpseCannotCaptureTheWeaponOfARespawnedPlayer()
    {
        var game = CreateGame(Level());
        var world = (SimulationWorld)typeof(Game1).GetField("_world", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(game)!;
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        var pose = Body(true);
        Invoke(game, "TryCaptureElkondoRagdollWeapon", pose, world.LocalPlayer);
        Assert.Empty(pose.WeaponSpriteName);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FastBodiesCannotPassThroughThinFloors(bool elkondo)
    {
        var level = Level(new LevelSolid(0, 100, 200, 1));
        var body = Body(elkondo);
        body.Y = 70;
        body.VelocityY = 32;
        Assert.True(Game1.AdvanceRagdollSegmentCollision(body, level, level.Bounds));
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
        Assert.True(body.Y < 100);
        Assert.True(body.VelocityY <= 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FastBodiesCannotPassThroughThinWalls(bool elkondo)
    {
        var level = Level(new LevelSolid(120, 0, 1, 200));
        var body = Body(elkondo);
        body.X = 90;
        body.VelocityX = 32;
        Game1.AdvanceRagdollSegmentCollision(body, level, level.Bounds);
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
        Assert.True(body.X < 120);
        Assert.True(body.VelocityX < 0);
    }

    [Fact]
    public void WholeSolidInteriorBlocksTheBodyAndOverlapResolvesWithoutAnImpulse()
    {
        var level = Level(new LevelSolid(60, 60, 80, 80));
        var body = Body(false);
        Assert.True(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
        Game1.AdvanceRagdollSegmentCollision(body, level, level.Bounds);
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
        Assert.Equal(0f, body.VelocityX);
        Assert.Equal(0f, body.VelocityY);
    }

    [Fact]
    public void AttachedWeaponDoesNotChangeBodyCollision()
    {
        var level = Level(new LevelSolid(120, 0, 1, 200));
        var body = Body(true);
        body.X = 100;
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
        body.WeaponSpriteName = "RocketLauncherS";
        body.WeaponAttachLocalX = 100;
        body.WeaponFlapDegrees = 90;
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
    }

    [Fact]
    public void CapacityPreservesOldPosesInsteadOfDeletingAndRelaunchingThem()
    {
        var game = CreateGame(Level(new LevelSolid(0, 150, 200, 50)));
        var bodies = GetBodies(game);
        for (var id = 1; id <= 35; id++)
        {
            var body = Body(false, id);
            body.AgeTicks = id;
            body.VelocityX = 5;
            body.VelocityY = -3;
            body.RotationDegrees = 17;
            body.PivotDegrees[0] = 23;
            bodies.Add(id, body);
        }
        Invoke(game, "TrimDynamicRagdollCapacity", false);
        Assert.Equal(35, bodies.Count);
        Assert.Equal(28, bodies.Values.Count(body => !body.SimulationFrozen));
        var oldest = bodies[35];
        Assert.True(oldest.SimulationFrozen);
        Assert.Equal(0f, oldest.VelocityX);
        for (var tick = 0; tick < 10; tick++) Invoke(game, "AdvanceDynamicRagdolls");
        Assert.Same(oldest, bodies[35]);
        Assert.Equal(100f, oldest.X);
        Assert.Equal(100f, oldest.Y);
        Assert.Equal(17f, oldest.RotationDegrees);
        Assert.Equal(23f, oldest.PivotDegrees[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BodiesRemainOutsideTerrainDuringRotationAndEventuallyRest(bool elkondo)
    {
        var level = Level(new LevelSolid(0, 150, 200, 50));
        var game = CreateGame(level);
        var body = Body(elkondo);
        body.RotationDegrees = elkondo ? 0 : 90;
        body.AngularVelocityDegrees = 4;
        body.VelocityX = 3;
        GetBodies(game).Add(1, body);
        for (var tick = 0; tick < 400; tick++)
        {
            Invoke(game, "AdvanceDynamicRagdolls");
            Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds), $"Body overlapped terrain on tick {tick}");
        }
        Assert.True(body.Settled, $"x={body.X} y={body.Y} vx={body.VelocityX} vy={body.VelocityY} rotation={body.RotationDegrees} spin={body.AngularVelocityDegrees} grounded={body.GroundedTicks}; pivots={string.Join(',', body.PivotDegrees)}");
        Assert.Equal(0f, body.VelocityY);
    }

    private static Game1.DynamicRagdollState Body(bool elkondo, int id = 1) => new()
    {
        DeadBodyId = id, SourcePlayerId = id, ClassId = PlayerClass.Scout,
        Team = PlayerTeam.Red, GameplayClassId = "scout", AnimationKind = DeadBodyAnimationKind.Default,
        FacingLeft = false, X = 100, Y = 100,
        UseElkondoVerticalVisual = elkondo,
        OpaqueBounds = elkondo ? new(0, 0, 12, 24) : new(0, 0, 24, 12),
    };

    private static SimpleLevel Level(params LevelSolid[] solids) => new("ragdoll-test",
        GameModeKind.CaptureTheFlag, new(200, 200), 1, null, 1, 1,
        new(30, 30), [], [], [], [], 200, solids, false);

    private static Game1 CreateGame(SimpleLevel level)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Level))!.SetValue(world, level);
        Set(game, "_world", world);
        Set(game, "_dynamicRagdollEnabled", true);
        // The dead-body renderer is resolved through the service container, which
        // the constructor normally populates.
        var services = new ClientServiceContainer();
        services.Register(new GameplayDeadBodyRenderController((IRenderContext)game));
        Set(game, "_services", services);
        foreach (var name in new[] { "_dynamicRagdolls", "_staleDynamicRagdollIds", "_retainedDeadBodies", "_immediateNetworkDeadBodies",
            "_networkClient", "_trackedDeadBodyVisuals", "_staleTrackedDeadBodyIds" })
        {
            var field = typeof(Game1).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            field.SetValue(game, Activator.CreateInstance(field.FieldType));
        }
        return game;
    }

    private static Dictionary<int, Game1.DynamicRagdollState> GetBodies(Game1 game)
        => (Dictionary<int, Game1.DynamicRagdollState>)typeof(Game1).GetField("_dynamicRagdolls", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(game)!;
    private static void Set(Game1 game, string name, object value)
        => typeof(Game1).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(game, value);
    private static void Invoke(Game1 game, string name, params object[] args)
        => typeof(Game1).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(game, args);
}
