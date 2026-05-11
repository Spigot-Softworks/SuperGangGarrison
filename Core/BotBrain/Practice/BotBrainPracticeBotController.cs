using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;

namespace OpenGarrison.Core.BotBrain;

public sealed class BotBrainPracticeBotController : IPracticeBotController
{
    private readonly Dictionary<byte, BotBrainController> _controllersBySlot = new();
    private readonly Dictionary<byte, ControlledBotSlot> _configuredSlots = new();
    private readonly Dictionary<byte, PlayerTeam> _controlledTeamsBySlot = new();
    private readonly BotBrainChatBubbleController _chatBubbles = new();

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics => BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        foreach (var controller in _controllersBySlot.Values)
        {
            controller.Reset();
        }

        _chatBubbles.Reset();
    }

    public void ConfigureSpawnOverrides(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        foreach (var slot in _controllersBySlot.Keys.Except(controlledSlots.Keys).ToArray())
        {
            _controllersBySlot.Remove(slot);
            _configuredSlots.Remove(slot);
            _chatBubbles.RemoveSlot(slot);
        }

        foreach (var (slot, controlledSlot) in controlledSlots)
        {
            if (!_controllersBySlot.TryGetValue(slot, out var controller))
            {
                controller = new BotBrainController();
                _controllersBySlot[slot] = controller;
                _configuredSlots[slot] = controlledSlot;
                continue;
            }

            if (_configuredSlots.TryGetValue(slot, out var previousSlot)
                && previousSlot.Team == controlledSlot.Team
                && previousSlot.ClassId == controlledSlot.ClassId)
            {
                continue;
            }

            _configuredSlots[slot] = controlledSlot;
            controller.Reset();
        }
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        var inputs = new Dictionary<byte, PlayerInputSnapshot>(controlledSlots.Count);
        _controlledTeamsBySlot.Clear();
        foreach (var (slot, controlledSlot) in controlledSlots)
        {
            _controlledTeamsBySlot[slot] = controlledSlot.Team;
        }

        foreach (var (slot, controlledSlot) in controlledSlots)
        {
            if (!world.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            if (!_controllersBySlot.TryGetValue(slot, out var controller))
            {
                controller = new BotBrainController();
                _controllersBySlot[slot] = controller;
                _configuredSlots[slot] = controlledSlot;
            }

            var input = controller.Think(player, world, controlledSlot.Team);
            inputs[slot] = _chatBubbles.Update(
                world,
                slot,
                player,
                controlledSlot.Team,
                controller,
                input,
                _controlledTeamsBySlot);
        }

        return inputs;
    }
}
