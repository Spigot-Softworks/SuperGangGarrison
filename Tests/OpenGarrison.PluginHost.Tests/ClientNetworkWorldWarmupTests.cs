using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientNetworkWorldWarmupTests
{
    [Fact]
    public void WarmupStaysHiddenWhileInterpolationHistoriesAreStillSeeding()
    {
        var shouldRelease = Game1.ShouldReleaseNetworkWorldWarmup(
            hasAuthoritativeLocalPlayer: true,
            fullSnapshotApplied: true,
            appliedSnapshotsAfterFull: 4,
            hasFreshRemotePlayerHistories: true,
            hasQueuedAuthoritativeSnapshots: false,
            interpolationWarmupActive: true);

        Assert.False(shouldRelease);
    }

    [Fact]
    public void WarmupReleasesOnlyWhenAllPresentationReadinessChecksPass()
    {
        Assert.True(Game1.ShouldReleaseNetworkWorldWarmup(
            hasAuthoritativeLocalPlayer: true,
            fullSnapshotApplied: true,
            appliedSnapshotsAfterFull: 4,
            hasFreshRemotePlayerHistories: true,
            hasQueuedAuthoritativeSnapshots: false,
            interpolationWarmupActive: false));

        Assert.False(Game1.ShouldReleaseNetworkWorldWarmup(
            hasAuthoritativeLocalPlayer: true,
            fullSnapshotApplied: true,
            appliedSnapshotsAfterFull: 1,
            hasFreshRemotePlayerHistories: true,
            hasQueuedAuthoritativeSnapshots: false,
            interpolationWarmupActive: false));
        Assert.False(Game1.ShouldReleaseNetworkWorldWarmup(
            hasAuthoritativeLocalPlayer: true,
            fullSnapshotApplied: true,
            appliedSnapshotsAfterFull: 4,
            hasFreshRemotePlayerHistories: true,
            hasQueuedAuthoritativeSnapshots: true,
            interpolationWarmupActive: false));
    }
}
