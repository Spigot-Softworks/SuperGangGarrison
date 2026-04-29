using OpenGarrison.BotAI;
using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Policies;
using System.Runtime.InteropServices;

namespace OpenGarrison.MLBot;

public sealed class MLPracticeBotController : IPracticeBotController
{
    private readonly IMLBotPolicyRuntime _policyRuntime;
    private readonly Dictionary<byte, MLBotObservationRuntimeState> _runtimeStates = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _inputs = new();

    public MLPracticeBotController()
        : this(MLBotPolicyRuntimeFactory.CreateDefault())
    {
    }

    public MLPracticeBotController(IMLBotPolicyRuntime policyRuntime)
    {
        _policyRuntime = policyRuntime;
    }

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics { get; private set; } = BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _runtimeStates.Clear();
        _inputs.Clear();
        LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _inputs.Clear();

        foreach (var entry in controlledSlots)
        {
            if (!world.TryGetNetworkPlayer(entry.Key, out var player))
            {
                continue;
            }

            ref var runtimeState = ref CollectionsMarshal.GetValueRefOrAddDefault(_runtimeStates, entry.Key, out _);
            runtimeState ??= new MLBotObservationRuntimeState();

            var taskPhase = MLBotTaskStateResolver.Resolve(world, player);
            var observation = MLBotObservationBuilder.Build(
                world,
                entry.Key,
                player,
                taskPhase,
                runtimeState);

            MLBotObservationRuntimeStateTracker.Update(runtimeState, observation, player);
            var action = _policyRuntime.Evaluate(observation);
            MLBotObservationRuntimeStateTracker.RecordAction(runtimeState, action);
            _inputs[entry.Key] = MLBotActionDecoder.Decode(action);
        }

        return _inputs;
    }
}
