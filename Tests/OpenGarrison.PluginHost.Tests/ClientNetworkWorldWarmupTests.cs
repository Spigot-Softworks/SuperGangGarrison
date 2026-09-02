using OpenGarrison.Client;
using OpenGarrison.Protocol;
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

    [Theory]
    [InlineData(LastToDieWirePhase.Lobby)]
    [InlineData(LastToDieWirePhase.SurvivorChoice)]
    [InlineData(LastToDieWirePhase.RewardChoice)]
    [InlineData(LastToDieWirePhase.LoadingStage)]
    [InlineData(LastToDieWirePhase.Won)]
    [InlineData(LastToDieWirePhase.Lost)]
    public void WarmupDoesNotHideHostedLastToDieFullScreenMenus(LastToDieWirePhase phase)
    {
        Assert.False(Game1.ShouldBlockNetworkWorldWarmupPresentation(
            gameplayWarmupBlocking: true,
            lastToDiePhase: phase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(LastToDieWirePhase.Playing)]
    public void WarmupStillHidesUnreadyGameplayWorld(LastToDieWirePhase? phase)
    {
        Assert.True(Game1.ShouldBlockNetworkWorldWarmupPresentation(
            gameplayWarmupBlocking: true,
            lastToDiePhase: phase));
    }

    [Fact]
    public void InactiveWarmupNeverBlocksPresentation()
    {
        Assert.False(Game1.ShouldBlockNetworkWorldWarmupPresentation(
            gameplayWarmupBlocking: false,
            lastToDiePhase: LastToDieWirePhase.Playing));
    }
}
