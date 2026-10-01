using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class ProjectileSystemTests
{

    [Fact]
    public void AdvanceShotsMovesProjectilesAlongTheirTrajectory()
    {
        var store = new EntityStore();
        var combat = new CombatSystem(store);
        var projectiles = new ProjectileSystem(store, combat);
        var owner = CreatePlayer(1, PlayerTeam.Red);
        store.Add(owner);

        projectiles.SpawnShot(owner, 100f, 200f, 10f, -5f);
        var shot = Assert.Single(projectiles.Shots);

        projectiles.AdvanceShots();

        Assert.Equal(110f, shot.X);
        Assert.Equal(195f, shot.Y);
        Assert.Single(projectiles.Shots);
    }

    [Fact]
    public void ShotImpactRoutesDamageThroughCombatSystem()
    {
        var store = new EntityStore();
        var combat = new CombatSystem(store);
        var attacker = CreatePlayer(1, PlayerTeam.Red);
        var target = CreatePlayer(2, PlayerTeam.Blue);
        store.Add(attacker);
        store.Add(target);
        var targetHealthBefore = target.Health;

        var projectiles = new ProjectileSystem(store, combat, new ShotAlwaysHitsHost(store, target));

        projectiles.SpawnShot(attacker, target.X - 50f, target.Y, 10f, 0f, damagePerHit: 5f);
        projectiles.AdvanceShots();

        Assert.True(target.Health < targetHealthBefore);
    }

    private static PlayerEntity CreatePlayer(int id, PlayerTeam team)
    {
        var player = new PlayerEntity(id, CharacterClassCatalog.Scout, $"Player {id}");
        player.Spawn(team, 0f, 0f);
        return player;
    }

    private sealed class ShotAlwaysHitsHost(EntityStore store, PlayerEntity target) : DetachedSimulationHost(store)
    {
        public override ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float distance)
            => new ShotHitResult(distance, target.X, target.Y, target, null, null);
    }
}
