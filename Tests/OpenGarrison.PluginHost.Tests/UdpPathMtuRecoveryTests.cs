using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class UdpPathMtuRecoveryTests
{
    private const string Peer = "route";

    [Fact]
    public void ConfirmedMaximumCeilingIsRevalidatedWithoutDroppingOnSuccessfulProbe()
    {
        var discovery = new UdpPathMtuDiscovery();
        long probeAt = 0;
        foreach (var expectedSize in new[] { 1200, 1240, 1280, 1320, 1360, 1400 })
        {
            var probe = CreateProbe(discovery, probeAt, expectedSize);
            var acknowledgedAt = probeAt + 1;
            AcknowledgeProbe(discovery, probe, acknowledgedAt, expectedSize);
            Assert.Equal(expectedSize, discovery.GetDatagramLimit(Peer, acknowledgedAt));
            probeAt = acknowledgedAt + (expectedSize == UdpPathMtuDiscovery.InitialDatagramBytes
                ? UdpPathMtuDiscovery.ProbeIntervalMilliseconds
                : UdpPathMtuDiscovery.RevalidationIntervalMilliseconds);
        }

        var revalidation = CreateProbe(discovery, probeAt, expectedSize: 1400);
        AcknowledgeProbe(discovery, revalidation, probeAt + 1, expectedCeiling: 1400);
        Assert.Equal(1400, discovery.GetDatagramLimit(Peer, probeAt + 1));
    }

    [Fact]
    public void FailedIntermediateAndKnownCeilingProbesFallBackWithinFiveSeconds()
    {
        var discovery = new UdpPathMtuDiscovery();
        var baseline = CreateProbe(discovery, 0, UdpPathMtuDiscovery.InitialDatagramBytes);
        AcknowledgeProbe(
            discovery,
            baseline,
            nowMilliseconds: 1,
            expectedCeiling: UdpPathMtuDiscovery.InitialDatagramBytes);

        var firstUpwardProbeAt = 1 + UdpPathMtuDiscovery.ProbeIntervalMilliseconds;
        var firstUpwardProbe = CreateProbe(discovery, firstUpwardProbeAt, expectedSize: 1240);
        AcknowledgeProbe(discovery, firstUpwardProbe, firstUpwardProbeAt + 1, expectedCeiling: 1240);
        var pathShrankAt = firstUpwardProbeAt + 1;

        // This next candidate is too large for the changed route. When its
        // timeout expires, discovery must immediately recheck the last known
        // good ceiling instead of sleeping through another probe interval.
        var largerProbeAt = pathShrankAt + UdpPathMtuDiscovery.RevalidationIntervalMilliseconds;
        _ = CreateProbe(discovery, largerProbeAt, expectedSize: 1280);
        var knownCeilingProbeAt = largerProbeAt + UdpPathMtuDiscovery.ProbeTimeoutMilliseconds;
        _ = CreateProbe(discovery, knownCeilingProbeAt, expectedSize: 1240);

        // The current ceiling also times out, so discovery immediately sends
        // a base-size confirmation. Both failures fit within the server's
        // five-second idle window for this 1200-byte-reachable route.
        var fallbackProbeAt = knownCeilingProbeAt + UdpPathMtuDiscovery.ProbeTimeoutMilliseconds;
        var fallbackProbe = CreateProbe(discovery, fallbackProbeAt, UdpPathMtuDiscovery.InitialDatagramBytes);
        Assert.Equal(4_000L, fallbackProbeAt - pathShrankAt);
        Assert.True(fallbackProbeAt - pathShrankAt < 5_000);
        Assert.Equal(UdpPathMtuDiscovery.InitialDatagramBytes, discovery.GetDatagramLimit(Peer, fallbackProbeAt));
        AcknowledgeProbe(
            discovery,
            fallbackProbe,
            fallbackProbeAt + 1,
            UdpPathMtuDiscovery.InitialDatagramBytes);
        Assert.Equal(UdpPathMtuDiscovery.InitialDatagramBytes, discovery.GetDatagramLimit(Peer, fallbackProbeAt + 1));
    }

    [Fact]
    public void FailedLargerCandidatePreservesIntermediateCeilingWhenItStillAnswers()
    {
        var discovery = new UdpPathMtuDiscovery();
        var baseline = CreateProbe(discovery, 0, UdpPathMtuDiscovery.InitialDatagramBytes);
        AcknowledgeProbe(discovery, baseline, nowMilliseconds: 1, expectedCeiling: UdpPathMtuDiscovery.InitialDatagramBytes);

        var firstUpwardProbeAt = 1 + UdpPathMtuDiscovery.ProbeIntervalMilliseconds;
        var firstUpwardProbe = CreateProbe(discovery, firstUpwardProbeAt, expectedSize: 1240);
        AcknowledgeProbe(discovery, firstUpwardProbe, firstUpwardProbeAt + 1, expectedCeiling: 1240);

        var largerProbeAt = firstUpwardProbeAt + 1 + UdpPathMtuDiscovery.RevalidationIntervalMilliseconds;
        _ = CreateProbe(discovery, largerProbeAt, expectedSize: 1280);
        var knownCeilingProbeAt = largerProbeAt + UdpPathMtuDiscovery.ProbeTimeoutMilliseconds;
        var knownCeilingProbe = CreateProbe(discovery, knownCeilingProbeAt, expectedSize: 1240);
        AcknowledgeProbe(discovery, knownCeilingProbe, knownCeilingProbeAt + 1, expectedCeiling: 1240);
        Assert.Equal(1240, discovery.GetDatagramLimit(Peer, knownCeilingProbeAt + 1));
    }

    private static UdpFragmentation.DecodedFrame CreateProbe(
        UdpPathMtuDiscovery discovery,
        long nowMilliseconds,
        int expectedSize)
    {
        Assert.True(discovery.TryCreateProbe(Peer, nowMilliseconds, out var packet));
        Assert.NotNull(packet);
        Assert.True(UdpFragmentation.TryDecode(packet!, out var frame));
        Assert.True(frame.IsMtuProbe);
        Assert.Equal(expectedSize, frame.TotalLength);
        return frame;
    }

    private static void AcknowledgeProbe(
        UdpPathMtuDiscovery discovery,
        UdpFragmentation.DecodedFrame probe,
        long nowMilliseconds,
        int expectedCeiling)
    {
        var acknowledgementBytes = UdpFragmentation.CreateMtuAck(probe.MessageId, probe.TotalLength);
        Assert.True(UdpFragmentation.TryDecode(acknowledgementBytes, out var acknowledgement));
        Assert.True(discovery.TryAcceptAck(Peer, acknowledgement, nowMilliseconds, out var confirmedCeiling));
        Assert.Equal(expectedCeiling, confirmedCeiling);
    }
}
