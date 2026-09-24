using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CameraPanningRuntimeTests
{
    [Theory]
    [InlineData(960, 540)]
    [InlineData(800, 600)]
    [InlineData(780, 624)]
    public void DeadZoneUsesWidthBasedEllipse(int viewportWidth, int viewportHeight)
    {
        var centerX = viewportWidth / 2f;
        var centerY = viewportHeight / 2f;
        var (deadZoneRadiusX, deadZoneRadiusY) = CameraPanningState.ResolveDeadZoneRadii(
            viewportWidth,
            viewportHeight);

        var inside = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + deadZoneRadiusX * 0.99f,
            centerY);
        var justOutside = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + deadZoneRadiusX * 1.01f,
            centerY);
        var diagonalInside = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + deadZoneRadiusX * 0.7f,
            centerY + deadZoneRadiusY * 0.7f);

        Assert.Equal(Vector2.Zero, inside);
        Assert.Equal(Vector2.Zero, diagonalInside);
        Assert.True(justOutside.X > 0f);
        Assert.Equal(0f, justOutside.Y);
        Assert.True(justOutside.Length() < 0.05f);
    }

    [Fact]
    public void WideAspectRatiosClampVerticalDeadZoneToSixtyPercentOfHeight()
    {
        const int viewportWidth = 1920;
        const int viewportHeight = 400;
        var (radiusX, radiusY) = CameraPanningState.ResolveDeadZoneRadii(viewportWidth, viewportHeight);

        Assert.Equal(viewportWidth * CameraPanningState.DeadZoneRadiusScreenWidthFraction, radiusX);
        Assert.Equal(viewportHeight * CameraPanningState.DeadZoneMaxHeightScreenFraction * 0.5f, radiusY);
        Assert.True(radiusY < radiusX);

        var centerX = viewportWidth / 2f;
        var centerY = viewportHeight / 2f;
        var insideVertically = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX,
            centerY + radiusY * 0.99f);
        var outsideVertically = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX,
            centerY + radiusY * 1.01f);

        Assert.Equal(Vector2.Zero, insideVertically);
        Assert.True(outsideVertically.Y > 0f);
        Assert.Equal(0f, outsideVertically.X);
    }

    [Theory]
    [InlineData(960, 540)]
    [InlineData(800, 600)]
    [InlineData(780, 624)]
    public void PanStrengthRampsFromDeadZoneEdgeToNinetyPercentOfScreenEdge(int viewportWidth, int viewportHeight)
    {
        var centerX = viewportWidth / 2f;
        var centerY = viewportHeight / 2f;
        var (deadZoneRadiusX, _) = CameraPanningState.ResolveDeadZoneRadii(viewportWidth, viewportHeight);
        var halfWidth = viewportWidth * 0.5f;
        var fullPanDistance = halfWidth * CameraPanningState.FullPanScreenFraction;
        var midDistance = (deadZoneRadiusX + fullPanDistance) * 0.5f;

        var mid = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + midDistance,
            centerY);
        var atFullPan = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + fullPanDistance,
            centerY);
        var pastFullPan = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + halfWidth * 0.98f,
            centerY);

        Assert.InRange(mid.X, 0.5f - 0.0001f, 0.5f + 0.0001f);
        Assert.Equal(0f, mid.Y);
        Assert.InRange(atFullPan.X, 1f - 0.0001f, 1f + 0.0001f);
        Assert.InRange(pastFullPan.X, 1f - 0.0001f, 1f + 0.0001f);

        var state = new CameraPanningState();
        var pan = state.Update(atFullPan, 0f, 1d, advance: true);
        Assert.InRange(pan.X, CameraPanningState.OffsetPixels - 0.0001f, CameraPanningState.OffsetPixels + 0.0001f);
        Assert.Equal(0f, pan.Y);
    }

    [Theory]
    [InlineData(960, 540)]
    [InlineData(800, 600)]
    [InlineData(780, 624)]
    public void FartherMouseProducesStrongerPanOnBothAxes(int viewportWidth, int viewportHeight)
    {
        var centerX = viewportWidth / 2f;
        var centerY = viewportHeight / 2f;
        var (deadZoneRadiusX, deadZoneRadiusY) = CameraPanningState.ResolveDeadZoneRadii(
            viewportWidth,
            viewportHeight);
        var near = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + deadZoneRadiusX + 20f,
            centerY + deadZoneRadiusY + 20f);
        var far = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            centerX + deadZoneRadiusX + 120f,
            centerY + deadZoneRadiusY + 120f);

        var nearState = new CameraPanningState();
        var farState = new CameraPanningState();
        var nearPan = nearState.Update(near, 0f, 1d, advance: true);
        var farPan = farState.Update(far, 0f, 1d, advance: true);

        Assert.True(far.Length() > near.Length());
        Assert.True(MathF.Abs(farPan.X) > MathF.Abs(nearPan.X));
        Assert.True(MathF.Abs(farPan.Y) > MathF.Abs(nearPan.Y));
    }

    [Fact]
    public void PanOffsetUsesBothAxesOfAim()
    {
        var state = new CameraPanningState();
        var pan = state.Update(new Vector2(0.6f, 0.8f), 0f, 1d, advance: true);

        Assert.InRange(pan.Length(), CameraPanningState.OffsetPixels - 0.0001f, CameraPanningState.OffsetPixels + 0.0001f);
        Assert.InRange(pan.X, 38.4f - 0.0001f, 38.4f + 0.0001f);
        Assert.InRange(pan.Y, 51.2f - 0.0001f, 51.2f + 0.0001f);
    }

    [Fact]
    public void VerticalAimPansTheCameraVertically()
    {
        var state = new CameraPanningState();
        var pan = state.Update(Vector2.UnitY, 0f, 1d, advance: true);

        Assert.Equal(0f, pan.X);
        Assert.InRange(pan.Y, CameraPanningState.OffsetPixels - 0.0001f, CameraPanningState.OffsetPixels + 0.0001f);
    }

    [Theory]
    [InlineData(960, 540)]
    [InlineData(800, 600)]
    [InlineData(780, 624)]
    public void CenterDeadZoneReturnsZeroDirection(int viewportWidth, int viewportHeight)
    {
        var center = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            viewportWidth / 2f,
            viewportHeight / 2f);
        var onePixelFromCenter = CameraPanningState.GetMouseDirection(
            viewportWidth,
            viewportHeight,
            viewportWidth / 2f + 1f,
            viewportHeight / 2f - 1f);

        Assert.Equal(Vector2.Zero, center);
        Assert.Equal(Vector2.Zero, onePixelFromCenter);
    }

    [Fact]
    public void ReversingHorizontalPanNeverSwingsVertically()
    {
        var state = new CameraPanningState();
        state.Update(Vector2.UnitX, 0f, 0d, true);
        for (var frame = 1; frame <= 60; frame++)
        {
            var pan = state.Update(-Vector2.UnitX, 1f / 60f, frame / 60d, true);
            Assert.Equal(0f, pan.Y);
            Assert.InRange(pan.X, -64f, 64f);
        }
    }

    [Fact]
    public void OffsetAdvanceHasComparableProgressAtCommonFrameRates()
    {
        var target = new Vector2(-64f, 0f);
        var atThirty = new Vector2(64f, 0f);
        var atSixty = atThirty;
        for (var frame = 0; frame < 3; frame++)
            atThirty = CameraPanningState.AdvanceOffset(atThirty, target, 1f / 30f);
        for (var frame = 0; frame < 6; frame++)
            atSixty = CameraPanningState.AdvanceOffset(atSixty, target, 1f / 60f);
        Assert.InRange(Vector2.Distance(atThirty, atSixty), 0f, 0.001f);
    }

    [Theory]
    [InlineData(1f, 100f, 650f)]
    [InlineData(1.25f, 400f, 680f)]
    [InlineData(1.5f, 800f, 700f)]
    public void MouseOverPlayerRemainsInDeadZoneAtClampedEdges(float zoom, float x, float y)
    {
        var player = new Vector2(x, y);
        var camera = CameraPanningState.ClampToMap(player - new Vector2(480f,270f) / zoom,
            (int)(960 / zoom), (int)(540 / zoom), new WorldBounds(1024,768));
        var screen = (player-camera)*zoom;
        Assert.Equal(Vector2.Zero, CameraPanningState.GetMouseDirectionFromPlayer(960,540,screen,screen));
        // 100px is inside the 20%-width dead zone on a 960-wide viewport.
        Assert.Equal(Vector2.Zero, CameraPanningState.GetMouseDirectionFromPlayer(960,540,screen+new Vector2(100,0),screen));
        var outsideDeadZone = CameraPanningState.GetMouseDirectionFromPlayer(
            960, 540, screen + new Vector2(250f, 0f), screen);
        Assert.True(outsideDeadZone.X > 0f);
        Assert.Equal(0f, outsideDeadZone.Y);
        Assert.True(outsideDeadZone.X < 1f);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void NetworkAimRemainsHorizontalAtBottomMapBoundary(float zoom)
    {
        var player = new Vector2(400,700);
        var camera = CameraPanningState.ClampToMap(player-new Vector2(480,270)/zoom,
            (int)(960/zoom),(int)(540/zoom),new WorldBounds(1024,768));
        var game = (Game1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        SetPrivateField(game,"_hasGameplayCameraTopLeft",true);
        SetPrivateField(game,"_gameplayCameraTopLeft",camera);
        SetPrivateField(game,"_gameplayCameraPlayerPosition",player);
        var origin = (Vector2)typeof(Game1).GetMethod("GetGameplayInputAimOrigin",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(game,null)!;
        var mouse = (player-camera)*zoom + new Vector2(100,0);
        var aimWorld = camera + mouse/zoom;
        var relative = aimWorld-origin;
        var serverInput = ServerHelpers.ConvertRelativeAimToWorld(
            default(OpenGarrison.Core.PlayerInputSnapshot) with { AimWorldX=relative.X, AimWorldY=relative.Y },
            player.X,player.Y);
        Assert.InRange(MathF.Abs(serverInput.AimWorldY-player.Y),0f,0.0001f);
        Assert.True(serverInput.AimWorldX>player.X);
    }

    [Fact]
    public void PreviewAndRepeatedFrameUpdatesDoNotAdvanceStateTwice()
    {
        var state = new CameraPanningState();
        var initial = state.Update(Vector2.UnitX, 0f, 6d, advance: true);
        var preview = state.Update(-Vector2.UnitX, 1f / 60f, 7d, advance: false);
        var committed = state.Update(-Vector2.UnitX, 1f / 60f, 7d, advance: true);
        var repeated = state.Update(-Vector2.UnitX, 1f / 60f, 7d, advance: true);

        Assert.Equal(initial, preview);
        Assert.NotEqual(initial, committed);
        Assert.Equal(committed, repeated);
        Assert.Equal(0f, committed.Y);
    }

    [Fact]
    public void PreviewBeforeFirstCommitMatchesTheFirstCommittedDirectionWithoutMutatingState()
    {
        var state = new CameraPanningState();
        var preview = state.Update(Vector2.UnitX, 1f / 60f, 8d, advance: false);
        var committed = state.Update(Vector2.UnitX, 1f / 60f, 8d, advance: true);

        Assert.InRange(Vector2.Distance(preview, committed), 0f, 0.0001f);
    }

    [Fact]
    public void CenterDeadZoneRetainsTheLastPanAngle()
    {
        var state = new CameraPanningState();
        var initial = state.Update(Vector2.UnitX, 0f, 1d, advance: true);
        var afterCenter = state.Update(Vector2.Zero, 1f / 60f, 2d, advance: true);

        Assert.Equal(initial, afterCenter);
        Assert.Equal(0f, initial.Y);
    }

    [Fact]
    public void ResetClearsTheHeldPanDirection()
    {
        var state = new CameraPanningState();
        state.Update(Vector2.UnitX, 0f, 1d, advance: true);
        state.Reset();

        Assert.Equal(Vector2.Zero, state.Update(Vector2.Zero, 1f / 60f, 2d, advance: true));
    }

    [Fact]
    public void GameplayInputUsesTheCachedFinalCameraThatRenderingComputed()
    {
        var game = (Game1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        SetPrivateField(game, "_hasGameplayCameraTopLeft", true);
        SetPrivateField(game, "_gameplayCameraTopLeft", new Vector2(123f, 234f));

        var method = typeof(Game1).GetMethod(
            "GetGameplayInputCameraTopLeft",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var cached = (Vector2)method.Invoke(game, [960, 540, 0, 0])!;
        var withDifferentMouse = (Vector2)method.Invoke(game, [780, 624, 779, 623])!;

        Assert.Equal(new Vector2(123f, 234f), cached);
        Assert.Equal(cached, withDifferentMouse);
    }

    [Fact]
    public void FinalCameraPathRoundsThenClampsPostEffectPositionsAtEveryMapEdge()
    {
        var world = CreateWorld(new WorldBounds(1024f, 768f));
        var game = (Game1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        SetPrivateField(game, "_world", world);
        SetPrivateField(game, "_builderEditorEnabled", false);
        var finalize = typeof(Game1).GetMethod(
            "FinalizeGameplayCameraTopLeft",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        var belowLeft = (Vector2)finalize.Invoke(game, [new Vector2(-10.6f, -20.6f), 960, 540, true])!;
        var aboveRight = (Vector2)finalize.Invoke(game, [new Vector2(63.6f, 227.6f), 960, 540, true])!;
        var beyondRight = (Vector2)finalize.Invoke(game, [new Vector2(900f, 900f), 960, 540, true])!;

        Assert.Equal(Vector2.Zero, belowLeft);
        Assert.Equal(new Vector2(64f, 228f), aboveRight);
        Assert.Equal(new Vector2(64f, 228f), beyondRight);

        SetPrivateField(game, "_builderEditorEnabled", true);
        var builderView = (Vector2)finalize.Invoke(game, [new Vector2(-10.6f, 500.6f), 960, 540, true])!;
        Assert.Equal(new Vector2(-11f, 501f), builderView);
    }

    [Fact]
    public void MapClampUsesViewportExtentsAndCentersMapsSmallerThanTheViewport()
    {
        var normal = new WorldBounds(1024f, 768f);
        Assert.Equal(Vector2.Zero, CameraPanningState.ClampToMap(new Vector2(-20f, -30f), 960, 540, normal));
        Assert.Equal(new Vector2(64f, 228f), CameraPanningState.ClampToMap(new Vector2(500f, 500f), 960, 540, normal));

        var small = new WorldBounds(400f, 300f);
        Assert.Equal(new Vector2(-280f, -120f), CameraPanningState.ClampToMap(Vector2.Zero, 960, 540, small));
    }

    private static void SetPrivateField(Game1 game, string name, object value)
    {
        typeof(Game1).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(game, value);
    }

    private static SimulationWorld CreateWorld(WorldBounds bounds)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        var level = new SimpleLevel(
            "camera_panning_test",
            GameModeKind.TeamDeathmatch,
            bounds,
            1f,
            null,
            1,
            1,
            new SpawnPoint(bounds.Width * 0.5f, bounds.Height * 0.5f),
            [new SpawnPoint(100f, bounds.Height - 92f)],
            [new SpawnPoint(bounds.Width - 100f, bounds.Height - 92f)],
            [],
            [],
            bounds.Height - 92f,
            [],
            false);
        typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(world, [level]);
        world.TeleportLocalPlayer(bounds.Width * 0.5f, bounds.Height * 0.5f);
        world.LocalPlayer.SetSpawnRoomState(false);
        return world;
    }
}
