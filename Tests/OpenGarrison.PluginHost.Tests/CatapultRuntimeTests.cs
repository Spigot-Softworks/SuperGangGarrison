using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CatapultRuntimeTests
{
    private static readonly MethodInfo ApplyRoomForcesMethod = typeof(SimulationWorld).GetMethod(
        "ApplyRoomForces",
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(PlayerEntity), typeof(bool)],
        null)
        ?? throw new MissingMethodException(typeof(SimulationWorld).FullName, "ApplyRoomForces");

    [Fact]
    public void CatapultLaunchesOnZoneEntryOnlyOnce()
    {
        var world = CreateWorldWithCatapult(new CatapultConfiguration(
            90f,
            10f * LegacyMovementModel.SourceTicksPerSecond,
            RequiresJumpPress: false));
        var player = world.LocalPlayer;

        var consumedJump = InvokeApplyRoomForces(world, player, jumpPressed: false);

        Assert.False(consumedJump);
        Assert.InRange(player.VerticalSpeed, -301f, -299f);
        Assert.InRange(MathF.Abs(player.HorizontalSpeed), 0f, 0.001f);

        consumedJump = InvokeApplyRoomForces(world, player, jumpPressed: false);

        Assert.False(consumedJump);
        Assert.InRange(player.VerticalSpeed, -301f, -299f);
    }

    [Fact]
    public void CatapultCanRequireFreshJumpPress()
    {
        var world = CreateWorldWithCatapult(new CatapultConfiguration(
            0f,
            10f * LegacyMovementModel.SourceTicksPerSecond,
            RequiresJumpPress: true));
        var player = world.LocalPlayer;

        var consumedJump = InvokeApplyRoomForces(world, player, jumpPressed: false);

        Assert.False(consumedJump);
        Assert.InRange(MathF.Abs(player.HorizontalSpeed), 0f, 0.001f);
        Assert.InRange(MathF.Abs(player.VerticalSpeed), 0f, 0.001f);

        consumedJump = InvokeApplyRoomForces(world, player, jumpPressed: true);

        Assert.True(consumedJump);
        Assert.InRange(player.HorizontalSpeed, 299f, 301f);
        Assert.InRange(MathF.Abs(player.VerticalSpeed), 0f, 0.001f);
    }

    private static bool InvokeApplyRoomForces(SimulationWorld world, PlayerEntity player, bool jumpPressed)
    {
        return (bool)(ApplyRoomForcesMethod.Invoke(world, [player, jumpPressed]) ?? false);
    }

    private static SimulationWorld CreateWorldWithCatapult(CatapultConfiguration configuration)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(100f, 100f);
        world.CombatTestSetLevel(new SimpleLevel(
            "catapult-runtime-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(512f, 512f),
            1f,
            null,
            1,
            1,
            spawn,
            [spawn],
            [spawn],
            [],
            [
                new RoomObjectMarker(
                    RoomObjectType.Catapult,
                    0f,
                    0f,
                    220f,
                    220f,
                    string.Empty,
                    SourceName: CatapultMetadata.EntityType,
                    Value: configuration.Speed,
                    Catapult: configuration),
            ],
            floorY: 512f,
            [],
            importedFromSource: false));
        Assert.True(world.TrySetNetworkPlayerTeam(
            SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Red,
            respawnLivePlayerImmediately: true));
        return world;
    }
}
