using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64HeldInputOrderingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScopePressIsConsumedOnceWhenStateAndCommandArriveOnDifferentTicks(bool stateFirst)
    {
        var (world, client, manager) = CreateSession(PlayerClass.Sniper);
        var held = default(PlayerInputSnapshot) with { FireSecondary = true };
        var command = new Protocol64InputCommand(1, 1, Protocol64InputCommandKind.FireSecondary,
            InputButtons.FireSecondary, 100, 0, CommandSequence: 1);

        if (stateFirst) client.TrySetLatestInput(1, held);
        else manager.HandleProtocol64InputCommand(client, command);
        Tick(world, manager);

        if (stateFirst) manager.HandleProtocol64InputCommand(client, command);
        else client.TrySetLatestInput(1, held);
        Tick(world, manager);
        Assert.True(world.LocalPlayer.IsSniperScoped);

        // Retransmitting the same reliable press must not toggle scope again.
        manager.HandleProtocol64InputCommand(client, command);
        for (var tick = 0; tick < 40; tick++) Tick(world, manager);
        Assert.True(world.LocalPlayer.IsSniperScoped);

        client.TrySetLatestInput(2, default);
        Tick(world, manager);
        Assert.True(world.LocalPlayer.IsSniperScoped);
        client.TrySetLatestInput(3, held);
        manager.HandleProtocol64InputCommand(client, command with
        {
            CommandId = 2, InputSequence = 3, CommandSequence = 2,
        });
        Tick(world, manager);
        Assert.False(world.LocalPlayer.IsSniperScoped);
    }

    [Fact]
    public void DelayedCommandDoesNotRestoreAnUnrelatedReleasedButton()
    {
        var (world, client, manager) = CreateSession(PlayerClass.Pyro);
        client.TrySetLatestInput(2, default);
        Tick(world, manager);
        var initialFuel = world.LocalPlayer.PyroPrimaryFuelScaled;

        // The jump was pressed while firing, but the newer input has released M1.
        manager.HandleProtocol64InputCommand(client, new Protocol64InputCommand(
            1, 1, Protocol64InputCommandKind.Jump, InputButtons.FirePrimary | InputButtons.Up,
            100, 0, CommandSequence: 1));
        Tick(world, manager);

        Assert.Equal(initialFuel, world.LocalPlayer.PyroPrimaryFuelScaled);
        Assert.Empty(world.Flames);
    }

    [Fact]
    public void AShortPressStillWorksWhenOnlyItsReliableCommandArrives()
    {
        var (world, client, manager) = CreateSession(PlayerClass.Sniper);
        // Release overtakes/losses the unreliable pressed state.
        client.TrySetLatestInput(2, default);
        Tick(world, manager);
        manager.HandleProtocol64InputCommand(client, new Protocol64InputCommand(
            1, 1, Protocol64InputCommandKind.FireSecondary, InputButtons.FireSecondary,
            100, 0, CommandSequence: 1));
        Tick(world, manager);
        Assert.True(world.LocalPlayer.IsSniperScoped);
        Tick(world, manager);
        Assert.True(world.LocalPlayer.IsSniperScoped);
    }

    [Fact]
    public void HeldJumpStateDoesNotSpendASecondAirJumpAfterTheReliablePress()
    {
        var (world, client, manager) = CreateSession(PlayerClass.Scout);
        world.LocalPlayer.SetExperimentalBonusAirJumps(2);
        var jump = default(PlayerInputSnapshot) with { Up = true };
        client.TrySetLatestInput(1, jump);
        Tick(world, manager);
        manager.HandleProtocol64InputCommand(client, new Protocol64InputCommand(
            1, 1, Protocol64InputCommandKind.Jump, InputButtons.Up, 100, 0, CommandSequence: 1));
        Tick(world, manager);
        var remaining = world.LocalPlayer.RemainingAirJumps;
        for (uint sequence = 2; sequence < 6; sequence++)
        {
            client.TrySetLatestInput(sequence, jump);
            Tick(world, manager);
        }
        Assert.Equal(remaining, world.LocalPlayer.RemainingAirJumps);
        manager.HandleProtocol64InputCommand(client, new Protocol64InputCommand(
            2, 6, Protocol64InputCommandKind.Jump, InputButtons.Up, 100, 0, CommandSequence: 2));
        Tick(world, manager);
        Assert.Equal(remaining - 1, world.LocalPlayer.RemainingAirJumps);
    }

    private static void Tick(SimulationWorld world, ServerSessionManager manager)
    {
        manager.PreparePlayableClientInputsForNextTick();
        world.AdvanceOneTick();
        manager.CompleteProtocol64InputsAfterSimulationTick();
    }

    private static (SimulationWorld, ClientSession, ServerSessionManager) CreateSession(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(100, 100);
        world.CombatTestSetLevel(new SimpleLevel("held-input-order", GameModeKind.TeamDeathmatch,
            new WorldBounds(512, 512), 1, null, 1, 1, spawn, [spawn], [spawn], [], [],
            floorY: 512, [], importedFromSource: false));
        world.TrySetLocalClass(playerClass);
        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red,
            respawnLivePlayerImmediately: true));
        var client = new ClientSession(SimulationWorld.LocalPlayerSlot, 1,
            new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero)
        { Protocol64Enabled = true };
        var manager = new ServerSessionManager(world, new() { [client.Slot] = client },
            4, 4, 0, () => TimeSpan.Zero, null, false, 20, 20, 5,
            _ => null, _ => { }, _ => { }, (_, _) => { }, _ => { });
        for (var tick = 0; tick < 120; tick++) Tick(world, manager);
        Assert.True(world.LocalPlayer.IsAlive);
        return (world, client, manager);
    }
}
