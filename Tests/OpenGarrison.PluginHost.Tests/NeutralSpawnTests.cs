using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NeutralSpawnTests
{
    [Fact]
    public void NeutralSpawnImportsIntoBothTeamLists()
    {
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = new List<SpawnPoint>(),
            BlueSpawns = new List<SpawnPoint>(),
            RoomObjects = new List<RoomObjectMarker>(),
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "spawn",
            12f,
            18f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                ["team"] = "neutral",
                ["forward"] = "false",
            },
            context));

        var redSpawn = Assert.Single(context.RedSpawns);
        var blueSpawn = Assert.Single(context.BlueSpawns);
        Assert.Equal(12f, redSpawn.X);
        Assert.Equal(18f, redSpawn.Y);
        Assert.Equal(redSpawn, blueSpawn);
        Assert.True(redSpawn.IsStandardSpawn);
    }

    [Fact]
    public void CountsAsTeamSpawnForBothTeamsWhenNeutral()
    {
        var entity = CustomMapBuilderEntity.Create(
            "spawn",
            1f,
            2f,
            new Dictionary<string, string>
            {
                ["team"] = "neutral",
                ["forward"] = "false",
            });

        Assert.True(CustomMapBuilderEntityNormalization.CountsAsTeamSpawn(entity, "red"));
        Assert.True(CustomMapBuilderEntityNormalization.CountsAsTeamSpawn(entity, "blue"));
    }

    [Fact]
    public void NeutralSpawnStaysModernTypeOnLegacyExport()
    {
        var entity = CustomMapBuilderEntity.Create(
            "spawn",
            3f,
            4f,
            new Dictionary<string, string>
            {
                ["team"] = "neutral",
                ["forward"] = "false",
            });

        var exported = CustomMapBuilderEntityNormalization.ResolveEntityForExport(entity);

        Assert.Equal("spawn", exported.Type);
        Assert.Equal("neutral", exported.Properties["team"]);
    }

    [Fact]
    public void BothTeamsIncludeNeutralSpawnInSelectionPool()
    {
        var neutralSpawn = new SpawnPoint(55f, 55f);
        var world = CreateWorldWithSpawns(
            redSpawns: [new SpawnPoint(10f, 10f), neutralSpawn],
            blueSpawns: [new SpawnPoint(90f, 90f), neutralSpawn]);

        var redPool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red);
        var bluePool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Blue);

        Assert.Contains(redPool, spawn => spawn.X == 55f && spawn.Y == 55f);
        Assert.Contains(bluePool, spawn => spawn.X == 55f && spawn.Y == 55f);
    }

    private static SimulationWorld CreateWorldWithSpawns(
        IReadOnlyList<SpawnPoint> redSpawns,
        IReadOnlyList<SpawnPoint> blueSpawns)
    {
        var world = new SimulationWorld();
        var setLevel = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("CombatTestSetLevel was not found.");
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "neutral_spawn_test",
                    GameModeKind.CaptureTheFlag,
                    new WorldBounds(1024f, 768f),
                    1f,
                    null,
                    1,
                    1,
                    new SpawnPoint(0f, 0f),
                    redSpawns,
                    blueSpawns,
                    [],
                    [],
                    768f,
                    [],
                    false),
            ]);

        return world;
    }
}
