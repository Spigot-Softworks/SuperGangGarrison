using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HealthHudTests
{
    [Fact]
    public void SoldierAt144Of160ClipsTheTopOfTheMedicineCross()
    {
        var source = Game1.GetLocalHealthMedicineSourceRectangle(
            frameWidth: 24,
            frameHeight: 24,
            opaqueBounds: new Rectangle(0, 2, 24, 20),
            health: 144,
            maxHealth: 160);

        Assert.Equal(new Rectangle(0, 4, 24, 18), source);
    }

    [Fact]
    public void FullHealthUsesTheEntireOpaqueMedicineCross()
    {
        var source = Game1.GetLocalHealthMedicineSourceRectangle(
            frameWidth: 24,
            frameHeight: 24,
            opaqueBounds: new Rectangle(0, 2, 24, 20),
            health: 160,
            maxHealth: 160);

        Assert.Equal(new Rectangle(0, 2, 24, 20), source);
    }
}
