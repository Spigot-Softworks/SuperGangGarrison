using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ArenaRoundEndRespawnTests
{
    [Fact]
    public void DeadArenaPlayerDoesNotRespawnDuringRoundEndHumiliation()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.CombatTestSetLevel(CreateArenaLevel());
        world.PrepareLocalPlayerJoin();
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red, respawnLivePlayerImmediately: true));
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var bluePlayer));

        Assert.True(world.ForceKillNetworkPlayer(2));
        world.AdvanceOneTick();

        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
        Assert.False(bluePlayer.IsAlive);

        for (var tick = 0; tick < world.Config.TicksPerSecond; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(bluePlayer.IsAlive);
        Assert.Equal(0, world.GetNetworkPlayerRespawnTicks(2));
    }

    private static SimpleLevel CreateArenaLevel()
    {
        return new SimpleLevel(
            name: "arena_round_end_respawn_test",
            mode: GameModeKind.Arena,
            bounds: new WorldBounds(960f, 640f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(64f, 64f),
            redSpawns: [new SpawnPoint(64f, 64f)],
            blueSpawns: [new SpawnPoint(860f, 64f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 64f, 64f),
                new IntelBaseMarker(PlayerTeam.Blue, 860f, 64f),
            ],
            roomObjects:
            [
                new RoomObjectMarker(
                    RoomObjectType.ArenaControlPoint,
                    459f,
                    299f,
                    42f,
                    42f,
                    "ControlPointNeutralS",
                    SourceName: "ArenaControlPoint"),
                new RoomObjectMarker(
                    RoomObjectType.CaptureZone,
                    380f,
                    240f,
                    200f,
                    180f,
                    string.Empty,
                    SourceName: "ArenaCaptureZone"),
            ],
            floorY: 320f,
            solids: [new LevelSolid(0f, 320f, 960f, 320f)],
            importedFromSource: false);
    }
}
