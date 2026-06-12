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
}
