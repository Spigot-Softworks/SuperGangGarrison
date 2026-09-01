using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HudWeaponPresentationTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void StowedPrimaryPanelAppearsBesideAnEquippedSecondary(
        bool showOnlyActiveWeapon,
        bool hasStowedPrimaryWeapon,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldShowStowedPrimaryWeaponHud(showOnlyActiveWeapon, hasStowedPrimaryWeapon));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void SecondaryPanelRepresentsOnlyARealSecondaryWeapon(
        bool showOnlyActiveWeapon,
        bool secondaryWeaponAvailable,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldShowSecondaryWeaponHud(
                showOnlyActiveWeapon,
                secondaryWeaponAvailable));
    }
}
