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
    [InlineData(false, true, true, true, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, false, true, true, true)]
    [InlineData(true, true, true, true, false)]
    [InlineData(false, true, true, false, false)]
    public void LockedAlternatePrimaryOccupiesTheOnlyActiveWeaponPanel(
        bool showOnlyActiveWeapon,
        bool lockedPrimaryWeaponClass,
        bool lockedPrimaryWeaponSelected,
        bool secondaryWeaponAvailable,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldShowSecondaryWeaponHud(
                showOnlyActiveWeapon,
                lockedPrimaryWeaponClass,
                lockedPrimaryWeaponSelected,
                secondaryWeaponAvailable));
    }
}
