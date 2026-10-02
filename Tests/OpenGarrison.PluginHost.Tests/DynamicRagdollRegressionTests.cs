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
        GetGameplayManager(game).RuntimeSettings.CorpseDurationMode = ClientSettings.CorpseDurationInfinite;
        var world = (SimulationWorld)typeof(Game1).GetField("_world", instance)!.GetValue(game)!;
        var corpses = world.WorldObjects.DeadBodies;
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
        var corpses = world.WorldObjects.DeadBodies;
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

    [Theory]
    [InlineData(false, 90f)]
    [InlineData(true, 0f)]
    public void RenderedRagdollEndpointTouchingFloorIsNotPenetrating(bool elkondo, float rotationDegrees)
    {
        var level = Level(new LevelSolid(0, 150, 200, 50));
        var body = Body(elkondo);
        body.RotationDegrees = rotationDegrees;
        body.Y = 138;

        Assert.InRange(GetRenderedRagdollBottom(body), 149.99f, 150.01f);
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
    }

    [Fact]
    public void RotatedStripDoesNotCollideWithSolidInItsEmptyAxisAlignedCorner()
    {
        var solid = new LevelSolid(95.6f, 104.2f, 0.05f, 0.05f);
        var level = Level(solid);
        var body = Body(false);
        body.RotationDegrees = 45f;

        var radians = body.RotationDegrees * (MathF.PI / 180f);
        var localAcross = new Vector2(-MathF.Sin(radians), MathF.Cos(radians));
        var solidCenter = new Vector2(solid.Left + (solid.Width * 0.5f), solid.Top + (solid.Height * 0.5f));
        var acrossDistance = MathF.Abs(Vector2.Dot(solidCenter - new Vector2(body.X, body.Y), localAcross));
        var solidAcrossRadius = (solid.Width * MathF.Abs(localAcross.X) + solid.Height * MathF.Abs(localAcross.Y)) * 0.5f;
        Assert.True(acrossDistance - solidAcrossRadius > body.OpaqueBounds.Height * 0.5f + 0.01f);
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
    }

    [Theory]
    [InlineData(false, 151f)]
    [InlineData(true, 141f)]
    public void AsymmetricElkondoAlphaBoundsKeepTheMirroredFootSliceAtGroundContact(bool facingLeft, float rootY)
    {
        const int width = 64;
        const int height = 64;
        var pixels = new Color[width * height];
        FillAlphaRect(pixels, width, new Rectangle(26, 24, 14, 11));
        FillAlphaRect(pixels, width, new Rectangle(16, 35, 20, 12));
        FillAlphaRect(pixels, width, new Rectangle(14, 47, 22, 9));
        FillAlphaRect(pixels, width, new Rectangle(28, 56, 8, 8));
        var opaqueBounds = new Rectangle(14, 24, 26, 40);
        var segmentBounds = Game1.CreateRagdollCollisionSegmentOpaqueBounds(
            pixels,
            width,
            height,
            opaqueBounds,
            PlayerClass.Scout,
            useElkondoVerticalVisual: true);

        var actualSegmentBounds = Assert.IsType<Rectangle[]>(segmentBounds);
        Assert.Equal(
            new[]
            {
                new Rectangle(12, 0, 14, 11),
                new Rectangle(2, 0, 20, 12),
                new Rectangle(0, 0, 22, 9),
                new Rectangle(14, 0, 8, 8),
            },
            actualSegmentBounds);

        var body = Body(true, facingLeft: facingLeft);
        body.OpaqueBounds = opaqueBounds;
        body.CollisionSegmentOpaqueBounds = actualSegmentBounds;
        body.RotationDegrees = -90f;
        body.Y = rootY;
        var foot = actualSegmentBounds[3];
        var localAcrossStart = foot.Left - (opaqueBounds.Width * 0.5f);
        var localAcrossEnd = foot.Right - (opaqueBounds.Width * 0.5f);
        var acrossDirection = facingLeft ? 1f : -1f;
        var renderedFootBottom = Game1.GetRoundedPlayerSpriteOrigin(new Vector2(body.X, body.Y)).Y
            + MathF.Max(localAcrossStart * acrossDirection, localAcrossEnd * acrossDirection);
        var level = Level(new LevelSolid(112, 150, 10, 50));

        Assert.Equal(150f, renderedFootBottom, 3);
        Assert.False(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
        body.Y += 1f;
        Assert.True(Game1.IsRagdollPoseBlocked(body, level, level.Bounds));
    }

    [Fact]
    public void StockAlphaBoundsSkipAnEmptyCutWithoutLosingOtherSegments()
    {
        const int width = 64;
        const int height = 64;
        var pixels = new Color[width * height];
        FillAlphaRect(pixels, width, new Rectangle(4, 3, 7, 12));
        FillAlphaRect(pixels, width, new Rectangle(16, 8, 2, 2));
        FillAlphaRect(pixels, width, new Rectangle(24, 11, 4, 4));
        pixels[(10 * width) + 12] = new Color(255, 255, 255, 23);
        var opaqueBounds = new Rectangle(4, 3, 24, 12);
        var segmentBounds = Assert.IsType<Rectangle[]>(Game1.CreateRagdollCollisionSegmentOpaqueBounds(
            pixels,
            width,
            height,
            opaqueBounds,
            PlayerClass.Scout,
            useElkondoVerticalVisual: false));
        Assert.Equal(
            new[]
            {
                new Rectangle(0, 0, 7, 12),
                Rectangle.Empty,
                new Rectangle(0, 5, 2, 2),
                new Rectangle(2, 8, 4, 4),
            },
            segmentBounds);

        var body = Body(false);
        body.OpaqueBounds = opaqueBounds;
        body.CollisionSegmentOpaqueBounds = segmentBounds;
        Assert.False(Game1.IsRagdollPoseBlocked(
            body,
            Level(new LevelSolid(96, 98, 2, 4)),
            new WorldBounds(200, 200)));
        Assert.True(Game1.IsRagdollPoseBlocked(
            body,
            Level(new LevelSolid(90, 98, 1, 1)),
            new WorldBounds(200, 200)));
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
        var renderedGroundGap = level.Solids[0].Top - GetRenderedRagdollBottom(body);
        Assert.True(renderedGroundGap >= -1.5f && renderedGroundGap <= 2f,
            $"Rendered bottom gap={renderedGroundGap} x={body.X} y={body.Y} rotation={body.RotationDegrees}; pivots={string.Join(',', body.PivotDegrees)}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FoldedRenderedSegmentsTouchingFloorAreNotDepenetrated(bool elkondo)
    {
        var body = Body(elkondo);
        body.RotationDegrees = elkondo ? -12f : 18f;
        body.PivotDegrees[0] = elkondo ? 14f : -17f;
        body.PivotDegrees[1] = elkondo ? -24f : 22f;
        body.PivotDegrees[2] = elkondo ? 28f : -13f;
        body.Y = 100f;
        var floorTop = MathF.Ceiling(GetRenderedRagdollBottom(body));
        var level = Level(new LevelSolid(0, floorTop, 200, 200 - floorTop));
        var initialX = body.X;
        var initialY = body.Y;

        Game1.AdvanceRagdollSegmentCollision(body, level, level.Bounds);

        Assert.Equal(initialX, body.X);
        Assert.Equal(initialY, body.Y);
        var renderedGroundGap = floorTop - GetRenderedRagdollBottom(body);
        Assert.True(renderedGroundGap >= -1.5f && renderedGroundGap <= 1f,
            $"Rendered bottom gap={renderedGroundGap} x={body.X} y={body.Y} rotation={body.RotationDegrees}; pivots={string.Join(',', body.PivotDegrees)}");
    }

    private static Game1.DynamicRagdollState Body(bool elkondo, int id = 1, bool facingLeft = false) => new()
    {
        DeadBodyId = id, SourcePlayerId = id, ClassId = PlayerClass.Scout,
        Team = PlayerTeam.Red, GameplayClassId = "scout", AnimationKind = DeadBodyAnimationKind.Default,
        FacingLeft = facingLeft, X = 100, Y = 100,
        UseElkondoVerticalVisual = elkondo,
        OpaqueBounds = elkondo ? new(0, 0, 12, 24) : new(0, 0, 24, 12),
    };

    private static float GetRenderedRagdollBottom(Game1.DynamicRagdollState body)
    {
        var opaque = body.OpaqueBounds;
        var scaleX = body.FacingLeft ? -1f : 1f;
        var renderedOrigin = Game1.GetRoundedPlayerSpriteOrigin(new Vector2(body.X, body.Y));
        var bodyRotationRadians = body.RotationDegrees * (MathF.PI / 180f);
        var bottom = float.MinValue;
        if (body.UseElkondoVerticalVisual)
        {
            var waistFraction = body.ClassId switch
            {
                PlayerClass.Scout => 0.58f,
                PlayerClass.Soldier => 0.54f,
                PlayerClass.Pyro => 0.56f,
                PlayerClass.Demoman => 0.55f,
                PlayerClass.Heavy => 0.64f,
                PlayerClass.Medic => 0.57f,
                PlayerClass.Engineer => 0.56f,
                PlayerClass.Sniper => 0.54f,
                PlayerClass.Spy => 0.55f,
                _ => 0.56f,
            };
            var chestFraction = Math.Clamp(waistFraction * 0.48f, 0.16f, waistFraction - 0.10f);
            var kneeFraction = Math.Clamp(waistFraction + ((1f - waistFraction) * 0.50f), waistFraction + 0.10f, 0.90f);
            Span<float> cutFractions = stackalloc float[] { 0f, chestFraction, waistFraction, kneeFraction, 1f };
            Span<int> cutYs = stackalloc int[cutFractions.Length];
            for (var index = 0; index < cutFractions.Length; index += 1)
            {
                cutYs[index] = opaque.Top + Math.Clamp(
                    (int)MathF.Round(opaque.Height * cutFractions[index]),
                    0,
                    opaque.Height);
            }

            for (var index = 1; index < cutYs.Length; index += 1)
            {
                if (cutYs[index] <= cutYs[index - 1])
                {
                    cutYs[index] = Math.Min(opaque.Bottom, cutYs[index - 1] + 1);
                }
            }

            var cursor = renderedOrigin + TransformForDrawTest(
                new Vector2(0f, -opaque.Height * 0.5f), scaleX, bodyRotationRadians);
            var cumulativePivot = 0f;
            for (var segmentIndex = 0; segmentIndex < cutYs.Length - 1; segmentIndex += 1)
            {
                var segmentHeight = cutYs[segmentIndex + 1] - cutYs[segmentIndex];
                var radians = (body.RotationDegrees + cumulativePivot) * (MathF.PI / 180f);
                var along = TransformForDrawTest(Vector2.UnitY, scaleX, radians);
                var across = TransformForDrawTest(Vector2.UnitX, scaleX, radians);
                var center = cursor + (along * (segmentHeight * 0.5f));
                bottom = MathF.Max(bottom,
                    center.Y + (MathF.Abs(along.Y) * segmentHeight * 0.5f) + (MathF.Abs(across.Y) * opaque.Width * 0.5f));
                cursor += along * segmentHeight;
                if (segmentIndex < body.PivotDegrees.Length)
                {
                    cumulativePivot += body.PivotDegrees[segmentIndex];
                }
            }
        }
        else
        {
            Span<float> pivotFractions = stackalloc float[] { 0.28f, 0.52f, 0.76f };
            Span<int> cutXs = stackalloc int[Game1.DynamicRagdollPivotCount + 2];
            cutXs[0] = opaque.Left;
            for (var pivotIndex = 0; pivotIndex < Game1.DynamicRagdollPivotCount; pivotIndex += 1)
            {
                cutXs[pivotIndex + 1] = opaque.Left + Math.Clamp(
                    (int)MathF.Round(opaque.Width * pivotFractions[pivotIndex]),
                    1,
                    Math.Max(1, opaque.Width - 1));
            }

            cutXs[Game1.DynamicRagdollPivotCount + 1] = opaque.Right;
            for (var index = 1; index < cutXs.Length; index += 1)
            {
                if (cutXs[index] <= cutXs[index - 1])
                {
                    cutXs[index] = Math.Min(opaque.Right, cutXs[index - 1] + 1);
                }
            }

            var cursor = renderedOrigin + TransformForDrawTest(
                new Vector2(-opaque.Width * 0.5f, 0f), scaleX, bodyRotationRadians);
            var cumulativePivot = 0f;
            for (var segmentIndex = 0; segmentIndex < cutXs.Length - 1; segmentIndex += 1)
            {
                var segmentWidth = cutXs[segmentIndex + 1] - cutXs[segmentIndex];
                if (segmentWidth <= 0)
                {
                    continue;
                }

                var radians = (body.RotationDegrees + cumulativePivot) * (MathF.PI / 180f);
                var along = TransformForDrawTest(Vector2.UnitX, scaleX, radians);
                var across = TransformForDrawTest(Vector2.UnitY, scaleX, radians);
                var center = cursor + (along * (segmentWidth * 0.5f));
                bottom = MathF.Max(bottom,
                    center.Y + (MathF.Abs(along.Y) * segmentWidth * 0.5f) + (MathF.Abs(across.Y) * opaque.Height * 0.5f));
                cursor += along * segmentWidth;
                if (segmentIndex < Game1.DynamicRagdollPivotCount)
                {
                    cumulativePivot += body.PivotDegrees[segmentIndex];
                }
            }
        }

        return bottom;
    }

    private static Vector2 TransformForDrawTest(Vector2 local, float scaleX, float radians)
    {
        var scaledX = local.X * scaleX;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2((scaledX * cos) - (local.Y * sin), (scaledX * sin) + (local.Y * cos));
    }

    private static void FillAlphaRect(Color[] pixels, int width, Rectangle rectangle)
    {
        for (var y = rectangle.Top; y < rectangle.Bottom; y += 1)
        {
            Array.Fill(pixels, Color.White, (y * width) + rectangle.Left, rectangle.Width);
        }
    }

    private static SimpleLevel Level(params LevelSolid[] solids) => new("ragdoll-test",
        GameModeKind.CaptureTheFlag, new(200, 200), 1, null, 1, 1,
        new(30, 30), [], [], [], [], 200, solids, false);

    private static Game1 CreateGame(SimpleLevel level)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CombatTestReplaceLevel(level);
        Set(game, "_world", world);
        // The dead-body renderer is resolved through the service container, which
        // the constructor normally populates.
        var services = new ClientServiceContainer();
        Set(game, "_services", services);
        var gameplayManager = new GameplayManager((IGameplayContext)game);
        services.Register(gameplayManager);
        gameplayManager.RuntimeSettings.DynamicRagdollEnabled = true;
        services.Register(new GameplayDeadBodyRenderController((IRenderContext)game));
        foreach (var name in new[] { "_dynamicRagdolls", "_staleDynamicRagdollIds", "_retainedDeadBodies", "_immediateNetworkDeadBodies",
            "_networkClient" })
        {
            var field = typeof(Game1).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            field.SetValue(game, Activator.CreateInstance(field.FieldType));
        }
        return game;
    }

    private static Dictionary<int, Game1.DynamicRagdollState> GetBodies(Game1 game)
        => (Dictionary<int, Game1.DynamicRagdollState>)typeof(Game1).GetField("_dynamicRagdolls", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(game)!;
    private static GameplayManager GetGameplayManager(Game1 game)
        => (GameplayManager)typeof(Game1).GetProperty("_gameplayManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(game)!;
    private static void Set(Game1 game, string name, object value)
        => typeof(Game1).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(game, value);
    private static void Invoke(Game1 game, string name, params object[] args)
        => typeof(Game1).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(game, args);
}
