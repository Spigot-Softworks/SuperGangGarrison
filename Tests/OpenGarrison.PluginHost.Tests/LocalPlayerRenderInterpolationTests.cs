using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LocalPlayerRenderInterpolationTests
{
    private const double TickSeconds = 1d / 30d;
    private const double FrameSeconds = 1d / 60d;
    private const float PixelsPerTick = 4f;

    [Fact]
    public void AlphaIsTheElapsedFractionOfTheTickInterval()
    {
        Assert.Equal(0f, LocalPlayerRenderInterpolation.ComputeAlpha(0d, TickSeconds));
        Assert.Equal(0.5f, LocalPlayerRenderInterpolation.ComputeAlpha(TickSeconds / 2d, TickSeconds), 5);
        Assert.Equal(1f, LocalPlayerRenderInterpolation.ComputeAlpha(TickSeconds * 3d, TickSeconds));
        Assert.Equal(0f, LocalPlayerRenderInterpolation.ComputeAlpha(-1d, TickSeconds));
        Assert.Equal(1f, LocalPlayerRenderInterpolation.ComputeAlpha(0.01d, 0d));
    }

    [Fact]
    public void InterpolationBlendsWithinATickAndSnapsTeleports()
    {
        Assert.Equal(
            new Vector2(15f, 6f),
            LocalPlayerRenderInterpolation.Interpolate(Vector2.Zero, new Vector2(30f, 12f), 0.5f));

        var teleportTarget = new Vector2(LocalPlayerRenderInterpolation.TeleportSnapDistance + 1f, 0f);
        Assert.Equal(
            teleportTarget,
            LocalPlayerRenderInterpolation.Interpolate(Vector2.Zero, teleportTarget, 0.5f));
    }

    [Fact]
    public void ThirtyHertzTicksAdvanceTheDrawnPlayerEvenlyAtSixtyFramesPerSecond()
    {
        // Mirrors the client loop: the tick accumulator advances by the frame
        // time, consumes whole ticks, then the frame draws with the remainder.
        var accumulator = 0d;
        var tickCount = 0;
        var tickStart = Vector2.Zero;
        var tickEnd = Vector2.Zero;
        float? previousDrawnX = null;
        var frameSteps = new List<float>();

        for (var frame = 0; frame < 24; frame += 1)
        {
            accumulator += FrameSeconds;
            while (accumulator >= TickSeconds - 1e-9)
            {
                accumulator -= TickSeconds;
                tickCount += 1;
                tickStart = tickEnd;
                tickEnd = new Vector2(tickCount * PixelsPerTick, 0f);
            }

            var alpha = LocalPlayerRenderInterpolation.ComputeAlpha(accumulator, TickSeconds);
            var drawnX = LocalPlayerRenderInterpolation.Interpolate(tickStart, tickEnd, alpha).X;
            if (previousDrawnX.HasValue && frame > 2)
            {
                frameSteps.Add(drawnX - previousDrawnX.Value);
            }

            previousDrawnX = drawnX;
        }

        // Drawing the raw tick sample would alternate 4px / 0px here.
        Assert.All(frameSteps, step => Assert.Equal(PixelsPerTick / 2f, step, 3));
    }

    [Fact]
    public void AdvancingOneTickWithoutMispredictionAddsNoCorrection()
    {
        var delta = LocalPlayerRenderInterpolation.ComputeContinuityOffsetDelta(
            oldTickStart: new Vector2(0f, 0f),
            oldTickEnd: new Vector2(4f, 0f),
            newTickStart: new Vector2(4f, 0f),
            newTickEnd: new Vector2(8f, 0f),
            alpha: 0.02f,
            advancedOneTick: true);

        Assert.Equal(Vector2.Zero, delta);
    }

    [Fact]
    public void AdvancingOneTickCarriesOnlyTheMisprediction()
    {
        var delta = LocalPlayerRenderInterpolation.ComputeContinuityOffsetDelta(
            oldTickStart: new Vector2(0f, 0f),
            oldTickEnd: new Vector2(4f, 0f),
            newTickStart: new Vector2(3f, 1f),
            newTickEnd: new Vector2(7f, 1f),
            alpha: 0.02f,
            advancedOneTick: true);

        Assert.Equal(new Vector2(1f, -1f), delta);
    }

    [Fact]
    public void ReconciliationKeepsTheDrawnPositionContinuous()
    {
        var oldStart = new Vector2(0f, 0f);
        var oldEnd = new Vector2(4f, 0f);
        var newStart = new Vector2(1f, 2f);
        var newEnd = new Vector2(6f, 2f);
        const float alpha = 0.4f;

        var delta = LocalPlayerRenderInterpolation.ComputeContinuityOffsetDelta(
            oldStart, oldEnd, newStart, newEnd, alpha, advancedOneTick: false);

        var before = LocalPlayerRenderInterpolation.Interpolate(oldStart, oldEnd, alpha);
        var after = LocalPlayerRenderInterpolation.Interpolate(newStart, newEnd, alpha) + delta;
        Assert.Equal(before.X, after.X, 4);
        Assert.Equal(before.Y, after.Y, 4);
    }

    [Fact]
    public void OfflineControllerBlendsTheLocalPlayerAcrossTheLatestTick()
    {
        var controller = new OfflinePresentationController();
        controller.CaptureLocalPlayerTickStart(playerId: 3, new Vector2(10f, 20f), deathCount: 0, isAlive: true);

        Assert.Equal(
            new Vector2(12f, 20f),
            controller.GetLocalPlayerRenderPosition(3, new Vector2(14f, 20f), deathCount: 0, interpolationAlpha: 0.5f));
    }

    [Fact]
    public void OfflineControllerDrawsTheTickSampleAfterRespawnOrPlayerChange()
    {
        var controller = new OfflinePresentationController();
        controller.CaptureLocalPlayerTickStart(playerId: 3, new Vector2(10f, 20f), deathCount: 0, isAlive: true);
        var tickEnd = new Vector2(14f, 20f);

        Assert.Equal(tickEnd, controller.GetLocalPlayerRenderPosition(3, tickEnd, deathCount: 1, interpolationAlpha: 0.5f));
        Assert.Equal(tickEnd, controller.GetLocalPlayerRenderPosition(4, tickEnd, deathCount: 0, interpolationAlpha: 0.5f));

        controller.CaptureLocalPlayerTickStart(playerId: 3, new Vector2(10f, 20f), deathCount: 0, isAlive: false);
        Assert.Equal(tickEnd, controller.GetLocalPlayerRenderPosition(3, tickEnd, deathCount: 0, interpolationAlpha: 0.5f));
    }

    [Fact]
    public void OfflineSessionChangeDiscardsTheCapturedTickStart()
    {
        var controller = new OfflinePresentationController();
        var world = new object();
        var level = new object();
        controller.ObserveFrame(false, world, level, 3, isMatchEnded: false);
        controller.CaptureLocalPlayerTickStart(playerId: 3, new Vector2(10f, 20f), deathCount: 0, isAlive: true);

        controller.ObserveFrame(false, world, new object(), 3, isMatchEnded: false);

        var tickEnd = new Vector2(14f, 20f);
        Assert.Equal(tickEnd, controller.GetLocalPlayerRenderPosition(3, tickEnd, deathCount: 0, interpolationAlpha: 0.5f));
    }
}
