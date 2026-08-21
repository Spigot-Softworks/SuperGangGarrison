using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class FireZoneTests
{
    [Fact]
    public void BuilderCatalogCreatesResizableFireZone()
    {
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition("firebox", out var definition));

        var entity = definition.CreateEntity(20f, 30f);
        Assert.True(CustomMapRoomObjectFactory.TryCreateFromBuilderEntity(entity, out var marker));
        Assert.Equal(RoomObjectType.FireBox, marker.Type);
        Assert.Equal(42f, marker.Width);
        Assert.Equal(42f, marker.Height);
    }

    [Fact]
    public void FireZoneIgnitesAndRefreshesPlayersWhileOccupied()
    {
        var fireZone = new RoomObjectMarker(
            RoomObjectType.FireBox,
            0f,
            0f,
            42f,
            42f,
            "sprite64");
        var level = new SimpleLevel(
            "fire-zone-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(256f, 256f),
            1f,
            null,
            0,
            1,
            new SpawnPoint(0f, 0f),
            [],
            [],
            [],
            [fireZone],
            0f,
            [],
            importedFromSource: false);
        var world = new SimulationWorld();
        world.CombatTestSetLevel(level);
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        world.LocalPlayer.TeleportTo(10f, 10f);

        InvokeRoomHazards(world, world.LocalPlayer);
        Assert.True(world.LocalPlayer.IsBurning);
        var firstDuration = world.LocalPlayer.BurnDurationSourceTicks;

        world.LocalPlayer.AdvanceAfterburn((float)world.Config.FixedDeltaSeconds);
        var decayedDuration = world.LocalPlayer.BurnDurationSourceTicks;
        InvokeRoomHazards(world, world.LocalPlayer);

        Assert.True(decayedDuration < firstDuration);
        Assert.True(world.LocalPlayer.BurnDurationSourceTicks > decayedDuration);
        Assert.True(world.LocalPlayer.IsBurning);
    }

    private static void InvokeRoomHazards(SimulationWorld world, PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyRoomHazards",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(world, [player]);
    }
}
