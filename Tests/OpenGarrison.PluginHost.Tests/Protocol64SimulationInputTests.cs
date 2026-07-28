using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64SimulationInputTests
{
    [Fact]
    public void ExplicitProtocol64JumpEdgesRemainDistinctAcrossAdjacentTicks()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(100f, 100f);
        world.CombatTestSetLevel(new SimpleLevel(
            "protocol64-input-edge-test",
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
            [],
            floorY: 512f,
            [],
            importedFromSource: false));

        Assert.True(world.TrySetNetworkPlayerTeam(
            SimulationWorld.LocalPlayerSlot,
            PlayerTeam.Red,
            respawnLivePlayerImmediately: true));

        for (var tick = 0; tick < 120 && !world.LocalPlayer.IsGrounded; tick += 1)
        {
            world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, default);
            world.AdvanceOneTick();
        }

        Assert.True(world.LocalPlayer.IsGrounded);
        world.LocalPlayer.SetExperimentalBonusAirJumps(1);

        var jump = default(PlayerInputSnapshot) with { Up = true };
        world.TrySetNetworkPlayerInput(
            SimulationWorld.LocalPlayerSlot,
            jump,
            InputButtons.Up);
        world.AdvanceOneTick();
        Assert.False(world.LocalPlayer.IsGrounded);
        var remainingAfterFirstJump = world.LocalPlayer.RemainingAirJumps;

        world.TrySetNetworkPlayerInput(
            SimulationWorld.LocalPlayerSlot,
            jump,
            InputButtons.Up);
        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.VerticalSpeed < 0f);
        Assert.True(
            world.LocalPlayer.RemainingAirJumps < remainingAfterFirstJump,
            $"expected explicit second jump to consume an air jump, max={world.LocalPlayer.MaxAirJumps}, first={remainingAfterFirstJump}, second={world.LocalPlayer.RemainingAirJumps}, vertical={world.LocalPlayer.VerticalSpeed:0.###}");
    }
}
