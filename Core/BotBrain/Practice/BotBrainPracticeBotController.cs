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

    public BotBrainPracticeBotRuntimeSnapshot RuntimeSnapshot
    {
        get
        {
            var activeControllerCount = 0;
            var navigationLoadedCount = 0;
            var navigationMissingCount = 0;
            var objectiveTapeLoadedCount = 0;
            var activePathCount = 0;
            foreach (var slot in _configuredSlots.Keys)
            {
                if (!_controllersBySlot.TryGetValue(slot, out var controller))
                {
                    continue;
                }

                activeControllerCount += 1;
                if (controller.HasNavigationGraph)
                {
                    navigationLoadedCount += 1;
                }
                else
                {
                    navigationMissingCount += 1;
                }

                if (controller.HasObjectiveTapeAsset)
                {
                    objectiveTapeLoadedCount += 1;
                }

                if (controller.HasActivePath)
                {
                    activePathCount += 1;
                }
            }

            return new BotBrainPracticeBotRuntimeSnapshot(
                ActiveControllerCount: activeControllerCount,
                NavigationLoadedCount: navigationLoadedCount,
                NavigationMissingCount: navigationMissingCount,
                ObjectiveTapeLoadedCount: objectiveTapeLoadedCount,
                ActivePathCount: activePathCount);
        }
    }

    public void Reset()
    {
        foreach (var controller in _controllersBySlot.Values)
        {
            controller.Reset();
        }

        _controllersBySlot.Clear();
        _configuredSlots.Clear();
        _controlledTeamsBySlot.Clear();
        _chatBubbles.Reset();
    }

    /// <summary>
    /// Test/diagnostic affordance: look up the per-slot brain controller. Intended for
    /// xUnit harnesses and debug overlays that need to read trace strings and graph state.
    /// </summary>
    public bool TryGetBotBrainController(byte slot, out BotBrainController? controller)
    {
        return _controllersBySlot.TryGetValue(slot, out controller);
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
                controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
                _controllersBySlot[slot] = controller;
                _configuredSlots[slot] = controlledSlot;
                continue;
            }

            if (_configuredSlots.TryGetValue(slot, out var previousSlot)
                && previousSlot.Team == controlledSlot.Team
                && previousSlot.ClassId == controlledSlot.ClassId
                && previousSlot.PreferEnemyPlayerObjective == controlledSlot.PreferEnemyPlayerObjective)
            {
                continue;
            }

            _configuredSlots[slot] = controlledSlot;
            controller.Reset();
            controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
        }
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        return BuildInputsForSlots(world, controlledSlots, new List<byte>(controlledSlots.Keys));
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputsForSlots(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyCollection<byte> slotsToThink)
    {
        ConfigureSpawnOverrides(world, controlledSlots);

        var inputs = new Dictionary<byte, PlayerInputSnapshot>(slotsToThink.Count);
        _controlledTeamsBySlot.Clear();
        foreach (var (slot, controlledSlot) in controlledSlots)
        {
            _controlledTeamsBySlot[slot] = controlledSlot.Team;
        }

        foreach (var slot in slotsToThink)
        {
            if (!controlledSlots.TryGetValue(slot, out var controlledSlot))
            {
                continue;
            }

            if (!world.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            if (!_controllersBySlot.TryGetValue(slot, out var controller))
            {
                controller = new BotBrainController();
                controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
                _controllersBySlot[slot] = controller;
                _configuredSlots[slot] = controlledSlot;
            }
            else if (controller.PreferEnemyPlayerObjective != controlledSlot.PreferEnemyPlayerObjective)
            {
                controller.Reset();
                controller.PreferEnemyPlayerObjective = controlledSlot.PreferEnemyPlayerObjective;
                _configuredSlots[slot] = controlledSlot;
            }

            var input = controller.Think(player, world, controlledSlot.Team, _controlledTeamsBySlot);
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

public readonly record struct BotBrainPracticeBotRuntimeSnapshot(
    int ActiveControllerCount,
    int NavigationLoadedCount,
    int NavigationMissingCount,
    int ObjectiveTapeLoadedCount,
    int ActivePathCount);
