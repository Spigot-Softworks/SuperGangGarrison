using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainChatBubbleControllerTests
{
    [Fact]
    public void KillTauntFiresAboutOnePointSixSecondsAfterKillAtDefaultTickRate()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
        });
        var player = world.LocalPlayer;
        var controller = new BotBrainChatBubbleController();

        _ = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);
        player.AddKill();

        var killTickInput = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);
        SetFrame(world, 47);
        var beforeDelayInput = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);
        SetFrame(world, 48);
        var delayedInput = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);

        Assert.False(killTickInput.Taunt);
        Assert.False(beforeDelayInput.Taunt);
        Assert.True(delayedInput.Taunt);
    }

    [Fact]
    public void KillTauntDelayScalesWithSimulationTickRate()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = 60,
            EnableLocalDummies = false,
        });
        var player = world.LocalPlayer;
        var controller = new BotBrainChatBubbleController();

        _ = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);
        player.AddKill();
        _ = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);

        SetFrame(world, 95);
        var beforeDelayInput = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);
        SetFrame(world, 96);
        var delayedInput = controller.Update(world, SimulationWorld.LocalPlayerSlot, player, PlayerTeam.Red, EmptyContext(), default, EmptyControlledSlots);

        Assert.False(beforeDelayInput.Taunt);
        Assert.True(delayedInput.Taunt);
    }

    private static IReadOnlyDictionary<byte, PlayerTeam> EmptyControlledSlots { get; } =
        new Dictionary<byte, PlayerTeam>();

    private static BotBrainChatBubbleContext EmptyContext() =>
        new(
            DirectTrace: string.Empty,
            SemanticRecoveryTrace: string.Empty,
            MedicHealTargetId: null,
            MedicHealTargetIsPocket: false);

    private static void SetFrame(SimulationWorld world, long frame)
    {
        var property = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Frame), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        property!.SetValue(world, frame);
    }
}
