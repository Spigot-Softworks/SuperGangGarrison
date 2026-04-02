using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public interface IPracticeBotController
{
    bool CollectDiagnostics { get; set; }

    BotControllerDiagnosticsSnapshot LastDiagnostics { get; }

    void Reset();

    IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyDictionary<PlayerClass, BotNavigationAsset>? navigationAssets = null);
}
