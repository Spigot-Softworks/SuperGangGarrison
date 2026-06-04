using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerOnlyBarrierProjectileTests
{
    [Fact]
    public void ShotAllowBarrierDoesNotRaycastFromInteriorSegment()
    {
        var marker = CreatePlayerOnlyBarrier(0f, 0f);
        const float insideX = 3f;
        const float insideY = 30f;

        Assert.False(BarrierProjectileRaycast.TryRaycastMarker(
            marker.Barrier,
            PlayerTeam.Red,
            marker,
            insideX,
            insideY,
            directionX: 1f,
            directionY: 0f,
            maxDistance: 2f,
            out _));

        Assert.False(SimpleLevelBarrierCollision.BlocksPointForProjectile(
            CreateLevel(marker),
            PlayerTeam.Red,
            insideX,
            insideY));
    }

    [Fact]
    public void ProjectileSpawnIsNotBlockedThroughPlayerOnlyBarrierForFiringTeam()
    {
        var marker = CreatePlayerOnlyBarrier(0f, 0f);
        var level = CreateLevel(marker);
        var world = new SimulationWorld();
        SetLevel(world, level);

        const float insideX = 3f;
        const float insideY = 30f;
        Assert.False(InvokeProjectileSpawnBlocked(world, insideX, insideY, insideX + 15f, insideY, PlayerTeam.Red));
    }

    [Fact]
    public void ProjectileSpawnIgnoresOpposingTeamShotBlockOnBarrier()
    {
        var marker = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            new BarrierConfiguration(new BarrierTargetFilters(
                RedPlayers: BarrierTargetFilter.Allow,
                BluePlayers: BarrierTargetFilter.Allow,
                RedShots: BarrierTargetFilter.Allow,
                BlueShots: BarrierTargetFilter.Block,
                RedIntel: BarrierTargetFilter.Allow,
                BlueIntel: BarrierTargetFilter.Allow)));
        var world = new SimulationWorld();
        SetLevel(world, CreateLevel(marker));

        Assert.False(InvokeProjectileSpawnBlocked(world, 3f, 30f, 18f, 30f, PlayerTeam.Red));
        Assert.True(InvokeProjectileSpawnBlocked(world, 3f, 30f, 18f, 30f, PlayerTeam.Blue));
    }

    private static RoomObjectMarker CreatePlayerOnlyBarrier(float x, float y)
    {
        return BarrierConfiguration.CreateMarker(
            x,
            y,
            1f,
            1f,
            new BarrierConfiguration(new BarrierTargetFilters(
                RedPlayers: BarrierTargetFilter.Block,
                BluePlayers: BarrierTargetFilter.Block,
                RedShots: BarrierTargetFilter.Allow,
                BlueShots: BarrierTargetFilter.Allow,
                RedIntel: BarrierTargetFilter.Allow,
                BlueIntel: BarrierTargetFilter.Allow)));
    }

    private static SimpleLevel CreateLevel(RoomObjectMarker barrier)
    {
        return new SimpleLevel(
            "player-only-barrier-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(256f, 256f),
            1f,
            null,
            0,
            1,
            new SpawnPoint(32f, 32f),
            Array.Empty<SpawnPoint>(),
            Array.Empty<SpawnPoint>(),
            Array.Empty<IntelBaseMarker>(),
            [barrier],
            0f,
            Array.Empty<LevelSolid>(),
            importedFromSource: false);
    }

    private static bool InvokeProjectileSpawnBlocked(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY,
        PlayerTeam shotTeam)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestIsProjectileSpawnBlocked", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [originX, originY, targetX, targetY, shotTeam]);
        Assert.IsType<bool>(result);
        return (bool)result!;
    }

    private static void SetLevel(SimulationWorld world, SimpleLevel level)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [level]);
    }

}
