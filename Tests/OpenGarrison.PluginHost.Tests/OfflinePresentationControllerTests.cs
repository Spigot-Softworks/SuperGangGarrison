using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class OfflinePresentationControllerTests
{
    [Fact]
    public void OfflinePresentationStateResetsOnlyOnEntryOrWorldTransition()
    {
        var controller = new OfflinePresentationController();
        var world = new object();
        var level = new object();

        Assert.True(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
        Assert.False(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
        Assert.False(controller.ObserveFrame(true, world, level, 1, isMatchEnded: false));
        Assert.True(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
        Assert.False(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
        Assert.True(controller.ObserveFrame(false, world, new object(), 1, isMatchEnded: false));
    }

    [Fact]
    public void OfflineRoundRestartRequestsOnePresentationHistoryReset()
    {
        var controller = new OfflinePresentationController();
        var world = new object();
        var level = new object();

        Assert.True(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
        Assert.False(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
        Assert.True(controller.ObserveFrame(false, world, level, 1, isMatchEnded: true));
        Assert.True(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
        Assert.False(controller.ObserveFrame(false, world, level, 1, isMatchEnded: false));
    }

    [Fact]
    public void PlayerDeathCountChangeMarksNewLifeForImmediatePositionSeed()
    {
        var controller = new OfflinePresentationController();

        Assert.False(controller.HasPlayerRespawned(playerId: 9, deathCount: 0));
        Assert.False(controller.HasPlayerRespawned(playerId: 9, deathCount: 0));
        Assert.True(controller.HasPlayerRespawned(playerId: 9, deathCount: 1));
        Assert.False(controller.HasPlayerRespawned(playerId: 9, deathCount: 1));
    }

    [Fact]
    public void OfflineLocalPlayerSkipsSmoothingWhileRemotePlayersCanInterpolate()
    {
        Assert.False(OfflinePresentationController.ShouldInterpolatePlayer(isLocalPlayer: true));
        Assert.True(OfflinePresentationController.ShouldInterpolatePlayer(isLocalPlayer: false));
    }

    [Fact]
    public void StoppedPlayerTeleportBeyondSmallStepSnapsInsteadOfLeavingATrail()
    {
        Assert.True(OfflinePresentationController.ShouldSnapStoppedPlayer(
            new Vector2(10f, 20f),
            new Vector2(70f, 20f),
            Vector2.Zero));
        Assert.False(OfflinePresentationController.ShouldSnapStoppedPlayer(
            new Vector2(10f, 20f),
            new Vector2(30f, 20f),
            Vector2.Zero));
        Assert.False(OfflinePresentationController.ShouldSnapStoppedPlayer(
            new Vector2(10f, 20f),
            new Vector2(70f, 20f),
            new Vector2(1f, 0f)));
    }

    [Fact]
    public void OfflineTrackMovesAcrossRenderFramesWithinOneSimulationTick()
    {
        var start = new Vector2(0f, 0f);
        var target = new Vector2(30f, 12f);
        const double startTimeSeconds = 4d;
        const float fixedDeltaSeconds = 1f / 30f;

        Assert.Equal(start, OfflinePresentationController.EvaluateTrack(
            start, target, startTimeSeconds, fixedDeltaSeconds, startTimeSeconds));
        Assert.Equal(new Vector2(15f, 6f), OfflinePresentationController.EvaluateTrack(
            start, target, startTimeSeconds, fixedDeltaSeconds, startTimeSeconds + (fixedDeltaSeconds / 2d)));
        Assert.Equal(target, OfflinePresentationController.EvaluateTrack(
            start, target, startTimeSeconds, fixedDeltaSeconds, startTimeSeconds + fixedDeltaSeconds));
        Assert.Equal(target, OfflinePresentationController.EvaluateTrack(
            start, target, startTimeSeconds, fixedDeltaSeconds, startTimeSeconds + fixedDeltaSeconds * 2d));
    }
}
