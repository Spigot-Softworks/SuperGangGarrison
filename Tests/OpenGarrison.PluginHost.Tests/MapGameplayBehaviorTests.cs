using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapGameplayBehaviorTests
{

    [Fact]
    public void RuntimeImporterCreatesSpawnClassBehaviorMarker()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            SpawnClassBehaviorMetadata.EntityType,
            240f,
            220f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [SpawnClassBehaviorMetadata.TeamPropertyKey] = "red",
                [SpawnClassBehaviorMetadata.ForceClassPropertyKey] = "soldier",
                [SpawnClassBehaviorMetadata.ManualSpawnPropertyKey] = "true",
                [SpawnClassBehaviorMetadata.SkipTeamSelectPropertyKey] = "true",
                [SpawnClassBehaviorMetadata.AllowTeamChangePropertyKey] = "false",
                [SpawnClassBehaviorMetadata.AllowClassChangePropertyKey] = "false",
            },
            context));

        var marker = Assert.Single(context.SpawnClassBehaviors);
        Assert.Equal(240f, marker.X);
        Assert.Equal(220f, marker.Y);
        Assert.Equal(SpawnClassBehaviorTeam.Red, marker.Team);
        Assert.Equal(PlayerClass.Soldier, marker.ForcedClass);
        Assert.True(marker.ManualSpawn);
        Assert.True(marker.SkipTeamSelect);
        Assert.False(marker.AllowTeamChange);
        Assert.False(marker.AllowClassChange);
    }

    [Fact]
    public void SpawnClassBehaviorForcesPlayerClassAndManualSpawn()
    {
        var world = CreateWorldWithSpawnClassBehavior(
            new SpawnClassBehaviorMarker(
                240f,
                220f,
                SpawnClassBehaviorTeam.Red,
                PlayerClass.Soldier,
                ManualSpawn: true,
                SkipTeamSelect: true,
                AllowTeamChange: false,
                AllowClassChange: false));

        world.PrepareLocalPlayerJoin();
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);

        Assert.Equal(PlayerTeam.Red, world.LocalPlayer.Team);
        Assert.Equal(PlayerClass.Soldier, world.LocalPlayer.ClassId);
        Assert.Equal(240f, world.LocalPlayer.X);
        Assert.Equal(220f, world.LocalPlayer.Y);
        Assert.False(world.CanNetworkPlayerChangeTeamInCurrentMode(SimulationWorld.LocalPlayerSlot));
        Assert.False(world.CanNetworkPlayerSelectClassInCurrentMode(
            SimulationWorld.LocalPlayerSlot,
            CharacterClassCatalog.Scout));
    }

    [Fact]
    public void MapSpawnedServerBotBypassesPlayerSpawnClassBehavior()
    {
        var world = CreateWorldWithSpawnClassBehavior(
            new SpawnClassBehaviorMarker(
                40f,
                160f,
                SpawnClassBehaviorTeam.Any,
                PlayerClass.Soldier,
                ManualSpawn: true,
                SkipTeamSelect: false,
                AllowTeamChange: false,
                AllowClassChange: false));
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Red,
            PlayerClass.Medic,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            string.Empty,
            false,
            false,
            240f,
            220f,
            out var slot));

        Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
        Assert.Equal(PlayerTeam.Red, bot.Team);
        Assert.Equal(PlayerClass.Medic, bot.ClassId);
        Assert.Equal(240f, bot.X);
        Assert.Equal(220f, bot.Y);
    }

    private static SimulationWorld CreateWorldWithSpawnClassBehavior(SpawnClassBehaviorMarker behavior)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var fallbackSpawn = new SpawnPoint(40f, 160f);
        world.CombatTestSetLevel(new SimpleLevel(
            "map-gameplay-behavior-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(512f, 512f),
            1f,
            null,
            1,
            1,
            fallbackSpawn,
            [fallbackSpawn],
            [fallbackSpawn],
            [],
            [],
            floorY: 512f,
            [],
            importedFromSource: false,
            spawnClassBehaviors: [behavior]));
        return world;
    }
}
