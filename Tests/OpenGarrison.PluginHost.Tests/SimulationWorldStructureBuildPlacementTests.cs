using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldStructureBuildPlacementTests
{
    [Fact]
    public void BuildingSentryFailsWhenInitialBodyWouldOverlapSolid()
    {
        var world = CreateEngineerWorld(
            solids:
            [
                new LevelSolid(90f, 90f, 40f, 40f),
            ]);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);
        var metalBefore = world.LocalPlayer.Metal;

        Assert.False(world.TryBuildLocalSentry());

        Assert.Empty(world.Sentries);
        Assert.Equal(metalBefore, world.LocalPlayer.Metal);
    }

    [Fact]
    public void BuildingSentrySucceedsWhenInitialBodyIsClearOfSolid()
    {
        var world = CreateEngineerWorld(
            solids:
            [
                new LevelSolid(130f, 90f, 40f, 40f),
            ]);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);

        Assert.True(world.TryBuildLocalSentry());

        var sentry = Assert.Single(world.Sentries);
        Assert.Equal(world.LocalPlayer.Id, sentry.OwnerPlayerId);
    }

    [Fact]
    public void BuildingSentryCanShiftHorizontallyAwayFromWallHuggingSolid()
    {
        var world = CreateEngineerWorld(
            solids:
            [
                new LevelSolid(113f, 90f, 40f, 40f),
            ]);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);

        Assert.True(world.TryBuildLocalSentry());

        var sentry = Assert.Single(world.Sentries);
        Assert.Equal(99f, sentry.X);
        Assert.Equal(100f, sentry.Y);
    }

    [Fact]
    public void BuildingJumpPadFailsWhenInitialBodyWouldOverlapSolid()
    {
        var world = CreateEngineerWorld(
            solids:
            [
                new LevelSolid(90f, 90f, 40f, 40f),
            ]);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);
        var metalBefore = world.LocalPlayer.Metal;

        Assert.False(world.TryBuildLocalJumpPad());

        Assert.Empty(world.JumpPads);
        Assert.Equal(metalBefore, world.LocalPlayer.Metal);
    }

    [Fact]
    public void BuildingJumpPadSucceedsWhenInitialBodyIsClearOfSolid()
    {
        var world = CreateEngineerWorld(
            solids:
            [
                new LevelSolid(112f, 90f, 40f, 40f),
            ]);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);

        Assert.True(world.TryBuildLocalJumpPad());

        var pad = Assert.Single(world.JumpPads);
        Assert.Equal(world.LocalPlayer.Id, pad.OwnerPlayerId);
    }

    [Fact]
    public void BuildingJumpPadCanShiftHorizontallyAwayFromWallHuggingSolid()
    {
        var world = CreateEngineerWorld(
            solids:
            [
                new LevelSolid(109f, 90f, 40f, 40f),
            ]);
        world.TeleportLocalPlayer(100f, 100f);
        world.LocalPlayer.SetSpawnRoomState(false);

        Assert.True(world.TryBuildLocalJumpPad());

        var pad = Assert.Single(world.JumpPads);
        Assert.Equal(99f, pad.X);
        Assert.Equal(100f, pad.Y);
    }

    private static SimulationWorld CreateEngineerWorld(IReadOnlyList<LevelSolid> solids)
    {
        var world = new SimulationWorld();
        world.CombatTestSetLevel(new SimpleLevel(
            name: "sentry_build_placement_test",
            mode: GameModeKind.TeamDeathmatch,
            bounds: new WorldBounds(512f, 512f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 0,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(300f, 100f)],
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: Array.Empty<RoomObjectMarker>(),
            floorY: 480f,
            solids: solids,
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        return world;
    }
}
