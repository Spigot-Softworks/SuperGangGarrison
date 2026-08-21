using System.Collections.Generic;
using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HealthPackSpawnTests
{
    private static readonly MethodInfo KillPlayerMethod = typeof(SimulationWorld)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(method => method.Name == "KillPlayer" && method.GetParameters().Length == 14);

    [Fact]
    public void RuntimeImporterCreatesHealthPackSpawnMarker()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            HealthPackMetadata.HealthPackEntityType,
            100f,
            120f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [HealthPackMetadata.SizePropertyKey] = HealthPackMetadata.SmallSizeValue,
                [HealthPackMetadata.RespawnSecondsPropertyKey] = "2",
            },
            context));

        var marker = Assert.Single(context.HealthPackSpawns);
        Assert.Equal(100f, marker.X);
        Assert.Equal(120f, marker.Y);
        Assert.Equal(HealthPackSize.Small, marker.Size);
        Assert.Equal(60, marker.RespawnTicks);
    }

    [Fact]
    public void MapHealthPackHealsAndRespawnsAfterConfiguredTicks()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(128f, 128f);
        world.CombatTestSetLevel(new SimpleLevel(
            "health-pack-spawn-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(512f, 512f),
            1f,
            null,
            0,
            1,
            spawn,
            [spawn],
            [],
            [],
            [],
            floorY: 512f,
            [],
            importedFromSource: false,
            healthPackSpawns:
            [
                new HealthPackSpawnMarker(128f, 128f, HealthPackSize.Small, RespawnTicks: 2),
            ]));

        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        world.TeleportLocalPlayer(128f, 128f);

        var maxHealth = world.LocalPlayer.MaxHealth;
        var healthBeforePickup = Math.Max(1, maxHealth - 60);
        world.LocalPlayer.ForceSetHealth(healthBeforePickup);

        Assert.Single(world.HealthPacks);

        world.AdvanceOneTick();

        Assert.Equal(
            healthBeforePickup + (int)MathF.Round(maxHealth * HealthPackEntity.SmallHealFraction),
            world.LocalPlayer.Health);
        Assert.Empty(world.HealthPacks);

        world.LocalPlayer.ForceSetHealth(maxHealth);
        world.AdvanceOneTick();

        Assert.Empty(world.HealthPacks);

        world.AdvanceOneTick();

        var respawnedPack = Assert.Single(world.HealthPacks);
        Assert.Equal(HealthPackSize.Small, respawnedPack.Size);
        Assert.True(respawnedPack.IsMapSpawned);
    }

    [Theory]
    [InlineData(PlayerClass.Soldier)]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Medic)]
    [InlineData(PlayerClass.Sniper)]
    public void EnemyHealthPackDropDoesNotDependOnKillerClass(PlayerClass killerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(killerClass);
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var victim));
        Assert.Equal(PlayerTeam.Red, world.LocalPlayer.Team);
        Assert.Equal(PlayerTeam.Blue, victim.Team);
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableEnemyHealthPackDrops: true,
            EnemyHealthPackDropChance: 1f));

        KillPlayerMethod.Invoke(
            world,
            [
                victim,
                false,
                world.LocalPlayer,
                null,
                DeadBodyAnimationKind.Default,
                null,
                null,
                null,
                true,
                true,
                false,
                true,
                -1,
                false,
            ]);

        Assert.False(victim.IsAlive);
        Assert.Single(world.HealthPacks.Where(candidate => !candidate.IsMapSpawned));
    }
}
