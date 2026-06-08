using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NetworkInterpolationPolicyTests
{
    [Fact]
    public void SnapshotInterpolationStaysActiveOnlineWhenSmoothingPreferenceIsDisabled()
    {
        Assert.True(NetworkInterpolationPolicy.IsSnapshotInterpolationActive(
            isConnected: true,
            positionSmoothingEnabled: false,
            isReplayConnection: false));
    }

    [Fact]
    public void OnlineLocalBackTimeUsesDeeperAuthoritativeBuffer()
    {
        var backTimeSeconds = NetworkInterpolationPolicy.CalculateLocalBackTimeSeconds(
            isReplayConnection: false,
            smoothedSnapshotIntervalSeconds: 1f / 30f,
            smoothedSnapshotJitterSeconds: 0.008f,
            minimumBackTimeSeconds: 0.050f,
            maximumBackTimeSeconds: 0.180f);

        Assert.InRange(backTimeSeconds, 0.095f, 0.180f);
    }

    [Fact]
    public void ProjectileBackTimeUsesExpectedCadenceInsteadOfHardFortyFiveMillisecondCap()
    {
        var backTimeSeconds = NetworkInterpolationPolicy.CalculateEntityBackTimeSeconds(
            networkSnapshotInterpolationDurationSeconds: 1f / 60f,
            smoothedSnapshotIntervalSeconds: 1f / 60f,
            smoothedSnapshotJitterSeconds: 0.010f,
            configuredTickRate: 60,
            expectedProjectileUpdateIntervalTicks: 3,
            minimumBackTimeSeconds: 0.050f,
            maximumBackTimeSeconds: 0.150f);

        Assert.True(backTimeSeconds > 0.045f);
        Assert.InRange(backTimeSeconds, 0.069f, 0.071f);
    }

    [Fact]
    public void OnlineRemoteBackTimeCanCoverRemoteServerJitter()
    {
        var backTimeSeconds = NetworkInterpolationPolicy.CalculateRemoteBackTimeSeconds(
            isReplayConnection: false,
            smoothedSnapshotIntervalSeconds: 0.060f,
            smoothedSnapshotJitterSeconds: 0.070f,
            minimumBackTimeSeconds: 0.050f,
            maximumBackTimeSeconds: 0.280f);

        Assert.InRange(backTimeSeconds, 0.279f, 0.281f);
    }

    [Fact]
    public void ProjectileBackTimeCanCoverSparseRemoteSnapshots()
    {
        var backTimeSeconds = NetworkInterpolationPolicy.CalculateEntityBackTimeSeconds(
            networkSnapshotInterpolationDurationSeconds: 0.075f,
            smoothedSnapshotIntervalSeconds: 0.060f,
            smoothedSnapshotJitterSeconds: 0.040f,
            configuredTickRate: 30,
            expectedProjectileUpdateIntervalTicks: 3,
            minimumBackTimeSeconds: 0.050f,
            maximumBackTimeSeconds: 0.300f);

        Assert.InRange(backTimeSeconds, 0.259f, 0.261f);
    }

    [Fact]
    public void OnlineLocalOwnedProjectilesBypassSnapshotHistory()
    {
        Assert.False(NetworkInterpolationPolicy.ShouldUseSnapshotHistoryForProjectile(
            isReplayConnection: false,
            projectileOwnerId: 42,
            authoritativeLocalPlayerId: 42));

        Assert.True(NetworkInterpolationPolicy.ShouldUseSnapshotHistoryForProjectile(
            isReplayConnection: false,
            projectileOwnerId: 43,
            authoritativeLocalPlayerId: 42));

        Assert.True(NetworkInterpolationPolicy.ShouldUseSnapshotHistoryForProjectile(
            isReplayConnection: true,
            projectileOwnerId: 42,
            authoritativeLocalPlayerId: 42));
    }

    [Fact]
    public void BurstJitterUsesTotalArrivalVsServerTime()
    {
        var jitterSeconds = NetworkInterpolationPolicy.CalculateSnapshotJitterSampleSeconds(
            arrivalIntervalSecondsTotal: 0.130f,
            observedIntervalSecondsTotal: 0.100f,
            burstCount: 3);

        Assert.InRange(jitterSeconds, 0.029f, 0.031f);
    }
}
