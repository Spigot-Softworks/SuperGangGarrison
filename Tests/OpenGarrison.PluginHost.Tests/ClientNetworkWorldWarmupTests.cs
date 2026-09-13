using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientNetworkWorldWarmupTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EscapeOrFocusLossCannotPauseBeforeWarmupCompletes(int maximumPlayers)
    {
        // A menu/focus pause request spans LoadingStage -> Playing and remains
        // present while baseline + subsequent snapshots seed interpolation.
        foreach (var phase in new[] { LastToDieWirePhase.LoadingStage, LastToDieWirePhase.Playing })
        {
            for (var applied = 0; applied <= 2; applied++)
            {
                var ready = Game1.ShouldReleaseNetworkWorldWarmup(true, true, applied, true, false, false);
                var pause = Game1.ShouldPauseHostedLastToDieSoloSimulation(true, true, maximumPlayers,
                    phase, hasOpenGameplayOverlay: true, isLoading: !ready);
                Assert.Equal(ready && maximumPlayers == 1 && phase == LastToDieWirePhase.Playing, pause);
            }
        }
    }

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

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, true)]
    public void AwaitingJoinPlayerIsNotMistakenForAuthoritativeGameplayEntity(
        bool isSpectator,
        bool hasSnapshotEntityId,
        bool isAwaitingJoin,
        bool expected)
    {
        Assert.Equal(expected, Game1.ShouldTreatLocalPlayerAsAuthoritativeForWarmup(
            isSpectator,
            hasSnapshotEntityId,
            isAwaitingJoin));
    }

    [Theory]
    [InlineData(0, 10.0, 10.0, false)]
    [InlineData(1, 10.0, 10.0, false)]
    [InlineData(2, 10.0, 10.0, true)]
    [InlineData(2, 10.0, 8.9, false)]
    [InlineData(2, 10.0, 10.1, false)]
    public void PlayerPresentationRequiresTwoFreshMonotonicSamples(
        int sampleCount,
        double latestSnapshotTime,
        double latestSampleTime,
        bool expected)
    {
        Assert.Equal(expected, Game1.IsNetworkPlayerPresentationHistoryReady(
            sampleCount,
            latestSnapshotTime,
            latestSampleTime,
            freshnessSeconds: 1.0));
    }

    [Fact]
    public void OneFreshSampleRendersAfterWarmupButCannotReleaseInitialWarmup()
    {
        Assert.True(Game1.IsNetworkPlayerPresentationHistoryRenderable(
            sampleCount: 1,
            latestSnapshotServerTimeSeconds: 10.0,
            latestHistorySampleTimeSeconds: 10.0,
            freshnessSeconds: 1.0));
        Assert.False(Game1.IsNetworkPlayerPresentationHistoryReady(
            sampleCount: 1,
            latestSnapshotServerTimeSeconds: 10.0,
            latestHistorySampleTimeSeconds: 10.0,
            freshnessSeconds: 1.0));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void OnlyPresentationEpochChangesResetSnapshotHistories(
        bool isServerFullSnapshot,
        bool presentationEpochChanged,
        bool expected)
    {
        Assert.Equal(expected, Game1.ShouldResetSnapshotPresentationHistories(
            isServerFullSnapshot,
            presentationEpochChanged));
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(SimulationWorld.FirstSpectatorSlot, false, false)]
    [InlineData(1, true, false)]
    public void ResolvedSnapshotsRefreshPlayablePlayerPresentationHistories(
        byte playerSlot,
        bool isSpectator,
        bool expected)
    {
        Assert.Equal(expected, Game1.ShouldRefreshResolvedPlayerPresentationHistory(
            playerSlot,
            isSpectator));
    }

    [Theory]
    [InlineData(LastToDieWirePhase.LoadingStage, LastToDieWirePhase.Playing, true)]
    [InlineData(LastToDieWirePhase.SurvivorChoice, LastToDieWirePhase.Playing, true)]
    [InlineData(LastToDieWirePhase.Playing, LastToDieWirePhase.Playing, false)]
    [InlineData(LastToDieWirePhase.Playing, LastToDieWirePhase.LoadingStage, false)]
    public void EnteringLastToDieGameplayStartsANewPresentationEpoch(
        LastToDieWirePhase previous,
        LastToDieWirePhase current,
        bool expected)
    {
        Assert.Equal(expected, Game1.ShouldRestartNetworkPresentationForLastToDiePhase(previous, current));
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(89999f, true)]
    [InlineData(90000f, true)]
    [InlineData(90001f, false)]
    public void MedicBeamPresentationRejectsImpossibleEndpointDistances(float distanceSquared, bool expected)
    {
        Assert.Equal(expected, Game1.IsMedicBeamPresentationDistanceValid(distanceSquared));
    }
}
