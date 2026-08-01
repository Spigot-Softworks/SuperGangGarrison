using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CrosshairHudTests
{
    [Fact]
    public void RechargeCrosshairUsesIdleFrameWhenWeaponTimersAreClear()
    {
        Assert.Equal(
            Game1.RechargeCrosshairIdleFrameIndex,
            Game1.GetCrosshairFrameIndex(CharacterClassCatalog.Scattergun, 0, 0));
    }

    [Fact]
    public void RechargeCrosshairStartsFullAndDrainsAsTimerAdvances()
    {
        var weapon = CharacterClassCatalog.Scattergun;

        var fullFrame = Game1.GetCrosshairFrameIndex(weapon, weapon.ReloadDelayTicks, 0);
        var nearlyReadyFrame = Game1.GetCrosshairFrameIndex(weapon, 1, 0);

        Assert.Equal(Game1.RechargeCrosshairActiveFrameOffset, fullFrame);
        Assert.Equal(
            Game1.RechargeCrosshairActiveFrameOffset + Game1.RechargeCrosshairActiveFrameCount - 1,
            nearlyReadyFrame);
    }

    [Fact]
    public void ContinuousCrosshairUsesContinuousWeaponFrames()
    {
        var weapon = CharacterClassCatalog.Minigun;

        Assert.True(Game1.IsContinuousCrosshairWeapon(weapon));
        Assert.Equal(
            Game1.ContinuousCrosshairIdleFrameIndex,
            Game1.GetCrosshairFrameIndex(weapon, 0, 0));
        Assert.Equal(
            0,
            Game1.GetCrosshairFrameIndex(weapon, weapon.ReloadDelayTicks, 0));
        Assert.Equal(
            0,
            Game1.GetContinuousCrosshairFrameIndex(1, weapon.ReloadDelayTicks));
        Assert.True(
            Game1.GetContinuousCrosshairFrameIndex(weapon.ReloadDelayTicks * 5, weapon.ReloadDelayTicks)
                > Game1.GetContinuousCrosshairFrameIndex(1, weapon.ReloadDelayTicks));
        Assert.Equal(
            Game1.ContinuousCrosshairActiveFrameCount - 1,
            Game1.GetContinuousCrosshairFrameIndex(
                weapon.ReloadDelayTicks * Game1.ContinuousCrosshairFillCycles,
                weapon.ReloadDelayTicks));
    }
}
