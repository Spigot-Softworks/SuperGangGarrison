using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntitySniperStateTests
{
    [Fact]
    public void ForceEndSniperScopeForHumiliationClearsScopeAndCharge()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Sniper);
        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.True(player.TryToggleSniperScope());
        for (var tick = 0; tick < 3; tick += 1)
        {
            player.AdvanceTickState(default, 1d / 30d);
        }

        Assert.True(player.IsSniperScoped);
        Assert.True(player.SniperChargeTicks > 0);

        player.ForceEndSniperScopeForHumiliation();

        Assert.False(player.IsSniperScoped);
        Assert.Equal(0, player.SniperChargeTicks);
    }

    [Fact]
    public void DroppingScopeClearsConsecutiveFullyChargedHitBonuses()
    {
        var player = CreateSniperWithChargedHitStreak();
        Assert.True(player.TryToggleSniperScope());
        Assert.True(player.SniperRifleFullChargeTicks < PlayerEntity.SniperChargeMaxTicks);
        Assert.True(player.GetSniperRifleStreakDamageMultiplier(isFullyCharged: true) > 1f);

        Assert.True(player.TryToggleSniperScope());

        Assert.False(player.IsSniperScoped);
        Assert.Equal(0, player.SniperRifleFullyChargedHitStreak);
        Assert.Equal(PlayerEntity.SniperChargeMaxTicks, player.SniperRifleFullChargeTicks);
        Assert.Equal(1f, player.GetSniperRifleStreakDamageMultiplier(isFullyCharged: true));
    }

    [Fact]
    public void SwitchingWeaponsClearsConsecutiveFullyChargedHitBonuses()
    {
        var player = CreateSniperWithChargedHitStreak();
        Assert.Equal(GameplayEquipmentSlot.Primary, player.GameplayLoadoutState.EquippedSlot);

        Assert.True(player.TrySelectGameplayEquippedSlot(GameplayEquipmentSlot.Secondary));

        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(0, player.SniperRifleFullyChargedHitStreak);
        Assert.Equal(PlayerEntity.SniperChargeMaxTicks, player.SniperRifleFullChargeTicks);
        Assert.Equal(1f, player.GetSniperRifleStreakDamageMultiplier(isFullyCharged: true));
    }

    private static PlayerEntity CreateSniperWithChargedHitStreak()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Sniper);
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.ResolveSniperRifleStreakShot(isFullyCharged: true, hitEnemyPlayer: true);
        player.ResolveSniperRifleStreakShot(isFullyCharged: true, hitEnemyPlayer: true);
        Assert.Equal(2, player.SniperRifleFullyChargedHitStreak);
        return player;
    }
}
