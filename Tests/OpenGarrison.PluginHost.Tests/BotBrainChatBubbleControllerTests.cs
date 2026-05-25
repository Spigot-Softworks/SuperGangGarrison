using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainChatBubbleControllerTests
{
    private const byte MirrorBotSlot = 2;

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

    [Fact]
    public void NearbyAlliedHumanTauntUsesFiftyPercentMirrorChance()
    {
        var tauntScenario = CreateNearbyHumanTauntScenario(expectedTaunt: true);
        var noTauntScenario = CreateNearbyHumanTauntScenario(expectedTaunt: false);

        Assert.True(tauntScenario.Input.Taunt);
        Assert.False(noTauntScenario.Input.Taunt);
    }

    [Fact]
    public void NearbyAlliedHumanTauntDoesNotRerollWhileSameTauntIsActive()
    {
        var scenario = CreateNearbyHumanTauntScenario(expectedTaunt: true);

        SetFrame(scenario.World, scenario.Frame + 1);
        var repeatInput = scenario.Controller.Update(
            scenario.World,
            MirrorBotSlot,
            scenario.Bot,
            PlayerTeam.Red,
            EmptyContext(),
            default,
            scenario.ControlledSlots);

        Assert.True(scenario.Input.Taunt);
        Assert.False(repeatInput.Taunt);
    }

    [Fact]
    public void NearbyHumanTauntMirrorRequiresAlliedNearbyHuman()
    {
        var trueFrame = CreateNearbyHumanTauntScenario(expectedTaunt: true).Frame;
        var farScenario = RunNearbyHumanTauntScenario(trueFrame, PlayerTeam.Red, PlayerTeam.Red, botOffsetX: 900f);
        var enemyScenario = RunNearbyHumanTauntScenario(trueFrame, PlayerTeam.Blue, PlayerTeam.Red, botOffsetX: 48f);

        Assert.False(farScenario.Input.Taunt);
        Assert.False(enemyScenario.Input.Taunt);
    }

    private static IReadOnlyDictionary<byte, PlayerTeam> EmptyControlledSlots { get; } =
        new Dictionary<byte, PlayerTeam>();

    private static BotBrainChatBubbleContext EmptyContext() =>
        new(
            DirectTrace: string.Empty,
            SemanticRecoveryTrace: string.Empty,
            MedicHealTargetId: null,
            MedicHealTargetIsPocket: false);

    private static (
        SimulationWorld World,
        BotBrainChatBubbleController Controller,
        PlayerEntity Human,
        PlayerEntity Bot,
        IReadOnlyDictionary<byte, PlayerTeam> ControlledSlots,
        PlayerInputSnapshot Input,
        long Frame) CreateNearbyHumanTauntScenario(bool expectedTaunt)
    {
        for (var frame = 0; frame < 256; frame += 1)
        {
            var scenario = RunNearbyHumanTauntScenario(frame, PlayerTeam.Red, PlayerTeam.Red, botOffsetX: 48f);
            if (scenario.Input.Taunt == expectedTaunt)
            {
                return scenario;
            }
        }

        throw new InvalidOperationException($"Could not find a deterministic nearby human taunt roll for expectedTaunt={expectedTaunt}.");
    }

    private static (
        SimulationWorld World,
        BotBrainChatBubbleController Controller,
        PlayerEntity Human,
        PlayerEntity Bot,
        IReadOnlyDictionary<byte, PlayerTeam> ControlledSlots,
        PlayerInputSnapshot Input,
        long Frame) RunNearbyHumanTauntScenario(
            long frame,
            PlayerTeam humanTeam,
            PlayerTeam botTeam,
            float botOffsetX)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
        });
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, humanTeam));
        world.ForceRespawnLocalPlayer();
        var human = world.LocalPlayer;
        human.TeleportTo(100f, 100f);
        Assert.True(world.TryPrepareNetworkPlayerJoin(MirrorBotSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(MirrorBotSlot, botTeam));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(MirrorBotSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(MirrorBotSlot, out var bot));
        bot.TeleportTo(human.X + botOffsetX, human.Y);
        var controller = new BotBrainChatBubbleController();
        var controlledSlots = new Dictionary<byte, PlayerTeam>
        {
            [MirrorBotSlot] = botTeam,
        };

        SetFrame(world, frame);
        Assert.True(human.TryStartTaunt());
        var input = controller.Update(world, MirrorBotSlot, bot, botTeam, EmptyContext(), default, controlledSlots);
        return (world, controller, human, bot, controlledSlots, input, frame);
    }

    private static void SetFrame(SimulationWorld world, long frame)
    {
        var property = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Frame), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        property!.SetValue(world, frame);
    }
}
