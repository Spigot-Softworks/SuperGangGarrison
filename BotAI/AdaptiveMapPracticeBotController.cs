using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public static class PracticeBotControllerMapPolicy
{
    public static bool ShouldUseModernGraphRoute(string? levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return false;
        }

        return levelName.Equals("Conflict", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AdaptiveMapPracticeBotController : IPracticeBotController
{
    private readonly IPracticeBotController _defaultController;
    private readonly IPracticeBotController _modernController;
    private IPracticeBotController? _activeController;
    private bool _collectDiagnostics;

    public AdaptiveMapPracticeBotController(
        IPracticeBotController defaultController,
        IPracticeBotController modernController)
    {
        _defaultController = defaultController;
        _modernController = modernController;
    }

    public bool CollectDiagnostics
    {
        get => _collectDiagnostics;
        set
        {
            _collectDiagnostics = value;
            _defaultController.CollectDiagnostics = value;
            _modernController.CollectDiagnostics = value;
        }
    }

    public BotControllerDiagnosticsSnapshot LastDiagnostics =>
        _activeController?.LastDiagnostics ?? BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _defaultController.Reset();
        _modernController.Reset();
        _activeController = null;
    }

    public void ConfigureSpawnOverrides(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        var selectedController = PracticeBotControllerMapPolicy.ShouldUseModernGraphRoute(world.Level.Name)
            ? _modernController
            : _defaultController;
        selectedController.ConfigureSpawnOverrides(world, controlledSlots);
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        var selectedController = PracticeBotControllerMapPolicy.ShouldUseModernGraphRoute(world.Level.Name)
            ? _modernController
            : _defaultController;

        if (!ReferenceEquals(_activeController, selectedController))
        {
            selectedController.Reset();
            _activeController = selectedController;
        }

        selectedController.CollectDiagnostics = CollectDiagnostics;
        return selectedController.BuildInputs(world, controlledSlots);
    }
}
