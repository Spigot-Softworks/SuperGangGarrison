using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SniperChargeHudTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(PlayerEntity.SniperChargeMaxTicks / 2, Game1.SniperChargeHudFillMaxWidth / 2)]
    [InlineData(PlayerEntity.SniperChargeMaxTicks, Game1.SniperChargeHudFillMaxWidth)]
    [InlineData(PlayerEntity.SniperChargeMaxTicks * 2, Game1.SniperChargeHudFillMaxWidth)]
    public void SniperChargeTicksMapLinearlyToHudFillWidth(int chargeTicks, int expectedWidth)
    {
        Assert.Equal(expectedWidth, Game1.GetSniperChargeHudFillWidthForTicks(chargeTicks));
    }

    [Fact]
    public void LastToDieEffectiveChargeMaximaFillBothSniperMeters()
    {
        Assert.Equal(
            Game1.SniperChargeHudFillMaxWidth,
            Game1.GetSniperChargeHudFillWidthForTicks(45, 45));
        Assert.Equal(
            Game1.SniperChargeHudFillMaxWidth,
            Game1.GetSniperBowChargeHudFillWidthForTicks(15, 15));
    }
}
