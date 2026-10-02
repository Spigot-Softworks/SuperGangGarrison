using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PresentationCadenceTests
{
    private const double Step = 1d / 60d;

    [Fact]
    public void NearExactFrameTimesSnapToWholeVisualSteps()
    {
        Assert.Equal(Step, ClientTickCadence.SnapFrameSeconds(Step + 0.0007d, Step));
        Assert.Equal(Step, ClientTickCadence.SnapFrameSeconds(Step - 0.0012d, Step));
        Assert.Equal(2d * Step, ClientTickCadence.SnapFrameSeconds((2d * Step) + 0.001d, Step));

        // 144 Hz and 75 Hz frames are left alone.
        Assert.Equal(1d / 144d, ClientTickCadence.SnapFrameSeconds(1d / 144d, Step));
        Assert.Equal(1d / 75d, ClientTickCadence.SnapFrameSeconds(1d / 75d, Step));
    }

    [Fact]
    public void SixtyHertzFramesAdvanceExactlyOneVisualStepRegardlessOfPhase()
    {
        var random = new Random(9);
        // Worst case: accumulator phase sitting right at a step boundary.
        var accumulator = Step - 0.0002d;
        var previousTimestamp = 0d;
        for (var frame = 1; frame < 2000; frame += 1)
        {
            var timestamp = (frame / 60d) + ((random.NextDouble() - 0.5d) * 0.0014d);
            accumulator += ClientTickCadence.SnapFrameSeconds(timestamp - previousTimestamp, Step);
            previousTimestamp = timestamp;
            var steps = 0;
            while (accumulator >= Step)
            {
                accumulator -= Step;
                steps += 1;
            }

            Assert.Equal(1, steps);
        }
    }

    [Fact]
    public void ServerClockOffsetFilterSmoothsJitterAndResetsOnLargeJumps()
    {
        Assert.Equal(0.25d, NetworkInterpolationPolicy.FilterServerClockOffset(false, 0d, 0.25d));

        var filtered = NetworkInterpolationPolicy.FilterServerClockOffset(true, 1.000d, 1.016d);
        Assert.Equal(1.0008d, filtered, 6);

        Assert.Equal(1.5d, NetworkInterpolationPolicy.FilterServerClockOffset(true, 1.0d, 1.5d));
    }

    [Fact]
    public void FilteredServerTimeAdvancesWithTheLocalClockBetweenSnapshots()
    {
        const double offset = 10d;
        const double latestServer = 100.5d;
        const double headroom = 0.08d;
        double? previous = null;
        for (var frame = 0; frame < 4; frame += 1)
        {
            var clock = 90.5d + (frame * Step);
            var anchored = latestServer + Math.Min(frame * Step, headroom);
            var estimate = NetworkInterpolationPolicy.EstimateServerTimeSeconds(clock, offset, latestServer, anchored, headroom);
            if (previous.HasValue)
            {
                Assert.Equal(Step, estimate - previous.Value, 9);
            }

            previous = estimate;
        }

        // It never runs past the newest snapshot's headroom when snapshots stop.
        Assert.Equal(
            latestServer + headroom,
            NetworkInterpolationPolicy.EstimateServerTimeSeconds(95d, offset, latestServer, latestServer + headroom, headroom));
    }

    [Fact]
    public void OfflineBotsBlendAcrossTheLatestTickAndSnapStoppedTeleports()
    {
        var controller = new OfflinePresentationController();
        controller.BeginPlayerTickCapture();
        controller.CapturePlayerTickStart(playerKey: 1007, new Vector2(10f, 20f), deathCount: 2, isAlive: true);

        Assert.Equal(
            new Vector2(14f, 20f),
            controller.GetPlayerRenderPosition(1007, new Vector2(18f, 20f), deathCount: 2, new Vector2(8f, 0f), interpolationAlpha: 0.5f));

        // A stopped player that moved 60 px in one tick teleported: draw it at the destination.
        Assert.Equal(
            new Vector2(70f, 20f),
            controller.GetPlayerRenderPosition(1007, new Vector2(70f, 20f), deathCount: 2, Vector2.Zero, interpolationAlpha: 0.5f));

        controller.BeginPlayerTickCapture();
        Assert.Equal(
            new Vector2(18f, 20f),
            controller.GetPlayerRenderPosition(1007, new Vector2(18f, 20f), deathCount: 2, new Vector2(8f, 0f), interpolationAlpha: 0.5f));
    }
}
