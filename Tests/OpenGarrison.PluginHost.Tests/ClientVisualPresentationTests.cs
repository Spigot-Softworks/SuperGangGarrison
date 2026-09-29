using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientVisualPresentationTests
{
    [Fact]
    public void PredictedAirBlastSequenceIsShownOnceAcrossReplayAndAfterCueExpiry()
    {
        Assert.True(Game1.IsNewerPredictedInputSequence(10, 9));
        Assert.False(Game1.IsNewerPredictedInputSequence(10, 10));
        Assert.False(Game1.IsNewerPredictedInputSequence(9, 10));
        Assert.True(Game1.IsNewerPredictedInputSequence(1, uint.MaxValue));
    }

    [Fact]
    public void FrozenSpyObservationCannotBeConsumedTwiceWithoutNewObservation()
    {
        Assert.True(Game1.ShouldConsumeFrozenSpyObservation(8, 0));
        Assert.False(Game1.ShouldConsumeFrozenSpyObservation(8, 8));
        Assert.True(Game1.ShouldConsumeFrozenSpyObservation(9, 8));
        Assert.False(Game1.ShouldConsumeFrozenSpyObservation(0, 0));
    }



}
