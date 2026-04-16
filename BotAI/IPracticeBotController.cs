using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public interface IPracticeBotController
{
    bool CollectDiagnostics { get; set; }

    BotControllerDiagnosticsSnapshot LastDiagnostics { get; }

    void Reset();

    void WarmNavigationGraphs(IReadOnlyDictionary<PlayerClass, BotNavigationAsset>? navigationAssets);

    IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyDictionary<PlayerClass, BotNavigationAsset>? navigationAssets = null);
}
