using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class ProjectileSystemTests
{
    [Fact]
    public void SpawnShotRegistersProjectileInSystemAndStore()
    {
        var store = new EntityStore();
        var combat = new CombatSystem(store);
        var projectiles = CreateSystem(store, combat);
        var owner = CreatePlayer(1, PlayerTeam.Red);
        store.Add(owner);

        projectiles.SpawnShot(owner, 100f, 200f, 10f, -5f);

        var shot = Assert.Single(projectiles.Shots);
        Assert.Equal(100f, shot.X);
        Assert.Equal(200f, shot.Y);
        Assert.Equal(10f, shot.VelocityX);
        Assert.Equal(-5f, shot.VelocityY);
        Assert.Equal(owner.Id, shot.OwnerId);
        Assert.Equal(PlayerTeam.Red, shot.Team);
        Assert.NotNull(store.Get(shot.Id));
    }

    [Fact]
    public void AdvanceShotsMovesProjectilesAlongTheirTrajectory()
    {
        var store = new EntityStore();
        var combat = new CombatSystem(store);
        var projectiles = CreateSystem(store, combat);
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

        var nextId = 100;
        var dependencies = new ProjectileSystemDependencies
        {
            AllocateEntityId = () => nextId++,
            EnumerateSimulatedPlayers = () => store.All().OfType<PlayerEntity>(),
            FindPlayerById = id => store.Get(id) as PlayerEntity,
            GetNearestShotHit = (shot, directionX, directionY, distance) =>
                new ShotHitResult(distance, target.X, target.Y, target, null, null),
        };
        var projectiles = new ProjectileSystem(store, combat, dependencies);

        projectiles.SpawnShot(attacker, target.X - 50f, target.Y, 10f, 0f, damagePerHit: 5f);
        projectiles.AdvanceShots();

        Assert.True(target.Health < targetHealthBefore);
    }

    private static ProjectileSystem CreateSystem(EntityStore store, CombatSystem combat)
    {
        var nextId = 100;
        return new ProjectileSystem(
            store,
            combat,
            new ProjectileSystemDependencies
            {
                AllocateEntityId = () => nextId++,
                EnumerateSimulatedPlayers = () => store.All().OfType<PlayerEntity>(),
                FindPlayerById = id => store.Get(id) as PlayerEntity,
            });
    }

    private static PlayerEntity CreatePlayer(int id, PlayerTeam team)
    {
        var player = new PlayerEntity(id, CharacterClassCatalog.Scout, $"Player {id}");
        player.Spawn(team, 0f, 0f);
        return player;
    }
}
