using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HudWeaponPresentationTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void AlternatePrimaryDoesNotAddAStowedPrimaryPanel(
        bool showOnlyActiveWeapon,
        bool alternatePrimarySelected,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldShowStowedPrimaryWeaponHud(showOnlyActiveWeapon, alternatePrimarySelected));
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
