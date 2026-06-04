using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldForwardSpawnSelectionTests
{
    private static readonly MethodInfo CombatTestSetLevelMethod = GetRequiredSimulationWorldMethod("CombatTestSetLevel");

    [Fact]
    public void ActiveForwardSpawnsTakePriorityOverStandardSpawns()
    {
        var world = CreateControlPointWorld(
            redSpawns:
            [
                new SpawnPoint(10f, 10f),
                new SpawnPoint(200f, 200f, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveOwnedByTeam),
            ]);

        var pool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red);

        Assert.Single(pool);
        Assert.Equal(200f, pool[0].X);
    }

    [Fact]
    public void StandardSpawnsUsedWhenNoForwardSpawnMatchesCondition()
    {
        var world = CreateControlPointWorld(
            redSpawns:
            [
                new SpawnPoint(10f, 10f),
                new SpawnPoint(200f, 200f, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveOwnedByTeam),
            ]);

        world.CombatTestSetControlPointOwner(1, PlayerTeam.Blue);
        var pool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red);

        Assert.Single(pool);
        Assert.Equal(10f, pool[0].X);
    }

    [Fact]
    public void MultipleForwardSpawnsWithDifferentConditionsCanBothBeActive()
    {
        var world = CreateWorldWithSpawns(
            redSpawns:
            [
                new SpawnPoint(10f, 10f),
                new SpawnPoint(100f, 100f, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveNeutral),
                new SpawnPoint(300f, 300f, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam),
            ]);

        world.CombatTestSetControlPointOwner(1, null);
        var pool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red);

        Assert.Equal(2, pool.Count);
        Assert.Contains(pool, spawn => spawn.X == 100f);
        Assert.Contains(pool, spawn => spawn.X == 300f);
    }

    [Fact]
    public void LegacyForwardSpawnIndexMatchesControlPointMarkerName()
    {
        var world = CreateControlPointWorld(
            redSpawns:
            [
                new SpawnPoint(10f, 10f),
                new SpawnPoint(200f, 200f, SpawnPointRole.Forward, 2, ForwardSpawnUseCondition.ObjectiveOwnedByTeam),
            ]);

        world.CombatTestSetControlPointOwner(2, PlayerTeam.Red);
        var pool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red);

        Assert.Single(pool);
        Assert.Equal(200f, pool[0].X);
    }

    [Fact]
    public void ActiveForwardSpawnsAreOrderedByDescendingPriority()
    {
        var world = CreateControlPointWorld(
            redSpawns:
            [
                new SpawnPoint(10f, 10f),
                new SpawnPoint(100f, 100f, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveOwnedByTeam, Priority: 1),
                new SpawnPoint(300f, 300f, SpawnPointRole.Forward, 2, ForwardSpawnUseCondition.ObjectiveNotOwnedByTeam, Priority: 3),
            ]);

        world.CombatTestSetControlPointOwner(1, PlayerTeam.Red);
        world.CombatTestSetControlPointOwner(2, null);
        var pool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red);

        Assert.Equal(2, pool.Count);
        Assert.Equal(300f, pool[0].X);
        Assert.Equal(100f, pool[1].X);
    }

    [Fact]
    public void ReserveSpawnPrefersHigherPriorityEvenWhenLinkedToLowerControlPoint()
    {
        var world = CreateControlPointWorld(
            redSpawns:
            [
                new SpawnPoint(10f, 10f),
                new SpawnPoint(100f, 100f, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveOwnedByTeam, Priority: 1),
                new SpawnPoint(300f, 300f, SpawnPointRole.Forward, 1, ForwardSpawnUseCondition.ObjectiveOwnedByTeam, Priority: 4),
            ]);

        world.CombatTestSetControlPointOwner(1, PlayerTeam.Red);
        var pool = world.CombatTestGetTeamSpawnSelectionPool(PlayerTeam.Red);

        Assert.Equal(2, pool.Count);
        Assert.Equal(300f, pool[0].X);

        var spawn = world.CombatTestReserveTeamSpawn(world.LocalPlayer, PlayerTeam.Red);
        Assert.Equal(300f, spawn.X);
    }

    private static SimulationWorld CreateControlPointWorld(IReadOnlyList<SpawnPoint> redSpawns)
    {
        var world = new SimulationWorld();
        CombatTestSetLevelMethod.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "forward_spawn_test",
                    mode: GameModeKind.ControlPoint,
                    bounds: new WorldBounds(1024f, 768f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(10f, 10f),
                    redSpawns: redSpawns,
                    blueSpawns: [new SpawnPoint(900f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            100f,
                            200f,
                            48f,
                            24f,
                            "ControlPointNeutralS",
                            SourceName: "ControlPoint1"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            512f,
                            200f,
                            48f,
                            24f,
                            "ControlPointNeutralS",
                            SourceName: "ControlPoint2"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            900f,
                            200f,
                            48f,
                            24f,
                            "ControlPointNeutralS",
                            SourceName: "ControlPoint3"),
                    ],
                    floorY: 768f,
                    solids: [],
                    importedFromSource: false),
            ]);

        return world;
    }

    private static SimulationWorld CreateWorldWithSpawns(IReadOnlyList<SpawnPoint> redSpawns)
    {
        var world = new SimulationWorld();
        CombatTestSetLevelMethod.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "forward_spawn_test",
                    mode: GameModeKind.KingOfTheHill,
                    bounds: new WorldBounds(1024f, 768f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(10f, 10f),
                    redSpawns: redSpawns,
                    blueSpawns: [new SpawnPoint(900f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            512f,
                            200f,
                            48f,
                            24f,
                            "ControlPointNeutralS",
                            SourceName: "KothControlPoint"),
                    ],
                    floorY: 768f,
                    solids: [],
                    importedFromSource: false),
            ]);

        return world;
    }

    private static MethodInfo GetRequiredSimulationWorldMethod(string name)
    {
        return typeof(SimulationWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Could not find SimulationWorld.{name}.");
    }
}
