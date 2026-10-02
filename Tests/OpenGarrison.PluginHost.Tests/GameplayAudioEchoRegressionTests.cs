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
    public void CaptureCompletionChimeUsesGlobalMixWhileCaptureAndGibCuesStaySpatial()
    {
        var game = (Game1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Game1));

        Assert.Equal((1f, 0f), game.GetWorldSoundMix(new WorldSoundEvent("IntelPutSnd", 4000f, -3000f)));
        Assert.False(Game1.UsesGlobalWorldSoundMix("CPCapturedSnd"));
        Assert.False(Game1.UsesGlobalWorldSoundMix("Gibbing"));
    }
}
