using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HealthPackSpawnTests
{
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
}
