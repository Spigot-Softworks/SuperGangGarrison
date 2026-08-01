using OpenGarrison.Core;

namespace OpenGarrison.Core.BotBrain;

public interface IPracticeBotController
{
    bool CollectDiagnostics { get; set; }

    BotControllerDiagnosticsSnapshot LastDiagnostics { get; }

    void Reset();

    void ConfigureSpawnOverrides(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots);

    IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots);

    IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputsForSlots(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyCollection<byte> slotsToThink);

    IReadOnlyDictionary<byte, PlayerInputSnapshot> AdvanceCachedNavigationForSlots(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyCollection<byte> slotsToAdvance,
        IReadOnlyDictionary<byte, PlayerInputSnapshot> cachedInputs) =>
        new Dictionary<byte, PlayerInputSnapshot>();

    bool RequiresPerTickNavigationThink(byte slot) => false;

    bool RequiresImmediateNavigationThink(byte slot) => false;
}
