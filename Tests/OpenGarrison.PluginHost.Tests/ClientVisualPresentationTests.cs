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

    [Fact]
    public void QuoteBladeIncreaseDoesNotStartPrimaryWeaponAnimation()
    {
        var quote = new PlayerEntity(12, CharacterClassCatalog.Quote, "Quote");
        var previousBladeCount = quote.QuoteBladesOut;
        quote.IncrementQuoteBubbleCount();
        Assert.True(Game1.IsQuotePrimaryAnimationStart(
            previousBubbleCount: 0,
            currentBubbleCount: quote.QuoteBubbleCount));
        Assert.False(Game1.IsQuoteBladeSecondaryAnimationStart(
            quote.ClassId,
            previousBladeCount,
            quote.QuoteBladesOut));

        quote.IncrementQuoteBladeCount();
        Assert.False(Game1.IsQuotePrimaryAnimationStart(
            previousBubbleCount: quote.QuoteBubbleCount,
            currentBubbleCount: quote.QuoteBubbleCount));
        Assert.True(Game1.IsQuoteBladeSecondaryAnimationStart(
            quote.ClassId,
            previousBladeCount,
            quote.QuoteBladesOut));
        Assert.False(Game1.IsQuoteBladeSecondaryAnimationStart(PlayerClass.Quote, 1, 1));
        Assert.False(Game1.IsQuoteBladeSecondaryAnimationStart(PlayerClass.Scout, 0, 1));
    }

    [Fact]
    public void MedicBeamPresentationUsesAuthoritativeRange()
    {
        Assert.True(Game1.IsMedicBeamPresentationDistanceValid(300f * 300f));
        Assert.False(Game1.IsMedicBeamPresentationDistanceValid(301f * 301f));
    }

    [Fact]
    public void DispenserBeamPresentationUsesItsOwnAuraRange()
    {
        Assert.True(Game1.IsDispenserBeamPresentationDistanceValid(75f * 75f));
        Assert.False(Game1.IsDispenserBeamPresentationDistanceValid(76f * 76f));
    }
}
