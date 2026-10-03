using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayAudioEchoRegressionTests
{
    [Fact]
    public void FailedPredictedGibPlaybackDoesNotSuppressTheAuthoritativeCue()
    {
        var tracker = new RecentGibSoundEchoTracker();
        var predicted = new WorldSoundEvent("Gibbing", 40f, 80f);
        var authoritative = new WorldSoundEvent("Gibbing", 40f, 80f, EventId: 17);

        tracker.RecordPlayback(predicted, playbackSucceeded: false);

        Assert.False(tracker.ShouldSuppress(authoritative));
    }

    [Fact]
    public void SuccessfulPredictedGibPlaybackStillSuppressesItsNetworkEcho()
    {
        var tracker = new RecentGibSoundEchoTracker();
        var predicted = new WorldSoundEvent("Gibbing", 40f, 80f);
        var authoritative = new WorldSoundEvent("Gibbing", 40f, 80f, EventId: 17);

        tracker.RecordPlayback(predicted, playbackSucceeded: true);

        Assert.True(tracker.ShouldSuppress(authoritative));
    }

    [Fact]
    public void DeferredNetworkGibPlaybackKeepsItsEventIdentityAndRecordsOnlyAfterReplaySucceeds()
    {
        var tracker = new RecentGibSoundEchoTracker();
        var authoritative = new WorldSoundEvent("Gibbing", 40f, 80f, EventId: 17, SourceFrame: 9, SourcePlayerId: 3);
        var pending = new Game1.PendingBrowserSoundEvent(authoritative, ticksRemaining: 4);
        var predicted = new WorldSoundEvent("Gibbing", 40f, 80f);

        Assert.Equal(authoritative, pending.SoundEvent);
        tracker.RecordPlayback(pending.SoundEvent, playbackSucceeded: false);
        Assert.False(tracker.ShouldSuppress(predicted));

        tracker.RecordPlayback(pending.SoundEvent, playbackSucceeded: true);
        Assert.True(tracker.ShouldSuppress(predicted));
    }

    [Fact]
    public void SuccessfulQueuedGibReplaySuppressesAnOppositeQueuedEcho()
    {
        var tracker = new RecentGibSoundEchoTracker();
        var networkPending = new Game1.PendingBrowserSoundEvent(
            new WorldSoundEvent("Gibbing", 40f, 80f, EventId: 17),
            ticksRemaining: 4);
        var predictedPending = new Game1.PendingBrowserSoundEvent(
            new WorldSoundEvent("Gibbing", 40f, 80f),
            ticksRemaining: 4);

        tracker.RecordPlayback(networkPending.SoundEvent, playbackSucceeded: true);

        Assert.True(tracker.ShouldSuppress(predictedPending.SoundEvent));
    }

    [Fact]
    public void CaptureCompletionChimeUsesGlobalMixWhileCaptureAndGibCuesStaySpatial()
    {
        var game = (Game1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Game1));

        Assert.Equal((1f, 0f), game.GetWorldSoundMix(new WorldSoundEvent("IntelPutSnd", 4000f, -3000f)));
        Assert.False(Game1.UsesGlobalWorldSoundMix("CPCapturedSnd"));
        Assert.False(Game1.UsesGlobalWorldSoundMix("Gibbing"));
    }
}
