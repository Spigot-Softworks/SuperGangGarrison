using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class CombatSystemTests
{
    [Fact]
    public void AppliesDamageByEntityIdAndLeavesMissingTargetsUnchanged()
    {
        var store = new EntityStore();
        var target = CreatePlayer(7, PlayerTeam.Blue);
        store.Add(target);
        var combat = new CombatSystem(store);
        var healthBefore = target.Health;

        Assert.True(combat.TryApplyPlayerDamage(target.Id, 12));
        Assert.Equal(healthBefore - 12, target.Health);
        Assert.False(combat.TryApplyPlayerDamage(999, 12));
    }

    [Fact]
    public void ExplosiveDamageFallsOffWithDistance()
    {
        Assert.Equal(
            100f,
            CombatSystem.ResolveExplosiveSplashDamageAtDistance(100f, 0f, 100f, minimumDamage: 0f));
        Assert.Equal(
            50f,
            CombatSystem.ResolveExplosiveSplashDamageAtDistance(100f, 50f, 100f, minimumDamage: 0f));
        Assert.Equal(
            0f,
            CombatSystem.ResolveExplosiveSplashDamageAtDistance(100f, 100f, 100f, minimumDamage: 0f));
    }

    [Fact]
    public void FriendlyFireUsesTheConfiguredTeamRule()
    {
        var store = new EntityStore();
        var attacker = CreatePlayer(1, PlayerTeam.Red);
        var friendlyTarget = CreatePlayer(2, PlayerTeam.Red);
        var enemyTarget = CreatePlayer(3, PlayerTeam.Blue);
        store.Add(attacker);
        store.Add(friendlyTarget);
        store.Add(enemyTarget);
        var combat = new CombatSystem(store);

        var friendlyHealthBefore = friendlyTarget.Health;
        var enemyHealthBefore = enemyTarget.Health;

        Assert.False(combat.TryApplyPlayerDamage(friendlyTarget.Id, 10, attacker.Id));
        Assert.Equal(friendlyHealthBefore, friendlyTarget.Health);
        Assert.True(combat.TryApplyPlayerDamage(enemyTarget.Id, 10, attacker.Id));
        Assert.Equal(enemyHealthBefore - 10, enemyTarget.Health);
    }

    private static PlayerEntity CreatePlayer(int id, PlayerTeam team)
    {
        var player = new PlayerEntity(id, CharacterClassCatalog.Scout, $"Player {id}");
        player.Spawn(team, 0f, 0f);
        return player;
    }
}
