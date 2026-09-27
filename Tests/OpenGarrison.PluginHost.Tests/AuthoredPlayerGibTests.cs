using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class AuthoredPlayerGibTests
{
    [Theory]
    [InlineData("civilian", 8)]
    [InlineData("scout", 8)]
    [InlineData("soldier", 9)]
    [InlineData("pyro", 8)]
    [InlineData("demoman", 9)]
    [InlineData("heavy", 8)]
    [InlineData("engineer", 9)]
    [InlineData("medic", 8)]
    [InlineData("sniper", 9)]
    [InlineData("spy", 6)]
    public void EveryStockClassHasACompleteTeamSet(string classId, int expectedCount)
    {
        Assert.True(AuthoredPlayerGibCatalog.TryGetParts(classId, PlayerTeam.Red, out var red));
        Assert.True(AuthoredPlayerGibCatalog.TryGetParts(classId, PlayerTeam.Blue, out var blue));
        Assert.Equal(expectedCount, red.Count);
        Assert.Equal(expectedCount, blue.Count);
        for (var index = 0; index < expectedCount; index += 1)
        {
            Assert.Equal(red[index].SpawnOffsetX, blue[index].SpawnOffsetX);
            Assert.Equal(red[index].SpawnOffsetY, blue[index].SpawnOffsetY);
            Assert.Equal(index, red[index].CatalogPartIndex);
            Assert.Equal(index, blue[index].CatalogPartIndex);
            Assert.True(AuthoredPlayerGibCatalog.IsAuthoredSprite(red[index].SpriteName));
            Assert.True(AuthoredPlayerGibCatalog.IsAuthoredSprite(blue[index].SpriteName));
        }
    }

    [Fact]
    public void NetworkDeathUsesSheetPositionsAndMirrorsLeftFacingParts()
    {
        var world = CreateJoinedWorld(PlayerTeam.Red);
        var player = world.LocalPlayer;
        Assert.True(AuthoredPlayerGibCatalog.TryGetParts("scout", PlayerTeam.Red, out var parts));

        player.RestoreMovementProbeState(null, null, 1f);
        world.SpawnClientPlayerGibsFromNetworkDeath(player, 600f, 500f);
        Assert.Equal(parts.Count, world.PlayerGibs.Count);
        for (var index = 0; index < parts.Count; index += 1)
        {
            Assert.Equal(parts[index].SpriteName, world.PlayerGibs[index].SpriteName);
            Assert.Equal(600f + parts[index].SpawnOffsetX, world.PlayerGibs[index].X);
            Assert.Equal(500f + parts[index].SpawnOffsetY, world.PlayerGibs[index].Y);
            Assert.False(world.PlayerGibs[index].FlipHorizontally);
        }

        var leftWorld = CreateJoinedWorld(PlayerTeam.Blue);
        var leftPlayer = leftWorld.LocalPlayer;
        leftPlayer.RestoreMovementProbeState(null, null, -1f);
        leftWorld.SpawnClientPlayerGibsFromNetworkDeath(leftPlayer, 600f, 500f);
        Assert.Equal(parts.Count, leftWorld.PlayerGibs.Count);
        for (var index = 0; index < parts.Count; index += 1)
        {
            Assert.Equal(600f - parts[index].SpawnOffsetX, leftWorld.PlayerGibs[index].X);
            Assert.Equal(500f + parts[index].SpawnOffsetY, leftWorld.PlayerGibs[index].Y);
            Assert.True(leftWorld.PlayerGibs[index].FlipHorizontally);
            Assert.Equal(2f, leftWorld.PlayerGibs[index].AuthoredRenderScale);
        }
    }

    private static SimulationWorld CreateJoinedWorld(PlayerTeam team)
    {
        var world = new SimulationWorld();
        var redSpawn = new SpawnPoint(600f, 500f);
        var blueSpawn = new SpawnPoint(800f, 500f);
        world.CombatTestSetLevel(new SimpleLevel(
            "authored-gib-placement",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(2048f, 2048f),
            1f,
            null,
            1,
            1,
            redSpawn,
            [redSpawn],
            [blueSpawn],
            [],
            [],
            floorY: 1024f,
            [new LevelSolid(0f, 1024f, 2048f, 1024f)],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(team);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        return world;
    }
}
