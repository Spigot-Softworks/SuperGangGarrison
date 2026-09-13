using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieStatsDocumentTests
{
    [Fact]
    public void AuthoritativeAttemptsUpdatePersonalRecordsExactlyOnce()
    {
        var stats = new LastToDieStatsDocument();
        var firstAttempt = Guid.NewGuid();
        var secondAttempt = Guid.NewGuid();

        Assert.True(stats.RecordRun(1_250, 4, firstAttempt));
        Assert.False(stats.RecordRun(1_250, 4, firstAttempt));
        Assert.True(stats.RecordRun(900, 7, secondAttempt));

        Assert.Equal(2, stats.RunsPlayed);
        Assert.Equal(1_250, stats.BestScoreUnits);
        Assert.Equal(7, stats.HighestRoundCompleted);
        Assert.Equal(900, stats.LastRunScoreUnits);
        Assert.Equal(7, stats.LastRunRound);
    }
}
