using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class WhippingCordPresentationTrackTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(144)]
    public void LatchReleaseBackswingAdvancesAtRenderRateAndRepeatedFalseSnapshotsDoNotRestart(int framesPerSecond)
    {
        var track = new WhippingCordPresentationTrack();
        Assert.Equal(
            WhippingCordPresentationPhase.Latched,
            Update(track, isLatched: true, elapsedSeconds: 0f));
        Assert.Equal(
            WhippingCordPresentationPhase.ReleaseBackswing,
            Update(track, isLatched: false, elapsedSeconds: 0f));

        var halfDurationFrames = framesPerSecond / 10;
        for (var frame = 0; frame < halfDurationFrames; frame++)
        {
            _ = Update(track, isLatched: false, elapsedSeconds: 1f / framesPerSecond);
        }

        Assert.Equal(WhippingCordPresentationPhase.ReleaseBackswing, track.Phase);
        Assert.InRange(track.ReleaseProgress, 0.45f, 0.55f);

        var remainingFrames = (int)Math.Ceiling(framesPerSecond / 10d);
        for (var frame = 0; frame < remainingFrames; frame++)
        {
            _ = Update(track, isLatched: false, elapsedSeconds: 1f / framesPerSecond);
        }

        Assert.Equal(WhippingCordPresentationPhase.Released, track.Phase);
        Assert.Equal(1f, track.ReleaseProgress);
        Assert.Equal(
            WhippingCordPresentationPhase.Released,
            Update(track, isLatched: false, elapsedSeconds: 1f / framesPerSecond));
    }

    [Fact]
    public void CooldownSamplesAdvanceOneSwingMonotonicallyAndOnlyARealFireEdgeStartsAnother()
    {
        var track = new WhippingCordPresentationTrack();
        Assert.Equal(
            WhippingCordPresentationPhase.SwingRecoil,
            Update(track, cooldownTicks: 18, attackStarted: true, elapsedSeconds: 0f));

        _ = Update(track, cooldownTicks: 18, elapsedSeconds: 0.1f);
        var progressBeforeSparseCorrection = track.RecoilProgress;
        Assert.InRange(progressBeforeSparseCorrection, 0.25f, 0.4f);

        _ = Update(track, cooldownTicks: 12, elapsedSeconds: 0f);
        var progressAfterServerSample = track.RecoilProgress;
        Assert.True(progressAfterServerSample > progressBeforeSparseCorrection);

        // A repeated or older cooldown sample cannot rewind or restart a completed swing.
        for (var frame = 0; frame < 12; frame++)
        {
            _ = Update(track, cooldownTicks: 18, elapsedSeconds: 1f / 60f);
        }

        Assert.Equal(WhippingCordPresentationPhase.SwingComplete, track.Phase);
        Assert.Equal(1f, track.RecoilProgress);
        Assert.Equal(
            WhippingCordPresentationPhase.SwingComplete,
            Update(track, cooldownTicks: 0, elapsedSeconds: 1f / 60f));
        Assert.Equal(
            WhippingCordPresentationPhase.SwingRecoil,
            Update(track, cooldownTicks: 18, attackStarted: true, elapsedSeconds: 0f));
        Assert.Equal(0f, track.RecoilProgress);
    }

    [Fact]
    public void WeaponSwitchAndDeathClearLatchedAndReleasePresentation()
    {
        var track = new WhippingCordPresentationTrack();
        _ = Update(track, isLatched: true, elapsedSeconds: 0f);
        _ = Update(track, isLatched: false, elapsedSeconds: 0f);
        Assert.Equal(WhippingCordPresentationPhase.ReleaseBackswing, track.Phase);

        Assert.Equal(
            WhippingCordPresentationPhase.Normal,
            Update(track, hasWhip: false, elapsedSeconds: 0f));
        _ = Update(track, isLatched: true, elapsedSeconds: 0f);
        Assert.Equal(
            WhippingCordPresentationPhase.Normal,
            Update(track, isAlive: false, elapsedSeconds: 0f));
    }

    [Fact]
    public void LatchReleaseAndLaterSwingFinishWithoutRevivingAnOlderBackswing()
    {
        var track = new WhippingCordPresentationTrack();
        _ = Update(track, isLatched: true, elapsedSeconds: 0f);
        _ = Update(track, isLatched: false, elapsedSeconds: 0f);
        for (var frame = 0; frame < 12; frame++)
        {
            _ = Update(track, isLatched: false, elapsedSeconds: 1f / 60f);
        }

        Assert.Equal(WhippingCordPresentationPhase.Released, track.Phase);
        Assert.Equal(
            WhippingCordPresentationPhase.SwingRecoil,
            Update(track, cooldownTicks: 18, attackStarted: true, elapsedSeconds: 0f));
        for (var frame = 0; frame < 18; frame++)
        {
            _ = Update(track, cooldownTicks: 18, elapsedSeconds: 1f / 60f);
        }

        Assert.Equal(WhippingCordPresentationPhase.SwingComplete, track.Phase);
        Assert.Equal(
            WhippingCordPresentationPhase.SwingComplete,
            Update(track, cooldownTicks: 18, elapsedSeconds: 1f / 60f));
    }

    private static WhippingCordPresentationPhase Update(
        WhippingCordPresentationTrack track,
        bool hasWhip = true,
        bool isAlive = true,
        bool isLatched = false,
        int cooldownTicks = 0,
        bool attackStarted = false,
        float elapsedSeconds = 0f)
    {
        return track.Update(
            hasWhippingCordEquipped: hasWhip,
            isAlive,
            isLatched,
            currentCooldownTicks: cooldownTicks,
            maximumCooldownTicks: 18,
            attackStarted,
            elapsedSeconds,
            recoilTicks: 9,
            backswingTicks: 6,
            ticksPerSecond: 30);
    }
}
