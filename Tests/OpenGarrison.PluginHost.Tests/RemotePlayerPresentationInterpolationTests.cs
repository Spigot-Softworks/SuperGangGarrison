using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class RemotePlayerPresentationInterpolationTests
{
    [Fact]
    public void RespawnResetsToOneImmediatelyRenderableAuthoritativeSample()
    {
        var dead = Sample(Vector2.Zero, timeSeconds: 1.0, isAlive: false, isGrounded: true);
        var respawned = Sample(new Vector2(320f, 180f), timeSeconds: 1.05, isAlive: true, isGrounded: true);
        var history = new List<Game1.PlayerSnapshotSample> { dead };

        Assert.True(Game1.ShouldResetRemotePlayerSnapshotHistory(respawned, history));
        Assert.True(Game1.IsNetworkPlayerPresentationHistoryRenderable(1, 1.05, 1.05, 1.0));
    }

    [Fact]
    public void TeleportResetsToOneImmediatelyRenderableAuthoritativeSample()
    {
        var before = Sample(Vector2.Zero, timeSeconds: 2.0, isAlive: true, isGrounded: true);
        var after = Sample(new Vector2(256f, 0f), timeSeconds: 2.05, isAlive: true, isGrounded: true);
        var history = new List<Game1.PlayerSnapshotSample> { before };

        Assert.True(Game1.ShouldResetRemotePlayerSnapshotHistory(after, history));
        Assert.True(Game1.IsNetworkPlayerPresentationHistoryRenderable(1, 2.05, 2.05, 1.0));
    }

    [Fact]
    public void ExpectedMovementAcrossPacketLossDoesNotLookLikeATeleport()
    {
        var velocity = new Vector2(1000f, 0f);
        var before = Sample(Vector2.Zero, velocity, timeSeconds: 3.0, isAlive: true, isGrounded: true);
        var after = Sample(new Vector2(200f, 0f), velocity, timeSeconds: 3.2, isAlive: true, isGrounded: true);
        var history = new List<Game1.PlayerSnapshotSample> { before };

        Assert.False(Game1.ShouldResetRemotePlayerSnapshotHistory(after, history));
    }

    [Fact]
    public void FullRecoveryBaselineKeepsContinuousPlayerHistory()
    {
        var before = Sample(new Vector2(100f, 200f), new Vector2(120f, 0f), 4.0, isAlive: true, isGrounded: true);
        var recovered = Sample(new Vector2(106f, 200f), new Vector2(120f, 0f), 4.05, isAlive: true, isGrounded: true);
        var history = new List<Game1.PlayerSnapshotSample> { before };

        Assert.False(Game1.ShouldResetSnapshotPresentationHistories(
            isServerFullSnapshot: true,
            presentationEpochChanged: false));
        Assert.False(Game1.ShouldResetRemotePlayerSnapshotHistory(recovered, history));
    }

    [Fact]
    public void EigerGroundedInterpolationNeverPresentsPlayerInsideSlope()
    {
        using var contentRoot = CoreContentRootScope.Create();
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TryLoadLevel("Eiger", 1, preservePlayerStats: false));
        world.SetPendingLocalPlayerClass(PlayerClass.Scout);
        world.ForceRespawnLocalPlayer();
        var player = world.LocalPlayer;

        var older = new Vector2(4573.5f, 852f);
        var newer = new Vector2(4581.1f, 846f);
        var midpoint = Vector2.Lerp(older, newer, 0.5f);
        Assert.False(IntersectsSolid(player, world.Level, older));
        Assert.False(IntersectsSolid(player, world.Level, newer));
        Assert.True(IntersectsSolid(player, world.Level, midpoint));

        var constrained = Game1.ConstrainGroundedRemotePlayerPresentationPosition(
            player,
            world.Level,
            midpoint,
            authoritativeFallback: newer,
            constrainToGround: true);

        Assert.Equal(midpoint.X, constrained.X);
        Assert.True(constrained.Y < midpoint.Y);
        Assert.False(IntersectsSolid(player, world.Level, constrained));
    }

    private static Game1.PlayerSnapshotSample Sample(
        Vector2 position,
        double timeSeconds,
        bool isAlive,
        bool isGrounded)
        => Sample(position, Vector2.Zero, timeSeconds, isAlive, isGrounded);

    private static Game1.PlayerSnapshotSample Sample(
        Vector2 position,
        Vector2 velocity,
        double timeSeconds,
        bool isAlive,
        bool isGrounded)
        => new(
            position,
            velocity,
            position + new Vector2(100f, 0f),
            timeSeconds,
            PlayerTeam.Red,
            PlayerClass.Scout,
            isAlive,
            isGrounded);

    private static bool IntersectsSolid(PlayerEntity player, SimpleLevel level, Vector2 position)
    {
        player.GetCollisionBoundsAt(position.X, position.Y, out var left, out var top, out var right, out var bottom);
        return level.IntersectsSolid(left, top, right, bottom);
    }

    private sealed class CoreContentRootScope : IDisposable
    {
        private readonly string _originalContentRoot;

        private CoreContentRootScope(string originalContentRoot)
        {
            _originalContentRoot = originalContentRoot;
        }

        public static CoreContentRootScope Create()
        {
            var originalContentRoot = ContentRoot.Path;
            var coreContent = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"));
            Assert.False(string.IsNullOrWhiteSpace(coreContent));
            ContentRoot.Initialize(coreContent!);
            return new CoreContentRootScope(originalContentRoot);
        }

        public void Dispose()
        {
            ContentRoot.Initialize(_originalContentRoot);
        }
    }
}
