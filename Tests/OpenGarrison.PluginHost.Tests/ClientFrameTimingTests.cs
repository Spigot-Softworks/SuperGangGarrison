using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientFrameTimingTests
{
    [Fact]
    public void CompletedFrameIntervalsReportPercentilesAndStrictHitchThresholds()
    {
        var timing = new ClientFrameTimingAccumulator();
        timing.RecordInterval(16.66d);
        timing.RecordInterval(16.67d);
        timing.RecordInterval(20d);
        timing.RecordInterval(33.34d);
        timing.RecordInterval(50d);
        timing.RecordInterval(50.01d);

        var summary = timing.GetSummary();

        Assert.Equal(6, summary.SampleCount);
        Assert.Equal(20d, summary.P50Milliseconds, precision: 2);
        Assert.Equal(50.1d, summary.P95Milliseconds, precision: 2);
        Assert.Equal(50.1d, summary.P99Milliseconds, precision: 2);
        Assert.Equal(50.01d, summary.MaxMilliseconds, precision: 2);
        Assert.Equal(4, summary.Over16Point67Milliseconds);
        Assert.Equal(2, summary.Over33Point34Milliseconds);
        Assert.Equal(1, summary.Over50Milliseconds);
    }

    [Fact]
    public void ResetClearsCompletedFrameIntervalWindowAndClock()
    {
        var timing = new ClientFrameTimingAccumulator();
        timing.RecordInterval(16.7d);
        timing.Reset();
        timing.RecordInterval(16.6d);

        var summary = timing.GetSummary();

        Assert.Equal(1, summary.SampleCount);
        Assert.Equal(16.6d, summary.P50Milliseconds, precision: 2);
        Assert.Equal(0, summary.Over16Point67Milliseconds);
    }

    [Fact]
    public void PercentileUpperEdgeDoesNotOverstateAnExactGateValue()
    {
        var timing = new ClientFrameTimingAccumulator();
        timing.RecordInterval(20d);

        Assert.Equal(20d, timing.GetSummary().P95Milliseconds, precision: 2);
    }
}
