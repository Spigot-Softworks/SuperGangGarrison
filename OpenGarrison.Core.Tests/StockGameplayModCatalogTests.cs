using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class StockGameplayModCatalogTests
{
    [Theory]
    [InlineData(PlayerClass.Scout, "weapon.scattergun", "scout.stock")]
    [InlineData(PlayerClass.Engineer, "weapon.shotgun", "engineer.stock")]
    [InlineData(PlayerClass.Pyro, "weapon.flamethrower", "pyro.stock")]
    [InlineData(PlayerClass.Soldier, "weapon.rocketlauncher", "soldier.stock")]
    [InlineData(PlayerClass.Demoman, "weapon.minelauncher", "demoman.stock")]
    [InlineData(PlayerClass.Heavy, "weapon.minigun", "heavy.stock")]
    [InlineData(PlayerClass.Sniper, "weapon.rifle", "sniper.stock")]
    [InlineData(PlayerClass.Medic, "weapon.medigun", "medic.stock")]
    [InlineData(PlayerClass.Spy, "weapon.revolver", "spy.stock")]
    [InlineData(PlayerClass.Quote, "weapon.blade", "quote.stock")]
    public void DefaultLoadout_ResolvesExpectedPrimaryItem(PlayerClass playerClass, string primaryItemId, string loadoutId)
    {
        var classDefinition = StockGameplayModCatalog.GetClassDefinition(playerClass);
        var loadout = StockGameplayModCatalog.GetDefaultLoadout(playerClass);
        var primaryItem = StockGameplayModCatalog.GetPrimaryItem(playerClass);

        Assert.Equal(loadoutId, classDefinition.DefaultLoadoutId);
        Assert.Equal(primaryItemId, loadout.PrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Primary, primaryItem.Slot);
    }

    [Fact]
    public void CharacterClassCatalog_RemainsAlignedWithStockGameplayDefinitions()
    {
        var soldier = CharacterClassCatalog.GetDefinition(PlayerClass.Soldier);
        var soldierPrimary = StockGameplayModCatalog.GetPrimaryItem(PlayerClass.Soldier);
        var quote = CharacterClassCatalog.GetDefinition(PlayerClass.Quote);
        var quoteClass = StockGameplayModCatalog.GetClassDefinition(PlayerClass.Quote);

        Assert.Equal(soldierPrimary.DisplayName, soldier.PrimaryWeapon.DisplayName);
        Assert.Equal(PrimaryWeaponKind.RocketLauncher, soldier.PrimaryWeapon.Kind);
        Assert.Equal(quoteClass.Movement.CollisionBottom - quoteClass.Movement.CollisionTop, quote.Height);
        Assert.Equal(quoteClass.Movement.MaxHealth, quote.MaxHealth);
    }
}
