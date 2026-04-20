using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldProjectileSpawnBlockingTests
{
    [Fact]
    public void ProjectileSpawnBlockCheckDoesNotFalsePositiveForHorizontalRayAlongSolidEdge()
    {
        var world = new SimulationWorld();
        SetLevel(
            world,
            CreateLevel(
                solids:
                [
                    new LevelSolid(100f, 50f, 20f, 20f),
                ]));

        var blocked = InvokeProjectileSpawnBlocked(world, 0f, 50f, 20f, 50f);

        Assert.False(blocked);
    }

    [Fact]
    public void ProjectileSpawnBlockCheckStillDetectsActualHorizontalSolidBlocker()
    {
        var world = new SimulationWorld();
        SetLevel(
            world,
            CreateLevel(
                solids:
                [
                    new LevelSolid(10f, 40f, 8f, 20f),
                ]));

        var blocked = InvokeProjectileSpawnBlocked(world, 0f, 50f, 20f, 50f);

        Assert.True(blocked);
    }

    private static bool InvokeProjectileSpawnBlocked(SimulationWorld world, float originX, float originY, float targetX, float targetY)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestIsProjectileSpawnBlocked", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [originX, originY, targetX, targetY]);
        Assert.IsType<bool>(result);
        return (bool)result!;
    }

    private static void SetLevel(SimulationWorld world, SimpleLevel level)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [level]);
    }

    private static SimpleLevel CreateLevel(IReadOnlyList<LevelSolid>? solids = null, IReadOnlyList<RoomObjectMarker>? roomObjects = null)
    {
        return new SimpleLevel(
            name: "test",
            mode: GameModeKind.TeamDeathmatch,
            bounds: new WorldBounds(512f, 512f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 0,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(32f, 32f),
            redSpawns: [new SpawnPoint(32f, 32f)],
            blueSpawns: [new SpawnPoint(64f, 32f)],
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: roomObjects ?? Array.Empty<RoomObjectMarker>(),
            floorY: 480f,
            solids: solids ?? Array.Empty<LevelSolid>(),
            importedFromSource: false);
    }
}